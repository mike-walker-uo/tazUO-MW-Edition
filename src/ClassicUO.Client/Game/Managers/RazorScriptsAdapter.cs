using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ClassicUO.Configuration;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    internal sealed class RazorScriptsAdapter : IDisposable
    {
        internal enum ActionKind { Run, Stop, StopAll }
        internal sealed class Script
        {
            internal readonly string Id, Name, Error;
            internal readonly bool? Running;
            internal Script(string id, string name, bool? running = null, string error = null)
            { Id = id; Name = name; Running = running; Error = error; }
        }
        internal sealed class Snapshot
        {
            internal readonly bool Available;
            internal readonly string Profile, Feedback;
            internal readonly Script[] Scripts;
            internal readonly bool AnyRunning;
            internal int RunningCount => Scripts.Count(s => s.Running == true);
            internal Snapshot(bool available, string profile, Script[] scripts, string feedback, bool anyRunning = false)
            { Available = available; Profile = profile; Scripts = scripts; Feedback = feedback; AnyRunning = anyRunning; }
            internal static Snapshot Unavailable(string reason) => new Snapshot(false, "", Array.Empty<Script>(), reason);
        }
        // Test seam; all Razor discovery/reflection/dispatch stays in this adapter.
        internal interface ISession
        {
            void Post(Action callback);
            string Profile { get; }
            IEnumerable<Script> Discover();
            bool Status(string id);
            bool AllStopped();
            void Execute(ActionKind kind, string id);
        }
        private sealed class Pending
        {
            internal ActionKind Kind;
            internal string Id, Profile;
            internal DateTime Started;
        }
        private readonly Func<ISession> _connect;
        private readonly Func<DateTime> _now;
        private Snapshot _current = Snapshot.Unavailable("Razor unavailable: checking connection...");
        private Pending _pending;
        private int _inFlight;
        private long _started;
        private volatile bool _disposed;

        internal RazorScriptsAdapter(Func<ISession> connect = null, Func<DateTime> now = null)
        { _connect = connect ?? Connect; _now = now ?? (() => DateTime.UtcNow); }
        internal bool Busy => Volatile.Read(ref _inFlight) != 0 || Volatile.Read(ref _pending) != null;
        internal Snapshot Current
        {
            get
            {
                if (Volatile.Read(ref _inFlight) != 0 && _now().Ticks - Interlocked.Read(ref _started) > TimeSpan.TicksPerSecond * 5)
                {
                    Snapshot current = Volatile.Read(ref _current);
                    if (current.Available || current.Feedback != "Razor unavailable: its UI is not responding.")
                        Volatile.Write(ref _current, Snapshot.Unavailable("Razor unavailable: its UI is not responding."));
                }
                return Volatile.Read(ref _current);
            }
        }
        internal void Refresh() => Queue(null);
        internal void Act(ActionKind kind, string id, string profile)
        {
            if (!Busy && Current.Available)
                Queue(new Pending { Kind = kind, Id = id, Profile = profile, Started = _now() });
        }
        private void Queue(Pending request)
        {
            if (_disposed || Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return;
            Interlocked.Exchange(ref _started, _now().Ticks);
            if (request != null)
            {
                Snapshot previous = Current;
                Publish(new Snapshot(previous.Available, previous.Profile, previous.Scripts,
                    "Sending " + request.Kind + " to Razor...", previous.AnyRunning));
            }
            try
            {
                ISession session = _connect();
                if (session == null) throw new InvalidOperationException("Razor is not loaded.");
                session.Post(() =>
                {
                    try { if (!_disposed) Read(session, request); }
                    catch (Exception ex)
                    {
                        Volatile.Write(ref _pending, null);
                        Publish(Snapshot.Unavailable("Razor unavailable: " + Error(ex)));
                    }
                    finally { Volatile.Write(ref _inFlight, 0); }
                });
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _pending, null);
                Publish(Snapshot.Unavailable("Razor unavailable: " + Error(ex)));
                Volatile.Write(ref _inFlight, 0);
            }
        }
        private void Read(ISession session, Pending request)
        {
            string profile = session.Profile;
            if (string.IsNullOrEmpty(profile)) throw new InvalidOperationException("Razor profile is loading.");
            Script[] scripts = ReadScripts(session);
            string feedback = Current.Feedback;
            if (!Current.Available || Current.Profile != profile) feedback = "";
            Pending pending = Volatile.Read(ref _pending);
            if (pending != null && pending.Profile != profile)
            { pending = null; feedback = "Razor profile changed; pending action was not confirmed."; }
            if (request != null)
            {
                pending = null;
                if (request.Profile != profile) feedback = "Razor profile changed; action cancelled.";
                else
                {
                    Script target = scripts.FirstOrDefault(s => s.Id == request.Id);
                    if (request.Kind != ActionKind.StopAll && (target == null || target.Error != null))
                        feedback = "Action failed: script is no longer available in Razor.";
                    else
                    {
                        try
                        {
                            session.Execute(request.Kind, request.Id);
                            pending = request;
                            feedback = "Waiting for Razor to confirm " + request.Kind + "...";
                            scripts = ReadScripts(session);
                        }
                        catch (Exception ex) { feedback = "Action failed: " + Error(ex); }
                    }
                }
            }
            if (pending != null)
            {
                Script target = scripts.FirstOrDefault(s => s.Id == pending.Id);
                bool confirmed = pending.Kind == ActionKind.StopAll ? session.AllStopped()
                    : target?.Error == null && target?.Running == (pending.Kind == ActionKind.Run);
                if (confirmed)
                {
                    feedback = pending.Kind == ActionKind.StopAll ? "Stop All confirmed by Razor."
                        : (pending.Kind == ActionKind.Run ? "Running: " : "Stopped: ") + target.Name;
                    pending = null;
                }
                else if (pending.Kind != ActionKind.StopAll && target == null)
                { feedback = "Action not confirmed: script was removed from Razor."; pending = null; }
                else if ((_now() - pending.Started).TotalSeconds >= 5)
                {
                    feedback = pending.Kind == ActionKind.Run
                        ? "Run not confirmed; the script may have finished immediately."
                        : "Stop not confirmed; Razor still reports a running or unreadable script.";
                    pending = null;
                }
            }
            Volatile.Write(ref _pending, pending);
            Publish(new Snapshot(true, profile, scripts, feedback, !session.AllStopped()));
        }
        private static Script[] ReadScripts(ISession session) => session.Discover()
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .Select(s =>
            {
                try { return new Script(s.Id, s.Name, session.Status(s.Id)); }
                catch (Exception ex) { return new Script(s.Id, s.Name, null, Error(ex)); }
            })
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
        internal static IEnumerable<Script> Filter(IEnumerable<Script> scripts, string search, bool runningOnly) =>
            scripts.Where(s => (!runningOnly || s.Running == true)
                && s.Name.IndexOf(search?.Trim() ?? "", StringComparison.OrdinalIgnoreCase) >= 0);

        // Stored by the existing desktop command pin system; full paths disambiguate names.
        internal static string PinCommand(string profile, string id) => "-razorscripts "
            + (id == null ? "stopall " : "toggle ") + Encode(profile)
            + (id == null ? "" : " " + Encode(id));
        private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        internal static bool TryReadPin(string command, out string profile, out string id)
        {
            profile = id = null;
            string[] parts = (command ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0] != "-razorscripts"
                || !(parts[1] == "stopall" && parts.Length == 3 || parts[1] == "toggle" && parts.Length == 4)) return false;
            try
            {
                profile = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                if (parts.Length == 4) id = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
                return !string.IsNullOrEmpty(profile) && (parts.Length == 3 || !string.IsNullOrEmpty(id));
            }
            catch (FormatException) { return false; }
        }
        internal static string PinName(string id) => id == null ? "Stop All" : id.Substring(id.LastIndexOfAny(new[] { '/', '\\' }) + 1);
        internal bool PinState(string command, out string label, out string tooltip)
        {
            label = command; tooltip = "Invalid Razor script pin.";
            if (!TryReadPin(command, out string profile, out string id)) return false;
            Snapshot snapshot = Current;
            Script script = snapshot.Scripts.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            label = id == null ? "Razor: Stop All" : (script?.Running == true ? "Stop " : "Run ") + PinName(id);
            string reason = !snapshot.Available ? snapshot.Feedback
                : snapshot.Profile != profile ? "Switch Razor to profile: " + profile
                : id != null && script == null ? "Script is not registered in Razor."
                : script?.Running == null && id != null ? script?.Error ?? "Script status is unknown."
                : Busy ? "Waiting for Razor..."
                : id == null && !snapshot.AnyRunning ? "No scripts running." : "";
            tooltip = profile + (id == null ? "\nStop all scripts, including editor scripts." : "\n" + id)
                + "\n" + (reason.Length == 0 ? snapshot.Feedback : reason);
            return reason.Length == 0;
        }
        internal void ExecutePin(string command)
        {
            if (!PinState(command, out _, out _) || !TryReadPin(command, out string profile, out string id)) return;
            Script script = Current.Scripts.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            Act(id == null ? ActionKind.StopAll : script.Running == true ? ActionKind.Stop : ActionKind.Run, id, profile);
        }
        private static string Error(Exception ex) => ex.GetBaseException().Message.Replace('\r', ' ').Replace('\n', ' ');
        private void Publish(Snapshot snapshot) { if (!_disposed) Volatile.Write(ref _current, snapshot); }
        public void Dispose() { _disposed = true; Volatile.Write(ref _pending, null); }

        private static ISession Connect()
        {
            // Reuse the plugin's exact loaded assembly; never Load/LoadFrom/ReflectionOnlyLoad.
            bool unmatchedPlugin = false;
            foreach (Plugin plugin in Plugin.Plugins)
            {
                if (!plugin.IsValid || !RazorPluginSettings.IsRazorPath(plugin.PluginPath)) continue;
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic
                    && string.Equals(a.Location, plugin.PluginPath, StringComparison.OrdinalIgnoreCase));
                if (assembly != null) return new ReflectionSession(assembly);
                unmatchedPlugin = true;
            }
            if (unmatchedPlugin) throw new NotSupportedException("the loaded Razor plugin's managed assembly could not be matched; native wrappers are unsupported.");
            return null;
        }
        private sealed class ReflectionSession : ISession
        {
            private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            private readonly Control _window;
            private readonly PropertyInfo _profile, _filename, _path, _running;
            private readonly FieldInfo[] _lists;
            private readonly object _service;
            private readonly MethodInfo _status, _run, _stop, _stopAll, _search, _all;

            internal ReflectionSession(Assembly assembly)
            {
                // Verified MW Edition 0.1 / RE 1.0.0.14:
                // UI/Other/Script.cs LoadAndInitializeScripts uses these three registration lists.
                // Misc.cs exposes actions/status; EnhancedScript.cs Search accepts full paths.
                // ClassicUO.cs RunUI owns a separate STA thread; queue onto its existing form.
                if (assembly.GetName().Version != new Version(1, 0, 0, 14))
                    throw new NotSupportedException("unsupported Razor version; expected 1.0.0.14.");
                Type misc = Require(assembly.GetType("RazorEnhanced.Misc"), "RazorEnhanced.Misc");
                Type scripts = Require(assembly.GetType("RazorEnhanced.Scripts"), "RazorEnhanced.Scripts");
                Type item = Require(assembly.GetType("RazorEnhanced.Scripts+ScriptItem"), "Scripts.ScriptItem");
                Type enhanced = Require(assembly.GetType("RazorEnhanced.EnhancedScript"), "RazorEnhanced.EnhancedScript");
                _window = Require(Require(assembly.GetType("Assistant.Engine"), "Assistant.Engine").GetProperty("MainWindow", Static), "Engine.MainWindow")
                    .GetValue(null) as Control;
                if (_window == null || _window.IsDisposed || _window.Disposing || !_window.IsHandleCreated)
                    throw new InvalidOperationException("Razor window is not ready.");
                _profile = Require(Require(assembly.GetType("RazorEnhanced.Settings"), "RazorEnhanced.Settings").GetProperty("CurrentProfile", Static), "Settings.CurrentProfile");
                _lists = new[] { "PyScripts", "UosScripts", "CsScripts" }.Select(n => Require(scripts.GetField(n, Static), "Scripts." + n)).ToArray();
                _filename = Require(item.GetProperty("Filename"), "ScriptItem.Filename");
                _path = Require(item.GetProperty("FullPath"), "ScriptItem.FullPath");
                _running = Require(enhanced.GetProperty("IsRunning", Instance), "EnhancedScript.IsRunning");
                _service = Require(enhanced.GetField("Service", Static), "EnhancedScript.Service").GetValue(null)
                    ?? throw new InvalidOperationException("Razor script service is not ready.");
                _search = Require(_service.GetType().GetMethod("Search", Instance, null, new[] { typeof(string), typeof(bool) }, null), "Service.Search(string, bool)");
                _all = Require(_service.GetType().GetMethod("ScriptList", Instance, null, Type.EmptyTypes, null), "Service.ScriptList()");
                _status = Require(misc.GetMethod("ScriptStatus", new[] { typeof(string) }), "Misc.ScriptStatus(string)");
                _run = Require(misc.GetMethod("ScriptRun", new[] { typeof(string) }), "Misc.ScriptRun(string)");
                _stop = Require(misc.GetMethod("ScriptStop", new[] { typeof(string) }), "Misc.ScriptStop(string)");
                _stopAll = Require(misc.GetMethod("ScriptStopAll", new[] { typeof(bool) }), "Misc.ScriptStopAll(bool)");
            }
            private static T Require<T>(T member, string name = "script list") where T : class => member
                ?? throw new NotSupportedException("unsupported Razor script interface; missing " + name + ".");
            public void Post(Action callback) => _window.BeginInvoke(callback);
            public string Profile => (string)_profile.GetValue(null);
            public IEnumerable<Script> Discover()
            {
                foreach (FieldInfo list in _lists)
                    foreach (object item in (IEnumerable)Require(list.GetValue(null)))
                    {
                        string path = (string)_path.GetValue(item), name = (string)_filename.GetValue(item);
                        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
                            throw new InvalidOperationException("Razor returned an incomplete script registration.");
                        yield return new Script(path, name);
                    }
            }
            public bool Status(string id)
            {
                // Avoid Misc's chat error for a missing registration (e.g. a deleted script file).
                if (_search.Invoke(_service, new object[] { id, false }) == null)
                    throw new InvalidOperationException("Script is unavailable in Razor.");
                return (bool)_status.Invoke(null, new object[] { id });
            }
            public bool AllStopped() => ((IEnumerable)_all.Invoke(_service, null)).Cast<object>()
                .All(s => !(bool)_running.GetValue(s));
            public void Execute(ActionKind kind, string id)
            {
                if (kind == ActionKind.StopAll) _stopAll.Invoke(null, new object[] { false });
                else
                {
                    if (_search.Invoke(_service, new object[] { id, false }) == null)
                        throw new InvalidOperationException("Script is no longer registered in Razor.");
                    (kind == ActionKind.Run ? _run : _stop).Invoke(null, new object[] { id });
                }
            }
        }
    }
}

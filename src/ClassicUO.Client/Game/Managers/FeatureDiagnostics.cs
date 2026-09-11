#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Fault boundary and health registry for optional TazUO features.
    /// A repeatedly failing feature is isolated instead of taking down the
    /// game loop. Failures remain inspectable through the diagnostics command.
    /// </summary>
    public static class FeatureDiagnostics
    {
        public sealed class Entry
        {
            public string Name;
            public int FailureCount;
            public long FirstFailureAt;
            public long LastFailureAt;
            public string LastError;
            public bool Disabled;
        }

        private const int MAX_FAILURES = 3;
        private const long FAILURE_WINDOW_MS = 60_000;
        private static readonly object _sync = new object();
        private static readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static volatile string[] _disabledNames = Array.Empty<string>();

        public static void Guard(string name, Action action)
        {
            if (action == null || IsDisabled(name)) return;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                RecordFailure(name, ex);
            }
        }

        public static void Guard<T>(string name, T arg, Action<T> action)
        {
            if (action == null || IsDisabled(name)) return;
            try { action(arg); }
            catch (Exception ex) { RecordFailure(name, ex); }
        }

        public static void Guard<T1, T2>(string name, T1 arg1, T2 arg2, Action<T1, T2> action)
        {
            if (action == null || IsDisabled(name)) return;
            try { action(arg1, arg2); }
            catch (Exception ex) { RecordFailure(name, ex); }
        }

        public static void RecordFailure(string name, Exception ex)
        {
            if (string.IsNullOrEmpty(name)) name = "Unknown";
            long now = (long)Time.Ticks;
            bool disabledNow = false;

            lock (_sync)
            {
                if (!_entries.TryGetValue(name, out Entry entry))
                {
                    entry = new Entry { Name = name, FirstFailureAt = now };
                    _entries[name] = entry;
                }

                if (now - entry.FirstFailureAt > FAILURE_WINDOW_MS)
                {
                    entry.FailureCount = 0;
                    entry.FirstFailureAt = now;
                }

                entry.FailureCount++;
                entry.LastFailureAt = now;
                entry.LastError = ex?.Message ?? "Unknown error";

                if (!entry.Disabled && entry.FailureCount >= MAX_FAILURES)
                {
                    entry.Disabled = true;
                    RebuildDisabledNamesUnderLock();
                    disabledNow = true;
                }
            }

            Log.Error($"TazUO feature '{name}' failed: {ex}");

            if (disabledNow)
            {
                string disabledName = name;
                MainThreadQueue.EnqueueAction(() =>
                    GameActions.Print($"[TazUO] Disabled '{disabledName}' after repeated errors. Use -diagnostics reenable {disabledName}.", 0x21));
            }
        }

        public static bool IsDisabled(string name)
        {
            if (name == null) return false;

            string[] disabledNames = _disabledNames;
            for (int i = 0; i < disabledNames.Length; i++)
            {
                if (string.Equals(name, disabledNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static Entry[] Snapshot()
        {
            lock (_sync)
            {
                var result = new Entry[_entries.Count];
                int i = 0;
                foreach (Entry source in _entries.Values)
                {
                    result[i++] = new Entry
                    {
                        Name = source.Name,
                        FailureCount = source.FailureCount,
                        FirstFailureAt = source.FirstFailureAt,
                        LastFailureAt = source.LastFailureAt,
                        LastError = source.LastError,
                        Disabled = source.Disabled
                    };
                }
                return result;
            }
        }

        public static bool Reenable(string name)
        {
            lock (_sync)
            {
                if (name == null || !_entries.TryGetValue(name, out Entry entry)) return false;
                entry.Disabled = false;
                entry.FailureCount = 0;
                entry.FirstFailureAt = 0;
                RebuildDisabledNamesUnderLock();
                return true;
            }
        }

        public static void ResetSession()
        {
            lock (_sync)
            {
                _entries.Clear();
                _disabledNames = Array.Empty<string>();
            }
        }

        private static void RebuildDisabledNamesUnderLock()
        {
            int count = 0;
            foreach (Entry entry in _entries.Values)
                if (entry.Disabled) count++;

            if (count == 0)
            {
                _disabledNames = Array.Empty<string>();
                return;
            }

            var disabledNames = new string[count];
            int index = 0;
            foreach (Entry entry in _entries.Values)
                if (entry.Disabled) disabledNames[index++] = entry.Name;

            _disabledNames = disabledNames;
        }
    }
}

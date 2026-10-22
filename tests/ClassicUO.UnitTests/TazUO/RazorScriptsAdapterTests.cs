using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;
using Adapter = ClassicUO.Game.Managers.RazorScriptsAdapter;

namespace ClassicUO.UnitTests.TazUO
{
    public class RazorScriptsAdapterTests
    {
        private sealed class Session : Adapter.ISession
        {
            internal readonly Queue<Action> Work = new Queue<Action>();
            internal readonly List<Adapter.Script> Scripts = new List<Adapter.Script>();
            internal readonly Dictionary<string, bool> Running = new Dictionary<string, bool>();
            internal readonly List<string> Actions = new List<string>();
            internal bool ThrowOnAction, ExtraEditorRunning;
            public string Profile { get; set; } = "First";
            public void Post(Action callback) => Work.Enqueue(callback);
            internal void Drain() { while (Work.Count != 0) Work.Dequeue()(); }
            internal void Add(string id, string name, bool running = false)
            { Scripts.Add(new Adapter.Script(id, name)); Running[id] = running; }
            public IEnumerable<Adapter.Script> Discover() => Scripts;
            public bool Status(string id) => Running.TryGetValue(id, out bool running) ? running
                : throw new InvalidOperationException("Missing Razor registration.");
            public bool AllStopped() => !ExtraEditorRunning && Running.Values.All(r => !r);
            public void Execute(Adapter.ActionKind kind, string id)
            {
                if (ThrowOnAction) throw new InvalidOperationException("Razor rejected the action.");
                Actions.Add(kind + ":" + id);
                // Deliberately do not change status: only Razor's subsequent observation confirms an action.
            }
        }
        private static Adapter Connect(Session session)
        {
            var adapter = new Adapter(() => session);
            adapter.Refresh(); session.Drain(); return adapter;
        }

        [Fact]
        public void Discovery_uses_registered_identities_and_sorts_names_without_sorting_by_status()
        {
            var session = new Session();
            session.Add("outside\\Z.py", "Z.py", true); session.Add("other\\a.py", "a.py");
            using var adapter = Connect(session);
            Assert.True(adapter.Current.Available);
            Assert.Equal(new[] { "other\\a.py", "outside\\Z.py" }, adapter.Current.Scripts.Select(s => s.Id));
            session.Running["other\\a.py"] = true; session.Running["outside\\Z.py"] = false;
            adapter.Refresh(); session.Drain();
            Assert.Equal(new[] { "other\\a.py", "outside\\Z.py" }, adapter.Current.Scripts.Select(s => s.Id));
            Assert.True(adapter.Current.Scripts[0].Running.Value);
            Assert.False(adapter.Current.Scripts[1].Running.Value);
        }

        [Fact]
        public void Unavailable_Razor_is_not_an_empty_connected_grid()
        {
            using var adapter = new Adapter(() => null);
            adapter.Refresh();
            Assert.False(adapter.Current.Available);
            Assert.Empty(adapter.Current.Scripts);
            Assert.Contains("not loaded", adapter.Current.Feedback);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First");
            Assert.False(adapter.Busy);
        }

        [Fact]
        public void Connected_empty_grid_and_no_running_scripts_remain_distinguishable()
        {
            var session = new Session();
            using var adapter = Connect(session);
            Assert.True(adapter.Current.Available); Assert.Empty(adapter.Current.Scripts);
            session.Add("a.py", "a.py"); adapter.Refresh(); session.Drain();
            Assert.Single(adapter.Current.Scripts); Assert.Equal(0, adapter.Current.RunningCount);
        }

        [Fact]
        public void Run_is_queued_and_success_waits_for_Razor_status()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First");
            Assert.Empty(session.Actions); Assert.True(adapter.Busy);
            session.Drain();
            Assert.Contains("Waiting", adapter.Current.Feedback); Assert.True(adapter.Busy);
            session.Running["a.py"] = true; adapter.Refresh(); session.Drain();
            Assert.Equal("Running: a.py", adapter.Current.Feedback); Assert.False(adapter.Busy);
        }

        [Fact]
        public void Stop_All_waits_for_editor_scripts_as_well_as_registered_rows()
        {
            var session = new Session { ExtraEditorRunning = true }; session.Add("a.py", "a.py", true);
            using var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.StopAll, null, "First"); session.Drain();
            session.Running["a.py"] = false; adapter.Refresh(); session.Drain();
            Assert.Contains("Waiting", adapter.Current.Feedback);
            session.ExtraEditorRunning = false; adapter.Refresh(); session.Drain();
            Assert.Equal("Stop All confirmed by Razor.", adapter.Current.Feedback);
        }

        [Fact]
        public void Action_exceptions_are_inline_failures_and_do_not_claim_success()
        {
            var session = new Session { ThrowOnAction = true }; session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First"); session.Drain();
            Assert.True(adapter.Current.Available);
            Assert.Contains("Action failed: Razor rejected", adapter.Current.Feedback);
            Assert.False(adapter.Busy); Assert.Equal(0, adapter.Current.RunningCount);
        }

        [Fact]
        public void Missing_script_status_is_unknown_not_stopped_and_cannot_be_run()
        {
            var session = new Session(); session.Add("a.py", "a.py"); session.Running.Clear();
            using var adapter = Connect(session);
            Assert.Null(adapter.Current.Scripts[0].Running); Assert.NotNull(adapter.Current.Scripts[0].Error);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First"); session.Drain();
            Assert.Empty(session.Actions); Assert.Contains("Action failed", adapter.Current.Feedback);
        }

        [Fact]
        public void Profile_change_rediscovery_cancels_queued_actions_for_previous_profile()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First");
            session.Profile = "Second"; session.Scripts.Clear(); session.Add("b.py", "b.py");
            session.Drain();
            Assert.Empty(session.Actions); Assert.Equal("Second", adapter.Current.Profile);
            Assert.Equal("b.py", Assert.Single(adapter.Current.Scripts).Name);
            Assert.Contains("action cancelled", adapter.Current.Feedback);
        }

        [Fact]
        public void Duplicate_names_keep_distinct_full_path_action_targets()
        {
            var session = new Session(); session.Add("one\\a.py", "a.py"); session.Add("two\\a.py", "a.py");
            using var adapter = Connect(session);
            Assert.Equal(2, adapter.Current.Scripts.Length);
            adapter.Act(Adapter.ActionKind.Run, "two\\a.py", "First"); session.Drain();
            Assert.Equal("Run:two\\a.py", Assert.Single(session.Actions));
        }

        [Fact]
        public void Filters_match_names_case_insensitively_and_reflect_external_state_changes()
        {
            var session = new Session(); session.Add("1", "Attack.py"); session.Add("2", "Healing.py", true);
            using var adapter = Connect(session);
            Assert.Equal("Attack.py", Assert.Single(Adapter.Filter(adapter.Current.Scripts, " ATT ", false)).Name);
            Assert.Equal("Healing.py", Assert.Single(Adapter.Filter(adapter.Current.Scripts, "", true)).Name);
            session.Running["1"] = true; session.Running["2"] = false; adapter.Refresh(); session.Drain();
            Assert.Equal("Attack.py", Assert.Single(Adapter.Filter(adapter.Current.Scripts, "", true)).Name);
        }

        [Fact]
        public void Unconfirmed_fast_script_run_times_out_without_false_success()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            DateTime now = new DateTime(2026, 10, 19);
            using var adapter = new Adapter(() => session, () => now);
            adapter.Refresh(); session.Drain();
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First"); session.Drain();
            now = now.AddSeconds(6); adapter.Refresh(); session.Drain();
            Assert.Contains("Run not confirmed", adapter.Current.Feedback); Assert.False(adapter.Busy);
        }

        [Fact]
        public void Unresponsive_Razor_does_not_queue_more_work_or_block_the_caller()
        {
            var session = new Session(); DateTime now = new DateTime(2026, 10, 19);
            using var adapter = new Adapter(() => session, () => now);
            adapter.Refresh(); now = now.AddSeconds(6);
            Assert.False(adapter.Current.Available); Assert.Contains("not responding", adapter.Current.Feedback);
            adapter.Refresh(); Assert.Single(session.Work);
            session.Drain(); Assert.True(adapter.Current.Available);
        }

        [Fact]
        public void Closing_window_cancels_actions_that_have_not_reached_Razor()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First"); adapter.Dispose(); session.Drain();
            Assert.Empty(session.Actions);
        }

        [Fact]
        public void Stop_waits_for_a_confirmed_stopped_state()
        {
            var session = new Session(); session.Add("a.py", "a.py", true);
            using var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.Stop, "a.py", "First"); session.Drain();
            Assert.Contains("Waiting", adapter.Current.Feedback);
            session.Running["a.py"] = false; adapter.Refresh(); session.Drain();
            Assert.Equal("Stopped: a.py", adapter.Current.Feedback); Assert.False(adapter.Busy);
        }

        [Fact]
        public void Losing_Razor_clears_stale_rows_and_availability()
        {
            var session = new Session(); session.Add("a.py", "a.py", true);
            bool connected = true;
            using var adapter = new Adapter(() => connected ? session : null);
            adapter.Refresh(); session.Drain(); Assert.Equal(1, adapter.Current.RunningCount);
            connected = false; adapter.Refresh();
            Assert.False(adapter.Current.Available); Assert.Empty(adapter.Current.Scripts);
        }

        [Fact]
        public void Pending_confirmation_is_not_carried_into_a_new_profile()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            adapter.Act(Adapter.ActionKind.Run, "a.py", "First"); session.Drain();
            session.Profile = "Second"; session.Running["a.py"] = true;
            adapter.Refresh(); session.Drain();
            Assert.Contains("profile changed", adapter.Current.Feedback);
            Assert.DoesNotContain("Running:", adapter.Current.Feedback); Assert.False(adapter.Busy);
        }

        [Fact]
        public void Unsupported_interface_is_reported_instead_of_falling_back_to_an_empty_grid()
        {
            using var adapter = new Adapter(() => throw new NotSupportedException("unsupported Razor script interface"));
            adapter.Refresh();
            Assert.False(adapter.Current.Available);
            Assert.Contains("unsupported Razor script interface", adapter.Current.Feedback);
        }

        [Fact]
        public void Stop_All_remains_available_for_Razor_editor_scripts_outside_the_registered_grid()
        {
            var session = new Session { ExtraEditorRunning = true };
            using var adapter = Connect(session);
            Assert.Empty(adapter.Current.Scripts); Assert.Equal(0, adapter.Current.RunningCount);
            Assert.True(adapter.Current.AnyRunning);
        }

        [Fact]
        public void Script_pins_preserve_full_paths_and_Razor_profile_with_spaces_and_unicode()
        {
            const string profile = "My profile ä";
            const string path = "D:\\Other scripts\\heal ü.py";
            string pin = Adapter.PinCommand(profile, path);
            Assert.True(Adapter.TryReadPin(pin, out string restoredProfile, out string restoredPath));
            Assert.Equal(profile, restoredProfile); Assert.Equal(path, restoredPath);
            Assert.Equal("heal ü.py", Adapter.PinName(restoredPath));
            Assert.True(Adapter.TryReadPin(Adapter.PinCommand(profile, null), out _, out string stopAll));
            Assert.Null(stopAll);
        }

        [Theory]
        [InlineData("-razorscripts")]
        [InlineData("-razorscripts toggle invalid invalid")]
        [InlineData("-razorscripts stopall Rmlyc3Q= extra")]
        [InlineData("-other stopall Rmlyc3Q=")]
        public void Malformed_or_unrelated_pins_are_not_Razor_actions(string command) =>
            Assert.False(Adapter.TryReadPin(command, out _, out _));

        [Fact]
        public void Desktop_pin_runs_or_stops_from_observed_status_and_waits_for_confirmation()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            string pin = Adapter.PinCommand("First", "a.py");
            Assert.True(adapter.PinState(pin, out string label, out _)); Assert.Equal("Run a.py", label);
            adapter.ExecutePin(pin);
            Assert.Empty(session.Actions); session.Drain();
            Assert.False(adapter.PinState(pin, out _, out _));
            session.Running["a.py"] = true; adapter.Refresh(); session.Drain();
            Assert.True(adapter.PinState(pin, out label, out _)); Assert.Equal("Stop a.py", label);
            adapter.ExecutePin(pin); session.Drain();
            Assert.Equal(new[] { "Run:a.py", "Stop:a.py" }, session.Actions);
            Assert.Contains("Waiting", adapter.Current.Feedback);
        }

        [Fact]
        public void Desktop_pins_reject_other_profiles_unknown_status_and_missing_registrations()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            string otherProfile = Adapter.PinCommand("Other", "a.py");
            Assert.False(adapter.PinState(otherProfile, out _, out string reason));
            Assert.Contains("Switch Razor", reason); adapter.ExecutePin(otherProfile);
            string pin = Adapter.PinCommand("First", "a.py");
            session.Running.Clear(); adapter.Refresh(); session.Drain();
            Assert.False(adapter.PinState(pin, out _, out _)); adapter.ExecutePin(pin);
            session.Scripts.Clear(); adapter.Refresh(); session.Drain();
            Assert.False(adapter.PinState(pin, out _, out _)); adapter.ExecutePin(pin);
            Assert.Empty(session.Actions);
        }

        [Fact]
        public void Desktop_pin_reflects_external_changes_and_only_targets_its_exact_script()
        {
            var session = new Session(); session.Add("one\\same.py", "same.py"); session.Add("two\\same.py", "same.py");
            using var adapter = Connect(session);
            string pin = Adapter.PinCommand("First", "two\\same.py");
            session.Running["two\\same.py"] = true; adapter.Refresh(); session.Drain();
            Assert.True(adapter.PinState(pin, out string label, out _)); Assert.Equal("Stop same.py", label);
            adapter.ExecutePin(pin); session.Drain();
            Assert.Equal(new[] { "Stop:two\\same.py" }, session.Actions);
        }

        [Fact]
        public void Queued_desktop_pin_cancels_if_Razor_changes_profile_before_dispatch()
        {
            var session = new Session(); session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            string pin = Adapter.PinCommand("First", "a.py");
            adapter.ExecutePin(pin); session.Profile = "Second"; session.Drain();
            Assert.Empty(session.Actions);
            Assert.Contains("cancelled", adapter.Current.Feedback);
            Assert.False(adapter.PinState(pin, out _, out _));
        }

        [Fact]
        public void Desktop_pin_exposes_action_failure_in_its_feedback_tooltip()
        {
            var session = new Session { ThrowOnAction = true }; session.Add("a.py", "a.py");
            using var adapter = Connect(session);
            string pin = Adapter.PinCommand("First", "a.py");
            adapter.ExecutePin(pin); session.Drain();
            Assert.True(adapter.PinState(pin, out string label, out string tooltip));
            Assert.Equal("Run a.py", label);
            Assert.Contains("Action failed: Razor rejected", tooltip);
        }

        [Fact]
        public void Desktop_Stop_All_pin_handles_editor_only_activity_and_unavailable_Razor()
        {
            string pin = Adapter.PinCommand("First", null);
            using var offline = new Adapter(() => null);
            offline.Refresh(); Assert.False(offline.PinState(pin, out _, out _)); offline.ExecutePin(pin);
            var session = new Session();
            using var adapter = Connect(session);
            Assert.False(adapter.PinState(pin, out _, out _));
            session.ExtraEditorRunning = true; adapter.Refresh(); session.Drain();
            Assert.True(adapter.PinState(pin, out _, out _));
            adapter.ExecutePin(pin); session.Drain();
            Assert.Equal(new[] { "StopAll:" }, session.Actions);
            Assert.Contains("Waiting", adapter.Current.Feedback);
        }

        [Fact]
        public void Window_geometry_round_trips_in_character_profile()
        {
            var profile = new Profile { RazorScriptsPosition = new Point(220, 130), RazorScriptsSize = new Point(320, 520),
                RazorScriptsCompact = true, RazorScriptsCompactSize = new Point(148, 104) };
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(profile.RazorScriptsPosition, restored.RazorScriptsPosition);
            Assert.Equal(profile.RazorScriptsSize, restored.RazorScriptsSize);
            Assert.Equal(profile.RazorScriptsCompactSize, restored.RazorScriptsCompactSize);
            Assert.True(restored.RazorScriptsCompact);
        }
    }
}

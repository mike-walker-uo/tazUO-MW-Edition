#region license
// TazUO addition.
#endregion

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Serializes automatic gameplay actions so independent helpers do not
    /// consume, cast, equip, or replace a target in the same server window.
    /// Manual commands are intentionally not routed through this coordinator.
    /// </summary>
    public static class AutomationCoordinator
    {
        private const int MIN_ACTION_GAP_MS = 250;
        private static long _nextActionAt;
        private static string _owner;

        public static bool Enabled { get; private set; } = true;
        public static string CurrentOwner => Time.Ticks < _nextActionAt ? _owner : null;
        public static long RemainingMs => System.Math.Max(0, _nextActionAt - (long)Time.Ticks);

        public static bool TryAcquire(string owner, int holdMs, bool requiresTarget = false)
        {
            if (!Enabled || World.Player == null || !World.InGame) return false;
            if (Time.Ticks < _nextActionAt) return false;
            if (requiresTarget && !TargetIsAvailable(TargetManager.IsTargeting, TargetManager.NextAutoTarget.IsSet)) return false;

            _owner = owner ?? "automation";
            _nextActionAt = (long)Time.Ticks + System.Math.Max(MIN_ACTION_GAP_MS, holdMs);
            return true;
        }

        internal static bool TargetIsAvailable(bool manualTargetActive, bool automaticTargetQueued)
            => !manualTargetActive && !automaticTargetQueued;

        public static void SetEnabled(bool enabled, bool announce = true)
        {
            Enabled = enabled;
            if (!enabled) ResetLease();
            if (announce)
            {
                GameActions.Print($"Automation {(enabled ? "ON" : "PAUSED")}.", (ushort)(enabled ? 0x35 : 0x21));
            }
        }

        public static void ResetForProfile(bool enabled)
        {
            Enabled = enabled;
            ResetLease();
        }

        private static void ResetLease()
        {
            _nextActionAt = 0;
            _owner = null;
        }
    }
}

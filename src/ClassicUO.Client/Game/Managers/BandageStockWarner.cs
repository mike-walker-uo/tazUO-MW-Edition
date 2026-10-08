#region license
// TazUO addition.
#endregion

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Polls the player's backpack bandage count and toasts + chimes once
    /// when the count drops below <see cref="Threshold"/>. Re-arms when
    /// the count climbs back above (threshold + 10) so it doesn't spam.
    /// </summary>
    public static class BandageStockWarner
    {
        public static bool Enabled = true;
        public static int Threshold = 100;

        private const long POLL_INTERVAL_MS = 4000;
        private static long _nextPoll;
        private static bool _alerted;
        private static bool _hasSeenBandages;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (backpack == null) return;
            int count = CountBandages(backpack);
            if (count > 0) _hasSeenBandages = true;
            if (!ShouldMonitor(count, _hasSeenBandages, AutoBandageManager.Enabled,
                PetBandageManager.Enabled, ExternalBandageManager.Enabled)) return;

            if (count < Threshold && !_alerted)
            {
                _alerted = true;
                try { UI.Gumps.ToastManager.Show($"Low on bandages: {count} (threshold {Threshold})", 0x21, 4500,
                    "low-bandages", AlertCategory.Supplies, AlertSeverity.Warning); } catch { }
                if (!AlertCenterManager.IsSuppressed(AlertCategory.Supplies, "low-bandages"))
                    try { Client.Game?.Audio?.PlaySound(0x0055); } catch { }
            }
            else if (count >= Threshold + 10 && _alerted)
            {
                _alerted = false; // re-arm
            }
        }

        internal static bool ShouldMonitor(int count, bool seenBandages, bool auto, bool pets, bool external) =>
            count > 0 || seenBandages || auto || pets || external;

        private static int CountBandages(Item parent) =>
            InventoryCounts.Get(parent).Count(AutoBandageManager.BANDAGE_GRAPHIC, 0, anyHue: true, rawAmounts: true);

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            _alerted = false;
            BandageSettings.MarkDirty();
            GameActions.Print($"Bandage stock warner {(on ? "ON" : "OFF")} (threshold {Threshold}).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void SetThreshold(int n)
        {
            if (n < 1) n = 1;
            if (n > 5000) n = 5000;
            Threshold = n;
            _alerted = false;
            BandageSettings.MarkDirty();
            GameActions.Print($"Bandage stock threshold = {n}.", 0x35);
        }

        public static void ResetForProfile()
        {
            Enabled = true;
            Threshold = 100;
            _nextPoll = 0;
            _alerted = false;
            _hasSeenBandages = false;
        }
    }
}

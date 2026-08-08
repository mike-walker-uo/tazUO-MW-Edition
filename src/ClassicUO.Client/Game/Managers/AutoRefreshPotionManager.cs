#region license
// TazUO addition.
#endregion

using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// When player stamina% drops below ThresholdPct, finds the first
    /// refresh potion (graphic 0x0F0B) in pack and DoubleClicks it.
    /// 10 s drink cooldown. Off by default. `-autopot refresh on|off|<pct>`.
    /// </summary>
    public static class AutoRefreshPotionManager
    {
        public static bool Enabled;
        public static int ThresholdPct = 30;
        public const ushort REFRESH_POT_GRAPHIC = 0x0F0B;
        private const long POLL_INTERVAL_MS = 500;
        private const long DRINK_COOLDOWN_MS = 10000;
        private static long _nextPoll;
        private static long _lastDrinkAt;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame || World.Player.IsDead) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            int max = World.Player.StaminaMax;
            if (max <= 0) return;
            int pct = World.Player.Stamina * 100 / max;
            if (pct >= ThresholdPct) return;
            if (Time.Ticks - _lastDrinkAt < DRINK_COOLDOWN_MS) return;

            var bp = World.Player.FindItemByLayer(Layer.Backpack);
            if (bp == null) return;
            for (var node = bp.Items; node != null; node = node.Next)
            {
                if (!(node is GameObjects.Item it)) continue;
                if (it.Graphic != REFRESH_POT_GRAPHIC) continue;
                if (!AutomationCoordinator.TryAcquire("AutoRefreshPotion", 750)) return;
                _lastDrinkAt = (long)Time.Ticks;
                GameActions.DoubleClick(it.Serial);
                return;
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Auto refresh-pot {(on ? "ON" : "OFF")} (<{ThresholdPct}%).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void SetThreshold(int pct)
        {
            if (pct < 1) pct = 1;
            if (pct > 99) pct = 99;
            ThresholdPct = pct;
            GameActions.Print($"Auto refresh-pot threshold = {pct}%.", 0x35);
        }
    }
}

#region license
// TazUO addition.
#endregion

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Auto-bandages a fixed external target (configured via target cursor).
    /// Use case: keep a designated ally / party member topped up. Shares
    /// the BandageScheduler cooldown with Player + Pet bandage managers.
    /// `-extbandage pick|clear|on|off`.
    /// </summary>
    public static class ExternalBandageManager
    {
        public static bool Enabled = false;
        public static uint TargetSerial = 0;
        public static int ThresholdPct = 90;
        public static bool BlockOnPoisoned = false;
        public static bool BlockOnMortal = true;
        public static bool BlockOnDead = true;

        private const long POLL_INTERVAL_MS = 250;
        private static long _nextPoll;

        public static void Tick()
        {
            if (!Enabled) return;
            if (TargetSerial == 0) return;
            if (World.Player == null || !World.InGame || World.Player.IsDead) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            if (BandageScheduler.IsBusy) return;

            // Defer to higher-priority targets if they also need heal.
            if (BandageScheduler.Priority == BandageScheduler.Pref.Player
                && AutoBandageManager.Enabled
                && World.Player.HitsMax > 0)
            {
                float pp = World.Player.Hits * 100f / World.Player.HitsMax;
                if (pp < AutoBandageManager.ThresholdPercent) return;
            }
            if (BandageScheduler.Priority == BandageScheduler.Pref.Pet
                && PetBandageManager.Enabled)
            {
                foreach (var m in UI.MobileCache.Pets)
                {
                    if (m == null || m.IsDestroyed || m.IsDead) continue;
                    if (m == World.Player) continue;
                    if (!m.IsRenamable) continue;
                    var nf = m.NotorietyFlag;
                    if (nf == NotorietyFlag.Enemy || nf == NotorietyFlag.Invulnerable) continue;
                    if (m.HitsMax <= 0) continue;
                    if (m.Distance > PetBandageManager.MaxDistance) continue;
                    int pp = m.Hits * 100 / m.HitsMax;
                    if (pp < PetBandageManager.ThresholdPct) return;
                }
            }

            var target = World.Mobiles.Get(TargetSerial);
            if (target == null || target.IsDestroyed) return;
            if (BlockOnDead && target.IsDead) return;
            if (target.HitsMax <= 0) return;
            if (BlockOnPoisoned && target.IsPoisoned) return;
            if (BlockOnMortal && target.IsYellowHits) return;
            int pct = target.Hits * 100 / target.HitsMax;
            // Trigger also when poisoned even at full HP (bandages cure poison).
            bool poisonTrigger = !BlockOnPoisoned && target.IsPoisoned;
            if (pct >= ThresholdPct && !poisonTrigger) return;

            var bandage = World.Player.FindBandage();
            if (bandage == null) return;
            int holdMs = (int)System.Math.Min(int.MaxValue, System.Math.Max(0, AutoBandageManager.CycleMs));
            if (!AutomationCoordinator.TryAcquire("ExternalBandage", holdMs, requiresTarget: true)) return;

            TargetManager.SetAutoTarget(target.Serial, TargetType.Beneficial, CursorTarget.Object, 5000);
            GameActions.DoubleClick(bandage.Serial);
            BandageScheduler.MarkFired(target.Serial, AutoBandageManager.CycleMs);

            try { UI.Gumps.ToastManager.Show($"Ext bandage: {target.Name ?? "target"} ({pct}%)", 0x44, 1800); } catch { }
        }

        public static async void PickTarget()
        {
            string profilePath = ClassicUO.Configuration.ProfileManager.ProfilePath;
            GameActions.Print("External bandage: click target...", 0x35);
            try
            {
                await TargetHelper.TargetAsync();
            }
            catch (System.Exception ex)
            {
                FeatureDiagnostics.RecordFailure("ExternalBandage:PickTarget", ex);
                return;
            }
            if (!string.Equals(profilePath, ClassicUO.Configuration.ProfileManager.ProfilePath,
                    System.StringComparison.OrdinalIgnoreCase)) return;
            var lt = TargetManager.LastTargetInfo;
            if (lt == null || !lt.IsEntity) { GameActions.Print("Need a mobile.", 0x21); return; }
            TargetSerial = lt.Serial;
            BandageSettings.MarkDirty();
            var m = World.Mobiles.Get(TargetSerial);
            GameActions.Print($"External bandage target = {(m?.Name ?? "?")} (0x{TargetSerial:X8}).", 0x35);
        }

        public static void Clear()
        {
            TargetSerial = 0;
            BandageSettings.MarkDirty();
            GameActions.Print("External bandage target cleared.", 0x21);
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            BandageSettings.MarkDirty();
            GameActions.Print($"External bandage {(on ? "ON" : "OFF")} (threshold {ThresholdPct}%).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void SetThreshold(int pct)
        {
            if (pct < 5) pct = 5;
            if (pct > 99) pct = 99;
            ThresholdPct = pct;
            BandageSettings.MarkDirty();
            GameActions.Print($"External bandage threshold = {pct}%.", 0x35);
        }

        public static string TargetName()
        {
            if (TargetSerial == 0) return "(none)";
            var m = World.Mobiles.Get(TargetSerial);
            return m?.Name ?? $"0x{TargetSerial:X8}";
        }

        public static void ResetForProfile()
        {
            Enabled = false;
            TargetSerial = 0;
            ThresholdPct = 90;
            BlockOnPoisoned = false;
            BlockOnMortal = true;
            BlockOnDead = true;
            _nextPoll = 0;
        }
    }
}

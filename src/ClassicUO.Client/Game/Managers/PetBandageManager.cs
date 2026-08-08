#region license
// TazUO addition. See LastTargetHighlight.cs for upstream header.
#endregion

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Polls nearby pets' HP%; when one drops below ThresholdPct, double-
    /// clicks a bandage and auto-targets the pet. Compensates for the
    /// vanilla BandageAgent only handling self. `-petbandage on|off|<pct>`.
    /// </summary>
    public static class PetBandageManager
    {
        public const float AUTO_ENABLE_VETERINARY_SKILL = 40f;
        public static bool Enabled;
        public static bool ManualOverride { get; private set; }
        public static int ThresholdPct = 90;
        // Shard-actual bandage range — pets within 2 tiles only.
        public static int MaxDistance = 2;

        /// <summary>How to handle multiple low-HP pets.</summary>
        public enum MultiPetMode
        {
            /// <summary>Always re-pick the absolute weakest each cycle.</summary>
            AlwaysWeakest,
            /// <summary>Lock onto weakest until HP ≥ 90% then switch.</summary>
            FocusUntil90,
            /// <summary>Lock onto weakest until HP ≥ 50% then switch.</summary>
            FocusUntil50,
        }
        private static MultiPetMode _mode = MultiPetMode.AlwaysWeakest;
        public static MultiPetMode Mode
        {
            get => _mode;
            set { if (_mode != value) { _mode = value; BandageSettings.MarkDirty(); } }
        }
        // Currently focused pet (when Mode != AlwaysWeakest).
        private static uint _focusedSerial;
        private const long POLL_INTERVAL_MS = 200;
        // Cycle delay — set to shard's bandage timing.
        public static long CycleMs = 2200;
        // Skip pet bandage if the pet is in one of these states. Poisoned is
        // OFF by default so a poisoned pet at full HP still gets a bandage
        // (cures poison on most shards).
        public static bool BlockOnPoisoned = false;
        public static bool BlockOnMortal = true;
        public static bool BlockOnDead = false;

        private static long _nextPoll;
        // Skill / bandage-availability re-check throttle (5 min).
        private const long SKILL_RECHECK_MS = 300_000;
        private static long _nextSkillCheck;

        public static void Tick()
        {
            if (World.Player == null || !World.InGame) return;

            // Auto-enable based on Veterinary skill ≥ threshold. Bandage
            // availability is checked separately when an action is needed.
            if (!ManualOverride && Time.Ticks >= _nextSkillCheck)
            {
                float vet = SkillReader.Get("Veterinary");
                if (vet <= 0f)
                {
                    _nextSkillCheck = (long)Time.Ticks + 500;
                }
                else
                {
                    _nextSkillCheck = (long)Time.Ticks + SKILL_RECHECK_MS;
                    bool hasBand = World.Player.FindBandage() != null;
                    if (!hasBand && vet >= AUTO_ENABLE_VETERINARY_SKILL)
                    {
                        // Piggy-back on AutoBandage's auto-open helper.
                        AutoBandageManager.TryAutoOpenBackpackContainersPublic();
                        _nextSkillCheck = (long)Time.Ticks + 2000;
                    }
                    bool wantOn = ShouldAutoEnable(vet);
                    if (wantOn != Enabled) Enabled = wantOn;
                }
            }

            if (!Enabled) return;
            if (World.Player.IsDead) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            // Shared healer cooldown — starting a second bandage on the
            // same healer cancels the first (ServUO BandageContext keyed on
            // healer). Wait until prior bandage finishes regardless of
            // patient.
            if (BandageScheduler.IsBusy) return;

            // Preference: defer to higher-priority targets if they also need heal.
            if (BandageScheduler.Priority == BandageScheduler.Pref.Player
                && AutoBandageManager.Enabled
                && World.Player.HitsMax > 0)
            {
                float playerPct = World.Player.Hits * 100f / World.Player.HitsMax;
                if (playerPct < AutoBandageManager.ThresholdPercent) return;
            }
            if (BandageScheduler.Priority == BandageScheduler.Pref.External
                && ExternalBandageManager.Enabled
                && ExternalBandageManager.TargetSerial != 0)
            {
                var ext = World.Mobiles.Get(ExternalBandageManager.TargetSerial);
                if (ext != null && !ext.IsDestroyed && !ext.IsDead && ext.HitsMax > 0)
                {
                    int extPct = ext.Hits * 100 / ext.HitsMax;
                    if (extPct < ExternalBandageManager.ThresholdPct) return;
                }
            }

            Mobile pet = FindLowestPet(out int worstPct);
            if (pet == null) return;

            Item bandage = World.Player.FindBandage();
            if (bandage == null) return;
            int holdMs = (int)System.Math.Min(int.MaxValue, System.Math.Max(0, CycleMs));
            if (!AutomationCoordinator.TryAcquire("PetBandage", holdMs, requiresTarget: true)) return;

            // Beneficial auto-target so the bandage target cursor lands on the pet.
            TargetManager.SetAutoTarget(pet.Serial, TargetType.Beneficial, CursorTarget.Object, 5000);
            GameActions.DoubleClick(bandage.Serial);
            BandageScheduler.MarkFired(pet.Serial, CycleMs);
            if (Mode != MultiPetMode.AlwaysWeakest) _focusedSerial = pet.Serial;

            try { UI.Gumps.ToastManager.Show($"Pet bandage: {pet.Name ?? "pet"} ({worstPct}%)", 0x44, 1800); } catch { }
        }

        private static Mobile FindLowestPet(out int worstPct)
        {
            // Focus modes: if we have a focused pet still alive and below
            // its release threshold, keep it; otherwise drop focus.
            if (Mode != MultiPetMode.AlwaysWeakest && _focusedSerial != 0)
            {
                var focused = World.Mobiles.Get(_focusedSerial);
                int releaseCap = Mode == MultiPetMode.FocusUntil90 ? 90 : 50;
                if (focused != null && !focused.IsDestroyed && !focused.IsDead
                    && focused.HitsMax > 0
                    && focused.Distance <= MaxDistance
                    && (!BlockOnPoisoned || !focused.IsPoisoned)
                    && (!BlockOnMortal   || !focused.IsYellowHits))
                {
                    int fpct = focused.Hits * 100 / focused.HitsMax;
                    if (fpct < releaseCap)
                    {
                        worstPct = fpct;
                        return focused;
                    }
                }
                _focusedSerial = 0; // release focus
            }

            Mobile best = null;
            worstPct = 100;
            foreach (var m in UI.MobileCache.Pets)
            {
                if (m == null || m.IsDestroyed) continue;
                if (BlockOnDead && m.IsDead) continue;
                if (m == World.Player) continue;
                if (!m.IsRenamable) continue; // pets/tames are renamable
                if (m.NotorietyFlag == NotorietyFlag.Enemy
                 || m.NotorietyFlag == NotorietyFlag.Invulnerable) continue;
                if (m.HitsMax <= 0) continue;
                if (m.Distance > MaxDistance) continue;
                if (BlockOnPoisoned && m.IsPoisoned) continue;
                if (BlockOnMortal && m.IsYellowHits) continue;

                int pct = m.Hits * 100 / m.HitsMax;
                bool poisonTrigger = !BlockOnPoisoned && m.IsPoisoned;
                if ((pct < ThresholdPct || poisonTrigger) && pct < worstPct)
                {
                    worstPct = pct;
                    best = m;
                }
            }
            return best;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            ManualOverride = true;
            BandageSettings.MarkDirty();
            GameActions.Print(
                $"Pet bandage {(on ? "ON" : "OFF")} (threshold {ThresholdPct}%, range {MaxDistance}).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static bool ShouldAutoEnable(float veterinarySkill)
            => veterinarySkill >= AUTO_ENABLE_VETERINARY_SKILL;

        public static void SetEnabledQuiet(bool on)        { Enabled = on; }
        public static void SetManualOverride(bool v)       { ManualOverride = v; }

        public static void SetThreshold(int pct)
        {
            if (pct < 5 || pct > 99) { GameActions.Print("Threshold must be 5..99.", 0x21); return; }
            ThresholdPct = pct;
            BandageSettings.MarkDirty();
            GameActions.Print($"Pet bandage threshold = {pct}%.", 0x35);
        }

        public static void ResetForProfile()
        {
            Enabled = false;
            ManualOverride = false;
            ThresholdPct = 90;
            MaxDistance = 2;
            CycleMs = 2200;
            BlockOnPoisoned = false;
            BlockOnMortal = true;
            BlockOnDead = false;
            _mode = MultiPetMode.AlwaysWeakest;
            _focusedSerial = 0;
            _nextPoll = 0;
            _nextSkillCheck = 0;
        }
    }
}

#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Auto-applies a bandage on the player when HP drops below a threshold,
    /// throttled by a minimum interval. Disabled by default; toggled via the
    /// `-autobandage on|off|N` chat command (N = HP percent threshold).
    /// </summary>
    public static class AutoBandageManager
    {
        public const ushort BANDAGE_GRAPHIC = 0x0E21;
        public const int DEFAULT_THRESHOLD = 90;     // percent
        public const float AUTO_ENABLE_HEALING_SKILL = 40f;

        // Shard-specific bandage cycle in ms (mutable via gump).
        public static long CycleMs = 2200;
        // Skip bandage if target is in one of these states.
        public static bool BlockOnPoisoned = false;
        public static bool BlockOnMortal = true;
        public static bool BlockOnDead = true;

        // Starts OFF until skills arrive. It auto-enables when Healing >= 40,
        // unless the user manually overrides it.
        public static bool Enabled { get; private set; }
        public static bool ManualOverride { get; private set; }
        public static int ThresholdPercent { get; private set; } = DEFAULT_THRESHOLD;
        private static long _lastCheckTime;
        // Skill / bandage-availability re-check throttle (5 min).
        private const long SKILL_RECHECK_MS = 300_000;
        private static long _nextSkillCheck;

        // Sub-containers in the backpack we've already auto-opened to
        // populate their contents (e.g. First Aid Belt). Re-trying every
        // tick would spam doubleclick packets.
        private static readonly System.Collections.Generic.HashSet<uint> _autoOpenedContainers
            = new System.Collections.Generic.HashSet<uint>();
        private static long _nextAutoOpen;

        public static void Tick()
        {
            // Auto-enable based on Healing skill ≥ threshold. Bandage
            // availability is checked separately when an action is needed.
            if (!ManualOverride && World.Player != null && World.InGame
                && Time.Ticks >= _nextSkillCheck)
            {
                float healing = SkillReader.Get("Healing");
                // Skill value 0 = not yet loaded from server (skill packets
                // arrive a few seconds after login). Don't lock in the 5-min
                // window yet — retry next tick.
                if (healing <= 0f)
                {
                    _nextSkillCheck = (long)Time.Ticks + 500;
                }
                else
                {
                    _nextSkillCheck = (long)Time.Ticks + SKILL_RECHECK_MS;
                    bool hasBand = FindBandageInBackpack() != null;
                    // Skill OK but no bandage visible? Sub-containers may
                    // not have streamed their contents yet — kick the
                    // First-Aid-Belt-style auto-open.
                    if (!hasBand && healing >= AUTO_ENABLE_HEALING_SKILL)
                    {
                        TryAutoOpenBackpackContainers();
                        // Retry sooner (skills are loaded, we just need
                        // containers to populate).
                        _nextSkillCheck = (long)Time.Ticks + 2000;
                    }
                    bool wantOn = ShouldAutoEnable(healing);
                    if (wantOn != Enabled) Enabled = wantOn;
                }
            }

            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (BlockOnDead && World.Player.IsDead) return;
            if (BlockOnPoisoned && World.Player.IsPoisoned) return;
            if (BlockOnMortal && World.Player.IsYellowHits) return;
            if (Time.Ticks < _lastCheckTime) return; // 500ms throttle on the check itself
            _lastCheckTime = (long)Time.Ticks + 500;

            int hits = World.Player.Hits;
            int hitsMax = World.Player.HitsMax;
            if (hitsMax <= 0) return;

            float pct = hits * 100f / hitsMax;
            // Trigger also when poisoned (and the user hasn't blocked that
            // case) — bandages cure poison on most shards.
            bool poisonTrigger = !BlockOnPoisoned && World.Player.IsPoisoned;
            if (pct >= ThresholdPercent && !poisonTrigger) return;
            // Healer-bound cooldown: shared with PetBandageManager. Starting
            // a second bandage on the same character would cancel the first.
            if (BandageScheduler.IsBusy) return;

            // Preference: defer when a higher-priority target also needs heal.
            if (BandageScheduler.Priority == BandageScheduler.Pref.Pet
                && PetBandageManager.Enabled)
            {
                foreach (var m in UI.MobileCache.Pets)
                {
                    if (m == null || m.IsDestroyed || m.IsDead) continue;
                    if (m == World.Player) continue;
                    if (!m.IsRenamable) continue;
                    var nf = m.NotorietyFlag;
                    if (nf == Data.NotorietyFlag.Enemy || nf == Data.NotorietyFlag.Invulnerable) continue;
                    if (m.HitsMax <= 0) continue;
                    if (m.Distance > PetBandageManager.MaxDistance) continue;
                    int pp = m.Hits * 100 / m.HitsMax;
                    if (pp < PetBandageManager.ThresholdPct) return;
                }
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

            Item bandage = FindBandageInBackpack();
            if (bandage == null) return;
            int holdMs = (int)System.Math.Min(int.MaxValue, System.Math.Max(0, CycleMs));
            if (!AutomationCoordinator.TryAcquire("AutoBandage", holdMs, requiresTarget: true)) return;

            // Use bandage on self: double-click + auto-target self.
            TargetManager.SetAutoTarget(World.Player.Serial, TargetType.Beneficial, CursorTarget.Object, 5000);
            GameActions.DoubleClick(bandage.Serial);

            BandageScheduler.MarkFired(World.Player.Serial, CycleMs);
            // Mirror to the floater so the cooldown timer there shows the same value.
            UI.Gumps.PlayerInfoFloater.LastBandageUsedAt = (long)Time.Ticks;
        }

        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            ManualOverride = true; // any -autobandage cmd disables skill-based auto-toggle
            BandageSettings.MarkDirty();
            GameActions.Print(
                $"Auto-bandage {(enabled ? "ON" : "OFF")} (threshold {ThresholdPercent}%).",
                (ushort)(enabled ? 0x35 : 0x21));
        }

        public static bool ShouldAutoEnable(float healingSkill)
            => healingSkill >= AUTO_ENABLE_HEALING_SKILL;

        public static void SetEnabledQuiet(bool enabled) { Enabled = enabled; }
        public static void SetManualOverride(bool v)     { ManualOverride = v; }
        public static void SetThresholdQuiet(int pct)
        {
            if (pct < 1) pct = 1;
            if (pct > 99) pct = 99;
            ThresholdPercent = pct;
        }

        public static void SetThreshold(int pct)
        {
            SetThresholdQuiet(pct);
            BandageSettings.MarkDirty();
            GameActions.Print($"Auto-bandage threshold set to {ThresholdPercent}%.", 0x35);
        }

        public static void ResetForProfile()
        {
            Enabled = false;
            ManualOverride = false;
            ThresholdPercent = DEFAULT_THRESHOLD;
            CycleMs = 2200;
            BlockOnPoisoned = false;
            BlockOnMortal = true;
            BlockOnDead = true;
            _lastCheckTime = 0;
            _nextSkillCheck = 0;
            _nextAutoOpen = 0;
            _autoOpenedContainers.Clear();
        }

        public static void TryAutoOpenBackpackContainersPublic() => TryAutoOpenBackpackContainers();

        // Whitelist of container graphics we auto-open to populate their
        // contents on startup. Currently just the First Aid Belt — extend
        // if other bandage holders are added later.
        public const ushort FIRST_AID_BELT_GRAPHIC = 0xA1F6;
        private const string FIRST_AID_BELT_NAME = "First Aid Belt";

        // Walk top-level sub-containers in the backpack and double-click
        // ONLY whitelisted bandage holders (e.g. First Aid Belt). Anything
        // else — spellbooks, runebooks, regular bags — is left untouched.
        private static void TryAutoOpenBackpackContainers()
        {
            if (Time.Ticks < _nextAutoOpen) return;
            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (backpack == null) return;
            for (LinkedObject i = backpack.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                if (it.Graphic != FIRST_AID_BELT_GRAPHIC) continue;
                if (_autoOpenedContainers.Contains(it.Serial)) continue;

                // Name is best-effort — if OPL hasn't arrived yet, name may
                // be empty. Trust the graphic match alone; require name to
                // match only when it IS populated. This avoids the chicken-
                // and-egg of needing to open the container to fetch its name.
                if (!string.IsNullOrEmpty(it.Name)
                    && it.Name.IndexOf(FIRST_AID_BELT_NAME, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!AutomationCoordinator.TryAcquire("AutoOpenFirstAidBelt", 400)) return;
                _autoOpenedContainers.Add(it.Serial);
                _nextAutoOpen = (long)Time.Ticks + 400;
                GameActions.DoubleClick(it.Serial);
                return;
            }
        }

        private static Item FindBandageInBackpack()
        {
            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (backpack == null) return null;
            return FindRecursive(backpack);
        }

        private static Item FindRecursive(Item parent)
        {
            for (LinkedObject i = parent.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                if (it.Graphic == BANDAGE_GRAPHIC && it.Amount > 0 && it.Exists) return it;
                if (it.ItemData.IsContainer && !it.IsEmpty)
                {
                    var nested = FindRecursive(it);
                    if (nested != null) return nested;
                }
            }
            return null;
        }
    }
}

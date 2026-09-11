#region license
// TazUO addition. Shared 20 Hz snapshot of world entities for overlays/managers
// that scan all mobiles every frame — iterating a List is cheaper than the
// dictionary's ValueCollection enumerator, and one snapshot serves ~12 callers.
#endregion

using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;

namespace ClassicUO.Game.UI
{
    public static class MobileCache
    {
        // Rebuilt at 20 Hz. Consumers must still check IsDestroyed — an
        // entity can be destroyed between snapshots.
        public static readonly List<Mobile> All = new List<Mobile>(64);
        public static readonly List<Mobile> Hostiles = new List<Mobile>(32);
        public static readonly List<Mobile> Pets = new List<Mobile>(16);
        public static readonly List<Item> GroundItems = new List<Item>(128);

        public static bool IsNeeded => NeedsAllMobiles || NeedsHostiles || NeedsPets || NeedsGroundItems;

        private static bool NeedsAllMobiles =>
            GhostFadeOverlay.Enabled
            || AmbienceOverlay.Enabled
            || NotorietyDotOverlay.Enabled
            || MobBloodOverlay.Enabled
            || SpellAbilityEffectSettings.CustomEffectsEnabled
                && (EffectsBundle.Death || EffectsBundle.SpeedLines
                    || EffectsBundle.Knockback || EffectsBundle.StatusAura);

        private static bool NeedsHostiles =>
            HostileEdgeHighlight.Enabled
            || OffscreenEnemyArrow.Enabled
            || NearestHostileLine.Enabled
            || CombatMobHpBars.Range > 0
            || TargetingYouAura.Enabled;

        private static bool NeedsPets =>
            AutoBandageManager.Enabled
            || PetBandageManager.Enabled
            || ExternalBandageManager.Enabled
            || PetHpBarsOverlay.Enabled;

        private static bool NeedsGroundItems =>
            AmbienceOverlay.Enabled
            || GroundLootFinder.Range > 0
            || HideTrashOverlay.Range > 0
            || CorpseFadeOverlay.Enabled
            || SpellAbilityEffectSettings.CustomEffectsEnabled && EffectsBundle.LootPillar;

        public static void Rebuild()
        {
            bool needAll = NeedsAllMobiles;
            bool needHostiles = NeedsHostiles;
            bool needPets = NeedsPets;
            bool needGroundItems = NeedsGroundItems;

            All.Clear();
            Hostiles.Clear();
            Pets.Clear();
            GroundItems.Clear();

            if (needAll || needHostiles || needPets)
            {
                foreach (var m in World.Mobiles.Values)
                {
                    if (m != null && !m.IsDestroyed)
                    {
                        if (needAll) All.Add(m);
                        if (m != World.Player)
                        {
                            var notoriety = m.NotorietyFlag;
                            if (needHostiles && !m.IsDead
                                && notoriety != NotorietyFlag.Innocent
                                && notoriety != NotorietyFlag.Invulnerable
                                && notoriety != NotorietyFlag.Ally)
                            {
                                Hostiles.Add(m);
                            }

                            if (needPets && ShouldCachePet(false, m.IsDead, m.IsRenamable, notoriety))
                            {
                                Pets.Add(m);
                            }
                        }
                    }
                }
            }

            if (needGroundItems)
            {
                foreach (var item in World.Items.Values)
                {
                    if (item != null && !item.IsDestroyed && item.OnGround)
                    {
                        GroundItems.Add(item);
                    }
                }
            }
        }

        internal static bool ShouldCachePet(bool isPlayer, bool isDead, bool isRenamable, NotorietyFlag notoriety)
        {
            // Dead bonded pets stay eligible so Veterinary can resurrect them.
            // PetBandageManager.BlockOnDead remains the user-facing opt-out.
            return !isPlayer && isRenamable &&
                   notoriety != NotorietyFlag.Enemy &&
                   notoriety != NotorietyFlag.Invulnerable;
        }

        public static void Clear()
        {
            All.Clear();
            Hostiles.Clear();
            Pets.Clear();
            GroundItems.Clear();
        }
    }
}

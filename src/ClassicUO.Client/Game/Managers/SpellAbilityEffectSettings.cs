#region license
// TazUO addition. Per-profile controls and local previews for custom combat art.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI;

namespace ClassicUO.Game.Managers
{
    internal enum SpellAbilityEffectId : byte
    {
        MagicArrow,
        Fireball,
        EnergyBolt,
        NetherBolt,
        EagleStrike,
        Bombard,
        DeathRay,
        WordOfDeath,
        Explosion,
        MeteorSwarm,
        Thunderstorm,
        Wildfire,
        NetherCyclone,
        HailStorm,
        NetherBlast,
        Lightning,
        Harm,
        LowerAttack,
        LowerDefense,
        ManaDrain,
        StaminaDrain,
        BattleLust,
        HitFireArea,
        HitColdArea,
        HitPoisonArea,
        HitEnergyArea,
        Onslaught,
        ConsecrateWeapon,
        DivineFury,
        SacredJourney,
        EnemyOfOne,
        CleanseByFire,
        CloseWounds,
        RemoveCurse,
        Confidence,
        Evasion,
        CounterAttack,
        LightningStrike,
        MomentumStrike
    }

    internal sealed class SpellAbilityEffectEntry
    {
        internal SpellAbilityEffectEntry(
            SpellAbilityEffectId id,
            string category,
            string name)
        {
            Id = id;
            Category = category;
            Name = name;
        }

        internal SpellAbilityEffectId Id { get; }
        internal string Category { get; }
        internal string Name { get; }
    }

    internal static class SpellAbilityEffectSettings
    {
        private const string FILENAME = "spell_ability_effects.tsv";
        private const int PREVIEW_TARGET_Z_LIFT = 8;

        internal static readonly SpellAbilityEffectEntry[] Entries =
        {
            E(SpellAbilityEffectId.MagicArrow, "PROJECTILES", "Magic Arrow"),
            E(SpellAbilityEffectId.Fireball, "PROJECTILES", "Fireball"),
            E(SpellAbilityEffectId.EnergyBolt, "PROJECTILES", "Energy Bolt"),
            E(SpellAbilityEffectId.NetherBolt, "PROJECTILES", "Nether Bolt"),
            E(SpellAbilityEffectId.EagleStrike, "PROJECTILES", "Eagle Strike"),
            E(SpellAbilityEffectId.Bombard, "PROJECTILES", "Bombard"),
            E(SpellAbilityEffectId.DeathRay, "PROJECTILES", "Death Ray"),
            E(SpellAbilityEffectId.WordOfDeath, "IMPACTS", "Word of Death"),
            E(SpellAbilityEffectId.Explosion, "IMPACTS", "Explosion"),
            E(SpellAbilityEffectId.MeteorSwarm, "IMPACTS", "Meteor Swarm"),
            E(SpellAbilityEffectId.Lightning, "IMPACTS", "Lightning / Hit Lightning"),
            E(SpellAbilityEffectId.Thunderstorm, "AREA SPELLS", "Thunderstorm"),
            E(SpellAbilityEffectId.Wildfire, "AREA SPELLS", "Wildfire"),
            E(SpellAbilityEffectId.NetherCyclone, "AREA SPELLS", "Nether Cyclone"),
            E(SpellAbilityEffectId.HailStorm, "AREA SPELLS", "Hail Storm"),
            E(SpellAbilityEffectId.NetherBlast, "AREA SPELLS", "Nether Blast Vortex"),
            E(SpellAbilityEffectId.Harm, "COMBAT PROCS", "Harm"),
            E(SpellAbilityEffectId.LowerAttack, "COMBAT PROCS", "Hit Lower Attack"),
            E(SpellAbilityEffectId.LowerDefense, "COMBAT PROCS", "Hit Lower Defense"),
            E(SpellAbilityEffectId.ManaDrain, "COMBAT PROCS", "Hit Mana Drain"),
            E(SpellAbilityEffectId.StaminaDrain, "COMBAT PROCS", "Hit Stamina Drain"),
            E(SpellAbilityEffectId.BattleLust, "COMBAT PROCS", "Battle Lust"),
            E(SpellAbilityEffectId.HitFireArea, "WEAPON AREAS", "Hit Fire Area"),
            E(SpellAbilityEffectId.HitColdArea, "WEAPON AREAS", "Hit Cold Area"),
            E(SpellAbilityEffectId.HitPoisonArea, "WEAPON AREAS", "Hit Poison Area"),
            E(SpellAbilityEffectId.HitEnergyArea, "WEAPON AREAS", "Hit Energy Area"),
            E(SpellAbilityEffectId.Onslaught, "OVERHEAD: CHIVALRY", "Onslaught"),
            E(SpellAbilityEffectId.ConsecrateWeapon, "OVERHEAD: CHIVALRY", "Consecrate Weapon"),
            E(SpellAbilityEffectId.DivineFury, "OVERHEAD: CHIVALRY", "Divine Fury"),
            E(SpellAbilityEffectId.SacredJourney, "OVERHEAD: CHIVALRY", "Sacred Journey"),
            E(SpellAbilityEffectId.EnemyOfOne, "OVERHEAD: CHIVALRY", "Enemy of One"),
            E(SpellAbilityEffectId.CleanseByFire, "OVERHEAD: CHIVALRY", "Cleanse by Fire"),
            E(SpellAbilityEffectId.CloseWounds, "OVERHEAD: CHIVALRY", "Close Wounds"),
            E(SpellAbilityEffectId.RemoveCurse, "OVERHEAD: CHIVALRY", "Remove Curse"),
            E(SpellAbilityEffectId.Confidence, "OVERHEAD: BUSHIDO", "Confidence"),
            E(SpellAbilityEffectId.Evasion, "OVERHEAD: BUSHIDO", "Evasion"),
            E(SpellAbilityEffectId.CounterAttack, "OVERHEAD: BUSHIDO", "Counter Attack"),
            E(SpellAbilityEffectId.LightningStrike, "OVERHEAD: BUSHIDO", "Lightning Strike hit"),
            E(SpellAbilityEffectId.MomentumStrike, "OVERHEAD: BUSHIDO", "Momentum Strike hit")
        };

        private static readonly HashSet<SpellAbilityEffectId> _disabled =
            new HashSet<SpellAbilityEffectId>();
        private static bool _loaded;

        internal static bool VisualSilenceEnabled =>
            CUOEnviroment.SafeGraphicsMode || ProfileManager.CurrentProfile?.VisualSilence == true;

        internal static bool ClassicEffectsOnlyEnabled =>
            CUOEnviroment.SafeGraphicsMode || ProfileManager.CurrentProfile?.ClassicEffectsOnly == true;

        internal static bool CustomEffectsEnabled =>
            !VisualSilenceEnabled && !ClassicEffectsOnlyEnabled;

        internal static bool OriginalEffectsEnabled => !VisualSilenceEnabled;

        internal static bool IsEnabled(SpellAbilityEffectId id)
        {
            EnsureLoaded();
            return CustomEffectsEnabled && !_disabled.Contains(id);
        }

        internal static bool IsConfiguredEnabled(SpellAbilityEffectId id)
        {
            EnsureLoaded();
            return !_disabled.Contains(id);
        }

        internal static void SetVisualSilence(bool enabled)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null)
            {
                return;
            }

            profile.VisualSilence = enabled;

            if (enabled)
            {
                World.ClearVisualEffects();
                ClearCustomEffectState();
            }
        }

        internal static void SetClassicEffectsOnly(bool enabled)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null)
            {
                return;
            }

            profile.ClassicEffectsOnly = enabled;

            if (enabled)
            {
                World.ClearVisualEffects();
                ClearCustomEffectState();
            }
        }

        private static void ClearCustomEffectState()
        {
            EnhancedSpellVisualTrigger.ResetSession();
            OnslaughtDeliveryEffect.ResetSession();
            ConsecrateWeaponEffect.ResetSession();
            DivineFuryEffect.ResetSession();
            SacredJourneyEffect.ResetSession();
            AbilityOverheadEffect.ResetSession();
            EffectsBundle.ResetSession();
            HealReceivedPulse.ResetSession();
            MobBloodOverlay.ResetSession();
        }

        internal static void SetEnabled(SpellAbilityEffectId id, bool enabled)
        {
            EnsureLoaded();

            if (enabled)
            {
                _disabled.Remove(id);
            }
            else
            {
                _disabled.Add(id);
            }

            Save();
        }

        internal static void SetAll(bool enabled)
        {
            EnsureLoaded();
            _disabled.Clear();

            if (!enabled)
            {
                foreach (SpellAbilityEffectEntry entry in Entries)
                {
                    _disabled.Add(entry.Id);
                }
            }

            Save();
        }

        internal static void ResetForProfile()
        {
            _loaded = false;
            _disabled.Clear();
        }

        internal static EnhancedSpellVisualKind Filter(EnhancedSpellVisualKind kind)
        {
            SpellAbilityEffectId id;
            return TryMap(kind, out id) && !IsEnabled(id)
                ? EnhancedSpellVisualKind.None
                : kind;
        }

        internal static CombatVisualKind Filter(CombatVisualKind kind)
        {
            SpellAbilityEffectId id;
            return TryMap(kind, out id) && !IsEnabled(id)
                ? CombatVisualKind.None
                : kind;
        }

        internal static HitAreaElement Filter(HitAreaElement element)
        {
            SpellAbilityEffectId id;
            return TryMap(element, out id) && !IsEnabled(id)
                ? HitAreaElement.None
                : element;
        }

        internal static bool IsEnabled(AbilityOverheadKind kind)
        {
            SpellAbilityEffectId id;
            return !TryMap(kind, out id) || IsEnabled(id);
        }

        internal static bool Preview(SpellAbilityEffectId id)
        {
            if (
                !CustomEffectsEnabled
                || !World.InGame
                || World.Player == null
            )
            {
                return false;
            }

            EnhancedSpellVisualKind spellKind;
            CombatVisualKind combatKind;
            HitAreaElement areaElement;
            AbilityOverheadKind overheadKind;
            GetPreviewTarget(out ushort targetX, out ushort targetY, out sbyte targetZ);

            if (id == SpellAbilityEffectId.DeathRay)
            {
                World.SpawnDeathRayPreview(targetX, targetY, targetZ);
                return true;
            }

            if (TryMap(id, out spellKind))
            {
                World.SpawnEnhancedSpellVisualPreview(
                    spellKind,
                    targetX,
                    targetY,
                    targetZ
                );
                return true;
            }

            if (TryMap(id, out combatKind))
            {
                World.SpawnCombatVisualPreview(
                    combatKind,
                    targetX,
                    targetY,
                    targetZ
                );
                return true;
            }

            if (TryMap(id, out areaElement))
            {
                World.SpawnHitAreaVisual(
                    targetX,
                    targetY,
                    targetZ,
                    areaElement
                );
                return true;
            }

            if (TryMap(id, out overheadKind))
            {
                AbilityOverheadEffect.Preview(overheadKind);
                return true;
            }

            switch (id)
            {
                case SpellAbilityEffectId.NetherBlast:
                    NetherBlastVortexManager.Preview(
                        targetX,
                        targetY,
                        targetZ
                    );
                    return true;
                case SpellAbilityEffectId.Lightning:
                    World.SpawnLightningPreview(targetX, targetY, targetZ);
                    return true;
                case SpellAbilityEffectId.Onslaught:
                    OnslaughtDeliveryEffect.Preview();
                    return true;
                case SpellAbilityEffectId.ConsecrateWeapon:
                    ConsecrateWeaponEffect.Preview();
                    return true;
                case SpellAbilityEffectId.DivineFury:
                    DivineFuryEffect.Preview();
                    return true;
                case SpellAbilityEffectId.SacredJourney:
                    SacredJourneyEffect.Preview();
                    return true;
                default:
                    return false;
            }
        }

        private static void GetPreviewTarget(
            out ushort targetX,
            out ushort targetY,
            out sbyte targetZ)
        {
            int dx = 0;
            int dy = 0;

            switch (World.Player.Direction & Direction.Mask)
            {
                case Direction.North: dy = -1; break;
                case Direction.Right: dx = 1; dy = -1; break;
                case Direction.East: dx = 1; break;
                case Direction.Down: dx = 1; dy = 1; break;
                case Direction.South: dy = 1; break;
                case Direction.Left: dx = -1; dy = 1; break;
                case Direction.West: dx = -1; break;
                case Direction.Up: dx = -1; dy = -1; break;
            }

            targetX = (ushort)Math.Max(
                ushort.MinValue,
                Math.Min(ushort.MaxValue, World.Player.X + dx * 5)
            );
            targetY = (ushort)Math.Max(
                ushort.MinValue,
                Math.Min(ushort.MaxValue, World.Player.Y + dy * 5)
            );
            targetZ = GetRaisedPreviewZ(World.Player.Z);
        }

        internal static sbyte GetRaisedPreviewZ(sbyte groundZ)
        {
            return (sbyte)Math.Min(
                sbyte.MaxValue,
                groundZ + PREVIEW_TARGET_Z_LIFT
            );
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _disabled.Clear();

            foreach (string line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                SpellAbilityEffectId id;

                if (Enum.TryParse(line?.Trim(), true, out id))
                {
                    _disabled.Add(id);
                }
            }
        }

        private static void Save()
        {
            var lines = new List<string>(_disabled.Count);

            foreach (SpellAbilityEffectEntry entry in Entries)
            {
                if (_disabled.Contains(entry.Id))
                {
                    lines.Add(entry.Id.ToString());
                }
            }

            ProfileDataStore.WriteAllLines(FILENAME, lines);
        }

        private static SpellAbilityEffectEntry E(
            SpellAbilityEffectId id,
            string category,
            string name)
        {
            return new SpellAbilityEffectEntry(id, category, name);
        }

        private static bool TryMap(
            EnhancedSpellVisualKind kind,
            out SpellAbilityEffectId id)
        {
            switch (kind)
            {
                case EnhancedSpellVisualKind.EnergyBolt: id = SpellAbilityEffectId.EnergyBolt; return true;
                case EnhancedSpellVisualKind.Thunderstorm: id = SpellAbilityEffectId.Thunderstorm; return true;
                case EnhancedSpellVisualKind.Wildfire: id = SpellAbilityEffectId.Wildfire; return true;
                case EnhancedSpellVisualKind.NetherCyclone: id = SpellAbilityEffectId.NetherCyclone; return true;
                case EnhancedSpellVisualKind.Bombard: id = SpellAbilityEffectId.Bombard; return true;
                case EnhancedSpellVisualKind.DeathRay: id = SpellAbilityEffectId.DeathRay; return true;
                case EnhancedSpellVisualKind.WordOfDeath: id = SpellAbilityEffectId.WordOfDeath; return true;
                case EnhancedSpellVisualKind.Explosion: id = SpellAbilityEffectId.Explosion; return true;
                case EnhancedSpellVisualKind.MeteorSwarm: id = SpellAbilityEffectId.MeteorSwarm; return true;
                case EnhancedSpellVisualKind.NetherBolt: id = SpellAbilityEffectId.NetherBolt; return true;
                case EnhancedSpellVisualKind.EagleStrike: id = SpellAbilityEffectId.EagleStrike; return true;
                case EnhancedSpellVisualKind.HailStorm: id = SpellAbilityEffectId.HailStorm; return true;
                default: id = default(SpellAbilityEffectId); return false;
            }
        }

        private static bool TryMap(
            SpellAbilityEffectId id,
            out EnhancedSpellVisualKind kind)
        {
            foreach (EnhancedSpellVisualKind candidate in
                (EnhancedSpellVisualKind[])Enum.GetValues(typeof(EnhancedSpellVisualKind)))
            {
                SpellAbilityEffectId mapped;
                if (TryMap(candidate, out mapped) && mapped == id)
                {
                    kind = candidate;
                    return true;
                }
            }

            kind = EnhancedSpellVisualKind.None;
            return false;
        }

        private static bool TryMap(CombatVisualKind kind, out SpellAbilityEffectId id)
        {
            switch (kind)
            {
                case CombatVisualKind.MagicArrow: id = SpellAbilityEffectId.MagicArrow; return true;
                case CombatVisualKind.Fireball: id = SpellAbilityEffectId.Fireball; return true;
                case CombatVisualKind.Harm: id = SpellAbilityEffectId.Harm; return true;
                case CombatVisualKind.LowerAttack: id = SpellAbilityEffectId.LowerAttack; return true;
                case CombatVisualKind.LowerDefense: id = SpellAbilityEffectId.LowerDefense; return true;
                case CombatVisualKind.ManaDrain: id = SpellAbilityEffectId.ManaDrain; return true;
                case CombatVisualKind.StaminaDrain: id = SpellAbilityEffectId.StaminaDrain; return true;
                case CombatVisualKind.BattleLust: id = SpellAbilityEffectId.BattleLust; return true;
                default: id = default(SpellAbilityEffectId); return false;
            }
        }

        private static bool TryMap(SpellAbilityEffectId id, out CombatVisualKind kind)
        {
            foreach (CombatVisualKind candidate in
                (CombatVisualKind[])Enum.GetValues(typeof(CombatVisualKind)))
            {
                SpellAbilityEffectId mapped;
                if (TryMap(candidate, out mapped) && mapped == id)
                {
                    kind = candidate;
                    return true;
                }
            }

            kind = CombatVisualKind.None;
            return false;
        }

        private static bool TryMap(HitAreaElement element, out SpellAbilityEffectId id)
        {
            switch (element)
            {
                case HitAreaElement.Fire: id = SpellAbilityEffectId.HitFireArea; return true;
                case HitAreaElement.Cold: id = SpellAbilityEffectId.HitColdArea; return true;
                case HitAreaElement.Poison: id = SpellAbilityEffectId.HitPoisonArea; return true;
                case HitAreaElement.Energy: id = SpellAbilityEffectId.HitEnergyArea; return true;
                default: id = default(SpellAbilityEffectId); return false;
            }
        }

        private static bool TryMap(SpellAbilityEffectId id, out HitAreaElement element)
        {
            foreach (HitAreaElement candidate in
                (HitAreaElement[])Enum.GetValues(typeof(HitAreaElement)))
            {
                SpellAbilityEffectId mapped;
                if (TryMap(candidate, out mapped) && mapped == id)
                {
                    element = candidate;
                    return true;
                }
            }

            element = HitAreaElement.None;
            return false;
        }

        private static bool TryMap(AbilityOverheadKind kind, out SpellAbilityEffectId id)
        {
            switch (kind)
            {
                case AbilityOverheadKind.EnemyOfOne: id = SpellAbilityEffectId.EnemyOfOne; return true;
                case AbilityOverheadKind.CleanseByFire: id = SpellAbilityEffectId.CleanseByFire; return true;
                case AbilityOverheadKind.CloseWounds: id = SpellAbilityEffectId.CloseWounds; return true;
                case AbilityOverheadKind.RemoveCurse: id = SpellAbilityEffectId.RemoveCurse; return true;
                case AbilityOverheadKind.Confidence: id = SpellAbilityEffectId.Confidence; return true;
                case AbilityOverheadKind.Evasion: id = SpellAbilityEffectId.Evasion; return true;
                case AbilityOverheadKind.CounterAttack: id = SpellAbilityEffectId.CounterAttack; return true;
                case AbilityOverheadKind.LightningStrikeHit: id = SpellAbilityEffectId.LightningStrike; return true;
                case AbilityOverheadKind.MomentumStrikeHit: id = SpellAbilityEffectId.MomentumStrike; return true;
                default: id = default(SpellAbilityEffectId); return false;
            }
        }

        private static bool TryMap(SpellAbilityEffectId id, out AbilityOverheadKind kind)
        {
            foreach (AbilityOverheadKind candidate in
                (AbilityOverheadKind[])Enum.GetValues(typeof(AbilityOverheadKind)))
            {
                SpellAbilityEffectId mapped;
                if (TryMap(candidate, out mapped) && mapped == id)
                {
                    kind = candidate;
                    return true;
                }
            }

            kind = AbilityOverheadKind.None;
            return false;
        }
    }
}

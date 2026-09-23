// TazUO addition: character-aware equipment analysis and loadout recommendations.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal static class EquipmentGuruManager
    {
        private const string SETTINGS_FILE = "equipment_guru_goals.tsv";
        private const string RULES_FILE = "equipment_guru_rules.tsv";
        private const int MAX_CANDIDATES_PER_LAYER = 48;
        private const int CORE_CANDIDATES_PER_LAYER = 16;
        private const int MAX_BEAM_STATES = 140;
        private const double CHANGE_PENALTY = 0.015;
        private const double OVERFLOW_PENALTY = 0.0001;
        private const double SCORE_EPSILON = 0.0001;

        private static readonly Layer[] _replaceableLayers =
        {
            Layer.Shoes, Layer.Pants, Layer.Shirt, Layer.Helmet, Layer.Gloves,
            Layer.Ring, Layer.Necklace, Layer.Waist, Layer.Torso, Layer.Bracelet,
            Layer.Tunic, Layer.Earrings, Layer.Arms, Layer.Cloak, Layer.Robe,
            Layer.Skirt, Layer.Legs
        };

        private static readonly string[] _candidateStatKeys =
        {
            "Strength", "Dexterity", "Intelligence", "HitPoints", "Stamina", "Mana",
            "HitChanceIncrease", "DefenseChanceIncrease", "DamageIncrease",
            "SwingSpeedIncrease", "LowerManaCost", "LowerReagentCost",
            "FasterCasting", "FasterCastRecovery", "SpellDamageIncrease",
            "HitPointRegeneration", "StaminaRegeneration", "ManaRegeneration",
            "PhysicalResist", "FireResist", "ColdResist", "PoisonResist",
            "EnergyResist", "Luck"
        };

        private static readonly string[] _resistKeys =
        {
            "PhysicalResist", "FireResist", "ColdResist", "PoisonResist", "EnergyResist"
        };

        private static readonly string[] _combatSkillNames =
        {
            "Anatomy", "Animal Taming", "Animal Lore", "Archery", "Bushido",
            "Chivalry", "Evaluating Intelligence", "Fencing", "Focus", "Healing",
            "Inscription", "Lumberjacking", "Mace Fighting", "Magery", "Meditation",
            "Mysticism", "Necromancy", "Ninjitsu", "Poisoning",
            "Parrying", "Resisting Spells", "Spirit Speak", "Spellweaving",
            "Swordsmanship", "Tactics", "Throwing", "Veterinary", "Wrestling"
        };

        private static readonly Dictionary<string, string[]> _skillPropertyKeys =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Swordsmanship", new[] { "swordsmanship", "swords" } },
                { "Mace Fighting", new[] { "macefighting", "macing" } },
                { "Resisting Spells", new[] { "resistingspells", "magicresistance" } },
                { "Animal Taming", new[] { "animaltaming" } },
                { "Animal Lore", new[] { "animallore" } },
                { "Spirit Speak", new[] { "spiritspeak" } },
                { "Evaluating Intelligence", new[] { "evaluatingintelligence", "evalint" } }
            };

        private static readonly Dictionary<EquipmentGuruBuild, EquipmentGuruGoals> _goals =
            new Dictionary<EquipmentGuruBuild, EquipmentGuruGoals>();
        private static string _loadedPath;
        private static uint _loadedOwner;
        private static readonly HashSet<uint> _excludedItems = new HashSet<uint>();
        private static readonly HashSet<Layer> _lockedLayers = new HashSet<Layer>();

        internal static IReadOnlyCollection<uint> ExcludedItems { get { EnsureLoaded(); return _excludedItems; } }
        internal static IReadOnlyCollection<Layer> LockedLayers { get { EnsureLoaded(); return _lockedLayers; } }
        internal static bool IsExcluded(uint serial) { EnsureLoaded(); return _excludedItems.Contains(serial); }
        internal static bool IsLayerLocked(Layer layer) { EnsureLoaded(); return _lockedLayers.Contains(layer); }
        internal static bool CanLockLayer(Layer layer) => Array.IndexOf(_replaceableLayers, layer) >= 0
            || layer == Layer.TwoHanded;

        internal static void ToggleExcluded(uint serial)
        {
            EnsureLoaded();
            if (!_excludedItems.Add(serial)) _excludedItems.Remove(serial);
            SaveRules();
        }

        internal static void ClearExclusions()
        {
            EnsureLoaded();
            _excludedItems.Clear();
            SaveRules();
        }

        internal static void ToggleLayerLock(Layer layer)
        {
            EnsureLoaded();
            if (!_lockedLayers.Add(layer)) _lockedLayers.Remove(layer);
            SaveRules();
        }

        internal enum EquipmentGuruBuild
        {
            Auto,
            Fighter,
            Archer,
            Thrower,
            MeleeTamer,
            Mage
        }

        internal enum LoadoutKind
        {
            Upgrade,
            MustTradeoff,
            Current
        }

        internal sealed class SkillSnapshot
        {
            internal string Name;
            internal float Base;
            internal float Value;
            internal float Cap;
        }

        internal sealed class EquipmentGuruGoals
        {
            internal int Strength;
            internal int Dexterity;
            internal int Intelligence;
            internal int HitPoints;
            internal int Stamina;
            internal int Mana;
            internal int HitChanceIncrease;
            internal int DefenseChanceIncrease;
            internal int DamageIncrease;
            internal int SwingSpeedIncrease;
            internal int LowerManaCost;
            internal int LowerReagentCost;
            internal int FasterCasting;
            internal int FasterCastRecovery;
            internal int SpellDamageIncrease;
            internal int HitPointRegeneration;
            internal int StaminaRegeneration;
            internal int ManaRegeneration;
            internal int Resist;
            internal int PhysicalResist = -1;
            internal int FireResist = -1;
            internal int ColdResist = -1;
            internal int PoisonResist = -1;
            internal int EnergyResist = -1;
            internal int Luck;
            internal bool PreferInsured = true;
            internal bool PreferDurability = true;
            internal Dictionary<string, int> SkillTargets =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            internal HashSet<string> MustTargets =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal HashSet<string> PreserveTargets =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            internal EquipmentGuruGoals Clone()
            {
                var clone = (EquipmentGuruGoals)MemberwiseClone();
                clone.SkillTargets = new Dictionary<string, int>(
                    SkillTargets ?? new Dictionary<string, int>(),
                    StringComparer.OrdinalIgnoreCase);
                clone.MustTargets = new HashSet<string>(
                    MustTargets ?? new HashSet<string>(),
                    StringComparer.OrdinalIgnoreCase);
                clone.PreserveTargets = new HashSet<string>(
                    PreserveTargets ?? new HashSet<string>(),
                    StringComparer.OrdinalIgnoreCase);
                return clone;
            }
        }

        internal sealed class Metric
        {
            internal string Name;
            internal int Current;
            internal int Recommended;
            internal int Target;
            internal bool LowerIsBetter;
            internal bool CountsAsTarget;
            internal bool IsMust;
            internal bool IsPreserve;
            internal int DisplayCap;
            internal int RecommendedDisplayCap;
        }

        internal sealed class RecommendedItem
        {
            internal Layer Layer;
            internal string CurrentName;
            internal ItemFinderManager.SearchResult CurrentItem;
            internal ItemFinderManager.SearchResult Item;
        }

        internal sealed class Loadout
        {
            internal readonly List<RecommendedItem> Changes = new List<RecommendedItem>();
            internal readonly List<Metric> Metrics = new List<Metric>();
            internal readonly Dictionary<Layer, uint> Items = new Dictionary<Layer, uint>();
            internal double Score;
            internal double Improvement;
            internal double QualityPreference;
            internal LoadoutKind Kind;
            internal bool MeetsRequirements;
        }

        internal sealed class Analysis
        {
            internal EquipmentGuruBuild DetectedBuild;
            internal readonly List<SkillSnapshot> UsedSkills = new List<SkillSnapshot>();
            internal string FixedItems;
            internal int CatalogItems;
            internal int ItemsWithoutProperties;
            internal readonly HashSet<uint> GearItemsWithoutProperties = new HashSet<uint>();
            internal double CurrentScore;
            internal readonly List<Loadout> Loadouts = new List<Loadout>();
            internal int UpgradeCount;
            internal int TradeoffCount;
            internal int RingCandidates;
            internal int RingsConsidered;
            internal int BraceletCandidates;
            internal int BraceletsConsidered;
            internal string Status;
        }

        private sealed class Candidate
        {
            internal Layer Layer;
            internal ItemFinderManager.SearchResult Result;
            internal StatBlock Stats;
            internal bool IsCurrent;
        }

        private sealed class Slot
        {
            internal Layer Layer;
            internal Candidate Current;
            internal readonly List<Candidate> Candidates = new List<Candidate>();
            internal readonly List<Candidate> AllCandidates = new List<Candidate>();
        }

        private sealed class BeamState
        {
            internal StatBlock Totals;
            internal readonly List<Candidate> Choices = new List<Candidate>();
            internal double Score;

            internal BeamState Copy()
            {
                var copy = new BeamState { Totals = Totals.Clone(), Score = Score };
                copy.Choices.AddRange(Choices);
                return copy;
            }
        }

        private sealed class StatBlock
        {
            internal int Strength;
            internal int Dexterity;
            internal int Intelligence;
            internal int HitPoints;
            internal int RawHitPointIncrease;
            internal int Stamina;
            internal int Mana;
            internal int Hci;
            internal int Dci;
            internal int Di;
            internal int Ssi;
            internal int Lmc;
            internal int InherentLmc;
            internal int Lrc;
            internal int Fc;
            internal int Fcr;
            internal int Sdi;
            internal int HitPointRegen;
            internal int StaminaRegen;
            internal int ManaRegen;
            internal int PhysicalResist;
            internal int FireResist;
            internal int ColdResist;
            internal int PoisonResist;
            internal int EnergyResist;
            internal int PhysicalResistCap;
            internal int FireResistCap;
            internal int ColdResistCap;
            internal int PoisonResistCap;
            internal int EnergyResistCap;
            internal int Luck;
            internal int InsuredItems;
            internal double DurabilityQuality;
            internal Dictionary<string, int> SkillBonuses =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            internal StatBlock Clone()
            {
                var clone = (StatBlock)MemberwiseClone();
                clone.SkillBonuses = new Dictionary<string, int>(
                    SkillBonuses, StringComparer.OrdinalIgnoreCase);
                return clone;
            }

            internal void Add(StatBlock value, int multiplier = 1)
            {
                int previousStrength = Strength;
                int previousHitPointBonus = RawHitPointIncrease;
                Strength += value.Strength * multiplier;
                Dexterity += value.Dexterity * multiplier;
                Intelligence += value.Intelligence * multiplier;
                RawHitPointIncrease += value.HitPoints * multiplier;
                // ServUO uses 50 + STR / 2 + up to 25 item hit points.
                HitPoints += Math.Min(25, RawHitPointIncrease)
                    - Math.Min(25, previousHitPointBonus)
                    + Strength / 2 - previousStrength / 2;
                // On this shard, each DEX point also changes maximum stamina by one.
                Stamina += (value.Stamina + value.Dexterity) * multiplier;
                Mana += value.Mana * multiplier;
                Hci += value.Hci * multiplier;
                Dci += value.Dci * multiplier;
                Di += value.Di * multiplier;
                Ssi += value.Ssi * multiplier;
                Lmc += value.Lmc * multiplier;
                InherentLmc += value.InherentLmc * multiplier;
                Lrc += value.Lrc * multiplier;
                Fc += value.Fc * multiplier;
                Fcr += value.Fcr * multiplier;
                Sdi += value.Sdi * multiplier;
                HitPointRegen += value.HitPointRegen * multiplier;
                StaminaRegen += value.StaminaRegen * multiplier;
                ManaRegen += value.ManaRegen * multiplier;
                PhysicalResist += value.PhysicalResist * multiplier;
                FireResist += value.FireResist * multiplier;
                ColdResist += value.ColdResist * multiplier;
                PoisonResist += value.PoisonResist * multiplier;
                EnergyResist += value.EnergyResist * multiplier;
                PhysicalResistCap += value.PhysicalResistCap * multiplier;
                FireResistCap += value.FireResistCap * multiplier;
                ColdResistCap += value.ColdResistCap * multiplier;
                PoisonResistCap += value.PoisonResistCap * multiplier;
                EnergyResistCap += value.EnergyResistCap * multiplier;
                Luck += value.Luck * multiplier;
                InsuredItems += value.InsuredItems * multiplier;
                DurabilityQuality += value.DurabilityQuality * multiplier;
                foreach (KeyValuePair<string, int> skill in value.SkillBonuses)
                {
                    SkillBonuses.TryGetValue(skill.Key, out int current);
                    int updated = current + skill.Value * multiplier;
                    if (updated == 0) SkillBonuses.Remove(skill.Key);
                    else SkillBonuses[skill.Key] = updated;
                }
            }

            internal int SkillBonus(string name) => SkillBonuses.TryGetValue(name, out int value)
                ? value : 0;
        }

        internal static string BuildName(EquipmentGuruBuild build)
        {
            switch (build)
            {
                case EquipmentGuruBuild.MeleeTamer: return "Melee tamer";
                default: return build.ToString();
            }
        }

        internal static EquipmentGuruGoals GetGoals(EquipmentGuruBuild build)
        {
            EnsureLoaded();
            build = build == EquipmentGuruBuild.Auto ? EquipmentGuruBuild.Fighter : build;
            EquipmentGuruGoals goals = _goals[build].Clone();
            EnsureDetectedSkillTargets(goals);
            return goals;
        }

        internal static List<SkillSnapshot> GetActiveSkills(EquipmentGuruGoals goals = null)
        {
            var skills = new List<SkillSnapshot>();
            CollectUsedSkills(skills, goals?.SkillTargets?.Keys);
            return skills;
        }

        internal static void SaveGoals(EquipmentGuruBuild build, EquipmentGuruGoals goals)
        {
            EnsureLoaded();

            if (build == EquipmentGuruBuild.Auto || goals == null)
            {
                return;
            }

            _goals[build] = goals.Clone();
            Save();
        }

        internal static EquipmentGuruGoals ResetGoals(EquipmentGuruBuild build)
        {
            EnsureLoaded();
            build = build == EquipmentGuruBuild.Auto ? EquipmentGuruBuild.Fighter : build;
            EquipmentGuruGoals goals = CreateDefaultGoals(build);
            EnsureDetectedSkillTargets(goals);
            _goals[build] = goals;
            Save();
            return goals.Clone();
        }

        internal static EquipmentGuruBuild DetectBuild()
        {
            if (World.Player == null)
            {
                return EquipmentGuruBuild.Fighter;
            }

            float archery = SkillBase("Archery");
            float throwing = SkillBase("Throwing");
            float melee = Math.Max(SkillBase("Swordsmanship"),
                Math.Max(SkillBase("Fencing"), SkillBase("Mace Fighting")));
            float magic = Math.Max(SkillBase("Magery"),
                Math.Max(SkillBase("Mysticism"),
                    Math.Max(SkillBase("Necromancy"), SkillBase("Spellweaving"))));

            if (magic >= 70 && magic > Math.Max(melee, Math.Max(archery, throwing)))
            {
                return EquipmentGuruBuild.Mage;
            }

            if (archery >= 70 && archery >= Math.Max(melee, throwing))
            {
                return EquipmentGuruBuild.Archer;
            }

            if (throwing >= 70 && throwing >= Math.Max(melee, archery))
            {
                return EquipmentGuruBuild.Thrower;
            }

            if (SkillBase("Animal Taming") >= 70 && melee >= 70)
            {
                return EquipmentGuruBuild.MeleeTamer;
            }

            return EquipmentGuruBuild.Fighter;
        }

        internal static Analysis Analyze(EquipmentGuruBuild requestedBuild,
            EquipmentGuruGoals editedGoals)
        {
            var analysis = new Analysis();

            if (World.Player == null)
            {
                analysis.Status = "Log in before analyzing equipment.";
                return analysis;
            }

            analysis.DetectedBuild = DetectBuild();
            EquipmentGuruBuild build = requestedBuild == EquipmentGuruBuild.Auto
                ? analysis.DetectedBuild
                : requestedBuild;
            EquipmentGuruGoals goals = editedGoals ?? GetGoals(build);
            EnsureDetectedSkillTargets(goals);
            CollectUsedSkills(analysis.UsedSkills, goals.SkillTargets.Keys);
            Dictionary<string, string> skillAliases = BuildSkillAliases(analysis.UsedSkills);

            if (!ItemFinderManager.TrySearch("*", null,
                out ItemFinderManager.SearchReport search, out string error))
            {
                analysis.Status = error;
                return analysis;
            }

            analysis.CatalogItems = search.Results.Count;
            analysis.ItemsWithoutProperties = search.UnloadedProperties;
            Dictionary<Layer, Item> equipped = GetEquipped();
            analysis.FixedItems = DescribeFixedItems(equipped);
            StatBlock baseline = GetPlayerTotals(skillAliases, equipped.Values, search.Results);
            List<Slot> slots = BuildSlots(search.Results, equipped, skillAliases, goals,
                build, baseline, analysis);
            analysis.CurrentScore = Score(baseline, goals, build);

            var states = new List<BeamState>
            {
                new BeamState { Totals = baseline.Clone(), Score = analysis.CurrentScore }
            };
            Dictionary<string, int>[] remainingPotential =
                BuildRemainingPotential(slots, goals);
            int processedSlots = 0;

            foreach (Slot slot in slots)
            {
                var next = new List<BeamState>();

                foreach (BeamState state in states)
                {
                    foreach (Candidate candidate in slot.Candidates)
                    {
                        BeamState expanded = state.Copy();
                        expanded.Totals.Add(slot.Current.Stats, -1);
                        expanded.Totals.Add(candidate.Stats);
                        expanded.Choices.Add(candidate);
                        expanded.Score = Score(expanded.Totals, goals, build);
                        next.Add(expanded);
                    }
                }

                int requirementCount = goals.MustTargets.Count + goals.PreserveTargets.Count;
                int beamLimit = requirementCount == 0
                    ? MAX_BEAM_STATES
                    : MAX_BEAM_STATES * 4;
                processedSlots++;
                states = next.OrderByDescending(state => PartialStateRank(state, goals,
                        baseline, remainingPotential[processedSlots]))
                    .Take(beamLimit).ToList();
            }

            states = RefineStates(states, slots, baseline, goals, build);
            states = RefineJewelryPairs(states, slots, baseline, goals, build);

            var displayedTotals = new HashSet<string>(StringComparer.Ordinal);
            var viable = new List<Loadout>();

            foreach (BeamState state in states.OrderByDescending(value =>
                StateRank(value, goals, baseline)))
            {
                if (!MeetsRequirements(state.Totals, goals, baseline))
                {
                    continue;
                }

                Loadout loadout = CreateLoadout(state, slots, baseline, goals, equipped);
                if (loadout.Changes.Count == 0)
                {
                    continue;
                }
                string displayedSignature = string.Join("|", loadout.Metrics.Select(metric =>
                    metric.Name + ":" + metric.Recommended.ToString(CultureInfo.InvariantCulture)));

                if (!displayedTotals.Add(displayedSignature))
                {
                    continue;
                }

                loadout.Improvement = state.Score - analysis.CurrentScore
                    - loadout.Changes.Count * CHANGE_PENALTY;
                loadout.Kind = loadout.Improvement > SCORE_EPSILON
                    ? LoadoutKind.Upgrade
                    : LoadoutKind.MustTradeoff;
                loadout.MeetsRequirements = true;
                viable.Add(loadout);
            }

            viable = RemoveDominated(viable)
                .OrderByDescending(loadout => loadout.Improvement)
                .ThenBy(loadout => loadout.Changes.Count)
                .ThenBy(loadout => TargetOverflow(loadout.Metrics))
                .ThenByDescending(loadout => loadout.QualityPreference)
                .ToList();
            List<Loadout> upgrades = viable.Where(loadout =>
                loadout.Kind == LoadoutKind.Upgrade).ToList();
            List<Loadout> tradeoffs = viable.Where(loadout =>
                loadout.Kind == LoadoutKind.MustTradeoff).ToList();
            analysis.UpgradeCount = upgrades.Count;
            analysis.TradeoffCount = tradeoffs.Count;

            analysis.Loadouts.AddRange(upgrades.Take(3));
            int remaining = 3 - analysis.Loadouts.Count;
            if (remaining > 0)
            {
                analysis.Loadouts.AddRange(tradeoffs.Take(remaining));
            }

            Loadout currentLoadout = CreateCurrentLoadout(
                slots, baseline, goals, equipped, analysis.CurrentScore);
            analysis.Loadouts.Add(currentLoadout);

            bool currentMeets = currentLoadout.MeetsRequirements;
            if (upgrades.Count > 0)
            {
                analysis.Status =
                    $"{upgrades.Count} upgrade(s) found. Tradeoffs are labelled separately; Current is always available for comparison.";
            }
            else if (tradeoffs.Count > 0 && !currentMeets)
            {
                analysis.Status =
                    $"No overall upgrade found. {tradeoffs.Count} loadout(s) meet every MIN/KEEP requirement but score below the current set.";
            }
            else if (currentMeets)
            {
                analysis.Status = "No better set found by this analysis for these targets.";
            }
            else
            {
                analysis.Status =
                    "No set found that meets every MIN/KEEP requirement. Lower a required value or clear the requirement.";
            }

            if (analysis.ItemsWithoutProperties > 0
                || analysis.GearItemsWithoutProperties.Count > 0)
            {
                analysis.Status += $" {analysis.ItemsWithoutProperties} catalog item(s) lack loaded properties; {analysis.GearItemsWithoutProperties.Count} potential equipment item(s) could not be compared.";
                if (analysis.GearItemsWithoutProperties.Count > 0)
                    analysis.Status += " Open or scan those items while nearby, wait for properties, then analyze again.";
            }

            string rings = IsLayerLocked(Layer.Ring)
                ? "rings locked"
                : $"{analysis.RingsConsidered}/{analysis.RingCandidates} rings";
            string bracelets = IsLayerLocked(Layer.Bracelet)
                ? "bracelets locked"
                : $"{analysis.BraceletsConsidered}/{analysis.BraceletCandidates} bracelets";
            int eligible = slots.Sum(slot => slot.AllCandidates.Count - 1);
            int shortlisted = slots.Sum(slot => slot.Candidates.Count - 1);
            analysis.Status += $" Catalog: {analysis.CatalogItems}; eligible slot alternatives: {eligible}; beam shortlist: {shortlisted}. All eligible items scored as swaps; combinations are approximate. {rings}, {bracelets}.";

            return analysis;
        }

        private static List<Slot> BuildSlots(
            List<ItemFinderManager.SearchResult> results,
            Dictionary<Layer, Item> equipped,
            Dictionary<string, string> skillAliases,
            EquipmentGuruGoals goals,
            EquipmentGuruBuild build,
            StatBlock baseline,
            Analysis analysis)
        {
            var slots = new List<Slot>();
            var wearableByLayer = new Dictionary<Layer, List<ItemFinderManager.SearchResult>>();

            foreach (ItemFinderManager.SearchResult result in results)
            {
                ref StaticTiles data = ref TileDataLoader.Instance.StaticData[result.Graphic];
                Layer itemLayer = (Layer)data.Layer;
                if (!data.IsWearable || IsExcluded(result.Serial))
                    continue;
                if (!HasLoadedProperties(result))
                {
                    if (Array.IndexOf(_replaceableLayers, itemLayer) >= 0
                        && !IsLayerLocked(itemLayer))
                        analysis.GearItemsWithoutProperties.Add(result.Serial);
                    continue;
                }
                if (!IsRaceCompatible(result, itemLayer)) continue;
                if (!wearableByLayer.TryGetValue(itemLayer, out List<ItemFinderManager.SearchResult> list))
                    wearableByLayer[itemLayer] = list = new List<ItemFinderManager.SearchResult>();
                list.Add(result);
            }

            foreach (Layer layer in _replaceableLayers)
            {
                if (IsLayerLocked(layer))
                {
                    continue;
                }

                equipped.TryGetValue(layer, out Item current);
                ItemFinderManager.SearchResult indexedCurrent = current == null
                    ? null
                    : results.FirstOrDefault(result => result.Serial == current.Serial);
                ItemFinderManager.SearchResult currentResult = current == null
                    ? null : indexedCurrent ?? CreateResult(current);
                if (current != null && !HasLoadedProperties(currentResult))
                {
                    analysis.GearItemsWithoutProperties.Add(current.Serial);
                    World.OPL.Contains(current.Serial);
                    continue;
                }
                Candidate currentCandidate = current != null
                    ? CreateCandidate(currentResult, layer, skillAliases, true)
                    : new Candidate
                    {
                        Layer = layer,
                        IsCurrent = true,
                        Stats = new StatBlock(),
                        Result = new ItemFinderManager.SearchResult
                        {
                            Name = "Empty",
                            Location = "Equipped",
                            AllProperties = string.Empty
                        }
                    };
                var slot = new Slot { Layer = layer, Current = currentCandidate };
                slot.Candidates.Add(currentCandidate);

                if (!wearableByLayer.TryGetValue(layer,
                    out List<ItemFinderManager.SearchResult> layerResults))
                    layerResults = new List<ItemFinderManager.SearchResult>();

                foreach (ItemFinderManager.SearchResult result in layerResults)
                {
                    if (current != null && result.Serial == current.Serial)
                    {
                        continue;
                    }

                    slot.Candidates.Add(CreateCandidate(result, layer, skillAliases, false));
                }

                if (layer == Layer.Ring) analysis.RingCandidates = slot.Candidates.Count - 1;
                if (layer == Layer.Bracelet) analysis.BraceletCandidates = slot.Candidates.Count - 1;

                if (slot.Candidates.Count == 1 && current == null)
                {
                    continue;
                }

                ShortlistCandidates(slot, baseline, goals, build);
                if (layer == Layer.Ring) analysis.RingsConsidered = slot.Candidates.Count - 1;
                if (layer == Layer.Bracelet) analysis.BraceletsConsidered = slot.Candidates.Count - 1;
                slots.Add(slot);
            }

            AddShieldSlot(results, equipped, skillAliases, goals, build, baseline,
                slots, analysis);
            return slots;
        }

        private static void AddShieldSlot(List<ItemFinderManager.SearchResult> results,
            Dictionary<Layer, Item> equipped, Dictionary<string, string> skillAliases,
            EquipmentGuruGoals goals, EquipmentGuruBuild build, StatBlock baseline,
            List<Slot> slots, Analysis analysis)
        {
            if (!equipped.TryGetValue(Layer.OneHanded, out Item weapon)
                || !weapon.ItemData.IsWeapon
                || !equipped.TryGetValue(Layer.TwoHanded, out Item shield)
                || !IsShield(shield.Graphic))
            {
                return;
            }

            if (IsLayerLocked(Layer.TwoHanded))
            {
                return;
            }

            ItemFinderManager.SearchResult indexedShield = results.FirstOrDefault(result =>
                result.Serial == shield.Serial);
            ItemFinderManager.SearchResult currentResult = indexedShield ?? CreateResult(shield);
            if (!HasLoadedProperties(currentResult))
            {
                analysis.GearItemsWithoutProperties.Add(shield.Serial);
                World.OPL.Contains(shield.Serial);
                return;
            }
            Candidate current = CreateCandidate(currentResult, Layer.TwoHanded,
                skillAliases, true);
            var slot = new Slot { Layer = Layer.TwoHanded, Current = current };
            slot.Candidates.Add(current);

            foreach (ItemFinderManager.SearchResult result in results)
            {
                if (result.Serial == shield.Serial || !IsShield(result.Graphic)
                    || IsExcluded(result.Serial))
                {
                    continue;
                }

                if (!HasLoadedProperties(result))
                {
                    analysis.GearItemsWithoutProperties.Add(result.Serial);
                    continue;
                }
                if (!IsRaceCompatible(result, Layer.TwoHanded)) continue;

                slot.Candidates.Add(CreateCandidate(result, Layer.TwoHanded, skillAliases, false));
            }

            ShortlistCandidates(slot, baseline, goals, build);

            slots.Add(slot);
        }

        private static void ShortlistCandidates(Slot slot, StatBlock baseline,
            EquipmentGuruGoals goals, EquipmentGuruBuild build)
        {
            slot.AllCandidates.AddRange(slot.Candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Rank = CandidatePotential(candidate, slot.Current, baseline, goals, build)
                })
                .OrderByDescending(value => value.Rank)
                .ThenBy(value => value.Candidate.Result.Serial)
                .Select(value => value.Candidate));

            var selected = new List<Candidate> { slot.Current };
            var serials = new HashSet<uint> { slot.Current.Result.Serial };

            foreach (Candidate candidate in slot.AllCandidates)
            {
                AddShortlistCandidate(selected, serials, candidate);
                if (selected.Count >= CORE_CANDIDATES_PER_LAYER) break;
            }

            foreach (string key in _candidateStatKeys)
            {
                if (IsRequiredStat(goals, key))
                    AddBestStatCandidates(slot, selected, serials, key, 2);
            }

            foreach (KeyValuePair<string, int> skill in goals.SkillTargets)
            {
                string key = "SkillTarget:" + skill.Key;
                if (skill.Value > 0 && (goals.MustTargets.Contains(key)
                    || goals.PreserveTargets.Contains(key)))
                    AddBestSkillCandidates(slot, selected, serials, skill.Key, 2);
            }

            foreach (string key in _candidateStatKeys)
            {
                if (GoalTarget(goals, key) > 0 && !IsRequiredStat(goals, key))
                    AddBestStatCandidates(slot, selected, serials, key, 1);
            }

            foreach (KeyValuePair<string, int> skill in goals.SkillTargets)
            {
                string key = "SkillTarget:" + skill.Key;
                if (skill.Value > 0 && !goals.MustTargets.Contains(key)
                    && !goals.PreserveTargets.Contains(key))
                    AddBestSkillCandidates(slot, selected, serials, skill.Key, 1);
            }

            foreach (Candidate candidate in slot.AllCandidates)
            {
                AddShortlistCandidate(selected, serials, candidate);
                if (selected.Count >= MAX_CANDIDATES_PER_LAYER) break;
            }

            slot.Candidates.Clear();
            slot.Candidates.AddRange(selected);
        }

        private static void AddShortlistCandidate(List<Candidate> selected,
            HashSet<uint> serials, Candidate candidate)
        {
            if (candidate != null && selected.Count < MAX_CANDIDATES_PER_LAYER
                && serials.Add(candidate.Result.Serial))
                selected.Add(candidate);
        }

        private static bool IsRequiredStat(EquipmentGuruGoals goals, string key)
        {
            return goals.MustTargets.Contains(key) || goals.PreserveTargets.Contains(key)
                || (key.EndsWith("Resist", StringComparison.Ordinal)
                    && (goals.MustTargets.Contains("Resist")
                        || goals.PreserveTargets.Contains("Resist")));
        }

        private static void AddBestStatCandidates(Slot slot, List<Candidate> selected,
            HashSet<uint> serials, string key, int count)
        {
            for (int i = 0; i < count && selected.Count < MAX_CANDIDATES_PER_LAYER; i++)
            {
                Candidate best = null;
                int bestValue = 0;
                foreach (Candidate candidate in slot.AllCandidates)
                {
                    if (serials.Contains(candidate.Result.Serial)) continue;
                    int value = RequirementItemGain(slot.Current.Stats, candidate.Stats, key);
                    if (value <= bestValue) continue;
                    best = candidate;
                    bestValue = value;
                }
                AddShortlistCandidate(selected, serials, best);
                if (best == null) break;
            }
        }

        private static void AddBestSkillCandidates(Slot slot, List<Candidate> selected,
            HashSet<uint> serials, string skill, int count)
        {
            int currentValue = slot.Current.Stats.SkillBonus(skill);
            for (int i = 0; i < count && selected.Count < MAX_CANDIDATES_PER_LAYER; i++)
            {
                Candidate best = null;
                int bestValue = currentValue;
                foreach (Candidate candidate in slot.AllCandidates)
                {
                    if (serials.Contains(candidate.Result.Serial)) continue;
                    int value = candidate.Stats.SkillBonus(skill);
                    if (value <= bestValue) continue;
                    best = candidate;
                    bestValue = value;
                }
                AddShortlistCandidate(selected, serials, best);
                if (best == null) break;
            }
        }

        private static double CandidatePotential(Candidate candidate, Candidate current,
            StatBlock baseline, EquipmentGuruGoals goals, EquipmentGuruBuild build)
        {
            StatBlock totals = baseline.Clone();
            totals.Add(current.Stats, -1);
            totals.Add(candidate.Stats);
            return Score(totals, goals, build)
                - RequirementDeficit(totals, goals, baseline) * 100.0
                - TargetOverflow(totals, goals) * OVERFLOW_PENALTY
                + QualityRank(totals, goals);
        }

        private static List<BeamState> RefineStates(List<BeamState> states,
            List<Slot> slots, StatBlock baseline, EquipmentGuruGoals goals,
            EquipmentGuruBuild build)
        {
            var refined = new List<BeamState>(states);
            if (slots.Count == 0) return refined;

            var current = new BeamState
            {
                Totals = baseline.Clone(),
                Score = Score(baseline, goals, build)
            };
            current.Choices.AddRange(slots.Select(slot => slot.Current));

            var seeds = states.OrderByDescending(state => StateRank(state, goals, baseline))
                .Take(3).ToList();
            seeds.Add(current);

            foreach (BeamState seed in seeds)
            {
                BeamState best = seed;
                for (int pass = 0; pass < 2; pass++)
                {
                    BeamState bestNext = null;
                    double bestRank = StateRank(best, goals, baseline);
                    int changes = best.Choices.Count(candidate => !candidate.IsCurrent);

                    for (int index = 0; index < slots.Count; index++)
                    {
                        Candidate previous = best.Choices[index];
                        StatBlock trial = best.Totals.Clone();
                        trial.Add(previous.Stats, -1);
                        Candidate slotBest = null;
                        Candidate feasibleBest = null;
                        StatBlock slotTotals = null;
                        StatBlock feasibleTotals = null;
                        double slotScore = 0;
                        double feasibleScore = 0;
                        double slotRank = double.NegativeInfinity;
                        double feasibleRank = double.NegativeInfinity;
                        int unchanged = changes - (previous.IsCurrent ? 0 : 1);

                        foreach (Candidate candidate in slots[index].AllCandidates)
                        {
                            if (ReferenceEquals(candidate, previous)) continue;
                            trial.Add(candidate.Stats);
                            double score = Score(trial, goals, build);
                            double rank = RankTotals(trial, score,
                                unchanged + (candidate.IsCurrent ? 0 : 1), goals, baseline);
                            if (rank > slotRank)
                            {
                                slotBest = candidate;
                                slotTotals = trial.Clone();
                                slotScore = score;
                                slotRank = rank;
                            }
                            if (rank > feasibleRank && MeetsRequirements(trial, goals, baseline))
                            {
                                feasibleBest = candidate;
                                feasibleTotals = trial.Clone();
                                feasibleScore = score;
                                feasibleRank = rank;
                            }
                            trial.Add(candidate.Stats, -1);
                        }

                        if (slotBest != null)
                        {
                            BeamState option = SwapChoice(best, index, slotBest,
                                slotTotals, slotScore);
                            refined.Add(option);
                            if (slotRank > bestRank + SCORE_EPSILON)
                            {
                                bestNext = option;
                                bestRank = slotRank;
                            }
                        }
                        if (feasibleBest != null && !ReferenceEquals(feasibleBest, slotBest))
                            refined.Add(SwapChoice(best, index, feasibleBest,
                                feasibleTotals, feasibleScore));
                    }

                    if (bestNext == null) break;
                    best = bestNext;
                }
            }

            return refined;
        }

        private static List<BeamState> RefineJewelryPairs(List<BeamState> states,
            List<Slot> slots, StatBlock baseline, EquipmentGuruGoals goals,
            EquipmentGuruBuild build)
        {
            int ringIndex = slots.FindIndex(slot => slot.Layer == Layer.Ring);
            int braceletIndex = slots.FindIndex(slot => slot.Layer == Layer.Bracelet);
            if (ringIndex < 0 || braceletIndex < 0
                || slots[ringIndex].Candidates.Count < 2
                || slots[braceletIndex].Candidates.Count < 2)
                return states;

            var refined = new List<BeamState>(states);
            var seeds = states.OrderByDescending(state => StateRank(state, goals, baseline))
                .Take(3).ToList();
            var current = new BeamState
            {
                Totals = baseline.Clone(),
                Score = Score(baseline, goals, build)
            };
            current.Choices.AddRange(slots.Select(slot => slot.Current));
            seeds.Add(current);

            foreach (BeamState seed in seeds)
            {
                StatBlock withoutJewelry = seed.Totals.Clone();
                withoutJewelry.Add(seed.Choices[ringIndex].Stats, -1);
                withoutJewelry.Add(seed.Choices[braceletIndex].Stats, -1);
                var pairs = new List<BeamState>();

                foreach (Candidate ring in slots[ringIndex].Candidates)
                {
                    StatBlock withRing = withoutJewelry.Clone();
                    withRing.Add(ring.Stats);
                    foreach (Candidate bracelet in slots[braceletIndex].Candidates)
                    {
                        StatBlock totals = withRing.Clone();
                        totals.Add(bracelet.Stats);
                        var pair = new BeamState
                        {
                            Totals = totals,
                            Score = Score(totals, goals, build)
                        };
                        pair.Choices.AddRange(seed.Choices);
                        pair.Choices[ringIndex] = ring;
                        pair.Choices[braceletIndex] = bracelet;
                        pairs.Add(pair);
                    }
                }

                var ranked = pairs.Select(pair => new
                {
                    State = pair,
                    Rank = StateRank(pair, goals, baseline),
                    Feasible = MeetsRequirements(pair.Totals, goals, baseline)
                }).OrderByDescending(value => value.Rank).ToList();
                refined.AddRange(ranked.Take(12).Select(value => value.State));
                refined.AddRange(ranked.Where(value => value.Feasible).Take(12)
                    .Select(value => value.State));
            }

            return refined;
        }

        private static BeamState SwapChoice(BeamState source, int index,
            Candidate candidate, StatBlock totals, double score)
        {
            var swapped = new BeamState { Totals = totals, Score = score };
            swapped.Choices.AddRange(source.Choices);
            swapped.Choices[index] = candidate;
            return swapped;
        }

        private static Candidate CreateCandidate(ItemFinderManager.SearchResult result,
            Layer layer, Dictionary<string, string> skillAliases, bool current)
        {
            return new Candidate
            {
                Layer = layer,
                Result = result,
                Stats = ParseStats(result, layer, skillAliases),
                IsCurrent = current
            };
        }

        private static bool HasLoadedProperties(ItemFinderManager.SearchResult result)
        {
            return result != null
                && !string.IsNullOrWhiteSpace(result.AllProperties)
                && !string.Equals(result.AllProperties, "No item properties loaded",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRaceCompatible(ItemFinderManager.SearchResult result, Layer layer)
        {
            if (World.Player == null || result == null)
            {
                return false;
            }

            string description = (result.Name ?? string.Empty) + "\n"
                + (result.AllProperties ?? string.Empty) + "\n"
                + TileDataLoader.Instance.StaticData[result.Graphic].Name;
            bool gargoyleOnly = ContainsRaceRestriction(description, "Gargish")
                || ContainsRaceRestriction(description, "Gargoyle Only")
                || ContainsRaceRestriction(description, "Gargoyles Only");
            bool elfOnly = ContainsRaceRestriction(description, "Elf Only")
                || ContainsRaceRestriction(description, "Elves Only");

            if (gargoyleOnly && elfOnly) return false;

            if (World.Player.Race == RaceType.GARGOYLE)
            {
                return gargoyleOnly || (!elfOnly
                    && (layer == Layer.Ring || layer == Layer.Bracelet));
            }

            return !gargoyleOnly
                && (World.Player.Race == RaceType.ELF || !elfOnly);
        }

        private static bool ContainsRaceRestriction(string text, string restriction)
        {
            return text.IndexOf(restriction, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ItemFinderManager.SearchResult CreateResult(Item item)
        {
            string name = item.Name;
            string data = string.Empty;

            if (World.OPL.TryGetNameAndData(item.Serial, out string oplName, out string oplData))
            {
                name = string.IsNullOrWhiteSpace(oplName) ? name : oplName.Trim();
                data = oplData;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = item.ItemData.Name;
            }

            var properties = new ItemPropertiesData((name ?? string.Empty) + "\n" + data);
            return new ItemFinderManager.SearchResult
            {
                Item = item,
                Serial = item.Serial,
                ContainerSerial = item.Container,
                RootContainerSerial = item.RootContainer,
                Graphic = item.Graphic,
                Hue = item.Hue,
                Amount = item.Amount,
                Name = name ?? $"Item 0x{item.Graphic:X4}",
                Location = "Equipped",
                AllProperties = FormatProperties(properties)
            };
        }

        private static StatBlock GetPlayerTotals(Dictionary<string, string> skillAliases,
            IEnumerable<Item> equipped, List<ItemFinderManager.SearchResult> catalog)
        {
            PlayerMobile player = World.Player;
            // Live values include transformation penalties but hide resist above the cap.
            var totals = new StatBlock
            {
                Strength = player.Strength,
                Dexterity = player.Dexterity,
                Intelligence = player.Intelligence,
                HitPoints = player.HitsMax,
                RawHitPointIncrease = player.HitPointsIncrease,
                Stamina = player.StaminaMax,
                Mana = player.ManaMax,
                Hci = player.HitChanceIncrease,
                Dci = player.DefenseChanceIncrease,
                Di = player.DamageIncrease,
                Ssi = player.SwingSpeedIncrease,
                Lmc = player.LowerManaCost,
                Lrc = player.LowerReagentCost,
                Fc = player.FasterCasting,
                Fcr = player.FasterCastRecovery,
                Sdi = player.SpellDamageIncrease,
                PhysicalResist = player.PhysicalResistance,
                FireResist = player.FireResistance,
                ColdResist = player.ColdResistance,
                PoisonResist = player.PoisonResistance,
                EnergyResist = player.EnergyResistance,
                PhysicalResistCap = player.MaxPhysicResistence,
                FireResistCap = player.MaxFireResistence,
                ColdResistCap = player.MaxColdResistence,
                PoisonResistCap = player.MaxPoisonResistence,
                EnergyResistCap = player.MaxEnergyResistence,
                Luck = player.Luck
            };

            int equippedFireResist = 0;
            int equippedLmc = 0;
            int equippedHitPointIncrease = 0;

            foreach (Item item in equipped)
            {
                ItemFinderManager.SearchResult result = catalog.FirstOrDefault(candidate =>
                    candidate.Serial == item.Serial) ?? CreateResult(item);
                StatBlock equipmentStats = ParseStats(result, item.Layer, skillAliases);
                equippedFireResist += equipmentStats.FireResist;
                equippedLmc += equipmentStats.Lmc;
                equippedHitPointIncrease += equipmentStats.HitPoints;
                totals.InherentLmc += equipmentStats.InherentLmc;
                foreach (KeyValuePair<string, int> skill in equipmentStats.SkillBonuses)
                {
                    totals.SkillBonuses.TryGetValue(skill.Key, out int current);
                    totals.SkillBonuses[skill.Key] = current + skill.Value;
                }
                totals.HitPointRegen += equipmentStats.HitPointRegen;
                totals.StaminaRegen += equipmentStats.StaminaRegen;
                totals.ManaRegen += equipmentStats.ManaRegen;
                totals.InsuredItems += equipmentStats.InsuredItems;
                totals.DurabilityQuality += equipmentStats.DurabilityQuality;
            }

            // Vampiric Embrace removes 25 fire resist from the equipped total.
            if (player.IsBuffIconExists(BuffIconType.VampiricEmbrace))
                totals.FireResist = Math.Max(totals.FireResist, equippedFireResist - 25);

            // The status value caps explicit LMC at 40 before adding armor LMC.
            totals.Lmc = Math.Max(equippedLmc,
                player.LowerManaCost - Math.Min(15, totals.InherentLmc));
            totals.RawHitPointIncrease = Math.Max(
                totals.RawHitPointIncrease, equippedHitPointIncrease);

            return totals;
        }

        private static StatBlock ParseStats(ItemFinderManager.SearchResult result, Layer layer,
            Dictionary<string, string> skillAliases)
        {
            var stats = new StatBlock();
            string tooltip = result?.AllProperties;

            if (string.IsNullOrWhiteSpace(tooltip))
            {
                stats.InherentLmc = InherentArmorLmc(result, layer);
                return stats;
            }

            var properties = new ItemPropertiesData("Item\n" + tooltip);
            bool mageArmor = false;

            foreach (ItemPropertiesData.SinglePropertyData property in properties.singlePropertyData)
            {
                string key = Normalize(property.Name);
                if (key == "magearmor") mageArmor = true;
                if (key == "insured")
                {
                    stats.InsuredItems++;
                }

                if (property.FirstValue == double.MinValue)
                {
                    continue;
                }

                int value = (int)Math.Round(property.FirstValue);
                if (key == "durability" && property.SecondValue > 0
                    && property.SecondValue != double.MinValue)
                {
                    stats.DurabilityQuality += Math.Max(0, Math.Min(
                        property.FirstValue / property.SecondValue, 1.0));
                }

                switch (key)
                {
                    case "strengthbonus": stats.Strength += value; break;
                    case "dexteritybonus": stats.Dexterity += value; break;
                    case "intelligencebonus": stats.Intelligence += value; break;
                    case "hitpointincrease": stats.HitPoints += value; break;
                    case "staminaincrease": stats.Stamina += value; break;
                    case "manaincrease": stats.Mana += value; break;
                    case "hitchanceincrease": stats.Hci += value; break;
                    case "defensechanceincrease": stats.Dci += value; break;
                    case "damageincrease": stats.Di += value; break;
                    case "swingspeedincrease": stats.Ssi += value; break;
                    case "lowermanacost": stats.Lmc += value; break;
                    case "lowerreagentcost": stats.Lrc += value; break;
                    case "fastercasting": stats.Fc += value; break;
                    case "fastercastrecovery": stats.Fcr += value; break;
                    case "spelldamageincrease": stats.Sdi += value; break;
                    case "hitpointregeneration": stats.HitPointRegen += value; break;
                    case "staminaregeneration": stats.StaminaRegen += value; break;
                    case "manaregeneration": stats.ManaRegen += value; break;
                    case "physicalresist": stats.PhysicalResist += value; break;
                    case "physicalresistmax":
                        stats.PhysicalResist += value;
                        if (property.SecondValue != double.MinValue)
                            stats.PhysicalResistCap += (int)Math.Round(property.SecondValue);
                        break;
                    case "fireresist": stats.FireResist += value; break;
                    case "fireresistmax":
                        stats.FireResist += value;
                        if (property.SecondValue != double.MinValue)
                            stats.FireResistCap += (int)Math.Round(property.SecondValue);
                        break;
                    case "coldresist": stats.ColdResist += value; break;
                    case "coldresistmax":
                        stats.ColdResist += value;
                        if (property.SecondValue != double.MinValue)
                            stats.ColdResistCap += (int)Math.Round(property.SecondValue);
                        break;
                    case "poisonresist": stats.PoisonResist += value; break;
                    case "poisonresistmax":
                        stats.PoisonResist += value;
                        if (property.SecondValue != double.MinValue)
                            stats.PoisonResistCap += (int)Math.Round(property.SecondValue);
                        break;
                    case "energyresist": stats.EnergyResist += value; break;
                    case "energyresistmax":
                        stats.EnergyResist += value;
                        if (property.SecondValue != double.MinValue)
                            stats.EnergyResistCap += (int)Math.Round(property.SecondValue);
                        break;
                    case "luck": stats.Luck += value; break;
                    default:
                        if (skillAliases.TryGetValue(key, out string skillName))
                        {
                            stats.SkillBonuses.TryGetValue(skillName, out int current);
                            stats.SkillBonuses[skillName] = current + value;
                        }
                        break;
                }
            }

            if (!mageArmor)
                stats.InherentLmc = InherentArmorLmc(result, layer);

            return stats;
        }

        private static int InherentArmorLmc(ItemFinderManager.SearchResult item, Layer layer)
        {
            // The client has no server ArmorMaterialType; infer it from wearable art/name.
            switch (layer)
            {
                case Layer.Pants:
                case Layer.Helmet:
                case Layer.Gloves:
                case Layer.Necklace:
                case Layer.Torso:
                case Layer.Tunic:
                case Layer.Arms:
                case Layer.Skirt:
                case Layer.Legs:
                    break;
                default:
                    return 0;
            }

            string tileName = TileDataLoader.Instance.StaticData[item.Graphic].Name;
            int bonus = ArmorMaterialLmc(tileName);
            return bonus != 0 ? bonus : ArmorMaterialLmc(item.Name);
        }

        private static int ArmorMaterialLmc(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            string material = name.ToLowerInvariant();
            if (!(material.Contains("armor") || material.Contains("chest")
                || material.Contains("arms") || material.Contains("sleeves")
                || material.Contains("gloves") || material.Contains("helm")
                || material.Contains("hatsuburi") || material.Contains("kabuto")
                || material.Contains("legs") || material.Contains("kilt")
                || material.Contains("gorget") || material.Contains("tunic")
                || material.Contains("bustier") || material.Contains("mask"))) return 0;
            if (material.Contains("studded") || material.Contains("bone")
                || material.Contains("stone")) return 3;
            if (material.Contains("ringmail") || material.Contains("ring mail")
                || material.Contains("chainmail") || material.Contains("chain mail")
                || material.Contains("plate")
                || (material.Contains("dragon") && !material.Contains("turtle"))) return 1;
            return 0;
        }

        private static double StateRank(BeamState state, EquipmentGuruGoals goals,
            StatBlock current)
        {
            int changes = state.Choices.Count(candidate => !candidate.IsCurrent);
            return RankTotals(state.Totals, state.Score, changes, goals, current);
        }

        private static Dictionary<string, int>[] BuildRemainingPotential(List<Slot> slots,
            EquipmentGuruGoals goals)
        {
            var keys = new HashSet<string>(goals.MustTargets,
                StringComparer.OrdinalIgnoreCase);
            keys.UnionWith(goals.PreserveTargets);
            if (keys.Remove("Resist"))
            {
                keys.Add("PhysicalResist");
                keys.Add("FireResist");
                keys.Add("ColdResist");
                keys.Add("PoisonResist");
                keys.Add("EnergyResist");
            }

            var remaining = new Dictionary<string, int>[slots.Count + 1];
            remaining[slots.Count] = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = slots.Count - 1; index >= 0; index--)
            {
                Slot slot = slots[index];
                var gains = new Dictionary<string, int>(
                    remaining[index + 1], StringComparer.OrdinalIgnoreCase);
                foreach (string key in keys)
                {
                    int bestGain = 0;
                    foreach (Candidate candidate in slot.Candidates)
                        bestGain = Math.Max(bestGain,
                            RequirementItemGain(slot.Current.Stats, candidate.Stats, key));
                    gains.TryGetValue(key, out int laterGain);
                    gains[key] = laterGain + bestGain;
                }
                remaining[index] = gains;
            }
            return remaining;
        }

        private static int RequirementItemValue(StatBlock stats, string key)
        {
            const string skillPrefix = "SkillTarget:";
            if (key.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase))
                return stats.SkillBonus(key.Substring(skillPrefix.Length));
            if (key.Equals("Stamina", StringComparison.OrdinalIgnoreCase))
                return stats.Stamina + stats.Dexterity;
            if (key.Equals("LowerManaCost", StringComparison.OrdinalIgnoreCase))
                return stats.Lmc + stats.InherentLmc;
            return StatValue(stats, key);
        }

        private static int RequirementItemGain(StatBlock current, StatBlock candidate,
            string key)
        {
            if (key.Equals("HitPoints", StringComparison.OrdinalIgnoreCase))
                return Math.Max(0, candidate.HitPoints - current.HitPoints)
                    + (int)Math.Ceiling((candidate.Strength - current.Strength) / 2.0);
            return RequirementItemValue(candidate, key) - RequirementItemValue(current, key);
        }

        private static double PartialStateRank(BeamState state,
            EquipmentGuruGoals goals, StatBlock current,
            Dictionary<string, int> remainingPotential)
        {
            int changes = state.Choices.Count(candidate => !candidate.IsCurrent);
            return state.Score - OptimisticRequirementDeficit(state.Totals, goals,
                    current, remainingPotential) * 100.0
                - changes * CHANGE_PENALTY
                - TargetOverflow(state.Totals, goals) * OVERFLOW_PENALTY
                + QualityRank(state.Totals, goals);
        }

        private static double OptimisticRequirementDeficit(StatBlock stats,
            EquipmentGuruGoals goals, StatBlock current,
            Dictionary<string, int> remainingPotential)
        {
            double deficit = 0;
            foreach (string key in goals.MustTargets)
            {
                int target = GoalTarget(goals, key);
                if (target <= 0) continue;
                if (key.Equals("Resist", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string resist in _resistKeys)
                        deficit += NormalizedDeficit(
                            OptimisticValue(stats, resist, remainingPotential), target);
                }
                else
                    deficit += NormalizedDeficit(
                        OptimisticValue(stats, key, remainingPotential), target);
            }

            foreach (string key in goals.PreserveTargets)
            {
                if (key.Equals("Resist", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string resist in _resistKeys)
                    {
                        int target = EffectiveStatValue(current, resist);
                        if (target > 0)
                            deficit += NormalizedDeficit(
                                OptimisticValue(stats, resist, remainingPotential), target);
                    }
                }
                else if (key.StartsWith("SkillTarget:", StringComparison.OrdinalIgnoreCase))
                {
                    string skill = key.Substring("SkillTarget:".Length);
                    double target = EffectiveSkillValue(skill, current);
                    if (target > 0)
                        deficit += NormalizedDeficit(
                            OptimisticValue(stats, key, remainingPotential), target);
                }
                else
                {
                    int target = EffectiveStatValue(current, key);
                    if (target > 0)
                        deficit += NormalizedDeficit(
                            OptimisticValue(stats, key, remainingPotential), target);
                }
            }

            return deficit;
        }

        private static double OptimisticValue(StatBlock stats, string key,
            Dictionary<string, int> remainingPotential)
        {
            remainingPotential.TryGetValue(key, out int gain);
            return key.StartsWith("SkillTarget:", StringComparison.OrdinalIgnoreCase)
                ? SkillBase(key.Substring("SkillTarget:".Length))
                    + RequirementItemValue(stats, key) + gain
                : (key.Equals("LowerManaCost", StringComparison.OrdinalIgnoreCase)
                    ? EffectiveStatValue(stats, key) : StatValue(stats, key)) + gain;
        }

        private static double RankTotals(StatBlock totals, double score, int changes,
            EquipmentGuruGoals goals, StatBlock current)
        {
            return score - RequirementDeficit(totals, goals, current) * 100.0
                - changes * CHANGE_PENALTY
                - TargetOverflow(totals, goals) * OVERFLOW_PENALTY
                + QualityRank(totals, goals);
        }

        private static double TargetOverflow(StatBlock stats, EquipmentGuruGoals goals)
        {
            double overflow = 0;
            overflow += NormalizedOverflow(stats.Strength, goals.Strength);
            overflow += NormalizedOverflow(stats.Dexterity, goals.Dexterity);
            overflow += NormalizedOverflow(stats.Intelligence, goals.Intelligence);
            overflow += NormalizedOverflow(stats.HitPoints, goals.HitPoints);
            overflow += NormalizedOverflow(stats.Stamina, goals.Stamina);
            overflow += NormalizedOverflow(stats.Mana, goals.Mana);
            overflow += NormalizedOverflow(stats.Hci, goals.HitChanceIncrease);
            overflow += NormalizedOverflow(stats.Dci, goals.DefenseChanceIncrease);
            overflow += NormalizedOverflow(stats.Di, goals.DamageIncrease);
            overflow += NormalizedOverflow(stats.Ssi, goals.SwingSpeedIncrease);
            overflow += NormalizedOverflow(
                EffectiveStatValue(stats, "LowerManaCost"), goals.LowerManaCost);
            overflow += NormalizedOverflow(stats.Lrc, goals.LowerReagentCost);
            overflow += NormalizedOverflow(stats.Fc, goals.FasterCasting);
            overflow += NormalizedOverflow(stats.Fcr, goals.FasterCastRecovery);
            overflow += NormalizedOverflow(stats.Sdi, goals.SpellDamageIncrease);
            overflow += NormalizedOverflow(stats.HitPointRegen,
                goals.HitPointRegeneration);
            overflow += NormalizedOverflow(stats.StaminaRegen,
                goals.StaminaRegeneration);
            overflow += NormalizedOverflow(stats.ManaRegen, goals.ManaRegeneration);
            overflow += NormalizedOverflow(stats.PhysicalResist,
                ResistTarget(goals.PhysicalResist, goals.Resist));
            overflow += NormalizedOverflow(stats.FireResist,
                ResistTarget(goals.FireResist, goals.Resist));
            overflow += NormalizedOverflow(stats.ColdResist,
                ResistTarget(goals.ColdResist, goals.Resist));
            overflow += NormalizedOverflow(stats.PoisonResist,
                ResistTarget(goals.PoisonResist, goals.Resist));
            overflow += NormalizedOverflow(stats.EnergyResist,
                ResistTarget(goals.EnergyResist, goals.Resist));
            overflow += NormalizedOverflow(stats.Luck, goals.Luck);

            foreach (KeyValuePair<string, int> skill in goals.SkillTargets)
            {
                overflow += NormalizedOverflow(
                    SkillBase(skill.Key) + stats.SkillBonus(skill.Key), skill.Value);
            }

            return overflow;
        }

        private static double NormalizedOverflow(double value, int target)
        {
            return target > 0 && value > target
                ? (value - target) / target
                : 0;
        }

        private static bool MeetsRequirements(StatBlock stats, EquipmentGuruGoals goals,
            StatBlock current)
        {
            return RequirementDeficit(stats, goals, current) <= SCORE_EPSILON;
        }

        private static double RequirementDeficit(StatBlock stats,
            EquipmentGuruGoals goals, StatBlock current)
        {
            double deficit = 0;

            foreach (string key in goals.MustTargets)
            {
                int target = GoalTarget(goals, key);

                if (target <= 0)
                {
                    continue;
                }

                if (key.Equals("Resist", StringComparison.OrdinalIgnoreCase))
                {
                    deficit += NormalizedDeficit(
                        EffectiveStatValue(stats, "PhysicalResist"), target);
                    deficit += NormalizedDeficit(
                        EffectiveStatValue(stats, "FireResist"), target);
                    deficit += NormalizedDeficit(
                        EffectiveStatValue(stats, "ColdResist"), target);
                    deficit += NormalizedDeficit(
                        EffectiveStatValue(stats, "PoisonResist"), target);
                    deficit += NormalizedDeficit(
                        EffectiveStatValue(stats, "EnergyResist"), target);
                    continue;
                }

                const string skillPrefix = "SkillTarget:";
                if (key.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string skill = key.Substring(skillPrefix.Length);
                    double effective = EffectiveSkillValue(skill, stats);
                    deficit += NormalizedDeficit(effective, target);
                    continue;
                }

                deficit += NormalizedDeficit(
                    EffectiveStatValue(stats, key), target);
            }

            foreach (string key in goals.PreserveTargets)
            {
                const string skillPrefix = "SkillTarget:";
                if (key.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string skill = key.Substring(skillPrefix.Length);
                    deficit += NormalizedDeficit(
                        EffectiveSkillValue(skill, stats),
                        EffectiveSkillValue(skill, current));
                    continue;
                }

                if (key.Equals("Resist", StringComparison.OrdinalIgnoreCase))
                {
                    deficit += PreserveDeficit(stats, current, "PhysicalResist");
                    deficit += PreserveDeficit(stats, current, "FireResist");
                    deficit += PreserveDeficit(stats, current, "ColdResist");
                    deficit += PreserveDeficit(stats, current, "PoisonResist");
                    deficit += PreserveDeficit(stats, current, "EnergyResist");
                    continue;
                }

                deficit += PreserveDeficit(stats, current, key);
            }

            return deficit;
        }

        private static double PreserveDeficit(StatBlock stats, StatBlock current,
            string key)
        {
            double currentValue = EffectiveStatValue(current, key);
            return currentValue <= 0
                ? 0
                : NormalizedDeficit(EffectiveStatValue(stats, key), currentValue);
        }

        private static double NormalizedDeficit(double value, double target)
        {
            return value >= target ? 0 : (target - value) / Math.Max(1.0, target);
        }

        private static int StatValue(StatBlock stats, string key)
        {
            switch (key)
            {
                case "Strength": return stats.Strength;
                case "Dexterity": return stats.Dexterity;
                case "Intelligence": return stats.Intelligence;
                case "HitPoints": return stats.HitPoints;
                case "Stamina": return stats.Stamina;
                case "Mana": return stats.Mana;
                case "HitChanceIncrease": return stats.Hci;
                case "DefenseChanceIncrease": return stats.Dci;
                case "DamageIncrease": return stats.Di;
                case "SwingSpeedIncrease": return stats.Ssi;
                case "LowerManaCost": return stats.Lmc;
                case "LowerReagentCost": return stats.Lrc;
                case "FasterCasting": return stats.Fc;
                case "FasterCastRecovery": return stats.Fcr;
                case "SpellDamageIncrease": return stats.Sdi;
                case "HitPointRegeneration": return stats.HitPointRegen;
                case "StaminaRegeneration": return stats.StaminaRegen;
                case "ManaRegeneration": return stats.ManaRegen;
                case "PhysicalResist": return stats.PhysicalResist;
                case "FireResist": return stats.FireResist;
                case "ColdResist": return stats.ColdResist;
                case "PoisonResist": return stats.PoisonResist;
                case "EnergyResist": return stats.EnergyResist;
                case "Luck": return stats.Luck;
                default: return int.MaxValue;
            }
        }

        private static int EffectiveStatValue(StatBlock stats, string key)
        {
            if (key == "LowerManaCost")
                return Math.Min(40, stats.Lmc) + Math.Min(15, stats.InherentLmc);
            int value = StatValue(stats, key);
            int cap = StatCap(stats, key);
            return cap > 0 ? Math.Min(value, cap) : value;
        }

        private static int StatCap(StatBlock stats, string key)
        {
            switch (key)
            {
                case "HitChanceIncrease": return 45;
                case "DefenseChanceIncrease": return 45;
                case "DamageIncrease": return 100;
                case "SwingSpeedIncrease": return 60;
                case "LowerReagentCost": return 100;
                case "FasterCasting": return 4;
                case "FasterCastRecovery": return 6;
                case "PhysicalResist": return stats.PhysicalResistCap;
                case "FireResist": return stats.FireResistCap;
                case "ColdResist": return stats.ColdResistCap;
                case "PoisonResist": return stats.PoisonResistCap;
                case "EnergyResist": return stats.EnergyResistCap;
                default:
                    return 0;
            }
        }

        private static double EffectiveSkillValue(string skillName, StatBlock stats)
        {
            Skill skill = World.Player?.Skills.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, skillName, StringComparison.OrdinalIgnoreCase));
            double value = (skill?.Base ?? 0) + stats.SkillBonus(skillName);
            return skill != null && skill.Cap > 0
                ? Math.Min(value, skill.Cap)
                : value;
        }

        private static int GoalTarget(EquipmentGuruGoals goals, string key)
        {
            const string skillPrefix = "SkillTarget:";
            if (key.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string skill = key.Substring(skillPrefix.Length);
                return goals.SkillTargets.TryGetValue(skill, out int target) ? target : 0;
            }

            switch (key)
            {
                case "Strength": return goals.Strength;
                case "Dexterity": return goals.Dexterity;
                case "Intelligence": return goals.Intelligence;
                case "HitPoints": return goals.HitPoints;
                case "Stamina": return goals.Stamina;
                case "Mana": return goals.Mana;
                case "HitChanceIncrease": return goals.HitChanceIncrease;
                case "DefenseChanceIncrease": return goals.DefenseChanceIncrease;
                case "DamageIncrease": return goals.DamageIncrease;
                case "SwingSpeedIncrease": return goals.SwingSpeedIncrease;
                case "LowerManaCost": return goals.LowerManaCost;
                case "LowerReagentCost": return goals.LowerReagentCost;
                case "FasterCasting": return goals.FasterCasting;
                case "FasterCastRecovery": return goals.FasterCastRecovery;
                case "SpellDamageIncrease": return goals.SpellDamageIncrease;
                case "HitPointRegeneration": return goals.HitPointRegeneration;
                case "StaminaRegeneration": return goals.StaminaRegeneration;
                case "ManaRegeneration": return goals.ManaRegeneration;
                case "Resist": return goals.Resist;
                case "PhysicalResist": return ResistTarget(goals.PhysicalResist, goals.Resist);
                case "FireResist": return ResistTarget(goals.FireResist, goals.Resist);
                case "ColdResist": return ResistTarget(goals.ColdResist, goals.Resist);
                case "PoisonResist": return ResistTarget(goals.PoisonResist, goals.Resist);
                case "EnergyResist": return ResistTarget(goals.EnergyResist, goals.Resist);
                case "Luck": return goals.Luck;
                default: return 0;
            }
        }

        private static double Score(StatBlock stats, EquipmentGuruGoals goals,
            EquipmentGuruBuild build)
        {
            double score = 0;
            score += Goal(stats.Strength, goals.Strength, 1.0);
            score += Goal(stats.Dexterity, goals.Dexterity, 1.0);
            score += Goal(stats.Intelligence, goals.Intelligence, 1.0);
            score += Goal(stats.HitPoints, goals.HitPoints, 1.4);
            score += Goal(stats.Stamina, goals.Stamina, 1.4);
            score += Goal(stats.Mana, goals.Mana, 1.2);
            score += Goal(EffectiveStatValue(stats, "HitChanceIncrease"),
                goals.HitChanceIncrease, 1.4);
            score += Goal(EffectiveStatValue(stats, "DefenseChanceIncrease"),
                goals.DefenseChanceIncrease, 1.0);
            score += Goal(EffectiveStatValue(stats, "DamageIncrease"),
                goals.DamageIncrease, 1.2);
            score += Goal(EffectiveStatValue(stats, "SwingSpeedIncrease"),
                goals.SwingSpeedIncrease, 1.4);
            score += Goal(EffectiveStatValue(stats, "LowerManaCost"),
                goals.LowerManaCost, 1.2);
            score += Goal(EffectiveStatValue(stats, "LowerReagentCost"),
                goals.LowerReagentCost, 1.2);
            double castingWeight = build == EquipmentGuruBuild.Mage ? 1.5 : 0.45;
            score += Goal(EffectiveStatValue(stats, "FasterCasting"),
                goals.FasterCasting, castingWeight);
            score += Goal(EffectiveStatValue(stats, "FasterCastRecovery"),
                goals.FasterCastRecovery, castingWeight);
            score += Goal(stats.Sdi, goals.SpellDamageIncrease,
                build == EquipmentGuruBuild.Mage ? 1.8 : 0.15);
            score += Goal(stats.HitPointRegen, goals.HitPointRegeneration,
                build == EquipmentGuruBuild.Mage ? 0.35 : 0.55);
            score += Goal(stats.StaminaRegen, goals.StaminaRegeneration,
                build == EquipmentGuruBuild.Mage ? 0.25 : 0.55);
            score += Goal(stats.ManaRegen, goals.ManaRegeneration,
                build == EquipmentGuruBuild.Mage ? 1.4 : 0.25);
            score += Goal(EffectiveStatValue(stats, "PhysicalResist"),
                ResistTarget(goals.PhysicalResist, goals.Resist), 1.1);
            score += Goal(EffectiveStatValue(stats, "FireResist"),
                ResistTarget(goals.FireResist, goals.Resist), 1.1);
            score += Goal(EffectiveStatValue(stats, "ColdResist"),
                ResistTarget(goals.ColdResist, goals.Resist), 1.1);
            score += Goal(EffectiveStatValue(stats, "PoisonResist"),
                ResistTarget(goals.PoisonResist, goals.Resist), 1.1);
            score += Goal(EffectiveStatValue(stats, "EnergyResist"),
                ResistTarget(goals.EnergyResist, goals.Resist), 1.1);
            foreach (KeyValuePair<string, int> skill in goals.SkillTargets)
            {
                if (skill.Value <= 0) continue;
                int cap = (int)Math.Round(SkillCap(skill.Key));
                int real = (int)Math.Round(SkillBase(skill.Key));
                int bonus = Math.Min(Math.Max(0, stats.SkillBonus(skill.Key)),
                    Math.Max(0, cap - real));
                int neededBonus = Math.Max(0, Math.Min(skill.Value, cap) - real);
                score += Math.Min(bonus, neededBonus) / 20.0 * 0.35;
            }
            score += Goal(stats.Luck, goals.Luck, 0.2);

            if (goals.Luck <= 0)
            {
                score += Math.Min(stats.Luck, 3000) / 3000.0 * 0.08;
            }

            return score;
        }

        private static double Goal(int value, int target, double weight)
        {
            if (target <= 0)
            {
                return 0;
            }

            double ratio = Math.Max(0, value) / (double)target;
            return Math.Min(ratio, 1.0) * weight;
        }

        private static int ResistTarget(int individualTarget, int sharedTarget)
        {
            return individualTarget >= 0 ? individualTarget : sharedTarget;
        }

        private static double QualityRank(StatBlock stats, EquipmentGuruGoals goals)
        {
            double rank = 0;
            if (goals.PreferInsured)
                rank += stats.InsuredItems * 0.000001;
            if (goals.PreferDurability)
                rank += stats.DurabilityQuality * 0.0000001;
            return rank;
        }

        private static Loadout CreateLoadout(BeamState state, List<Slot> slots,
            StatBlock current, EquipmentGuruGoals goals, Dictionary<Layer, Item> equipped)
        {
            var loadout = new Loadout
            {
                Score = state.Score,
                QualityPreference = QualityRank(state.Totals, goals)
            };

            foreach (KeyValuePair<Layer, Item> item in equipped)
            {
                if (IsLoadoutLayer(item.Key)
                    && item.Value != null && item.Value.Serial != 0)
                {
                    loadout.Items[item.Key] = item.Value.Serial;
                }
            }

            for (int i = 0; i < state.Choices.Count && i < slots.Count; i++)
            {
                Candidate choice = state.Choices[i];

                if (choice.Result.Serial == 0)
                {
                    loadout.Items.Remove(slots[i].Layer);
                }
                else
                {
                    loadout.Items[slots[i].Layer] = choice.Result.Serial;
                }

                if (!choice.IsCurrent)
                {
                    loadout.Changes.Add(new RecommendedItem
                    {
                        Layer = slots[i].Layer,
                        CurrentName = slots[i].Current.Result.Name,
                        CurrentItem = slots[i].Current.Result,
                        Item = choice.Result
                    });
                }
            }

            AddMetric(loadout, "STR", current.Strength, state.Totals.Strength,
                goals.Strength, IsMustTarget(goals, "Strength"));
            AddMetric(loadout, "DEX", current.Dexterity, state.Totals.Dexterity,
                goals.Dexterity, IsMustTarget(goals, "Dexterity"));
            AddMetric(loadout, "INT", current.Intelligence, state.Totals.Intelligence,
                goals.Intelligence, IsMustTarget(goals, "Intelligence"));
            AddMetric(loadout, "HP", current.HitPoints, state.Totals.HitPoints,
                goals.HitPoints, IsMustTarget(goals, "HitPoints"));
            AddMetric(loadout, "Stam", current.Stamina, state.Totals.Stamina,
                goals.Stamina, IsMustTarget(goals, "Stamina"));
            AddMetric(loadout, "Mana", current.Mana, state.Totals.Mana,
                goals.Mana, IsMustTarget(goals, "Mana"));
            AddMetric(loadout, "HCI", current.Hci, state.Totals.Hci,
                goals.HitChanceIncrease, IsMustTarget(goals, "HitChanceIncrease"));
            AddMetric(loadout, "DCI", current.Dci, state.Totals.Dci,
                goals.DefenseChanceIncrease, IsMustTarget(goals, "DefenseChanceIncrease"));
            AddMetric(loadout, "DI", current.Di, state.Totals.Di,
                goals.DamageIncrease, IsMustTarget(goals, "DamageIncrease"));
            AddMetric(loadout, "SSI", current.Ssi, state.Totals.Ssi,
                goals.SwingSpeedIncrease, IsMustTarget(goals, "SwingSpeedIncrease"));
            AddMetric(loadout, "LMC", EffectiveStatValue(current, "LowerManaCost"),
                EffectiveStatValue(state.Totals, "LowerManaCost"),
                goals.LowerManaCost, IsMustTarget(goals, "LowerManaCost"));
            AddMetric(loadout, "LRC", current.Lrc, state.Totals.Lrc,
                goals.LowerReagentCost, IsMustTarget(goals, "LowerReagentCost"));
            AddMetric(loadout, "FC", current.Fc, state.Totals.Fc,
                goals.FasterCasting, IsMustTarget(goals, "FasterCasting"));
            AddMetric(loadout, "FCR", current.Fcr, state.Totals.Fcr,
                goals.FasterCastRecovery, IsMustTarget(goals, "FasterCastRecovery"));
            AddMetric(loadout, "SDI", current.Sdi, state.Totals.Sdi,
                goals.SpellDamageIncrease, IsMustTarget(goals, "SpellDamageIncrease"));
            AddMetric(loadout, "HP regen", current.HitPointRegen,
                state.Totals.HitPointRegen, goals.HitPointRegeneration,
                IsMustTarget(goals, "HitPointRegeneration"));
            AddMetric(loadout, "Stam regen", current.StaminaRegen,
                state.Totals.StaminaRegen, goals.StaminaRegeneration,
                IsMustTarget(goals, "StaminaRegeneration"));
            AddMetric(loadout, "MR", current.ManaRegen, state.Totals.ManaRegen,
                goals.ManaRegeneration, IsMustTarget(goals, "ManaRegeneration"));
            AddMetric(loadout, "Phys", current.PhysicalResist, state.Totals.PhysicalResist,
                ResistTarget(goals.PhysicalResist, goals.Resist),
                IsMustTarget(goals, "PhysicalResist"));
            AddMetric(loadout, "Fire", current.FireResist, state.Totals.FireResist,
                ResistTarget(goals.FireResist, goals.Resist),
                IsMustTarget(goals, "FireResist"));
            AddMetric(loadout, "Cold", current.ColdResist, state.Totals.ColdResist,
                ResistTarget(goals.ColdResist, goals.Resist),
                IsMustTarget(goals, "ColdResist"));
            AddMetric(loadout, "Poison", current.PoisonResist, state.Totals.PoisonResist,
                ResistTarget(goals.PoisonResist, goals.Resist),
                IsMustTarget(goals, "PoisonResist"));
            AddMetric(loadout, "Energy", current.EnergyResist, state.Totals.EnergyResist,
                ResistTarget(goals.EnergyResist, goals.Resist),
                IsMustTarget(goals, "EnergyResist"));
            foreach (KeyValuePair<string, int> skill in goals.SkillTargets
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (skill.Value <= 0) continue;
                AddSkillMetric(loadout, skill.Key, skill.Value,
                    current.SkillBonus(skill.Key), state.Totals.SkillBonus(skill.Key),
                    IsMustTarget(goals, "SkillTarget:" + skill.Key),
                    IsPreserveTarget(goals, "SkillTarget:" + skill.Key));
            }
            AddSkillPointMetric(loadout, goals, current, state.Totals);
            AddMetric(loadout, "Luck", current.Luck, state.Totals.Luck, goals.Luck,
                IsMustTarget(goals, "Luck"));
            ApplyRequirementMetadata(loadout, goals, current, state.Totals);
            return loadout;
        }

        private static Loadout CreateCurrentLoadout(List<Slot> slots, StatBlock current,
            EquipmentGuruGoals goals, Dictionary<Layer, Item> equipped, double currentScore)
        {
            var state = new BeamState
            {
                Totals = current.Clone(),
                Score = currentScore
            };
            state.Choices.AddRange(slots.Select(slot => slot.Current));
            Loadout loadout = CreateLoadout(state, slots, current, goals, equipped);
            loadout.Kind = LoadoutKind.Current;
            loadout.Improvement = 0;
            loadout.MeetsRequirements = MeetsRequirements(current, goals, current);
            return loadout;
        }

        private static List<Loadout> RemoveDominated(List<Loadout> loadouts)
        {
            return loadouts.Where(candidate => !loadouts.Any(other =>
                !ReferenceEquals(candidate, other) && Dominates(other, candidate))).ToList();
        }

        private static bool Dominates(Loadout left, Loadout right)
        {
            if (left.Changes.Count > right.Changes.Count
                || left.Score + SCORE_EPSILON < right.Score)
            {
                return false;
            }

            bool strictlyBetter = left.Changes.Count < right.Changes.Count
                || left.Score > right.Score + SCORE_EPSILON;
            foreach (Metric rightMetric in right.Metrics.Where(metric =>
                metric.CountsAsTarget))
            {
                Metric leftMetric = left.Metrics.FirstOrDefault(metric =>
                    string.Equals(metric.Name, rightMetric.Name,
                        StringComparison.OrdinalIgnoreCase));
                if (leftMetric == null)
                {
                    continue;
                }

                int leftValue = EffectiveMetricValue(leftMetric);
                int rightValue = EffectiveMetricValue(rightMetric);
                if (rightMetric.LowerIsBetter)
                {
                    if (leftValue > rightValue) return false;
                    if (leftValue < rightValue) strictlyBetter = true;
                }
                else
                {
                    int target = Math.Max(1, rightMetric.Target);
                    int leftProgress = Math.Min(leftValue, target);
                    int rightProgress = Math.Min(rightValue, target);
                    if (leftProgress < rightProgress) return false;
                    if (leftProgress > rightProgress) strictlyBetter = true;
                }
            }

            return strictlyBetter;
        }

        private static double TargetOverflow(List<Metric> metrics)
        {
            double overflow = 0;
            foreach (Metric metric in metrics)
            {
                if (metric.Target > 0 && !metric.LowerIsBetter
                    && metric.Recommended > metric.Target)
                {
                    overflow += (metric.Recommended - metric.Target)
                        / (double)metric.Target;
                }
            }
            return overflow;
        }

        private static int EffectiveMetricValue(Metric metric)
        {
            return metric.RecommendedDisplayCap > 0
                ? Math.Min(metric.Recommended, metric.RecommendedDisplayCap)
                : metric.Recommended;
        }

        private static void ApplyRequirementMetadata(Loadout loadout,
            EquipmentGuruGoals goals, StatBlock current, StatBlock recommended)
        {
            foreach (Metric metric in loadout.Metrics)
            {
                string key = MetricKey(metric.Name);
                if (key == null) continue;
                metric.IsPreserve = metric.IsPreserve
                    || goals.PreserveTargets.Contains(key);
                metric.DisplayCap = MetricDisplayCap(metric.Name, current);
                metric.RecommendedDisplayCap = MetricDisplayCap(metric.Name, recommended);
            }
        }

        private static string MetricKey(string name)
        {
            switch (name)
            {
                case "STR": return "Strength";
                case "DEX": return "Dexterity";
                case "INT": return "Intelligence";
                case "HP": return "HitPoints";
                case "Stam": return "Stamina";
                case "Mana": return "Mana";
                case "HCI": return "HitChanceIncrease";
                case "DCI": return "DefenseChanceIncrease";
                case "DI": return "DamageIncrease";
                case "SSI": return "SwingSpeedIncrease";
                case "LMC": return "LowerManaCost";
                case "LRC": return "LowerReagentCost";
                case "FC": return "FasterCasting";
                case "FCR": return "FasterCastRecovery";
                case "SDI": return "SpellDamageIncrease";
                case "HP regen": return "HitPointRegeneration";
                case "Stam regen": return "StaminaRegeneration";
                case "MR": return "ManaRegeneration";
                case "Phys": return "PhysicalResist";
                case "Fire": return "FireResist";
                case "Cold": return "ColdResist";
                case "Poison": return "PoisonResist";
                case "Energy": return "EnergyResist";
                case "Luck": return "Luck";
                default:
                    const string suffix = " real for ";
                    int index = name.IndexOf(suffix, StringComparison.Ordinal);
                    return index > 0 ? "SkillTarget:" + name.Substring(0, index) : null;
            }
        }

        private static int MetricDisplayCap(string name, StatBlock stats)
        {
            switch (name)
            {
                case "HCI": return 45;
                case "DCI": return 45;
                case "DI": return 100;
                case "SSI": return 60;
                case "LRC": return 100;
                case "FC": return 4;
                case "FCR": return 6;
                case "Phys": return stats.PhysicalResistCap;
                case "Fire": return stats.FireResistCap;
                case "Cold": return stats.ColdResistCap;
                case "Poison": return stats.PoisonResistCap;
                case "Energy": return stats.EnergyResistCap;
                default: return 0;
            }
        }

        private static bool IsMustTarget(EquipmentGuruGoals goals, string key)
        {
            return goals.MustTargets.Contains(key);
        }

        private static bool IsPreserveTarget(EquipmentGuruGoals goals, string key)
        {
            return goals.PreserveTargets.Contains(key);
        }

        private static void AddSkillMetric(Loadout loadout, string skillName, int target,
            int currentBonus, int recommendedBonus, bool isMust, bool isPreserve)
        {
            loadout.Metrics.Add(new Metric
            {
                Name = $"{SkillLabel(skillName)} real for {target}",
                Current = Math.Max(0, target - Math.Max(0, currentBonus)),
                Recommended = Math.Max(0, target - Math.Max(0, recommendedBonus)),
                LowerIsBetter = true,
                CountsAsTarget = true,
                IsMust = isMust,
                IsPreserve = isPreserve,
                Target = target
            });
        }

        private static void AddSkillPointMetric(Loadout loadout, EquipmentGuruGoals goals,
            StatBlock current, StatBlock recommended)
        {
            int currentRequired = 0;
            int recommendedRequired = 0;

            foreach (KeyValuePair<string, int> skill in goals.SkillTargets)
            {
                if (skill.Value <= 0) continue;
                currentRequired += Math.Max(0,
                    skill.Value - Math.Max(0, current.SkillBonus(skill.Key)));
                recommendedRequired += Math.Max(0,
                    skill.Value - Math.Max(0, recommended.SkillBonus(skill.Key)));
            }

            if (currentRequired != recommendedRequired)
            {
                loadout.Metrics.Add(new Metric
                {
                    Name = "Target skills real total",
                    Current = currentRequired,
                    Recommended = recommendedRequired,
                    LowerIsBetter = true
                });
            }
        }

        private static bool IsLoadoutLayer(Layer layer)
        {
            return layer == Layer.OneHanded || layer == Layer.TwoHanded
                || layer == Layer.Talisman || Array.IndexOf(_replaceableLayers, layer) >= 0;
        }

        private static void AddMetric(Loadout loadout, string name, int current,
            int recommended, int target, bool isMust = false)
        {
            if (target > 0 || current != recommended)
            {
                loadout.Metrics.Add(new Metric
                {
                    Name = name,
                    Current = current,
                    Recommended = recommended,
                    Target = target,
                    CountsAsTarget = target > 0,
                    IsMust = isMust
                });
            }
        }

        private static void CollectUsedSkills(List<SkillSnapshot> destination,
            IEnumerable<string> includedTargets = null)
        {
            if (World.Player == null)
            {
                return;
            }

            var targets = new HashSet<string>(includedTargets ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            foreach (Skill skill in World.Player.Skills)
            {
                if ((skill.Base >= 70 || targets.Contains(skill.Name))
                    && _combatSkillNames.Contains(skill.Name, StringComparer.OrdinalIgnoreCase))
                {
                    destination.Add(new SkillSnapshot
                    {
                        Name = skill.Name,
                        Base = skill.Base,
                        Value = skill.Value,
                        Cap = skill.Cap
                    });
                }
            }

            destination.Sort((left, right) => right.Base.CompareTo(left.Base));
        }

        private static Dictionary<string, string> BuildSkillAliases(List<SkillSnapshot> skills)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (SkillSnapshot skill in skills)
            {
                aliases[Normalize(skill.Name)] = skill.Name;

                if (_skillPropertyKeys.TryGetValue(skill.Name, out string[] propertyAliases))
                {
                    foreach (string alias in propertyAliases)
                    {
                        aliases[alias] = skill.Name;
                    }
                }
            }

            return aliases;
        }

        private static void EnsureDetectedSkillTargets(EquipmentGuruGoals goals)
        {
            goals.SkillTargets ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var detected = new List<SkillSnapshot>();
            CollectUsedSkills(detected);
            foreach (SkillSnapshot skill in detected)
            {
                if (!goals.SkillTargets.ContainsKey(skill.Name))
                {
                    float effective = Math.Max(skill.Base, skill.Value);
                    if (skill.Cap > 0) effective = Math.Min(effective, skill.Cap);
                    goals.SkillTargets[skill.Name] = Math.Max(0, (int)Math.Round(effective));
                }
            }
        }

        private static string SkillLabel(string name)
        {
            switch (name)
            {
                case "Swordsmanship": return "Swords";
                case "Mace Fighting": return "Macing";
                case "Evaluating Intelligence": return "Eval Int";
                case "Resisting Spells": return "Resist";
                default: return name;
            }
        }

        private static Dictionary<Layer, Item> GetEquipped()
        {
            var equipped = new Dictionary<Layer, Item>();

            for (var node = World.Player.Items; node != null; node = node.Next)
            {
                if (node is Item item && !item.IsDestroyed)
                {
                    equipped[item.Layer] = item;
                }
            }

            return equipped;
        }

        private static string DescribeFixedItems(Dictionary<Layer, Item> equipped)
        {
            var fixedItems = new List<string>();

            if (equipped.TryGetValue(Layer.OneHanded, out Item oneHanded))
            {
                fixedItems.Add(ItemName(oneHanded));
            }

            if (equipped.TryGetValue(Layer.TwoHanded, out Item twoHanded)
                && !IsShield(twoHanded.Graphic))
            {
                fixedItems.Add(ItemName(twoHanded));
            }

            if (equipped.TryGetValue(Layer.Talisman, out Item talisman))
            {
                fixedItems.Add(ItemName(talisman));
            }

            return fixedItems.Count == 0 ? "None detected" : string.Join("  •  ", fixedItems);
        }

        private static string ItemName(Item item)
        {
            if (World.OPL.TryGetNameAndData(item.Serial, out string name, out _)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            return string.IsNullOrWhiteSpace(item.Name) ? item.ItemData.Name : item.Name;
        }

        private static bool IsShield(ushort graphic)
        {
            ref StaticTiles data = ref TileDataLoader.Instance.StaticData[graphic];

            if ((Layer)data.Layer != Layer.TwoHanded || !data.IsWearable || data.IsLight)
            {
                return false;
            }

            return !data.IsWeapon || (!string.IsNullOrEmpty(data.Name)
                && (data.Name.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0
                    || data.Name.IndexOf("buckler", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static float SkillBase(string name)
        {
            Skill skill = World.Player?.Skills.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            return skill?.Base ?? 0;
        }

        private static float SkillCap(string name)
        {
            Skill skill = World.Player?.Skills.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            return skill?.Cap > 0 ? skill.Cap : 120;
        }

        private static string Normalize(string value)
        {
            var result = new StringBuilder(value?.Length ?? 0);

            if (value != null)
            {
                foreach (char c in value)
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        result.Append(char.ToLowerInvariant(c));
                    }
                }
            }

            return result.ToString();
        }

        private static string FormatProperties(ItemPropertiesData properties)
        {
            if (properties?.RawLines == null)
            {
                return string.Empty;
            }

            return string.Join("\n", properties.RawLines.Where(line =>
                !string.IsNullOrWhiteSpace(line)));
        }

        private static void EnsureLoaded()
        {
            string path = ProfileDataStore.GetPath(SETTINGS_FILE);
            uint owner = World.Player?.Serial ?? 0;

            if (owner == _loadedOwner && string.Equals(path, _loadedPath,
                StringComparison.OrdinalIgnoreCase) && _goals.Count > 0)
            {
                return;
            }

            _loadedOwner = owner;
            _loadedPath = path;
            _goals.Clear();
            _excludedItems.Clear();
            _lockedLayers.Clear();

            foreach (EquipmentGuruBuild build in Enum.GetValues(typeof(EquipmentGuruBuild)))
            {
                if (build != EquipmentGuruBuild.Auto)
                {
                    _goals[build] = CreateDefaultGoals(build);
                }
            }

            foreach (string line in ProfileDataStore.ReadAllLines(SETTINGS_FILE))
            {
                string[] parts = line.Split('\t');

                if (parts.Length != 3
                    || !Enum.TryParse(parts[0], true, out EquipmentGuruBuild build)
                    || build == EquipmentGuruBuild.Auto
                    || !_goals.TryGetValue(build, out EquipmentGuruGoals goals)
                    || !int.TryParse(parts[2], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int value))
                {
                    continue;
                }

                SetGoal(goals, parts[1], value);
            }

            foreach (EquipmentGuruGoals goals in _goals.Values)
            {
                if (goals.MustTargets.Remove("Resist"))
                {
                    goals.MustTargets.Add("PhysicalResist");
                    goals.MustTargets.Add("FireResist");
                    goals.MustTargets.Add("ColdResist");
                    goals.MustTargets.Add("PoisonResist");
                    goals.MustTargets.Add("EnergyResist");
                }
                if (goals.PreserveTargets.Remove("Resist"))
                {
                    goals.PreserveTargets.Add("PhysicalResist");
                    goals.PreserveTargets.Add("FireResist");
                    goals.PreserveTargets.Add("ColdResist");
                    goals.PreserveTargets.Add("PoisonResist");
                    goals.PreserveTargets.Add("EnergyResist");
                }
            }

            foreach (string line in ProfileDataStore.ReadAllLines(RULES_FILE))
            {
                string[] parts = line.Split('\t');
                if (parts.Length != 2) continue;

                if (parts[0] == "exclude"
                    && uint.TryParse(parts[1], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint serial))
                {
                    _excludedItems.Add(serial);
                }
                else if (parts[0] == "lock"
                    && Enum.TryParse(parts[1], true, out Layer layer))
                {
                    _lockedLayers.Add(layer);
                }
            }
        }

        private static void SaveRules()
        {
            ProfileDataStore.Write(RULES_FILE, writer =>
            {
                foreach (Layer layer in _lockedLayers.OrderBy(value => (int)value))
                    writer.WriteLine($"lock\t{layer}");
                foreach (uint serial in _excludedItems.OrderBy(value => value))
                    writer.WriteLine($"exclude\t{serial:X8}");
            });
        }

        private static void Save()
        {
            ProfileDataStore.Write(SETTINGS_FILE, writer =>
            {
                foreach (KeyValuePair<EquipmentGuruBuild, EquipmentGuruGoals> entry in _goals)
                {
                    foreach (KeyValuePair<string, int> value in GoalValues(entry.Value))
                    {
                        writer.WriteLine($"{entry.Key}\t{value.Key}\t{value.Value.ToString(CultureInfo.InvariantCulture)}");
                    }
                }
            });
        }

        private static EquipmentGuruGoals CreateDefaultGoals(EquipmentGuruBuild build)
        {
            if (build == EquipmentGuruBuild.Mage)
            {
                return new EquipmentGuruGoals
                {
                    Strength = 150,
                    Intelligence = 150,
                    HitPoints = 140,
                    Mana = 180,
                    LowerManaCost = 40,
                    LowerReagentCost = 100,
                    FasterCasting = 4,
                    FasterCastRecovery = 6,
                    SpellDamageIncrease = 200,
                    ManaRegeneration = 30,
                    Resist = 70
                };
            }

            bool ranged = build == EquipmentGuruBuild.Archer
                || build == EquipmentGuruBuild.Thrower;
            return new EquipmentGuruGoals
            {
                Strength = 150,
                Dexterity = 150,
                HitPoints = 160,
                Stamina = ranged ? 210 : 180,
                Mana = 50,
                HitChanceIncrease = 45,
                DefenseChanceIncrease = 30,
                DamageIncrease = 100,
                SwingSpeedIncrease = ranged ? 55 : 25,
                LowerManaCost = 40,
                FasterCasting = 4,
                FasterCastRecovery = 6,
                Resist = 70
            };
        }

        private static IEnumerable<KeyValuePair<string, int>> GoalValues(EquipmentGuruGoals goals)
        {
            yield return Pair("Strength", goals.Strength);
            yield return Pair("Dexterity", goals.Dexterity);
            yield return Pair("Intelligence", goals.Intelligence);
            yield return Pair("HitPoints", goals.HitPoints);
            yield return Pair("Stamina", goals.Stamina);
            yield return Pair("Mana", goals.Mana);
            yield return Pair("HitChanceIncrease", goals.HitChanceIncrease);
            yield return Pair("DefenseChanceIncrease", goals.DefenseChanceIncrease);
            yield return Pair("DamageIncrease", goals.DamageIncrease);
            yield return Pair("SwingSpeedIncrease", goals.SwingSpeedIncrease);
            yield return Pair("LowerManaCost", goals.LowerManaCost);
            yield return Pair("LowerReagentCost", goals.LowerReagentCost);
            yield return Pair("FasterCasting", goals.FasterCasting);
            yield return Pair("FasterCastRecovery", goals.FasterCastRecovery);
            yield return Pair("SpellDamageIncrease", goals.SpellDamageIncrease);
            yield return Pair("HitPointRegeneration", goals.HitPointRegeneration);
            yield return Pair("StaminaRegeneration", goals.StaminaRegeneration);
            yield return Pair("ManaRegeneration", goals.ManaRegeneration);
            yield return Pair("Resist", goals.Resist);
            if (goals.PhysicalResist >= 0)
                yield return Pair("PhysicalResist", goals.PhysicalResist);
            if (goals.FireResist >= 0)
                yield return Pair("FireResist", goals.FireResist);
            if (goals.ColdResist >= 0)
                yield return Pair("ColdResist", goals.ColdResist);
            if (goals.PoisonResist >= 0)
                yield return Pair("PoisonResist", goals.PoisonResist);
            if (goals.EnergyResist >= 0)
                yield return Pair("EnergyResist", goals.EnergyResist);
            yield return Pair("Luck", goals.Luck);
            yield return Pair("PreferInsured", goals.PreferInsured ? 1 : 0);
            yield return Pair("PreferDurability", goals.PreferDurability ? 1 : 0);
            foreach (KeyValuePair<string, int> skill in goals.SkillTargets
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
                yield return Pair("SkillTarget:" + skill.Key, skill.Value);
            foreach (string key in goals.MustTargets.OrderBy(value => value,
                StringComparer.OrdinalIgnoreCase))
                yield return Pair("Must:" + key, 1);
            foreach (string key in goals.PreserveTargets.OrderBy(value => value,
                StringComparer.OrdinalIgnoreCase))
                yield return Pair("Preserve:" + key, 1);
        }

        private static KeyValuePair<string, int> Pair(string key, int value) =>
            new KeyValuePair<string, int>(key, value);

        private static void SetGoal(EquipmentGuruGoals goals, string key, int value)
        {
            const string mustPrefix = "Must:";
            if (key.StartsWith(mustPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string target = key.Substring(mustPrefix.Length).Trim();
                if (!string.IsNullOrEmpty(target) && value != 0)
                {
                    goals.MustTargets.Add(target);
                }
                return;
            }

            const string preservePrefix = "Preserve:";
            if (key.StartsWith(preservePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string target = key.Substring(preservePrefix.Length).Trim();
                if (!string.IsNullOrEmpty(target) && value != 0)
                {
                    goals.PreserveTargets.Add(target);
                }
                return;
            }

            const string skillPrefix = "SkillTarget:";
            if (key.StartsWith(skillPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string skill = key.Substring(skillPrefix.Length).Trim();
                if (!string.IsNullOrEmpty(skill)) goals.SkillTargets[skill] = Math.Max(0, value);
                return;
            }

            switch (key)
            {
                case "Strength": goals.Strength = value; break;
                case "Dexterity": goals.Dexterity = value; break;
                case "Intelligence": goals.Intelligence = value; break;
                case "HitPoints": goals.HitPoints = value; break;
                case "Stamina": goals.Stamina = value; break;
                case "Mana": goals.Mana = value; break;
                case "HitChanceIncrease": goals.HitChanceIncrease = value; break;
                case "DefenseChanceIncrease": goals.DefenseChanceIncrease = value; break;
                case "DamageIncrease": goals.DamageIncrease = value; break;
                case "SwingSpeedIncrease": goals.SwingSpeedIncrease = value; break;
                case "LowerManaCost": goals.LowerManaCost = value; break;
                case "LowerReagentCost": goals.LowerReagentCost = value; break;
                case "FasterCasting": goals.FasterCasting = value; break;
                case "FasterCastRecovery": goals.FasterCastRecovery = value; break;
                case "SpellDamageIncrease": goals.SpellDamageIncrease = value; break;
                case "HitPointRegeneration": goals.HitPointRegeneration = value; break;
                case "StaminaRegeneration": goals.StaminaRegeneration = value; break;
                case "ManaRegeneration": goals.ManaRegeneration = value; break;
                case "Resist": goals.Resist = value; break;
                case "PhysicalResist": goals.PhysicalResist = value; break;
                case "FireResist": goals.FireResist = value; break;
                case "ColdResist": goals.ColdResist = value; break;
                case "PoisonResist": goals.PoisonResist = value; break;
                case "EnergyResist": goals.EnergyResist = value; break;
                case "Luck": goals.Luck = value; break;
                case "PreferInsured": goals.PreferInsured = value != 0; break;
                case "PreferDurability": goals.PreferDurability = value != 0; break;
            }
        }
    }
}

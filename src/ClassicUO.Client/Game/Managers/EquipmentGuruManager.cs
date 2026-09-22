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
        private const int MAX_CANDIDATES_PER_LAYER = 14;
        private const int MAX_BEAM_STATES = 140;

        private static readonly Layer[] _replaceableLayers =
        {
            Layer.Shoes, Layer.Pants, Layer.Shirt, Layer.Helmet, Layer.Gloves,
            Layer.Ring, Layer.Necklace, Layer.Waist, Layer.Torso, Layer.Bracelet,
            Layer.Tunic, Layer.Earrings, Layer.Arms, Layer.Cloak, Layer.Robe,
            Layer.Skirt, Layer.Legs
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
            internal int ManaRegeneration;
            internal int Resist;
            internal int Luck;
            internal Dictionary<string, int> SkillTargets =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            internal EquipmentGuruGoals Clone()
            {
                var clone = (EquipmentGuruGoals)MemberwiseClone();
                clone.SkillTargets = new Dictionary<string, int>(
                    SkillTargets ?? new Dictionary<string, int>(),
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
        }

        internal sealed class Analysis
        {
            internal EquipmentGuruBuild DetectedBuild;
            internal readonly List<SkillSnapshot> UsedSkills = new List<SkillSnapshot>();
            internal string FixedItems;
            internal int CatalogItems;
            internal int ItemsWithoutProperties;
            internal double CurrentScore;
            internal readonly List<Loadout> Loadouts = new List<Loadout>();
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
            internal int Stamina;
            internal int Mana;
            internal int Hci;
            internal int Dci;
            internal int Di;
            internal int Ssi;
            internal int Lmc;
            internal int Lrc;
            internal int Fc;
            internal int Fcr;
            internal int Sdi;
            internal int ManaRegen;
            internal int PhysicalResist;
            internal int FireResist;
            internal int ColdResist;
            internal int PoisonResist;
            internal int EnergyResist;
            internal int Luck;
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
                Strength += value.Strength * multiplier;
                Dexterity += value.Dexterity * multiplier;
                Intelligence += value.Intelligence * multiplier;
                HitPoints += value.HitPoints * multiplier;
                Stamina += value.Stamina * multiplier;
                Mana += value.Mana * multiplier;
                Hci += value.Hci * multiplier;
                Dci += value.Dci * multiplier;
                Di += value.Di * multiplier;
                Ssi += value.Ssi * multiplier;
                Lmc += value.Lmc * multiplier;
                Lrc += value.Lrc * multiplier;
                Fc += value.Fc * multiplier;
                Fcr += value.Fcr * multiplier;
                Sdi += value.Sdi * multiplier;
                ManaRegen += value.ManaRegen * multiplier;
                PhysicalResist += value.PhysicalResist * multiplier;
                FireResist += value.FireResist * multiplier;
                ColdResist += value.ColdResist * multiplier;
                PoisonResist += value.PoisonResist * multiplier;
                EnergyResist += value.EnergyResist * multiplier;
                Luck += value.Luck * multiplier;
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
            List<Slot> slots = BuildSlots(search.Results, equipped, skillAliases, goals, build);
            analysis.CurrentScore = Score(baseline, goals, build);

            var states = new List<BeamState>
            {
                new BeamState { Totals = baseline.Clone(), Score = analysis.CurrentScore }
            };

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

                states = next.OrderByDescending(state => state.Score)
                    .Take(MAX_BEAM_STATES).ToList();
            }

            var displayedTotals = new HashSet<string>(StringComparer.Ordinal);

            foreach (BeamState state in states.OrderByDescending(value => value.Score))
            {
                Loadout loadout = CreateLoadout(state, slots, baseline, goals, equipped);
                string displayedSignature = string.Join("|", loadout.Metrics.Select(metric =>
                    metric.Name + ":" + metric.Recommended.ToString(CultureInfo.InvariantCulture)));

                if (!displayedTotals.Add(displayedSignature))
                {
                    continue;
                }

                loadout.Improvement = state.Score - analysis.CurrentScore;
                analysis.Loadouts.Add(loadout);

                if (analysis.Loadouts.Count == 3)
                {
                    break;
                }
            }

            if (analysis.Loadouts.Count == 0)
            {
                analysis.Status = "No wearable candidates with loaded properties were found.";
            }
            else if (analysis.Loadouts[0].Improvement <= 0.0001)
            {
                analysis.Status = "Current equipment already scores best against these targets.";
            }
            else
            {
                analysis.Status = $"Compared {analysis.CatalogItems} catalog item(s) across {slots.Count} replaceable slot(s).";
            }

            if (analysis.ItemsWithoutProperties > 0)
            {
                analysis.Status += $" {analysis.ItemsWithoutProperties} item(s) were skipped because their properties are not loaded; scan, wait, then analyze again.";
            }

            return analysis;
        }

        private static List<Slot> BuildSlots(
            List<ItemFinderManager.SearchResult> results,
            Dictionary<Layer, Item> equipped,
            Dictionary<string, string> skillAliases,
            EquipmentGuruGoals goals,
            EquipmentGuruBuild build)
        {
            var slots = new List<Slot>();
            var wearableByLayer = new Dictionary<Layer, List<ItemFinderManager.SearchResult>>();

            foreach (ItemFinderManager.SearchResult result in results)
            {
                ref StaticTiles data = ref TileDataLoader.Instance.StaticData[result.Graphic];
                Layer itemLayer = (Layer)data.Layer;
                if (!data.IsWearable || !HasLoadedProperties(result) || IsExcluded(result.Serial)
                    || !IsRaceCompatible(result, itemLayer))
                    continue;
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
                Candidate currentCandidate = current != null
                    ? CreateCandidate(indexedCurrent ?? CreateResult(current), layer, skillAliases, true)
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

                slot.Candidates.Sort((left, right) =>
                    CandidateRank(right.Stats, goals, build)
                        .CompareTo(CandidateRank(left.Stats, goals, build)));

                if (slot.Candidates.Count == 1 && current == null)
                {
                    continue;
                }

                if (!slot.Candidates.Contains(currentCandidate))
                {
                    slot.Candidates.Insert(0, currentCandidate);
                }

                var unique = new List<Candidate> { currentCandidate };
                var serials = new HashSet<uint> { current?.Serial ?? 0 };

                foreach (Candidate candidate in slot.Candidates)
                {
                    if (serials.Add(candidate.Result.Serial))
                    {
                        unique.Add(candidate);
                    }

                    if (unique.Count >= MAX_CANDIDATES_PER_LAYER)
                    {
                        break;
                    }
                }

                slot.Candidates.Clear();
                slot.Candidates.AddRange(unique);
                slots.Add(slot);
            }

            AddShieldSlot(results, equipped, skillAliases, goals, build, slots);
            return slots;
        }

        private static void AddShieldSlot(List<ItemFinderManager.SearchResult> results,
            Dictionary<Layer, Item> equipped, Dictionary<string, string> skillAliases,
            EquipmentGuruGoals goals, EquipmentGuruBuild build, List<Slot> slots)
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

            Candidate current = CreateCandidate(CreateResult(shield), Layer.TwoHanded,
                skillAliases, true);
            ItemFinderManager.SearchResult indexedShield = results.FirstOrDefault(result =>
                result.Serial == shield.Serial);

            if (indexedShield != null)
            {
                current = CreateCandidate(indexedShield, Layer.TwoHanded, skillAliases, true);
            }
            var slot = new Slot { Layer = Layer.TwoHanded, Current = current };
            slot.Candidates.Add(current);

            foreach (ItemFinderManager.SearchResult result in results)
            {
                if (result.Serial == shield.Serial || !IsShield(result.Graphic)
                    || IsExcluded(result.Serial)
                    || !HasLoadedProperties(result)
                    || !IsRaceCompatible(result, Layer.TwoHanded))
                {
                    continue;
                }

                slot.Candidates.Add(CreateCandidate(result, Layer.TwoHanded, skillAliases, false));
            }

            slot.Candidates.Sort((left, right) =>
                CandidateRank(right.Stats, goals, build)
                    .CompareTo(CandidateRank(left.Stats, goals, build)));
            List<Candidate> best = slot.Candidates.Take(MAX_CANDIDATES_PER_LAYER).ToList();
            slot.Candidates.Clear();
            slot.Candidates.AddRange(best);

            if (!slot.Candidates.Any(candidate => candidate.IsCurrent))
            {
                slot.Candidates.Insert(0, current);
            }

            slots.Add(slot);
        }

        private static Candidate CreateCandidate(ItemFinderManager.SearchResult result,
            Layer layer, Dictionary<string, string> skillAliases, bool current)
        {
            return new Candidate
            {
                Layer = layer,
                Result = result,
                Stats = ParseStats(result.AllProperties, skillAliases),
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

            string properties = result.AllProperties ?? string.Empty;
            bool gargoyleOnly = ContainsRaceRestriction(properties, "Gargoyle Only")
                || ContainsRaceRestriction(properties, "Gargoyles Only");

            if (World.Player.Race == RaceType.GARGOYLE)
            {
                return gargoyleOnly || layer == Layer.Ring || layer == Layer.Bracelet;
            }

            return !gargoyleOnly;
        }

        private static bool ContainsRaceRestriction(string properties, string restriction)
        {
            return properties.IndexOf(restriction, StringComparison.OrdinalIgnoreCase) >= 0;
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
            var totals = new StatBlock
            {
                Strength = player.Strength,
                Dexterity = player.Dexterity,
                Intelligence = player.Intelligence,
                HitPoints = player.HitsMax,
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
                Luck = player.Luck
            };

            foreach (Item item in equipped)
            {
                ItemFinderManager.SearchResult result = catalog.FirstOrDefault(candidate =>
                    candidate.Serial == item.Serial) ?? CreateResult(item);
                StatBlock equipmentStats = ParseStats(result.AllProperties, skillAliases);
                foreach (KeyValuePair<string, int> skill in equipmentStats.SkillBonuses)
                {
                    totals.SkillBonuses.TryGetValue(skill.Key, out int current);
                    totals.SkillBonuses[skill.Key] = current + skill.Value;
                }
                totals.ManaRegen += equipmentStats.ManaRegen;
            }

            return totals;
        }

        private static StatBlock ParseStats(string tooltip,
            Dictionary<string, string> skillAliases)
        {
            var stats = new StatBlock();

            if (string.IsNullOrWhiteSpace(tooltip))
            {
                return stats;
            }

            var properties = new ItemPropertiesData("Item\n" + tooltip);

            foreach (ItemPropertiesData.SinglePropertyData property in properties.singlePropertyData)
            {
                if (property.FirstValue == double.MinValue)
                {
                    continue;
                }

                int value = (int)Math.Round(property.FirstValue);
                string key = Normalize(property.Name);

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
                    case "manaregeneration": stats.ManaRegen += value; break;
                    case "physicalresist": stats.PhysicalResist += value; break;
                    case "fireresist": stats.FireResist += value; break;
                    case "coldresist": stats.ColdResist += value; break;
                    case "poisonresist": stats.PoisonResist += value; break;
                    case "energyresist": stats.EnergyResist += value; break;
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

            return stats;
        }

        private static double Score(StatBlock stats, EquipmentGuruGoals goals,
            EquipmentGuruBuild build)
        {
            double score = 0;
            score += Goal(stats.Strength, goals.Strength, 1.0);
            score += Goal(stats.Dexterity, goals.Dexterity, 1.0);
            score += Goal(stats.Intelligence, goals.Intelligence, 1.0);
            score += Goal(stats.HitPoints, goals.HitPoints, 1.4, true);
            score += Goal(stats.Stamina, goals.Stamina, 1.4, true);
            score += Goal(stats.Mana, goals.Mana, 1.2, true);
            score += Goal(stats.Hci, goals.HitChanceIncrease, 1.4);
            score += Goal(stats.Dci, goals.DefenseChanceIncrease, 1.0);
            score += Goal(stats.Di, goals.DamageIncrease, 1.2);
            score += Goal(stats.Ssi, goals.SwingSpeedIncrease, 1.4);
            score += Goal(stats.Lmc, goals.LowerManaCost, 1.2);
            score += Goal(stats.Lrc, goals.LowerReagentCost, 1.2);
            double castingWeight = build == EquipmentGuruBuild.Mage ? 1.5 : 0.45;
            score += Goal(stats.Fc, goals.FasterCasting, castingWeight);
            score += Goal(stats.Fcr, goals.FasterCastRecovery, castingWeight);
            score += Goal(stats.Sdi, goals.SpellDamageIncrease,
                build == EquipmentGuruBuild.Mage ? 1.8 : 0.15, true);
            score += Goal(stats.ManaRegen, goals.ManaRegeneration,
                build == EquipmentGuruBuild.Mage ? 1.4 : 0.25, true);
            score += Goal(stats.PhysicalResist, goals.Resist, 1.1);
            score += Goal(stats.FireResist, goals.Resist, 1.1);
            score += Goal(stats.ColdResist, goals.Resist, 1.1);
            score += Goal(stats.PoisonResist, goals.Resist, 1.1);
            score += Goal(stats.EnergyResist, goals.Resist, 1.1);
            foreach (KeyValuePair<string, int> skill in goals.SkillTargets)
            {
                if (skill.Value <= 0) continue;
                int bonus = Math.Max(0, stats.SkillBonus(skill.Key));
                score += Math.Min(bonus, skill.Value) / 20.0 * 0.35;
            }
            score += Goal(stats.Luck, goals.Luck, 0.2, true);

            if (goals.Luck <= 0)
            {
                score += Math.Min(stats.Luck, 3000) / 3000.0 * 0.08;
            }

            return score;
        }

        private static double Goal(int value, int target, double weight,
            bool rewardOverflow = false)
        {
            if (target <= 0)
            {
                return 0;
            }

            double ratio = Math.Max(0, value) / (double)target;
            double score = Math.Min(ratio, 1.0) * weight;

            if (rewardOverflow && ratio > 1.0)
            {
                score += Math.Min(ratio - 1.0, 1.0) * weight * 0.12;
            }

            return score;
        }

        private static double RawCandidateValue(StatBlock stats)
        {
            return stats.Strength + stats.Dexterity + stats.Intelligence
                + stats.HitPoints + stats.Stamina + stats.Mana
                + stats.Hci * 1.5 + stats.Dci + stats.Di * 0.5 + stats.Ssi * 1.5
                + stats.Lmc + stats.Lrc + stats.Fc * 8 + stats.Fcr * 5
                + stats.Sdi * 0.5 + stats.ManaRegen * 4
                + stats.PhysicalResist + stats.FireResist + stats.ColdResist
                + stats.PoisonResist + stats.EnergyResist
                + stats.SkillBonuses.Values.Sum() + stats.Luck * 0.01;
        }

        private static double CandidateRank(StatBlock stats, EquipmentGuruGoals goals,
            EquipmentGuruBuild build)
        {
            return Score(stats, goals, build) + RawCandidateValue(stats) * 0.00001;
        }

        private static Loadout CreateLoadout(BeamState state, List<Slot> slots,
            StatBlock current, EquipmentGuruGoals goals, Dictionary<Layer, Item> equipped)
        {
            var loadout = new Loadout { Score = state.Score };

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

            AddMetric(loadout, "STR", current.Strength, state.Totals.Strength, goals.Strength);
            AddMetric(loadout, "DEX", current.Dexterity, state.Totals.Dexterity, goals.Dexterity);
            AddMetric(loadout, "INT", current.Intelligence, state.Totals.Intelligence, goals.Intelligence);
            AddMetric(loadout, "HP", current.HitPoints, state.Totals.HitPoints, goals.HitPoints);
            AddMetric(loadout, "Stam", current.Stamina, state.Totals.Stamina, goals.Stamina);
            AddMetric(loadout, "Mana", current.Mana, state.Totals.Mana, goals.Mana);
            AddMetric(loadout, "HCI", current.Hci, state.Totals.Hci, goals.HitChanceIncrease);
            AddMetric(loadout, "DCI", current.Dci, state.Totals.Dci, goals.DefenseChanceIncrease);
            AddMetric(loadout, "DI", current.Di, state.Totals.Di, goals.DamageIncrease);
            AddMetric(loadout, "SSI", current.Ssi, state.Totals.Ssi, goals.SwingSpeedIncrease);
            AddMetric(loadout, "LMC", current.Lmc, state.Totals.Lmc, goals.LowerManaCost);
            AddMetric(loadout, "LRC", current.Lrc, state.Totals.Lrc, goals.LowerReagentCost);
            AddMetric(loadout, "FC", current.Fc, state.Totals.Fc, goals.FasterCasting);
            AddMetric(loadout, "FCR", current.Fcr, state.Totals.Fcr, goals.FasterCastRecovery);
            AddMetric(loadout, "SDI", current.Sdi, state.Totals.Sdi, goals.SpellDamageIncrease);
            AddMetric(loadout, "MR", current.ManaRegen, state.Totals.ManaRegen, goals.ManaRegeneration);
            AddMetric(loadout, "Phys", current.PhysicalResist, state.Totals.PhysicalResist, goals.Resist);
            AddMetric(loadout, "Fire", current.FireResist, state.Totals.FireResist, goals.Resist);
            AddMetric(loadout, "Cold", current.ColdResist, state.Totals.ColdResist, goals.Resist);
            AddMetric(loadout, "Poison", current.PoisonResist, state.Totals.PoisonResist, goals.Resist);
            AddMetric(loadout, "Energy", current.EnergyResist, state.Totals.EnergyResist, goals.Resist);
            foreach (KeyValuePair<string, int> skill in goals.SkillTargets
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (skill.Value <= 0) continue;
                AddSkillMetric(loadout, skill.Key, skill.Value,
                    current.SkillBonus(skill.Key), state.Totals.SkillBonus(skill.Key));
            }
            AddSkillPointMetric(loadout, goals, current, state.Totals);
            AddMetric(loadout, "Luck", current.Luck, state.Totals.Luck, goals.Luck);
            return loadout;
        }

        private static void AddSkillMetric(Loadout loadout, string skillName, int target,
            int currentBonus, int recommendedBonus)
        {
            loadout.Metrics.Add(new Metric
            {
                Name = $"{SkillLabel(skillName)} real for {target}",
                Current = Math.Max(0, target - Math.Max(0, currentBonus)),
                Recommended = Math.Max(0, target - Math.Max(0, recommendedBonus)),
                LowerIsBetter = true,
                CountsAsTarget = true
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
            int recommended, int target)
        {
            if (target > 0 || current != recommended)
            {
                loadout.Metrics.Add(new Metric
                {
                    Name = name,
                    Current = current,
                    Recommended = recommended,
                    Target = target,
                    CountsAsTarget = target > 0
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
            yield return Pair("ManaRegeneration", goals.ManaRegeneration);
            yield return Pair("Resist", goals.Resist);
            yield return Pair("Luck", goals.Luck);
            foreach (KeyValuePair<string, int> skill in goals.SkillTargets
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase))
                yield return Pair("SkillTarget:" + skill.Key, skill.Value);
        }

        private static KeyValuePair<string, int> Pair(string key, int value) =>
            new KeyValuePair<string, int>(key, value);

        private static void SetGoal(EquipmentGuruGoals goals, string key, int value)
        {
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
                case "ManaRegeneration": goals.ManaRegeneration = value; break;
                case "Resist": goals.Resist = value; break;
                case "Luck": goals.Luck = value; break;
            }
        }
    }
}

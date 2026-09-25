// TazUO addition: multi-source restock agent with per-item destinations.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    internal static class RestockAgentManager
    {
        private static readonly Layer[] _equipmentLayers =
        {
            Layer.OneHanded, Layer.TwoHanded, Layer.Shoes, Layer.Pants, Layer.Shirt,
            Layer.Helmet, Layer.Gloves, Layer.Ring, Layer.Talisman, Layer.Necklace,
            Layer.Waist, Layer.Torso, Layer.Bracelet, Layer.Tunic, Layer.Earrings,
            Layer.Arms, Layer.Cloak, Layer.Robe, Layer.Skirt, Layer.Legs
        };

        private static readonly RestockPreset[] _presets =
        {
            new RestockPreset("Bandages", new[]
            {
                Entry(0x0E21, "Bandages", 100)
            }),
            new RestockPreset("Mage Reagents", new[]
            {
                Entry(0x0F7A, "Black Pearl", 50),
                Entry(0x0F7B, "Blood Moss", 50),
                Entry(0x0F84, "Garlic", 50),
                Entry(0x0F85, "Ginseng", 50),
                Entry(0x0F86, "Mandrake Root", 50),
                Entry(0x0F88, "Nightshade", 50),
                Entry(0x0F8C, "Sulfurous Ash", 50),
                Entry(0x0F8D, "Spider's Silk", 50)
            }),
            new RestockPreset("Necro Reagents", new[]
            {
                Entry(0x0F78, "Bat Wing", 30),
                Entry(0x0F8F, "Grave Dust", 30),
                Entry(0x0F7D, "Daemon Blood", 30),
                Entry(0x0F8E, "Nox Crystal", 30),
                Entry(0x0F8A, "Pig Iron", 30)
            }),
            new RestockPreset("Ammunition", new[]
            {
                Entry(0x0F3F, "Arrows", 100),
                Entry(0x1BFB, "Crossbow Bolts", 100)
            }),
            new RestockPreset("Combat Potions", new[]
            {
                Entry(0x0F0C, "Heal Potions", 10),
                Entry(0x0F07, "Cure Potions", 10),
                Entry(0x0F0B, "Refresh Potions", 10),
                Entry(0x0F08, "Agility Potions", 10),
                Entry(0x0F09, "Strength Potions", 10)
            })
        };

        internal static RestockSettings Settings { get; private set; } = new RestockSettings();
        internal static IReadOnlyList<RestockPreset> Presets => _presets;
        internal static IReadOnlyList<RestockLoadout> Loadouts => Settings.Loadouts;
        private static bool _verificationPending;
        private static long _verifyAt;
        private static int _verificationRetries;

        internal static string ActiveLoadoutDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Settings.ActiveLoadoutName))
                {
                    return "Unsaved setup";
                }

                RestockLoadout loadout = Settings.Loadouts.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, Settings.ActiveLoadoutName,
                        StringComparison.OrdinalIgnoreCase));

                return loadout == null
                    ? "Unsaved setup"
                    : EntriesEqual(Settings.Items, loadout.Items)
                        && (!loadout.HasSourcePriority
                            || Settings.SourceSerials.SequenceEqual(loadout.SourceSerials))
                        ? loadout.Name
                        : loadout.Name + " • modified";
            }
        }

        private const string SAVE_FILE = "RestockAgent.json";

        internal static void Load()
        {
            Settings = new RestockSettings();

            try
            {
                string json = ProfileDataStore.ReadAllText(SAVE_FILE);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    Settings = JsonSerializer.Deserialize(
                        json,
                        RestockAgentJsonContext.Default.RestockSettings
                    ) ?? new RestockSettings();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to load restock agent: {ex.Message}");
                Settings = new RestockSettings();
            }

            Settings.Items ??= new List<RestockEntry>();
            Settings.SourceSerials ??= new List<uint>();
            Settings.RequiredEquipmentLayers ??= new List<byte>();
            Settings.Loadouts ??= new List<RestockLoadout>();
            Settings.ActiveLoadoutName ??= string.Empty;

            if (Settings.SourceSerial != 0 && !Settings.SourceSerials.Contains(Settings.SourceSerial))
            {
                Settings.SourceSerials.Add(Settings.SourceSerial);
            }

            Settings.SourceSerials = Settings.SourceSerials.Where(serial => serial != 0)
                .Distinct().ToList();
            Settings.Items = Settings.Items.Where(entry => entry != null).ToList();

            foreach (RestockEntry entry in Settings.Items)
            {
                NormalizeEntry(entry);
            }

            Settings.Loadouts = Settings.Loadouts
                .Where(loadout => loadout != null && !string.IsNullOrWhiteSpace(loadout.Name))
                .GroupBy(loadout => loadout.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (RestockLoadout loadout in Settings.Loadouts)
            {
                loadout.Name = loadout.Name.Trim();
                loadout.Items ??= new List<RestockEntry>();
                loadout.SourceSerials ??= new List<uint>();
                loadout.Items = loadout.Items.Where(entry => entry != null).ToList();

                foreach (RestockEntry entry in loadout.Items)
                {
                    NormalizeEntry(entry);
                }
            }

            if (!Settings.Loadouts.Any(loadout => string.Equals(
                loadout.Name, Settings.ActiveLoadoutName,
                StringComparison.OrdinalIgnoreCase)))
            {
                Settings.ActiveLoadoutName = string.Empty;
            }
        }

        internal static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(
                    Settings,
                    RestockAgentJsonContext.Default.RestockSettings
                );

                if (!ProfileDataStore.WriteAllText(SAVE_FILE, json))
                {
                    Log.Error("Unable to save restock agent.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to save restock agent: {ex.Message}");
            }
        }

        internal static void Unload()
        {
            Save();
            Settings = new RestockSettings();
            _verificationPending = false;
            _verificationRetries = 0;
            _verifyAt = 0;
        }

        internal static bool AddSource(Item source)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (source == null || source.IsDestroyed || !source.ItemData.IsContainer
                || (backpack != null && (source.Serial == backpack.Serial
                    || IsInside(source, backpack.Serial)))
                || Settings.SourceSerials.Contains(source.Serial))
            {
                return false;
            }

            Settings.SourceSerials.Add(source.Serial);
            Settings.SourceSerial = Settings.SourceSerials[0];
            Save();
            return true;
        }

        internal static void ClearSources()
        {
            Settings.SourceSerials.Clear();
            Settings.SourceSerial = 0;
            Save();
        }

        internal static bool AddItem(Item item)
        {
            if (item == null || item.IsDestroyed)
            {
                return false;
            }

            RestockEntry existing = Settings.Items.FirstOrDefault(entry =>
                entry.Graphic == item.Graphic
                && (entry.MatchAnyHue || entry.Hue == item.Hue)
            );

            if (existing != null)
            {
                return false;
            }

            Settings.Items.Add(new RestockEntry
            {
                Graphic = item.Graphic,
                Hue = item.Hue,
                MatchAnyHue = false,
                Name = GetItemName(item),
                DesiredAmount = item.ItemData.IsStackable ? (ushort)20 : (ushort)1
            });
            Save();
            return true;
        }

        internal static int AddPreset(int index)
        {
            if (index < 0 || index >= _presets.Length)
            {
                return 0;
            }

            int added = 0;

            foreach (RestockEntry presetEntry in _presets[index].Items)
            {
                if (Settings.Items.Any(entry => entry.Graphic == presetEntry.Graphic))
                {
                    continue;
                }

                Settings.Items.Add(presetEntry.Copy());
                added++;
            }

            if (added > 0)
            {
                Save();
            }

            return added;
        }

        internal static void Remove(RestockEntry entry)
        {
            if (entry != null && Settings.Items.Remove(entry))
            {
                Save();
            }
        }

        internal static bool AddLoadout(string name)
        {
            name = name?.Trim();

            if (string.IsNullOrEmpty(name) || Settings.Loadouts.Any(loadout =>
                string.Equals(loadout.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            Settings.Loadouts.Add(new RestockLoadout
            {
                Name = name,
                Items = CopyEntries(Settings.Items),
                SourceSerials = Settings.SourceSerials.ToList(),
                HasSourcePriority = true
            });
            Settings.ActiveLoadoutName = name;
            Save();
            return true;
        }

        internal static bool UpdateLoadout(int index, string name)
        {
            name = name?.Trim();

            if (index < 0 || index >= Settings.Loadouts.Count
                || string.IsNullOrEmpty(name)
                || Settings.Loadouts.Where((loadout, loadoutIndex) => loadoutIndex != index)
                    .Any(loadout => string.Equals(
                        loadout.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            RestockLoadout loadout = Settings.Loadouts[index];
            loadout.Name = name;
            loadout.Items = CopyEntries(Settings.Items);
            loadout.SourceSerials = Settings.SourceSerials.ToList();
            loadout.HasSourcePriority = true;
            Settings.ActiveLoadoutName = name;
            Save();
            return true;
        }

        internal static bool LoadLoadout(int index)
        {
            if (index < 0 || index >= Settings.Loadouts.Count)
            {
                return false;
            }

            RestockLoadout loadout = Settings.Loadouts[index];
            Settings.Items = CopyEntries(loadout.Items);
            if (loadout.HasSourcePriority)
            {
                Settings.SourceSerials = loadout.SourceSerials.Where(serial => serial != 0)
                    .Distinct().ToList();
                Settings.SourceSerial = Settings.SourceSerials.FirstOrDefault();
            }
            Settings.ActiveLoadoutName = loadout.Name;
            Save();
            return true;
        }

        internal static bool DeleteLoadout(int index)
        {
            if (index < 0 || index >= Settings.Loadouts.Count)
            {
                return false;
            }

            string name = Settings.Loadouts[index].Name;
            Settings.Loadouts.RemoveAt(index);

            if (string.Equals(Settings.ActiveLoadoutName, name,
                StringComparison.OrdinalIgnoreCase))
            {
                Settings.ActiveLoadoutName = string.Empty;
            }

            Save();
            return true;
        }

        internal static int CountInBackpack(RestockEntry entry)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);
            return backpack == null ? 0 : CountRecursive(backpack, entry);
        }

        internal static int CountInTarget(RestockEntry entry)
        {
            Item target = ResolveTarget(entry);
            return target == null ? 0 : CountRecursive(target, entry);
        }

        internal static int CountInSources(RestockEntry entry)
        {
            int count = 0;
            var seen = new HashSet<uint>();

            foreach (Item source in GetLoadedSources())
            {
                count += CountRecursive(source, entry, seen);
            }

            return count;
        }

        internal static RestockCountSnapshot CreateCountSnapshot(IEnumerable<RestockEntry> entries)
        {
            var snapshot = new RestockCountSnapshot();
            List<RestockEntry> list = entries?.Where(entry => entry != null).ToList()
                ?? new List<RestockEntry>();
            foreach (RestockEntry entry in list)
            {
                snapshot.Target[entry] = 0;
                snapshot.Source[entry] = 0;
            }

            foreach (IGrouping<uint, RestockEntry> group in list.GroupBy(entry =>
                ResolveTarget(entry)?.Serial ?? 0))
            {
                Item target = World.Items.Get(group.Key);
                if (target != null) CountAllRecursive(target, group.ToList(), snapshot.Target,
                    new HashSet<uint>());
            }

            var sourceSeen = new HashSet<uint>();
            foreach (Item source in GetLoadedSources())
                CountAllRecursive(source, list, snapshot.Source, sourceSeen);
            return snapshot;
        }

        private static void CountAllRecursive(Item parent, List<RestockEntry> entries,
            Dictionary<RestockEntry, int> counts, HashSet<uint> seen)
        {
            for (var node = parent.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed || !seen.Add(item.Serial)) continue;
                int amount = item.ItemData.IsStackable ? Math.Max(1, (int)item.Amount) : 1;
                foreach (RestockEntry entry in entries)
                    if (entry.IsMatch(item)) counts[entry] += amount;
                if (!item.IsEmpty) CountAllRecursive(item, entries, counts, seen);
            }
        }

        internal static int CountBackpackItems()
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);
            return backpack == null ? 0 : CountItemsRecursive(backpack);
        }

        internal static int CaptureEquipmentBaseline()
        {
            Settings.RequiredEquipmentLayers.Clear();

            if (World.Player != null)
            {
                foreach (Layer layer in _equipmentLayers)
                {
                    if (World.Player.FindItemByLayer(layer) != null)
                    {
                        Settings.RequiredEquipmentLayers.Add((byte)layer);
                    }
                }
            }

            Save();
            return Settings.RequiredEquipmentLayers.Count;
        }

        internal static string SourceDescription()
        {
            if (Settings.SourceSerials.Count == 0)
            {
                return "No sources selected";
            }

            int loaded = GetLoadedSources().Count;
            string names = string.Join(", ", Settings.SourceSerials.Take(3).Select(serial =>
            {
                Item source = World.Items.Get(serial);
                return source == null ? $"0x{serial:X8}" : GetItemName(source);
            }));
            string more = Settings.SourceSerials.Count > 3 ? $" +{Settings.SourceSerials.Count - 3}" : string.Empty;
            return $"{Settings.SourceSerials.Count} source(s), {loaded} loaded: {names}{more}";
        }

        internal static string DestinationDescription(RestockEntry entry)
        {
            if (entry == null || entry.TargetContainerSerial == 0)
            {
                return "Backpack";
            }

            Item target = World.Items.Get(entry.TargetContainerSerial);
            return target == null ? "Unavailable" : GetItemName(target);
        }

        internal static bool SetDestination(RestockEntry entry, Item target)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (entry == null || backpack == null || target == null || target.IsDestroyed
                || !target.ItemData.IsContainer
                || (target.Serial != backpack.Serial && !IsInside(target, backpack.Serial)))
            {
                return false;
            }

            entry.TargetContainerSerial = target.Serial == backpack.Serial ? 0 : target.Serial;
            Save();
            return true;
        }

        internal static void OpenSources()
        {
            foreach (Item source in GetLoadedSources())
            {
                GameActions.DoubleClick(source.Serial);
            }
        }

        internal static bool MoveSource(int index, int offset)
        {
            int target = index + offset;
            if (index < 0 || index >= Settings.SourceSerials.Count
                || target < 0 || target >= Settings.SourceSerials.Count) return false;
            uint serial = Settings.SourceSerials[index];
            Settings.SourceSerials.RemoveAt(index);
            Settings.SourceSerials.Insert(target, serial);
            Settings.SourceSerial = Settings.SourceSerials.FirstOrDefault();
            Save();
            return true;
        }

        internal static bool RemoveSourceAt(int index)
        {
            if (index < 0 || index >= Settings.SourceSerials.Count) return false;
            Settings.SourceSerials.RemoveAt(index);
            Settings.SourceSerial = Settings.SourceSerials.FirstOrDefault();
            Save();
            return true;
        }

        internal static bool Run() => Run(false);

        private static bool Run(bool verificationRetry)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (backpack == null)
            {
                GameActions.Print("Restock: backpack not found.", 0x21);
                return false;
            }

            List<Item> sources = GetLoadedSources();

            if (sources.Count == 0)
            {
                GameActions.Print("Restock: add and open at least one source container first.", 0x21);
                return false;
            }

            if (sources.Any(source => source.Serial == backpack.Serial
                || IsInside(source, backpack.Serial)))
            {
                GameActions.Print("Restock: source containers must be outside your backpack.", 0x21);
                return false;
            }

            if (MoveItemQueue.Instance == null)
            {
                GameActions.Print("Restock: item move queue is unavailable.", 0x21);
                return false;
            }

            int queuedUnits = 0;
            int completedEntries = 0;
            int activeEntries = Settings.Items.Count(entry => entry.DesiredAmount > 0);

            if (activeEntries == 0)
            {
                GameActions.Print("Restock: no active targets configured.", 0x21);
                return false;
            }

            foreach (RestockEntry entry in Settings.Items)
            {
                if (entry.DesiredAmount == 0)
                {
                    continue;
                }

                Item target = ResolveTarget(entry);

                if (target == null)
                {
                    GameActions.Print($"Restock: target container for {entry.Name} is unavailable.", 0x21);
                    continue;
                }

                int deficit = entry.DesiredAmount - CountRecursive(target, entry);

                if (deficit <= 0)
                {
                    completedEntries++;
                    continue;
                }

                var seen = new HashSet<uint>();

                foreach (Item item in sources.SelectMany(source => FindRecursive(source, entry, seen)))
                {
                    if (deficit <= 0)
                    {
                        break;
                    }

                    ushort available = item.ItemData.IsStackable
                        ? (ushort)Math.Max(1, (int)item.Amount)
                        : (ushort)1;
                    ushort amount = (ushort)Math.Min(deficit, available);
                    MoveItemQueue.Instance.Enqueue(
                        item.Serial,
                        target.Serial,
                        amount,
                        0xFFFF,
                        0xFFFF,
                        0
                    );
                    deficit -= amount;
                    queuedUnits += amount;
                }

                if (deficit <= 0)
                {
                    completedEntries++;
                }
            }

            if (queuedUnits > 0)
            {
                _verificationPending = true;
                _verifyAt = (long)Time.Ticks + 2000;
                if (!verificationRetry) _verificationRetries = 1;
                GameActions.Print(
                    $"Restock: queued {queuedUnits} item(s); {completedEntries}/{activeEntries} targets satisfied.",
                    0x35
                );
                return true;
            }

            GameActions.Print("Restock: nothing to move. Targets are filled or source is empty.", 0x35);
            return false;
        }

        internal static void Tick()
        {
            if (!_verificationPending || Time.Ticks < _verifyAt
                || (MoveItemQueue.Instance != null && !MoveItemQueue.Instance.IsEmpty)) return;

            _verificationPending = false;
            int active = Settings.Items.Count(entry => entry.DesiredAmount > 0);
            int ready = Settings.Items.Count(entry => entry.DesiredAmount > 0
                && CountInTarget(entry) >= entry.DesiredAmount);
            if (active > 0 && ready == active)
            {
                GameActions.Print($"Restock verified: {ready}/{active} targets ready.", 0x35);
                return;
            }

            if (_verificationRetries > 0)
            {
                _verificationRetries--;
                GameActions.Print($"Restock verification: {ready}/{active} ready; retrying missing items once.", 0x35);
                if (!Run(true))
                    GameActions.Print($"Restock incomplete: {ready}/{active} targets ready.", 0x21);
                return;
            }

            GameActions.Print($"Restock incomplete after retry: {ready}/{active} targets ready.", 0x21);
        }

        private static RestockEntry Entry(ushort graphic, string name, ushort desired) =>
            new RestockEntry
            {
                Graphic = graphic,
                Hue = 0,
                MatchAnyHue = true,
                Name = name,
                DesiredAmount = desired
            };

        private static List<RestockEntry> CopyEntries(IEnumerable<RestockEntry> entries)
        {
            return entries?.Where(entry => entry != null)
                .Select(entry => entry.Copy()).ToList()
                ?? new List<RestockEntry>();
        }

        private static bool EntriesEqual(
            IReadOnlyList<RestockEntry> left, IReadOnlyList<RestockEntry> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                RestockEntry a = left[i];
                RestockEntry b = right[i];

                if (a == null || b == null || a.Graphic != b.Graphic || a.Hue != b.Hue
                    || a.MatchAnyHue != b.MatchAnyHue || a.DesiredAmount != b.DesiredAmount
                    || a.TargetContainerSerial != b.TargetContainerSerial
                    || !string.Equals(a.Name, b.Name, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void NormalizeEntry(RestockEntry entry)
        {
            if (entry != null && entry.Hue == ushort.MaxValue)
            {
                entry.Hue = 0;
                entry.MatchAnyHue = true;
            }
        }

        private static int CountRecursive(Item parent, RestockEntry entry)
        {
            return CountRecursive(parent, entry, new HashSet<uint>());
        }

        private static int CountRecursive(Item parent, RestockEntry entry, HashSet<uint> seen)
        {
            int count = 0;

            for (var node = parent.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed || !seen.Add(item.Serial))
                {
                    continue;
                }

                if (entry.IsMatch(item))
                {
                    count += item.ItemData.IsStackable ? Math.Max(1, (int)item.Amount) : 1;
                }

                if (!item.IsEmpty)
                {
                    count += CountRecursive(item, entry, seen);
                }
            }

            return count;
        }

        private static int CountItemsRecursive(Item parent)
        {
            int count = 0;

            for (var node = parent.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed)
                {
                    continue;
                }

                count++;

                if (!item.IsEmpty)
                {
                    count += CountItemsRecursive(item);
                }
            }

            return count;
        }

        private static IEnumerable<Item> FindRecursive(Item parent, RestockEntry entry,
            HashSet<uint> seen)
        {
            for (var node = parent.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed || !seen.Add(item.Serial))
                {
                    continue;
                }

                if (entry.IsMatch(item))
                {
                    yield return item;
                }

                if (!item.IsEmpty)
                {
                    foreach (Item nested in FindRecursive(item, entry, seen))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static List<Item> GetLoadedSources()
        {
            return Settings.SourceSerials.Select(serial => World.Items.Get(serial))
                .Where(source => source != null && !source.IsDestroyed)
                .ToList();
        }

        private static Item ResolveTarget(RestockEntry entry)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (backpack == null || entry == null || entry.TargetContainerSerial == 0)
            {
                return backpack;
            }

            Item target = World.Items.Get(entry.TargetContainerSerial);
            return target != null && !target.IsDestroyed
                && (target.Serial == backpack.Serial || IsInside(target, backpack.Serial))
                ? target
                : null;
        }

        private static bool IsInside(Item item, uint containerSerial)
        {
            var seen = new HashSet<uint>();
            uint serial = item?.Container ?? 0;

            while (SerialHelper.IsItem(serial) && seen.Add(serial))
            {
                if (serial == containerSerial)
                {
                    return true;
                }

                Item parent = World.Items.Get(serial);
                serial = parent?.Container ?? 0;
            }

            return false;
        }

        private static string GetItemName(Item item)
        {
            if (World.OPL.TryGetNameAndData(item.Serial, out string name, out _)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(item.Name))
            {
                return item.Name.Trim();
            }

            return string.IsNullOrWhiteSpace(item.ItemData.Name)
                ? $"Item 0x{item.Graphic:X4}"
                : item.ItemData.Name.Trim();
        }
    }

    internal sealed class RestockSettings
    {
        public uint SourceSerial { get; set; }
        public List<uint> SourceSerials { get; set; } = new List<uint>();
        public List<RestockEntry> Items { get; set; } = new List<RestockEntry>();
        public List<byte> RequiredEquipmentLayers { get; set; } = new List<byte>();
        public List<RestockLoadout> Loadouts { get; set; } = new List<RestockLoadout>();
        public string ActiveLoadoutName { get; set; } = string.Empty;
    }

    internal sealed class RestockCountSnapshot
    {
        internal readonly Dictionary<RestockEntry, int> Target = new Dictionary<RestockEntry, int>();
        internal readonly Dictionary<RestockEntry, int> Source = new Dictionary<RestockEntry, int>();
    }

    internal sealed class RestockLoadout
    {
        public string Name { get; set; } = "Loadout";
        public List<RestockEntry> Items { get; set; } = new List<RestockEntry>();
        public List<uint> SourceSerials { get; set; } = new List<uint>();
        public bool HasSourcePriority { get; set; }
    }

    internal sealed class RestockEntry
    {
        public ushort Graphic { get; set; }
        public ushort Hue { get; set; }
        public bool MatchAnyHue { get; set; }
        public string Name { get; set; } = "Item";
        public ushort DesiredAmount { get; set; } = 1;
        public uint TargetContainerSerial { get; set; }

        internal bool IsMatch(Item item)
        {
            if (item.Graphic != Graphic || !MatchAnyHue && item.Hue != Hue)
                return false;

            if (!item.ItemData.IsWeapon)
                return true;

            return DurabilityManager.TryGetItemDurability(item, out int current, out _)
                && DurabilityManager.MeetsReadinessMinimum(current, true);
        }

        internal RestockEntry Copy() => new RestockEntry
        {
            Graphic = Graphic,
            Hue = Hue,
            MatchAnyHue = MatchAnyHue,
            Name = Name,
            DesiredAmount = DesiredAmount,
            TargetContainerSerial = TargetContainerSerial
        };
    }

    internal sealed class RestockPreset
    {
        internal RestockPreset(string name, RestockEntry[] items)
        {
            Name = name;
            Items = items;
        }

        internal string Name { get; }
        internal RestockEntry[] Items { get; }
    }

    [JsonSerializable(typeof(RestockSettings))]
    [JsonSerializable(typeof(RestockLoadout))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    internal sealed partial class RestockAgentJsonContext : JsonSerializerContext
    {
    }
}

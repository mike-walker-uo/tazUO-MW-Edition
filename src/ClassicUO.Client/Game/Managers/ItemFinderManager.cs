// TazUO addition: multi-property item search over loaded player containers.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal static class ItemFinderManager
    {
        private const int MAX_PROPERTY_REQUESTS = 40;
        private const int SCAN_RANGE = 2;
        private const int LOCATE_SECONDS = 12;
        private const uint ITEM_FINDER_ARROW_SERIAL = 0xFFFF_FFFD;
        private const string CATALOG_FILE = "item_finder_catalog.tsv";
        private const string SAVED_SEARCHES_FILE = "item_finder_searches.tsv";

        private static readonly Dictionary<uint, IndexedItem> _catalog =
            new Dictionary<uint, IndexedItem>();
        private static readonly Dictionary<uint, HighlightState> _highlights =
            new Dictionary<uint, HighlightState>();
        private static uint _locatedItemSerial;
        private static uint _locatedContainerSerial;
        private static long _locatedItemExpires;
        private static bool _locatedItemRevealed;
        private static uint _catalogOwner;
        private static string _catalogProfilePath;
        private static readonly List<SavedSearch> _savedSearches = new List<SavedSearch>();
        private static uint _savedSearchOwner;
        private static string _savedSearchProfilePath;

        private static readonly Regex _conditionPattern = new Regex(
            @"^([a-zA-Z][a-zA-Z0-9]*)(>=|<=|=|>|<)(-?\d+(?:[.,]\d+)?)$",
            RegexOptions.Compiled
        );

        private static readonly Regex _countGroupPattern = new Regex(
            @"^(\d+)of\((.*)\)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Dictionary<string, string> _propertyAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "hci", "hitchanceincrease" },
                { "dci", "defensechanceincrease" },
                { "di", "damageincrease" },
                { "ssi", "swingspeedincrease" },
                { "lmc", "lowermanacost" },
                { "lrc", "lowerreagentcost" },
                { "fc", "fastercasting" },
                { "fcr", "fastercastrecovery" },
                { "sdi", "spelldamageincrease" },
                { "mr", "manaregeneration" },
                { "hpregen", "hitpointregeneration" },
                { "stamregen", "staminaregeneration" }
            };

        internal sealed class SearchResult
        {
            internal Item Item;
            internal uint Serial;
            internal uint ContainerSerial;
            internal uint RootContainerSerial;
            internal ushort Graphic;
            internal ushort Hue;
            internal ushort Amount;
            internal string Name;
            internal string Location;
            internal string Properties;
            internal string AllProperties;
            internal bool HasWorldPosition;
            internal int RootX;
            internal int RootY;
            internal sbyte RootZ;
            internal int MapIndex;
        }

        internal sealed class CatalogSource
        {
            internal uint RootSerial;
            internal string Location;
            internal int ItemCount;
            internal long LastSeenUtcTicks;
            internal bool HasWorldPosition;
            internal int RootX;
            internal int RootY;
            internal int MapIndex;
        }

        internal sealed class SavedSearch
        {
            internal string Name;
            internal string Query;
        }

        internal sealed class SearchableProperty
        {
            internal string Name;
            internal string Key;
            internal int ItemCount;
        }

        internal sealed class ScanReport
        {
            internal readonly List<uint> OpenedContainerSerials = new List<uint>();
            internal int Containers;
            internal int Items;
            internal int NewItems;
            internal int RequestedProperties;
            internal int OpenedContainers;
            internal int RemovedItems;
            internal int TotalItems => _catalog.Count;
        }

        internal sealed class SearchReport
        {
            internal readonly List<SearchResult> Results = new List<SearchResult>();
            internal int MissingProperties;
            internal int UnloadedProperties;
            internal int RequestedProperties;
            internal string LogicSummary;
        }

        private sealed class Query
        {
            internal readonly List<string> RequiredTextTerms = new List<string>();
            internal readonly List<string> ExcludedTextTerms = new List<string>();
            internal readonly List<PropertyCondition> Conditions = new List<PropertyCondition>();
            internal readonly List<CountGroup> CountGroups = new List<CountGroup>();

            internal bool NeedsProperties => Conditions.Count > 0
                || CountGroups.Count > 0
                || ExcludedTextTerms.Count > 0;
        }

        private sealed class PropertyCondition
        {
            internal string Label;
            internal string PropertyName;
            internal string Operator;
            internal double Value;
            internal bool Negated;
        }

        private sealed class CountGroup
        {
            internal int Required;
            internal readonly List<PropertyCondition> Conditions = new List<PropertyCondition>();
        }

        private sealed class IndexedItem
        {
            internal uint Serial;
            internal uint ContainerSerial;
            internal uint RootContainerSerial;
            internal ushort Graphic;
            internal ushort Hue;
            internal ushort Amount;
            internal string Name;
            internal string FallbackText;
            internal string PropertyData;
            internal string FormattedProperties;
            internal ItemPropertiesData ParsedProperties;
            internal string Location;
            internal bool HasWorldPosition;
            internal int RootX;
            internal int RootY;
            internal sbyte RootZ;
            internal int MapIndex;
            internal long LastSeenUtcTicks;
        }

        private sealed class HighlightState
        {
            internal Item Item;
            internal bool Matched;
            internal Color Color;
            internal ushort Hue;
            internal long Expires;
        }

        internal static int CatalogCount => _catalog.Count;

        internal static string CatalogItemName(uint serial)
        {
            EnsureCatalogOwner();
            return _catalog.TryGetValue(serial, out IndexedItem item)
                && !string.IsNullOrWhiteSpace(item.Name)
                    ? item.Name
                    : $"Item 0x{serial:X8}";
        }

        internal static List<CatalogSource> GetCatalogSources()
        {
            EnsureCatalogOwner();
            return _catalog.Values
                .GroupBy(item => item.RootContainerSerial)
                .Select(group =>
                {
                    IndexedItem newest = group.OrderByDescending(item => item.LastSeenUtcTicks).First();
                    return new CatalogSource
                    {
                        RootSerial = group.Key,
                        Location = newest.Location,
                        ItemCount = group.Count(),
                        LastSeenUtcTicks = newest.LastSeenUtcTicks,
                        HasWorldPosition = newest.HasWorldPosition,
                        RootX = newest.RootX,
                        RootY = newest.RootY,
                        MapIndex = newest.MapIndex
                    };
                })
                .OrderBy(source => source.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static int ForgetCatalogSource(uint rootSerial)
        {
            EnsureCatalogOwner();
            List<uint> serials = _catalog.Values
                .Where(item => item.RootContainerSerial == rootSerial)
                .Select(item => item.Serial).ToList();
            foreach (uint serial in serials) _catalog.Remove(serial);
            if (serials.Count > 0) SaveCatalog();
            return serials.Count;
        }

        internal static List<SearchableProperty> GetSearchableProperties()
        {
            EnsureCatalogOwner();
            var properties = new Dictionary<string, SearchableProperty>(
                StringComparer.OrdinalIgnoreCase);

            foreach (IndexedItem indexed in _catalog.Values)
            {
                ItemPropertiesData itemProperties = GetParsedProperties(indexed);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (ItemPropertiesData.SinglePropertyData property in itemProperties.singlePropertyData)
                {
                    if (property.FirstValue == double.MinValue)
                    {
                        continue;
                    }

                    string key = Normalize(property.Name);

                    if (string.IsNullOrEmpty(key) || !seen.Add(key))
                    {
                        continue;
                    }

                    if (!properties.TryGetValue(key, out SearchableProperty searchable))
                    {
                        searchable = new SearchableProperty
                        {
                            Name = property.Name?.Trim() ?? key,
                            Key = key
                        };
                        properties.Add(key, searchable);
                    }

                    searchable.ItemCount++;
                }
            }

            return properties.Values
                .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static bool IsLocateTarget(uint serial)
        {
            return serial != 0 && serial == _locatedItemSerial
                && (long)Time.Ticks < _locatedItemExpires;
        }

        internal static List<uint> GetReachableContainerSerials()
        {
            var containers = new List<Item>();

            if (World.Player == null)
            {
                return new List<uint>();
            }

            foreach (Item item in World.Items.Values)
            {
                if (item != null && !item.IsDestroyed && item.OnGround
                    && item.ItemData.IsContainer && item.Distance <= SCAN_RANGE)
                {
                    containers.Add(item);
                }
            }

            containers.Sort((left, right) =>
            {
                int distance = left.Distance.CompareTo(right.Distance);
                return distance != 0 ? distance : left.Serial.CompareTo(right.Serial);
            });

            return containers.Select(item => item.Serial).ToList();
        }

        internal static IReadOnlyList<SavedSearch> SavedSearches
        {
            get
            {
                EnsureSavedSearchOwner();
                return _savedSearches;
            }
        }

        internal static bool AddSavedSearch(string name, string query)
        {
            EnsureSavedSearchOwner();
            name = name?.Trim();
            query = query?.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(query)
                || _savedSearches.Any(search => string.Equals(
                    search.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var search = new SavedSearch { Name = name, Query = query };
            _savedSearches.Add(search);

            if (SaveSavedSearches())
            {
                return true;
            }

            _savedSearches.Remove(search);
            return false;
        }

        internal static bool UpdateSavedSearch(int index, string name, string query)
        {
            EnsureSavedSearchOwner();
            name = name?.Trim();
            query = query?.Trim();

            if (index < 0 || index >= _savedSearches.Count
                || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(query)
                || _savedSearches.Where((search, searchIndex) => searchIndex != index)
                    .Any(search => string.Equals(
                        search.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            SavedSearch search = _savedSearches[index];
            string oldName = search.Name;
            string oldQuery = search.Query;
            search.Name = name;
            search.Query = query;

            if (SaveSavedSearches())
            {
                return true;
            }

            search.Name = oldName;
            search.Query = oldQuery;
            return false;
        }

        internal static bool DeleteSavedSearch(int index)
        {
            EnsureSavedSearchOwner();

            if (index < 0 || index >= _savedSearches.Count)
            {
                return false;
            }

            SavedSearch search = _savedSearches[index];
            _savedSearches.RemoveAt(index);

            if (SaveSavedSearches())
            {
                return true;
            }

            _savedSearches.Insert(index, search);
            return false;
        }

        internal static ScanReport ScanReachable(ISet<uint> requestedSerials = null,
            bool openContainers = false, bool pruneOpenedContainers = false)
        {
            var report = new ScanReport();

            if (World.Player == null)
            {
                return report;
            }

            EnsureCatalogOwner();

            var seen = new HashSet<uint>();
            var scannedContainers = new Dictionary<uint, HashSet<uint>>();

            for (var node = World.Player.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed)
                {
                    continue;
                }

                string location = item.Layer == Layer.Backpack
                    ? "Backpack"
                    : item.Layer == Layer.Bank ? "Bank" : "Equipped";
                IndexWalk(item, location, item.Serial, true, report, seen, requestedSerials,
                    false, 0, 0, 0, World.MapIndex);
            }

            foreach (Item container in World.Items.Values)
            {
                if (container == null || container.IsDestroyed || !container.OnGround
                    || !container.ItemData.IsContainer || container.Distance > SCAN_RANGE)
                {
                    continue;
                }

                report.Containers++;

                if (openContainers)
                {
                    GameActions.DoubleClick(container.Serial);
                    report.OpenedContainerSerials.Add(container.Serial);
                    report.OpenedContainers++;
                }

                string name = GetDisplayName(container, true);
                string location = $"Container: {name} • {GetFacetName(World.MapIndex)} "
                    + $"{container.X}, {container.Y}";
                IndexWalk(container, location, container.Serial, true, report, seen,
                    requestedSerials, true, container.X, container.Y, container.Z,
                    World.MapIndex);

                if (pruneOpenedContainers && container.Opened)
                {
                    var completeScan = new Dictionary<uint, HashSet<uint>>();

                    if (TryCollectFullyOpenedContainerContents(container, completeScan))
                    {
                        foreach (KeyValuePair<uint, HashSet<uint>> scanned in completeScan)
                        {
                            scannedContainers[scanned.Key] = scanned.Value;
                        }
                    }
                }
            }

            if (scannedContainers.Count > 0)
            {
                var stale = new HashSet<uint>();

                foreach (KeyValuePair<uint, HashSet<uint>> scanned in scannedContainers)
                {
                    foreach (IndexedItem item in _catalog.Values)
                    {
                        if (item.ContainerSerial == scanned.Key
                            && !scanned.Value.Contains(item.Serial))
                        {
                            stale.Add(item.Serial);
                        }
                    }
                }

                bool added;

                do
                {
                    added = false;

                    foreach (IndexedItem item in _catalog.Values)
                    {
                        if (stale.Contains(item.ContainerSerial) && stale.Add(item.Serial))
                        {
                            added = true;
                        }
                    }
                }
                while (added);

                foreach (uint serial in stale)
                {
                    _catalog.Remove(serial);
                }

                report.RemovedItems = stale.Count;
            }

            SaveCatalog();
            return report;
        }

        internal static bool TrySearch(
            string text,
            ISet<uint> requestedSerials,
            out SearchReport report,
            out string error)
        {
            report = new SearchReport();

            if (!TryParse(text, out Query query, out error))
            {
                return false;
            }

            report.LogicSummary = Describe(query);

            if (World.Player == null)
            {
                error = "Log in before searching.";
                return false;
            }

            EnsureCatalogOwner();

            if (_catalog.Count == 0)
            {
                ScanReachable(requestedSerials);
            }

            foreach (IndexedItem indexed in _catalog.Values)
            {
                RefreshIndexedItem(indexed);
                Evaluate(indexed, query, report, requestedSerials);
            }

            report.Results.Sort((left, right) =>
            {
                int location = string.Compare(left.Location, right.Location, StringComparison.OrdinalIgnoreCase);
                return location != 0
                    ? location
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            error = null;
            return true;
        }

        internal static void Locate(SearchResult result)
        {
            if (result == null)
            {
                return;
            }

            ClearHighlights();
            _locatedItemSerial = result.Serial;
            _locatedContainerSerial = result.ContainerSerial;
            _locatedItemExpires = (long)Time.Ticks + LOCATE_SECONDS * 1000;
            _locatedItemRevealed = false;

            Item item = World.Items.Get(result.Serial);
            Item container = World.Items.Get(result.ContainerSerial);
            Item root = World.Items.Get(result.RootContainerSerial);

            if (item != null && !item.IsDestroyed)
            {
                Highlight(item);
            }

            if (root != null && !root.IsDestroyed && root.Serial != item?.Serial)
            {
                Highlight(root);
            }

            if (World.Player != null && result.ContainerSerial == World.Player.Serial)
            {
                ClearTrackingArrow();
                GameActions.DoubleClick(World.Player.Serial);
                GameActions.Print($"Located {result.Name} on your paperdoll.", 0x0035);
                return;
            }

            Item open = container ?? root;

            if (open == null || open.IsDestroyed)
            {
                PointToStoredContainer(result);
                return;
            }

            Item worldContainer = root != null && root.OnGround ? root : open;

            if (worldContainer.OnGround && worldContainer.Distance > SCAN_RANGE)
            {
                PointToStoredContainer(result);
                return;
            }

            Highlight(open);
            ClearTrackingArrow();
            GameActions.DoubleClick(open.Serial);
            GameActions.Print(
                $"Located {result.Name}; the item pulses pink for {LOCATE_SECONDS} seconds.",
                0x0035);
        }

        private static void PointToStoredContainer(SearchResult result)
        {
            if (!result.HasWorldPosition || World.Player == null)
            {
                GameActions.Print(
                    $"{result.Name}: return to its scanned area and open the container.", 0x21);
                return;
            }

            string position = $"{GetFacetName(result.MapIndex)} "
                + $"{result.RootX}, {result.RootY}";

            if (result.MapIndex != World.MapIndex)
            {
                UIManager.GetGump<QuestArrowGump>(ITEM_FINDER_ARROW_SERIAL)?.Dispose();
                GameActions.Print(
                    $"{result.Name} is stored at {position}. Travel to that facet, then locate it again.",
                    0x21);
                return;
            }

            UIManager.GetGump<QuestArrowGump>(ITEM_FINDER_ARROW_SERIAL)?.Dispose();
            UIManager.Add(new QuestArrowGump(
                ITEM_FINDER_ARROW_SERIAL, result.RootX, result.RootY)
            {
                CanCloseWithRightClick = true
            });
            GameActions.Print(
                $"Tracking {result.Name}'s scanned container at {position}. Right-click the arrow to close it.",
                0x0035);
        }

        private static void ClearTrackingArrow()
        {
            UIManager.GetGump<QuestArrowGump>(ITEM_FINDER_ARROW_SERIAL)?.Dispose();
        }

        internal static void UpdateHighlights()
        {
            long now = (long)Time.Ticks;

            if (_locatedItemSerial != 0 && now < _locatedItemExpires)
            {
                Item located = World.Items.Get(_locatedItemSerial);

                if (located != null && !located.IsDestroyed
                    && !_highlights.ContainsKey(located.Serial))
                {
                    Highlight(located, _locatedItemExpires);
                }

                if (!_locatedItemRevealed)
                {
                    GridContainer grid = UIManager.GetGump<GridContainer>(
                        _locatedContainerSerial);

                    if (grid != null && !grid.IsDisposed
                        && grid.RevealItem(_locatedItemSerial))
                    {
                        _locatedItemRevealed = true;
                    }
                }
            }
            else if (_locatedItemSerial != 0)
            {
                _locatedItemSerial = 0;
                _locatedContainerSerial = 0;
                _locatedItemExpires = 0;
                _locatedItemRevealed = false;
            }

            foreach (uint serial in _highlights.Where(pair => pair.Value.Expires <= now)
                .Select(pair => pair.Key).ToArray())
            {
                HighlightState state = _highlights[serial];

                if (state.Item != null && !state.Item.IsDestroyed)
                {
                    state.Item.MatchesHighlightData = state.Matched;
                    state.Item.HighlightColor = state.Color;
                    state.Item.Hue = state.Hue;
                }

                _highlights.Remove(serial);
            }
        }

        internal static void ClearHighlights()
        {
            foreach (HighlightState state in _highlights.Values)
            {
                if (state.Item != null && !state.Item.IsDestroyed)
                {
                    state.Item.MatchesHighlightData = state.Matched;
                    state.Item.HighlightColor = state.Color;
                    state.Item.Hue = state.Hue;
                }
            }

            _highlights.Clear();
            _locatedItemSerial = 0;
            _locatedContainerSerial = 0;
            _locatedItemExpires = 0;
            _locatedItemRevealed = false;
        }

        private static void EnsureCatalogOwner()
        {
            if (World.Player == null)
            {
                return;
            }

            string profilePath = ProfileManager.ProfilePath ?? string.Empty;

            if (_catalogOwner == World.Player.Serial
                && string.Equals(_catalogProfilePath, profilePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ClearHighlights();
            _catalog.Clear();
            _catalogOwner = World.Player.Serial;
            _catalogProfilePath = profilePath;

            foreach (string line in ProfileDataStore.ReadAllLines(CATALOG_FILE))
            {
                string[] fields = line.Split('\t');

                if ((fields.Length != 10 && fields.Length != 15 && fields.Length != 16)
                    || !uint.TryParse(fields[0], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint serial)
                    || !uint.TryParse(fields[1], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint containerSerial)
                    || !uint.TryParse(fields[2], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint rootSerial)
                    || !ushort.TryParse(fields[3], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out ushort graphic)
                    || !ushort.TryParse(fields[4], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out ushort hue)
                    || !ushort.TryParse(fields[5], out ushort amount))
                {
                    continue;
                }

                try
                {
                    var indexed = new IndexedItem
                    {
                        Serial = serial,
                        ContainerSerial = containerSerial,
                        RootContainerSerial = rootSerial,
                        Graphic = graphic,
                        Hue = hue,
                        Amount = amount,
                        Name = Decode(fields[6]),
                        FallbackText = Decode(fields[7]),
                        PropertyData = Decode(fields[8]),
                        Location = Decode(fields[9])
                    };

                    if (fields.Length == 15 || fields.Length == 16)
                    {
                        if (fields[10] == "1"
                            && int.TryParse(fields[11], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int rootX)
                            && int.TryParse(fields[12], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int rootY)
                            && sbyte.TryParse(fields[13], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out sbyte rootZ)
                            && int.TryParse(fields[14], NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int mapIndex))
                        {
                            indexed.HasWorldPosition = true;
                            indexed.RootX = rootX;
                            indexed.RootY = rootY;
                            indexed.RootZ = rootZ;
                            indexed.MapIndex = mapIndex;
                        }
                    }

                    if (fields.Length == 16)
                    {
                        long.TryParse(fields[15], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out indexed.LastSeenUtcTicks);
                    }

                    _catalog[serial] = indexed;
                }
                catch (FormatException)
                {
                }
            }
        }

        private static void EnsureSavedSearchOwner()
        {
            if (World.Player == null)
            {
                return;
            }

            string profilePath = ProfileManager.ProfilePath ?? string.Empty;

            if (_savedSearchOwner == World.Player.Serial
                && string.Equals(_savedSearchProfilePath, profilePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _savedSearches.Clear();
            _savedSearchOwner = World.Player.Serial;
            _savedSearchProfilePath = profilePath;

            foreach (string line in ProfileDataStore.ReadAllLines(SAVED_SEARCHES_FILE))
            {
                string[] fields = line.Split('\t');

                if (fields.Length != 2)
                {
                    continue;
                }

                try
                {
                    string name = Decode(fields[0]).Trim();
                    string query = Decode(fields[1]).Trim();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(query))
                    {
                        _savedSearches.Add(new SavedSearch { Name = name, Query = query });
                    }
                }
                catch (FormatException)
                {
                }
            }
        }

        private static bool SaveSavedSearches()
        {
            return ProfileDataStore.Write(SAVED_SEARCHES_FILE, writer =>
            {
                foreach (SavedSearch search in _savedSearches)
                {
                    writer.WriteLine($"{Encode(search.Name)}\t{Encode(search.Query)}");
                }
            });
        }

        private static void SaveCatalog()
        {
            ProfileDataStore.Write(CATALOG_FILE, writer =>
            {
                foreach (IndexedItem item in _catalog.Values.OrderBy(item => item.Serial))
                {
                    writer.WriteLine(
                        $"{item.Serial:X8}\t{item.ContainerSerial:X8}\t{item.RootContainerSerial:X8}"
                        + $"\t{item.Graphic:X4}\t{item.Hue:X4}\t{item.Amount}"
                        + $"\t{Encode(item.Name)}\t{Encode(item.FallbackText)}"
                        + $"\t{Encode(item.PropertyData)}\t{Encode(item.Location)}"
                        + $"\t{(item.HasWorldPosition ? 1 : 0)}\t{item.RootX}\t{item.RootY}"
                        + $"\t{item.RootZ}\t{item.MapIndex}\t{item.LastSeenUtcTicks}");
                }
            });
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private static bool TryParse(string text, out Query query, out string error)
        {
            query = new Query();
            text = text?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                error = "Enter an item name, type, or property filter.";
                return false;
            }

            foreach (string originalToken in Tokenize(text))
            {
                string token = originalToken.Trim();

                if (token == "*")
                {
                    continue;
                }

                Match groupMatch = _countGroupPattern.Match(token);

                if (groupMatch.Success)
                {
                    if (!int.TryParse(groupMatch.Groups[1].Value, out int required))
                    {
                        error = $"Invalid count group '{token}'.";
                        return false;
                    }

                    string[] members = groupMatch.Groups[2].Value.Split(',');

                    if (members.Length == 0 || required < 1 || required > members.Length)
                    {
                        error = $"Count must be between 1 and {members.Length} in '{token}'.";
                        return false;
                    }

                    var group = new CountGroup { Required = required };

                    foreach (string member in members)
                    {
                        if (!TryParseCondition(member.Trim(), out PropertyCondition condition, out error))
                        {
                            error = $"{error} Count groups contain property filters separated by commas.";
                            return false;
                        }

                        group.Conditions.Add(condition);
                    }

                    query.CountGroups.Add(group);
                    continue;
                }

                bool negatedText = StripNegation(ref token);
                Match match = _conditionPattern.Match(token);

                if (!match.Success)
                {
                    if (token.IndexOfAny(new[] { '<', '>', '=' }) >= 0)
                    {
                        error = $"Invalid filter '{token}'. Example: hci>=10";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        error = $"Missing value after NOT in '{originalToken}'.";
                        return false;
                    }

                    (negatedText ? query.ExcludedTextTerms : query.RequiredTextTerms).Add(token);
                    continue;
                }

                if (!TryParseCondition((negatedText ? "!" : string.Empty) + token,
                    out PropertyCondition parsedCondition, out error))
                {
                    return false;
                }

                query.Conditions.Add(parsedCondition);
            }

            error = null;
            return true;
        }

        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            int depth = 0;

            foreach (char c in text)
            {
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')' && depth > 0)
                {
                    depth--;
                }

                if (char.IsWhiteSpace(c) && depth == 0)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }

        private static bool TryParseCondition(
            string token,
            out PropertyCondition condition,
            out string error)
        {
            condition = null;
            bool negated = StripNegation(ref token);
            Match match = _conditionPattern.Match(token);

            if (!match.Success)
            {
                error = $"Invalid property filter '{token}'. Example: hci>=10";
                return false;
            }

            string label = match.Groups[1].Value;
            string normalized = Normalize(label);
            string propertyName = _propertyAliases.TryGetValue(normalized, out string alias)
                ? alias
                : normalized;

            if (!double.TryParse(
                match.Groups[3].Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value))
            {
                error = $"Invalid value in '{token}'.";
                return false;
            }

            condition = new PropertyCondition
            {
                Label = label.ToUpperInvariant(),
                PropertyName = propertyName,
                Operator = match.Groups[2].Value,
                Value = value,
                Negated = negated
            };
            error = null;
            return true;
        }

        private static bool StripNegation(ref string token)
        {
            if (token.StartsWith("not:", StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(4);
                return true;
            }

            if (token.StartsWith("!", StringComparison.Ordinal))
            {
                token = token.Substring(1);
                return true;
            }

            return false;
        }

        private static string Describe(Query query)
        {
            int all = query.RequiredTextTerms.Count
                + query.Conditions.Count(condition => !condition.Negated);
            int not = query.ExcludedTextTerms.Count + query.Conditions.Count(condition => condition.Negated);
            var parts = new List<string>();

            if (all > 0)
            {
                parts.Add($"ALL {all}");
            }

            if (not > 0)
            {
                parts.Add($"NOT {not}");
            }

            foreach (CountGroup group in query.CountGroups)
            {
                parts.Add($"COUNT {group.Required} of {group.Conditions.Count}");
            }

            return parts.Count == 0
                ? "ALL LOADED ITEMS"
                : string.Join("  •  ", parts);
        }

        private static void IndexWalk(
            Item item, string path, uint rootSerial, bool root, ScanReport report,
            HashSet<uint> seen, ISet<uint> requestedSerials, bool hasWorldPosition,
            int rootX, int rootY, sbyte rootZ, int mapIndex)
        {
            if (item == null || item.IsDestroyed || !seen.Add(item.Serial))
            {
                return;
            }

            string fallbackName = GetDisplayName(item, false);
            string fallbackText = $"{fallbackName} {item.ItemData.Name} {item.Layer} {(Layer)item.ItemData.Layer}";
            bool hasProperties = World.OPL.TryGetNameAndData(item.Serial, out string oplName, out string oplData);
            bool isNew = !_catalog.TryGetValue(item.Serial, out IndexedItem indexed);

            if (isNew)
            {
                indexed = new IndexedItem { Serial = item.Serial };
                _catalog[item.Serial] = indexed;
                report.NewItems++;
            }

            indexed.ContainerSerial = item.Container;
            indexed.RootContainerSerial = rootSerial;
            indexed.Graphic = item.Graphic;
            indexed.Hue = item.Hue;
            indexed.Amount = item.Amount;
            string itemName = hasProperties && !string.IsNullOrWhiteSpace(oplName)
                ? oplName.Trim()
                : fallbackName;

            if (!string.Equals(indexed.Name, itemName, StringComparison.Ordinal))
            {
                indexed.Name = itemName;
                indexed.FormattedProperties = null;
                indexed.ParsedProperties = null;
            }
            indexed.FallbackText = fallbackText;
            if (hasProperties && !string.Equals(indexed.PropertyData, oplData, StringComparison.Ordinal))
            {
                indexed.PropertyData = oplData;
                indexed.FormattedProperties = null;
                indexed.ParsedProperties = null;
            }
            indexed.Location = path;
            indexed.HasWorldPosition = hasWorldPosition;
            indexed.RootX = rootX;
            indexed.RootY = rootY;
            indexed.RootZ = rootZ;
            indexed.MapIndex = mapIndex;
            indexed.LastSeenUtcTicks = DateTime.UtcNow.Ticks;
            report.Items++;

            if (!hasProperties && report.RequestedProperties < MAX_PROPERTY_REQUESTS
                && (requestedSerials == null || requestedSerials.Add(item.Serial)))
            {
                World.OPL.Contains(item.Serial);
                report.RequestedProperties++;
            }

            string childPath = root ? path : $"{path} > {indexed.Name}";

            for (var node = item.Items; node != null; node = node.Next)
            {
                if (node is Item child)
                {
                    IndexWalk(child, childPath, rootSerial, false, report, seen,
                        requestedSerials, hasWorldPosition, rootX, rootY, rootZ, mapIndex);
                }
            }
        }

        private static bool TryCollectFullyOpenedContainerContents(
            Item container, Dictionary<uint, HashSet<uint>> scannedContainers)
        {
            if (container == null || container.IsDestroyed || !container.Opened
                || !container.ItemData.IsContainer)
            {
                return false;
            }

            if (scannedContainers.ContainsKey(container.Serial))
            {
                return true;
            }

            var directItems = new HashSet<uint>();
            scannedContainers[container.Serial] = directItems;

            for (var node = container.Items; node != null; node = node.Next)
            {
                if (!(node is Item child) || child.IsDestroyed)
                {
                    continue;
                }

                directItems.Add(child.Serial);

                if (child.ItemData.IsContainer
                    && !TryCollectFullyOpenedContainerContents(child, scannedContainers))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RefreshIndexedItem(IndexedItem indexed)
        {
            Item item = World.Items.Get(indexed.Serial);

            if (item == null || item.IsDestroyed)
            {
                return;
            }

            indexed.ContainerSerial = item.Container;
            indexed.Graphic = item.Graphic;
            indexed.Hue = item.Hue;
            indexed.Amount = item.Amount;

            if (World.OPL.TryGetNameAndData(item.Serial, out string name, out string data))
            {
                string itemName = string.IsNullOrWhiteSpace(name) ? indexed.Name : name.Trim();

                if (!string.Equals(indexed.Name, itemName, StringComparison.Ordinal))
                {
                    indexed.Name = itemName;
                    indexed.FormattedProperties = null;
                    indexed.ParsedProperties = null;
                }

                if (!string.Equals(indexed.PropertyData, data, StringComparison.Ordinal))
                {
                    indexed.PropertyData = data;
                    indexed.FormattedProperties = null;
                    indexed.ParsedProperties = null;
                }
            }
        }

        private static void Evaluate(IndexedItem indexed, Query query, SearchReport report,
            ISet<uint> requestedSerials)
        {
            bool hasProperties = !string.IsNullOrWhiteSpace(indexed.PropertyData);
            string identityText = $"{indexed.Name} {indexed.FallbackText}";
            string fullText = $"{identityText} {indexed.PropertyData}";
            List<string> propertyTextTerms = query.RequiredTextTerms.Where(term =>
                identityText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0).ToList();

            if (!hasProperties)
            {
                report.UnloadedProperties++;
            }

            if (!hasProperties && propertyTextTerms.Count > 0)
            {
                RequestMissingProperties(indexed, report, requestedSerials);
                return;
            }

            if (!MatchesText(query, fullText))
            {
                return;
            }

            if (!hasProperties && query.NeedsProperties)
            {
                RequestMissingProperties(indexed, report, requestedSerials);
                return;
            }

            string matchedProperties = string.Empty;
            bool needsParsedProperties = query.Conditions.Count > 0
                || query.CountGroups.Count > 0
                || propertyTextTerms.Count > 0
                || indexed.FormattedProperties == null;
            var properties = hasProperties && needsParsedProperties
                ? GetParsedProperties(indexed)
                : null;

            if (propertyTextTerms.Count > 0 && properties != null)
            {
                matchedProperties = TextPropertySummary(properties, propertyTextTerms);
            }

            if ((query.Conditions.Count > 0 || query.CountGroups.Count > 0)
                && properties != null)
            {
                if (!MatchesProperties(query, properties, out string numericSummary))
                {
                    return;
                }

                matchedProperties = string.IsNullOrEmpty(matchedProperties)
                    ? numericSummary
                    : string.IsNullOrEmpty(numericSummary)
                        ? matchedProperties
                        : matchedProperties + ", " + numericSummary;
            }

            if (indexed.FormattedProperties == null)
            {
                indexed.FormattedProperties = FormatProperties(properties);
            }

            report.Results.Add(new SearchResult
            {
                Item = World.Items.Get(indexed.Serial),
                Serial = indexed.Serial,
                ContainerSerial = indexed.ContainerSerial,
                RootContainerSerial = indexed.RootContainerSerial,
                Graphic = indexed.Graphic,
                Hue = indexed.Hue,
                Amount = indexed.Amount,
                Name = indexed.Name,
                Location = indexed.Location,
                Properties = matchedProperties,
                AllProperties = indexed.FormattedProperties,
                HasWorldPosition = indexed.HasWorldPosition,
                RootX = indexed.RootX,
                RootY = indexed.RootY,
                RootZ = indexed.RootZ,
                MapIndex = indexed.MapIndex
            });
        }

        private static string GetFacetName(int mapIndex)
        {
            switch (mapIndex)
            {
                case 0: return "Felucca";
                case 1: return "Trammel";
                case 2: return "Ilshenar";
                case 3: return "Malas";
                case 4: return "Tokuno";
                case 5: return "Ter Mur";
                default: return $"Facet {mapIndex}";
            }
        }

        private static string FormatProperties(ItemPropertiesData properties)
        {
            if (properties?.RawLines == null || properties.RawLines.Length == 0)
            {
                return "No item properties loaded";
            }

            return string.Join("\n", properties.RawLines
                .Select(CleanPropertyLine)
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private static void RequestMissingProperties(IndexedItem indexed,
            SearchReport report, ISet<uint> requestedSerials)
        {
            report.MissingProperties++;

            if (report.RequestedProperties < MAX_PROPERTY_REQUESTS
                && World.Items.Get(indexed.Serial) != null
                && (requestedSerials == null || requestedSerials.Add(indexed.Serial)))
            {
                World.OPL.Contains(indexed.Serial);
                report.RequestedProperties++;
            }
        }

        private static string TextPropertySummary(ItemPropertiesData properties,
            List<string> terms)
        {
            if (properties?.RawLines == null)
            {
                return string.Empty;
            }

            return string.Join(", ", properties.RawLines
                .Select(CleanPropertyLine)
                .Where(line => !string.IsNullOrWhiteSpace(line)
                    && terms.Any(term => line.IndexOf(term,
                        StringComparison.OrdinalIgnoreCase) >= 0))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string CleanPropertyLine(string line)
        {
            string cleaned = WebUtility.HtmlDecode(
                Regex.Replace(line ?? string.Empty, "<[^>]+>", string.Empty));
            cleaned = Regex.Replace(cleaned, @"/c\[[^\]]+\]", string.Empty,
                RegexOptions.IgnoreCase);
            return cleaned.Replace("/cd", string.Empty).Trim();
        }

        private static ItemPropertiesData GetParsedProperties(IndexedItem indexed)
        {
            if (indexed.ParsedProperties == null)
                indexed.ParsedProperties = new ItemPropertiesData(
                    (indexed.Name ?? string.Empty) + "\n" + (indexed.PropertyData ?? string.Empty));
            return indexed.ParsedProperties;
        }

        private static void Highlight(Item item, long expires = 0)
        {
            if (item == null || item.IsDestroyed)
            {
                return;
            }

            if (!_highlights.TryGetValue(item.Serial, out HighlightState state))
            {
                state = new HighlightState
                {
                    Item = item,
                    Matched = item.MatchesHighlightData,
                    Color = item.HighlightColor,
                    Hue = item.Hue
                };
                _highlights[item.Serial] = state;
            }

            state.Expires = expires != 0
                ? expires
                : (long)Time.Ticks + LOCATE_SECONDS * 1000;
            item.MatchesHighlightData = true;
            item.HighlightColor = new Color(255, 20, 147);

            if (item.OnGround || item.Container == World.Player?.Serial)
            {
                item.Hue = 0x0026;
            }
        }

        private static bool MatchesText(Query query, string fullValue)
        {
            foreach (string term in query.RequiredTextTerms)
            {
                if (fullValue.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            foreach (string term in query.ExcludedTextTerms)
            {
                if (fullValue.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesProperties(
            Query query,
            ItemPropertiesData properties,
            out string summary)
        {
            if (query.Conditions.Count == 0 && query.CountGroups.Count == 0)
            {
                summary = string.Empty;
                return true;
            }

            var matches = new List<string>();

            foreach (PropertyCondition condition in query.Conditions)
            {
                if (!EvaluateCondition(condition, properties, out string matchText))
                {
                    summary = null;
                    return false;
                }

                matches.Add(matchText);
            }

            foreach (CountGroup group in query.CountGroups)
            {
                int matched = 0;
                var groupMatches = new List<string>();

                foreach (PropertyCondition condition in group.Conditions)
                {
                    if (EvaluateCondition(condition, properties, out string matchText))
                    {
                        matched++;
                        groupMatches.Add(matchText);
                    }
                }

                if (matched < group.Required)
                {
                    summary = null;
                    return false;
                }

                matches.Add($"{matched}/{group.Conditions.Count}: {string.Join(", ", groupMatches)}");
            }

            summary = string.Join(", ", matches);
            return true;
        }

        private static bool EvaluateCondition(
            PropertyCondition condition,
            ItemPropertiesData properties,
            out string matchText)
        {
            ItemPropertiesData.SinglePropertyData found = properties.singlePropertyData.FirstOrDefault(
                property => Normalize(property.Name) == condition.PropertyName
                            && property.FirstValue != double.MinValue
            );
            bool baseMatch = found != null && Compare(found.FirstValue, condition.Operator, condition.Value);
            bool matches = condition.Negated ? !baseMatch : baseMatch;

            if (!matches)
            {
                matchText = null;
                return false;
            }

            if (condition.Negated)
            {
                matchText = found == null
                    ? $"NOT {condition.Label}"
                    : $"NOT {condition.Label}{condition.Operator}{condition.Value:0.##} ({found.FirstValue:0.##})";
            }
            else
            {
                matchText = $"{condition.Label} {found.FirstValue:0.##}";
            }

            return true;
        }

        private static bool Compare(double actual, string op, double expected)
        {
            switch (op)
            {
                case ">=": return actual >= expected;
                case "<=": return actual <= expected;
                case ">": return actual > expected;
                case "<": return actual < expected;
                default: return Math.Abs(actual - expected) < 0.0001;
            }
        }

        private static string GetDisplayName(Item item, bool useProperties)
        {
            if (useProperties
                && World.OPL.TryGetNameAndData(item.Serial, out string oplName, out _)
                && !string.IsNullOrWhiteSpace(oplName))
            {
                return oplName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(item.Name))
            {
                return item.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(item.ItemData.Name))
            {
                return item.ItemData.Name.Trim();
            }

            return $"Item 0x{item.Graphic:X4}";
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
    }
}

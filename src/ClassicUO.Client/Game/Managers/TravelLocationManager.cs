using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    internal static class TravelLocationManager
    {
        private static readonly string[] Facets = { "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "Ter Mur" };
        private static readonly Regex Sextant = new Regex(@"(\d{1,3})\s*[°o]\s*(\d{1,2})\s*['′]\s*([NS])\s*,?\s*(\d{1,3})\s*[°o]\s*(\d{1,2})\s*['′]\s*([EW])", RegexOptions.IgnoreCase);
        private static Profile _pendingProfile;
        private static WorldExplorerPin _pending;
        private static int _fromFacet;
        private static int _fromX, _fromY;
        private static long _deadline, _castUntil;
        private static long _ambiguousUntil;
        private sealed class BookReplySnapshot
        {
            internal uint Source;
            internal Dictionary<int, WorldExplorerPin> Rows = new Dictionary<int, WorldExplorerPin>();
            internal Dictionary<int, WorldExplorerPin> Travel = new Dictionary<int, WorldExplorerPin>();
        }
        private static readonly Dictionary<string, BookReplySnapshot> BookReplies = new Dictionary<string, BookReplySnapshot>();
        private static readonly Dictionary<uint, int> AtlasSelections = new Dictionary<uint, int>();
        private static uint _lastBook;
        private static long _bookOpenedAt;
        private static long _bookAmbiguousUntil;
        private static bool _awaitingBook;

        internal static string Key(WorldExplorerPin pin) => TravelDestinationIdentity.SourceKey(pin) + "|" + TravelDestinationIdentity.NameKey(pin);
        internal static string FacetName(int facet) => facet >= 0 && facet < Facets.Length ? Facets[facet] : "Unknown facet";

        internal static void ObserveBookOpen(uint serial)
        {
            var item = World.Items.Get(serial);
            if (item == null || !(item.Graphic == 0x22C5 || item.Graphic == 0x22C6 || item.Graphic == 0x9C16 || item.Graphic == 0x9C17)) return;
            if (_awaitingBook && _lastBook != serial && Time.Ticks - _bookOpenedAt <= 5000)
            { BookReplies.Clear(); _lastBook = 0; _awaitingBook = false; _bookAmbiguousUntil = (long)Time.Ticks + 5000; return; }
            if (Time.Ticks < _bookAmbiguousUntil) return;
            _lastBook = serial; _bookOpenedAt = Time.Ticks; _awaitingBook = true;
        }

        internal static void ObserveBookGump(Gump gump)
        {
            string key = $"{gump.LocalSerial:X8}:{gump.ServerSerial:X8}";
            if (!BookReplies.ContainsKey(key) && (_lastBook == 0 || Time.Ticks - _bookOpenedAt > 5000)) return;
            string kind = WorldExplorerGump.BookType(gump);
            if (kind == null) return;
            var direct = World.Items.Get(gump.LocalSerial);
            bool directSource = direct != null && (kind == "atlas" ? direct.Graphic == 0x9C16 || direct.Graphic == 0x9C17 : direct.Graphic == 0x22C5 || direct.Graphic == 0x22C6);
            if (!directSource && Time.Ticks < _bookAmbiguousUntil) return;
            uint source = directSource ? direct.Serial
                : _awaitingBook && Time.Ticks - _bookOpenedAt <= 5000 ? _lastBook
                : BookReplies.TryGetValue(key, out var previous) ? previous.Source : Time.Ticks - _bookOpenedAt <= 5000 ? _lastBook : 0;
            if (source == 0 || World.Items.Get(source) == null) return;
            if (source == _lastBook) _awaitingBook = false;
            var snapshot = new BookReplySnapshot { Source = source };
            var buttons = kind == "atlas" ? WorldExplorerGump.AtlasRows(gump)
                : gump.Children.OfType<Button>().Where(b => b.Page == 1 && b.ButtonID >= 10 && b.ButtonID < 26).ToList();
            foreach (var button in buttons)
            {
                string name = WorldExplorerGump.RowName(gump, button);
                if (string.IsNullOrWhiteSpace(name) || name.Equals("Empty", StringComparison.OrdinalIgnoreCase)) continue;
                var pin = new WorldExplorerPin { Kind = kind, Serial = source, Slot = button.ButtonID - (kind == "atlas" ? 100 : 10), Name = name };
                snapshot.Rows[button.ButtonID] = pin;
                if (kind == "book")
                    foreach (int id in new[] { 10 + pin.Slot, 50 + pin.Slot, 75 + pin.Slot })
                        if (gump.Children.OfType<Button>().Any(b => b.ButtonID == id && b.ButtonAction == ButtonAction.Activate)) snapshot.Travel[id] = pin;
            }
            if (kind == "atlas" && buttons.Count == 16 && AtlasSelections.TryGetValue(source, out int selected)
                && snapshot.Rows.TryGetValue(100 + selected, out var chosen)
                && gump.Children.Any(c => c.Y > buttons.Max(b => b.Y) + 40 && c.X < buttons[8].X && WorldExplorerGump.ControlText(c) == chosen.Name))
                foreach (Button button in gump.Children.OfType<Button>().Where(b => b.Y > buttons.Max(row => row.Y) && b.ButtonAction == ButtonAction.Activate))
                {
                    string action = WorldExplorerGump.RowName(gump, button) ?? "";
                    if (action.StartsWith("Recall", StringComparison.OrdinalIgnoreCase) || action.Equals("Sacred Journey", StringComparison.OrdinalIgnoreCase)) snapshot.Travel[button.ButtonID] = chosen;
                }
            if (!BookReplies.ContainsKey(key) && BookReplies.Count >= 16) BookReplies.Remove(BookReplies.Keys.First());
            BookReplies[key] = snapshot;
        }

        internal static void ObserveBookReply(uint sender, uint server, int button)
        {
            if (!BookReplies.TryGetValue($"{sender:X8}:{server:X8}", out var snapshot)) return;
            _lastBook = snapshot.Source; _bookOpenedAt = Time.Ticks;
            if (snapshot.Travel.TryGetValue(button, out var pin)) Begin(pin);
            else if (snapshot.Rows.TryGetValue(button, out pin) && pin.Kind == "atlas")
            {
                if (!AtlasSelections.ContainsKey(snapshot.Source) && AtlasSelections.Count >= 128) AtlasSelections.Remove(AtlasSelections.Keys.First());
                AtlasSelections[snapshot.Source] = pin.Slot;
            }
        }
        internal static int? FacetFromText(string text)
        {
            int? result = null;
            for (int i = 0; i < Facets.Length; i++)
                if (Regex.IsMatch(text ?? "", @"\b" + Facets[i].Replace(" ", @"\s*") + @"\b", RegexOptions.IgnoreCase))
                { if (result.HasValue) return null; result = i; }
            return result;
        }

        internal static int? FacetFromHue(ushort hue, bool atlas)
        {
            if (hue == 10) return 1;
            if (hue == 81) return 0;
            if (hue == 1154) return 4;
            if (hue == 1645) return 5;
            if (atlas && hue == 1102) return 3;
            return null; // Runebooks share the Malas/Ilshenar hue. Selected atlas hues are also ambiguous.
        }

        internal static TravelDestinationLocation Parse(string text, int? facet)
        {
            text = System.Net.WebUtility.HtmlDecode(Regex.Replace(text ?? "", "<[^>]*>", " "));
            facet = FacetFromText(text) ?? facet;
            if (!facet.HasValue || facet.Value < 0 || facet.Value >= MapLoader.MAPS_COUNT) return null;
            var direct = Regex.Match(text ?? "", @"\bX\s*[:=]\s*(\d+)\s*[,; ]+Y\s*[:=]\s*(\d+)(?:\s*[,; ]+Z\s*[:=]\s*(-?\d+))?", RegexOptions.IgnoreCase);
            if (direct.Success && int.TryParse(direct.Groups[1].Value, out int dx) && int.TryParse(direct.Groups[2].Value, out int dy))
                return Valid(new TravelDestinationLocation { X = dx, Y = dy, Facet = facet.Value,
                    Z = int.TryParse(direct.Groups[3].Value, out int z) ? (int?)z : null, Source = "Scanned coordinates" });
            Match match = Sextant.Match(text ?? "");
            if (!match.Success) return null;
            int lat = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), latMin = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            int lon = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture), lonMin = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
            if (lat > 180 || lon > 180 || latMin >= 60 || lonMin >= 60 || lat == 180 && latMin != 0 || lon == 180 && lonMin != 0) return null;
            double latitude = lat + latMin / 60.0, longitude = lon + lonMin / 60.0;
            // Britannia and the Lost Lands use different sextant origins on the same facet.
            // If both regions can match, keep it unknown until an observed arrival supplies exact coordinates.
            if (facet.Value <= 1)
            {
                double lx = 5936 + (match.Groups[6].Value.Equals("W", StringComparison.OrdinalIgnoreCase) ? -longitude : longitude) * 5120 / 360;
                double ly = 3112 + (match.Groups[3].Value.Equals("N", StringComparison.OrdinalIgnoreCase) ? -latitude : latitude) * 4096 / 360;
                if (lx >= 5120 && lx < 6144 && ly >= 2304 && ly < 4096) return null;
            }
            if (match.Groups[3].Value.Equals("N", StringComparison.OrdinalIgnoreCase)) latitude = 360 - latitude;
            if (match.Groups[6].Value.Equals("W", StringComparison.OrdinalIgnoreCase)) longitude = 360 - longitude;
            return Valid(new TravelDestinationLocation { X = (1323 + (int)(longitude * 5120 / 360)) % 5120,
                Y = (1624 + (int)(latitude * 4096 / 360)) % 4096, Facet = facet.Value, Approximate = true, Source = "Sextant (approximate)" });
        }

        private static TravelDestinationLocation Valid(TravelDestinationLocation location)
        {
            if (location == null) return null;
            var sizes = MapLoader.Instance.MapsDefaultSize;
            return location.Facet >= 0 && location.Facet < sizes.GetLength(0) && location.X >= 0 && location.Y >= 0
                && location.X < sizes[location.Facet, 0] && location.Y < sizes[location.Facet, 1] ? location : null;
        }

        internal static void Restore(WorldExplorerPin pin)
        {
            if (ProfileManager.CurrentProfile?.WorldExplorerLocations?.TryGetValue(Key(pin), out var location) == true)
                pin.Location = Valid(location);
        }

        internal static void Store(WorldExplorerPin pin, TravelDestinationLocation location, bool save = true)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null || Valid(location) == null) return;
            if (profile.WorldExplorerLocations == null) profile.WorldExplorerLocations = new System.Collections.Generic.Dictionary<string, TravelDestinationLocation>();
            string key = Key(pin);
            if (profile.WorldExplorerLocations.TryGetValue(key, out var existing) && Valid(existing) != null) { pin.Location = existing; return; }
            profile.WorldExplorerLocations[key] = pin.Location = location;
            foreach (var saved in profile.WorldExplorerPins ?? new System.Collections.Generic.List<WorldExplorerPin>())
                if (Key(saved) == key) saved.Location = location;
            if (save) profile.Save(ProfileManager.ProfilePath, false);
        }

        internal static void Begin(WorldExplorerPin pin)
        {
            if (_pending != null && Time.Ticks <= _deadline && Key(_pending) != Key(pin))
            { _ambiguousUntil = (long)Time.Ticks + 15000; ClearPending(); return; }
            ClearPending();
            Restore(pin);
            if (Time.Ticks < _ambiguousUntil || pin.Location != null || World.Player == null) return;
            _pendingProfile = ProfileManager.CurrentProfile; _pending = pin;
            _fromFacet = World.MapIndex;
            _fromX = World.Player.X; _fromY = World.Player.Y;
            _deadline = (long)Time.Ticks + 15000;
        }

        internal static void ObserveCast(int spell)
        {
            _castUntil = spell == 32 || spell == 210 ? (long)Time.Ticks + 12000 : 0;
            if (_castUntil == 0) ClearPending();
        }
        internal static void ObserveRuneTarget(Entity entity)
        {
            if (_castUntil == 0 || Time.Ticks > _castUntil || !(entity is Item rune) || rune.Graphic < 0x1F14 || rune.Graphic > 0x1F17) return;
            if (!World.OPL.TryGetNameAndData(rune.Serial, out string name, out string data)) return;
            var match = Regex.Match((name + " " + data).Trim(), @"(?:marked\s*(?:for|:)|rune\s+for)\s+([^\n\r]+)", RegexOptions.IgnoreCase);
            if (!match.Success) return;
            Begin(new WorldExplorerPin { Kind = "rune", Serial = rune.Serial, Name = Regex.Replace(match.Groups[1].Value, "<[^>]*>", "").Trim() });
            _castUntil = 0;
        }

        // Called only for a server position reset, never ordinary walk acknowledgements or the local fly camera.
        internal static void ObservePosition(int x, int y, int z)
        {
            if (_pending == null) return;
            if (!World.InGame || World.Player.IsDead || _pendingProfile != ProfileManager.CurrentProfile || Time.Ticks > _deadline) { ClearPending(); return; }
            if (Math.Max(Math.Abs(World.Player.X - _fromX), Math.Abs(World.Player.Y - _fromY)) > 2) { ClearPending(); return; }
            if (World.MapIndex == _fromFacet && Math.Max(Math.Abs(x - World.Player.X), Math.Abs(y - World.Player.Y)) <= 2) return;
            Store(_pending, new TravelDestinationLocation { X = x, Y = y, Z = z, Facet = World.MapIndex, Source = "Observed arrival" });
            ClearPending();
        }

        private static void ClearPending() { _pending = null; _pendingProfile = null; }
        internal static void Reset() { ClearPending(); _castUntil = _ambiguousUntil = _bookAmbiguousUntil = 0; _lastBook = 0; _bookOpenedAt = 0; _awaitingBook = false; BookReplies.Clear(); AtlasSelections.Clear(); }
    }
}

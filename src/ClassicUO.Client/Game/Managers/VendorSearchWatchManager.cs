using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal static class VendorSearchWatchManager
    {
        private static readonly Queue<VendorWatchItem> _queue = new Queue<VendorWatchItem>();
        private static readonly HashSet<string> _pages = new HashSet<string>();
        private static readonly HashSet<uint> _freshProperties = new HashSet<uint>();
        private static Profile _profile;
        private static VendorWatchItem _watch;
        private static VendorMarketScan _scan;
        private static Gump _results;
        private static Action _action;
        private static int _stage, _finished;
        private static long _due, _deadline, _propertiesUntil;
        private static bool _capture, _cleared;
        internal static bool Active => _stage != 0;
        internal static string Status { get; private set; } = "";
        internal static int Revision { get; private set; }

        internal static VendorWatchItem AddWatch(Profile profile, string query)
        {
            query = (query ?? "").Trim();
            if (profile == null || query.Length == 0 || query.Length > 100) return null;
            var item = profile.VendorWatchItems.FirstOrDefault(w => string.Equals(w.Query, query, StringComparison.OrdinalIgnoreCase));
            if (item != null) return item;
            if (profile.VendorWatchItems.Count >= 32) return null;
            item = new VendorWatchItem { Query = query }; profile.VendorWatchItems.Add(item); return item;
        }

        internal static void StartScan()
        {
            if (!World.InGame || Active) return;
            var profile = ProfileManager.CurrentProfile;
            if (profile == null || !profile.VendorSearchScansEnabled) { GameActions.Print("Enable VS scans in Market watch first.", 0x35); return; }
            if (profile.VendorWatchItems.Count == 0) { GameActions.Print("Add at least one watched item.", 0x35); return; }
            Reset(); _profile = profile; _capture = true;
            EventSink.OPLOnReceive += PropertiesReceived;
            foreach (var watch in profile.VendorWatchItems.Take(32)) _queue.Enqueue(watch);
            NextSearch();
        }

        internal static void OpenSearch(string query = null)
        {
            if (!World.InGame) return;
            if (Active) { GameActions.Print("Vendor Search is already running. Use Stop scan before starting another search.", 0x35); return; }
            Reset(); _profile = ProfileManager.CurrentProfile;
            _queue.Enqueue(query == null ? null : new VendorWatchItem { Query = query });
            NextSearch();
        }

        private static void NextSearch()
        {
            _results = null; _pages.Clear(); _freshProperties.Clear(); _cleared = false;
            if (_queue.Count == 0)
            {
                int finished = _finished; Reset();
                SetStatus("VS scan finished: " + finished + " watched searches saved."); return;
            }
            _watch = _queue.Dequeue(); _scan = new VendorMarketScan { ScanUtcTicks = DateTime.UtcNow.Ticks };
            Schedule(1, () => GameActions.OpenPopupMenu(World.Player.Serial, true), "Opening Vendor Search" + (_watch == null ? "" : ": " + _watch.Query), 250);
        }

        private static void SetStatus(string text) { Status = text; Revision++; }
        private static void PropertiesReceived(object sender, OPLEventArgs e)
        { if (_freshProperties.Count < 8192) _freshProperties.Add(e.Serial); }
        private static void Schedule(int stage, Action action, string status, int delay = 1500)
        { _stage = stage; _action = action; _due = (long)Time.Ticks + delay; _deadline = _due + 20000; SetStatus(status); }

        internal static bool IsQueryWindow(string text) => text.IndexOf("Vendor Search Query", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("Selected Search Criteria", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool IsResultsWindow(string text) => !IsQueryWindow(text)
            && (text.IndexOf("Vendor Search", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Global Vendor Market", StringComparison.OrdinalIgnoreCase) >= 0)
            && text.IndexOf("Results", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static int NameFieldScore(Rectangle label, Rectangle field)
        {
            int vertical = Math.Abs(field.Y + field.Height / 2 - label.Y - label.Height / 2);
            // UOAlive puts the field above the caption; classic layouts put it just below.
            if (vertical > 28 || field.X < label.X - 12 || field.X > label.Right + 220) return int.MaxValue;
            return vertical * 8 + Math.Abs(field.X - label.X);
        }

        internal static bool ObservePopup(PopupMenuData popup)
        {
            if (_stage != 1 || !World.InGame || popup.Serial != World.Player.Serial || _profile != ProfileManager.CurrentProfile) return false;
            foreach (var item in popup.Items)
                if ((item.Flags & 1) == 0 && string.Equals(ClilocLoader.Instance.GetString(item.Cliloc), "Vendor Search", StringComparison.OrdinalIgnoreCase))
                {
                    ushort index = item.Index;
                    Schedule(2, () => GameActions.ResponsePopupMenu(popup.Serial, index), "Waiting for Vendor Search query...", 250);
                    return true;
                }
            Stop("Vendor Search is unavailable here. Try a guarded town or your house.");
            return false;
        }

        internal static void ObserveGump(Gump gump)
        {
            if (!Active || _action != null || !gump.IsFromServer || _profile != ProfileManager.CurrentProfile) return;
            string text = string.Join("\n", VendorLedgerManager.Cells(gump).Select(c => c.Text));
            bool searchWindow = text.IndexOf("Vendor Search", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Global Vendor Market", StringComparison.OrdinalIgnoreCase) >= 0;
            if (_stage == 2 && searchWindow && IsQueryWindow(text))
            {
                if (_watch == null) { _stage = 0; SetStatus("Vendor Search opened."); return; }
                var fields = gump.Children.OfType<StbTextBox>().Where(c => c.IsFromServer && !c.IsDisposed).ToList();
                var nameLabel = gump.Children.FirstOrDefault(c => c.IsFromServer && !c.IsDisposed
                    && VendorLedgerManager.Text(c).Equals("Item Name", StringComparison.OrdinalIgnoreCase));
                var name = nameLabel == null ? null : fields.Select(c => new { Field = c, Score = NameFieldScore(
                        new Rectangle(nameLabel.X, nameLabel.Y, nameLabel.Width, nameLabel.Height), new Rectangle(c.X, c.Y, c.Width, c.Height)) })
                    .Where(c => c.Score != int.MaxValue).OrderBy(c => c.Score).Select(c => c.Field).FirstOrDefault();
                var search = VendorRefreshManager.Action(gump, "Search");
                if (name == null || search == null) { Stop("Unsupported Vendor Search query layout; original window retained."); return; }
                if (!_cleared)
                {
                    var clear = VendorRefreshManager.Action(gump, "Clear Search Criteria", "Clear criteria");
                    if (clear == null) { Stop("Cannot clear existing VS filters safely. Original window retained."); return; }
                    _cleared = true;
                    Schedule(2, () =>
                    {
                        if (gump.IsDisposed) { Stop("Vendor Search query closed before filters could be cleared."); return; }
                        gump.OnButtonClick(clear.ButtonID);
                    }, "Clearing old VS filters...", 250);
                    return;
                }
                name.SetText(_watch.Query);
                if (!string.Equals(name.Text, _watch.Query, StringComparison.Ordinal)) { Stop("Watched name exceeds the server field's limit. Shorten it."); return; }
                Schedule(5, () =>
                {
                    if (gump.IsDisposed) { Stop("Vendor Search query closed before the search could be submitted."); return; }
                    gump.OnButtonClick(search.ButtonID);
                    SetStatus("Waiting for server search results: " + _watch.Query);
                }, "Submitting search: " + _watch.Query);
            }
            else if (_stage == 5)
            {
                if (!searchWindow) return;
                if (text.IndexOf("No items matched", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("No results found", StringComparison.OrdinalIgnoreCase) >= 0)
                { Finish(true, "Server returned no matching listings."); return; }
                if (!IsResultsWindow(text)) return;
                if (!_capture) { _stage = 0; SetStatus("Search results opened."); return; }
                _results = gump; _stage = 6; _due = (long)Time.Ticks + 1500; _propertiesUntil = (long)Time.Ticks + 6500;
                foreach (uint serial in gump.Children.Where(c => c.Tooltip is uint).Select(c => (uint)c.Tooltip).Distinct())
                    if (!_freshProperties.Contains(serial)) PacketHandlers.AddMegaClilocRequest(serial);
                _deadline = (long)Time.Ticks + 20000;
                SetStatus("Reading results: " + _watch.Query);
            }
        }

        internal static int? Amount(string name, string data)
        {
            var explicitAmount = Regex.Match(VendorLedgerManager.Plain(data), @"\b(?:Amount|Quantity|Units)\s*:\s*([\d,]+)", RegexOptions.IgnoreCase);
            var prefix = Regex.Match(VendorLedgerManager.Plain(name), @"^([\d,]+)\s+\D");
            string value = explicitAmount.Success ? explicitAmount.Groups[1].Value : prefix.Success ? prefix.Groups[1].Value : "";
            return VendorLedgerManager.Money(value, out long amount) && amount > 0 && amount <= int.MaxValue ? (int?)amount : null;
        }

        internal static string Column(List<VendorLedgerManager.Cell> cells, VendorLedgerManager.Cell header, int y, int bottom)
        {
            if (header == null) return "";
            int end = cells.Where(c => Math.Abs(c.Y - header.Y) <= 5 && c.X > header.X).Select(c => c.X).DefaultIfEmpty(header.X + 180).Min();
            return string.Join(" ", cells.Where(c => c.Y >= y - 3 && c.Y < bottom && c.X >= header.X - 10 && c.X < end - 10)
                .OrderBy(c => c.Y).Select(c => c.Text));
        }

        internal static decimal? UnitQuote(string text)
            => decimal.TryParse(text, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture, out decimal price) && price >= 0 ? (decimal?)price : null;

        private static VendorLedgerManager.Cell UnitHeader(List<VendorLedgerManager.Cell> cells, int y)
            => cells.FirstOrDefault(c => Math.Abs(c.Y - y) <= 5 && (c.Text.Equals("Price Per Unit", StringComparison.OrdinalIgnoreCase)
                || c.Text.Equals("Unit Price", StringComparison.OrdinalIgnoreCase) || c.Text.Equals("Price/Unit", StringComparison.OrdinalIgnoreCase)));

        internal static List<VendorMarketListing> ParseTable(List<VendorLedgerManager.Cell> cells)
        {
            var name = cells.FirstOrDefault(c => c.Text.Equals("Item Name", StringComparison.OrdinalIgnoreCase));
            var price = cells.FirstOrDefault(c => c.Text.Equals("Price", StringComparison.OrdinalIgnoreCase));
            if (name == null || price == null || Math.Abs(name.Y - price.Y) > 5) return null;
            var amount = cells.FirstOrDefault(c => Math.Abs(c.Y - name.Y) <= 5 && (c.Text.Equals("Amount", StringComparison.OrdinalIgnoreCase) || c.Text.Equals("Quantity", StringComparison.OrdinalIgnoreCase)));
            var date = cells.FirstOrDefault(c => Math.Abs(c.Y - name.Y) <= 5 && (c.Text.Equals("List Date", StringComparison.OrdinalIgnoreCase) || c.Text.Equals("Listed", StringComparison.OrdinalIgnoreCase)));
            int nameEnd = cells.Where(c => Math.Abs(c.Y - name.Y) <= 5 && c.X > name.X).Select(c => c.X).DefaultIfEmpty(price.X).Min();
            var rows = cells.Where(c => c.X >= name.X - 5 && c.X < nameEnd - 10 && c.Y > name.Y + 5).OrderBy(c => c.Y).ToList();
            var result = new List<VendorMarketListing>();
            for (int i = 0; i < rows.Count; i++)
            {
                int bottom = i + 1 < rows.Count ? rows[i + 1].Y - 3 : rows[i].Y + 24;
                string p = Column(cells, price, rows[i].Y, bottom);
                if (!VendorLedgerManager.Money(p, out long value)) continue;
                string a = Column(cells, amount, rows[i].Y, bottom);
                int? count = VendorLedgerManager.Money(a, out long n) && n > 0 && n <= int.MaxValue ? (int?)n : Amount(rows[i].Text, "");
                result.Add(new VendorMarketListing { ItemName = rows[i].Text, Price = value, Amount = count,
                    QuotedUnitPrice = UnitQuote(Column(cells, UnitHeader(cells, name.Y), rows[i].Y, bottom)), ListDate = Column(cells, date, rows[i].Y, bottom) });
            }
            return result.Count == 0 ? null : result;
        }

        private static List<VendorMarketListing> ParseResults(Gump gump, out bool propertiesReady)
        {
            propertiesReady = true;
            var cells = VendorLedgerManager.Cells(gump);
            var icons = gump.Children.Where(c => c.IsFromServer && !c.IsDisposed && (c.Page == 0 || c.Page == gump.ActivePage) && c.Tooltip is uint)
                .GroupBy(c => (uint)c.Tooltip).Select(g => g.OrderBy(c => c.Y).First())
                .OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
            if (icons.Count == 0) return ParseTable(cells);
            var price = cells.FirstOrDefault(c => c.Text.Equals("Price", StringComparison.OrdinalIgnoreCase));
            if (price == null) return null;
            var amount = cells.FirstOrDefault(c => Math.Abs(c.Y - price.Y) <= 5 && (c.Text.Equals("Amount", StringComparison.OrdinalIgnoreCase) || c.Text.Equals("Quantity", StringComparison.OrdinalIgnoreCase)));
            var date = cells.FirstOrDefault(c => Math.Abs(c.Y - price.Y) <= 5 && (c.Text.Equals("List Date", StringComparison.OrdinalIgnoreCase) || c.Text.Equals("Listed", StringComparison.OrdinalIgnoreCase)));
            var facet = cells.FirstOrDefault(c => Math.Abs(c.Y - price.Y) <= 5 && c.Text.Equals("Facet", StringComparison.OrdinalIgnoreCase));
            var result = new List<VendorMarketListing>();
            for (int i = 0; i < icons.Count; i++)
            {
                var icon = icons[i]; uint serial = (uint)icon.Tooltip;
                int bottom = i + 1 < icons.Count ? icons[i + 1].Y - 3 : icon.Y + Math.Max(45, icon.Height);
                bool found = World.OPL.TryGetNameAndData(serial, out string name, out string data);
                bool fresh = found && _freshProperties.Contains(serial);
                if (!fresh || string.IsNullOrWhiteSpace(name)) propertiesReady = false;
                string p = Column(cells, price, icon.Y, bottom);
                string a = Column(cells, amount, icon.Y, bottom);
                result.Add(new VendorMarketListing { Serial = serial, ItemName = found ? VendorLedgerManager.Plain(name) : "Item " + serial.ToString("X8"),
                    ItemGraphic = icon is ButtonTileArt tile ? (ushort?)tile.ItemGraphic : icon is StaticPic pic ? (ushort?)pic.Graphic : null,
                    ItemHue = icon is ButtonTileArt tileHue ? (ushort?)tileHue.ItemHue : icon is StaticPic picHue ? (ushort?)picHue.Hue : null,
                    PropertiesKnown = fresh,
                    Data = (fresh ? "" : "Cached properties; refresh unavailable.\n") + VendorLedgerManager.Plain(data), Price = VendorLedgerManager.Money(p, out long value) ? (long?)value : null,
                    Amount = VendorLedgerManager.Money(a, out long n) && n > 0 && n <= int.MaxValue ? (int?)n : fresh ? Amount(name, data) : null,
                    QuotedUnitPrice = UnitQuote(Column(cells, UnitHeader(cells, price.Y), icon.Y, bottom)),
                    ListDate = Column(cells, date, icon.Y, bottom), Facet = Column(cells, facet, icon.Y, bottom) });
            }
            return result;
        }

        internal static void SaveScan(VendorWatchItem watch, VendorMarketScan scan)
        {
            watch.Scans.Add(scan);
            while (watch.Scans.Count > 32) watch.Scans.RemoveAt(0);
        }

        internal static void TrimHistory(System.Collections.Generic.List<VendorWatchItem> watches, int budget = 12000)
        {
            int count = watches.Sum(w => w.Scans.Sum(s => s.Listings.Count));
            while (count > budget)
            {
                var oldest = watches.Where(w => w.Scans.Count > 1).OrderBy(w => w.Scans[0].ScanUtcTicks).FirstOrDefault();
                if (oldest == null) break; // Retain each watch's most recent observation.
                count -= oldest.Scans[0].Listings.Count; oldest.Scans.RemoveAt(0);
            }
        }

        private static void ReadPage()
        {
            if (_results == null || _results.IsDisposed) { Stop("VS results closed; scan stopped."); return; }
            var rows = ParseResults(_results, out bool ready);
            if (!ready && Time.Ticks < _propertiesUntil) { _due = (long)Time.Ticks + 250; return; }
            if (rows == null) { Finish(false, "Unsupported results layout; original window retained."); return; }
            string key = string.Join("|", rows.Select(r => r.Serial != 0 ? r.Serial.ToString("X8") : r.ItemName + ":" + r.Amount + ":" + r.Price + ":" + r.ListDate));
            if (!_pages.Add(key)) { Finish(false, "Repeated result page; scan stopped to prevent duplicates."); return; }
            _scan.Pages++;
            var known = new HashSet<uint>(_scan.Listings.Where(r => r.Serial != 0).Select(r => r.Serial));
            var incoming = rows.Where(r => r.Serial == 0 || known.Add(r.Serial)).ToList();
            int remaining = 300 - _scan.Listings.Count;
            _scan.Listings.AddRange(incoming.Take(remaining));
            if (incoming.Count > remaining) { Finish(false, "Scan cap reached (300 listings)."); return; }
            if (!ready) _scan.Note = "Some item properties were unavailable.";
            var next = VendorRefreshManager.Action(_results, "NEXT PAGE", "Next", "Next page");
            string pageText = string.Join(" ", VendorLedgerManager.Cells(_results).Select(c => c.Text));
            var pageCount = Regex.Match(pageText, @"\bPage\s+(\d+)\s*(?:of|/)\s*(\d+)", RegexOptions.IgnoreCase);
            if (next == null && pageCount.Success && int.TryParse(pageCount.Groups[1].Value, out int page)
                && int.TryParse(pageCount.Groups[2].Value, out int total) && page < total)
            { Finish(false, "More pages exist, but the Next action could not be identified safely."); return; }
            if (next == null) { Finish(ready && _scan.Note.Length == 0, _scan.Note); return; }
            if (_scan.Pages >= 60 || _scan.Listings.Count >= 300) { Finish(false, "Scan cap reached (60 pages / 300 listings)."); return; }
            var gump = _results; int id = next.ButtonID;
            Schedule(5, () => { if (!gump.IsDisposed) gump.OnButtonClick(id); }, "Reading next results page: " + _watch.Query);
        }

        private static void Finish(bool complete, string note)
        {
            if (!_capture) { Reset(); SetStatus(note); return; }
            if (_capture && _watch != null)
            {
                _scan.Complete = complete; _scan.Note = note ?? "";
                SaveScan(_watch, _scan); VendorLedgerManager.Changed(); _finished++; Revision++;
                if (VendorMarketAnalysis.EvaluateAlert(_watch, _scan))
                    ToastManager.Show("Market watch: " + _watch.Query + " below " + _watch.AlertBelowUnitPrice.Value.ToString("N2", CultureInfo.InvariantCulture)
                        + " gold/unit in the latest scan.", 0x0044, 6000, "vendor-price:" + _watch.Query, AlertCategory.Inventory, AlertSeverity.Info);
                TrimHistory(_profile.VendorWatchItems);
            }
            NextSearch();
        }

        internal static void Update()
        {
            if (!Active) return;
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Reset(); return; }
            if (_capture && !_profile.VendorSearchScansEnabled) { Stop("VS scans disabled."); return; }
            if (Time.Ticks > _deadline) { Stop("VS timed out. Server cooldown, permissions or an unknown layout may have blocked it."); return; }
            if (Time.Ticks < _due) return;
            if (_action != null) { var action = _action; _action = null; action(); return; }
            if (_stage == 6) ReadPage();
        }

        internal static void Stop(string note = "VS scan stopped. Previous completed scans remain saved.")
        {
            if (_capture && _watch != null && _scan != null && _scan.Pages > 0)
            { _scan.Complete = false; _scan.Note = note; SaveScan(_watch, _scan); TrimHistory(_profile.VendorWatchItems); VendorLedgerManager.Changed(); }
            Reset(); SetStatus(note);
        }

        internal static void Reset()
        { EventSink.OPLOnReceive -= PropertiesReceived; _freshProperties.Clear(); _stage = 0; _action = null; _results = null; _watch = null; _scan = null; _profile = null; _queue.Clear(); _pages.Clear(); _capture = _cleared = false; _finished = 0; Revision++; Status = ""; }
    }
}

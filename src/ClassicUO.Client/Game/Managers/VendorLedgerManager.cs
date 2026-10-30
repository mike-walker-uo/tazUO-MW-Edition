using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    internal enum VendorGumpKind { None, Owner, History, Listing }
    internal enum VendorLedgerColumn { Received, Shop, Item, Buyer, Price, Listed, Sold, Amount, FirstSeen, LastSeen, Sales, Stock }

    internal static class VendorLedgerManager
    {
        internal sealed class Cell { internal string Text; internal int X, Y; internal object Tooltip; }
        private static Profile _profile;
        private static uint _lastMobile;
        private static long _lastInteraction, _saveAt, _stockAt;
        private static VendorLedgerRecord _active;
        internal static int Revision { get; private set; }
        private static readonly HashSet<uint> _stockDirty = new HashSet<uint>();
        private static readonly Dictionary<uint, List<VendorSaleRecord>> _pendingProperties = new Dictionary<uint, List<VendorSaleRecord>>();

        internal static string Plain(string text) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(text ?? "", @"<br\s*/?>", "\n", RegexOptions.IgnoreCase), "<[^>]*>", " ")).Trim();
        internal static string Text(Control control) => Plain(control is Label label ? label.Text
            : control is CroppedText cropped ? cropped.Text : control is HtmlControl html ? html.Text : "");
        internal static List<Cell> Cells(Gump gump) => gump.Children.Where(c => c.IsFromServer && !c.IsDisposed && Text(c).Length > 0)
            .SelectMany(c => Text(c).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select((line, i) => new Cell { Text = line.Trim(), X = c.X, Y = c.Y + i * 18, Tooltip = c.Tooltip })).ToList();

        internal static VendorGumpKind Recognize(string text)
        {
            if (Has(text, "Set Price and Description") && Has(text, "Listing fee")) return VendorGumpKind.Listing;
            if (Has(text, "Vendor Sales History") && Has(text, "Buyer") && Has(text, "Sell Price")) return VendorGumpKind.History;
            if (Has(text, "Shop Name") && (Has(text, "View Sales History") || Has(text, "See goods"))) return VendorGumpKind.Owner;
            return VendorGumpKind.None;
        }
        private static bool Has(string text, string value) => (text ?? "").IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool Money(string text, out long value)
        {
            value = 0;
            return Regex.IsMatch(text ?? "", @"^\s*[0-9][0-9,\s]*\s*$")
                && long.TryParse(Regex.Replace(text, @"[,\s]", ""), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        internal static List<VendorSaleRecord> ParseSales(List<Cell> cells)
        {
            var title = cells.FirstOrDefault(c => c.Text.Equals("Item Name", StringComparison.OrdinalIgnoreCase));
            if (title == null) return null;
            var header = cells.Where(c => Math.Abs(c.Y - title.Y) <= 3).OrderBy(c => c.X).ToList();
            if (!header.Any(c => Has(c.Text, "Buyer")) || !header.Any(c => Has(c.Text, "Sell Price"))
                || !header.Any(c => Has(c.Text, "List Date")) || !header.Any(c => Has(c.Text, "Sell Date"))) return null;
            var sales = new List<VendorSaleRecord>();
            var body = cells.Where(c => c.Y > title.Y + 5).OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
            for (int i = 0; i < body.Count;)
            {
                int y = body[i].Y;
                var row = new List<Cell>();
                while (i < body.Count && body[i].Y <= y + 3) row.Add(body[i++]);
                row = row.OrderBy(c => c.X).ToList();
                if (row.Count != 5) { if (row.Any(c => DateText(c.Text))) return null; continue; }
                if (!Money(row[2].Text, out long price) || !DateText(row[3].Text) || !DateText(row[4].Text)) return null;
                var sale = new VendorSaleRecord { ItemName = row[0].Text, Buyer = row[1].Text, Price = price,
                    ListDate = row[3].Text, SellDate = row[4].Text,
                    PropertyText = row[0].Tooltip as string ?? string.Empty,
                    PropertySerial = row[0].Tooltip is uint serial ? serial : 0 };
                var item = sale.PropertySerial == 0 ? null : World.Items.Get(sale.PropertySerial);
                if (item != null) sale.PropertyLayer = item.ItemData.Layer;
                if (sale.PropertySerial != 0 && World.OPL.TryGetNameAndData(sale.PropertySerial, out string name, out string data)
                    && !string.IsNullOrWhiteSpace(data)) sale.PropertyText = name + "\n" + data;
                sales.Add(sale);
                if (sales.Count >= 2000) break;
            }
            return sales;
        }

        private static bool DateText(string text) => Regex.IsMatch(text ?? "", @"^\d{1,4}[/.-]\d{1,2}(?:[/.-]\d{2,4})?(?:\s+.*)?$");
        internal static decimal Gross(IEnumerable<VendorSaleRecord> sales) => sales.Sum(s => (decimal)s.Price);
        internal static string SaleIdentity(VendorSaleRecord sale) => sale.ItemName + "|" + sale.Buyer + "|" + sale.Price + "|" + sale.ListDate + "|" + sale.SellDate;
        internal static string SaleTooltip(VendorSaleRecord sale)
        {
            if (string.IsNullOrWhiteSpace(sale.PropertyText)) return null;
            // Server tooltips may contain HTML breaks or properties without a name.
            // The shared formatter needs a separate name line before the stat lines.
            string name = Plain(sale.ItemName);
            // Split HTML lines before converting colors so each property reaches
            // the shared formatter separately. Keep the server's fallback colors.
            string text = WebUtility.HtmlDecode(sale.PropertyText).Replace("\r\n", "\n").Replace('\r', '\n');
            text = Plain(TextBox.ConvertHTMLColorsToFSS(text));
            text = text.Replace("major Artifact", "Major Artifact").Replace("greater Artifact", "Greater Artifact");
            var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            if (lines.Count > 0 && string.Equals(Regex.Replace(lines[0], @"/c\[[^\]]*\]|/cd", "", RegexOptions.IgnoreCase).Trim(), name, StringComparison.OrdinalIgnoreCase)) lines.RemoveAt(0);
            string properties = name + "\n" + string.Join("\n", lines);
            return ToolTipOverrideData.ProcessTooltipText(properties, sale.PropertyLayer ?? (byte)TooltipLayers.Any) ?? properties;
        }
        internal static bool Matches(VendorSaleRecord sale, string query) => Has(sale.ItemName + " " + sale.Buyer + " "
            + sale.Price.ToString(CultureInfo.InvariantCulture) + " " + sale.ListDate + " " + sale.SellDate, query.Trim());

        internal static IEnumerable<(VendorLedgerRecord Vendor, VendorSaleRecord Sale)> SalesRows(
            IEnumerable<VendorLedgerRecord> vendors, VendorLedgerRecord selected, string query)
            => (selected == null ? vendors : new[] { selected }).SelectMany(v => v.Sales.Select(s => (Vendor: v, Sale: s)))
                .Where(r => Matches(r.Sale, query) || Has(r.Vendor.ShopName, query.Trim()));

        internal static IEnumerable<(VendorLedgerRecord Vendor, VendorSaleRecord Sale)> SortSales(
            IEnumerable<(VendorLedgerRecord Vendor, VendorSaleRecord Sale)> rows, VendorLedgerColumn column, bool descending)
        {
            switch (column)
            {
                case VendorLedgerColumn.Shop: return ByText(rows, r => r.Vendor.ShopName, descending);
                case VendorLedgerColumn.Item: return ByText(rows, r => r.Sale.ItemName, descending);
                case VendorLedgerColumn.Buyer: return ByText(rows, r => r.Sale.Buyer, descending);
                case VendorLedgerColumn.Price: return ByNumber(rows, r => r.Sale.Price, descending);
                case VendorLedgerColumn.Listed: return ByNumber(rows, r => SaleDate(r.Sale.ListDate), descending);
                case VendorLedgerColumn.Sold: return ByNumber(rows, r => SaleDate(r.Sale.SellDate), descending);
                default: return rows;
            }
        }

        internal static IEnumerable<(VendorLedgerRecord Vendor, VendorStockRecord Stock)> SortStock(
            IEnumerable<(VendorLedgerRecord Vendor, VendorStockRecord Stock)> rows, VendorLedgerColumn column, bool descending)
        {
            switch (column)
            {
                case VendorLedgerColumn.Shop: return ByText(rows, r => r.Vendor.ShopName, descending);
                case VendorLedgerColumn.Item: return ByText(rows, r => r.Stock.ItemName, descending);
                case VendorLedgerColumn.Amount: return ByNumber(rows, r => r.Stock.Amount, descending);
                case VendorLedgerColumn.Price: return ByNumber(rows, r => r.Stock.Price, descending);
                case VendorLedgerColumn.FirstSeen: return ByNumber(rows, r => ObservedDate(r.Stock.FirstSeenUtcTicks), descending);
                case VendorLedgerColumn.LastSeen: return ByNumber(rows, r => ObservedDate(r.Stock.LastSeenUtcTicks), descending);
                default: return rows;
            }
        }

        private static IOrderedEnumerable<T> ByText<T>(IEnumerable<T> rows, Func<T, string> key, bool descending)
            => descending ? rows.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(key, StringComparer.OrdinalIgnoreCase);

        // OrderBy remains stable, caches each key once, and never changes the captured records.
        private static IOrderedEnumerable<T> ByNumber<T>(IEnumerable<T> rows, Func<T, long?> key, bool descending)
            => rows.OrderBy(key, Comparer<long?>.Create((a, b) => !a.HasValue ? (b.HasValue ? 1 : 0)
                : !b.HasValue ? -1 : descending ? b.Value.CompareTo(a.Value) : a.Value.CompareTo(b.Value)));

        private static long? ObservedDate(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks ? (long?)ticks : null;
        private static readonly string[] SaleDateFormats = { "M/d/yyyy", "M/d/yy", "yyyy-MM-dd" };
        private static long? SaleDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (DateTime.TryParseExact(value, SaleDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                return date.Ticks;
            // Server M/d dates have no year: use a fixed leap year for calendar ordering.
            return DateTime.TryParseExact("2000/" + value, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                ? (long?)date.Ticks : null;
        }

        internal static IEnumerable<(VendorLedgerRecord Vendor, string ItemName, int Sales)> RestockRows(
            IEnumerable<VendorLedgerRecord> vendors, VendorLedgerRecord selected, string query)
            => (selected == null ? vendors : new[] { selected }).SelectMany(RestockItems)
                .Where(r => !r.Vendor.RestockHiddenNames.Contains(r.ItemName, StringComparer.OrdinalIgnoreCase)
                    && (Has(r.ItemName, query.Trim()) || Has(r.Vendor.ShopName, query.Trim())))
                .OrderBy(r => r.ItemName).ThenBy(r => r.Vendor.ShopName);

        internal static IEnumerable<(VendorLedgerRecord Vendor, string ItemName, int Sales)> SortRestock(
            IEnumerable<(VendorLedgerRecord Vendor, string ItemName, int Sales)> rows, VendorLedgerColumn column, bool descending)
        {
            switch (column)
            {
                case VendorLedgerColumn.Shop: return ByText(rows, r => r.Vendor.ShopName, descending);
                case VendorLedgerColumn.Item: return ByText(rows, r => r.ItemName, descending);
                case VendorLedgerColumn.Sales: return ByNumber(rows, r => r.Sales, descending);
                case VendorLedgerColumn.Stock: return ByNumber(rows, r =>
                {
                    var stock = RestockStock(r.Vendor, r.ItemName);
                    return stock.Complete || stock.Listings > 0 ? (long?)stock.Listings : null;
                }, descending);
                default: return rows;
            }
        }

        internal sealed class RestockGroup
        {
            internal readonly string ItemName, Shops;
            internal readonly (VendorLedgerRecord Vendor, string ItemName, int Sales)[] Members;
            internal readonly long Sales;
            internal readonly (VendorLedgerRecord Vendor, VendorSaleRecord Sale)[] PriceHistory;
            internal readonly (int Count, long? Low, decimal? Median, long? High) Prices;
            internal readonly (int Listings, long Units, bool Complete) Stock;
            internal readonly bool OlderThanHistory;

            internal RestockGroup(string name, (VendorLedgerRecord Vendor, string ItemName, int Sales)[] members,
                (VendorLedgerRecord Vendor, VendorSaleRecord Sale)[] priceHistory)
            {
                ItemName = name; Members = members;
                Shops = string.Join(", ", members.Select(r => r.Vendor.ShopName));
                Sales = members.Sum(r => (long)r.Sales);
                PriceHistory = priceHistory;
                Prices = SalePrices(PriceHistory.Select(r => r.Sale));
                int listings = 0; long units = 0; bool complete = true;
                foreach (var member in members)
                {
                    var stock = RestockStock(member.Vendor, member.ItemName);
                    listings += stock.Listings; units += stock.Units; complete &= stock.Complete;
                    OlderThanHistory |= member.Vendor.LastStockUtcTicks > 0
                        && member.Vendor.LastHistoryUtcTicks > member.Vendor.LastStockUtcTicks;
                }
                Stock = (listings, units, complete);
            }
        }

        internal static (int Count, long? Low, decimal? Median, long? High) SalePrices(IEnumerable<VendorSaleRecord> sales)
        {
            var prices = sales.Select(s => s.Price).OrderBy(p => p).ToArray();
            if (prices.Length == 0) return (0, null, null, null);
            int middle = prices.Length / 2;
            decimal median = prices.Length % 2 == 0 ? ((decimal)prices[middle - 1] + prices[middle]) / 2 : prices[middle];
            return (prices.Length, prices[0], median, prices[prices.Length - 1]);
        }

        internal static IEnumerable<RestockGroup> GroupRestock(IEnumerable<VendorLedgerRecord> vendors,
            VendorLedgerRecord selected, string query)
        {
            var scope = (selected == null ? vendors : new[] { selected }).OrderBy(v => v.ShopName, StringComparer.OrdinalIgnoreCase).ToArray();
            var sales = scope.ToDictionary(v => v, v => v.Sales.ToLookup(s => s.ItemName, StringComparer.OrdinalIgnoreCase));
            return RestockRows(scope, null, "").GroupBy(r => r.ItemName, StringComparer.OrdinalIgnoreCase)
                .Where(g => Has(g.Key, query.Trim()) || g.Any(r => Has(r.Vendor.ShopName, query.Trim()))).Select(group =>
            {
                var entries = group.ToDictionary(r => r.Vendor);
                // Include vendors with no sales/stock row: an incomplete scan there cannot prove zero stock.
                var members = scope.Where(v => !v.RestockHiddenNames.Contains(group.Key, StringComparer.OrdinalIgnoreCase))
                    .Select(v => entries.TryGetValue(v, out var row) ? row : (Vendor: v, ItemName: group.Key, Sales: 0)).ToArray();
                var history = members.SelectMany(r => sales[r.Vendor][r.ItemName].Select(s => (Vendor: r.Vendor, Sale: s))).ToArray();
                return new RestockGroup(group.Key, members, history);
            }).OrderBy(g => g.ItemName, StringComparer.OrdinalIgnoreCase);
        }

        internal static IEnumerable<RestockGroup> SortRestock(IEnumerable<RestockGroup> rows, VendorLedgerColumn column, bool descending)
        {
            switch (column)
            {
                case VendorLedgerColumn.Shop: return ByText(rows, r => r.Shops, descending);
                case VendorLedgerColumn.Item: return ByText(rows, r => r.ItemName, descending);
                case VendorLedgerColumn.Sales: return ByNumber(rows, r => r.Sales, descending);
                case VendorLedgerColumn.Stock: return ByNumber(rows, r => r.Stock.Complete || r.Stock.Listings > 0 ? (long?)r.Stock.Listings : null, descending);
                case VendorLedgerColumn.Price: return rows.OrderBy(r => r.Prices.Median, Comparer<decimal?>.Create((a, b) =>
                    !a.HasValue ? b.HasValue ? 1 : 0 : !b.HasValue ? -1 : descending ? b.Value.CompareTo(a.Value) : a.Value.CompareTo(b.Value)));
                default: return rows;
            }
        }

        private static IEnumerable<(VendorLedgerRecord Vendor, string ItemName, int Sales)> RestockItems(VendorLedgerRecord vendor)
        {
            var sales = vendor.Sales.ToLookup(s => s.ItemName, StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(sales.Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            var bundles = new Dictionary<string, HashSet<int?>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sale in sales)
            {
                string key = RestockName(sale.Key, out int? amount);
                if (!bundles.TryGetValue(key, out var amounts)) bundles[key] = amounts = new HashSet<int?>();
                amounts.Add(amount);
                yield return (vendor, sale.Key, sale.Count());
            }
            foreach (var stock in vendor.Stock)
            {
                string key = RestockName(stock.ItemName, out _);
                bool hasSales = bundles.TryGetValue(key, out var amounts);
                if (sales.Contains(stock.ItemName) || hasSales && (amounts.Contains(null) || amounts.Contains(stock.Amount))) continue;
                string name = stock.ItemName;
                // Keep unsold bundle sizes separate from sold ones even when OPL omits the count.
                RestockName(name, out int? shownAmount);
                if (hasSales && !shownAmount.HasValue && stock.Amount > 0)
                    name = stock.Amount + " " + key;
                if (names.Add(name)) yield return (vendor, name, 0);
            }
        }

        private static string RestockName(string name, out int? amount)
        {
            name = (name ?? "").Trim(); amount = null;
            int space = name.IndexOf(' ');
            if (space > 0 && int.TryParse(name.Substring(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0)
            { amount = count; return name.Substring(space + 1).TrimStart(); }
            if (name.StartsWith("a ", StringComparison.OrdinalIgnoreCase)) return name.Substring(2);
            if (name.StartsWith("an ", StringComparison.OrdinalIgnoreCase)) return name.Substring(3);
            return name;
        }

        internal static (int Listings, long Units, bool Complete) RestockStock(VendorLedgerRecord vendor, string name)
        {
            string key = RestockName(name, out int? amount);
            int listings = 0; long units = 0;
            bool complete = vendor.StockComplete && vendor.LastStockUtcTicks > 0 && vendor.LastStockUtcTicks <= DateTime.MaxValue.Ticks;
            foreach (var stock in vendor.Stock)
            {
                if (!string.Equals(name, stock.ItemName, StringComparison.OrdinalIgnoreCase)
                    && (!string.Equals(key, RestockName(stock.ItemName, out _), StringComparison.OrdinalIgnoreCase)
                        || amount.HasValue && amount.Value != stock.Amount)) continue;
                listings++; units += Math.Max(0L, stock.Amount);
                if (stock.Amount <= 0) complete = false;
            }
            return (listings, units, complete);
        }

        internal static bool RemoveRestock(VendorLedgerRecord vendor, string name)
        {
            if (vendor.RestockHiddenNames.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
            vendor.RestockHiddenNames.Add(name); return true;
        }

        internal static bool RestoreRestock(IEnumerable<VendorLedgerRecord> vendors, VendorLedgerRecord selected)
        {
            bool changed = false;
            foreach (var vendor in selected == null ? vendors : new[] { selected })
                if (vendor.RestockHiddenNames.Count > 0) { vendor.RestockHiddenNames.Clear(); changed = true; }
            return changed;
        }

        internal static IEnumerable<(VendorLedgerRecord Vendor, VendorStockRecord Stock)> StaleStockRows(
            IEnumerable<VendorLedgerRecord> vendors, VendorLedgerRecord selected, string query, int days, long now)
            => (selected == null ? vendors : new[] { selected }).SelectMany(v => v.Stock.Select(s => (Vendor: v, Stock: s)))
                .Where(r => (Has(r.Stock.ItemName, query.Trim()) || Has(r.Vendor.ShopName, query.Trim()))
                    && r.Stock.FirstSeenUtcTicks > 0 && r.Stock.FirstSeenUtcTicks <= now
                    && new TimeSpan(now - r.Stock.FirstSeenUtcTicks).TotalDays >= days)
                .OrderBy(r => r.Stock.FirstSeenUtcTicks).ThenBy(r => r.Vendor.ShopName);

        private static void EnsureProfile()
        {
            if (_profile == ProfileManager.CurrentProfile) return;
            Reset(); _profile = ProfileManager.CurrentProfile;
            if (_profile != null) EventSink.OPLOnReceive += PropertiesReceived;
        }
        internal static List<VendorLedgerRecord> Records
        {
            get
            {
                EnsureProfile();
                if (_profile == null) return new List<VendorLedgerRecord>();
                return _profile.VendorLedger ?? (_profile.VendorLedger = new List<VendorLedgerRecord>());
            }
        }

        internal static void ObserveInteraction(uint serial)
        {
            EnsureProfile();
            if (World.Mobiles.Get(serial) == null || serial == World.Player) return;
            _lastMobile = serial; _lastInteraction = Time.Ticks;
        }

        internal static void TargetVendor()
        {
            if (!World.InGame || TargetManager.IsTargeting) { GameActions.Print("Finish your current target first.", 0x35); return; }
            TargetManager.SetTargeting(CursorTarget.AddLedgerVendor, CursorType.Target, TargetType.Neutral);
            GameActions.Print("Target your vendor. Ownership is verified when its owner menu opens.", 0x35);
        }

        internal static VendorLedgerRecord AddVendor(Profile profile, uint serial, string name)
        {
            if (profile == null || serial == 0) return null;
            string key = "vendor:" + serial.ToString("X8");
            var record = profile.VendorLedger.FirstOrDefault(r => r.Key == key);
            if (record != null) return record;
            if (profile.VendorLedger.Count >= 128) return null;
            record = new VendorLedgerRecord { Key = key, VendorSerial = serial, ShopName = name ?? "Vendor" };
            profile.VendorLedger.Add(record);
            return record;
        }

        internal static void AddTarget(Entity entity)
        {
            EnsureProfile();
            if (!(entity is Mobile) || entity == World.Player) { GameActions.Print("Target a vendor mobile.", 0x35); return; }
            var record = AddVendor(_profile, entity.Serial, entity.Name);
            if (record == null) { GameActions.Print("Vendor list is full (128).", 0x35); return; }
            _profile.Save(ProfileManager.ProfilePath, false);
            VendorLedgerGump.Open(record);
            GameActions.Print("Vendor saved. Use Update vendors nearby to capture its owner menu, history and goods.", 0x35);
        }

        internal static bool RemoveVendor(VendorLedgerRecord vendor)
        {
            EnsureProfile();
            if (vendor == null || _profile?.VendorLedger == null || !_profile.VendorLedger.Remove(vendor)) return false;
            TrackProperties(vendor.Sales, Enumerable.Empty<VendorSaleRecord>());
            if (VendorRefreshManager.Active) VendorRefreshManager.Stop();
            if (_active == vendor) _active = null;
            if (_lastMobile == vendor.VendorSerial) { _lastMobile = 0; _lastInteraction = 0; }
            _stockDirty.Remove(vendor.GoodsSerial);
            Changed();
            return true;
        }

        internal static VendorLedgerRecord ObserveGump(Gump gump, VendorGumpKind kind, List<Cell> cells)
        {
            EnsureProfile();
            if (_profile == null || kind == VendorGumpKind.None || kind == VendorGumpKind.Listing) return _active;
            string shop;
            if (kind == VendorGumpKind.Owner)
            {
                Cell label = cells.FirstOrDefault(c => Has(c.Text, "Shop Name"));
                shop = cells.Where(c => c != label && c.X > label.X && Math.Abs(c.Y - label.Y) <= 5).OrderBy(c => c.X).FirstOrDefault()?.Text;
                if (string.IsNullOrWhiteSpace(shop) && label.Text.Contains(":")) shop = label.Text.Substring(label.Text.IndexOf(':') + 1).Trim();
                if (string.IsNullOrWhiteSpace(shop)) return null;
                // Server gump serials are independent counters, not vendor mobile serials.
                // Only an accepted recent interaction identifies the vendor that opened this menu.
                uint serial = Time.Ticks - _lastInteraction <= 15000 && World.Mobiles.Get(_lastMobile) != null ? _lastMobile : 0;
                string key = serial != 0 ? "vendor:" + serial.ToString("X8") : "shop:" + shop.ToUpperInvariant();
                _active = Records.FirstOrDefault(r => r.Key == key);
                if (serial != 0)
                {
                    var unbound = Records.Where(r => r.VendorSerial == 0 && string.Equals(r.ShopName, shop, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (unbound.Count == 1 && !Records.Any(r => r.VendorSerial != 0 && r.VendorSerial != serial
                        && string.Equals(r.ShopName, shop, StringComparison.OrdinalIgnoreCase)))
                    {
                        var previous = unbound[0];
                        if (_active == null) { _active = previous; _active.Key = key; _active.VendorSerial = serial; }
                        else
                        {
                            if (previous.LastHistoryUtcTicks > _active.LastHistoryUtcTicks)
                            {
                                _active.Sales = previous.Sales; _active.LastHistoryUtcTicks = previous.LastHistoryUtcTicks;
                                _active.RestockedNames = previous.RestockedNames;
                            }
                            if (previous.LastStockUtcTicks > _active.LastStockUtcTicks)
                            { _active.Stock = previous.Stock; _active.LastStockUtcTicks = previous.LastStockUtcTicks; _active.StockComplete = previous.StockComplete; _active.StockNote = previous.StockNote; }
                            foreach (string name in previous.RestockHiddenNames)
                                RemoveRestock(_active, name);
                            Records.Remove(previous);
                        }
                    }
                }
                if (_active == null)
                {
                    if (Records.Count >= 128) return null;
                    Records.Add(_active = new VendorLedgerRecord { Key = key, VendorSerial = serial });
                }
                _active.ShopName = shop;
                _active.OwnerVerified = cells.Any(c => Has(c.Text, "Dismiss Vendor"));
                var goldLabel = cells.FirstOrDefault(c => Has(c.Text, "Gold held"));
                var gold = goldLabel == null ? null : cells.Where(c => c != goldLabel && c.X > goldLabel.X && Math.Abs(c.Y - goldLabel.Y) <= 5).OrderBy(c => c.X).FirstOrDefault();
                _active.GoldHeld = gold != null && Money(gold.Text, out long value) ? (long?)value : null;
                var backpack = World.Mobiles.Get(serial)?.FindItemByLayer(Layer.Backpack);
                if (backpack != null) _active.GoodsSerial = backpack.Serial;
                _active.LastObservedUtcTicks = DateTime.UtcNow.Ticks;
            }
            else
            {
                var title = cells.FirstOrDefault(c => Has(c.Text, "Vendor Sales History"));
                shop = cells.Where(c => c.Y > title.Y && c.Y < title.Y + 50 && !Has(c.Text, "Vendor Sales History"))
                    .OrderBy(c => c.Y).FirstOrDefault()?.Text;
                if (string.IsNullOrWhiteSpace(shop)) shop = Regex.Replace(title.Text, @"^\s*Vendor Sales History\s*", "", RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrWhiteSpace(shop)) return null;
                var matches = Records.Where(r => string.Equals(r.ShopName, shop, StringComparison.OrdinalIgnoreCase)).ToList();
                _active = _active != null && string.Equals(_active.ShopName, shop, StringComparison.OrdinalIgnoreCase)
                    ? _active : matches.Count == 1 ? matches[0] : null;
                if (_active == null)
                {
                    if (Records.Count >= 128) return null;
                    Records.Add(_active = new VendorLedgerRecord { Key = "history:" + shop.ToUpperInvariant(), ShopName = shop });
                }
                var sales = ParseSales(cells);
                if (sales == null) return _active;
                foreach (string name in _active.RestockedNames.ToArray())
                    if (string.Join("\n", _active.Sales.Where(s => s.ItemName == name).Select(SaleIdentity).OrderBy(s => s, StringComparer.Ordinal))
                        != string.Join("\n", sales.Where(s => s.ItemName == name).Select(SaleIdentity).OrderBy(s => s, StringComparer.Ordinal))) _active.RestockedNames.Remove(name);
                TrackProperties(_active.Sales, sales);
                _active.Sales = sales;
                _active.LastHistoryUtcTicks = DateTime.UtcNow.Ticks;
            }
            Changed();
            VendorRefreshManager.ObserveGump(gump, kind, _active);
            return _active;
        }

        private static void TrackProperties(IEnumerable<VendorSaleRecord> previous, IEnumerable<VendorSaleRecord> current)
        {
            foreach (var sale in previous)
                if (_pendingProperties.TryGetValue(sale.PropertySerial, out var previousPending))
                {
                    previousPending.Remove(sale);
                    if (previousPending.Count == 0) _pendingProperties.Remove(sale.PropertySerial);
                }
            foreach (var sale in current)
                if (sale.PropertySerial != 0 && string.IsNullOrEmpty(sale.PropertyText))
                {
                    if (!_pendingProperties.TryGetValue(sale.PropertySerial, out var currentPending))
                        _pendingProperties[sale.PropertySerial] = currentPending = new List<VendorSaleRecord>();
                    currentPending.Add(sale);
                }
        }

        private static void PropertiesReceived(object sender, OPLEventArgs e)
        {
            if (_profile == null || _profile != ProfileManager.CurrentProfile) return;
            uint owner = World.Items.Get(e.Serial)?.RootContainer ?? 0;
            var vendor = owner == 0 ? null : _profile.VendorLedger.FirstOrDefault(v => v.VendorSerial != 0 && v.VendorSerial == owner);
            if (vendor != null && vendor.GoodsSerial != 0)
            { _stockDirty.Add(vendor.GoodsSerial); _stockAt = (long)Time.Ticks + 1000; }
            if (!_pendingProperties.TryGetValue(e.Serial, out var pending) || string.IsNullOrWhiteSpace(e.Data)) return;
            foreach (var sale in pending) sale.PropertyText = e.Name + "\n" + e.Data;
            _pendingProperties.Remove(e.Serial);
            Changed();
        }

        internal static void ObserveContainer(uint container)
        {
            EnsureProfile();
            if (_profile?.VendorLedger == null || _profile.VendorLedger.Count == 0) return;
            Item item = World.Items.Get(container);
            if (item == null) return;
            uint owner = item.RootContainer;
            if (!_profile.VendorLedger.Any(r => r.VendorSerial != 0 && r.VendorSerial == owner)) return;
            var visited = new HashSet<uint>();
            while (item != null && visited.Add(item.Serial))
            {
                var record = Records.FirstOrDefault(r => r.VendorSerial != 0 && r.VendorSerial == item.Container);
                if (record != null && item.Layer == Layer.Backpack)
                {
                    record.GoodsSerial = item.Serial; _stockDirty.Add(item.Serial); _stockAt = (long)Time.Ticks + 1000;
                    VendorRefreshManager.ObserveContainer(container, record);
                    return;
                }
                item = World.Items.Get(item.Container);
            }
        }

        private static long? StockPrice(string data)
        {
            var price = Regex.Match(Plain(data), @"\bPrice\s*:\s*([\d, ]+)", RegexOptions.IgnoreCase);
            return price.Success && Money(price.Groups[1].Value.Trim(), out long value) ? (long?)value : null;
        }

        // Books use the container tile flag too. Priced bags are one sale lot, not display shelves.
        internal static bool StockContainer(Item item) => ItemFinderManager.CanScanContainer(item)
            && (!World.OPL.TryGetNameAndData(item.Serial, out _, out string data) || !StockPrice(data).HasValue);

        private static void CaptureStock(VendorLedgerRecord record)
        {
            Item root = World.Items.Get(record.GoodsSerial);
            if (root == null || root.Container != record.VendorSerial) return;
            var stock = new List<VendorStockRecord>();
            var previousStock = record.Stock.GroupBy(s => s.Serial).ToDictionary(g => g.Key, g => g.First());
            var queue = new Queue<Item>(); var seen = new HashSet<uint>(); queue.Enqueue(root);
            int unopened = root.Opened ? 0 : 1, unnamed = 0;
            long now = DateTime.UtcNow.Ticks;
            while (queue.Count > 0 && stock.Count < 4096)
            {
                Item container = queue.Dequeue();
                if (!seen.Add(container.Serial)) continue;
                for (var node = container.Items; node != null && stock.Count < 4096; node = node.Next)
                {
                    if (!(node is Item item) || item.IsDestroyed) continue;
                    bool properties = World.OPL.TryGetNameAndData(item.Serial, out string name, out string data);
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(item.Name))
                    {
                        unnamed++;
                        if (World.ClientFeatures.TooltipsEnabled) PacketHandlers.AddMegaClilocRequest(item.Serial);
                    }
                    else if (!properties && World.ClientFeatures.TooltipsEnabled)
                        PacketHandlers.AddMegaClilocRequest(item.Serial);
                    previousStock.TryGetValue(item.Serial, out var previous);
                    long? price = StockPrice(data);
                    stock.Add(new VendorStockRecord { Serial = item.Serial, ItemName = string.IsNullOrWhiteSpace(name) ? previous?.ItemName ?? item.Name ?? $"Item {item.Serial:X8}" : Plain(name),
                        Amount = item.Amount, Price = properties ? price : previous?.Price,
                        FirstSeenUtcTicks = previous?.FirstSeenUtcTicks ?? now, LastSeenUtcTicks = now });
                    if (StockContainer(item))
                    {
                        if (!item.Opened) unopened++;
                        if (!item.IsEmpty) queue.Enqueue(item);
                    }
                }
            }
            record.Stock = stock; record.LastStockUtcTicks = now;
            bool limited = stock.Count >= 4096 || queue.Count != 0;
            record.StockComplete = unopened == 0 && unnamed == 0 && !limited;
            record.StockNote = record.StockComplete ? "All display bags received; item names loaded."
                : unopened + " unopened display bags; " + unnamed + " item names pending."
                    + (limited ? " Capture limit reached." : "");
            Changed();
        }

        internal static void Changed() { _saveAt = (long)Time.Ticks + 2000; Revision++; }
        internal static void Update()
        {
            EnsureProfile();
            if (_profile == null || !World.InGame) return;
            if (_stockDirty.Count > 0 && Time.Ticks >= _stockAt)
            {
                uint serial = _stockDirty.First(); _stockDirty.Remove(serial);
                var record = Records.FirstOrDefault(r => r.GoodsSerial == serial);
                if (record != null) CaptureStock(record);
            }
            if (_saveAt != 0 && Time.Ticks >= _saveAt) { _saveAt = 0; _profile.Save(ProfileManager.ProfilePath, false); }
        }

        internal static void Reset()
        {
            EventSink.OPLOnReceive -= PropertiesReceived;
            _pendingProperties.Clear();
            _profile = null; _lastMobile = 0; _lastInteraction = 0; _active = null; _stockDirty.Clear(); _saveAt = _stockAt = 0;
        }
    }
}

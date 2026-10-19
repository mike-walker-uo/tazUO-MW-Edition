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

namespace ClassicUO.Game.Managers
{
    internal enum VendorGumpKind { None, Owner, History, Listing }

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
            if (Has(text, "Shop Name") && Has(text, "Dismiss Vendor") && Has(text, "View Sales History")) return VendorGumpKind.Owner;
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
            string text = Plain(sale.PropertyText).Replace("\r\n", "\n").Replace('\r', '\n');
            text = Regex.Replace(text, @"/c\[[^\]]*\]|/cd", "", RegexOptions.IgnoreCase);
            var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            if (lines.Count > 0 && string.Equals(lines[0], name, StringComparison.OrdinalIgnoreCase)) lines.RemoveAt(0);
            string properties = name + "\n" + string.Join("\n", lines);
            return ToolTipOverrideData.ProcessTooltipText(properties, sale.PropertyLayer ?? (byte)TooltipLayers.Any) ?? properties;
        }
        internal static bool Matches(VendorSaleRecord sale, string query) => Has(sale.ItemName + " " + sale.Buyer + " "
            + sale.Price.ToString(CultureInfo.InvariantCulture) + " " + sale.ListDate + " " + sale.SellDate, query.Trim());

        internal static IEnumerable<(VendorLedgerRecord Vendor, VendorSaleRecord Sale)> SalesRows(
            IEnumerable<VendorLedgerRecord> vendors, VendorLedgerRecord selected, string query)
            => (selected == null ? vendors : new[] { selected }).SelectMany(v => v.Sales.Select(s => (Vendor: v, Sale: s)))
                .Where(r => Matches(r.Sale, query) || Has(r.Vendor.ShopName, query.Trim()));

        internal static IEnumerable<(VendorLedgerRecord Vendor, string ItemName, int Sales)> RestockRows(
            IEnumerable<VendorLedgerRecord> vendors, VendorLedgerRecord selected, string query)
            => (selected == null ? vendors : new[] { selected }).SelectMany(v => v.Sales
                .Where(s => Has(s.ItemName, query.Trim()) || Has(v.ShopName, query.Trim())).GroupBy(s => s.ItemName)
                .Select(g => (Vendor: v, ItemName: g.Key, Sales: g.Count())))
                .OrderBy(r => r.ItemName).ThenBy(r => r.Vendor.ShopName);

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
                            { _active.Stock = previous.Stock; _active.LastStockUtcTicks = previous.LastStockUtcTicks; }
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
                _active.OwnerVerified = true;
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

        private static void CaptureStock(VendorLedgerRecord record)
        {
            Item root = World.Items.Get(record.GoodsSerial);
            if (root == null || root.Container != record.VendorSerial) return;
            var stock = new List<VendorStockRecord>();
            var previousStock = record.Stock.GroupBy(s => s.Serial).ToDictionary(g => g.Key, g => g.First());
            var queue = new Queue<Item>(); var seen = new HashSet<uint>(); queue.Enqueue(root);
            long now = DateTime.UtcNow.Ticks;
            while (queue.Count > 0 && stock.Count < 4096)
            {
                Item container = queue.Dequeue();
                if (!seen.Add(container.Serial)) continue;
                for (var node = container.Items; node != null && stock.Count < 4096; node = node.Next)
                {
                    if (!(node is Item item) || item.IsDestroyed) continue;
                    bool properties = World.OPL.TryGetNameAndData(item.Serial, out string name, out string data);
                    previousStock.TryGetValue(item.Serial, out var previous);
                    var price = Regex.Match(Plain(data), @"\bPrice\s*:\s*([\d, ]+)", RegexOptions.IgnoreCase);
                    stock.Add(new VendorStockRecord { Serial = item.Serial, ItemName = string.IsNullOrWhiteSpace(name) ? previous?.ItemName ?? item.Name ?? $"Item {item.Serial:X8}" : Plain(name),
                        Amount = item.Amount, Price = properties ? price.Success && Money(price.Groups[1].Value.Trim(), out long value) ? (long?)value : null : previous?.Price,
                        FirstSeenUtcTicks = previous?.FirstSeenUtcTicks ?? now, LastSeenUtcTicks = now });
                    if (item.ItemData.IsContainer && !item.IsEmpty) queue.Enqueue(item);
                }
            }
            record.Stock = stock; record.LastStockUtcTicks = now; Changed();
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

using System;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorRestockDetailsGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly VendorLedgerRecord[] _vendors;
        private readonly string _name;
        private VendorLedgerManager.RestockGroup _group;
        private int _tab, _page, _revision;
        private VendorLedgerColumn _sort = VendorLedgerColumn.Sold;
        private bool _descending = true;

        private VendorRestockDetailsGump(VendorLedgerManager.RestockGroup group) : base(0, 0)
        {
            _group = group; _name = group.ItemName; _vendors = group.Members.Select(r => r.Vendor).ToArray();
            _revision = VendorLedgerManager.Revision;
            Width = 800; Height = 620; WantUpdateSize = false; CanMove = AcceptMouseInput = true;
            CanCloseWithRightClick = CanCloseWithEsc = true;
            Build(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        internal static void Open(VendorLedgerManager.RestockGroup group)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            UIManager.GetGump<VendorRestockDetailsGump>()?.Dispose();
            UIManager.Add(new VendorRestockDetailsGump(group));
        }

        private static Label Text(Control parent, string text, int x, int y, int width, ushort hue = 0x0481)
        {
            var label = new Label(text, true, hue, width, font: 1, style: FontStyle.Cropped) { X = x, Y = y, AcceptMouseInput = true };
            label.SetTooltip(text, 360); parent.Add(label); return label;
        }
        private static string Price(decimal? value) => VendorLedgerGump.RestockPrice(value);
        private static string Stamp(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("g") : "not scanned";

        private void Build()
        {
            Clear(); Children.Clear();
            Add(new VendorGumpArtwork(Width, Height));
            Text(this, "RESTOCK DETAILS", 28, 22, 600);
            Add(new VendorLocalButton(684, 20, 88, "Close", Dispose));
            Text(this, _name, 28, 58, 744, 0x0035);
            // Solid panels keep the ledger texture and rows underneath from competing with the text.
            Add(new AlphaBlendControl(1) { X = 24, Y = 90, Width = 752, Height = 514, BaseColor = new Color(8, 18, 14) });
            Add(new AlphaBlendControl(1) { X = 28, Y = 94, Width = 744, Height = 86, BaseColor = new Color(18, 36, 28) });
            Text(this, "MEDIAN SOLD", 40, 104, 210);
            Text(this, Price(_group?.Prices.Median), 40, 136, 210, 0x0035);
            Text(this, "LOW / HIGH", 270, 104, 270);
            Text(this, _group != null && _group.Prices.Count > 0
                ? Price(_group.Prices.Low) + " / " + Price(_group.Prices.High) : "No sales", 270, 136, 270);
            Text(this, "SAVED SALES", 560, 104, 200);
            Text(this, (_group?.Prices.Count ?? 0).ToString(CultureInfo.InvariantCulture), 560, 136, 200);
            Text(this, "Gold per sold listing / bundle. Sale prices include the full listing quantity.", 40, 190, 720);
            Add(new VendorLocalButton(28, 220, 150, (_tab == 0 ? "* " : "") + "Vendors", () => { _tab = 0; Build(); }));
            Add(new VendorLocalButton(188, 220, 180, (_tab == 1 ? "* " : "") + "Price history", () => { _tab = 1; Build(); }));
            if (_group != null)
                Add(new VendorLocalButton(520, 220, 252, "Prepare listing", () => VendorListingTemplatesGump.OpenForRestock(_group)));
            var body = new ScrollArea(28, 256, 744, 280, true); Add(body);
            if (_group == null) Text(body, "Item removed from this restock scope.", 10, 10, 704);
            else if (_tab == 0) Vendors(body);
            else History(body);
            Text(this, "Name-based history: items with different stats may share a name. Hover a sale price for saved stats.", 40, 580, 720);
        }

        private void Vendors(ScrollArea body)
        {
            Text(body, "SHOP", 10, 0, 250); Text(body, "SALES", 296, 0, 60);
            Text(body, "STOCK", 376, 0, 110); Text(body, "MEDIAN SOLD", 530, 0, 180);
            int y = 30;
            foreach (var member in _group.Members)
            {
                var vendor = member.Vendor;
                var stock = VendorLedgerManager.RestockStock(vendor, member.ItemName);
                bool older = vendor.LastStockUtcTicks > 0 && vendor.LastHistoryUtcTicks > vendor.LastStockUtcTicks;
                var prices = VendorLedgerManager.SalePrices(_group.PriceHistory.Where(r => r.Vendor == vendor).Select(r => r.Sale));
                body.Add(new AlphaBlendControl(1) { X = 2, Y = y, Width = 722, Height = 84, BaseColor = new Color(18, 31, 25) });
                var open = new VendorLocalButton(10, y + 4, 266, vendor.ShopName, () => { VendorLedgerGump.Open(vendor, 3); Dispose(); });
                open.SetTooltip("Open this vendor's restock list.\n0x" + vendor.VendorSerial.ToString("X8"), 300); body.Add(open);
                Text(body, member.Sales.ToString(), 296, y + 8, 60);
                Text(body, VendorLedgerGump.RestockCount(stock, older), 376, y + 8, 110,
                    stock.Complete && !older && stock.Listings == 0 ? (ushort)0x0021 : (ushort)0x0481);
                Text(body, Price(prices.Median), 530, y + 8, 180, 0x0035);
                Text(body, "Stock captured: " + Stamp(vendor.LastStockUtcTicks) + " | Units observed: " + stock.Units + (stock.Complete ? "" : "+"), 10, y + 34, 704);
                Text(body, older ? "History is newer than stock. Update this vendor before judging shortages."
                    : string.IsNullOrWhiteSpace(vendor.StockNote) ? stock.Complete ? "Complete stock scan." : "Stock scan incomplete or unavailable."
                    : vendor.StockNote, 10, y + 60, 704);
                y += 94;
            }
            Text(this, "Click a shop to open its Restock list. Scroll for more vendors.", 40, 548, 720);
        }

        private void Header(ScrollArea body, string text, VendorLedgerColumn column, int x, int width)
        {
            var label = Text(body, text + (_sort == column ? _descending ? " v" : " ^" : ""), x, 0, width);
            label.AcceptMouseInput = true;
            label.SetTooltip("Click to sort; click again to reverse.", 260);
            label.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtonType.Left) return;
                _descending = _sort == column && !_descending; _sort = column; _page = 0; Build();
            };
        }

        private void History(ScrollArea body)
        {
            Header(body, "SHOP", VendorLedgerColumn.Shop, 10, 180); Header(body, "BUYER", VendorLedgerColumn.Buyer, 200, 150);
            Header(body, "SOLD PRICE", VendorLedgerColumn.Price, 360, 125);
            Header(body, "LISTED", VendorLedgerColumn.Listed, 495, 95); Header(body, "SOLD", VendorLedgerColumn.Sold, 600, 110);
            var rows = VendorLedgerManager.SortSales(_group.PriceHistory, _sort, _descending).ToArray();
            _page = Math.Min(_page, Math.Max(0, (rows.Length - 1) / 8));
            int start = _page * 8;
            for (int i = start; i < Math.Min(start + 8, rows.Length); i++)
            {
                var row = rows[i]; int y = 30 + (i - start) * 30;
                body.Add(new AlphaBlendControl(1) { X = 2, Y = y, Width = 722, Height = 28,
                    BaseColor = i % 2 == 0 ? new Color(18, 31, 25) : new Color(10, 22, 17) });
                Text(body, row.Vendor.ShopName, 10, y + 5, 180); Text(body, row.Sale.Buyer, 200, y + 5, 150);
                var price = Text(body, Price(row.Sale.Price), 360, y + 5, 125, 0x0035);
                price.AcceptMouseInput = true;
                price.SetTooltip(VendorLedgerManager.SaleTooltip(row.Sale) ?? "No saved item properties.", 360);
                Text(body, row.Sale.ListDate, 495, y + 5, 95); Text(body, row.Sale.SellDate, 600, y + 5, 110);
            }
            if (rows.Length == 0) Text(body, "No saved sales for this item. Update vendors to capture their sales history.", 10, 34, 704);
            Add(new VendorLocalButton(28, 544, 90, "Previous", () => { _page--; Build(); }) { IsEnabled = _page > 0 });
            Add(new VendorLocalButton(128, 544, 90, "Next", () => { _page++; Build(); }) { IsEnabled = start + 8 < rows.Length });
            Text(this, rows.Length + " sales | Page " + (_page + 1) + " / " + Math.Max(1, (rows.Length + 7) / 8)
                + " | Dates without a year sort by month/day.", 238, 548, 520);
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            if (_revision != VendorLedgerManager.Revision)
            {
                _revision = VendorLedgerManager.Revision;
                _group = VendorLedgerManager.GroupRestock(_vendors.Where(v => VendorLedgerManager.Records.Contains(v)), null, _name)
                    .FirstOrDefault(g => string.Equals(g.ItemName, _name, StringComparison.OrdinalIgnoreCase));
                Build();
            }
            base.Update();
        }
    }
}

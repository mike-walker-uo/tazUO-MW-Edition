using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorLedgerGump : Gump
    {
        private static readonly string[] Tabs = { "All vendors", "Sales history", "Sales summary", "Restock", "Stale stock" };
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private VendorLedgerRecord _vendor;
        private Gump _nativeHistory;
        private ScrollArea _body;
        private StbTextBox _search;
        private Label _status;
        private Label _updateStatus;
        private int _tab, _page, _staleDays = 7;
        private VendorLedgerColumn _historySort = VendorLedgerColumn.Received, _stockSort = VendorLedgerColumn.FirstSeen;
        private VendorLedgerColumn _restockSort = VendorLedgerColumn.Received;
        private bool _historyDescending, _stockDescending, _restockDescending;
        private long _refreshAt;
        private string _query = "";
        private int _revision;

        private VendorLedgerGump(VendorLedgerRecord vendor, int tab, Gump native) : base(0, 0)
        {
            _vendor = vendor; _tab = tab; _nativeHistory = native;
            Width = 920; Height = 710; WantUpdateSize = false; CanMove = true; AcceptMouseInput = true;
            CanCloseWithRightClick = true; CanCloseWithEsc = true;
            Build(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        internal static void Open(VendorLedgerRecord vendor = null, int tab = 0, Gump native = null)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            if (vendor != null && !VendorLedgerManager.Records.Contains(vendor)) vendor = null;
            var previous = UIManager.GetGump<VendorLedgerGump>();
            if (previous != null && previous._nativeHistory == native) previous._nativeHistory = null;
            previous?.Dispose();
            UIManager.Add(new VendorLedgerGump(vendor, tab, native));
        }

        private void Build()
        {
            Clear();
            Children.Clear();
            Add(new VendorGumpArtwork(Width, Height));
            Add(new Label("MERCHANT'S LEDGER", true, 0x0481, font: 1) { X = 32, Y = 22 });
            Add(new VendorLocalButton(550, 20, 116, "Export / fees", ExportMenu));
            Add(new VendorLocalButton(676, 20, 130, "Templates", () => VendorListingTemplatesGump.Open(null)));
            Add(new VendorLocalButton(816, 20, 70, "Close", Dispose));
            Add(new Label(_vendor?.ShopName ?? "All vendors", true, 0x0481, 620, font: 1) { X = 32, Y = 54 });
            for (int i = 0; i < Tabs.Length; i++)
            {
                int index = i;
                Add(new VendorLocalButton(32 + i * 172, 78, 164, (_tab == i ? "* " : "") + Tabs[i],
                    () => { _query = _search.Text; _tab = index; if (index == 0) _vendor = null; _page = 0; Build(); }));
            }
            Add(new Label("Find:", true, 0x0481, font: 1) { X = 32, Y = 120 });
            Add(new AlphaBlendControl(1) { X = 88, Y = 114, Width = 420, Height = 28, BaseColor = new Microsoft.Xna.Framework.Color(22, 21, 19) });
            _search = new StbTextBox(1, 120, 406, true, hue: 0x0481) { X = 95, Y = 118, Width = 406, Height = 22 };
            _search.SetText(_query);
            _search.TextChanged += (s, e) => { _query = _search.Text; _page = 0; _refreshAt = (long)Time.Ticks + 200; };
            Add(_search);
            if (_tab == 3)
            {
                var restore = new VendorLocalButton(520, 114, 140, "Restore removed", () =>
                {
                    if (VendorLedgerManager.RestoreRestock(VendorLedgerManager.Records, _vendor)) VendorLedgerManager.Changed();
                    _page = 0; RefreshRows();
                });
                restore.SetTooltip("Restore removed restock items for " + (_vendor == null ? "all vendors." : "this vendor."));
                Add(restore);
                Add(new VendorLocalButton(780, 114, 106, "Reset sort", () =>
                {
                    _restockSort = VendorLedgerColumn.Received; _restockDescending = false;
                    _page = 0; RefreshRows();
                }));
            }
            else
            {
                var resetSort = new VendorLocalButton(520, 114, 140, "Reset sort", () =>
                {
                    if (_tab == 1) { _historySort = VendorLedgerColumn.Received; _historyDescending = false; }
                    else { _stockSort = VendorLedgerColumn.FirstSeen; _stockDescending = false; }
                    _page = 0; RefreshRows();
                }) { IsEnabled = _tab == 1 || _tab == 4 };
                resetSort.SetTooltip("Click a column heading to sort; click again to reverse. Reset restores received sales / oldest stock first.");
                Add(resetSort);
            }
            Add(new VendorLocalButton(668, 114, 104, "Refresh", RefreshRows));
            if (_tab != 3)
                Add(new VendorLocalButton(780, 114, 106, "Age " + _staleDays + "d", () => { _staleDays = _staleDays == 7 ? 14 : _staleDays == 14 ? 30 : _staleDays == 30 ? 1 : 7; _page = 0; RefreshRows(); }));
            Add(new VendorLocalButton(32, 158, 124, "Add vendor", VendorLedgerManager.TargetVendor));
            Add(new VendorLocalButton(164, 158, 176, "Update vendors", () => VendorRefreshManager.Start()));
            Add(new VendorLocalButton(348, 158, 112, "Stop update", VendorRefreshManager.Stop));
            Add(new VendorLocalButton(468, 158, 150, "Market watch", VendorMarketWatchGump.Open));
            Add(new VendorLocalButton(626, 158, 150, "Vendor Search", () => VendorSearchWatchManager.OpenSearch()));
            Add(new VendorLocalButton(784, 158, 102, "Update one", () => { if (_vendor != null) VendorRefreshManager.Start(_vendor); }));
            _body = new ScrollArea(30, 202, 860, 382, true); Add(_body);
            _status = new Label("", true, 0x0386, 850, font: 1, style: FontStyle.Cropped) { X = 32, Y = 595 }; Add(_status);
            Add(new VendorLocalButton(32, 630, 90, "Previous", () => { _page = Math.Max(0, _page - 1); RefreshRows(); }));
            Add(new VendorLocalButton(132, 630, 90, "Next", () => { _page++; RefreshRows(); }));
            Add(new Label("Open nearby vendors to update. Fees and unopened bags remain unknown.", true, 0x0386, 650, font: 1) { X = 242, Y = 634 });
            _updateStatus = new Label("", true, 0x0481, 850, font: 1, style: FontStyle.Cropped) { X = 32, Y = 672 }; Add(_updateStatus);
            RefreshRows();
        }

        private void Line(string text, int x, int y, int width = 800, ushort hue = 0x0481)
        {
            var label = new Label(text, true, hue, width, font: 1, style: FontStyle.Cropped) { X = x, Y = y };
            label.SetTooltip(text); _body.Add(label);
        }
        private void SortHeader(string text, VendorLedgerColumn column, int x, int width)
        {
            bool history = _tab == 1, restock = _tab == 3;
            bool active = column == (history ? _historySort : restock ? _restockSort : _stockSort);
            bool descending = history ? _historyDescending : restock ? _restockDescending : _stockDescending;
            var label = new Label(text + (active ? descending ? " v" : " ^" : ""), true, 0x0481,
                width, font: 1, style: FontStyle.Cropped) { X = x, Y = 0, AcceptMouseInput = true };
            label.SetTooltip(text + (active ? descending ? ": descending." : ": ascending." : ".")
                + " Click to sort " + (active && !descending ? "descending." : "ascending.")
                + (column == VendorLedgerColumn.Listed || column == VendorLedgerColumn.Sold
                    ? " Server dates without a year sort by month/day; unrecognized dates stay last." : "")
                + (column == VendorLedgerColumn.Stock ? " Sorts captured listing counts; + means a lower bound. Unknown stays last." : "")
                + (restock && column == VendorLedgerColumn.Price ? " Median sold gold per listing/bundle. No sales stays last." : ""));
            label.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtonType.Left) return;
                if (history) { _historyDescending = active && !descending; _historySort = column; }
                else if (restock) { _restockDescending = active && !descending; _restockSort = column; }
                else { _stockDescending = active && !descending; _stockSort = column; }
                _page = 0; RefreshRows();
            };
            _body.Add(label);
        }
        private static string Gold(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private void SaleItem(VendorSaleRecord sale, int x, int y, int width)
        {
            var label = new Label(sale.ItemName, true, 0x0481, width, font: 1, style: FontStyle.Cropped)
                { X = x, Y = y, AcceptMouseInput = true };
            if (!string.IsNullOrEmpty(sale.PropertyText)) label.SetTooltip(VendorLedgerManager.SaleTooltip(sale));
            else if (sale.PropertySerial != 0) label.SetTooltip(sale.PropertySerial);
            else label.SetTooltip(sale.ItemName + "\nItem stats were not captured. Reopen the vendor's sales history to refresh.");
            _body.Add(label);
        }
        private static string Stamp(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("g") : "not observed";
        private bool Match(string text) => (text ?? "").IndexOf(_query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        private int PageStart(int count) { _page = Math.Min(_page, Math.Max(0, (count - 1) / 10)); return _page * 10; }

        private void RefreshRows()
        {
            _refreshAt = 0; _body.Clear(); _body.Children.RemoveAll(c => c.IsDisposed);
            _revision = VendorLedgerManager.Revision;
            if (_tab == 0) { Overview(); _status.SetTooltip(_status.Text); return; }
            if (_tab == 1) History();
            else if (_tab == 2) Summary();
            else if (_tab == 3) Restock();
            else Stock();
            _status.SetTooltip(_status.Text);
        }

        private void Overview()
        {
            var records = VendorLedgerManager.Records.Where(r => Match(r.ShopName)).OrderBy(r => r.ShopName).ToList();
            Line("SHOP", 4, 0, 270); Line("GOLD HELD", 280, 0, 145); Line("SALES / STOCK", 440, 0, 170); Line("LAST OWNER VIEW", 620, 0, 205);
            int start = PageStart(records.Count);
            for (int i = start; i < Math.Min(start + 10, records.Count); i++)
            {
                var record = records[i]; int y = 30 + (i - start) * 33;
                var name = new VendorLocalButton(4, y, 200, record.ShopName, () => { _vendor = record; _tab = 1; _page = 0; _query = ""; Build(); });
                name.SetTooltip(record.ShopName); _body.Add(name);
                _body.Add(new VendorLocalButton(212, y, 64, "Remove", () => RemoveVendor(record)));
                Line(record.GoldHeld.HasValue ? Gold(record.GoldHeld.Value) : "unknown", 280, y + 6, 145);
                Line(record.Sales.Count + " / " + (record.LastStockUtcTicks == 0 ? "unknown" : record.Stock.Count.ToString()), 440, y + 6, 170);
                Line(record.OwnerVerified ? Stamp(record.LastObservedUtcTicks) : "ownership unverified", 620, y + 6, 205);
            }
            _status.Text = records.Count + " vendors / page " + (_page + 1) + ". Open owner menus to add or update records.";
            if (records.Count == 0) Line("No vendors recorded yet. Open a vendor's owner menu or sales history.", 4, 36);
        }

        private void RemoveVendor(VendorLedgerRecord record)
        {
            UIManager.Add(new MessageBoxGump(460, 210,
                "Remove \"" + record.ShopName + "\" from your ledger?\n"
                + "Its saved sales history, stock and restock checklist will be deleted. Your in-game vendor is unaffected.",
                confirmed =>
                {
                    if (!confirmed || IsDisposed || _profile != ProfileManager.CurrentProfile) return;
                    if (!VendorLedgerManager.RemoveVendor(record)) return;
                    if (_vendor == record) _vendor = null;
                    _tab = _page = 0;
                    Build();
                }, menuType: MessageButtonType.OK_CANCEL));
        }

        private void History()
        {
            bool all = _vendor == null;
            var query = VendorLedgerManager.SalesRows(VendorLedgerManager.Records, _vendor, _query);
            var rows = VendorLedgerManager.SortSales(query, _historySort, _historyDescending).ToList();
            int start = PageStart(rows.Count);
            if (all)
            {
                SortHeader("SHOP", VendorLedgerColumn.Shop, 4, 155); SortHeader("ITEM", VendorLedgerColumn.Item, 169, 235);
                SortHeader("BUYER", VendorLedgerColumn.Buyer, 414, 125); SortHeader("PRICE", VendorLedgerColumn.Price, 549, 100);
                SortHeader("LISTED", VendorLedgerColumn.Listed, 659, 80); SortHeader("SOLD", VendorLedgerColumn.Sold, 749, 80);
            }
            else
            {
                SortHeader("ITEM", VendorLedgerColumn.Item, 4, 300); SortHeader("BUYER", VendorLedgerColumn.Buyer, 310, 140);
                SortHeader("PRICE", VendorLedgerColumn.Price, 460, 120); SortHeader("LISTED", VendorLedgerColumn.Listed, 590, 100);
                SortHeader("SOLD", VendorLedgerColumn.Sold, 705, 100);
            }
            for (int i = start; i < Math.Min(start + 10, rows.Count); i++)
            {
                var row = rows[i].Sale; int y = 34 + (i - start) * 30;
                if (all)
                {
                    Line(rows[i].Vendor.ShopName, 4, y, 155); SaleItem(row, 169, y, 235); Line(row.Buyer, 414, y, 125);
                    Line(Gold(row.Price), 549, y, 100); Line(row.ListDate, 659, y, 80); Line(row.SellDate, 749, y, 80);
                }
                else
                {
                    SaleItem(row, 4, y, 300); Line(row.Buyer, 310, y, 140); Line(Gold(row.Price), 460, y, 120);
                    Line(row.ListDate, 590, y, 100); Line(row.SellDate, 705, y, 100);
                }
            }
            _status.Text = rows.Count + " matching sale rows / gross " + Gold(VendorLedgerManager.Gross(rows.Select(r => r.Sale))) + " / " + HistoryCoverage();
            if (_vendor?.LastHistoryUtcTicks == 0) Line("Open View Sales History on this vendor to populate the ledger.", 4, 36);
            else if (rows.Count == 0) Line("No matching captured sales. Use Update vendors to refresh saved histories.", 4, 36);
        }

        private string HistoryCoverage() => _vendor != null ? "captured " + Stamp(_vendor.LastHistoryUtcTicks)
            : "saved histories: " + VendorLedgerManager.Records.Count(v => v.LastHistoryUtcTicks > 0) + " / " + VendorLedgerManager.Records.Count + " vendors";

        private void Summary()
        {
            var rows = VendorLedgerManager.SalesRows(VendorLedgerManager.Records, _vendor, _query).Select(r => r.Sale).ToList();
            decimal gross = VendorLedgerManager.Gross(rows);
            Line("GROSS GOLD: " + Gold(gross), 4, 8); Line("SALE ENTRIES: " + rows.Count, 4, 42);
            Line("AVERAGE LISTING SALE: " + (rows.Count == 0 ? "—" : Gold(gross / rows.Count)), 4, 76);
            Line("Fees / net profit: unknown. Up to 2000 received history rows per vendor.", 4, 110, 810, 0x0386);
            _body.Add(new VendorBarChart("BEST SELLERS — gross gold from received history", rows.GroupBy(s => s.ItemName)
                .OrderByDescending(VendorLedgerManager.Gross).Take(5).Select(g => new VendorBarChart.Bar {
                    Name = g.Key, Value = VendorLedgerManager.Gross(g), Detail = g.Key + " / " + g.Count() + " sales / " + Gold(VendorLedgerManager.Gross(g)) + " gold" }).ToList(), 810) { X = 4, Y = 156 });
            _status.Text = HistoryCoverage() + ". Identical sale rows count separately; dates retain the server's format.";
        }

        internal static string RestockCount((int Listings, long Units, bool Complete) stock, bool olderThanHistory)
            => !stock.Complete && stock.Listings == 0 ? "Unknown"
                : stock.Complete && !olderThanHistory ? stock.Listings + " left"
                : stock.Listings + (stock.Complete ? " seen" : "+ seen");

        internal static string RestockPrice(decimal? value)
            => value.HasValue ? value.Value.ToString("#,0.##", CultureInfo.InvariantCulture) : "No sales";

        private void RestockCell(string text, int x, int y, int width, string tooltip,
            VendorLedgerManager.RestockGroup group, ushort hue = 0x0481)
        {
            var label = new Label(text, true, hue, width, font: 1, style: FontStyle.Cropped)
                { X = x, Y = y, AcceptMouseInput = true };
            label.SetTooltip(tooltip, 320);
            label.MouseUp += (_, e) => { if (e.Button == MouseButtonType.Left) VendorRestockDetailsGump.Open(group); };
            _body.Add(label);
        }

        private void Restock()
        {
            bool all = _vendor == null;
            var query = VendorLedgerManager.GroupRestock(VendorLedgerManager.Records, _vendor, _query);
            var groups = VendorLedgerManager.SortRestock(query, _restockSort, _restockDescending).ToList();
            int start = PageStart(groups.Count);
            if (all) { SortHeader("VENDORS", VendorLedgerColumn.Shop, 4, 94); SortHeader("ITEM", VendorLedgerColumn.Item, 108, 200); }
            else SortHeader("ITEM", VendorLedgerColumn.Item, 4, 304);
            SortHeader("SALES", VendorLedgerColumn.Sales, 318, 52); SortHeader("STOCK", VendorLedgerColumn.Stock, 378, 80);
            SortHeader("MEDIAN", VendorLedgerColumn.Price, 468, 91);
            for (int i = start; i < Math.Min(start + 10, groups.Count); i++)
            {
                var group = groups[i];
                string name = group.ItemName; int y = 32 + (i - start) * 33;
                string count = RestockCount(group.Stock, group.OlderThanHistory);
                string heading = name.Length > 36 ? name.Substring(0, 33) + "..." : name;
                string detail = "/c[#E6C86E]" + heading + "/c[white]\n" + group.Members.Length + " vendors | " + group.Sales + " sales"
                    + "\nStock: " + count + "\nMedian sold: " + RestockPrice(group.Prices.Median) + (group.Prices.Median.HasValue ? " gold" : "")
                    + "\nClick for vendors & price history.";
                if (all) RestockCell(group.Members.Length + (group.Members.Length == 1 ? " vendor" : " vendors"), 4, y + 6, 94, detail, group);
                RestockCell(name, all ? 108 : 4, y + 6, all ? 200 : 304, detail, group);
                RestockCell(group.Sales.ToString(), 318, y + 6, 52, detail, group);
                RestockCell(count, 378, y + 6, 80, detail, group,
                    !group.OlderThanHistory && group.Stock.Complete && group.Stock.Listings == 0 ? (ushort)0x0021 : (ushort)0x0481);
                RestockCell(RestockPrice(group.Prices.Median), 468, y + 6, 91, detail, group, 0x0035);
                bool marked = group.Members.All(r => r.Vendor.RestockedNames.Contains(r.ItemName, StringComparer.OrdinalIgnoreCase));
                var done = new VendorLocalButton(569, y, 66, marked ? "Done" : "Mark", () =>
                {
                    foreach (var member in group.Members)
                    {
                        if (marked) member.Vendor.RestockedNames.RemoveAll(n => string.Equals(n, member.ItemName, StringComparison.OrdinalIgnoreCase));
                        else if (!member.Vendor.RestockedNames.Contains(member.ItemName, StringComparer.OrdinalIgnoreCase)) member.Vendor.RestockedNames.Add(member.ItemName);
                    }
                    VendorLedgerManager.Changed(); RefreshRows();
                });
                done.SetTooltip("Toggle the manual restocked checkmark for all " + group.Members.Length + " listed vendors. Does not change captured stock."); _body.Add(done);
                _body.Add(new VendorLocalButton(643, y, 52, "Find", () => Find(name)));
                _body.Add(new VendorLocalButton(703, y, 36, "VS", () => VendorSearchWatchManager.OpenSearch(SearchName(name))));
                _body.Add(new VendorLocalButton(747, y, 46, "Watch", () => VendorMarketWatchGump.Watch(SearchName(name))));
                var remove = new VendorLocalButton(801, y, 35, "X", () =>
                {
                    bool changed = false;
                    foreach (var member in group.Members) changed |= VendorLedgerManager.RemoveRestock(member.Vendor, member.ItemName);
                    if (changed) VendorLedgerManager.Changed();
                    RefreshRows();
                });
                remove.SetTooltip("Remove this item from all " + group.Members.Length + " listed vendors' restock lists. Sales stay saved. Restore removed undoes this.");
                _body.Add(remove);
            }
            _status.Text = groups.Count + " items. Median = sold gold per listing/bundle. Click a row for vendors & price history. X removes; Restore removed undoes.";
            if (groups.Count == 0) Line("No matching restock items. Restore removed or update vendors to refresh stock and sales.", 4, 36);
        }

        private void Stock()
        {
            long now = DateTime.UtcNow.Ticks;
            bool all = _vendor == null;
            var query = VendorLedgerManager.StaleStockRows(VendorLedgerManager.Records, _vendor, _query, _staleDays, now);
            var rows = VendorLedgerManager.SortStock(query, _stockSort, _stockDescending).ToList();
            int start = PageStart(rows.Count);
            if (all)
            {
                SortHeader("SHOP", VendorLedgerColumn.Shop, 4, 155); SortHeader("ITEM", VendorLedgerColumn.Item, 169, 245);
                SortHeader("AMOUNT", VendorLedgerColumn.Amount, 424, 76); Line("/", 502, 0, 12);
                SortHeader("PRICE", VendorLedgerColumn.Price, 516, 78);
                SortHeader("FIRST SEEN", VendorLedgerColumn.FirstSeen, 604, 108); SortHeader("LAST SEEN", VendorLedgerColumn.LastSeen, 722, 108);
            }
            else
            {
                SortHeader("ITEM", VendorLedgerColumn.Item, 4, 320);
                SortHeader("AMOUNT", VendorLedgerColumn.Amount, 330, 88); Line("/", 420, 0, 12);
                SortHeader("PRICE", VendorLedgerColumn.Price, 434, 86);
                SortHeader("FIRST OBSERVED", VendorLedgerColumn.FirstSeen, 530, 150);
                SortHeader("LAST OBSERVED", VendorLedgerColumn.LastSeen, 690, 140);
            }
            for (int i = start; i < Math.Min(start + 10, rows.Count); i++)
            {
                var row = rows[i].Stock; int y = 34 + (i - start) * 30;
                string amountPrice = row.Amount + " / " + (row.Price.HasValue ? Gold(row.Price.Value) : "unknown");
                if (all)
                {
                    Line(rows[i].Vendor.ShopName, 4, y, 155); Line(row.ItemName, 169, y, 245); Line(amountPrice, 424, y, 170);
                    Line(Stamp(row.FirstSeenUtcTicks), 604, y, 108); Line(Stamp(row.LastSeenUtcTicks), 722, y, 108);
                }
                else
                {
                    Line(row.ItemName, 4, y, 320); Line(amountPrice, 330, y, 190);
                    Line(Stamp(row.FirstSeenUtcTicks), 530, y, 150); Line(Stamp(row.LastSeenUtcTicks), 690, y, 140);
                }
            }
            _status.Text = rows.Count + " records aged " + _staleDays + "+ days. " + (all
                ? "Stock captured: " + VendorLedgerManager.Records.Count(v => v.LastStockUtcTicks > 0) + " / " + VendorLedgerManager.Records.Count + " vendors."
                : "Last stock capture: " + Stamp(_vendor.LastStockUtcTicks) + ".") + " Reopen See goods to verify availability.";
            if (_vendor?.LastStockUtcTicks == 0) Line("Open See goods after the owner menu. Only loaded vendor bags are recorded.", 4, 36);
            else if (rows.Count == 0) Line("No captured stock matches the age and Find filters.", 4, 36);
        }

        private static void Find(string name)
        {
            name = System.Text.RegularExpressions.Regex.Replace(name, @"^\d+\s+", "");
            var gump = UIManager.GetGump<ItemFinderGump>();
            if (gump != null) { gump.SetQuery(name); gump.BringOnTop(); }
            else UIManager.Add(new ItemFinderGump(name));
        }

        private static string SearchName(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"^\d+\s+", "");

        private void ExportMenu()
        {
            var menu = new ContextMenuControl();
            if (_vendor != null) menu.Add(new ContextMenuItemEntry("Export selected vendor's captured sales", () => VendorCsvExport.Sales(_vendor)));
            menu.Add(new ContextMenuItemEntry("Export all captured vendor sales", () => VendorCsvExport.Sales()));
            menu.Add(new ContextMenuItemEntry("Fee calculator", () => VendorPricingGump.Open()));
            menu.Add(new ContextMenuItemEntry("Open export folder", VendorCsvExport.OpenFolder));
            menu.Show();
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { _nativeHistory = null; Dispose(); return; }
            if ((_refreshAt != 0 && Time.Ticks >= _refreshAt) || _revision != VendorLedgerManager.Revision) RefreshRows();
            if (_updateStatus.Text != VendorRefreshManager.Status) { _updateStatus.Text = VendorRefreshManager.Status; _updateStatus.SetTooltip(_updateStatus.Text); }
            base.Update();
        }
        public override void Dispose()
        {
            if (IsDisposed) return;
            if (_nativeHistory != null && !_nativeHistory.IsDisposed)
            {
                if (World.InGame && _profile == ProfileManager.CurrentProfile
                    && _nativeHistory.CanCloseWithRightClick && _nativeHistory.ServerSerial != 0)
                    _nativeHistory.OnButtonClick(0);
                _nativeHistory.Dispose();
            }
            _nativeHistory = null; base.Dispose();
        }
    }
}

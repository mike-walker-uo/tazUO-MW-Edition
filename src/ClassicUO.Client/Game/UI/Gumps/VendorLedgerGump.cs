using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

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
        private int _tab, _page, _sort, _staleDays = 7;
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
            Add(new VendorLocalButton(520, 114, 140, "Sort: " + (_sort == 0 ? "received" : _sort == 1 ? "name" : "price"),
                () => { _sort = (_sort + 1) % 3; _page = 0; RefreshRows(); }));
            Add(new VendorLocalButton(668, 114, 104, "Refresh", RefreshRows));
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
        private static string Gold(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
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
            if (_sort == 1) query = query.OrderBy(r => r.Sale.ItemName).ThenBy(r => r.Sale.Buyer);
            else if (_sort == 2) query = query.OrderByDescending(r => r.Sale.Price);
            var rows = query.ToList(); int start = PageStart(rows.Count);
            if (all)
            {
                Line("SHOP", 4, 0, 155); Line("ITEM", 169, 0, 235); Line("BUYER", 414, 0, 125);
                Line("PRICE", 549, 0, 100); Line("LISTED", 659, 0, 80); Line("SOLD", 749, 0, 80);
            }
            else { Line("ITEM", 4, 0, 300); Line("BUYER", 310, 0, 140); Line("PRICE", 460, 0, 120); Line("LISTED", 590, 0, 100); Line("SOLD", 705, 0, 100); }
            for (int i = start; i < Math.Min(start + 10, rows.Count); i++)
            {
                var row = rows[i].Sale; int y = 34 + (i - start) * 30;
                if (all)
                {
                    Line(rows[i].Vendor.ShopName, 4, y, 155); Line(row.ItemName, 169, y, 235); Line(row.Buyer, 414, y, 125);
                    Line(Gold(row.Price), 549, y, 100); Line(row.ListDate, 659, y, 80); Line(row.SellDate, 749, y, 80);
                }
                else
                {
                    Line(row.ItemName, 4, y, 300); Line(row.Buyer, 310, y, 140); Line(Gold(row.Price), 460, y, 120);
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

        private void Restock()
        {
            bool all = _vendor == null;
            var groups = VendorLedgerManager.RestockRows(VendorLedgerManager.Records, _vendor, _query).ToList();
            int start = PageStart(groups.Count);
            if (all) { Line("SHOP", 4, 0, 155); Line("SOLD ITEM", 169, 0, 235); Line("SALES", 420, 0, 60); }
            else Line("SOLD ITEM / sale entries, not inferred units", 4, 0, 520);
            for (int i = start; i < Math.Min(start + 10, groups.Count); i++)
            {
                var vendor = groups[i].Vendor;
                string name = groups[i].ItemName; int y = 32 + (i - start) * 33;
                if (all) { Line(vendor.ShopName, 4, y + 6, 155); Line(name, 169, y + 6, 235); }
                else Line(name, 4, y + 6, 405);
                Line(groups[i].Sales.ToString(), 420, y + 6, 60);
                _body.Add(new VendorLocalButton(490, y, 100, vendor.RestockedNames.Contains(name) ? "Done" : "Restocked", () =>
                {
                    if (!vendor.RestockedNames.Remove(name)) vendor.RestockedNames.Add(name);
                    VendorLedgerManager.Changed(); RefreshRows();
                }));
                _body.Add(new VendorLocalButton(598, y, 96, "Find item", () => Find(name)));
                _body.Add(new VendorLocalButton(702, y, 60, "VS", () => VendorSearchWatchManager.OpenSearch(SearchName(name))));
                _body.Add(new VendorLocalButton(770, y, 60, "Watch", () => VendorMarketWatchGump.Watch(SearchName(name))));
            }
            _status.Text = groups.Count + " matching checklist entries. Sale counts are entries, not units. Checkmarks apply to each vendor separately.";
            if (groups.Count == 0) Line("No matching captured sales for this checklist. Use Update vendors to refresh histories.", 4, 36);
        }

        private void Stock()
        {
            long now = DateTime.UtcNow.Ticks;
            bool all = _vendor == null;
            var rows = VendorLedgerManager.StaleStockRows(VendorLedgerManager.Records, _vendor, _query, _staleDays, now).ToList();
            int start = PageStart(rows.Count);
            if (all)
            {
                Line("SHOP", 4, 0, 155); Line("ITEM", 169, 0, 245); Line("AMOUNT / PRICE", 424, 0, 170);
                Line("FIRST SEEN", 604, 0, 108); Line("LAST SEEN", 722, 0, 108);
            }
            else { Line("ITEM", 4, 0, 320); Line("AMOUNT / PRICE", 330, 0, 190); Line("FIRST OBSERVED", 530, 0, 150); Line("LAST OBSERVED", 690, 0, 140); }
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

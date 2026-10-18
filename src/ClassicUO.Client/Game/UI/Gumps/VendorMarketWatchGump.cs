using System;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorMarketWatchGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly DataBox _watches, _results;
        private readonly Label _status;
        private readonly StbTextBox _name;
        private VendorWatchItem _selected;
        private int _watchPage, _resultPage, _scanIndex = -1, _revision;

        private VendorMarketWatchGump() : base(0, 0)
        {
            Width = 1040; Height = 710; WantUpdateSize = false; CanMove = true; AcceptMouseInput = true;
            CanCloseWithRightClick = CanCloseWithEsc = true;
            Add(new VendorGumpArtwork(Width, Height));
            Text(this, "ROYAL MARKET WATCH", 32, 22, 780);
            Add(new VendorLocalButton(930, 20, 76, "Close", Dispose));
            Text(this, "Watch an item name:", 32, 53, 310);
            Add(new AlphaBlendControl(1) { X = 32, Y = 78, Width = 300, Height = 28, BaseColor = new Microsoft.Xna.Framework.Color(10, 27, 20) });
            _name = new StbTextBox(1, 100, 286, true, hue: 0x0481) { X = 39, Y = 82, Width = 286, Height = 22 }; Add(_name);
            Add(new VendorLocalButton(340, 78, 100, "Add watch", () => AddWatch(_name.Text)));
            Add(new VendorLocalButton(448, 78, 140, "Vendor Search", () => VendorSearchWatchManager.OpenSearch(
                string.IsNullOrWhiteSpace(_name.Text) ? _selected?.Query : _name.Text.Trim())));
            Add(new VendorLocalButton(596, 78, 132, "VS Scan all", VendorSearchWatchManager.StartScan));
            Add(new VendorLocalButton(736, 78, 90, "Stop scan", () => VendorSearchWatchManager.Stop()));
            Add(new VendorLocalButton(834, 78, 80, "Tools", Tools));
            Add(new VendorLocalButton(922, 78, 84, "Ledger", () => VendorLedgerGump.Open()));
            var enabled = new Checkbox(0x00D2, 0x00D3, "Enable VS scans", 1, 0x0481) { X = 32, Y = 115, IsChecked = _profile.VendorSearchScansEnabled };
            enabled.ValueChanged += (s, e) =>
            {
                _profile.VendorSearchScansEnabled = enabled.IsChecked;
                if (!enabled.IsChecked) VendorSearchWatchManager.Stop("VS scans disabled.");
                Save();
            };
            Add(enabled);
            Text(this, "Read-only scans. Original server restrictions and cooldowns apply.", 285, 117, 720, 0x0386);
            Add(new AlphaBlendControl(1) { X = 30, Y = 153, Width = 245, Height = 482, BaseColor = new Microsoft.Xna.Framework.Color(10, 27, 20) });
            _watches = new DataBox(30, 153, 245, 482); Add(_watches);
            Add(new AlphaBlendControl(1) { X = 285, Y = 153, Width = 722, Height = 492, BaseColor = new Microsoft.Xna.Framework.Color(9, 24, 18) });
            _results = new DataBox(285, 153, 722, 492); Add(_results);
            _status = new Label("", true, 0x0481, 974, font: 1, style: FontStyle.Cropped) { X = 32, Y = 659 }; Add(_status);
            Text(this, "* = partial scan. Unknown amounts stay unknown. Missing listings do not prove a sale.", 32, 684, 974, 0x0386);
            Refresh(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        internal static void Open()
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            var existing = UIManager.GetGump<VendorMarketWatchGump>();
            if (existing != null) { existing.Refresh(); existing.BringOnTop(); return; }
            UIManager.Add(new VendorMarketWatchGump());
        }

        internal static void Watch(string query)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            Open(); UIManager.GetGump<VendorMarketWatchGump>()?.AddWatch(query);
        }

        private void Save() => _profile.Save(ProfileManager.ProfilePath, false);
        private VendorMarketScan SelectedScan() => _selected == null || _selected.Scans.Count == 0 ? null
            : _selected.Scans[_scanIndex < 0 ? _selected.Scans.Count - 1 : Math.Min(_scanIndex, _selected.Scans.Count - 1)];
        private void Tools()
        {
            var menu = new ContextMenuControl();
            menu.Add(new ContextMenuItemEntry("Pricing, fees & price alerts", () => VendorPricingGump.Open(_selected, SelectedScan())));
            if (_selected != null)
            {
                menu.Add(new ContextMenuItemEntry("Compare selected scan with previous", () => VendorScanChangesGump.Open(_selected, SelectedScan())));
                menu.Add(new ContextMenuItemEntry("Export this watch's scans as CSV", () => VendorCsvExport.Market(_selected)));
            }
            menu.Add(new ContextMenuItemEntry("Export all market scans as CSV", () => VendorCsvExport.Market()));
            menu.Add(new ContextMenuItemEntry("Open export folder", VendorCsvExport.OpenFolder));
            menu.Show();
        }
        private void AddWatch(string query)
        {
            var item = VendorSearchWatchManager.AddWatch(_profile, query);
            if (item == null) { GameActions.Print("Enter an item name (1–100 characters). Up to 32 watches can be saved.", 0x35); return; }
            _selected = item; _watchPage = _profile.VendorWatchItems.IndexOf(item) / 10; _scanIndex = -1; _resultPage = 0; Save(); Refresh();
        }

        private static void Text(Control parent, string text, int x, int y, int width, ushort hue = 0x0481, string tip = null)
        {
            var label = new Label(text, true, hue, width, font: 1, style: FontStyle.Cropped) { X = x, Y = y };
            label.SetTooltip(tip ?? text); parent.Add(label);
        }
        private static string Gold(decimal? value) => value?.ToString("N2", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.') ?? "unknown";
        private static string Stamp(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("MM/dd HH:mm") : "unknown";

        private void Refresh()
        {
            _revision = VendorSearchWatchManager.Revision; _watches.Clear(); _results.Clear();
            _watches.Children.RemoveAll(c => c.IsDisposed); _results.Children.RemoveAll(c => c.IsDisposed);
            if (_selected == null || !_profile.VendorWatchItems.Contains(_selected)) _selected = _profile.VendorWatchItems.FirstOrDefault();
            Text(_watches, "WATCHED ITEMS", 8, 8, 229);
            _watchPage = Math.Min(_watchPage, Math.Max(0, (_profile.VendorWatchItems.Count - 1) / 10));
            for (int i = _watchPage * 10; i < Math.Min(_watchPage * 10 + 10, _profile.VendorWatchItems.Count); i++)
            {
                var watch = _profile.VendorWatchItems[i]; int y = 40 + (i % 10) * 34;
                var select = new VendorLocalButton(8, y, 229, (_selected == watch ? "* " : "") + watch.Query,
                    () => { _selected = watch; _scanIndex = -1; _resultPage = 0; Refresh(); });
                select.SetTooltip(watch.Query + " / " + watch.Scans.Count + " saved scans"); _watches.Add(select);
            }
            _watches.Add(new VendorLocalButton(8, 398, 110, "Previous", () => { _watchPage = Math.Max(0, _watchPage - 1); Refresh(); }));
            _watches.Add(new VendorLocalButton(126, 398, 110, "Next", () => { _watchPage++; Refresh(); }));
            Text(_watches, _profile.VendorWatchItems.Count + " / 32 watches", 8, 439, 229, 0x0386);
            if (_selected == null) { Text(_results, "Add watched item names, then click VS Scan all.", 12, 12, 696); return; }
            Text(_results, _selected.Query, 12, 10, 440);
            _results.Add(new VendorLocalButton(458, 6, 104, "Search VS", () => VendorSearchWatchManager.OpenSearch(_selected.Query)));
            _results.Add(new VendorLocalButton(570, 6, 136, "Remove watch", () =>
            {
                if (VendorSearchWatchManager.Active) { GameActions.Print("Stop the scan before removing a watched item.", 0x35); return; }
                _profile.VendorWatchItems.Remove(_selected); _selected = null; _scanIndex = -1; Save(); Refresh();
            }));
            if (_selected.Scans.Count == 0) { Text(_results, "No scans yet. VS Scan all searches every watched item.", 12, 56, 696); return; }
            int index = _scanIndex < 0 ? _selected.Scans.Count - 1 : Math.Min(_scanIndex, _selected.Scans.Count - 1);
            var scan = _selected.Scans[index];
            _results.Add(new VendorLocalButton(12, 42, 120, "Older scan", () => { _scanIndex = Math.Max(0, index - 1); _resultPage = 0; Refresh(); }));
            _results.Add(new VendorLocalButton(140, 42, 120, "Newer scan", () => { _scanIndex = index + 1 >= _selected.Scans.Count - 1 ? -1 : index + 1; _resultPage = 0; Refresh(); }));
            Text(_results, Stamp(scan.ScanUtcTicks) + " / " + (scan.Complete ? "complete" : "PARTIAL") + " / " + scan.Pages + " pages", 275, 48, 420,
                scan.Complete ? (ushort)0x0481 : (ushort)0x0035, scan.Note);
            long units = scan.Listings.Where(l => l.Amount.HasValue).Sum(l => (long)l.Amount.Value);
            int unknown = scan.Listings.Count(l => !l.Amount.HasValue);
            var prices = scan.Listings.Where(l => l.UnitPrice.HasValue).Select(l => l.UnitPrice.Value).ToList();
            Text(_results, scan.Listings.Count + " listings / " + units + " known units" + (unknown > 0 ? " + " + unknown + " unknown amounts" : "")
                + " / lowest unit price " + Gold(prices.Count == 0 ? (decimal?)null : prices.Min()), 12, 86, 696);
            Text(_results, "ITEM", 12, 120, 225); Text(_results, "AMOUNT", 242, 120, 76); Text(_results, "PRICE", 326, 120, 116);
            Text(_results, "UNIT PRICE", 450, 120, 110); Text(_results, "LISTED", 565, 120, 139);
            _resultPage = Math.Min(_resultPage, Math.Max(0, (scan.Listings.Count - 1) / 5));
            for (int i = _resultPage * 5; i < Math.Min(_resultPage * 5 + 5, scan.Listings.Count); i++)
            {
                var listing = scan.Listings[i]; int y = 152 + i % 5 * 29;
                string detail = listing.ItemName + "\n" + listing.Data + "\nFacet: " + listing.Facet + "\nListing date: "
                    + (listing.ListDate.Length == 0 ? "unknown" : listing.ListDate) + "\nScan: " + Stamp(scan.ScanUtcTicks);
                Text(_results, listing.ItemName, 12, y, 225, tip: detail);
                Text(_results, listing.Amount?.ToString() ?? "?", 242, y, 76);
                Text(_results, Gold(listing.Price), 326, y, 116); Text(_results, Gold(listing.UnitPrice), 450, y, 110);
                Text(_results, listing.ListDate.Length == 0 ? "unknown" : listing.ListDate, 565, y, 139);
            }
            _results.Add(new VendorLocalButton(12, 306, 100, "Previous", () => { _resultPage = Math.Max(0, _resultPage - 1); Refresh(); }));
            _results.Add(new VendorLocalButton(120, 306, 100, "Next", () => { _resultPage++; Refresh(); }));
            Text(_results, "Page " + (_resultPage + 1) + " / hover names for listing details", 234, 312, 470, 0x0386);
            var history = _selected.Scans.Skip(Math.Max(0, _selected.Scans.Count - 5)).ToList();
            _results.Add(new VendorBarChart("LOWEST UNIT PRICE / scan", history.Select(s => new VendorBarChart.Bar {
                Name = (s.Complete ? "" : "* ") + Stamp(s.ScanUtcTicks), Value = s.Listings.Where(l => l.UnitPrice.HasValue).Select(l => l.UnitPrice).DefaultIfEmpty(null).Min(),
                Detail = Stamp(s.ScanUtcTicks) + " / " + (s.Complete ? "complete" : "PARTIAL") + " / " + s.Note }).ToList(), 344, 21) { X = 12, Y = 355 });
            _results.Add(new VendorBarChart("KNOWN SUPPLY / scan", history.Select(s => new VendorBarChart.Bar {
                Name = (s.Complete ? "" : "* ") + Stamp(s.ScanUtcTicks), Value = !s.Complete || s.Listings.Any(l => !l.Amount.HasValue) ? (decimal?)null : s.Listings.Sum(l => (decimal)(l.Amount ?? 0)),
                Detail = Stamp(s.ScanUtcTicks) + " / " + (s.Complete ? "complete" : "PARTIAL") + " / unknown amounts excluded from totals" }).ToList(), 344, 21) { X = 367, Y = 355 });
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            if (_revision != VendorSearchWatchManager.Revision) Refresh();
            if (_status.Text != VendorSearchWatchManager.Status) { _status.Text = VendorSearchWatchManager.Status; _status.SetTooltip(_status.Text); }
            base.Update();
        }
    }
}

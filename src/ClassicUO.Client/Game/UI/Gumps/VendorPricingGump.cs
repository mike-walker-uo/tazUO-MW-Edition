using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorPricingGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly Gump _listing;
        private readonly StbTextBox[] _linkedFields;
        private readonly DataBox _summary;
        private readonly Label _watchName, _groupName, _feeResult, _status;
        private readonly StbTextBox _quantity, _price, _oldPrice, _rate, _repriceRate, _threshold;
        private readonly Checkbox _alertEnabled, _notForSale;
        private readonly VendorLocalButton _modeButton;
        private VendorWatchItem _watch;
        private VendorMarketScan _scan;
        private List<VendorMarketAnalysis.PriceGroup> _groups;
        private int _group;
        private VendorFeeMode _mode;

        private VendorPricingGump(VendorWatchItem watch, VendorMarketScan scan, Gump listing) : base(0, 0)
        {
            _watch = watch; _scan = scan; _listing = listing;
            _linkedFields = listing?.Children.OfType<StbTextBox>().Where(t => t.IsFromServer).OrderBy(t => t.Y).ThenBy(t => t.X).ToArray() ?? Array.Empty<StbTextBox>();
            Width = 860; Height = 650; WantUpdateSize = false; CanMove = true; AcceptMouseInput = true; CanCloseWithRightClick = CanCloseWithEsc = true;
            Add(new VendorGumpArtwork(Width, Height));
            Text("PRICING & FEE PREVIEW", 28, 22, 690);
            Add(new VendorLocalButton(750, 20, 80, "Close", Dispose));
            _watchName = Text("", 28, 58, 520);
            Add(new VendorLocalButton(558, 52, 140, "Next watch", NextWatch));
            Add(new VendorLocalButton(706, 52, 124, "Market watch", VendorMarketWatchGump.Open));
            _groupName = Text("", 28, 94, 540);
            Add(new VendorLocalButton(590, 88, 110, "Variant <", () => { _group = Math.Max(0, _group - 1); Summary(); }));
            Add(new VendorLocalButton(708, 88, 122, "Variant >", () => { if (_groups.Count > 0) _group = Math.Min(_groups.Count - 1, _group + 1); Summary(); }));
            Add(new AlphaBlendControl(1) { X = 28, Y = 126, Width = 804, Height = 112, BaseColor = new Color(9, 27, 19) });
            _summary = new DataBox(28, 126, 804, 112); Add(_summary);
            Text("Units", 28, 259, 70); _quantity = Input(100, 250, 100, "1");
            Text("Total price", 216, 259, 108); _price = Input(330, 250, 178, "");
            Add(new VendorLocalButton(520, 250, 148, "Use median", UseMedian));
            _modeButton = new VendorLocalButton(28, 294, 248, "New listing", () => { _mode = (VendorFeeMode)(((int)_mode + 1) % 3); Fees(); }); Add(_modeButton);
            Text("Old price", 292, 302, 110); _oldPrice = Input(402, 294, 158, "");
            Text("Rate %", 28, 342, 72); _rate = Input(100, 334, 100, _profile.VendorListingFeePercent.ToString(CultureInfo.InvariantCulture));
            Text("After 24h: % of listing fee", 216, 342, 260); _repriceRate = Input(484, 334, 100, _profile.VendorRepriceFeePercent.ToString(CultureInfo.InvariantCulture));
            _notForSale = new Checkbox(0x00D2, 0x00D3, "Not for sale", 1, 0x0481) { X = 612, Y = 338 }; Add(_notForSale);
            _feeResult = Text("", 28, 380, 804);
            Text("Estimates round up to whole gold. Prior fees / other costs excluded.", 28, 406, 804, 0x0386);
            _alertEnabled = new Checkbox(0x00D2, 0x00D3, "Price alert for selected variant", 1, 0x0481) { X = 28, Y = 448 }; Add(_alertEnabled);
            Text("Notify below gold / unit", 28, 490, 248); _threshold = Input(286, 482, 180, "");
            Add(new VendorLocalButton(482, 482, 200, "Save alert", SaveAlert));
            Text("One rule per watch. Checks complete new scans; re-arms at/above the threshold.", 28, 526, 804, 0x0386);
            Add(new VendorLocalButton(28, 562, 180, "Save fee rates", SaveRates));
            Add(new VendorLocalButton(218, 562, 180, "Copy listing price", CopyListing));
            Add(new VendorLocalButton(408, 562, 180, "Fill listing price", FillListing));
            _status = Text("Fee defaults: UOAlive wiki. Use the server's Calculate Fees to confirm the charge.", 28, 608, 804, 0x0386);
            _status.SetTooltip("https://uoalive.com/wiki/UOAlive_Vendors\nNew: full listing fee. Within 24h: positive fee difference. After 24h: configured fraction of listing fee. Description-only edits: zero.");
            foreach (var input in new[] { _price, _oldPrice, _rate, _repriceRate }) input.TextChanged += (s, e) => Fees();
            _notForSale.ValueChanged += (s, e) => Fees();
            SelectWatch();
            if (_listing != null) CopyListing(); else Fees();
            CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        internal static void Open(VendorWatchItem watch = null, VendorMarketScan scan = null, Gump listing = null)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            UIManager.GetGump<VendorPricingGump>()?.Dispose();
            UIManager.Add(new VendorPricingGump(watch, scan, listing));
        }
        private Label Text(string text, int x, int y, int width, ushort hue = 0x0481)
        {
            var label = new Label(text, true, hue, width, font: 1, style: FontStyle.Cropped) { X = x, Y = y }; label.SetTooltip(text); Add(label); return label;
        }
        private StbTextBox Input(int x, int y, int width, string value)
        {
            Add(new AlphaBlendControl(1) { X = x, Y = y, Width = width, Height = 30, BaseColor = new Color(9, 27, 19) });
            var input = new StbTextBox(1, 30, width - 12, true, hue: 0x0481) { X = x + 6, Y = y + 5, Width = width - 12, Height = 22 };
            input.SetText(value); Add(input); return input;
        }
        private static string Gold(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
        private void NextWatch()
        {
            if (_profile.VendorWatchItems.Count == 0) { _status.Text = "Add watched items and run VS Scan all first."; return; }
            int next = _watch == null ? 0 : (_profile.VendorWatchItems.IndexOf(_watch) + 1) % _profile.VendorWatchItems.Count;
            _watch = _profile.VendorWatchItems[next]; _scan = _watch.Scans.LastOrDefault(); SelectWatch();
        }
        private void SelectWatch()
        {
            if (_scan == null && _watch != null) _scan = _watch.Scans.LastOrDefault();
            _groups = VendorMarketAnalysis.Groups(_scan); _group = 0;
            if (_watch != null)
            {
                int selected = _groups.FindIndex(g => g.Key == _watch.AlertVariantKey); if (selected >= 0) _group = selected;
                _alertEnabled.IsChecked = _watch.PriceAlertEnabled;
                _threshold.SetText(_watch.AlertBelowUnitPrice?.ToString(CultureInfo.InvariantCulture) ?? "");
            }
            else { _alertEnabled.IsChecked = false; _threshold.SetText(""); }
            Summary();
        }
        private void Summary()
        {
            _summary.Clear(); _summary.Children.RemoveAll(c => c.IsDisposed);
            _watchName.Text = _watch?.Query ?? "Fee calculator — select a watch for market comparisons";
            _groupName.Text = "Variant " + (_groups.Count == 0 ? "0 / 0" : (_group + 1) + " / " + _groups.Count);
            if (_groups.Count == 0) { _summary.Add(new Label("No unit-price samples. Scan a watched item first.", true, 0x0481, 780, font: 1) { X = 10, Y = 12 }); return; }
            var group = _groups[_group];
            _quantity.SetText((group.Amount ?? 1).ToString(CultureInfo.InvariantCulture));
            _groupName.Text += " / pack " + (group.Amount?.ToString() ?? "?") + " / graphic " + (group.Graphic?.ToString("X4") ?? "?") + " / hue " + (group.Hue?.ToString("X4") ?? "?");
            _groupName.SetTooltip(_groupName.Text);
            _alertEnabled.IsChecked = _watch != null && _watch.PriceAlertEnabled && _watch.AlertVariantKey == group.Key;
            _threshold.SetText(_watch != null && _watch.AlertVariantKey == group.Key ? _watch.AlertBelowUnitPrice?.ToString(CultureInfo.InvariantCulture) ?? "" : "");
            var title = new Label(group.Name, true, 0x0481, 780, font: 1, style: FontStyle.Cropped) { X = 10, Y = 8 };
            title.SetTooltip(group.Properties.Length == 0 ? "Properties unavailable: name-only comparison." : group.Properties); _summary.Add(title);
            _summary.Add(new Label(group.Prices.Count + " comparable quotes / " + (group.PropertiesKnown ? "captured properties matched" : "NAME ONLY — inspect item properties")
                + " / " + (_scan.Complete ? "complete scan" : "PARTIAL scan"), true, 0x0386, 780, font: 1, style: FontStyle.Cropped) { X = 10, Y = 34 });
            _summary.Add(new Label("Low " + Gold(group.Low) + " / median " + Gold(group.Median) + " gold per unit", true, 0x0481, 780, font: 1) { X = 10, Y = 58 });
            _summary.Add(new Label("Typical range (25–75%): " + Gold(group.LowerQuartile) + " – " + Gold(group.UpperQuartile)
                + " / " + VendorCsvExport.TimeStamp(_scan.ScanUtcTicks), true, 0x0481, 780, font: 1, style: FontStyle.Cropped) { X = 10, Y = 82 });
        }
        private void UseMedian()
        {
            if (_groups.Count == 0 || !_scan.Complete || _groups[_group].Prices.Count < 3)
            { _status.Text = "A suggestion requires a complete scan and at least three comparable unit-price quotes."; return; }
            if (!_groups[_group].PropertiesKnown) { _status.Text = "Name-only quotes cannot verify item quality. Capture fresh properties before using a suggestion."; return; }
            if (!VendorLedgerManager.Money(_quantity.Text, out long quantity) || quantity < 1 || quantity > int.MaxValue)
            { _status.Text = "Enter a positive whole unit count."; return; }
            if (_groups[_group].Median > long.MaxValue / (decimal)quantity) { _status.Text = "Suggested total exceeds the supported price range."; return; }
            decimal total = decimal.Ceiling(_groups[_group].Median * quantity);
            _price.SetText(total.ToString("0", CultureInfo.InvariantCulture)); _status.Text = "Median-based suggestion filled. Review its captured properties and scan date.";
        }
        private static decimal? Percent(string value) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal n) && n >= 0 && n <= 100 ? (decimal?)n : null;
        private void Fees()
        {
            _modeButton.SetText(_mode == VendorFeeMode.NewListing ? "New listing" : _mode == VendorFeeMode.Within24Hours ? "Reprice: within 24h" : "Reprice: after 24h");
            if (_notForSale.IsChecked) { _feeResult.Text = "Not for sale: estimated listing fee 0; no sale proceeds."; return; }
            decimal? rate = Percent(_rate.Text), share = Percent(_repriceRate.Text);
            if (!VendorLedgerManager.Money(_price.Text, out long price) || !rate.HasValue || !share.HasValue)
            { _feeResult.Text = "Enter a whole gold price and percentages from 0 to 100 (decimal point: .)."; return; }
            long? old = VendorLedgerManager.Money(_oldPrice.Text, out long previous) ? (long?)previous : null;
            var fee = VendorMarketAnalysis.Fee(price, rate.Value, _mode, old, share.Value);
            _feeResult.Text = fee.HasValue ? "Estimated fee now: " + Gold(fee.Value) + " / sale price less this fee: " + Gold(price - fee.Value)
                : "Enter the original listing price for a repricing estimate.";
        }
        private void SaveRates()
        {
            var rate = Percent(_rate.Text); var share = Percent(_repriceRate.Text);
            if (!rate.HasValue || !share.HasValue) { _status.Text = "Use percentages between 0 and 100."; return; }
            _profile.VendorListingFeePercent = rate.Value; _profile.VendorRepriceFeePercent = share.Value;
            _profile.Save(ProfileManager.ProfilePath, false); _status.Text = "Fee rates saved for this character.";
        }
        private void SaveAlert()
        {
            if (_watch == null) { _status.Text = "Select a watched item first."; return; }
            if (!_alertEnabled.IsChecked)
            {
                _watch.PriceAlertEnabled = false; _watch.PriceAlertTriggered = false;
                _profile.Save(ProfileManager.ProfilePath, false); _status.Text = "Price alert disabled."; return;
            }
            var threshold = VendorSearchWatchManager.UnitQuote(_threshold.Text);
            if (_groups.Count == 0 || !threshold.HasValue) { _status.Text = "Select a sampled variant and enter a non-negative unit-price threshold."; return; }
            _watch.PriceAlertEnabled = true; _watch.AlertBelowUnitPrice = threshold;
            _watch.AlertVariantKey = _groups[_group].Key; _watch.PriceAlertTriggered = false;
            _watch.LastAlertScanUtcTicks = _watch.Scans.LastOrDefault()?.ScanUtcTicks ?? 0;
            _profile.Save(ProfileManager.ProfilePath, false); _status.Text = "Alert saved for this variant; checked on the next complete scan. Alert Centre mute settings apply.";
        }
        private StbTextBox[] Fields() => _listing == null || _listing.IsDisposed || _linkedFields.Any(t => t.IsDisposed || t.Parent != _listing)
            ? Array.Empty<StbTextBox>() : _linkedFields;
        private void CopyListing()
        {
            var fields = Fields(); if (fields.Length != 2) { _status.Text = "Open Pricing / fees from the original listing window to link its price field."; return; }
            var caption = _listing.Children.FirstOrDefault(c => c.IsFromServer && VendorLedgerManager.Text(c).StartsWith("Not for sale", StringComparison.OrdinalIgnoreCase));
            var sale = caption == null ? null : _listing.Children.OfType<Checkbox>().Where(c => c.IsFromServer && Math.Abs(c.Y - caption.Y) <= 12 && c.X <= caption.X)
                .OrderBy(c => Math.Abs(c.X - caption.X)).FirstOrDefault();
            _notForSale.IsChecked = sale?.IsChecked ?? false;
            _price.SetText(fields[0].Text); Fees();
        }
        private void FillListing()
        {
            var fields = Fields();
            if (fields.Length != 2 || !VendorLedgerManager.Money(_price.Text, out _)) { _status.Text = "Link a live listing window and enter a valid whole gold price."; return; }
            fields[0].SetText(_price.Text); _status.Text = "Price filled; sale checkbox unchanged. Use the original Calculate Fees and List Item buttons.";
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            if (_watch != null && !_profile.VendorWatchItems.Contains(_watch)) { _watch = null; _scan = null; SelectWatch(); }
            base.Update();
        }
    }
}

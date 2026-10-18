using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class VendorScanChangesGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly List<VendorMarketAnalysis.Change> _changes;
        private readonly ScrollArea _rows;
        private readonly Label _status;
        private int _page, _filter;

        private VendorScanChangesGump(VendorWatchItem watch, VendorMarketScan before, VendorMarketScan after) : base(0, 0)
        {
            Width = 960; Height = 640; WantUpdateSize = false; CanMove = true; AcceptMouseInput = true; CanCloseWithRightClick = CanCloseWithEsc = true;
            _changes = VendorMarketAnalysis.Compare(before, after);
            Add(new VendorGumpArtwork(Width, Height));
            Text(this, "SCAN COMPARISON — " + watch.Query, 28, 22, 780);
            Add(new VendorLocalButton(850, 20, 80, "Close", Dispose));
            Text(this, VendorCsvExport.TimeStamp(before.ScanUtcTicks) + " → " + VendorCsvExport.TimeStamp(after.ScanUtcTicks), 28, 58, 904);
            Text(this, "Before: " + (before.Complete ? "complete" : "PARTIAL") + " / after: " + (after.Complete ? "complete" : "PARTIAL")
                + ". Missing entries are observations, not confirmed sales.", 28, 84, 904, 0x0386);
            string[] labels = { "All", "Prices", "Amounts", "Newly observed", "Not seen" };
            for (int i = 0; i < labels.Length; i++)
            {
                int filter = i;
                Add(new VendorLocalButton(28 + i * 140, 114, 132, labels[i], () => { _filter = filter; _page = 0; Refresh(); }));
            }
            Add(new VendorLocalButton(740, 114, 190, "Export changes CSV", () => VendorCsvExport.Changes(watch.Query, before, after)));
            Text(this, "CHANGE", 28, 158, 196); Text(this, "ITEM", 230, 158, 240);
            Text(this, "AMOUNT: OLD → NEW", 480, 158, 180); Text(this, "PRICE: OLD → NEW", 670, 158, 260);
            _rows = new ScrollArea(28, 188, 904, 354, true); Add(_rows);
            Add(new VendorLocalButton(28, 556, 110, "Previous", () => { _page = Math.Max(0, _page - 1); Refresh(); }));
            Add(new VendorLocalButton(148, 556, 110, "Next", () => { _page++; Refresh(); }));
            _status = Text(this, "", 276, 562, 654);
            Text(this, "Matching IDs show changes. Anonymous rows require an exact match; partial scans cannot confirm absence.", 28, 610, 904, 0x0386);
            Refresh(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        internal static void Open(VendorWatchItem watch, VendorMarketScan current)
        {
            if (!World.InGame || ProfileManager.CurrentProfile == null) return;
            int index = watch == null || current == null ? -1 : watch.Scans.IndexOf(current);
            if (index < 1) { GameActions.Print("Select a scan with an earlier stored scan to compare.", 0x35); return; }
            UIManager.GetGump<VendorScanChangesGump>()?.Dispose();
            UIManager.Add(new VendorScanChangesGump(watch, watch.Scans[index - 1], current));
        }
        private static Label Text(Control parent, string text, int x, int y, int width, ushort hue = 0x0481, string tip = null)
        {
            var label = new Label(text, true, hue, width, font: 1, style: FontStyle.Cropped) { X = x, Y = y };
            label.SetTooltip(tip ?? text); parent.Add(label); return label;
        }
        private static string Number(decimal? value) => value?.ToString("N2", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.') ?? "?";
        private void Refresh()
        {
            _rows.Clear();
            var filtered = _changes.Where(c => _filter == 0 || (_filter == 1 && c.Kind.Contains("Price")) || (_filter == 2 && c.Kind.Contains("Amount"))
                || (_filter == 3 && c.After != null && c.Before == null) || (_filter == 4 && c.After == null)).ToList();
            _page = Math.Min(_page, Math.Max(0, (filtered.Count - 1) / 10));
            for (int i = _page * 10; i < Math.Min(_page * 10 + 10, filtered.Count); i++)
            {
                var change = filtered[i]; var item = change.After ?? change.Before; int y = (i % 10) * 35;
                ushort hue = change.After == null ? (ushort)0x0035 : change.Before == null ? (ushort)0x0044 : (ushort)0x0481;
                Text(_rows, change.Kind, 0, y, 196, hue);
                Text(_rows, item.ItemName, 202, y, 240, hue, (change.Before?.Data ?? "") + "\n→\n" + (change.After?.Data ?? "") + "\nSerial: " + item.Serial.ToString("X8"));
                Text(_rows, Number(change.Before?.Amount) + " → " + Number(change.After?.Amount), 452, y, 180);
                Text(_rows, Number(change.Before?.Price) + " → " + Number(change.After?.Price), 642, y, 240);
            }
            _status.Text = filtered.Count + " changes / page " + (_page + 1);
            if (filtered.Count == 0) Text(_rows, "No changes in this view.", 0, 8, 860);
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        { if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; } base.Update(); }
    }
}

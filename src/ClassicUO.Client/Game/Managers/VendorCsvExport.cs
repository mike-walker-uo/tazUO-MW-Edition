using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    internal static class VendorCsvExport
    {
        private static bool _busy;
        internal static string TimeStamp(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) : "";

        internal static string Cell(object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if (value is string)
            {
                string trimmed = text.TrimStart();
                if ((trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) || (text.Length > 0 && "\t\r\n".IndexOf(text[0]) >= 0)) text = "'" + text;
            }
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        internal static void Write(TextWriter writer, IEnumerable<object[]> rows)
        {
            foreach (var row in rows) writer.Write(string.Join(",", row.Select(Cell)) + "\r\n");
        }

        internal static List<object[]> SalesRows(IEnumerable<VendorLedgerRecord> vendors)
        {
            var rows = new List<object[]> { new object[] { "shop", "vendor_serial", "history_captured_utc", "item", "buyer", "price_gold", "listed_raw", "sold_raw" } };
            foreach (var vendor in vendors)
                foreach (var sale in vendor.Sales)
                    rows.Add(new object[] { vendor.ShopName, vendor.VendorSerial.ToString("X8"), TimeStamp(vendor.LastHistoryUtcTicks), sale.ItemName,
                        sale.Buyer, sale.Price, sale.ListDate, sale.SellDate });
            return rows;
        }

        internal static List<object[]> MarketRows(IEnumerable<VendorWatchItem> watches)
        {
            var rows = new List<object[]> { new object[] { "query", "scan_utc", "complete", "pages", "note", "item_serial", "item", "amount", "price_gold", "unit_price_gold", "listed_raw", "facet", "graphic", "hue", "properties_known", "details" } };
            foreach (var watch in watches)
                foreach (var scan in watch.Scans)
                {
                    string captured = TimeStamp(scan.ScanUtcTicks);
                    // Keep a row for an empty scan: zero supply must not vanish from the export.
                    if (scan.Listings.Count == 0) rows.Add(new object[] { watch.Query, captured, scan.Complete, scan.Pages, scan.Note, null, null, null, null, null, null, null, null, null, null, null });
                    foreach (var item in scan.Listings)
                        rows.Add(new object[] { watch.Query, captured, scan.Complete, scan.Pages, scan.Note,
                            item.Serial == 0 ? "" : item.Serial.ToString("X8"), item.ItemName, item.Amount, item.Price, item.UnitPrice,
                            item.ListDate, item.Facet, item.ItemGraphic, item.ItemHue, VendorMarketAnalysis.KnownProperties(item), item.Data });
                }
            return rows;
        }

        internal static void Sales(VendorLedgerRecord selected = null)
        {
            if (!World.InGame) return;
            Export("vendor-sales", SalesRows(VendorLedgerManager.Records.Where(v => selected == null || ReferenceEquals(v, selected))));
        }
        internal static void Market(VendorWatchItem selected = null)
        {
            var profile = ProfileManager.CurrentProfile;
            if (!World.InGame || profile == null) return;
            Export("vendor-market", MarketRows(profile.VendorWatchItems.Where(w => selected == null || ReferenceEquals(w, selected))));
        }
        internal static void Changes(string query, VendorMarketScan before, VendorMarketScan after)
        {
            if (before == null || after == null) return;
            var rows = new List<object[]> { new object[] { "query", "before_scan_utc", "after_scan_utc", "before_complete", "after_complete", "change", "item", "serial", "old_amount", "new_amount", "old_price", "new_price", "old_unit_price", "new_unit_price", "old_details", "new_details" } };
            var changes = VendorMarketAnalysis.Compare(before, after);
            if (changes.Count == 0) rows.Add(new object[] { query, TimeStamp(before.ScanUtcTicks), TimeStamp(after.ScanUtcTicks), before.Complete, after.Complete,
                "No observed changes", null, null, null, null, null, null, null, null, null, null });
            foreach (var change in changes)
            {
                var item = change.After ?? change.Before;
                rows.Add(new object[] { query, TimeStamp(before.ScanUtcTicks), TimeStamp(after.ScanUtcTicks), before.Complete, after.Complete,
                    change.Kind, item.ItemName, item.Serial == 0 ? "" : item.Serial.ToString("X8"), change.Before?.Amount, change.After?.Amount,
                    change.Before?.Price, change.After?.Price, change.Before?.UnitPrice, change.After?.UnitPrice, change.Before?.Data, change.After?.Data });
            }
            Export("vendor-changes", rows);
        }

        private static string Folder()
        {
            if (string.IsNullOrEmpty(ProfileManager.ProfilePath)) return null;
            return Path.Combine(ProfileManager.ProfilePath, "exports");
        }

        private static void Export(string prefix, List<object[]> rows)
        {
            if (_busy) { GameActions.Print("A vendor export is already running.", 0x35); return; }
            string folder = Folder(); if (folder == null) return;
            var profile = ProfileManager.CurrentProfile;
            string path = Path.Combine(folder, prefix + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N") + ".csv");
            _busy = true;
            // All rows are snapshots made on the game thread. The worker only handles strings/numbers and disk I/O.
            _ = Task.Run(() =>
            {
                string error = null, temporary = path + ".tmp";
                try
                {
                    Directory.CreateDirectory(folder);
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(true))) Write(writer, rows);
                    File.Move(temporary, path);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                MainThreadQueue.EnqueueAction(() =>
                {
                    _busy = false;
                    if (World.InGame && profile == ProfileManager.CurrentProfile)
                        GameActions.Print(error == null ? "Vendor CSV exported: " + path : "Vendor export failed: " + error, error == null ? (ushort)0x35 : (ushort)0x21);
                });
            });
        }

        internal static void OpenFolder()
        {
            string folder = Folder(); if (folder == null) return;
            try
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) { GameActions.Print("Could not open export folder: " + ex.Message, 0x21); }
        }
    }
}

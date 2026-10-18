using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    internal enum VendorFeeMode { NewListing, Within24Hours, After24Hours }

    internal static class VendorMarketAnalysis
    {
        internal sealed class PriceGroup
        {
            internal string Key, Name, Properties;
            internal bool PropertiesKnown;
            internal int? Amount;
            internal ushort? Graphic, Hue;
            internal List<decimal> Prices;
            internal decimal Low => Prices[0];
            internal decimal Median => Percentile(Prices, 0.5m);
            internal decimal LowerQuartile => Percentile(Prices, 0.25m);
            internal decimal UpperQuartile => Percentile(Prices, 0.75m);
        }

        internal sealed class Change
        {
            internal string Kind;
            internal VendorMarketListing Before, After;
        }

        internal static bool KnownProperties(VendorMarketListing item) => item.PropertiesKnown
            || (!string.IsNullOrWhiteSpace(item.Data) && !item.Data.StartsWith("Cached properties;", StringComparison.Ordinal));

        private static string Name(VendorMarketListing item)
        {
            string name = VendorLedgerManager.Plain(item.ItemName);
            var prefix = Regex.Match(name, @"^([\d,]+)\s+(.+)$");
            if (prefix.Success && item.Amount.HasValue && VendorLedgerManager.Money(prefix.Groups[1].Value, out long amount) && amount == item.Amount.Value)
                name = prefix.Groups[2].Value;
            return Regex.Replace(name, @"\s+", " ").Trim().ToUpperInvariant();
        }

        private static string Properties(VendorMarketListing item)
        {
            return string.Join("\n", VendorLedgerManager.Plain(item.Data).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Regex.Replace(s, @"\s+", " ").Trim().ToUpperInvariant())
                .Where(s => !Regex.IsMatch(s, @"^(?:PRICE(?: PER UNIT)?|UNIT PRICE|AMOUNT|QUANTITY|UNITS|LIST(?:ING)? DATE|LISTED|CACHED PROPERTIES)\s*[:;]"))
                .Where(s => !(item.Amount > 1 && s.StartsWith("WEIGHT:", StringComparison.Ordinal)))
                .OrderBy(s => s, StringComparer.Ordinal));
        }

        private static string DetailsKey(VendorMarketListing item) => Name(item) + "\u001f" + item.ItemGraphic?.ToString(CultureInfo.InvariantCulture)
            + "\u001f" + item.ItemHue?.ToString(CultureInfo.InvariantCulture) + "\u001f" + KnownProperties(item) + "\u001f" + Properties(item);
        internal static string VariantKey(VendorMarketListing item) => DetailsKey(item) + "\u001f" + item.Amount?.ToString(CultureInfo.InvariantCulture);

        internal static List<PriceGroup> Groups(VendorMarketScan scan)
        {
            if (scan == null) return new List<PriceGroup>();
            return scan.Listings.Where(l => l.Price.HasValue && l.UnitPrice.HasValue && l.UnitPrice.Value >= 0).GroupBy(VariantKey)
                .Select(g => new PriceGroup { Key = g.Key, Name = Name(g.First()), Properties = Properties(g.First()),
                    PropertiesKnown = KnownProperties(g.First()), Amount = g.First().Amount, Graphic = g.First().ItemGraphic, Hue = g.First().ItemHue,
                    Prices = g.Select(l => l.UnitPrice.Value).OrderBy(p => p).ToList() })
                .OrderByDescending(g => g.Prices.Count).ThenBy(g => g.Name, StringComparer.Ordinal).ToList();
        }

        internal static decimal Percentile(IReadOnlyList<decimal> sorted, decimal fraction)
        {
            decimal index = (sorted.Count - 1) * fraction;
            int low = (int)decimal.Floor(index), high = (int)decimal.Ceiling(index);
            return sorted[low] + (sorted[high] - sorted[low]) * (index - low);
        }

        internal static decimal? Fee(long price, decimal rate, VendorFeeMode mode, long? oldPrice, decimal repricingPercent)
        {
            if (price < 0 || rate < 0 || rate > 100 || repricingPercent < 0 || repricingPercent > 100) return null;
            if (mode == VendorFeeMode.NewListing) return decimal.Ceiling(price * rate / 100);
            if (!oldPrice.HasValue || oldPrice < 0) return null;
            if (price == oldPrice.Value) return 0; // Description-only edits.
            return mode == VendorFeeMode.Within24Hours
                ? decimal.Ceiling(Math.Max(0, price - oldPrice.Value) * rate / 100)
                : decimal.Ceiling(price * rate / 100 * repricingPercent / 100);
        }

        internal static bool EvaluateAlert(VendorWatchItem watch, VendorMarketScan scan)
        {
            if (!watch.PriceAlertEnabled || !watch.AlertBelowUnitPrice.HasValue || watch.AlertBelowUnitPrice < 0
                || string.IsNullOrEmpty(watch.AlertVariantKey) || !scan.Complete || scan.ScanUtcTicks <= watch.LastAlertScanUtcTicks) return false;
            var prices = scan.Listings.Where(l => l.Price.HasValue && l.UnitPrice.HasValue && VariantKey(l) == watch.AlertVariantKey).Select(l => l.UnitPrice.Value).ToList();
            watch.LastAlertScanUtcTicks = scan.ScanUtcTicks;
            if (prices.Count == 0) return false; // No evidence to re-arm an alert.
            bool below = prices.Min() < watch.AlertBelowUnitPrice.Value;
            bool notify = below && !watch.PriceAlertTriggered;
            watch.PriceAlertTriggered = below;
            return notify;
        }

        private static string Fingerprint(VendorMarketListing item) => VariantKey(item) + "\u001f" + item.Amount
            + "\u001f" + item.Price + "\u001f" + item.QuotedUnitPrice + "\u001f" + item.ListDate + "\u001f" + item.Facet;

        internal static List<Change> Compare(VendorMarketScan before, VendorMarketScan after)
        {
            var changes = new List<Change>();
            if (before == null || after == null) return changes;
            var serials = before.Listings.Where(l => l.Serial != 0).GroupBy(l => l.Serial).ToDictionary(g => g.Key, g => g.First());
            var anonymous = before.Listings.Where(l => l.Serial == 0).GroupBy(Fingerprint).ToDictionary(g => g.Key, g => new Queue<VendorMarketListing>(g));
            var matched = new HashSet<VendorMarketListing>();
            foreach (var item in after.Listings)
            {
                VendorMarketListing old = null;
                if (item.Serial != 0) serials.TryGetValue(item.Serial, out old);
                else if (anonymous.TryGetValue(Fingerprint(item), out var same) && same.Count > 0) old = same.Dequeue();
                if (old == null) { changes.Add(new Change { Kind = "Newly observed", After = item }); continue; }
                matched.Add(old);
                var kinds = new List<string>();
                if (old.Price != item.Price || old.QuotedUnitPrice != item.QuotedUnitPrice) kinds.Add(old.Price.HasValue && item.Price.HasValue ? "Price changed" : "Price data changed");
                if (old.Amount != item.Amount) kinds.Add(old.Amount.HasValue && item.Amount.HasValue ? "Amount changed" : "Amount data changed");
                if (DetailsKey(old) != DetailsKey(item)) kinds.Add("Details changed");
                if (kinds.Count > 0) changes.Add(new Change { Kind = string.Join(" / ", kinds), Before = old, After = item });
            }
            foreach (var old in before.Listings.Where(l => !matched.Contains(l)))
                changes.Add(new Change { Kind = after.Complete ? "No longer observed" : "Not seen (partial scan)", Before = old });
            return changes;
        }
    }
}

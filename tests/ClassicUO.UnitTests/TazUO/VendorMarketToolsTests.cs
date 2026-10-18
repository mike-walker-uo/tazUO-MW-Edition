using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class VendorMarketToolsTests
    {
        private static VendorMarketListing Item(uint serial = 1, long price = 100, int amount = 10, string data = "Exceptional\nPrice: 100", ushort hue = 0)
            => new VendorMarketListing { Serial = serial, ItemName = "Pearl", Price = price, Amount = amount, Data = data,
                ItemGraphic = 1, ItemHue = hue, PropertiesKnown = true };
        private static VendorMarketScan Scan(long ticks, bool complete, params VendorMarketListing[] listings)
            => new VendorMarketScan { ScanUtcTicks = ticks, Complete = complete, Listings = listings.ToList() };

        [Fact]
        public void Comparable_groups_keep_quality_hue_and_pack_size_separate()
        {
            var same = new[] { Item(1, 100), Item(2, 200, data: "Price: 200\nExceptional"), Item(3, 300) };
            var groups = VendorMarketAnalysis.Groups(Scan(1, true, same.Concat(new[] {
                Item(4, 50, data: "Normal"), Item(5, 50, hue: 2), Item(6, 500, amount: 50) }).ToArray()));
            Assert.Equal(4, groups.Count); Assert.Equal(3, groups[0].Prices.Count);
            Assert.Equal(20m, groups[0].Median); Assert.Equal(15m, groups[0].LowerQuartile); Assert.Equal(25m, groups[0].UpperQuartile);
        }

        [Fact]
        public void Unknown_properties_and_missing_unit_prices_do_not_mix_with_known_samples()
        {
            var unknown = Item(2); unknown.PropertiesKnown = false; unknown.Data = "";
            var noUnits = Item(3); noUnits.Amount = null;
            var groups = VendorMarketAnalysis.Groups(Scan(1, true, Item(), unknown, noUnits));
            Assert.Equal(2, groups.Count); Assert.Contains(groups, g => !g.PropertiesKnown);
            Assert.Empty(VendorMarketAnalysis.Groups(Scan(1, true, noUnits)));
            var bundled = Item(4); bundled.Price = null; bundled.QuotedUnitPrice = 1;
            Assert.Empty(VendorMarketAnalysis.Groups(Scan(1, true, bundled)));
        }

        [Fact]
        public void Matching_stack_names_ignore_explicit_count_prefix_without_merging_different_counts()
        {
            var first = Item(); first.ItemName = "10 Pearl";
            var same = Item(2); same.ItemName = "Pearl";
            Assert.Equal(VendorMarketAnalysis.VariantKey(first), VendorMarketAnalysis.VariantKey(same));
            same.Amount = 20; Assert.NotEqual(VendorMarketAnalysis.VariantKey(first), VendorMarketAnalysis.VariantKey(same));
        }

        [Theory]
        [InlineData(1000, 0, 0, 53)]
        [InlineData(1000, 2000, 1, 0)]
        [InlineData(2000, 1000, 1, 53)]
        [InlineData(1000, 2000, 2, 27)]
        [InlineData(1000, 1000, 1, 0)]
        [InlineData(1000, 1000, 2, 0)]
        public void Fee_preview_models_new_grace_and_post_grace_prices(long price, long old, int mode, int expected)
            => Assert.Equal((decimal)expected, VendorMarketAnalysis.Fee(price, 5.25m, (VendorFeeMode)mode, old, 50));

        [Fact]
        public void Fee_preview_rejects_missing_original_price_and_invalid_rates()
        {
            Assert.Null(VendorMarketAnalysis.Fee(1000, 5.25m, VendorFeeMode.Within24Hours, null, 50));
            Assert.Null(VendorMarketAnalysis.Fee(-1, 5.25m, VendorFeeMode.NewListing, null, 50));
            Assert.Null(VendorMarketAnalysis.Fee(1000, 101, VendorFeeMode.NewListing, null, 50));
            Assert.Equal((decimal)long.MaxValue, VendorMarketAnalysis.Fee(long.MaxValue, 100, VendorFeeMode.NewListing, null, 50));
        }

        [Fact]
        public void Opt_in_alerts_fire_once_then_rearm_only_on_complete_evidence()
        {
            var watch = new VendorWatchItem { PriceAlertEnabled = true, AlertBelowUnitPrice = 15, AlertVariantKey = VendorMarketAnalysis.VariantKey(Item()) };
            Assert.True(VendorMarketAnalysis.EvaluateAlert(watch, Scan(1, true, Item())));
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(2, true, Item())));
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(3, false, Item(price: 200))));
            Assert.True(watch.PriceAlertTriggered); Assert.Equal(2, watch.LastAlertScanUtcTicks);
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(4, true, Item(price: 200))));
            Assert.False(watch.PriceAlertTriggered);
            Assert.True(VendorMarketAnalysis.EvaluateAlert(watch, Scan(5, true, Item())));
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(5, true, Item())));
        }

        [Fact]
        public void Alerts_ignore_other_variants_and_missing_prices_without_rearming()
        {
            var watch = new VendorWatchItem { PriceAlertEnabled = true, AlertBelowUnitPrice = 15, PriceAlertTriggered = true, AlertVariantKey = VendorMarketAnalysis.VariantKey(Item()) };
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(1, true, Item(hue: 2))));
            Assert.True(watch.PriceAlertTriggered);
            var missingPrice = Item(); missingPrice.Price = null;
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(2, true, missingPrice)));
            Assert.True(watch.PriceAlertTriggered);
            watch.PriceAlertEnabled = false;
            Assert.False(VendorMarketAnalysis.EvaluateAlert(watch, Scan(3, true, Item())));
        }

        [Fact]
        public void Serial_matched_price_and_amount_changes_are_separate_from_absence()
        {
            var before = Scan(1, true, Item(1), Item(2));
            var after = Scan(2, true, Item(1, 200, 5), Item(3));
            var changes = VendorMarketAnalysis.Compare(before, after);
            Assert.Equal(3, changes.Count);
            Assert.Contains(changes, c => c.Before?.Serial == 1 && c.Kind == "Price changed / Amount changed");
            Assert.Contains(changes, c => c.Kind == "Newly observed" && c.After.Serial == 3);
            Assert.Contains(changes, c => c.Kind == "No longer observed" && c.Before.Serial == 2);
            Assert.DoesNotContain(changes, c => c.Kind.Contains("sold"));
        }

        [Fact]
        public void Anonymous_rows_match_as_a_multiset_and_never_guess_price_changes()
        {
            var before = Scan(1, true, Item(0), Item(0));
            Assert.Single(VendorMarketAnalysis.Compare(before, Scan(2, true, Item(0))));
            var changes = VendorMarketAnalysis.Compare(Scan(1, true, Item(0)), Scan(2, true, Item(0, 200)));
            Assert.Equal(2, changes.Count); Assert.DoesNotContain(changes, c => c.Kind.Contains("Price changed"));
        }

        [Fact]
        public void Partial_scan_absence_stays_explicitly_unconfirmed()
        {
            var change = Assert.Single(VendorMarketAnalysis.Compare(Scan(1, true, Item()), Scan(2, false)));
            Assert.Equal("Not seen (partial scan)", change.Kind);
            Assert.Empty(VendorMarketAnalysis.Compare(null, Scan(2, true)));
        }

        [Fact]
        public void Csv_preserves_quotes_newlines_unicode_and_invariant_numbers()
        {
            Assert.Equal("\"A, \"\"B\"\"\nÄ\"", VendorCsvExport.Cell("A, \"B\"\nÄ"));
            Assert.Equal("\"'-danger\"", VendorCsvExport.Cell("-danger"));
            Assert.Equal("\"'=1+1\"", VendorCsvExport.Cell("=1+1"));
            Assert.Equal("\"-5\"", VendorCsvExport.Cell(-5));
            Assert.Equal("\"12.5\"", VendorCsvExport.Cell(12.5m));
            Assert.Equal("\"\"", VendorCsvExport.Cell(null));
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                VendorCsvExport.Write(writer, new[] { new object[] { "item", 12.5m } });
                Assert.Equal("\"item\",\"12.5\"\r\n", writer.ToString());
            }
        }

        [Fact]
        public void Market_export_keeps_empty_and_partial_scan_metadata()
        {
            var watch = new VendorWatchItem { Query = "Pearl", Scans = new List<VendorMarketScan> { Scan(1, true), Scan(2, false, Item()) } };
            var rows = VendorCsvExport.MarketRows(new[] { watch });
            Assert.Equal(3, rows.Count); Assert.Equal(true, rows[1][2]); Assert.Null(rows[1][7]);
            Assert.Equal(false, rows[2][2]); Assert.Equal(10, rows[2][7]);
        }

        [Fact]
        public void Rates_alert_rules_and_captured_metadata_survive_serialization()
        {
            var profile = new Profile { VendorListingFeePercent = 6.5m, VendorRepriceFeePercent = 25 };
            profile.VendorWatchItems.Add(new VendorWatchItem { Query = "Pearl", PriceAlertEnabled = true, AlertBelowUnitPrice = 12.5m,
                AlertVariantKey = "sample", PriceAlertTriggered = true, LastAlertScanUtcTicks = 1, Scans = new List<VendorMarketScan> { Scan(1, true, Item()) } });
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(6.5m, restored.VendorListingFeePercent); Assert.Equal(25m, restored.VendorRepriceFeePercent);
            var watch = Assert.Single(restored.VendorWatchItems); Assert.True(watch.PriceAlertEnabled); Assert.True(watch.PriceAlertTriggered);
            Assert.Equal(12.5m, watch.AlertBelowUnitPrice); Assert.Equal((ushort)1, watch.Scans[0].Listings[0].ItemGraphic);
            Assert.True(watch.Scans[0].Listings[0].PropertiesKnown);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class VendorWatchTests
    {
        [Theory]
        [InlineData("Vendor Search Query\nItem Name\nSort Results\nSelected Search Criteria", true, false)]
        [InlineData("Vendor Search\nSelected Search Criteria\nSort Results", true, false)]
        [InlineData("Vendor Search Results\nPrice\nFacet\nCreate Map", false, true)]
        [InlineData("Global Vendor Market Results\nItem Name\nPrice", false, true)]
        [InlineData("Please wait for your search to complete.", false, false)]
        [InlineData("Sort Results", false, false)]
        public void Query_sort_label_is_not_a_market_results_window(string text, bool query, bool results)
        {
            Assert.Equal(query, VendorSearchWatchManager.IsQueryWindow(text));
            Assert.Equal(results, VendorSearchWatchManager.IsResultsWindow(text));
        }

        [Fact]
        public void UOAlive_name_field_above_caption_is_selected_instead_of_price_inputs()
        {
            var caption = new Rectangle(27, 87, 100, 20);
            Assert.NotEqual(int.MaxValue, VendorSearchWatchManager.NameFieldScore(caption, new Rectangle(186, 81, 297, 30)));
            Assert.Equal(int.MaxValue, VendorSearchWatchManager.NameFieldScore(caption, new Rectangle(186, 123, 116, 30)));
            Assert.Equal(int.MaxValue, VendorSearchWatchManager.NameFieldScore(caption, new Rectangle(186, 163, 116, 30)));
        }

        [Fact]
        public void Classic_name_field_below_caption_is_preferred_over_other_columns()
        {
            var caption = new Rectangle(10, 30, 246, 18);
            int name = VendorSearchWatchManager.NameFieldScore(caption, new Rectangle(12, 52, 242, 18));
            int price = VendorSearchWatchManager.NameFieldScore(caption, new Rectangle(268, 52, 242, 18));
            Assert.NotEqual(int.MaxValue, name); Assert.True(name < price);
        }

        [Fact]
        public void Targeted_vendors_are_deduplicated_without_claiming_ownership()
        {
            var profile = new Profile();
            var first = VendorLedgerManager.AddVendor(profile, 42, "Merchant");
            Assert.Same(first, VendorLedgerManager.AddVendor(profile, 42, "Renamed"));
            Assert.Single(profile.VendorLedger); Assert.Equal(42u, first.VendorSerial);
            Assert.False(first.OwnerVerified); Assert.Equal("Merchant", first.ShopName);
            Assert.Null(VendorLedgerManager.AddVendor(profile, 0, "Invalid"));
        }

        [Fact]
        public void Watches_normalize_outer_spaces_and_case_without_resetting_history()
        {
            var profile = new Profile();
            var first = VendorSearchWatchManager.AddWatch(profile, " White Pearl ");
            first.Scans.Add(new VendorMarketScan { ScanUtcTicks = 123 });
            Assert.Same(first, VendorSearchWatchManager.AddWatch(profile, "white pearl"));
            Assert.Single(profile.VendorWatchItems); Assert.Single(first.Scans);
            Assert.Null(VendorSearchWatchManager.AddWatch(profile, "  "));
            Assert.Null(VendorSearchWatchManager.AddWatch(profile, new string('x', 101)));
        }

        [Fact]
        public void Watch_count_is_bounded_but_existing_watches_remain_accessible()
        {
            var profile = new Profile();
            for (int i = 0; i < 32; i++) Assert.NotNull(VendorSearchWatchManager.AddWatch(profile, "Item " + i));
            Assert.Null(VendorSearchWatchManager.AddWatch(profile, "Overflow"));
            Assert.Same(profile.VendorWatchItems[0], VendorSearchWatchManager.AddWatch(profile, "Item 0"));
        }

        [Theory]
        [InlineData("White Pearl", "", null)]
        [InlineData("10 White Pearl", "", 10)]
        [InlineData("1,000 Boards", "", 1000)]
        [InlineData("White Pearl", "Quantity: 25", 25)]
        [InlineData("Sword", "Weight: 8 Stones", null)]
        [InlineData("Sword", "Quantity: 9999999999999999999999", null)]
        public void Quantities_require_explicit_server_evidence(string name, string data, int? expected)
            => Assert.Equal(expected, VendorSearchWatchManager.Amount(name, data));

        [Fact]
        public void Unit_price_preserves_stack_totals_and_unknown_amounts()
        {
            var listing = new VendorMarketListing { Price = 150000, Amount = 10 };
            Assert.Equal(15000m, listing.UnitPrice);
            listing.Amount = null; Assert.Null(listing.UnitPrice);
            listing.Amount = 0; Assert.Null(listing.UnitPrice);
            listing.Amount = 10; listing.Price = 0; Assert.Equal(0m, listing.UnitPrice);
            listing.Price = null; Assert.Null(listing.UnitPrice);
            listing.QuotedUnitPrice = 37.5m; Assert.Equal(37.5m, listing.UnitPrice);
        }

        [Theory]
        [InlineData("1,234.50", true)]
        [InlineData("0", true)]
        [InlineData("-1", false)]
        [InlineData("250k", false)]
        [InlineData("unknown", false)]
        public void Server_unit_prices_allow_fractions_but_reject_invalid_values(string text, bool valid)
            => Assert.Equal(valid, VendorSearchWatchManager.UnitQuote(text).HasValue);

        private static VendorLedgerManager.Cell Cell(string text, int x, int y)
            => new VendorLedgerManager.Cell { Text = text, X = x, Y = y };

        [Fact]
        public void Table_scan_distinguishes_amount_column_from_name_and_retains_raw_dates()
        {
            var rows = VendorSearchWatchManager.ParseTable(new List<VendorLedgerManager.Cell> {
                Cell("Item Name", 10, 20), Cell("Amount", 200, 20), Cell("Price", 300, 20), Cell("List Date", 410, 20),
                Cell("White Pearl", 10, 50), Cell("10", 200, 50), Cell("150,000", 300, 50), Cell("10/13", 410, 50),
                Cell("White Pearl", 10, 80), Cell("25", 200, 80), Cell("250,000", 300, 80), Cell("10/14", 410, 80) });
            Assert.Equal(2, rows.Count); Assert.Equal("White Pearl", rows[0].ItemName);
            Assert.Equal(10, rows[0].Amount); Assert.Equal(150000, rows[0].Price); Assert.Equal("10/13", rows[0].ListDate);
            Assert.Equal(10000m, rows[1].UnitPrice);
        }

        [Fact]
        public void Missing_table_amount_and_date_remain_unknown()
        {
            var rows = VendorSearchWatchManager.ParseTable(new List<VendorLedgerManager.Cell> {
                Cell("Item Name", 10, 20), Cell("Price", 300, 20), Cell("Sword", 10, 50), Cell("50,000", 300, 50) });
            var listing = Assert.Single(rows); Assert.Null(listing.Amount); Assert.Null(listing.UnitPrice); Assert.Empty(listing.ListDate);
            Assert.Null(VendorSearchWatchManager.ParseTable(new List<VendorLedgerManager.Cell> { Cell("Unrelated", 10, 10) }));
        }

        [Fact]
        public void Scan_history_is_bounded_and_keeps_partial_observations_labelled()
        {
            var watch = new VendorWatchItem();
            for (int i = 0; i < 40; i++) VendorSearchWatchManager.SaveScan(watch, new VendorMarketScan { ScanUtcTicks = i, Complete = i % 2 == 0 });
            Assert.Equal(32, watch.Scans.Count); Assert.Equal(8, watch.Scans[0].ScanUtcTicks);
            Assert.False(watch.Scans.Last().Complete);
        }

        [Fact]
        public void Global_cache_budget_evicts_old_scans_but_keeps_each_watch_latest()
        {
            var watches = new List<VendorWatchItem>();
            for (int i = 0; i < 3; i++)
                watches.Add(new VendorWatchItem { Query = "Item " + i, Scans = new List<VendorMarketScan> {
                    new VendorMarketScan { ScanUtcTicks = i, Listings = new List<VendorMarketListing> { new VendorMarketListing() } },
                    new VendorMarketScan { ScanUtcTicks = 100 + i, Listings = new List<VendorMarketListing> { new VendorMarketListing() } } } });
            VendorSearchWatchManager.TrimHistory(watches, 3);
            Assert.All(watches, w => Assert.True(Assert.Single(w.Scans).ScanUtcTicks >= 100));
        }

        [Fact]
        public void Vendor_and_market_data_survive_profile_serialization()
        {
            var profile = new Profile();
            VendorLedgerManager.AddVendor(profile, 42, "Shop").OwnerVerified = true;
            var watch = VendorSearchWatchManager.AddWatch(profile, "Pearl");
            watch.Scans.Add(new VendorMarketScan { ScanUtcTicks = 123, Complete = false, Pages = 2, Note = "Stopped", Listings = new List<VendorMarketListing> {
                new VendorMarketListing { Serial = 43, ItemName = "10 White Pearl", Amount = 10, Price = 150000, ListDate = "10/13", Data = "Crafting material" } } });
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.True(restored.VendorLedger[0].OwnerVerified);
            var scan = Assert.Single(Assert.Single(restored.VendorWatchItems).Scans);
            Assert.False(scan.Complete); Assert.Equal("Stopped", scan.Note); Assert.Equal(123, scan.ScanUtcTicks);
            Assert.Equal("10/13", scan.Listings[0].ListDate); Assert.Equal("Crafting material", scan.Listings[0].Data);
        }

        [Fact]
        public void Observations_do_not_enter_option_undo_but_scan_preference_does()
        {
            using (var before = JsonDocument.Parse("{\"vendor_watch_items\":[],\"vendor_search_scans_enabled\":true}"))
            using (var after = JsonDocument.Parse("{\"vendor_watch_items\":[1],\"vendor_search_scans_enabled\":false}"))
            {
                var change = SettingsHistory.Changes(before.RootElement, after.RootElement);
                Assert.Single(change.After); Assert.True(change.After.ContainsKey("vendor_search_scans_enabled"));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class TravelLocationAndVendorTests
    {
        [Theory]
        [InlineData("Trammel", 1)]
        [InlineData("Felucca", 0)]
        [InlineData("TerMur", 5)]
        [InlineData("Tokuno Islands", 4)]
        public void Coordinates_keep_the_explicit_facet(string facet, int expected)
        {
            var location = TravelLocationManager.Parse("X: 120, Y: 180, Z: -5 (" + facet + ")", null);
            Assert.NotNull(location); Assert.Equal(expected, location.Facet);
            Assert.Equal(120, location.X); Assert.Equal(180, location.Y); Assert.Equal(-5, location.Z);
            Assert.False(location.Approximate);
        }

        [Theory]
        [InlineData("Nowhere")]
        [InlineData("Shrine")]
        [InlineData("90° 60'N, 10° 2'E")]
        [InlineData("X: 999999, Y: 3")]
        public void Invalid_or_missing_coordinates_remain_unknown(string text) => Assert.Null(TravelLocationManager.Parse(text, 1));

        [Fact]
        public void Sextant_coordinates_decode_entities_and_mark_the_estimate()
        {
            var location = TravelLocationManager.Parse("<center>100&deg; 0'N, 100&deg; 0'E</center>", 1);
            Assert.Equal(2745, location.X); Assert.Equal(486, location.Y); Assert.True(location.Approximate);
            Assert.Null(TravelLocationManager.Parse("0° 0'N, 0° 0'E", null));
            Assert.Null(TravelLocationManager.Parse("0° 0'N, 0° 0'E", 1)); // Same sextant reading can identify two different regions.
        }

        [Fact]
        public void Shared_map_hues_and_conflicting_names_do_not_guess_a_facet()
        {
            Assert.Null(TravelLocationManager.FacetFromHue(1102, false));
            Assert.Null(TravelLocationManager.FacetFromHue(0x14B, true));
            Assert.Null(TravelLocationManager.FacetFromText("Felucca shop (Trammel)"));
            Assert.Equal(1, TravelLocationManager.FacetFromHue(10, true));
        }

        [Fact]
        public void Location_identity_distinguishes_sources_slots_and_changed_names()
        {
            var first = new WorldExplorerPin { Kind = "atlas", Serial = 1, Slot = 0, Name = "Bank" };
            var second = new WorldExplorerPin { Kind = "atlas", Serial = 1, Slot = 1, Name = "Bank" };
            Assert.NotEqual(TravelLocationManager.Key(first), TravelLocationManager.Key(second));
            second.Slot = 0; second.Serial = 2; Assert.NotEqual(TravelLocationManager.Key(first), TravelLocationManager.Key(second));
            second.Serial = 1; second.Name = "Changed"; Assert.NotEqual(TravelLocationManager.Key(first), TravelLocationManager.Key(second));
        }

        [Fact]
        public void A_destination_is_added_once_and_later_arrivals_do_not_replace_it()
        {
            var previous = ProfileManager.CurrentProfile;
            try
            {
                var profile = new Profile();
                typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, profile);
                var pin = new WorldExplorerPin { Kind = "rune", Serial = 42, Name = "Home" };
                profile.WorldExplorerPins.Add(new WorldExplorerPin { Kind = pin.Kind, Serial = pin.Serial, Name = pin.Name });
                var original = new TravelDestinationLocation { X = 100, Y = 200, Facet = 1 };
                TravelLocationManager.Store(pin, original, false);
                for (int i = 0; i < 20; i++) TravelLocationManager.Store(pin, new TravelDestinationLocation { X = 300, Y = 400, Facet = 0 }, false);
                Assert.Single(profile.WorldExplorerLocations);
                Assert.Same(original, pin.Location); Assert.Same(original, profile.WorldExplorerPins[0].Location);
            }
            finally { typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, previous); TravelLocationManager.Reset(); }
        }

        [Fact]
        public void Saved_locations_vendor_snapshots_and_templates_survive_profile_serialization()
        {
            var profile = new Profile();
            profile.WorldExplorerLocations["rune:1"] = new TravelDestinationLocation { X = 100, Y = 200, Facet = 1, Source = "Observed arrival" };
            profile.VendorLedger.Add(new VendorLedgerRecord { ShopName = "Crafting Finest", GoldHeld = 23827772,
                Sales = new List<VendorSaleRecord> { new VendorSaleRecord { ItemName = "Ring", Buyer = "Ners", Price = 75000, SellDate = "09/24",
                    PropertyText = "Ring\nFire Resist 15%", PropertyLayer = 8, PropertySerial = 0x40001234 } },
                Stock = new List<VendorStockRecord> { new VendorStockRecord { Serial = 42, FirstSeenUtcTicks = 123, LastSeenUtcTicks = 456 } } });
            profile.VendorListingTemplates.Add(new VendorListingTemplate { Name = "Pearls", Price = "150000", Description = "Ten pearls" });
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(1, restored.WorldExplorerLocations["rune:1"].Facet);
            Assert.Null(restored.WorldExplorerLocations["rune:1"].Z);
            Assert.Equal(75000, restored.VendorLedger[0].Sales[0].Price);
            Assert.Equal("09/24", restored.VendorLedger[0].Sales[0].SellDate);
            Assert.Equal("Ring\nFire Resist 15%", restored.VendorLedger[0].Sales[0].PropertyText);
            Assert.Equal((byte)8, restored.VendorLedger[0].Sales[0].PropertyLayer);
            Assert.Equal(0u, restored.VendorLedger[0].Sales[0].PropertySerial);
            Assert.Equal(123, restored.VendorLedger[0].Stock[0].FirstSeenUtcTicks);
            Assert.Equal("Ten pearls", restored.VendorListingTemplates[0].Description);
        }

        private static List<VendorLedgerManager.Cell> SalesTable()
        {
            var cells = new List<VendorLedgerManager.Cell>();
            string[] headers = { "Item Name", "Buyer", "Sell Price", "List Date", "Sell Date" };
            for (int i = 0; i < 5; i++) cells.Add(new VendorLedgerManager.Cell { X = i * 100, Y = 100, Text = headers[i] });
            for (int row = 0; row < 2; row++)
            {
                string[] data = { "10 White Pearl", "KableCrafter", "150,000", "05/12", "09/22" };
                for (int i = 0; i < 5; i++) cells.Add(new VendorLedgerManager.Cell { X = i * 100, Y = 130 + row * 30 + i % 2, Text = data[i] });
            }
            return cells;
        }

        [Fact]
        public void Sales_parser_preserves_identical_transactions_and_partial_dates()
        {
            var rows = VendorLedgerManager.ParseSales(SalesTable());
            Assert.Equal(2, rows.Count); Assert.Equal(300000m, VendorLedgerManager.Gross(rows));
            Assert.Equal("09/22", rows[0].SellDate);
            Assert.True(VendorLedgerManager.Matches(rows[0], "kable"));
            Assert.True(VendorLedgerManager.Matches(rows[0], "pearl"));
            Assert.False(VendorLedgerManager.Matches(rows[0], "slayer"));
        }

        [Fact]
        public void Sales_tooltips_stay_attached_to_individual_rows_with_identical_names()
        {
            var cells = SalesTable();
            cells[5].Tooltip = "10 White Pearl\nWeight 1";
            cells[10].Tooltip = "10 White Pearl\nWeight 2";
            var rows = VendorLedgerManager.ParseSales(cells);
            Assert.Equal("10 White Pearl\nWeight 1", rows[0].PropertyText);
            Assert.Equal("10 White Pearl\nWeight 2", rows[1].PropertyText);
            cells[10].Tooltip = null;
            rows = VendorLedgerManager.ParseSales(cells);
            Assert.Equal("", rows[1].PropertyText);
        }

        [Fact]
        public void Malformed_sales_do_not_become_partial_or_invented_totals()
        {
            var cells = SalesTable(); cells.Last().Text = "unknown";
            Assert.Null(VendorLedgerManager.ParseSales(cells));
            cells = SalesTable(); cells.RemoveAt(cells.Count - 2);
            Assert.Null(VendorLedgerManager.ParseSales(cells));
            Assert.Null(VendorLedgerManager.ParseSales(new List<VendorLedgerManager.Cell>()));
        }

        [Fact]
        public void Gold_parser_rejects_negative_fractional_and_overflow_values()
        {
            Assert.True(VendorLedgerManager.Money("23,827,772", out long gold)); Assert.Equal(23827772, gold);
            foreach (string invalid in new[] { "-1", "250k", "1.5", "unknown", "99999999999999999999999999" })
                Assert.False(VendorLedgerManager.Money(invalid, out _));
        }

        [Fact]
        public void Vendor_signatures_require_the_specific_owner_history_or_listing_markers()
        {
            Assert.Equal(VendorGumpKind.None, VendorLedgerManager.Recognize("Shop Name"));
            Assert.Equal(VendorGumpKind.Owner, VendorLedgerManager.Recognize("Shop Name Dismiss Vendor View Sales History"));
            Assert.Equal(VendorGumpKind.Owner, VendorLedgerManager.Recognize("Shop Name See goods View Sales History"));
            Assert.Equal(VendorGumpKind.Owner, VendorLedgerManager.Recognize("Shop Name See goods"));
            Assert.Equal(VendorGumpKind.Owner, VendorLedgerManager.Recognize("Shop Name View Sales History"));
            Assert.Equal(VendorGumpKind.None, VendorLedgerManager.Recognize("Shop Name Customize"));
            Assert.Equal(VendorGumpKind.History, VendorLedgerManager.Recognize("Vendor Sales History Buyer Sell Price"));
            Assert.Equal(VendorGumpKind.Listing, VendorLedgerManager.Recognize("Set Price and Description Listing fee"));
        }

        [Fact]
        public void Runtime_observations_are_not_option_undo_entries_but_templates_are()
        {
            using (var before = JsonDocument.Parse("{\"vendor_ledger\":[],\"world_explorer_locations\":{},\"vendor_listing_templates\":[]}"))
            using (var after = JsonDocument.Parse("{\"vendor_ledger\":[1],\"world_explorer_locations\":{\"rune\":1},\"vendor_listing_templates\":[1]}"))
            {
                var change = SettingsHistory.Changes(before.RootElement, after.RootElement);
                Assert.Single(change.After); Assert.True(change.After.ContainsKey("vendor_listing_templates"));
            }
        }

        [Fact]
        public void Outgoing_observer_decodes_casts_targets_and_context_requests_without_mutating_packets()
        {
            byte[] cast = { 0xBF, 0, 9, 0, 0x1C, 0, 2, 0, 32 };
            byte[] before = cast.ToArray();
            Assert.True(ClientInteractionObserver.Decode(cast, out int kind, out uint value));
            Assert.Equal(1, kind); Assert.Equal(32u, value); Assert.Equal(before, cast);
            byte[] target = new byte[19]; target[0] = 0x6C; target[7] = 0x40; target[10] = 5;
            Assert.True(ClientInteractionObserver.Decode(target, out kind, out value)); Assert.Equal(2, kind); Assert.Equal(0x40000005u, value);
            byte[] menu = { 0xBF, 0, 9, 0, 0x13, 0, 0, 0, 42 };
            Assert.True(ClientInteractionObserver.Decode(menu, out kind, out value)); Assert.Equal(3, kind); Assert.Equal(42u, value);
            Assert.False(ClientInteractionObserver.Decode(new byte[] { 0x6C, 0 }, out _, out _));
            target[1] = 1; Assert.False(ClientInteractionObserver.Decode(target, out _, out _));
            byte[] targeted = { 0xBF, 0, 11, 0, 0x2D, 0, 32, 0x40, 0, 0, 5 };
            Assert.True(ClientInteractionObserver.Decode(targeted, out kind, out value)); Assert.Equal(5, kind); Assert.Equal(32u, value);
            byte[] response = { 0xB1, 0, 15, 0, 0, 0, 42, 0, 0, 1, 0xF2, 0, 0, 0, 4 };
            Assert.True(ClientInteractionObserver.Decode(response, out kind, out value)); Assert.Equal(4, kind); Assert.Equal(4u, value);
        }
    }
}

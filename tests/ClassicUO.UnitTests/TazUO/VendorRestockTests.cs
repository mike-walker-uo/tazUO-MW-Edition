using System;
using System.Linq;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class VendorRestockTests
    {
        private static VendorLedgerRecord Vendor(string shop = "Shop") => new VendorLedgerRecord
        {
            ShopName = shop, LastStockUtcTicks = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc).Ticks,
            StockComplete = true,
            Sales = { new VendorSaleRecord { ItemName = "Ring", Price = 100 } }
        };

        [Fact]
        public void Remove_hides_only_that_vendors_checklist_row_without_deleting_sales_or_stock()
        {
            var first = Vendor("First"); var second = Vendor("Second");
            first.Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = 1 });
            first.RestockedNames.Add("Ring");
            var vendors = new[] { first, second };
            Assert.True(VendorLedgerManager.RemoveRestock(first, "Ring"));
            Assert.False(VendorLedgerManager.RemoveRestock(first, "ring"));
            Assert.Same(second, Assert.Single(VendorLedgerManager.RestockRows(vendors, null, "ring")).Vendor);
            Assert.Equal(2, VendorLedgerManager.SalesRows(vendors, null, "").Count());
            Assert.Single(first.Sales); Assert.Single(first.Stock); Assert.Contains("Ring", first.RestockedNames);
        }

        [Fact]
        public void Restore_can_apply_to_selected_vendor_or_all_vendors()
        {
            var first = Vendor("First"); var second = Vendor("Second"); var vendors = new[] { first, second };
            VendorLedgerManager.RemoveRestock(first, "Ring"); VendorLedgerManager.RemoveRestock(second, "Ring");
            Assert.True(VendorLedgerManager.RestoreRestock(vendors, first));
            Assert.Same(first, Assert.Single(VendorLedgerManager.RestockRows(vendors, null, "")).Vendor);
            Assert.False(VendorLedgerManager.RestoreRestock(vendors, first));
            Assert.True(VendorLedgerManager.RestoreRestock(vendors, null));
            Assert.Equal(2, VendorLedgerManager.RestockRows(vendors, null, "").Count());
        }

        [Fact]
        public void Stock_only_items_appear_without_inventing_sales_and_respect_search_and_scope()
        {
            var first = Vendor("First"); var second = Vendor("Second");
            first.Stock.Add(new VendorStockRecord { ItemName = "Pearl", Amount = 10 });
            first.Stock.Add(new VendorStockRecord { ItemName = "pearl", Amount = 20 });
            var vendors = new[] { first, second };
            var row = Assert.Single(VendorLedgerManager.RestockRows(vendors, null, "pearl"));
            Assert.Same(first, row.Vendor); Assert.Equal(0, row.Sales);
            Assert.Equal(1, first.Sales.Count);
            Assert.Empty(VendorLedgerManager.RestockRows(vendors, second, "pearl"));
            Assert.All(VendorLedgerManager.RestockRows(vendors, null, "First"), r => Assert.Same(first, r.Vendor));
            Assert.Equal((2, 30L, true), VendorLedgerManager.RestockStock(first, row.ItemName));
            VendorLedgerManager.RemoveRestock(first, row.ItemName);
            Assert.Empty(VendorLedgerManager.RestockRows(vendors, null, "pearl"));
        }

        [Fact]
        public void Bundle_rows_match_quantity_and_do_not_duplicate_stock_names_missing_the_count()
        {
            var vendor = Vendor(); vendor.Sales.Clear();
            vendor.Sales.Add(new VendorSaleRecord { ItemName = "10 spider carapace" });
            vendor.Stock.Add(new VendorStockRecord { ItemName = "spider carapace", Amount = 10 });
            vendor.Stock.Add(new VendorStockRecord { ItemName = "spider carapace", Amount = 100 });
            var rows = VendorLedgerManager.RestockRows(new[] { vendor }, null, "").ToArray();
            Assert.Equal(2, rows.Length);
            Assert.Equal(1, rows.Single(r => r.ItemName == "10 spider carapace").Sales);
            Assert.Equal(0, rows.Single(r => r.ItemName == "100 spider carapace").Sales);
            Assert.Equal((1, 10L, true), VendorLedgerManager.RestockStock(vendor, "10 spider carapace"));
            Assert.Equal((1, 100L, true), VendorLedgerManager.RestockStock(vendor, "100 spider carapace"));
            VendorLedgerManager.RemoveRestock(vendor, "10 spider carapace");
            Assert.Equal("100 spider carapace", Assert.Single(VendorLedgerManager.RestockRows(new[] { vendor }, null, "")).ItemName);
        }

        [Fact]
        public void Matching_names_keep_articles_case_and_exact_lot_descriptions_compatible()
        {
            var vendor = Vendor();
            vendor.Stock.Add(new VendorStockRecord { ItemName = "a ring", Amount = 1 });
            vendor.Stock.Add(new VendorStockRecord { ItemName = "RING", Amount = 1 });
            vendor.Stock.Add(new VendorStockRecord { ItemName = "ringmail", Amount = 50 });
            Assert.Equal((2, 2L, true), VendorLedgerManager.RestockStock(vendor, "Ring"));
            vendor.Stock.Add(new VendorStockRecord { ItemName = "10 pearls", Amount = 1 });
            Assert.Equal((1, 1L, true), VendorLedgerManager.RestockStock(vendor, "10 pearls"));
        }

        [Fact]
        public void Only_a_complete_capture_can_report_zero_as_known_stock()
        {
            var vendor = Vendor();
            vendor.StockComplete = false;
            Assert.Equal((0, 0L, false), VendorLedgerManager.RestockStock(vendor, "Ring"));
            vendor.Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = 10 });
            Assert.Equal((1, 10L, false), VendorLedgerManager.RestockStock(vendor, "Ring"));
            vendor.Stock.Clear(); vendor.StockComplete = true;
            Assert.Equal((0, 0L, true), VendorLedgerManager.RestockStock(vendor, "Ring"));
            vendor.LastStockUtcTicks = 0;
            Assert.False(VendorLedgerManager.RestockStock(vendor, "Ring").Complete);
            vendor.LastStockUtcTicks = long.MaxValue;
            Assert.False(VendorLedgerManager.RestockStock(vendor, "Ring").Complete);
        }

        [Fact]
        public void Unit_totals_use_captured_amounts_and_do_not_overflow_or_infer_sold_quantities()
        {
            var vendor = Vendor();
            vendor.Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = int.MaxValue });
            vendor.Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = int.MaxValue });
            Assert.Equal((2, 2L * int.MaxValue, true), VendorLedgerManager.RestockStock(vendor, "Ring"));
            vendor.Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = 0 });
            Assert.False(VendorLedgerManager.RestockStock(vendor, "Ring").Complete);
        }

        [Fact]
        public void Grouped_restock_combines_sales_and_stock_once_per_vendor_without_changing_records()
        {
            var first = Vendor("First"); var second = Vendor("Second"); var third = Vendor("Third");
            first.Sales.Clear(); second.Sales.Clear(); third.Sales.Clear();
            first.Sales.AddRange(Enumerable.Range(0, 4).Select(_ => new VendorSaleRecord { ItemName = "10 spider carapace" }));
            second.Sales.AddRange(Enumerable.Range(0, 3).Select(_ => new VendorSaleRecord { ItemName = "10 spider carapace" }));
            third.Sales.AddRange(Enumerable.Range(0, 7).Select(_ => new VendorSaleRecord { ItemName = "10 SPIDER CARAPACE" }));
            third.Stock.Add(new VendorStockRecord { ItemName = "spider carapace", Amount = 10 });
            var vendors = new[] { third, first, second };
            var row = Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "carapace"));
            Assert.Equal(14L, row.Sales); Assert.Equal((1, 10L, true), row.Stock);
            Assert.False(row.OlderThanHistory);
            Assert.Equal(new[] { first, second, third }, row.Members.Select(r => r.Vendor));
            Assert.Equal(new[] { 7, 4, 3 }, vendors.Select(v => v.Sales.Count));
            Assert.Single(third.Stock); Assert.Empty(first.Stock); Assert.Empty(second.Stock);
            Assert.All(vendors, v => Assert.Empty(v.RestockedNames));
        }

        [Fact]
        public void Grouped_totals_include_unknown_vendors_and_stock_only_vendors_without_inventing_sales()
        {
            var first = Vendor("First"); var second = Vendor("Second"); var unknown = Vendor("Unknown");
            second.Sales.Clear(); unknown.Sales.Clear(); unknown.StockComplete = false;
            second.Stock.Add(new VendorStockRecord { ItemName = "ring", Amount = 1 });
            var vendors = new[] { first, second, unknown };
            var row = Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "Ring"));
            Assert.Equal(1L, row.Sales); Assert.Equal((1, 1L, false), row.Stock);
            Assert.Equal(0, row.Members.Single(r => r.Vendor == unknown).Sales);
            second.Stock.Clear();
            Assert.Equal((0, 0L, false), Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "Ring")).Stock);
            unknown.StockComplete = true;
            Assert.Equal((0, 0L, true), Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "Ring")).Stock);
            first.LastHistoryUtcTicks = first.LastStockUtcTicks + 1;
            Assert.True(Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "Ring")).OlderThanHistory);
        }

        [Fact]
        public void Grouped_search_keeps_totals_and_vendor_scope_removal_and_restore_remain_explicit()
        {
            var first = Vendor("First"); var second = Vendor("Second"); var vendors = new[] { first, second };
            first.Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = 1 });
            var row = Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "First"));
            Assert.Equal(2L, row.Sales); Assert.Equal(2, row.Members.Length);
            Assert.Empty(VendorLedgerManager.GroupRestock(vendors, null, "missing"));
            Assert.Equal(1L, Assert.Single(VendorLedgerManager.GroupRestock(vendors, first, "Ring")).Sales);
            second.Sales.Add(new VendorSaleRecord { ItemName = "Pearl" });
            Assert.Equal("Ring", Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "First")).ItemName);
            second.Sales.RemoveAt(1);
            VendorLedgerManager.RemoveRestock(first, "Ring");
            row = Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "Ring"));
            Assert.Same(second, Assert.Single(row.Members).Vendor); Assert.Equal((0, 0L, true), row.Stock);
            foreach (var member in row.Members) VendorLedgerManager.RemoveRestock(member.Vendor, member.ItemName);
            Assert.Empty(VendorLedgerManager.GroupRestock(vendors, null, ""));
            Assert.True(VendorLedgerManager.RestoreRestock(vendors, null));
            Assert.Equal(2L, Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "Ring")).Sales);
            Assert.All(vendors, v => Assert.Single(v.Sales)); Assert.Single(first.Stock);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Grouped_sort_uses_totals_before_pagination_and_keeps_unknown_stock_last(bool descending)
        {
            var first = Vendor("First"); var second = Vendor("Second"); var vendors = new[] { first, second };
            first.Sales.Clear(); second.Sales.Clear();
            for (int i = 1; i <= 25; i++)
            {
                first.Sales.AddRange(Enumerable.Range(0, i).Select(_ => new VendorSaleRecord { ItemName = "Item " + i }));
                second.Sales.Add(new VendorSaleRecord { ItemName = "Item " + i });
            }
            var rows = VendorLedgerManager.GroupRestock(vendors, null, "").ToArray();
            var expected = Enumerable.Range(2, 25);
            if (descending) expected = expected.Reverse();
            Assert.Equal(expected.Skip(10).Take(10), VendorLedgerManager.SortRestock(rows, VendorLedgerColumn.Sales, descending)
                .Skip(10).Take(10).Select(r => (int)r.Sales));
            first.StockComplete = false;
            first.Stock.Add(new VendorStockRecord { ItemName = "Item 1", Amount = 1 });
            first.Stock.Add(new VendorStockRecord { ItemName = "Item 2", Amount = 1 });
            first.Stock.Add(new VendorStockRecord { ItemName = "Item 2", Amount = 1 });
            rows = VendorLedgerManager.SortRestock(VendorLedgerManager.GroupRestock(vendors, null, ""), VendorLedgerColumn.Stock, descending).ToArray();
            Assert.Equal(descending ? new[] { 2, 1 } : new[] { 1, 2 }, rows.Take(2).Select(r => r.Stock.Listings));
            Assert.All(rows.Skip(2), r => Assert.Equal((0, 0L, false), r.Stock));
        }

        [Fact]
        public void Grouped_restock_keeps_different_names_and_bundle_sizes_separate()
        {
            var first = Vendor("First"); var second = Vendor("Second");
            first.Sales.Add(new VendorSaleRecord { ItemName = "10 pearls" });
            second.Sales.Add(new VendorSaleRecord { ItemName = "100 pearls" });
            first.Sales.Add(new VendorSaleRecord { ItemName = "Ring of Quality" });
            var rows = VendorLedgerManager.GroupRestock(new[] { first, second }, null, "").ToArray();
            Assert.Equal(4, rows.Length);
            Assert.Equal(2L, rows.Single(r => r.ItemName == "Ring").Sales);
            Assert.Equal(1L, rows.Single(r => r.ItemName == "10 pearls").Sales);
            Assert.Equal(1L, rows.Single(r => r.ItemName == "100 pearls").Sales);
            Assert.Equal(1L, rows.Single(r => r.ItemName == "Ring of Quality").Sales);
        }

        [Fact]
        public void Grouped_price_history_uses_matching_saved_sales_and_keeps_vendor_and_properties()
        {
            var first = Vendor("First"); var second = Vendor("Second"); var vendors = new[] { first, second };
            first.Sales[0].Price = 100;
            second.Sales[0].Price = 300; second.Sales[0].ItemName = "ring";
            second.Sales[0].PropertyText = "Luck 100";
            first.Sales.Add(new VendorSaleRecord { ItemName = "Ring", Price = 200 });
            first.Sales.Add(new VendorSaleRecord { ItemName = "Ring of Quality", Price = 999 });
            var group = Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "").Where(g => string.Equals(g.ItemName, "Ring", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal((3, (long?)100, (decimal?)200, (long?)300), group.Prices);
            Assert.Equal(3, group.PriceHistory.Length);
            var saved = Assert.Single(group.PriceHistory.Where(r => r.Vendor == second));
            Assert.Same(second.Sales[0], saved.Sale); Assert.Equal("Luck 100", saved.Sale.PropertyText);
            Assert.Equal(2, Assert.Single(VendorLedgerManager.GroupRestock(vendors, first, "").Where(g => string.Equals(g.ItemName, "Ring", StringComparison.OrdinalIgnoreCase))).Prices.Count);
            VendorLedgerManager.RemoveRestock(second, "Ring");
            group = Assert.Single(VendorLedgerManager.GroupRestock(vendors, null, "").Where(g => string.Equals(g.ItemName, "Ring", StringComparison.OrdinalIgnoreCase)));
            Assert.Equal((2, (long?)100, (decimal?)150, (long?)200), group.Prices);
            Assert.All(group.PriceHistory, r => Assert.Same(first, r.Vendor));
            Assert.Equal(3, first.Sales.Count); Assert.Single(second.Sales);
        }

        [Fact]
        public void Price_summary_distinguishes_no_sales_from_zero_and_avoids_median_overflow()
        {
            Assert.Equal((0, (long?)null, (decimal?)null, (long?)null), VendorLedgerManager.SalePrices(Array.Empty<VendorSaleRecord>()));
            Assert.Equal((1, (long?)0, (decimal?)0, (long?)0), VendorLedgerManager.SalePrices(new[] { new VendorSaleRecord { Price = 0 } }));
            var sales = new[] { new VendorSaleRecord { Price = long.MaxValue }, new VendorSaleRecord { Price = long.MaxValue - 1 } };
            var summary = VendorLedgerManager.SalePrices(sales);
            Assert.Equal((decimal)long.MaxValue - 0.5m, summary.Median.Value);
            Assert.Equal(long.MaxValue, sales[0].Price); Assert.Equal(long.MaxValue - 1, sales[1].Price);
            Assert.Equal("No sales", VendorLedgerGump.RestockPrice(null));
            Assert.Equal("0", VendorLedgerGump.RestockPrice(0));
            Assert.Equal("300,000.5", VendorLedgerGump.RestockPrice(300000.5m));
        }

        [Fact]
        public void Requerying_restock_reflects_new_sale_prices_without_modifying_saved_rows()
        {
            var vendor = Vendor(); var original = vendor.Sales[0];
            var before = Assert.Single(VendorLedgerManager.GroupRestock(new[] { vendor }, null, "Ring"));
            Assert.Equal((decimal?)100, before.Prices.Median);
            vendor.Sales.Add(new VendorSaleRecord { ItemName = "Ring", Price = 200 });
            var after = Assert.Single(VendorLedgerManager.GroupRestock(new[] { vendor }, null, "Ring"));
            Assert.Equal((decimal?)150, after.Prices.Median); Assert.Equal(2, after.PriceHistory.Length);
            Assert.Same(original, after.PriceHistory[0].Sale); Assert.Equal(100, original.Price);
            Assert.Equal((decimal?)100, before.Prices.Median);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Grouped_price_sort_uses_numeric_medians_and_keeps_no_sales_last(bool descending)
        {
            var vendor = Vendor(); vendor.Sales.Clear();
            vendor.Stock.Add(new VendorStockRecord { ItemName = "No sales", Amount = 1 });
            foreach (int price in new[] { 10, 2, 100, 0 })
                vendor.Sales.Add(new VendorSaleRecord { ItemName = "Item " + price, Price = price });
            var rows = VendorLedgerManager.SortRestock(VendorLedgerManager.GroupRestock(new[] { vendor }, null, ""), VendorLedgerColumn.Price, descending).ToArray();
            Assert.Equal(descending ? new decimal?[] { 100, 10, 2, 0, null } : new decimal?[] { 0, 2, 10, 100, null }, rows.Select(r => r.Prices.Median));
            Assert.Equal("No sales", rows.Last().ItemName);
        }

        [Fact]
        public void Profile_round_trip_preserves_removed_items_and_capture_coverage()
        {
            var profile = new Profile(); var vendor = Vendor();
            vendor.StockNote = "1 unopened display bags; 0 item names pending.";
            VendorLedgerManager.RemoveRestock(vendor, "Ring"); profile.VendorLedger.Add(vendor);
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile).VendorLedger[0];
            Assert.True(restored.StockComplete); Assert.Contains("Ring", restored.RestockHiddenNames);
            Assert.Equal(vendor.StockNote, restored.StockNote);
            Assert.Single(restored.Sales);
            Assert.Empty(VendorLedgerManager.RestockRows(new[] { restored }, null, ""));
            var legacy = new VendorLedgerRecord();
            Assert.False(legacy.StockComplete); Assert.Empty(legacy.RestockHiddenNames);
        }
    }
}

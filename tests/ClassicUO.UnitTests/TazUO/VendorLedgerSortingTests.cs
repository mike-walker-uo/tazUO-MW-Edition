using System;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class VendorLedgerSortingTests
    {
        private static VendorLedgerRecord[] Vendors() => new[]
        {
            new VendorLedgerRecord { ShopName = "zeta", Sales = { new VendorSaleRecord {
                ItemName = "Ring", Buyer = "Beta", Price = 10, ListDate = "12/9", SellDate = "3/3" } },
                Stock = { new VendorStockRecord { ItemName = "Ring", Amount = 10, Price = 10,
                    FirstSeenUtcTicks = 100, LastSeenUtcTicks = 300 } } },
            new VendorLedgerRecord { ShopName = "Alpha", Sales = { new VendorSaleRecord {
                ItemName = "amulet", Buyer = "zoe", Price = 2, ListDate = "2/11", SellDate = "10/2" } },
                Stock = { new VendorStockRecord { ItemName = "amulet", Amount = 2, Price = 2,
                    FirstSeenUtcTicks = 200, LastSeenUtcTicks = 100 } } },
            new VendorLedgerRecord { ShopName = "beta", Sales = { new VendorSaleRecord {
                ItemName = "pearl", Buyer = "amy", Price = 100, ListDate = "10/2", SellDate = "1/11" } },
                Stock = { new VendorStockRecord { ItemName = "pearl", Amount = 100, Price = 100,
                    FirstSeenUtcTicks = 300, LastSeenUtcTicks = 200 } } }
        };

        [Theory]
        [InlineData((int)VendorLedgerColumn.Shop, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Item, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Buyer, 2, 0, 1)]
        [InlineData((int)VendorLedgerColumn.Price, 1, 0, 2)]
        [InlineData((int)VendorLedgerColumn.Listed, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Sold, 2, 0, 1)]
        public void History_columns_sort_both_directions_without_changing_captured_order(int column, int first, int second, int third)
        {
            var vendors = Vendors();
            var rows = VendorLedgerManager.SalesRows(vendors, null, "").ToArray();
            var expected = new[] { first, second, third }.Select(i => vendors[i]);
            Assert.Equal(expected, VendorLedgerManager.SortSales(rows, (VendorLedgerColumn)column, false).Select(r => r.Vendor));
            Assert.Equal(expected.Reverse(), VendorLedgerManager.SortSales(rows, (VendorLedgerColumn)column, true).Select(r => r.Vendor));
            Assert.Equal(vendors, rows.Select(r => r.Vendor));
            Assert.Equal(rows, VendorLedgerManager.SortSales(rows, VendorLedgerColumn.Received, false));
        }

        [Theory]
        [InlineData((int)VendorLedgerColumn.Shop, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Item, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Amount, 1, 0, 2)]
        [InlineData((int)VendorLedgerColumn.Price, 1, 0, 2)]
        [InlineData((int)VendorLedgerColumn.FirstSeen, 0, 1, 2)]
        [InlineData((int)VendorLedgerColumn.LastSeen, 1, 2, 0)]
        public void Stock_columns_sort_amount_price_and_observation_times_independently(int column, int first, int second, int third)
        {
            var vendors = Vendors();
            var rows = vendors.Select(v => (Vendor: v, Stock: v.Stock[0])).ToArray();
            var expected = new[] { first, second, third }.Select(i => vendors[i]);
            Assert.Equal(expected, VendorLedgerManager.SortStock(rows, (VendorLedgerColumn)column, false).Select(r => r.Vendor));
            Assert.Equal(expected.Reverse(), VendorLedgerManager.SortStock(rows, (VendorLedgerColumn)column, true).Select(r => r.Vendor));
            Assert.Equal(vendors, rows.Select(r => r.Vendor));
        }

        [Theory]
        [InlineData((int)VendorLedgerColumn.Shop, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Item, 1, 2, 0)]
        [InlineData((int)VendorLedgerColumn.Sales, 1, 0, 2)]
        public void Restock_text_and_sales_sort_both_directions_without_mutating_rows(int column, int first, int second, int third)
        {
            var vendors = Vendors();
            var counts = new[] { 10, 2, 100 };
            var rows = vendors.Select((v, i) => (Vendor: v, ItemName: v.Sales[0].ItemName, Sales: counts[i])).ToArray();
            var expected = new[] { first, second, third }.Select(i => vendors[i]);
            Assert.Equal(expected, VendorLedgerManager.SortRestock(rows, (VendorLedgerColumn)column, false).Select(r => r.Vendor));
            Assert.Equal(expected.Reverse(), VendorLedgerManager.SortRestock(rows, (VendorLedgerColumn)column, true).Select(r => r.Vendor));
            Assert.Equal(vendors, rows.Select(r => r.Vendor));
            Assert.Equal(rows, VendorLedgerManager.SortRestock(rows, VendorLedgerColumn.Received, false));
            Assert.All(vendors, v => Assert.Single(v.Sales));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Restock_stock_sorts_listing_counts_numerically_with_unknown_last(bool descending)
        {
            var vendors = Enumerable.Range(0, 5).Select(_ => new VendorLedgerRecord {
                StockComplete = true, LastStockUtcTicks = 100 }).ToArray();
            var counts = new[] { 0, 10, 2, 0, 1 };
            for (int i = 0; i < vendors.Length; i++)
                for (int j = 0; j < counts[i]; j++)
                    vendors[i].Stock.Add(new VendorStockRecord { ItemName = "Ring", Amount = 50 });
            vendors[3].StockComplete = false; // Unknown, not a confirmed zero.
            vendors[4].StockComplete = false; // Observed lower bound: 1+ seen.
            var rows = vendors.Select(v => (Vendor: v, ItemName: "Ring", Sales: 1)).ToArray();
            var expected = descending ? new[] { 1, 2, 4, 0, 3 } : new[] { 0, 4, 2, 1, 3 };
            Assert.Equal(expected.Select(i => vendors[i]),
                VendorLedgerManager.SortRestock(rows, VendorLedgerColumn.Stock, descending).Select(r => r.Vendor));
            Assert.Equal(counts, vendors.Select(v => v.Stock.Count));
        }

        [Fact]
        public void Restock_sort_respects_search_scope_removed_items_and_global_pagination()
        {
            var vendors = Enumerable.Range(1, 25).Reverse().Select(i => new VendorLedgerRecord {
                ShopName = "Shop " + i,
                Sales = Enumerable.Range(0, i).Select(_ => new VendorSaleRecord { ItemName = "Ring" }).ToList()
            }).ToArray();
            vendors[0].Sales.Add(new VendorSaleRecord { ItemName = "Pearl" });
            VendorLedgerManager.RemoveRestock(vendors[24], "Ring");
            var filtered = VendorLedgerManager.RestockRows(vendors, null, "ring");
            var sorted = VendorLedgerManager.SortRestock(filtered, VendorLedgerColumn.Sales, false).ToArray();
            Assert.Equal(Enumerable.Range(2, 24), sorted.Select(r => r.Sales));
            Assert.Equal(Enumerable.Range(12, 10), sorted.Skip(10).Take(10).Select(r => r.Sales));
            var selected = VendorLedgerManager.SortRestock(
                VendorLedgerManager.RestockRows(vendors, vendors[0], "ring"), VendorLedgerColumn.Sales, true);
            Assert.Same(vendors[0], Assert.Single(selected).Vendor);
            Assert.Single(vendors[0].Sales.Where(s => s.ItemName == "Pearl"));
            Assert.Contains("Ring", vendors[24].RestockHiddenNames);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Unknown_stock_prices_and_dates_stay_last_in_either_direction(bool descending)
        {
            var vendor = new VendorLedgerRecord();
            var known = new VendorStockRecord { Price = 0, LastSeenUtcTicks = 100 };
            var unknown = new VendorStockRecord();
            var invalid = new VendorStockRecord { LastSeenUtcTicks = DateTime.MaxValue.Ticks + 1 };
            var rows = new[] { (Vendor: vendor, Stock: unknown), (Vendor: vendor, Stock: invalid), (Vendor: vendor, Stock: known) };
            Assert.Same(known, VendorLedgerManager.SortStock(rows, VendorLedgerColumn.Price, descending).First().Stock);
            Assert.Equal(new[] { known, unknown, invalid },
                VendorLedgerManager.SortStock(rows, VendorLedgerColumn.LastSeen, descending).Select(r => r.Stock));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Sale_dates_use_calendar_order_keep_unknown_last_and_accept_leap_day(bool descending)
        {
            var vendor = new VendorLedgerRecord();
            var dates = new[] { "10/2", "unknown", "2/29", "1/10", "", "2/31" };
            var rows = dates.Select(d => (Vendor: vendor, Sale: new VendorSaleRecord { ListDate = d })).ToArray();
            var known = descending ? new[] { "10/2", "2/29", "1/10" } : new[] { "1/10", "2/29", "10/2" };
            Assert.Equal(known.Concat(new[] { "unknown", "", "2/31" }),
                VendorLedgerManager.SortSales(rows, VendorLedgerColumn.Listed, descending).Select(r => r.Sale.ListDate));
        }

        [Fact]
        public void Full_sale_dates_preserve_year_and_month_day_is_independent_of_client_culture()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var vendor = new VendorLedgerRecord();
                var dates = new[] { "2026-01-02", "12/31/2025", "1/10", "2/1" };
                var rows = dates.Select(d => (Vendor: vendor, Sale: new VendorSaleRecord { SellDate = d }));
                Assert.Equal(new[] { "1/10", "2/1", "12/31/2025", "2026-01-02" },
                    VendorLedgerManager.SortSales(rows, VendorLedgerColumn.Sold, false).Select(r => r.Sale.SellDate));
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }

        [Fact]
        public void Filtering_and_scope_apply_before_sorting_and_pages_use_the_full_sorted_set()
        {
            var first = new VendorLedgerRecord { ShopName = "First" };
            var second = new VendorLedgerRecord { ShopName = "Second" };
            for (int i = 25; i > 0; i--)
                (i % 2 == 0 ? first : second).Sales.Add(new VendorSaleRecord { ItemName = "Ring", Price = i });
            first.Sales.Add(new VendorSaleRecord { ItemName = "Pearl", Price = 0 });
            var vendors = new[] { first, second };
            var sorted = VendorLedgerManager.SortSales(VendorLedgerManager.SalesRows(vendors, null, "ring"), VendorLedgerColumn.Price, false).ToArray();
            Assert.Equal(Enumerable.Range(1, 25).Select(i => (long)i), sorted.Select(r => r.Sale.Price));
            Assert.Equal(Enumerable.Range(11, 10).Select(i => (long)i), sorted.Skip(10).Take(10).Select(r => r.Sale.Price));
            var selected = VendorLedgerManager.SortSales(VendorLedgerManager.SalesRows(vendors, first, "ring"), VendorLedgerColumn.Price, true);
            Assert.All(selected, r => Assert.Same(first, r.Vendor));
            Assert.Equal(Enumerable.Range(1, 12).Select(i => (long)i * 2).Reverse(), selected.Select(r => r.Sale.Price));
        }

        [Fact]
        public void Stock_sort_preserves_age_search_and_selected_vendor_filters()
        {
            var vendors = Vendors();
            long now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc).Ticks;
            vendors[0].Stock[0].FirstSeenUtcTicks = now - 8 * TimeSpan.TicksPerDay;
            vendors[1].Stock[0].FirstSeenUtcTicks = now - 6 * TimeSpan.TicksPerDay;
            vendors[2].Stock[0].FirstSeenUtcTicks = now - 10 * TimeSpan.TicksPerDay;
            var rows = VendorLedgerManager.StaleStockRows(vendors, null, "", 7, now);
            Assert.Equal(new[] { vendors[2], vendors[0] },
                VendorLedgerManager.SortStock(rows, VendorLedgerColumn.Amount, true).Select(r => r.Vendor));
            Assert.Same(vendors[0], Assert.Single(VendorLedgerManager.SortStock(
                VendorLedgerManager.StaleStockRows(vendors, null, "ring", 7, now), VendorLedgerColumn.Price, false)).Vendor);
            Assert.Empty(VendorLedgerManager.SortStock(
                VendorLedgerManager.StaleStockRows(vendors, vendors[1], "", 7, now), VendorLedgerColumn.FirstSeen, false));
            Assert.All(vendors, v => Assert.Single(v.Stock));
        }

        [Fact]
        public void Equal_keys_preserve_duplicate_sale_rows_and_their_order()
        {
            var vendor = new VendorLedgerRecord();
            var first = new VendorSaleRecord { ItemName = "Ring", Price = long.MaxValue };
            var second = new VendorSaleRecord { ItemName = "ring", Price = long.MaxValue };
            var rows = new[] { (Vendor: vendor, Sale: first), (Vendor: vendor, Sale: second) };
            foreach (bool descending in new[] { false, true })
            {
                Assert.Equal(new[] { first, second }, VendorLedgerManager.SortSales(rows, VendorLedgerColumn.Item, descending).Select(r => r.Sale));
                Assert.Equal(new[] { first, second }, VendorLedgerManager.SortSales(rows, VendorLedgerColumn.Price, descending).Select(r => r.Sale));
            }
        }
    }
}

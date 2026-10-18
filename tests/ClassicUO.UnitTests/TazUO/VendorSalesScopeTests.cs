using System;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class VendorSalesScopeTests
    {
        private readonly VendorLedgerRecord[] _vendors =
        {
            Vendor("Crafting Finest", "Ring", "Satori", 100, "10/14"),
            Vendor("Hunting Treasures", "Pearl", "Ners", 200, "10/15"),
            Vendor("Shop Not Yet Named", "Bracelet", "Ners", 300, "10/16")
        };

        [Fact]
        public void No_selection_combines_all_75_sales_without_deduplicating_identical_transactions()
        {
            var rows = VendorLedgerManager.SalesRows(_vendors, null, "").ToList();
            Assert.Equal(75, rows.Count);
            Assert.Equal(15000m, VendorLedgerManager.Gross(rows.Select(r => r.Sale)));
            Assert.Equal(3, rows.Select(r => r.Vendor).Distinct().Count());
            Assert.All(_vendors, v => Assert.Equal(25, v.Sales.Count));
        }

        [Fact]
        public void Selected_vendor_limits_both_history_rows_and_summary_totals()
        {
            var rows = VendorLedgerManager.SalesRows(_vendors, _vendors[1], "").ToList();
            Assert.Equal(25, rows.Count);
            Assert.All(rows, r => Assert.Same(_vendors[1], r.Vendor));
            Assert.Equal(5000m, VendorLedgerManager.Gross(rows.Select(r => r.Sale)));
        }

        [Theory]
        [InlineData(" hunting ", 25)]
        [InlineData("Ners", 50)]
        [InlineData("pearl", 25)]
        [InlineData("300", 25)]
        [InlineData("10/16", 25)]
        [InlineData("missing", 0)]
        public void Combined_find_matches_shop_or_existing_sale_fields(string query, int expected)
            => Assert.Equal(expected, VendorLedgerManager.SalesRows(_vendors, null, query).Count());

        [Fact]
        public void No_vendors_produces_empty_history_and_zero_gross()
        {
            var rows = VendorLedgerManager.SalesRows(new VendorLedgerRecord[0], null, "").ToList();
            Assert.Empty(rows);
            Assert.Equal(0m, VendorLedgerManager.Gross(rows.Select(r => r.Sale)));
        }

        [Fact]
        public void Combined_restock_keeps_same_named_items_and_checkmarks_separate_by_vendor()
        {
            foreach (var sale in _vendors[1].Sales) sale.ItemName = "Ring";
            var rows = VendorLedgerManager.RestockRows(_vendors, null, "Ring").ToList();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(25, r.Sales));
            var second = rows.Single(r => ReferenceEquals(r.Vendor, _vendors[1]));
            second.Vendor.RestockedNames.Add(second.ItemName);
            Assert.Contains("Ring", _vendors[1].RestockedNames);
            Assert.Empty(_vendors[0].RestockedNames);
        }

        [Fact]
        public void Restock_supports_shop_filter_and_selected_vendor_scope()
        {
            Assert.Same(_vendors[1], Assert.Single(VendorLedgerManager.RestockRows(_vendors, null, "hunting")).Vendor);
            Assert.Same(_vendors[2], Assert.Single(VendorLedgerManager.RestockRows(_vendors, _vendors[2], "")).Vendor);
            Assert.Empty(VendorLedgerManager.RestockRows(_vendors, _vendors[0], "Pearl"));
        }

        [Fact]
        public void Combined_stale_stock_includes_age_boundary_but_excludes_recent_unknown_and_future_dates()
        {
            long now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc).Ticks;
            _vendors[0].Stock.Add(new VendorStockRecord { ItemName = "Ring", FirstSeenUtcTicks = now - 8 * TimeSpan.TicksPerDay });
            _vendors[1].Stock.Add(new VendorStockRecord { ItemName = "Ring", FirstSeenUtcTicks = now - 10 * TimeSpan.TicksPerDay });
            _vendors[2].Stock.Add(new VendorStockRecord { ItemName = "Boundary", FirstSeenUtcTicks = now - 7 * TimeSpan.TicksPerDay });
            _vendors[2].Stock.Add(new VendorStockRecord { ItemName = "Recent", FirstSeenUtcTicks = now - TimeSpan.TicksPerDay });
            _vendors[2].Stock.Add(new VendorStockRecord { ItemName = "Unknown" });
            _vendors[2].Stock.Add(new VendorStockRecord { ItemName = "Future", FirstSeenUtcTicks = now + TimeSpan.TicksPerDay });

            var rows = VendorLedgerManager.StaleStockRows(_vendors, null, "", 7, now).ToList();
            Assert.Equal(3, rows.Count);
            Assert.Same(_vendors[1], rows[0].Vendor);
            Assert.Equal("Boundary", rows[2].Stock.ItemName);
            Assert.Equal(2, VendorLedgerManager.StaleStockRows(_vendors, null, "Ring", 7, now).Count());
            Assert.Empty(VendorLedgerManager.StaleStockRows(_vendors, null, "", 14, now));
            Assert.Equal(4, _vendors[2].Stock.Count);
        }

        [Fact]
        public void Stale_stock_supports_shop_filter_and_selected_vendor_scope()
        {
            long now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc).Ticks;
            foreach (var vendor in _vendors) vendor.Stock.Add(new VendorStockRecord { ItemName = "Ring", FirstSeenUtcTicks = now - 8 * TimeSpan.TicksPerDay });
            Assert.Same(_vendors[1], Assert.Single(VendorLedgerManager.StaleStockRows(_vendors, null, "hunting", 7, now)).Vendor);
            Assert.Same(_vendors[2], Assert.Single(VendorLedgerManager.StaleStockRows(_vendors, _vendors[2], "Ring", 7, now)).Vendor);
            Assert.Empty(VendorLedgerManager.StaleStockRows(new VendorLedgerRecord[0], null, "", 7, now));
        }

        private static VendorLedgerRecord Vendor(string shop, string item, string buyer, long price, string date)
            => new VendorLedgerRecord { ShopName = shop, LastHistoryUtcTicks = 100,
                Sales = Enumerable.Range(0, 25).Select(_ => new VendorSaleRecord {
                    ItemName = item, Buyer = buyer, Price = price, SellDate = date }).ToList() };
    }
}

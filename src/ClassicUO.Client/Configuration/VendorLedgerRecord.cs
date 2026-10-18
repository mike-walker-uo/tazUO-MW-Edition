using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClassicUO.Configuration
{
    public sealed class VendorLedgerRecord
    {
        public string Key { get; set; } = string.Empty;
        public uint VendorSerial { get; set; }
        public uint GoodsSerial { get; set; }
        public bool OwnerVerified { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public long? GoldHeld { get; set; }
        public long LastObservedUtcTicks { get; set; }
        public long LastHistoryUtcTicks { get; set; }
        public long LastStockUtcTicks { get; set; }
        public List<VendorSaleRecord> Sales { get; set; } = new List<VendorSaleRecord>();
        public List<VendorStockRecord> Stock { get; set; } = new List<VendorStockRecord>();
        public List<string> RestockedNames { get; set; } = new List<string>();
    }

    public sealed class VendorSaleRecord
    {
        public string ItemName { get; set; } = string.Empty;
        public string Buyer { get; set; } = string.Empty;
        public long Price { get; set; }
        public string ListDate { get; set; } = string.Empty;
        public string SellDate { get; set; } = string.Empty;
    }

    public sealed class VendorStockRecord
    {
        public uint Serial { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Amount { get; set; }
        public long? Price { get; set; }
        public long FirstSeenUtcTicks { get; set; }
        public long LastSeenUtcTicks { get; set; }
    }

    public sealed class VendorListingTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class VendorWatchItem
    {
        public string Query { get; set; } = string.Empty;
        public List<VendorMarketScan> Scans { get; set; } = new List<VendorMarketScan>();
        public bool PriceAlertEnabled { get; set; }
        public decimal? AlertBelowUnitPrice { get; set; }
        public string AlertVariantKey { get; set; } = string.Empty;
        public bool PriceAlertTriggered { get; set; }
        public long LastAlertScanUtcTicks { get; set; }
    }

    public sealed class VendorMarketScan
    {
        public long ScanUtcTicks { get; set; }
        public bool Complete { get; set; }
        public int Pages { get; set; }
        public string Note { get; set; } = string.Empty;
        public List<VendorMarketListing> Listings { get; set; } = new List<VendorMarketListing>();
    }

    public sealed class VendorMarketListing
    {
        public uint Serial { get; set; }
        public ushort? ItemGraphic { get; set; }
        public ushort? ItemHue { get; set; }
        public bool PropertiesKnown { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int? Amount { get; set; }
        public long? Price { get; set; }
        public string ListDate { get; set; } = string.Empty;
        public string Facet { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public decimal? QuotedUnitPrice { get; set; }
        [JsonIgnore] public decimal? UnitPrice => QuotedUnitPrice ?? (Amount.HasValue && Amount.Value > 0 && Price.HasValue ? (decimal?)Price.Value / Amount.Value : null);
    }
}

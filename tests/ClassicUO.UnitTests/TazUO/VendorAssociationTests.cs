using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class VendorAssociationTests : IDisposable
    {
        private const uint GumpSerial = 0x00E40101, VendorSerial = 0x00E40102;
        private readonly Profile _previousProfile = ProfileManager.CurrentProfile;
        private readonly PlayerMobile _previousPlayer = World.Player;
        private readonly Mobile _previousCollision = World.Mobiles.Get(GumpSerial);
        private readonly Mobile _previousVendor = World.Mobiles.Get(VendorSerial);
        private readonly Profile _profile = new Profile();

        public VendorAssociationTests()
        {
            ClientInteractionObserver.Reset();
            VendorRefreshManager.Reset(); VendorLedgerManager.Reset();
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);
            var player = (PlayerMobile)FormatterServices.GetUninitializedObject(typeof(PlayerMobile));
            player.Serial = 0x00E40100;
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, player);
            World.Mobiles[GumpSerial] = Mobile(GumpSerial);
            World.Mobiles[VendorSerial] = Mobile(VendorSerial);
        }

        [Fact]
        public void Owner_reply_updates_targeted_record_when_gump_serial_collides_with_another_mobile()
        {
            var other = VendorLedgerManager.AddVendor(_profile, GumpSerial, "Other vendor");
            var requested = VendorLedgerManager.AddVendor(_profile, VendorSerial, "Baul");
            VendorLedgerManager.ObserveInteraction(VendorSerial);

            var observed = VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.Owner, OwnerCells());

            Assert.Same(requested, observed);
            Assert.Equal("Crafting Finest", requested.ShopName);
            Assert.Equal(23827772L, requested.GoldHeld);
            Assert.True(requested.OwnerVerified);
            Assert.False(other.OwnerVerified);
            Assert.Equal("Other vendor", other.ShopName);
            Assert.Equal(2, _profile.VendorLedger.Count);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Missing_or_expired_interaction_never_uses_a_colliding_gump_serial(bool expired)
        {
            var other = VendorLedgerManager.AddVendor(_profile, GumpSerial, "Other vendor");
            if (expired)
            {
                VendorLedgerManager.ObserveInteraction(VendorSerial);
                typeof(VendorLedgerManager).GetField("_lastInteraction", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, (long)Time.Ticks - 15001);
            }

            var observed = VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.Owner, OwnerCells());

            Assert.Equal(0u, observed.VendorSerial);
            Assert.NotSame(other, observed);
            Assert.False(other.OwnerVerified);
        }

        [Fact]
        public void Queued_outgoing_use_identifies_vendor_before_its_owner_reply_is_processed()
        {
            var mapProperty = typeof(World).GetProperty(nameof(World.Map));
            object previousMap = mapProperty.GetValue(null);
            try
            {
                mapProperty.SetValue(null, FormatterServices.GetUninitializedObject(mapProperty.PropertyType));
                var requested = VendorLedgerManager.AddVendor(_profile, VendorSerial, "Baul");
                ClientInteractionObserver.Observe(new byte[] { 0x06, 0x00, 0xE4, 0x01, 0x02 });
                ClientInteractionObserver.Pump();
                Assert.Same(requested, VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.Owner, OwnerCells()));
            }
            finally { mapProperty.SetValue(null, previousMap); }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Owner_reply_merges_unlinked_shop_and_mobile_rows_preserving_the_newer_history(bool unlinkedNewer)
        {
            var requested = VendorLedgerManager.AddVendor(_profile, VendorSerial, "Baul");
            requested.LastHistoryUtcTicks = 100;
            requested.Sales.Add(new VendorSaleRecord { ItemName = "Current", Price = 100 });
            requested.RestockedNames.Add("Current");
            var unlinked = new VendorLedgerRecord { Key = "shop:CRAFTING FINEST", ShopName = "Crafting Finest",
                LastHistoryUtcTicks = unlinkedNewer ? 200 : 50 };
            unlinked.Sales.Add(new VendorSaleRecord { ItemName = "Saved", Price = 200 });
            unlinked.RestockedNames.Add("Saved");
            _profile.VendorLedger.Add(unlinked);
            VendorLedgerManager.ObserveInteraction(VendorSerial);

            Assert.Same(requested, VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.Owner, OwnerCells()));
            Assert.Same(requested, Assert.Single(_profile.VendorLedger));
            Assert.Equal(unlinkedNewer ? "Saved" : "Current", Assert.Single(requested.Sales).ItemName);
            Assert.Equal(unlinkedNewer ? "Saved" : "Current", Assert.Single(requested.RestockedNames));
        }

        [Fact]
        public void Shared_shop_names_do_not_merge_distinct_bound_vendors_or_ambiguous_unlinked_data()
        {
            var other = VendorLedgerManager.AddVendor(_profile, GumpSerial, "Crafting Finest");
            var requested = VendorLedgerManager.AddVendor(_profile, VendorSerial, "Baul");
            var unlinked = new VendorLedgerRecord { Key = "history:CRAFTING FINEST", ShopName = "Crafting Finest" };
            _profile.VendorLedger.Add(unlinked);
            VendorLedgerManager.ObserveInteraction(VendorSerial);

            Assert.Same(requested, VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.Owner, OwnerCells()));
            Assert.Equal(3, _profile.VendorLedger.Count);
            Assert.Contains(other, _profile.VendorLedger); Assert.Contains(unlinked, _profile.VendorLedger);
        }

        [Fact]
        public void Removal_persists_only_remaining_records_and_keeps_world_mobiles_and_market_watches()
        {
            var other = VendorLedgerManager.AddVendor(_profile, GumpSerial, "Other vendor");
            var removed = VendorLedgerManager.AddVendor(_profile, VendorSerial, "Baul");
            removed.Sales.Add(new VendorSaleRecord { ItemName = "Ring", Price = 100 });
            removed.Stock.Add(new VendorStockRecord { Serial = 42, ItemName = "Ring" });
            removed.RestockedNames.Add("Ring");
            _profile.VendorWatchItems.Add(new VendorWatchItem { Query = "Ring" });
            VendorLedgerManager.ObserveInteraction(VendorSerial);
            VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.Owner, OwnerCells());
            int revision = VendorLedgerManager.Revision;

            Assert.False(VendorLedgerManager.RemoveVendor(new VendorLedgerRecord { Key = other.Key }));
            Assert.True(VendorLedgerManager.RemoveVendor(removed));
            Assert.False(VendorLedgerManager.RemoveVendor(removed));
            Assert.Equal(revision + 1, VendorLedgerManager.Revision);
            Assert.Same(other, Assert.Single(VendorLedgerManager.Records));
            Assert.NotNull(World.Mobiles.Get(VendorSerial));

            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(GumpSerial, Assert.Single(restored.VendorLedger).VendorSerial);
            Assert.Equal("Ring", Assert.Single(restored.VendorWatchItems).Query);
        }

        [Fact]
        public void Delayed_sale_properties_are_saved_once_and_do_not_follow_later_item_changes()
        {
            const uint itemSerial = 0x7FFEFA01;
            var cells = new List<VendorLedgerManager.Cell>
            {
                new VendorLedgerManager.Cell { Text = "Vendor Sales History", X = 0, Y = 10 },
                new VendorLedgerManager.Cell { Text = "Crafting Finest", X = 0, Y = 30 }
            };
            string[] headers = { "Item Name", "Buyer", "Sell Price", "List Date", "Sell Date" };
            string[] values = { "Ring", "Buyer", "75000", "05/12", "09/24" };
            for (int i = 0; i < 5; i++)
            {
                cells.Add(new VendorLedgerManager.Cell { Text = headers[i], X = i * 100, Y = 100 });
                cells.Add(new VendorLedgerManager.Cell { Text = values[i], X = i * 100, Y = 130,
                    Tooltip = i == 0 ? (object)itemSerial : null });
            }
            var vendor = VendorLedgerManager.ObserveGump(new Gump(GumpSerial, 123), VendorGumpKind.History, cells);
            var sale = Assert.Single(vendor.Sales);
            Assert.Equal(itemSerial, sale.PropertySerial);
            Assert.Equal("", sale.PropertyText);
            EventSink.InvokeOPLOnReceive(null, new OPLEventArgs(itemSerial, "Ring", "Fire Resist 15%"));
            Assert.Equal("Ring\nFire Resist 15%", sale.PropertyText);
            EventSink.InvokeOPLOnReceive(null, new OPLEventArgs(itemSerial, "Ring", "Fire Resist 20%"));
            Assert.Equal("Ring\nFire Resist 15%", sale.PropertyText);
        }

        [Fact]
        public void Saved_sales_use_current_item_comparison_colors_and_layer_rules()
        {
            var sale = new VendorSaleRecord { ItemName = "Ring", PropertyText = "Ring\nFire Resist 15%", PropertyLayer = (byte)Layer.Ring };
            Assert.Contains("/c[red]Fire Resist 15%", VendorLedgerManager.SaleTooltip(sale));
            _profile.ToolTipOverride_NewFormat[1] = "/c[#55AAFF]Fire Resist {1}%";
            _profile.ToolTipOverride_Layer[1] = (byte)TooltipLayers.Jewelry_Group;
            Assert.Contains("/c[#55AAFF]Fire Resist 15%", VendorLedgerManager.SaleTooltip(sale));
            Assert.Equal("Ring\nFire Resist 15%", sale.PropertyText);
        }

        [Theory]
        [InlineData("<basefont color=#FFFFFF>Arcane ring<BR>Swing Speed Increase 10%<br/>Poison Resist 15%<br />Luck 100")]
        [InlineData("<basefont color=#FFFFFF>Swing Speed Increase 10%<BR>Poison Resist 15%<BR>Luck 100")]
        [InlineData("Swing Speed Increase 10%\rPoison Resist 15%\rLuck 100")]
        [InlineData("Arcane ring\r\nSwing Speed Increase 10%\r\nPoison Resist 15%\r\nLuck 100")]
        [InlineData("/c[yellow]Arcane ring/cd\n/c[orange]Swing Speed Increase 10%/cd\nPoison Resist 15%\nLuck 100")]
        public void Vendor_tooltip_formats_preserve_name_all_values_and_configured_colors(string raw)
        {
            new ToolTipOverrideData(_profile.ToolTipOverride_SearchText.Count, "Swing Speed Increase",
                "/c[orange]Swing Speed Increase {1}%", -1, 100, -1, 100, (byte)TooltipLayers.Any).Save();
            var sale = new VendorSaleRecord { ItemName = "Arcane ring", PropertyText = raw };
            string formatted = VendorLedgerManager.SaleTooltip(sale);
            Assert.Contains("/c[yellow]Arcane ring", formatted);
            Assert.Contains("/c[orange]Swing Speed Increase 10%", formatted);
            Assert.Contains("/c[green]Poison Resist 15%", formatted);
            Assert.Contains("Luck 100", formatted);
            Assert.Equal(1, formatted.Split(new[] { "Arcane ring" }, StringSplitOptions.None).Length - 1);
            Assert.Equal(raw, sale.PropertyText);
        }

        [Theory]
        [InlineData("Swing Speed Increase 10%", "Swing Speed Increase 10%")]
        [InlineData("Poison Resist 15%", "Poison Resist 15%")]
        public void Single_stat_vendor_tooltips_are_not_formatted_as_item_names(string raw, string expected)
        {
            new ToolTipOverrideData(_profile.ToolTipOverride_SearchText.Count, "Swing Speed Increase",
                "/c[orange]Swing Speed Increase {1}%", -1, 100, -1, 100, (byte)TooltipLayers.Any).Save();
            string formatted = VendorLedgerManager.SaleTooltip(new VendorSaleRecord { ItemName = "Exquisite bracelet", PropertyText = raw });
            Assert.Contains("Exquisite bracelet", formatted);
            Assert.Contains(expected, formatted);
        }

        private static Mobile Mobile(uint serial)
        {
            var mobile = (Mobile)FormatterServices.GetUninitializedObject(typeof(Mobile));
            mobile.Serial = serial;
            return mobile;
        }

        private static List<VendorLedgerManager.Cell> OwnerCells() => new List<VendorLedgerManager.Cell>
        {
            new VendorLedgerManager.Cell { Text = "Shop Name:", X = 40, Y = 128 },
            new VendorLedgerManager.Cell { Text = "Crafting Finest", X = 140, Y = 128 },
            new VendorLedgerManager.Cell { Text = "Gold held in my account:", X = 40, Y = 102 },
            new VendorLedgerManager.Cell { Text = "23827772", X = 300, Y = 102 }
        };

        public void Dispose()
        {
            ClientInteractionObserver.Reset();
            VendorRefreshManager.Reset(); VendorLedgerManager.Reset();
            if (_previousCollision == null) World.Mobiles.Remove(GumpSerial); else World.Mobiles[GumpSerial] = _previousCollision;
            if (_previousVendor == null) World.Mobiles.Remove(VendorSerial); else World.Mobiles[VendorSerial] = _previousVendor;
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _previousProfile);
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _previousPlayer);
        }
    }
}

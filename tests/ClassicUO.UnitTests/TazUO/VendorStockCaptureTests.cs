using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Network;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class VendorStockCaptureTests : IDisposable
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly StaticTiles[] _tiles = TileDataLoader._staticData;
        private readonly Dictionary<uint, Item> _items = new Dictionary<uint, Item>();
        private readonly Dictionary<uint, ItemProperty> _oldProperties = new Dictionary<uint, ItemProperty>();
        private readonly Dictionary<uint, ItemProperty> _properties = (Dictionary<uint, ItemProperty>)typeof(ObjectPropertiesListManager)
            .GetField("_itemsProperties", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(World.OPL);
        private readonly List<uint> _requests = (List<uint>)typeof(PacketHandlers)
            .GetField("_clilocRequests", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(PacketHandlers.Handler);
        private readonly HashSet<uint> _oldRequests;
        private readonly VendorLedgerRecord _vendor = new VendorLedgerRecord { VendorSerial = 0x00EF1100, GoodsSerial = 0x4FEF1100 };
        private readonly Item _root;

        public VendorStockCaptureTests()
        {
            _oldRequests = new HashSet<uint>(_requests);
            VendorRefreshManager.Reset(); VendorLedgerManager.Reset();
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, new Profile());
            VendorLedgerManager.Records.Add(_vendor);
            TileDataLoader._staticData = new StaticTiles[0xA000];
            TileDataLoader._staticData[1] = new StaticTiles { Flags = TileFlag.Container, Name = "Bag" };
            _root = Item(_vendor.GoodsSerial, 1, _vendor.VendorSerial);
            _root.Layer = Layer.Backpack; _root.Opened = true;
        }

        [Theory]
        [InlineData(0x0EFA)] // spellbook
        [InlineData(0x22C5)] // runebook
        [InlineData(0x2259)] // bulk order book
        [InlineData(0x9C17)] // runic atlas
        public void Books_are_stock_items_and_do_not_require_opening_for_complete_stock(int graphic)
        {
            TileDataLoader._staticData[graphic] = new StaticTiles { Flags = TileFlag.Container };
            var book = Item(_root.Serial + 1, (ushort)graphic, _root.Serial);
            Properties(book, "Book", "");
            Assert.False(VendorLedgerManager.StockContainer(book));
            Assert.Null(NextBag());
            Capture();
            Assert.True(_vendor.StockComplete);
            Assert.Equal("Book", Assert.Single(_vendor.Stock).ItemName);
        }

        [Fact]
        public void Priced_bag_is_one_sale_lot_and_its_cached_contents_are_not_separate_stock()
        {
            var bag = Item(_root.Serial + 1, 1, _root.Serial);
            var contents = Item(_root.Serial + 2, 0, bag.Serial);
            Properties(bag, "Supply bag", "Price: 10,000 Gold Coins");
            Properties(contents, "Pearl", "");
            Assert.False(VendorLedgerManager.StockContainer(bag)); Assert.Null(NextBag());
            Capture();
            Assert.True(_vendor.StockComplete);
            Assert.Equal(10000L, Assert.Single(_vendor.Stock).Price);
        }

        [Fact]
        public void Actual_display_bags_remain_unknown_until_opened_including_empty_bags()
        {
            var bag = Item(_root.Serial + 1, 1, _root.Serial);
            Properties(bag, "Resources", "Not for sale");
            Assert.Same(bag, NextBag());
            Capture();
            Assert.False(_vendor.StockComplete); Assert.Contains("1 unopened display bags", _vendor.StockNote);
            bag.Opened = true;
            Capture();
            Assert.True(_vendor.StockComplete);
            Assert.True(VendorLedgerManager.RestockStock(_vendor, "Pearl").Complete);
        }

        [Fact]
        public void Late_names_schedule_recapture_and_resolve_unknown_stock_without_reopening_bags()
        {
            var pearl = Item(_root.Serial + 1, 0, _root.Serial); pearl.Amount = 10;
            Capture();
            Assert.False(_vendor.StockComplete); Assert.Contains("1 item names pending", _vendor.StockNote);
            if (World.ClientFeatures.TooltipsEnabled) Assert.Contains(pearl.Serial, _requests);
            Properties(pearl, "Pearl", "");
            EventSink.InvokeOPLOnReceive(null, new OPLEventArgs(pearl.Serial, "Pearl", ""));
            var dirty = (HashSet<uint>)typeof(VendorLedgerManager).GetField("_stockDirty", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.Contains(_root.Serial, dirty);
            Capture();
            Assert.True(_vendor.StockComplete);
            Assert.Equal((1, 10L, true), VendorLedgerManager.RestockStock(_vendor, "10 Pearl"));
        }

        private Item Item(uint serial, ushort graphic, uint container)
        {
            _items[serial] = World.Items.Get(serial);
            _properties.TryGetValue(serial, out var properties); _oldProperties[serial] = properties; _properties.Remove(serial);
            var item = (Item)FormatterServices.GetUninitializedObject(typeof(Item));
            item.Serial = serial; item.Graphic = graphic; item.Container = container; item.Amount = 1;
            World.Items[serial] = item;
            World.Items.Get(container)?.PushToBack(item);
            return item;
        }

        private void Properties(Item item, string name, string data) =>
            _properties[item.Serial] = new ItemProperty { Name = name, Data = data };
        private void Capture() => typeof(VendorLedgerManager).GetMethod("CaptureStock", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { _vendor });
        private Item NextBag() => (Item)typeof(VendorRefreshManager).GetMethod("FindBag", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { _root, new HashSet<uint>() });

        public void Dispose()
        {
            VendorRefreshManager.Reset(); VendorLedgerManager.Reset();
            foreach (var pair in _items)
                if (pair.Value == null) World.Items.Remove(pair.Key); else World.Items[pair.Key] = pair.Value;
            foreach (var pair in _oldProperties)
                if (pair.Value == null) _properties.Remove(pair.Key); else _properties[pair.Key] = pair.Value;
            // Capture queues property requests; remove only this fixture's serials.
            _requests.RemoveAll(s => _items.ContainsKey(s) && !_oldRequests.Contains(s));
            TileDataLoader._staticData = _tiles;
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class EightBacklogInventoryTests : IDisposable
    {
        private readonly PlayerMobile _player = World.Player;
        private readonly StaticTiles[] _tiles = TileDataLoader._staticData;
        private readonly List<Item> _items = new();
        public EightBacklogInventoryTests()
        {
            TestLogging.EnsureInitialized(); InventoryCounts.Reset();
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, FormatterServices.GetUninitializedObject(typeof(PlayerMobile)));
            World.Player.Serial = 1;
            TileDataLoader._staticData = new StaticTiles[4];
            TileDataLoader._staticData[0] = new StaticTiles { Name = "Backpack", Flags = TileFlag.Container };
            TileDataLoader._staticData[1] = new StaticTiles { Name = "Gold", Flags = TileFlag.Generic };
            TileDataLoader._staticData[2] = new StaticTiles { Name = "Blue Bag", Flags = TileFlag.Container };
            TileDataLoader._staticData[3] = new StaticTiles { Name = "Ring", Flags = TileFlag.Wearable };
        }
        private Item Add(uint offset, ushort graphic, uint container = 1, ushort amount = 1)
        {
            var item = new Item { Serial = 0x4000B800 + offset, Graphic = graphic, Container = container, Amount = amount };
            World.Items.Add(item.Serial, item); _items.Add(item); return item;
        }
        [Fact]
        public void Repeated_read_reuses_revision_and_nested_stack_updates_invalidate_ancestors()
        {
            var pack = Add(1, 0); var bag = Add(2, 2, pack.Serial); var gold = Add(3, 1, bag.Serial, 12);
            pack.Items = bag; bag.Items = gold;
            var snapshot = InventoryCounts.Get(pack); long revision = snapshot.Revision;
            Assert.Equal(12, snapshot.Count(1, 0)); Assert.Equal(2, snapshot.ItemCount);
            Assert.Same(snapshot, InventoryCounts.Get(pack)); Assert.Equal(revision, snapshot.Revision);
            gold.Amount = 25; EventSink.InvokeOnItemUpdated(gold);
            Assert.Equal(25, InventoryCounts.Get(pack).Count(1, 0)); Assert.True(snapshot.Revision > revision);
        }
        [Fact]
        public void Move_invalidates_old_and_new_parent_counts_without_rebuilding_unrelated_roots()
        {
            var first = Add(1, 0); var second = Add(2, 2); var third = Add(3, 2); var ring = Add(4, 3, first.Serial);
            first.Items = ring;
            InventoryCounts.Get(first); InventoryCounts.Get(second); long untouched = InventoryCounts.Get(third).Revision;
            World.RemoveItemFromContainer(ring); second.Items = ring; ring.Container = second.Serial;
            EventSink.InvokeOnItemUpdated(ring);
            Assert.Equal(0, InventoryCounts.Get(first).Count(3, 0)); Assert.Equal(1, InventoryCounts.Get(second).Count(3, 0));
            Assert.Equal(untouched, InventoryCounts.Get(third).Revision);
        }
        [Fact]
        public void Newly_observed_item_invalidates_existing_parent_snapshot()
        {
            var pack = Add(1, 0); Assert.Equal(0, InventoryCounts.Get(pack).ItemCount);
            var gold = Add(2, 1, pack.Serial, 7); pack.Items = gold; EventSink.InvokeOnItemCreated(gold);
            Assert.Equal(7, InventoryCounts.Get(pack).Count(1, 0));
        }
        [Fact]
        public void Raw_counter_amounts_and_restock_units_keep_distinct_semantics()
        {
            var pack = Add(1, 0); var ring = Add(2, 3, pack.Serial, 25); var gold = Add(3, 1, pack.Serial, 0);
            pack.Items = ring; ring.Next = gold;
            var counts = InventoryCounts.Get(pack);
            Assert.Equal(1, counts.Count(3, 0)); Assert.Equal(25, counts.Count(3, 0, rawAmounts: true));
            Assert.Equal(1, counts.Count(1, 0)); Assert.Equal(0, counts.Count(1, 0, rawAmounts: true));
        }
        [Fact]
        public void Hue_changes_do_not_leave_old_totals_or_affect_other_roots()
        {
            var pack = Add(1, 0); var gold = Add(2, 1, pack.Serial, 7); pack.Items = gold;
            InventoryCounts.Get(pack); gold.Hue = 0x35; EventSink.InvokeOnItemUpdated(gold);
            var counts = InventoryCounts.Get(pack);
            Assert.Equal(0, counts.Count(1, 0)); Assert.Equal(7, counts.Count(1, 0x35)); Assert.Equal(7, counts.Count(1, 0, anyHue: true));
        }
        [Fact]
        public void Cache_and_breadcrumbs_bound_corrupt_parent_and_sibling_cycles()
        {
            var pack = Add(1, 0); var bag = Add(2, 2, pack.Serial); var ring = Add(3, 3, bag.Serial);
            pack.Container = bag.Serial; pack.Items = bag; bag.Items = ring; ring.Next = ring;
            Assert.Equal(2, InventoryCounts.Get(pack).ItemCount);
            var path = ContainerBreadcrumbs.Build(bag.Serial);
            Assert.Equal(2, path.Count); Assert.Equal(2, path.Select(p => p.Serial).Distinct().Count());
        }
        [Fact]
        public void Breadcrumbs_use_known_parents_and_label_unknown_parent_as_last_known()
        {
            var pack = Add(1, 0); var bag = Add(2, 2, pack.Serial);
            Assert.Equal(new[] { "Backpack", "Blue Bag" }, ContainerBreadcrumbs.Build(bag.Serial).Select(p => p.Name));
            bag.Container = 0x4000CAFE;
            var path = ContainerBreadcrumbs.Build(bag.Serial);
            Assert.False(path[0].Available); Assert.Contains("last-known", path[0].Name); Assert.True(path[1].Available);
        }
        public void Dispose()
        {
            InventoryCounts.Reset();
            foreach (Item item in _items) World.Items.Remove(item.Serial);
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _player); TileDataLoader._staticData = _tiles;
        }
    }
}

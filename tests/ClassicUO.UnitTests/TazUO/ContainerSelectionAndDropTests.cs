using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ContainerSelectionAndDropTests : IDisposable
    {
        private readonly PlayerMobile _player = World.Player;
        private readonly Point _range = World.RangeSize;
        private readonly StaticTiles[] _tiles = TileDataLoader._staticData;
        private readonly Item[] _queued = MultiItemMoveGump.MoveItems.ToArray();
        private readonly ConcurrentDictionary<uint, byte> _selected = (ConcurrentDictionary<uint, byte>)typeof(MultiItemMoveGump)
            .GetField("_selected", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private readonly uint[] _selectedSerials;

        public ContainerSelectionAndDropTests()
        {
            TestLogging.EnsureInitialized();
            _selectedSerials = _selected.Keys.ToArray(); _selected.Clear();
            while (MultiItemMoveGump.MoveItems.TryDequeue(out _)) { }
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, FormatterServices.GetUninitializedObject(typeof(PlayerMobile)));
            World.Player.Serial = 1; World.RangeSize = new Point(20, 20);
            TileDataLoader._staticData = new StaticTiles[5];
            TileDataLoader._staticData[1] = new StaticTiles { Name = "Robe", Flags = TileFlag.Wearable, Layer = (byte)Layer.Robe };
            TileDataLoader._staticData[2] = new StaticTiles { Flags = TileFlag.Surface, Height = 10 };
            TileDataLoader._staticData[3] = new StaticTiles { Flags = TileFlag.Container };
            TileDataLoader._staticData[4] = new StaticTiles { Flags = TileFlag.Generic };
        }

        [Fact]
        public void Bulk_selection_deduplicates_across_hues_and_rechecks_container_membership()
        {
            var first = MakeItem(10, 1, 42); var second = MakeItem(11, 1, 42); second.Hue = 0x35;
            var moved = MakeItem(12, 1, 99);
            Assert.Equal(2, GridContainer.SelectForMultiMove(42, new[] { first, first, second, moved }, i => i.Graphic == 1));
            Assert.Equal(0, GridContainer.SelectForMultiMove(42, new[] { first, second }, i => i.Graphic == 1));
            Assert.Equal(new uint[] { 10, 11 }, MultiItemMoveGump.MoveItems.Select(item => item.Serial));
            Assert.False(MultiItemMoveGump.IsSelected(moved.Serial));
            Assert.Equal(42u, first.Container); // Selection queues a review; it does not move items.
        }

        [Fact]
        public void Layer_selection_excludes_items_with_unrelated_metadata()
        {
            var robe = MakeItem(10, 1, 42); var surface = MakeItem(11, 2, 42);
            Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { robe, surface },
                item => GridContainer.GridSlotManager.EquipmentLayerSortKey(item.ItemData) == (int)Layer.Robe));
            Assert.True(MultiItemMoveGump.IsSelected(robe.Serial)); Assert.False(MultiItemMoveGump.IsSelected(surface.Serial));
        }

        [Fact]
        public void Name_selection_uses_existing_display_names_case_insensitively()
        {
            var robe = MakeItem(10, 1, 42);
            Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { robe },
                item => string.Equals(GridContainer.GridSlotManager.GetItemName(item), "robe", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void Drop_resolver_matches_surface_height_and_existing_stack_destinations()
        {
            var surface = MakeItem(10, 2, GroundDropPreview.WorldContainer); surface.Z = 5;
            Assert.True(GroundDropPreview.TryResolve(surface, 1, out GroundDropLocation onSurface));
            Assert.Equal((sbyte)15, onSurface.Z); Assert.Equal(GroundDropPreview.WorldContainer, onSurface.Container);
            var stack = MakeItem(11, 4, GroundDropPreview.WorldContainer);
            Assert.True(GroundDropPreview.TryResolve(stack, 4, out GroundDropLocation ontoStack)); Assert.Equal(stack.Serial, ontoStack.Container);
            Assert.False(GroundDropPreview.TryResolve(stack, 1, out _));
            var container = MakeItem(12, 3, GroundDropPreview.WorldContainer);
            Assert.True(GroundDropPreview.TryResolve(container, 1, out GroundDropLocation intoContainer));
            Assert.Equal((ushort)0xFFFF, intoContainer.X); Assert.Equal(container.Serial, intoContainer.Container);
        }

        [Fact]
        public void Drop_resolver_preserves_land_Z_and_rejects_out_of_range_targets()
        {
            var land = (Land)FormatterServices.GetUninitializedObject(typeof(Land)); land.X = 20; land.Y = 20; land.Z = 9;
            Assert.True(GroundDropPreview.TryResolve(land, 1, out GroundDropLocation drop)); Assert.Equal((sbyte)9, drop.Z);
            land.X = 100; Assert.False(GroundDropPreview.TryResolve(land, 1, out _));
            Assert.False(GroundDropPreview.TryResolve(null, 1, out _));
        }

        [Theory]
        [InlineData(5, 255, true, 5)] [InlineData(5, 10, true, 15)] [InlineData(5, 10, false, 5)]
        public void Unknown_height_and_non_surfaces_do_not_raise_drop_Z(int z, int height, bool surface, int expected) =>
            Assert.Equal((sbyte)expected, GroundDropPreview.SurfaceZ((sbyte)z, (byte)height, surface));

        private static Item MakeItem(uint serial, ushort graphic, uint container) =>
            new Item { Serial = serial, Graphic = graphic, Container = container, X = 20, Y = 20, Amount = 1 };
        public void Dispose()
        {
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _player); World.RangeSize = _range;
            TileDataLoader._staticData = _tiles; _selected.Clear();
            foreach (uint serial in _selectedSerials) _selected.TryAdd(serial, 1);
            while (MultiItemMoveGump.MoveItems.TryDequeue(out _)) { }
            foreach (Item item in _queued) MultiItemMoveGump.MoveItems.Enqueue(item);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Assets;
using ClassicUO.Configuration;
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
        private readonly Dictionary<uint, ItemProperty> _oldSearchProperties = new();
        private readonly Dictionary<uint, ItemProperty> _searchProperties = (Dictionary<uint, ItemProperty>)typeof(ObjectPropertiesListManager)
            .GetField("_itemsProperties", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(World.OPL);

        private void SetSearchProperties(Item item, string data)
        {
            if (!_oldSearchProperties.ContainsKey(item.Serial))
            {
                _searchProperties.TryGetValue(item.Serial, out var old);
                _oldSearchProperties[item.Serial] = old;
            }
            _searchProperties[item.Serial] = new ItemProperty { Name = item.Name, Data = data };
        }

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
        public void Select_all_includes_every_container_item_without_recursing_into_bags()
        {
            Item[] contents = Enumerable.Range(100, 150).Select(i => MakeItem((uint)i, 1, 42)).ToArray();
            var nested = MakeItem(250, 1, contents[0].Serial);
            Assert.Equal(150, GridContainer.SelectForMultiMove(42, contents.Concat(new[] { nested }), _ => true));
            Assert.Equal(150, MultiItemMoveGump.MoveItems.Count);
            Assert.False(MultiItemMoveGump.IsSelected(nested.Serial));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Select_all_search_matches_in_hide_and_highlight_modes(int mode)
        {
            Profile previous = ProfileManager.CurrentProfile;
            var profileProperty = typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile));
            try
            {
                profileProperty.SetValue(null, new Profile { GridContainerSearchMode = mode });
                var first = MakeItem(10, 1, 42); first.Name = "Composite Bow";
                var second = MakeItem(11, 1, 42); second.Name = "Composite Bow"; second.Hue = 0x35;
                var other = MakeItem(12, 1, 42); other.Name = "Longbow";
                Assert.Equal(2, GridContainer.SelectForMultiMove(42, new[] { first, second, other }, _ => true, "COMP"));
                Assert.Equal(new uint[] { 10, 11 }, MultiItemMoveGump.MoveItems.Select(i => i.Serial));
                Assert.Equal(42u, first.Container);
            }
            finally { profileProperty.SetValue(null, previous); }
        }

        [Fact]
        public void Search_selection_uses_tooltip_properties_and_tile_names()
        {
            var properties = (Dictionary<uint, ItemProperty>)typeof(ObjectPropertiesListManager)
                .GetField("_itemsProperties", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(World.OPL);
            const uint serial = 0x4FFF0001;
            properties.TryGetValue(serial, out ItemProperty previous);
            try
            {
                properties[serial] = new ItemProperty { Name = "Composite Bow", Data = "Hit Fireball 50%" };
                var bow = MakeItem(serial, 1, 42);
                Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { bow }, _ => true, "fireball"));
                var robe = MakeItem(11, 1, 42);
                Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { robe }, _ => true, "ROBE"));
                Assert.Equal(0, GridContainer.SelectForMultiMove(42, new[] { bow, robe }, _ => true, "not present"));
            }
            finally
            {
                if (previous == null) properties.Remove(serial);
                else properties[serial] = previous;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Negated_selection_uses_whole_query_and_direct_contents_in_both_search_modes(int mode)
        {
            Profile previous = ProfileManager.CurrentProfile;
            var profileProperty = typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile));
            try
            {
                profileProperty.SetValue(null, new Profile { GridContainerSearchMode = mode });
                var bow = MakeItem(10, 1, 42); bow.Name = "Composite Bow";
                var sword = MakeItem(11, 1, 42); sword.Name = "Sword";
                var robe = MakeItem(12, 1, 42); robe.Name = "Robe";
                SetSearchProperties(robe, "");
                var nested = MakeItem(13, 1, 99); nested.Name = "Robe";
                Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { bow, sword, robe, robe, nested },
                    _ => true, "bow OR sword", negateSearch: true));
                Assert.Equal(new[] { robe }, MultiItemMoveGump.MoveItems.ToArray());
                Assert.False(MultiItemMoveGump.IsSelected(bow.Serial));
                Assert.False(MultiItemMoveGump.IsSelected(sword.Serial));
                Assert.False(MultiItemMoveGump.IsSelected(nested.Serial));
                Assert.Equal(42u, robe.Container);
            }
            finally { profileProperty.SetValue(null, previous); }
        }

        [Fact]
        public void Negated_selection_matches_tooltips_and_layer_filters()
        {
            var properties = (Dictionary<uint, ItemProperty>)typeof(ObjectPropertiesListManager)
                .GetField("_itemsProperties", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(World.OPL);
            const uint serial = 0x4FFF0002;
            properties.TryGetValue(serial, out ItemProperty previous);
            try
            {
                properties[serial] = new ItemProperty { Name = "Composite Bow", Data = "Undead Slayer\nCursed" };
                var cursed = MakeItem(serial, 1, 42);
                var clean = MakeItem(11, 1, 42); clean.Name = "Composite Bow";
                SetSearchProperties(clean, "");
                var otherLayer = MakeItem(12, 2, 42);
                Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { cursed, clean, otherLayer },
                    i => i.Graphic == 1, "cursed AND slayer", negateSearch: true));
                Assert.Equal(new[] { clean }, MultiItemMoveGump.MoveItems.ToArray());
                Assert.True(GridContainer.GridSlotManager.SearchItemNameAndProps("bow AND (slayer OR luck)", cursed));
                Assert.False(GridContainer.GridSlotManager.SearchItemNameAndProps("bow AND NOT cursed", cursed));
            }
            finally
            {
                if (previous == null) properties.Remove(serial);
                else properties[serial] = previous;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Invalid_search_cannot_select_items_even_when_negated(bool negate)
        {
            var item = MakeItem(10, 1, 42); item.Name = "Bow";
            Assert.Equal(0, GridContainer.SelectForMultiMove(42, new[] { item }, _ => true, "bow AND", negate));
            Assert.Empty(MultiItemMoveGump.MoveItems);
            Assert.Equal(0, GridContainer.SelectForMultiMove(42, new[] { item }, _ => true, "NOT (bow", negate));
        }

        [Fact]
        public void Negated_blank_search_selects_nothing()
        {
            var item = MakeItem(10, 1, 42);
            Assert.Equal(0, GridContainer.SelectForMultiMove(42, new[] { item }, _ => true, "  ", negateSearch: true));
            Assert.Empty(MultiItemMoveGump.MoveItems);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Pending_tooltips_are_excluded_from_both_selection_actions(bool nonMatches)
        {
            var bow = MakeItem(0x4FFF0010, 1, 42); bow.Name = "Composite Bow";
            Assert.Equal(ContainerSearchQuery.MatchState.Pending, ContainerSearchQuery.Parse("bow AND NOT cursed").Evaluate(bow));
            Assert.Equal(0, GridContainer.SelectForMultiMove(42, new[] { bow }, _ => true, "bow AND NOT cursed", nonMatches));
            Assert.Empty(MultiItemMoveGump.MoveItems);
        }

        [Fact]
        public void Tooltip_arrival_changes_pending_to_confirmed_result()
        {
            var bow = MakeItem(0x4FFF0011, 1, 42); bow.Name = "Composite Bow";
            var query = ContainerSearchQuery.Parse("bow AND NOT cursed");
            Assert.Equal(ContainerSearchQuery.MatchState.Pending, query.Evaluate(bow));
            SetSearchProperties(bow, "Undead Slayer");
            Assert.Equal(ContainerSearchQuery.MatchState.Match, query.Evaluate(bow));
            SetSearchProperties(bow, "Undead Slayer\nCursed");
            Assert.Equal(ContainerSearchQuery.MatchState.NoMatch, query.Evaluate(bow));
        }

        [Fact]
        public void Search_counts_include_pending_and_hidden_selected_items()
        {
            var bow = MakeItem(0x4FFF0012, 1, 42); bow.Name = "Composite Bow";
            var robe = MakeItem(0x4FFF0013, 1, 42); robe.Name = "Robe";
            var unknown = MakeItem(0x4FFF0014, 1, 42); unknown.Name = "Sword";
            SetSearchProperties(bow, "Undead Slayer"); SetSearchProperties(robe, "");
            var counts = GridContainer.SearchCounts(new[] { bow, robe, unknown },
                ContainerSearchQuery.Parse("bow AND NOT cursed"), serial => serial != bow.Serial);
            Assert.Equal(1, counts.Matches); Assert.Equal(3, counts.Total); Assert.Equal(1, counts.Pending);
            Assert.Equal(2, counts.Selected); Assert.Equal(2, counts.OutsideSearch);
        }

        [Fact]
        public void Replacing_selection_removes_hidden_items_but_preserves_running_moves()
        {
            var type = typeof(MultiItemMoveGump);
            var processing = type.GetField("processing", BindingFlags.Static | BindingFlags.NonPublic);
            var operation = type.GetField("_operation", BindingFlags.Static | BindingFlags.NonPublic);
            object previousProcessing = processing.GetValue(null), previousOperation = operation.GetValue(null);
            try
            {
                processing.SetValue(null, false); operation.SetValue(null, null);
                var old = MakeItem(10, 1, 99);
                MultiItemMoveGump.TrySelect(old);
                Assert.True(MultiItemMoveGump.TryClearSelection());
                var match = MakeItem(11, 1, 42); match.Name = "Composite Bow";
                Assert.Equal(1, GridContainer.SelectForMultiMove(42, new[] { match }, _ => true, "comp"));
                Assert.False(MultiItemMoveGump.IsSelected(old.Serial));
                Assert.Equal(new[] { match }, MultiItemMoveGump.MoveItems.ToArray());
                processing.SetValue(null, true);
                Assert.False(MultiItemMoveGump.TryClearSelection());
                processing.SetValue(null, false);
                operation.SetValue(null, new QueuedOperation("Multi Move") { Total = 1 });
                Assert.False(MultiItemMoveGump.TryClearSelection());
                Assert.True(MultiItemMoveGump.IsSelected(match.Serial));
            }
            finally { processing.SetValue(null, previousProcessing); operation.SetValue(null, previousOperation); }
        }

        [Fact]
        public void Grid_toolbar_buttons_do_not_reply_to_server_or_close_container()
        {
            var grid = (GridContainer)FormatterServices.GetUninitializedObject(typeof(GridContainer));
            grid.LocalSerial = 42;
            grid.OnButtonClick(0);
            Assert.False(grid.IsDisposed);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Right_click_closes_move_window_and_cancels_unsent_moves_when_allowed(bool canClose)
        {
            var type = typeof(MultiItemMoveGump);
            var state = new[] { "processing", "_operation", "processType", "containerId", "tradeId", "groundX", "groundY", "groundZ" }
                .Select(name => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)).ToDictionary(field => field, field => field.GetValue(null));
            try
            {
                var operation = new QueuedOperation("Multi Move") { Total = 2, Sent = 1 };
                operation.CancelSource = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("ClearAll", BindingFlags.Static | BindingFlags.NonPublic));
                type.GetField("_operation", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, operation);
                type.GetField("processing", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
                MultiItemMoveGump.TrySelect(MakeItem(10, 1, 42));
                var gump = (MultiItemMoveGump)FormatterServices.GetUninitializedObject(type);
                gump.CanCloseWithRightClick = canClose;
                gump.InvokeMouseCloseGumpWithRClick();
                Assert.Equal(canClose, gump.IsDisposed);
                Assert.Equal(canClose, operation.Cancelled);
                Assert.Equal(1, operation.Sent);
                Assert.Equal(canClose ? 0 : 1, MultiItemMoveGump.SelectedCount);
                Assert.Equal(canClose ? 0 : 1, MultiItemMoveGump.MoveItems.Count);
                Assert.Equal(!canClose, (bool)type.GetField("processing", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            }
            finally { foreach (var field in state) field.Key.SetValue(null, field.Value); }
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
            foreach (var entry in _oldSearchProperties)
                if (entry.Value == null) _searchProperties.Remove(entry.Key);
                else _searchProperties[entry.Key] = entry.Value;
            typeof(World).GetProperty(nameof(World.Player)).SetValue(null, _player); World.RangeSize = _range;
            TileDataLoader._staticData = _tiles; _selected.Clear();
            foreach (uint serial in _selectedSerials) _selected.TryAdd(serial, 1);
            while (MultiItemMoveGump.MoveItems.TryDequeue(out _)) { }
            foreach (Item item in _queued) MultiItemMoveGump.MoveItems.Enqueue(item);
        }
    }
}

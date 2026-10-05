using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;
using Map = ClassicUO.Game.Map.Map;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class WorldMapLoadCancellationTests : IDisposable
    {
        private readonly Map _originalMap = World.Map;
        private readonly PlayerMobile _originalPlayer = World.Player;
        private readonly int[,] _originalBlocks = MapLoader.Instance.MapBlocksSize;

        public WorldMapLoadCancellationTests()
        {
            // Cancellation must happen before pointer reads, allocation or GPU access.
            SetWorld("Map", Empty<Map>());
            SetWorld("Player", Empty<PlayerMobile>());
            typeof(MapLoader).GetProperty(nameof(MapLoader.MapBlocksSize))
                .SetValue(MapLoader.Instance, new int[MapLoader.MAPS_COUNT, 2]);
        }

        [Fact]
        public void Same_facet_new_session_cancels_pending_map_before_accessing_assets()
        {
            var gump = Empty<WorldMapGump>();
            Task<bool> load = Load(gump);
            Assert.False(load.IsCompleted);
            SetWorld("Map", Empty<Map>());
            RunNextSlice();
            Assert.True(load.IsCompleted);
            Assert.False(load.GetAwaiter().GetResult());
            Assert.Null(gump.MapTexture);
        }

        [Fact]
        public void Logout_cancels_pending_map_before_accessing_assets()
        {
            var gump = Empty<WorldMapGump>();
            Task<bool> load = Load(gump);
            SetWorld("Player", null);
            RunNextSlice();
            Assert.False(load.GetAwaiter().GetResult());
            Assert.Null(gump.MapTexture);
        }

        [Fact]
        public void New_load_cancels_previous_generation()
        {
            var gump = Empty<WorldMapGump>();
            Task<bool> first = Load(gump);
            Task<bool> second = Load(gump);
            Assert.True(first.IsCompleted);
            Assert.False(first.GetAwaiter().GetResult());
            RunNextSlice();
            Assert.False(second.IsCompleted);
            SetWorld("Player", null);
            RunNextSlice();
            Assert.False(second.GetAwaiter().GetResult());
        }

        public void Dispose()
        {
            SetWorld("Player", null);
            while (MainThreadQueue.QueuedActions.TryDequeue(out Action action)) action();
            SetWorld("Map", _originalMap);
            SetWorld("Player", _originalPlayer);
            typeof(MapLoader).GetProperty(nameof(MapLoader.MapBlocksSize))
                .SetValue(MapLoader.Instance, _originalBlocks);
        }

        private static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static void SetWorld(string property, object value) => typeof(World).GetProperty(property).SetValue(null, value);
        private static Task<bool> Load(WorldMapGump gump) => (Task<bool>)typeof(WorldMapGump)
            .GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gump, null);
        private static void RunNextSlice()
        {
            Assert.True(MainThreadQueue.QueuedActions.TryDequeue(out Action action));
            action();
        }
    }
}

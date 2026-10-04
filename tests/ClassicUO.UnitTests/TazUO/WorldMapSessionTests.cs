using System.Collections.Generic;
using System.Reflection;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class WorldMapSessionTests
    {
        public WorldMapSessionTests() => TestLogging.EnsureInitialized();

        [Fact]
        public void Session_clear_removes_cached_names_and_corpse_marker()
        {
            const uint serial = 0x00007701;
            var cache = (Dictionary<uint, string>)typeof(WMapEntity)
                .GetField("_mobileNameCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var original = new Dictionary<uint, string>(cache);
            try
            {
                cache[serial] = "Previous shard player";
                var manager = new WorldMapEntityManager();
                var entity = new WMapEntity(serial);
                Assert.Equal("Previous shard player", entity.GetName());
                manager.Entities.Add(serial, entity);
                manager._corpse = entity;

                manager.Clear();

                Assert.Empty(manager.Entities);
                Assert.Null(manager._corpse);
                Assert.Equal("<out of range>", new WMapEntity(serial).GetName());
            }
            finally
            {
                cache.Clear();
                foreach (var entry in original) cache.Add(entry.Key, entry.Value);
            }
        }
    }
}

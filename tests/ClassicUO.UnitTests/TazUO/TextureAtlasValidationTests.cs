using System;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class TextureAtlasValidationTests
    {
        [Theory]
        [InlineData(5, 1)]
        [InlineData(1, 5)]
        [InlineData(3, 1)]
        [InlineData(1, 3)]
        [InlineData(4, 1)]
        [InlineData(1, 4)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        public void Unpackable_sprites_are_rejected_before_allocating_textures(int width, int height)
        {
            using var atlas = new TextureAtlas(null, 4, 4, SurfaceFormat.Color);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                atlas.AddSprite(new uint[25], width, height, out _));
            Assert.Equal(0, atlas.TexturesCount);
        }

        [Fact]
        public void Incomplete_pixel_data_is_rejected_before_unmanaged_upload()
        {
            using var atlas = new TextureAtlas(null, 4, 4, SurfaceFormat.Color);
            Assert.Throws<ArgumentException>(() => atlas.AddSprite(new uint[3], 2, 2, out _));
        }

        [Fact]
        public void Unused_atlas_can_be_disposed_repeatedly()
        {
            var atlas = new TextureAtlas(null, 4, 4, SurfaceFormat.Color);
            atlas.Dispose();
            atlas.Dispose();
            Assert.Equal(0, atlas.TexturesCount);
        }
    }
}

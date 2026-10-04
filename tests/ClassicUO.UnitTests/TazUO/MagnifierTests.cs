using System;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class MagnifierTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void LensCenterRemainsTheClickPositionAtEveryZoom(int zoom)
        {
            Point click = new Point(813, 427);
            Vector4 region = MagnifierManager.SourceRegion(click, zoom, 1920, 1080);
            Assert.Equal(click.X, region.X * 1920f, 3);
            Assert.Equal(click.Y, region.Y * 1080f, 3);
            Assert.Equal(90f / zoom, region.Z * 1920f, 3);
            Assert.Equal(90f / zoom, region.W * 1080f, 3);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1919, 1079)]
        public void WindowEdgesDoNotShiftTheLensAwayFromItsHotspot(int x, int y)
        {
            Vector4 region = MagnifierManager.SourceRegion(new Point(x, y), 4, 1920, 1080);
            Assert.Equal(x, region.X * 1920f, 3);
            Assert.Equal(y, region.Y * 1080f, 3);
        }

        [Theory]
        [InlineData(-1, 1)]
        [InlineData(0, 1)]
        [InlineData(5, 4)]
        public void InvalidSavedZoomIsBounded(int saved, int expected) =>
            Assert.Equal(expected, MagnifierManager.ClampZoom(saved));

        [Fact]
        public void ChosenZoomPersistsPerProfile()
        {
            Assert.Equal(2, new Profile().MagnifierZoom);
            string json = JsonSerializer.Serialize(new Profile { MagnifierZoom = 4 }, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(4, JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile).MagnifierZoom);
        }

        [Fact]
        public void EmbeddedArtworkMatchesTheCalibratedLensGeometry()
        {
            using var stream = typeof(MagnifierManager).Assembly.GetManifestResourceStream("ClassicUO.MagnifierFrame.png");
            Assert.NotNull(stream);
            byte[] header = new byte[24];
            Assert.Equal(header.Length, stream.Read(header, 0, header.Length));
            byte[] signature = new byte[8];
            Array.Copy(header, signature, signature.Length);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, signature);
            int width = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
            int height = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
            Assert.Equal(1254, width);
            Assert.Equal(1254, height);
        }
    }
}

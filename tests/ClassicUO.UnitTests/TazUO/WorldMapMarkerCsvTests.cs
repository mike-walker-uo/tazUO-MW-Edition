using System.IO;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class WorldMapMarkerCsvTests
    {
        public WorldMapMarkerCsvTests() => TestLogging.EnsureInitialized();

        [Fact]
        public void Quoted_unicode_and_multiline_name_round_trips_with_zoom()
        {
            var marker = new WorldMapGump.WMapMarker
            {
                X = 120, Y = 340, MapId = 0, Name = "Däd's \"shop\", north\r\nEntrance",
                MarkerIconName = "", ColorName = "purple", ZoomIndex = 8
            };
            string csv = WorldMapMarkerCsv.Format(marker);
            var actual = Assert.Single(WorldMapGump.ReadMarkers(new StringReader(csv), "fixture"));
            Assert.Equal(marker.Name, actual.Name);
            Assert.Equal(marker.X, actual.X);
            Assert.Equal(marker.Y, actual.Y);
            Assert.Equal(marker.MapId, actual.MapId);
            Assert.Equal(marker.ZoomIndex, actual.ZoomIndex);
            Assert.Equal(marker.ColorName, actual.ColorName);
        }

        [Fact]
        public void Invalid_records_are_skipped_without_losing_later_markers()
        {
            const string csv = "10,20,0,First,,white\r\nshort,row\r\n"
                + "not-a-number,20,0,Bad,,red,4\r\n"
                + "30,40,0,\"bad\"suffix,,white,4\r\n"
                + "50,60,0,Last,,blue,9\r\n";
            var markers = WorldMapGump.ReadMarkers(new StringReader(csv), "fixture");
            Assert.Equal(new[] { "First", "Last" }, markers.Select(marker => marker.Name));
            Assert.Equal(3, markers[0].ZoomIndex);
            Assert.Equal(9, markers[1].ZoomIndex);
        }

        [Theory]
        [InlineData("-1,20,0,Name,,red,4")]
        [InlineData("10,-1,0,Name,,red,4")]
        [InlineData("10,20,-1,Name,,red,4")]
        [InlineData("10,20,99,Name,,red,4")]
        [InlineData("10,20,0,Name,,red,-1")]
        [InlineData("10,20,0,Name,,red,10")]
        [InlineData("10,20,0,Name,,red,wrong")]
        [InlineData("10,20,0,Name,,red,4,extra")]
        [InlineData("10,20,0,\"unfinished")]
        public void Invalid_marker_fields_are_rejected(string csv)
        {
            Assert.Empty(WorldMapGump.ReadMarkers(new StringReader(csv), "fixture"));
        }

        [Fact]
        public void Custom_facet_coordinates_are_not_truncated_and_bounds_are_exclusive()
        {
            int[,] sizes = MapLoader.Instance.MapsDefaultSize;
            int originalWidth = sizes[0, 0];
            try
            {
                sizes[0, 0] = 20000;
                var marker = WorldMapGump.ParseMarker(new[] { "12345", "20", "0", "Name", "", "red" });
                Assert.NotNull(marker);
                Assert.Equal(12345, marker.X);
                Assert.Null(WorldMapGump.ParseMarker(new[] { "20000", "20", "0", "Name", "", "red" }));
            }
            finally
            {
                sizes[0, 0] = originalWidth;
            }
        }
    }
}

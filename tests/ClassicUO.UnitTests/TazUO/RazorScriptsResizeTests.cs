using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class RazorScriptsResizeTests
    {
        [Theory]
        [InlineData(false, 360, 500, 300, 240)]
        [InlineData(true, 180, 300, 148, 104)]
        public void Dragging_past_minimum_and_holding_still_never_accumulates_size(
            bool compact, int width, int height, int minWidth, int minHeight)
        {
            var drag = new RazorScriptsGump.ResizeDrag();
            var start = new Point(500, 400);
            var size = new Point(width, height);
            var viewport = new Point(1200, 800);
            drag.Begin(start, size);
            Assert.True(drag.Active);
            for (int frame = 0; frame < 120; frame++)
                Assert.Equal(new Point(minWidth, minHeight), drag.SizeAt(new Point(0, 0), compact, viewport));
            // Moving back uses the original pointer/size, not the last clamped result.
            Assert.Equal(size, drag.SizeAt(start, compact, viewport));
            Assert.Equal(new Point(width + 20, height + 40), drag.SizeAt(start + new Point(20, 40), compact, viewport));
            drag.End();
            Assert.False(drag.Active);
        }

        [Theory]
        [InlineData(false, 360, 500, 300, 240)]
        [InlineData(true, 180, 300, 148, 104)]
        public void Width_and_height_limits_are_independent_and_new_drags_capture_a_new_origin(
            bool compact, int width, int height, int minWidth, int minHeight)
        {
            var drag = new RazorScriptsGump.ResizeDrag();
            var viewport = new Point(1200, 800);
            drag.Begin(Point.Zero, new Point(width, height));
            Assert.Equal(new Point(minWidth, height), drag.SizeAt(new Point(-1000, 0), compact, viewport));
            Assert.Equal(new Point(width, minHeight), drag.SizeAt(new Point(0, -1000), compact, viewport));
            Assert.Equal(viewport, drag.SizeAt(new Point(10000, 10000), compact, viewport));
            Assert.Equal(new Point(width, height), drag.SizeAt(Point.Zero, compact, viewport));
            drag.End();
            drag.Begin(new Point(100, 100), new Point(minWidth, minHeight));
            Assert.Equal(new Point(minWidth + 30, minHeight + 20),
                drag.SizeAt(new Point(130, 120), compact, viewport));
        }

        [Theory]
        [InlineData(false, 1, 1, 300, 240)]
        [InlineData(true, 1, 1, 148, 104)]
        [InlineData(false, 360, 500, 360, 500)]
        [InlineData(true, 180, 300, 180, 300)]
        [InlineData(false, 500, 20000, 500, 800)]
        [InlineData(true, 20000, 20000, 1200, 800)]
        public void Saved_and_live_sizes_are_bounded_without_changing_valid_dimensions(
            bool compact, int width, int height, int expectedWidth, int expectedHeight)
        {
            Assert.Equal(new Point(expectedWidth, expectedHeight),
                RazorScriptsGump.LimitSize(new Point(width, height), compact, new Point(1200, 800)));
        }
    }
}

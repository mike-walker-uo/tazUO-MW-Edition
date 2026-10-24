using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ViewportEffectsTests
    {
        [Theory]
        [InlineData(503, 37, 0.5f, false)]
        [InlineData(503, 37, 1f, false)]
        [InlineData(503, 37, 2f, false)]
        [InlineData(503, 37, 0.5f, true)]
        [InlineData(503, 37, 2f, true)]
        [InlineData(0, 37, 1f, true)]
        [InlineData(503, 0, 1f, true)]
        public void Moving_viewport_keeps_world_effects_and_local_bounds_aligned(
            int x, int y, float zoom, bool peek)
        {
            var original = new Camera(0.5f, 2f) { Bounds = new Rectangle(0, 0, 1555, 995), Zoom = zoom, PeekingToMouse = peek };
            var moved = new Camera(0.5f, 2f) { Bounds = new Rectangle(x, y, 1555, 995), Zoom = zoom, PeekingToMouse = peek };
            var worldPoint = new Point(740, 475);
            for (int frame = 0; frame < 90; frame++)
            {
                original.Update(true, 1f / 60f, new Point(960, 520));
                moved.Update(true, 1f / 60f, new Point(960 + x, 520 + y));
                Point local = original.WorldToScreen(worldPoint);
                Point movedLocal = moved.WorldToScreen(worldPoint);
                Assert.Equal(local, movedLocal);
                Assert.Equal(original.ViewportBounds, moved.ViewportBounds);
                Assert.True(moved.ViewportBounds.Contains(movedLocal));
                // The graphics viewport applies translation once, after overlay projection.
                Assert.Equal(new Point(local.X + x, local.Y + y),
                    movedLocal + new Point(moved.GetViewport().X, moved.GetViewport().Y));
            }
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(503, 37)]
        [InlineData(503, 0)]
        [InlineData(0, 37)]
        public void Cursor_to_world_conversion_removes_only_the_window_origin(int x, int y)
        {
            var camera = new Camera { Bounds = new Rectangle(x, y, 1555, 995) };
            var localCursor = new Point(740, 475);
            camera.Update(true, 1f / 60f, localCursor + new Point(x, y));
            Assert.Equal(localCursor, camera.MouseToWorldPosition());
            Assert.Equal(localCursor, camera.WorldToScreen(camera.MouseToWorldPosition()));
        }
    }
}

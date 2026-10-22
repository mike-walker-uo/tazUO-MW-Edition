using ClassicUO.Game.GameObjects;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class SpellVisualPolishTests
    {
        [Theory]
        [InlineData(-0.1f)]
        [InlineData(0f)]
        [InlineData(1f)]
        [InlineData(1.1f)]
        public void Effects_are_invisible_outside_their_animation_window(float progress) =>
            Assert.Equal(0f, SpellVisualPrimitives.Envelope(progress));

        [Fact]
        public void Reveal_and_release_have_smooth_edges_and_a_readable_peak()
        {
            Assert.True(SpellVisualPrimitives.Envelope(0.01f) < SpellVisualPrimitives.Envelope(0.05f));
            Assert.Equal(1f, SpellVisualPrimitives.Envelope(0.3f));
            Assert.True(SpellVisualPrimitives.Envelope(0.98f) < SpellVisualPrimitives.Envelope(0.9f));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Texture_masks_have_a_solid_center_and_transparent_edges(bool beam)
        {
            Assert.Equal(1f, SpellVisualPrimitives.Coverage(0, 0, beam));
            Assert.Equal(0f, SpellVisualPrimitives.Coverage(1, 0, beam));
            Assert.Equal(0f, SpellVisualPrimitives.Coverage(0, 1, beam));
            Assert.Equal(SpellVisualPrimitives.Coverage(0.3f, 0.5f, beam),
                SpellVisualPrimitives.Coverage(-0.3f, -0.5f, beam));
            Assert.InRange(SpellVisualPrimitives.Coverage(0, 0.85f, beam), 0f, 0.3f);
        }

        [Fact]
        public void Ribbons_keep_endpoints_and_follow_the_camera_without_changing_shape()
        {
            var start = new Vector2(-10, 4);
            var control = new Vector2(40, -30);
            var end = new Vector2(80, 20);
            var camera = new Vector2(503, 37);
            Assert.Equal(start, SpellVisualPrimitives.Curve(start, control, end, 0));
            Assert.Equal(end, SpellVisualPrimitives.Curve(start, control, end, 1));
            Assert.Equal(SpellVisualPrimitives.Curve(start, control, end, 0.5f) + camera,
                SpellVisualPrimitives.Curve(start + camera, control + camera, end + camera, 0.5f));
        }

        [Fact]
        public void Transparent_and_degenerate_primitives_never_touch_the_graphics_device()
        {
            SpellVisualPrimitives.Stroke(null, Vector2.Zero, Vector2.Zero, Color.White, 1, 1, 0);
            SpellVisualPrimitives.Stroke(null, Vector2.Zero, Vector2.One, Color.White, 0, 1, 0);
            SpellVisualPrimitives.Mote(null, Vector2.Zero, Color.White, 0, 4, 0);
            SpellVisualPrimitives.Mist(null, Vector2.Zero, Color.White, 0, 20, 10, 0, 0);
            SpellVisualPrimitives.Fragment(null, Vector2.Zero, Color.White, 1, 0, 0, 0);
            SpellVisualPrimitives.Ribbon(null, Vector2.Zero, Vector2.One, Vector2.UnitX, Color.White, 0, 1, 0);
        }
    }
}

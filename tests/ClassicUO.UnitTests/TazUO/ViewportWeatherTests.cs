using ClassicUO.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ViewportWeatherTests
    {
        [Theory]
        [InlineData(0.5f, 960, 540)]
        [InlineData(1f, 1920, 1080)]
        [InlineData(2f, 3840, 2160)]
        [InlineData(3f, 4096, 4096)]
        public void Render_target_weather_covers_the_composited_viewport_even_when_the_target_is_capped(float zoom, int width, int height)
        {
            var window = new Point(1920, 1080);
            var target = new Point(width, height);
            Matrix projection = Weather.ViewportProjection(window, target, zoom);
            Vector2 first = Vector2.Transform(Vector2.Zero, projection);
            Vector2 last = Vector2.Transform(new Vector2(window.X, window.Y), projection);
            // The renderer samples a centered window * zoom rectangle from the target.
            Assert.Equal((target.X - window.X * zoom) / 2f, first.X, 3);
            Assert.Equal((target.Y - window.Y * zoom) / 2f, first.Y, 3);
            Assert.Equal(window.X, (last.X - first.X) / zoom, 3);
            Assert.Equal(window.Y, (last.Y - first.Y) / zoom, 3);
        }
        [Theory]
        [InlineData(1f)]
        [InlineData(1.5f)]
        [InlineData(2f)]
        public void Direct_weather_starts_at_the_viewport_origin_including_global_scaling(float scale)
        {
            var window = new Point(1920, 1080);
            Matrix projection = Weather.ViewportProjection(window, new Point((int)(window.X * scale), (int)(window.Y * scale)), scale);
            Assert.Equal(Vector2.Zero, Vector2.Transform(Vector2.Zero, projection));
            Assert.Equal(new Vector2(window.X * scale, window.Y * scale), Vector2.Transform(new Vector2(window.X, window.Y), projection));
        }
        [Fact]
        public void Weather_to_world_conversion_keeps_light_and_contact_samples_aligned()
        {
            Matrix world = Matrix.CreateScale(0.5f) * Matrix.CreateTranslation(220, 140, 0);
            Matrix weather = Weather.ViewportProjection(new Point(1920, 1080), new Point(1920, 1080), 1f);
            Vector2 particle = new Vector2(300, 250);
            Vector2 terrain = Vector2.Transform(particle, weather * Matrix.Invert(world));
            Assert.Equal(Vector2.Transform(particle, weather), Vector2.Transform(terrain, world));
        }
        [Theory]
        [InlineData(1920, 1080, 200)]
        [InlineData(3840, 2160, 400)]
        [InlineData(7680, 4320, 400)]
        public void Larger_displays_gain_density_with_a_fixed_particle_cap(int width, int height, int expected) =>
            Assert.Equal(expected, Weather.ViewportParticleCount(200, new Point(width, height), 400));
    }
}

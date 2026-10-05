using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class UserFeedbackPolicyTests
    {
        [Theory]
        [InlineData(ChatMode.Default, "[c", true, true)]
        [InlineData(ChatMode.Default, " [C  ", true, true)]
        [InlineData(ChatMode.ServUOCommand, "[c ", true, true)]
        [InlineData(ChatMode.Default, "[c hello", true, false)]
        [InlineData(ChatMode.ServUOCommand, "[c hello", true, false)]
        [InlineData(ChatMode.Default, "[tc", true, false)]
        [InlineData(ChatMode.Emote, "[c", true, false)]
        [InlineData(ChatMode.Guild, "[c", true, false)]
        [InlineData(ChatMode.Default, "[c", false, false)]
        [InlineData(ChatMode.Default, null, true, false)]
        public void Bare_chat_command_opens_history_only_in_native_public_command_modes(
            ChatMode mode, string text, bool native, bool expected)
        {
            Assert.Equal(expected, SystemChatControl.IsNativeGlobalHistoryRequest(mode, text, native));
        }

        [Theory]
        [InlineData(0, false, false, false, false, false)]
        [InlineData(0, true, false, false, false, true)]
        [InlineData(1, false, false, false, false, true)]
        [InlineData(0, false, true, false, false, true)]
        [InlineData(0, false, false, true, false, true)]
        [InlineData(0, false, false, false, true, true)]
        public void Stock_warning_requires_observed_bandages_or_active_bandage_automation(
            int count, bool seen, bool auto, bool pets, bool external, bool expected)
        {
            Assert.Equal(expected, BandageStockWarner.ShouldMonitor(count, seen, auto, pets, external));
        }

        [Fact]
        public void Fog_wash_covers_entire_unscaled_viewport_with_rounding_margin()
        {
            Assert.Equal(new Rectangle(-2, -2, 1924, 1084),
                Weather.FogCoverageBounds(Matrix.Identity, new Point(1920, 1080)));
        }

        [Theory]
        [InlineData(0.5f, 172.3f, -18.6f)]
        [InlineData(0.75f, -19.7f, 40.2f)]
        [InlineData(1f, 7.6f, -6.5f)]
        [InlineData(2f, -160.2f, 14.9f)]
        [InlineData(4f, 100.7f, 80.6f)]
        public void Fog_wash_covers_4k_at_zoom_and_camera_offsets(float zoom, float x, float y)
        {
            Matrix transform = Matrix.CreateScale(zoom) * Matrix.CreateTranslation(x, y, 0);
            Rectangle coverage = Weather.FogCoverageBounds(transform, new Point(3840, 2160));
            Vector2 first = Vector2.Transform(new Vector2(coverage.Left, coverage.Top), transform);
            Vector2 last = Vector2.Transform(new Vector2(coverage.Right, coverage.Bottom), transform);
            Assert.True(first.X <= 0 && first.Y <= 0);
            Assert.True(last.X >= 3840 && last.Y >= 2160);
        }
    }
}

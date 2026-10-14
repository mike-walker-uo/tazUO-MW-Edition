using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class MusicCrossfadeTests
    {
        [Fact]
        public void War_and_peace_crossfades_keep_combined_gain_stable_and_finish()
        {
            var fade = new MusicCrossfade();
            fade.Begin(100, 400, true);
            Assert.Equal(0f, fade.War);
            fade.Update(300);
            Assert.Equal(0.5f, fade.War, 5);
            Assert.Equal(1f, fade.Peace + fade.War, 5);
            fade.Update(500);
            Assert.False(fade.Active);
            Assert.Equal(0f, fade.Peace);
            fade.Begin(600, 400, false);
            fade.Update(800);
            Assert.Equal(0.5f, fade.Peace, 5);
            fade.Update(1000);
            Assert.Equal(1f, fade.Peace);
            Assert.Equal(0f, fade.War);
        }
        [Fact]
        public void Rapid_war_toggle_reverses_from_the_current_gain_without_a_jump()
        {
            var fade = new MusicCrossfade();
            fade.Begin(100, 400, true);
            fade.Update(200);
            float peace = fade.Peace, war = fade.War;
            fade.Begin(200, 400, false);
            Assert.Equal(peace, fade.Peace);
            Assert.Equal(war, fade.War);
            fade.Update(600);
            Assert.Equal(1f, fade.Peace);
            Assert.Equal(0f, fade.War);
        }
        [Fact]
        public void Disabled_fades_switch_immediately_and_reset_clears_pending_work()
        {
            var fade = new MusicCrossfade();
            fade.Begin(100, 0, true);
            Assert.False(fade.Active);
            Assert.Equal(1f, fade.War);
            fade.Begin(100, 500, false);
            fade.Reset();
            fade.Update(900);
            Assert.Equal(1f, fade.Peace);
            Assert.Equal(0f, fade.War);
            Assert.False(fade.Active);
        }
        [Fact]
        public void Tick_wrap_does_not_stall_a_fade()
        {
            var fade = new MusicCrossfade();
            fade.Begin(uint.MaxValue - 100, 200, true);
            fade.Update(99);
            Assert.False(fade.Active);
            Assert.Equal(1f, fade.War);
        }
    }
}

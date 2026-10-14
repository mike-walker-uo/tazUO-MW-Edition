using System;
using System.Reflection;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.IO.Audio;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class MusicWarModeTests : IDisposable
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly AudioManager _audio = new AudioManager();
        private readonly UOMusic _peace = new UOMusic(1200, "peace", false, "unused.mp3");
        private UOMusic[] Tracks => (UOMusic[])Field("_currentMusic").GetValue(_audio);
        private int[] Indices => (int[])Field("_currentMusicIndices").GetValue(_audio);
        private int[] Requests => (int[])Field("_requestedMusicIndices").GetValue(_audio);
        private static FieldInfo Field(string name) => typeof(AudioManager).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);

        public MusicWarModeTests()
        {
            // No audio device, MP3 file or client controller is needed for
            // these ownership checks; the streams have not been started.
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile))
                .SetValue(null, new Profile { EnableMusic = false });
            Tracks[0] = _peace;
            Indices[0] = 1200;
            Requests[0] = 14;
        }

        [Fact]
        public void Leaving_war_keeps_the_exact_peace_track_and_original_request()
        {
            Tracks[1] = new UOMusic(38, "war", false, "unused.mp3");
            Indices[1] = 38;
            _audio.StopWarMusic();
            Assert.Same(_peace, Tracks[0]);
            Assert.Null(Tracks[1]);
            Assert.Equal(1200, _audio.CurrentMusicIndex);
            Assert.Equal(14, Requests[0]);
            _audio.StopWarMusic();
            Assert.Same(_peace, Tracks[0]);
        }

        [Fact]
        public void Stopping_war_without_an_active_war_track_does_not_restart_music()
        {
            _audio.StopWarMusic();
            Assert.Same(_peace, Tracks[0]);
            Assert.Equal(1200, _audio.CurrentMusicIndex);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Manual_pause_or_stop_cancels_pending_automatic_resume(bool stop)
        {
            Field("_resumePeaceMusic").SetValue(_audio, true);
            if (stop) _audio.StopCurrentMusic();
            else _audio.PauseCurrentMusic();
            Assert.False((bool)Field("_resumePeaceMusic").GetValue(_audio));
        }

        [Fact]
        public void Full_stop_releases_both_slots_and_pending_resume()
        {
            Tracks[1] = new UOMusic(38, "war", false, "unused.mp3");
            Field("_resumePeaceMusic").SetValue(_audio, true);
            _audio.StopMusic();
            Assert.Null(Tracks[0]);
            Assert.Null(Tracks[1]);
            Assert.False((bool)Field("_resumePeaceMusic").GetValue(_audio));
        }

        public void Dispose()
        {
            _audio.StopMusic();
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);
        }
    }
}

#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Utility;
using ClassicUO.Configuration;
using ClassicUO.IO.Audio;
using ClassicUO.Assets;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Audio;

namespace ClassicUO.Game.Managers
{
    internal class AudioManager
    {
        const float SOUND_DELTA = 250;
        private const int FAVORITE_MUSIC_WEIGHT = 4;

        private static readonly string[] _ancientFmStreams =
        {
            "https://mediaserv73.live-streams.nl:18058/stream",
            "http://mediaserv73.live-streams.nl:8058/stream"
        };

        private bool _canReproduceAudio = true;
        private readonly LinkedList<UOSound> _currentSounds = new LinkedList<UOSound>();
        private readonly UOMusic[] _currentMusic = { null, null };
        private readonly int[] _currentMusicIndices = { 0, 0 };
        private readonly int[] _requestedMusicIndices = { -1, -1 };
        private bool _resumePeaceMusic, _peacePausedForWar, _musicPausedByUser, _returningToPeace;
        private readonly MusicCrossfade _musicFade = new MusicCrossfade();
        private bool WarMusicActive => _currentMusic[1] != null && !_returningToPeace;
        private int CrossfadeDuration => ProfileManager.CurrentProfile?.EnableMusic == true && !_musicPausedByUser
            ? Math.Max(0, Math.Min(1000, ProfileManager.CurrentProfile.CombatMusicCrossfadeMs)) : 0;
        private UORadioMusic _radioMusic;
        private bool _radioPaused;
        public int LoginMusicIndex { get; private set; }
        public int DeathMusicIndex { get; } = 42;

        public void Initialize()
        {
            try
            {
                new DynamicSoundEffectInstance(0, AudioChannels.Stereo).Dispose();
            }
            catch (NoAudioHardwareException ex)
            {
                Log.Warn(ex.ToString());
                _canReproduceAudio = false;
            }

            LoginMusicIndex = Client.Version >= ClientVersion.CV_7000 ? 78 : Client.Version > ClientVersion.CV_308Z ? 0 : 8;

            Client.Game.Activated += OnWindowActivated;
            Client.Game.Deactivated += OnWindowDeactivated;
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            if (!_canReproduceAudio || ProfileManager.CurrentProfile == null || ProfileManager.CurrentProfile.ReproduceSoundsInBackground)
            {
                return;
            }

            SoundEffect.MasterVolume = 0;
        }

        private void OnWindowActivated(object sender, EventArgs e)
        {
            if (!_canReproduceAudio || ProfileManager.CurrentProfile == null || ProfileManager.CurrentProfile.ReproduceSoundsInBackground)
            {
                return;
            }

            SoundEffect.MasterVolume = 1;
        }

        public void PlaySound(int index)
        {
            Profile currentProfile = ProfileManager.CurrentProfile;

            if (!_canReproduceAudio || currentProfile == null)
            {
                return;
            }

            float volume = currentProfile.SoundVolume / SOUND_DELTA;

            if (Client.Game.IsActive)
            {
                if (!currentProfile.ReproduceSoundsInBackground)
                {
                    volume = currentProfile.SoundVolume / SOUND_DELTA;
                }
            }
            else if (!currentProfile.ReproduceSoundsInBackground)
            {
                volume = 0;
            }

            if (volume < -1 || volume > 1f)
            {
                return;
            }

            if (!currentProfile.EnableSound || !Client.Game.IsActive && !currentProfile.ReproduceSoundsInBackground)
            {
                volume = 0;
            }

            UOSound sound = (UOSound) Client.Game.Sounds.GetSound(index);

            if (sound != null && sound.Play(Time.Ticks, volume))
            {
                sound.X = -1;
                sound.Y = -1;
                sound.CalculateByDistance = false;

                _currentSounds.AddLast(sound);
            }
        }

        public void PlaySoundWithDistance(int index, int x, int y)
        {
            PlaySoundWithDistance(index, x, y, 1f);
        }

        public void PlaySoundWithDistance(int index, int x, int y, float volumeScale)
        {
            if (!_canReproduceAudio || !World.InGame)
            {
                return;
            }

            int distX = Math.Abs(x - World.Player.X);
            int distY = Math.Abs(y - World.Player.Y);
            int distance = Math.Max(distX, distY);

            Profile currentProfile = ProfileManager.CurrentProfile;
            volumeScale = Math.Max(0f, Math.Min(1f, volumeScale));
            float volume = currentProfile.SoundVolume / SOUND_DELTA * volumeScale;
            float distanceFactor = 0.0f;

            if (distance >= 1)
            {
                float volumeByDist = volume / (World.ClientViewRange + 1);
                distanceFactor = volumeByDist * distance;
            }

            if (distance > World.ClientViewRange)
            {
                volume = 0;
            }

            if (volume < -1 || volume > 1f)
            {
                return;
            }

            if (currentProfile == null || !currentProfile.EnableSound || !Client.Game.IsActive && !currentProfile.ReproduceSoundsInBackground)
            {
                volume = 0;
            }

            UOSound sound = (UOSound)Client.Game.Sounds.GetSound(index);

            if (sound != null && sound.Play(Time.Ticks, volume, distanceFactor))
            {
                sound.X = x;
                sound.Y = y;
                sound.CalculateByDistance = true;

                _currentSounds.AddLast(sound);
            }
        }

        public void PlayMusic(int music, bool iswarmode = false, bool is_login = false, bool bypassSelection = false)
        {
            if (!_canReproduceAudio)
            {
                return;
            }

            if (_radioMusic != null && !is_login && !bypassSelection)
            {
                return;
            }

            if (music >= Constants.MAX_MUSIC_DATA_INDEX_COUNT)
            {
                return;
            }

            int requestedMusic = music;

            if (iswarmode && ProfileManager.CurrentProfile?.EnableCombatMusic == false)
            {
                return;
            }

            if (!is_login && !bypassSelection && !iswarmode)
            {
                // A repeated regional request must not reroll a custom track,
                // including the server request received on returning to peace.
                StopWarMusic();
                if (_currentMusic[0] != null && !_currentMusic[0].IsStopped
                    && requestedMusic == _requestedMusicIndices[0])
                {
                    return;
                }
            }

            if (!is_login && !bypassSelection)
            {
                music = SelectMusic(requestedMusic);
            }

            float volume;

            if (is_login)
            {
                volume = Settings.GlobalSettings.LoginMusic ? Settings.GlobalSettings.LoginMusicVolume / SOUND_DELTA : 0;
            }
            else
            {
                Profile currentProfile = ProfileManager.CurrentProfile;

                if (currentProfile == null || !currentProfile.EnableMusic)
                {
                    volume = 0;
                }
                else
                {
                    volume = currentProfile.MusicVolume / SOUND_DELTA;
                }
            }


            if (volume < -1 || volume > 1f)
            {
                return;
            }

            Sound m = Client.Game.Sounds.GetMusic(music);

            if (m is UOMusic selectedMusic && IsCustomMusic(music))
            {
                selectedMusic.Repeat = (ProfileManager.CurrentProfile?.MusicPlaybackMode ?? 0) == 0;
            }

            if (iswarmode && m == _currentMusic[0])
            {
                // Music objects are cached by index; one stream cannot occupy
                // both slots without losing its paused playback position.
                return;
            }

            int idx = iswarmode ? 1 : 0;
            if (m == null && !iswarmode && _currentMusic[0] != null)
            {
                StopMusic();
            }
            else if (m != null && (m != _currentMusic[idx] || ((UOMusic)m).IsStopped))
            {
                if (iswarmode)
                {
                    StopMusicSlot(1);
                    UOMusic peace = _currentMusic[0];
                    if (peace != null && !peace.IsStopped && !peace.IsPaused && !_musicPausedByUser)
                        _resumePeaceMusic = _peacePausedForWar = true;
                    _returningToPeace = false;
                    _musicFade.Begin(Time.Ticks, CrossfadeDuration, true);
                }
                else
                {
                    bool paused = _musicPausedByUser;
                    StopMusic();
                    _musicPausedByUser = paused;
                }

                _requestedMusicIndices[idx] = requestedMusic;
                _currentMusicIndices[idx] = music;
                _currentMusic[idx] = (UOMusic) m;

                bool played = _currentMusic[idx].Play(Time.Ticks, volume * (iswarmode ? _musicFade.War : 1f));
                if (!played && iswarmode) StopWarMusic();
                else
                {
                    if (_musicPausedByUser) _currentMusic[idx].PausePlayback();
                    CompleteMusicFade();
                    UpdateCurrentMusicVolume(is_login);
                }
            }
            else if (iswarmode && m != null && _returningToPeace)
            {
                _returningToPeace = false;
                _musicFade.Begin(Time.Ticks, CrossfadeDuration, true);
                if (!_musicPausedByUser) _currentMusic[1].ResumePlayback();
            }
        }

        private int SelectMusic(int requestedMusic)
        {
            byte mode = ProfileManager.CurrentProfile?.MusicSelectionMode ?? 2;
            IReadOnlyList<int> customMusic = SoundsLoader.Instance.CustomMusicIndices;

            if (mode == 0 || customMusic.Count == 0 || mode == 2 && !RandomHelper.RandomBool())
            {
                return requestedMusic;
            }

            return SelectWeightedCustomMusic(customMusic);
        }

        public void RefreshMusicSelection()
        {
            if (_radioMusic != null)
            {
                return;
            }

            int idx = WarMusicActive ? 1 : 0;
            int requestedMusic = _requestedMusicIndices[idx];

            if (requestedMusic < 0)
            {
                return;
            }

            StopMusic();
            PlayMusic(requestedMusic, idx == 1);
        }

        public int CurrentMusicIndex
        {
            get
            {
                if (_radioMusic != null)
                {
                    return -1;
                }

                if (WarMusicActive)
                {
                    return _currentMusicIndices[1];
                }

                return _currentMusic[0] != null ? _currentMusicIndices[0] : -1;
            }
        }

        public string CurrentMusicName
        {
            get
            {
                if (_radioMusic != null)
                {
                    return string.IsNullOrWhiteSpace(_radioMusic.CurrentTitle)
                        ? _radioMusic.Name
                        : _radioMusic.CurrentTitle;
                }

                int idx = WarMusicActive ? 1 : 0;
                return _currentMusic[idx]?.Name ?? string.Empty;
            }
        }

        public string CurrentMusicPlaybackState
        {
            get
            {
                if (_radioMusic != null)
                {
                    return _radioPaused
                        ? "PAUSED"
                        : _radioMusic.StreamState.ToString().ToUpperInvariant();
                }

                UOMusic music = _currentMusic[WarMusicActive ? 1 : 0];

                if (music == null || music.IsStopped)
                {
                    return "STOPPED";
                }

                return music.IsPaused ? "PAUSED" : "PLAYING";
            }
        }

        public void PlayCurrentMusic()
        {
            if (_radioMusic != null)
            {
                if (_radioPaused || _radioMusic.StreamState == RadioStreamState.Stopped)
                {
                    _radioPaused = false;
                    _radioMusic.StartConnecting();
                }

                return;
            }

            _musicPausedByUser = false;
            UOMusic music = _currentMusic[WarMusicActive ? 1 : 0];

            if (music == null)
            {
                StepCustomMusic(1);
                return;
            }

            if (WarMusicActive && _peacePausedForWar)
            {
                _resumePeaceMusic = true;
                if (_musicFade.Active && _musicFade.Peace > 0f) _currentMusic[0]?.ResumePlayback();
            }
            UpdateCurrentMusicVolume();
            if (music.IsPaused)
            {
                music.ResumePlayback();
                return;
            }

            if (music.IsStopped)
            {
                Profile profile = ProfileManager.CurrentProfile;
                float volume = profile == null || !profile.EnableMusic ? 0 : profile.MusicVolume / SOUND_DELTA;
                music.Play(Time.Ticks, volume * (WarMusicActive ? _musicFade.War : _musicFade.Peace));
            }
        }

        public void PauseCurrentMusic()
        {
            if (_radioMusic != null)
            {
                _radioPaused = true;
                _radioMusic.Stop();
                return;
            }

            for (int i = 0; i < _currentMusic.Length; i++) _currentMusic[i]?.PausePlayback();
            _musicPausedByUser = true;
            _resumePeaceMusic = false;
        }

        public void StopCurrentMusic()
        {
            if (_radioMusic != null)
            {
                _radioPaused = false;
                _radioMusic.Stop();
                return;
            }

            for (int i = 0; i < _currentMusic.Length; i++)
            {
                _currentMusic[i]?.Stop();
            }
            _musicPausedByUser = true;
            _resumePeaceMusic = false;
        }

        public void StepCustomMusic(int direction)
        {
            IReadOnlyList<int> customMusic = SoundsLoader.Instance.CustomMusicIndices;

            if (customMusic.Count == 0)
            {
                return;
            }

            StopRadio();

            int current = CurrentMusicIndex;
            int position = -1;

            for (int i = 0; i < customMusic.Count; i++)
            {
                if (customMusic[i] == current)
                {
                    position = i;
                    break;
                }
            }

            if (position < 0)
            {
                position = direction < 0 ? 0 : -1;
            }

            position = (position + direction + customMusic.Count) % customMusic.Count;
            PlayCustomMusic(customMusic[position]);
        }

        public void ShuffleCustomMusic()
        {
            IReadOnlyList<int> customMusic = SoundsLoader.Instance.CustomMusicIndices;

            if (customMusic.Count == 0)
            {
                return;
            }

            StopRadio();
            PlayCustomMusic(SelectWeightedCustomMusic(customMusic));
        }

        public bool IsFavoriteCustomMusic(int music)
        {
            Profile profile = ProfileManager.CurrentProfile;
            string key = GetFavoriteMusicKey(music);

            if (profile?.FavoriteMusicTracks == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (int i = 0; i < profile.FavoriteMusicTracks.Count; i++)
            {
                if (string.Equals(profile.FavoriteMusicTracks[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void ToggleFavoriteCustomMusic(int music)
        {
            Profile profile = ProfileManager.CurrentProfile;
            string key = GetFavoriteMusicKey(music);

            if (profile == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (profile.FavoriteMusicTracks == null)
            {
                profile.FavoriteMusicTracks = new List<string>();
            }

            for (int i = 0; i < profile.FavoriteMusicTracks.Count; i++)
            {
                if (string.Equals(profile.FavoriteMusicTracks[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    profile.FavoriteMusicTracks.RemoveAt(i);
                    return;
                }
            }

            profile.FavoriteMusicTracks.Add(key);
        }

        private int SelectWeightedCustomMusic(IReadOnlyList<int> customMusic)
        {
            int current = CurrentMusicIndex;
            bool avoidCurrent = customMusic.Count > 1;
            int totalWeight = 0;

            for (int i = 0; i < customMusic.Count; i++)
            {
                if (avoidCurrent && customMusic[i] == current)
                {
                    continue;
                }

                totalWeight += IsFavoriteCustomMusic(customMusic[i]) ? FAVORITE_MUSIC_WEIGHT : 1;
            }

            if (totalWeight <= 0)
            {
                return customMusic[0];
            }

            int choice = RandomHelper.GetValue(1, totalWeight);

            for (int i = 0; i < customMusic.Count; i++)
            {
                int music = customMusic[i];

                if (avoidCurrent && music == current)
                {
                    continue;
                }

                choice -= IsFavoriteCustomMusic(music) ? FAVORITE_MUSIC_WEIGHT : 1;

                if (choice <= 0)
                {
                    return music;
                }
            }

            return customMusic[customMusic.Count - 1];
        }

        private static string GetFavoriteMusicKey(int music)
        {
            if (!SoundsLoader.Instance.TryGetMusicData(music, out string name, out _))
            {
                return string.Empty;
            }

            return System.IO.Path.GetFileNameWithoutExtension(name) ?? string.Empty;
        }

        public void ApplyCustomMusicPlaybackMode()
        {
            bool repeat = (ProfileManager.CurrentProfile?.MusicPlaybackMode ?? 0) == 0;

            for (int i = 0; i < _currentMusic.Length; i++)
            {
                if (_currentMusic[i] != null && IsCustomMusic(_currentMusicIndices[i]))
                {
                    _currentMusic[i].Repeat = repeat;
                }
            }
        }

        public void PlayCustomMusic(int music)
        {
            StopRadio();
            int idx = WarMusicActive ? 1 : 0;
            int backgroundRequest = _requestedMusicIndices[idx];
            PlayMusic(music, iswarmode: idx == 1, bypassSelection: true);

            if (backgroundRequest >= 0)
            {
                _requestedMusicIndices[idx] = backgroundRequest;
            }
        }

        public bool IsRadioSelected => _radioMusic != null;

        public string CurrentRadioDetails
        {
            get
            {
                if (_radioMusic == null)
                {
                    return "Ancient FM | Mediaeval and Renaissance music";
                }

                var details = new List<string>
                {
                    string.IsNullOrWhiteSpace(_radioMusic.StationName) ? "Ancient FM" : _radioMusic.StationName
                };

                if (!string.IsNullOrWhiteSpace(_radioMusic.Genre))
                {
                    details.Add(_radioMusic.Genre);
                }
                else if (!string.IsNullOrWhiteSpace(_radioMusic.Description))
                {
                    details.Add(_radioMusic.Description);
                }

                if (_radioMusic.BitrateKbps > 0)
                {
                    details.Add($"{_radioMusic.BitrateKbps} kbps");
                }

                if (!string.IsNullOrWhiteSpace(_radioMusic.ActiveStreamUrl))
                {
                    details.Add(_radioMusic.ActiveStreamUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? "secure stream"
                        : "HTTP fallback");
                }

                return string.Join(" | ", details);
            }
        }

        public void PlayAncientFm()
        {
            if (!_canReproduceAudio)
            {
                return;
            }

            if (_radioMusic != null)
            {
                PlayCurrentMusic();
                return;
            }

            StopMusic();
            _radioMusic = new UORadioMusic("Ancient FM", _ancientFmStreams);
            _radioPaused = false;
            _radioMusic.StartConnecting();
        }

        public void UpdateCurrentMusicVolume(bool isLogin = false)
        {
            if (!_canReproduceAudio)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                if (_currentMusic[i] != null)
                {
                    float volume;

                    if (isLogin)
                    {
                        volume = Settings.GlobalSettings.LoginMusic ? Settings.GlobalSettings.LoginMusicVolume / SOUND_DELTA : 0;
                    }
                    else
                    {
                        Profile currentProfile = ProfileManager.CurrentProfile;

                        volume = currentProfile == null || !currentProfile.EnableMusic ? 0 : currentProfile.MusicVolume / SOUND_DELTA;
                    }


                    if (volume < -1 || volume > 1f)
                    {
                        return;
                    }

                    _currentMusic[i].Volume = volume * (i == 0 ? _musicFade.Peace : _musicFade.War);
                }
            }

            if (_radioMusic != null)
            {
                Profile currentProfile = ProfileManager.CurrentProfile;
                _radioMusic.Volume = currentProfile == null || !currentProfile.EnableMusic
                    ? 0
                    : currentProfile.MusicVolume / SOUND_DELTA;
            }
        }

        public void UpdateCurrentSoundsVolume()
        {
            if (!_canReproduceAudio)
            {
                return;
            }

            Profile currentProfile = ProfileManager.CurrentProfile;

            float volume = currentProfile == null || !currentProfile.EnableSound ? 0 : currentProfile.SoundVolume / SOUND_DELTA;

            if (volume < -1 || volume > 1f)
            {
                return;
            }

            for (LinkedListNode<UOSound> soundNode = _currentSounds.First; soundNode != null; soundNode = soundNode.Next)
            {
                soundNode.Value.Volume = volume;
            }
        }

        public void StopMusic()
        {
            for (int i = 0; i < 2; i++)
            {
                StopMusicSlot(i);
            }

            _resumePeaceMusic = _peacePausedForWar = _musicPausedByUser = _returningToPeace = false;
            _musicFade.Reset();
            StopRadio();
        }

        private void StopMusicSlot(int index)
        {
            if (_currentMusic[index] == null) return;
            _currentMusic[index].Stop();
            _currentMusic[index].Dispose();
            _currentMusic[index] = null;
        }

        public void StopWarMusic()
        {
            if (_currentMusic[1] == null || _returningToPeace) return;
            _returningToPeace = true;
            _musicFade.Begin(Time.Ticks, CrossfadeDuration, false);
            UpdateCurrentMusicVolume(); // Set return gain before resuming the saved stream.
            if (_resumePeaceMusic && !_musicPausedByUser) _currentMusic[0]?.ResumePlayback();
            CompleteMusicFade();
        }

        private void CompleteMusicFade()
        {
            if (_musicFade.Active) return;
            if (_returningToPeace)
            {
                StopMusicSlot(1);
                _returningToPeace = _resumePeaceMusic = _peacePausedForWar = false;
            }
            else if (WarMusicActive && _peacePausedForWar)
                _currentMusic[0]?.PausePlayback();
        }

        public void StopSounds()
        {
            LinkedListNode<UOSound> first = _currentSounds.First;

            while (first != null)
            {
                LinkedListNode<UOSound> next = first.Next;

                first.Value.Stop();

                _currentSounds.Remove(first);

                first = next;
            }
        }

        public void Update()
        {
            if (!_canReproduceAudio)
            {
                return;
            }

            Profile currentProfile = ProfileManager.CurrentProfile;
            if (currentProfile?.EnableCombatMusic == false) StopWarMusic();
            _musicFade.Update(Time.Ticks);
            CompleteMusicFade();
            int activeMusic = WarMusicActive ? 1 : 0;
            bool advanceCustomPlaylist = false;

            if (_radioMusic != null && !_radioPaused)
            {
                float radioVolume = currentProfile == null || !currentProfile.EnableMusic
                    ? 0
                    : currentProfile.MusicVolume / SOUND_DELTA;

                if (!Client.Game.IsActive && currentProfile != null && !currentProfile.ReproduceSoundsInBackground)
                {
                    radioVolume = 0;
                }

                _radioMusic.TryStartPlayback(Time.Ticks, radioVolume);
                _radioMusic.Volume = radioVolume;
                _radioMusic.Update();
            }

            for (int i = 0; i < 2; i++)
            {
                if (_currentMusic[i] != null && currentProfile != null)
                {
                    float volume = currentProfile.EnableMusic ? currentProfile.MusicVolume / SOUND_DELTA : 0;
                    if (!Client.Game.IsActive && !currentProfile.ReproduceSoundsInBackground) volume = 0;
                    _currentMusic[i].Volume = volume * (i == 0 ? _musicFade.Peace : _musicFade.War);
                }

                _currentMusic[i]?.Update();

                if (i == activeMusic && _currentMusic[i] != null &&
                    IsCustomMusic(_currentMusicIndices[i]) &&
                    _currentMusic[i].ConsumeFinishedNaturally())
                {
                    advanceCustomPlaylist = true;
                }
            }

            if (advanceCustomPlaylist && currentProfile != null)
            {
                if (currentProfile.MusicPlaybackMode == 1)
                {
                    StepCustomMusic(1);
                }
                else if (currentProfile.MusicPlaybackMode == 2)
                {
                    ShuffleCustomMusic();
                }
            }


            LinkedListNode<UOSound> first = _currentSounds.First;

            while (first != null)
            {
                LinkedListNode<UOSound> next = first.Next;

                if (!first.Value.IsPlaying(Time.Ticks))
                {
                    first.Value.Stop();
                    _currentSounds.Remove(first);
                }

                first = next;
            }
        }

        public UOMusic GetCurrentMusic()
        {
            UOMusic music = _currentMusic[WarMusicActive ? 1 : 0];
            return music != null && music.IsPlaying(Time.Ticks) ? music : null;
        }

        private static bool IsCustomMusic(int music)
        {
            IReadOnlyList<int> tracks = SoundsLoader.Instance.CustomMusicIndices;

            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] == music)
                {
                    return true;
                }
            }

            return false;
        }

        private void StopRadio()
        {
            if (_radioMusic != null)
            {
                _radioMusic.Stop();
                _radioMusic.Dispose();
                _radioMusic = null;
            }

            _radioPaused = false;
        }
    }
}

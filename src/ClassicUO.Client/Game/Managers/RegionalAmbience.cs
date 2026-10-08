using System;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.UI;
using ClassicUO.IO.Audio;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Audio;

namespace ClassicUO.Game.Managers
{
    internal static class RegionalAmbience
    {
        private sealed class Bed : Sound
        {
            private readonly byte[] _buffer;
            internal float Gain;
            internal Bed(int id, byte[] pcm, string name, int silenceSeconds) : base(name, id)
            {
                _buffer = new byte[pcm.Length + silenceSeconds * 44100];
                Array.Copy(pcm, _buffer, pcm.Length);
            }
            protected override byte[] GetBuffer() => _buffer;
            protected override void OnBufferNeeded(object sender, EventArgs e)
            {
                if (SoundInstance == null || SoundInstance.IsDisposed) return;
                while (SoundInstance.PendingBufferCount < 2) SoundInstance.SubmitBuffer(_buffer);
            }
        }

        private static readonly Bed[] _beds = new Bed[7];
        private static byte _preset;
        private static int _packRevision;
        private static uint _last;
        private static bool _unavailable;


        private static bool TryGetSample(AmbienceOverlay.AmbientBiome biome, int sound, out byte[] pcm, out string name)
        {
            if (LocalSoundscapePack.TryGet(biome, out pcm)) { name = biome + " local ambience"; return true; }
            return SoundsLoader.Instance.TryGetSound(sound, out pcm, out name);
        }

        internal static void Reset()
        {
            for (int i = 0; i < _beds.Length; i++) { _beds[i]?.Dispose(); _beds[i] = null; }
            _last = 0; _unavailable = false;
        }

        internal static void Update(AmbienceOverlay.AmbientBiome biome)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null || !World.InGame || !AmbienceOverlay.Enabled || VisualBudget.Settings?.RegionalSoundBeds != true)
            { if (_last != 0) Reset(); LocalSoundscapePack.Update(false, ""); return; }
            LocalSoundscapePack.Update(VisualBudget.Settings.LocalSoundscapePack, VisualBudget.Settings.SoundscapePackFolder);
            if (_packRevision != LocalSoundscapePack.Revision) { Reset(); _packRevision = LocalSoundscapePack.Revision; }
            byte preset = RegionalSoundscapes.Normalize(VisualBudget.Settings.RegionalSoundscape);
            if (preset != _preset) { Reset(); _preset = preset; }
            if (_unavailable) return;
            uint now = Time.Ticks;
            float dt = Math.Min(250u, _last == 0 ? 16u : now - _last) / 1000f;
            _last = now;
            int active = (int)biome;
            if (preset == 0 && !VisualBudget.Settings.LocalSoundscapePack)
            {
                if (biome == AmbienceOverlay.AmbientBiome.Forest || biome == AmbienceOverlay.AmbientBiome.Swamp) active = 0;
                else if (biome == AmbienceOverlay.AmbientBiome.Desert) active = (int)AmbienceOverlay.AmbientBiome.Coast;
            }
            if (active < 0 || active >= _beds.Length) active = 0;
            bool audible = profile.EnableSound && (Client.Game.IsActive || profile.ReproduceSoundsInBackground);
            float volume = audible ? profile.SoundVolume / 250f * SceneryInteractionManager.WeatherSoundScale : 0f;
            try
            {
                for (int i = 0; i < _beds.Length; i++)
                {
                    RegionalSoundscapes.Voice voice = RegionalSoundscapes.For(preset, (AmbienceOverlay.AmbientBiome)i);
                    if (_beds[i] == null && i == active && volume > 0 && TryGetSample((AmbienceOverlay.AmbientBiome)i, voice.Sound, out byte[] pcm, out string name))
                    {
                        _beds[i] = new Bed(voice.Sound, pcm, name, voice.Pause);
                        _beds[i].Play(now, 0f);
                    }
                    Bed bed = _beds[i];
                    if (bed == null) continue;
                    float target = i == active ? voice.Gain : 0f;
                    bed.Gain += (target - bed.Gain) * (1f - (float)Math.Exp(-dt / 3f));
                    bed.Volume = volume * bed.Gain;
                    if (i != active && bed.Gain < 0.001f) { bed.Dispose(); _beds[i] = null; }
                }
            }
            catch (NoAudioHardwareException ex)
            {
                Reset(); _unavailable = true;
                Log.Warn("Regional ambience unavailable: " + ex.Message);
            }
        }
    }
}

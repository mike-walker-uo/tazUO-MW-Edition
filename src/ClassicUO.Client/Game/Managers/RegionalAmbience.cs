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

        private static readonly Bed[] _beds = new Bed[4];
        private static uint _last;
        private static bool _unavailable;
        private static readonly int[] Sounds = { 0x01B, 0x014, 0x01B, 0x016 };

        internal static void Reset()
        {
            for (int i = 0; i < _beds.Length; i++) { _beds[i]?.Dispose(); _beds[i] = null; }
            _last = 0; _unavailable = false;
        }

        internal static void Update(AmbienceOverlay.AmbientBiome biome)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null || !World.InGame || !AmbienceOverlay.Enabled || VisualBudget.Settings?.RegionalSoundBeds != true)
            { if (_last != 0) Reset(); return; }
            if (_unavailable) return;
            uint now = Time.Ticks;
            float dt = Math.Min(250u, _last == 0 ? 16u : now - _last) / 1000f;
            _last = now;
            int active = biome == AmbienceOverlay.AmbientBiome.Dungeon ? 3
                : biome == AmbienceOverlay.AmbientBiome.Town ? 2
                : biome == AmbienceOverlay.AmbientBiome.Coast || biome == AmbienceOverlay.AmbientBiome.Desert ? 1 : 0;
            bool audible = profile.EnableSound && (Client.Game.IsActive || profile.ReproduceSoundsInBackground);
            float volume = audible ? profile.SoundVolume / 250f * SceneryInteractionManager.WeatherSoundScale : 0f;
            try
            {
                for (int i = 0; i < _beds.Length; i++)
                {
                    if (_beds[i] == null && i == active && volume > 0 && SoundsLoader.Instance.TryGetSound(Sounds[i], out byte[] pcm, out string name))
                    {
                        _beds[i] = new Bed(Sounds[i], pcm, name, i == 0 || i == 2 ? 14 : 2);
                        _beds[i].Play(now, 0f);
                    }
                    Bed bed = _beds[i];
                    if (bed == null) continue;
                    float target = i == active ? (i == 2 ? 0.08f : i == 3 ? 0.07f : 0.14f) : 0f;
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

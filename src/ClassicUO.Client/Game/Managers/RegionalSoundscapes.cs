using ClassicUO.Game.UI;

namespace ClassicUO.Game.Managers
{
    internal static class RegionalSoundscapes
    {
        internal static readonly string[] Names = { "Classic UO", "Britannian wilderness", "Quiet exploration" };
        internal readonly struct Voice
        {
            internal readonly int Sound, Pause;
            internal readonly float Gain;
            internal Voice(int sound, int pause, float gain) { Sound = sound; Pause = pause; Gain = gain; }
        }
        internal static byte Normalize(byte preset) => preset < Names.Length ? preset : (byte)0;
        internal static Voice For(byte preset, AmbienceOverlay.AmbientBiome biome)
        {
            preset = Normalize(preset);
            bool quiet = preset == 2;
            int sound = 0x01B, pause = 14;
            float gain = 0.14f;
            switch (biome)
            {
                case AmbienceOverlay.AmbientBiome.Coast: sound = 0x014; pause = 2; break;
                case AmbienceOverlay.AmbientBiome.Desert: sound = preset == 0 ? 0x014 : 0x015; pause = 2; break;
                case AmbienceOverlay.AmbientBiome.Swamp: if (preset != 0) { sound = 0x266; pause = 18; } break;
                case AmbienceOverlay.AmbientBiome.Town: gain = 0.08f; break;
                case AmbienceOverlay.AmbientBiome.Dungeon: sound = 0x016; pause = 2; gain = 0.07f; break;
            }
            if (quiet) { gain *= 0.5f; pause *= 2; }
            return new Voice(sound, pause, gain);
        }
    }
}

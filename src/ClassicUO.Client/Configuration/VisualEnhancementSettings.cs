using System.Collections.Generic;

namespace ClassicUO.Configuration
{
    public sealed class EffectDetailSettings
    {
        public int Intensity { get; set; } = 100;
        public int Density { get; set; } = 100;
        public int Glow { get; set; } = 100;
        public bool GroundEnabled { get; set; } = true;
        public int GroundSize { get; set; } = 100;
        public int GroundOpacity { get; set; } = 55;
    }

    public sealed class VisualEnhancementSettings
    {
        public bool AdaptiveParticles { get; set; } = true;
        public int TextureBudgetMB { get; set; } = 96;
        public bool SoftParticles { get; set; } = true;
        public bool LinearLight { get; set; }
        public bool WorldAntiAliasing { get; set; }
        public int WorldAntiAliasingStrength { get; set; } = 35;
        public int AntiAliasingDetailProtection { get; set; } = 75;
        public int PixelArtSharpness { get; set; } = 75;
        public bool SelectiveBloom { get; set; } = true;
        public int BloomStrength { get; set; } = 35;
        public bool UnifiedLightPalette { get; set; } = true;
        public bool ContactShadows { get; set; } = true;
        public bool LocalArtPack { get; set; }
        public string ArtPackFolder { get; set; } = "";
        public bool BiomeWeather { get; set; } = true;
        public bool WeatherCrossfade { get; set; } = true;
        public int LightningFlash { get; set; } = 100;
        public int ThunderVolume { get; set; } = 100;
        public int WeatherShake { get; set; } = 100;
        public bool WorldAnchoredFog { get; set; } = true;
        public bool RegionalSoundBeds { get; set; } = true;
        public byte RegionalSoundscape { get; set; }
        public bool LocalSoundscapePack { get; set; }
        public string SoundscapePackFolder { get; set; } = "";
        public bool SeasonalDetails { get; set; } = true;
        public Dictionary<int, int> MapParticleIntensity { get; set; } = new Dictionary<int, int>();
        public Dictionary<string, EffectDetailSettings> Effects { get; set; } = new Dictionary<string, EffectDetailSettings>();
        public bool SpellGroundEffectsEnabled { get; set; }
        public bool LightningGroundEnabled { get; set; } = true;
        public int LightningGroundSize { get; set; } = 100;
        public int LightningGroundOpacity { get; set; } = 65;
        public bool FireballGroundEnabled { get; set; } = true;
        public int FireballGroundSize { get; set; } = 100;
        public int FireballGroundOpacity { get; set; } = 55;
        public bool PrioritizeCombatEffects { get; set; } = true;
    }
}

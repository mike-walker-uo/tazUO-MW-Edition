using ClassicUO.Game.Scenes;

namespace ClassicUO.Configuration
{
    // Only spatial filtering is owned by presets; lighting, artwork and weather remain user settings.
    internal readonly struct WorldGraphicsSettings
    {
        internal readonly bool Processing, AntiAliasing;
        internal readonly ushort Filter;
        internal readonly int Strength, Sharpness, DetailProtection;

        internal WorldGraphicsSettings(bool processing, ushort filter, bool antiAliasing, int strength, int sharpness, int detailProtection)
        {
            Processing = processing; Filter = filter; AntiAliasing = antiAliasing;
            Strength = strength; Sharpness = sharpness; DetailProtection = detailProtection;
        }

        internal static WorldGraphicsSettings Capture(Profile profile)
        {
            var visual = profile.VisualEnhancements ?? new VisualEnhancementSettings();
            return new WorldGraphicsSettings(profile.EnablePostProcessingEffects, profile.PostProcessingType,
                visual.WorldAntiAliasing, visual.WorldAntiAliasingStrength, visual.PixelArtSharpness, visual.AntiAliasingDetailProtection);
        }

        internal static WorldGraphicsSettings Preset(int index) => index == 0
            ? new WorldGraphicsSettings(false, 0, false, 0, 100, 100)
            : index == 1 ? new WorldGraphicsSettings(true, 4, true, 15, 90, 90)
            : new WorldGraphicsSettings(true, 4, true, 35, 55, 65);

        internal void Apply(Profile profile)
        {
            profile.EnablePostProcessingEffects = Processing; profile.PostProcessingType = Filter;
            var visual = profile.VisualEnhancements ?? (profile.VisualEnhancements = new VisualEnhancementSettings());
            visual.WorldAntiAliasing = AntiAliasing; visual.WorldAntiAliasingStrength = Strength;
            visual.PixelArtSharpness = Sharpness; visual.AntiAliasingDetailProtection = DetailProtection;
        }
    }

    internal static class WorldGraphicsPreview
    {
        private static Profile _owner;
        private static WorldGraphicsSettings _settings;
        internal static WorldGraphicsSettings Effective(Profile profile) => _owner == profile && _owner != null
            ? _settings : WorldGraphicsSettings.Capture(profile);
        internal static void Show(Profile profile, WorldGraphicsSettings settings)
        {
            _owner = profile; _settings = settings;
            if (ProfileManager.CurrentProfile != null) Client.Game?.GetScene<GameScene>()?.SetPostProcessingSettings();
        }
        internal static void Clear()
        {
            _owner = null;
            if (ProfileManager.CurrentProfile != null) Client.Game?.GetScene<GameScene>()?.SetPostProcessingSettings();
        }
    }
}

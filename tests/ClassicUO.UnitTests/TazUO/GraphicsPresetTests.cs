using System;
using System.Text.Json;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class GraphicsPresetTests : IDisposable
    {
        private readonly Profile _previous = ProfileManager.CurrentProfile;
        private readonly Profile _profile = new Profile
        {
            EnablePostProcessingEffects = true, PostProcessingType = 2,
            VisualEnhancements = new VisualEnhancementSettings
            {
                LinearLight = true, BloomStrength = 83, ContactShadows = false,
                WorldAntiAliasing = true, WorldAntiAliasingStrength = 27, PixelArtSharpness = 63
            }
        };
        public GraphicsPresetTests() => typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Presets_can_restore_the_original_filter_without_overwriting_lighting(int preset)
        {
            var before = WorldGraphicsSettings.Capture(_profile);
            WorldGraphicsSettings.Preset(preset).Apply(_profile);
            Assert.True(_profile.VisualEnhancements.LinearLight);
            Assert.Equal(83, _profile.VisualEnhancements.BloomStrength);
            Assert.False(_profile.VisualEnhancements.ContactShadows);
            _profile.VisualEnhancements.BloomStrength = 41;
            before.Apply(_profile);
            Assert.Equal((ushort)2, _profile.PostProcessingType);
            Assert.True(_profile.EnablePostProcessingEffects);
            Assert.Equal(27, _profile.VisualEnhancements.WorldAntiAliasingStrength);
            Assert.Equal(63, _profile.VisualEnhancements.PixelArtSharpness);
            Assert.Equal(41, _profile.VisualEnhancements.BloomStrength);
        }
        [Fact]
        public void Preview_leaves_saved_fields_untouched_and_never_leaks_into_another_profile()
        {
            string saved = JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile);
            WorldGraphicsPreview.Show(_profile, WorldGraphicsSettings.Preset(0));
            Assert.Equal(saved, JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile));
            Assert.False(WorldGraphicsPreview.Effective(_profile).Processing);
            Assert.True(_profile.EnablePostProcessingEffects);
            Assert.Equal((ushort)2, _profile.PostProcessingType);
            var other = new Profile { EnablePostProcessingEffects = true, PostProcessingType = 3 };
            Assert.Equal((ushort)3, WorldGraphicsPreview.Effective(other).Filter);
            WorldGraphicsPreview.Clear();
            Assert.Equal((ushort)2, WorldGraphicsPreview.Effective(_profile).Filter);
        }
        [Fact]
        public void New_preferences_roundtrip_and_old_profiles_get_safe_defaults()
        {
            var old = JsonSerializer.Deserialize("{}", ProfileJsonContext.DefaultToUse.Profile);
            Assert.False(old.CompactTransferProgress);
            Assert.False(old.AutoOpenQueuedActions);
            Assert.Equal(350, old.CombatMusicCrossfadeMs);
            Assert.Equal(75, old.VisualEnhancements.AntiAliasingDetailProtection);
            _profile.CompactTransferProgress = true;
            _profile.AutoOpenQueuedActions = true;
            _profile.CombatMusicCrossfadeMs = 750;
            _profile.VisualEnhancements.AntiAliasingDetailProtection = 93;
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.True(restored.CompactTransferProgress);
            Assert.True(restored.AutoOpenQueuedActions);
            Assert.Equal(750, restored.CombatMusicCrossfadeMs);
            Assert.Equal(93, restored.VisualEnhancements.AntiAliasingDetailProtection);
        }
        public void Dispose()
        {
            WorldGraphicsPreview.Clear();
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _previous);
        }
    }
}

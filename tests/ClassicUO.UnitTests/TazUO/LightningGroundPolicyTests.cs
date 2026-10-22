using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class LightningGroundPolicyTests : IDisposable
    {
        private readonly Profile _previous = ProfileManager.CurrentProfile;
        private readonly Profile _profile = new Profile();

        public LightningGroundPolicyTests() =>
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);

        public void Dispose() =>
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _previous);

        [Theory]
        [InlineData(0u, 1f)]
        [InlineData(200u, 0.25f)]
        [InlineData(400u, 0f)]
        [InlineData(10000u, 0f)]
        [InlineData(uint.MaxValue, 0f)]
        public void Ground_light_expires_with_the_bolt_instead_of_leaving_a_decal(uint elapsed, float expected) =>
            Assert.Equal(expected, LightningEffect.GroundFade(elapsed));

        [Theory]
        [InlineData(false, 65, false, false)]
        [InlineData(true, 0, false, false)]
        [InlineData(true, 65, true, false)]
        [InlineData(true, 65, false, true)]
        public void Disabled_or_classic_modes_never_touch_graphics(bool enabled, int opacity, bool silence, bool classic)
        {
            _profile.VisualEnhancements = new VisualEnhancementSettings
                { SpellGroundEffectsEnabled = true, LightningGroundEnabled = enabled, LightningGroundOpacity = opacity };
            _profile.VisualSilence = silence;
            _profile.ClassicEffectsOnly = classic;
            var effect = (LightningEffect)FormatterServices.GetUninitializedObject(typeof(LightningEffect));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LightningEffect).GetField("_groundStarted", flags).SetValue(effect, Time.Ticks);
            typeof(LightningEffect).GetField("_enhancedAdmitted", flags).SetValue(effect, true);
            typeof(LightningEffect).GetField("_forceEnhanced", flags).SetValue(effect, true);
            Assert.False(effect.DrawGround(null));
        }

        [Fact]
        public void Global_switch_blocks_lightning_preview_without_graphics()
        {
            _profile.VisualEnhancements = new VisualEnhancementSettings();
            var effect = (LightningEffect)FormatterServices.GetUninitializedObject(typeof(LightningEffect));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(LightningEffect).GetField("_groundStarted", flags).SetValue(effect, Time.Ticks);
            typeof(LightningEffect).GetField("_enhancedAdmitted", flags).SetValue(effect, true);
            typeof(LightningEffect).GetField("_forceEnhanced", flags).SetValue(effect, true);
            Assert.False(effect.DrawGround(null));
        }

        [Fact]
        public void Ground_glow_controls_round_trip_with_the_character_profile()
        {
            _profile.VisualEnhancements = new VisualEnhancementSettings
                { LightningGroundEnabled = false, LightningGroundSize = 150, LightningGroundOpacity = 40 };
            string json = JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.False(restored.VisualEnhancements.LightningGroundEnabled);
            Assert.Equal(150, restored.VisualEnhancements.LightningGroundSize);
            Assert.Equal(40, restored.VisualEnhancements.LightningGroundOpacity);
        }
    }
}

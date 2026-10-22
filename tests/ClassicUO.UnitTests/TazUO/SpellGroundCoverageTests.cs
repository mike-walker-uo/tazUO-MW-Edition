using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class SpellGroundCoverageTests : IDisposable
    {
        private readonly Profile _previous = ProfileManager.CurrentProfile;
        private readonly Profile _profile = new Profile { VisualEnhancements = new VisualEnhancementSettings { SpellGroundEffectsEnabled = true } };
        private readonly FieldInfo _loaded = typeof(SpellAbilityEffectSettings).GetField("_loaded", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly HashSet<SpellAbilityEffectId> _disabled = (HashSet<SpellAbilityEffectId>)typeof(SpellAbilityEffectSettings)
            .GetField("_disabled", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private readonly bool _wasLoaded;
        private readonly SpellAbilityEffectId[] _previousDisabled;

        public SpellGroundCoverageTests()
        {
            _wasLoaded = (bool)_loaded.GetValue(null);
            _previousDisabled = _disabled.ToArray();
            _disabled.Clear();
            _loaded.SetValue(null, true);
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);
        }

        public void Dispose()
        {
            _disabled.Clear();
            _disabled.UnionWith(_previousDisabled);
            _loaded.SetValue(null, _wasLoaded);
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _previous);
        }

        public static IEnumerable<object[]> Effects => SpellAbilityEffectSettings.Entries.Select(e => new object[] { (int)e.Id });

        [Fact]
        public void Ground_effects_default_off_for_new_and_existing_profiles()
        {
            Assert.False(new Profile().VisualEnhancements.SpellGroundEffectsEnabled);
            var legacy = JsonSerializer.Deserialize("{\"visual_enhancements\":{\"lightning_ground_enabled\":true}}",
                ProfileJsonContext.DefaultToUse.Profile);
            Assert.False(legacy.VisualEnhancements.SpellGroundEffectsEnabled);
            Assert.True(legacy.VisualEnhancements.LightningGroundEnabled);
        }

        [Theory]
        [MemberData(nameof(Effects))]
        public void Global_switch_blocks_every_ground_style_and_preserves_spell_controls(int effectId)
        {
            var id = (SpellAbilityEffectId)effectId;
            _profile.VisualEnhancements.SpellGroundEffectsEnabled = false;
            Assert.False(SpellGroundGlow.TrySettings(id, out _, out _));
            Assert.False(SpellGroundGlow.DrawEffect(null, id, Vector2.Zero, 100, 800));
            _profile.VisualEnhancements.SpellGroundEffectsEnabled = true;
            Assert.True(SpellGroundGlow.TrySettings(id, out _, out _));
            Assert.True(SpellAbilityEffectSettings.IsEnabled(id));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Global_switch_round_trips_with_the_character_profile(bool enabled)
        {
            _profile.VisualEnhancements.SpellGroundEffectsEnabled = enabled;
            string json = JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(enabled, restored.VisualEnhancements.SpellGroundEffectsEnabled);
        }

        [Theory]
        [MemberData(nameof(Effects))]
        public void Every_registered_effect_has_an_explicit_ground_style_and_honors_spell_switch(int effectId)
        {
            var id = (SpellAbilityEffectId)effectId;
            Assert.True(SpellGroundGlow.TryStyle(id, out var style));
            Assert.InRange(style.Diameter, 60f, 300f);
            Assert.InRange(style.Strength, 0.1f, 0.8f);
            Assert.True(SpellGroundGlow.TrySettings(id, out _, out _));
            _disabled.Add(id);
            Assert.False(SpellGroundGlow.DrawEffect(null, id, Vector2.Zero, 100, 800));
        }

        [Theory]
        [MemberData(nameof(Effects))]
        public void Classic_and_silence_modes_disable_every_ground_effect_without_graphics(int effectId)
        {
            var id = (SpellAbilityEffectId)effectId;
            _profile.ClassicEffectsOnly = true;
            Assert.False(SpellGroundGlow.DrawEffect(null, id, Vector2.Zero, 100, 800));
            _profile.ClassicEffectsOnly = false;
            _profile.VisualSilence = true;
            Assert.False(SpellGroundGlow.DrawEffect(null, id, Vector2.Zero, 100, 800));
        }

        [Fact]
        public void Ground_controls_are_independent_and_clamped()
        {
            var controls = new EffectDetailSettings { GroundEnabled = false, GroundSize = 999, GroundOpacity = 999 };
            _profile.VisualEnhancements.Effects["Harm"] = controls;
            Assert.False(SpellGroundGlow.DrawEffect(null, SpellAbilityEffectId.Harm, Vector2.Zero, 100, 800));
            Assert.True(SpellGroundGlow.TrySettings(SpellAbilityEffectId.EnergyBolt, out _, out _));
            controls.GroundEnabled = true;
            Assert.True(SpellGroundGlow.TrySettings(SpellAbilityEffectId.Harm, out float size, out float opacity));
            Assert.Equal(2f, size);
            Assert.Equal(1f, opacity);
            controls.GroundOpacity = -1;
            Assert.False(SpellGroundGlow.DrawEffect(null, SpellAbilityEffectId.Harm, Vector2.Zero, 100, 800));
        }

        [Theory]
        [InlineData(0u, 800u, 0f)]
        [InlineData(30u, 800u, 0.5f)]
        [InlineData(100u, 800u, 1f)]
        [InlineData(650u, 800u, 0.5f)]
        [InlineData(800u, 800u, 0f)]
        [InlineData(uint.MaxValue, 800u, 0f)]
        [InlineData(100u, 0u, 0f)]
        public void Ground_effects_fade_with_existing_effect_lifetimes(uint age, uint duration, float expected) =>
            Assert.Equal(expected, SpellGroundGlow.Fade(age, duration));

        [Fact]
        public void Hit_area_colors_are_distinct_and_quieter_than_explosions()
        {
            var ids = new[] { SpellAbilityEffectId.HitFireArea, SpellAbilityEffectId.HitColdArea,
                SpellAbilityEffectId.HitPoisonArea, SpellAbilityEffectId.HitEnergyArea };
            var colors = new HashSet<Color>();
            SpellGroundGlow.TryStyle(SpellAbilityEffectId.Explosion, out var explosion);
            foreach (var id in ids)
            {
                SpellGroundGlow.TryStyle(id, out var style);
                Assert.True(colors.Add(style.Glow));
                Assert.True(style.Strength < explosion.Strength);
            }
        }

        [Fact]
        public void Per_spell_ground_controls_round_trip_and_old_profiles_keep_defaults()
        {
            _profile.VisualEnhancements.Effects["HitPoisonArea"] = new EffectDetailSettings
                { GroundEnabled = false, GroundSize = 130, GroundOpacity = 25, Intensity = 85 };
            string json = JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile)
                .VisualEnhancements.Effects["HitPoisonArea"];
            Assert.False(restored.GroundEnabled);
            Assert.Equal(130, restored.GroundSize);
            Assert.Equal(25, restored.GroundOpacity);
            Assert.Equal(85, restored.Intensity);
            var legacy = JsonSerializer.Deserialize<EffectDetailSettings>("{\"Intensity\":70,\"Glow\":90}");
            Assert.True(legacy.GroundEnabled);
            Assert.Equal(100, legacy.GroundSize);
            Assert.Equal(55, legacy.GroundOpacity);
        }
    }
}

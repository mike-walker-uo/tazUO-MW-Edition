using System;
using System.Runtime.Serialization;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class FireballGroundPolicyTests : IDisposable
    {
        private readonly Profile _previous = ProfileManager.CurrentProfile;
        private readonly Profile _profile = new Profile();

        public FireballGroundPolicyTests() =>
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _profile);

        public void Dispose() =>
            typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, _previous);

        [Theory]
        [InlineData(0u, 200f, 0f)]
        [InlineData(30u, 200f, 0.5f)]
        [InlineData(60u, 88f, 1f)]
        [InlineData(100u, 55f, 0.5f)]
        [InlineData(100u, 22f, 0f)]
        [InlineData(uint.MaxValue, 0f, 0f)]
        public void Glow_fades_at_launch_and_before_projectile_removal(uint elapsed, float distance, float expected) =>
            Assert.Equal(expected, MovingEffect.GroundFade(elapsed, distance));

        [Fact]
        public void Ground_pool_tracks_fractional_motion_and_stays_on_the_floor_when_projectile_rises()
        {
            var screen = new Point(300, 200);
            var offset = new Vector3(33f, 11f, -24f);
            Assert.Equal(new Point(101, 200), MovingEffect.GroundTile(100, 200, offset));
            Assert.Equal(new Vector2(355f, 265f), MovingEffect.GroundCenter(screen, offset, 10, 2));
            offset.Z = -80f;
            Assert.Equal(new Vector2(355f, 265f), MovingEffect.GroundCenter(screen, offset, 10, 2));
            Assert.True(MovingEffect.GroundStrength(20f) < MovingEffect.GroundStrength(0f));
            Assert.Equal(1f, MovingEffect.GroundStrength(-10f));
        }

        [Theory]
        [InlineData(false, 55, false, false, CombatVisualKind.Fireball)]
        [InlineData(true, 0, false, false, CombatVisualKind.Fireball)]
        [InlineData(true, 55, true, false, CombatVisualKind.Fireball)]
        [InlineData(true, 55, false, true, CombatVisualKind.Fireball)]
        [InlineData(true, 55, false, false, CombatVisualKind.MagicArrow)]
        public void Disabled_modes_and_other_projectiles_never_touch_graphics(bool enabled, int opacity,
            bool silence, bool classic, CombatVisualKind kind)
        {
            _profile.VisualEnhancements = new VisualEnhancementSettings
                { FireballGroundEnabled = enabled, FireballGroundOpacity = opacity };
            _profile.VisualSilence = silence;
            _profile.ClassicEffectsOnly = classic;
            var effect = (MovingEffect)FormatterServices.GetUninitializedObject(typeof(MovingEffect));
            effect.CombatVisual = kind;
            Assert.False(effect.DrawGround(null));
        }

        [Fact]
        public void Fireball_controls_round_trip_independently_of_lightning()
        {
            _profile.VisualEnhancements = new VisualEnhancementSettings
            {
                FireballGroundEnabled = false, FireballGroundSize = 150, FireballGroundOpacity = 40,
                LightningGroundEnabled = true, LightningGroundSize = 80, LightningGroundOpacity = 65
            };
            string json = JsonSerializer.Serialize(_profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile).VisualEnhancements;
            Assert.False(restored.FireballGroundEnabled);
            Assert.Equal(150, restored.FireballGroundSize);
            Assert.Equal(40, restored.FireballGroundOpacity);
            Assert.True(restored.LightningGroundEnabled);
            Assert.Equal(80, restored.LightningGroundSize);
            Assert.Equal(65, restored.LightningGroundOpacity);
        }
    }
}

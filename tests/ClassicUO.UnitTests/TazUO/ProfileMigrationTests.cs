using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ProfileMigrationTests
    {
        [Fact]
        public void ExistingProfilesReturnToModernGlobalChatDefault()
        {
            var profile = new Profile
            {
                UseNativeGlobalChatReplacement = false,
                NativeGlobalChatPreferenceVersion = 0
            };

            TazUOLifecycle.ApplyProfileMigrations(profile);

            profile.UseNativeGlobalChatReplacement.Should().BeTrue();
            profile.NativeGlobalChatPreferenceVersion.Should().Be(1);
        }

        [Fact]
        public void ExplicitVersionedPreferenceIsPreserved()
        {
            var profile = new Profile
            {
                UseNativeGlobalChatReplacement = false,
                NativeGlobalChatPreferenceVersion = 1,
                WaterStyleDefaultsVersion = 1
            };

            TazUOLifecycle.ApplyProfileMigrations(profile);

            profile.UseNativeGlobalChatReplacement.Should().BeFalse();
        }

        [Fact]
        public void NewProfilesDefaultToStormWater()
        {
            var profile = new Profile();

            profile.EnhancedWaterStyle.Should()
                .Be(WaterEnhancementManager.WATER_STYLE_STORM);
            profile.WaterMaterialIntensity.Should()
                .Be(WaterEnhancementManager.DEFAULT_INTENSITY_PERCENT);
        }

        [Fact]
        public void FormerNaturalWaterDefaultMigratesToStormOnce()
        {
            var profile = new Profile
            {
                EnhancedWaterStyle = WaterEnhancementManager.WATER_STYLE_NATURAL,
                WaterStyleDefaultsVersion = 0
            };

            TazUOLifecycle.ApplyProfileMigrations(profile);

            profile.EnhancedWaterStyle.Should()
                .Be(WaterEnhancementManager.WATER_STYLE_STORM);
            profile.WaterStyleDefaultsVersion.Should().Be(1);

            profile.EnhancedWaterStyle = WaterEnhancementManager.WATER_STYLE_NATURAL;
            TazUOLifecycle.ApplyProfileMigrations(profile);
            profile.EnhancedWaterStyle.Should()
                .Be(WaterEnhancementManager.WATER_STYLE_NATURAL);
        }

        [Fact]
        public void ExistingNonDefaultWaterStyleIsPreserved()
        {
            var profile = new Profile
            {
                EnhancedWaterStyle = WaterEnhancementManager.WATER_STYLE_MOONLIT,
                WaterStyleDefaultsVersion = 0
            };

            TazUOLifecycle.ApplyProfileMigrations(profile);

            profile.EnhancedWaterStyle.Should()
                .Be(WaterEnhancementManager.WATER_STYLE_MOONLIT);
            profile.WaterStyleDefaultsVersion.Should().Be(1);
        }

        [Fact]
        public void FormerWaterIntensityDefaultMigratesToTwoHundredOnce()
        {
            var profile = new Profile
            {
                WaterMaterialIntensity = 100,
                WaterIntensityDefaultsVersion = 0
            };

            TazUOLifecycle.ApplyProfileMigrations(profile);

            profile.WaterMaterialIntensity.Should()
                .Be(WaterEnhancementManager.DEFAULT_INTENSITY_PERCENT);
            profile.WaterIntensityDefaultsVersion.Should().Be(1);

            profile.WaterMaterialIntensity = 100;
            TazUOLifecycle.ApplyProfileMigrations(profile);
            profile.WaterMaterialIntensity.Should().Be(100);
        }

        [Fact]
        public void ExistingNonDefaultWaterIntensityIsPreserved()
        {
            var profile = new Profile
            {
                WaterMaterialIntensity = 150,
                WaterIntensityDefaultsVersion = 0
            };

            TazUOLifecycle.ApplyProfileMigrations(profile);

            profile.WaterMaterialIntensity.Should().Be(150);
            profile.WaterIntensityDefaultsVersion.Should().Be(1);
        }

        [Fact]
        public void FormerNoisyCombatOverlayDefaultsAreDisabledOnce()
        {
            var profile = new Profile { CombatOverlayDefaultsVersion = 0 };
            try
            {
                EffectsBundle.Crit = true;
                TargetingYouAura.Enabled = true;
                DamageSourceLineOverlay.Enabled = true;
                NearestHostileLine.Enabled = true;
                HealReceivedPulse.Enabled = true;

                TazUOLifecycle.ApplyCombatOverlayDefaults(profile);

                EffectsBundle.Crit.Should().BeFalse();
                TargetingYouAura.Enabled.Should().BeFalse();
                DamageSourceLineOverlay.Enabled.Should().BeFalse();
                NearestHostileLine.Enabled.Should().BeFalse();
                HealReceivedPulse.Enabled.Should().BeFalse();
                profile.CombatOverlayDefaultsVersion.Should().Be(3);

                EffectsBundle.Crit = true;
                TargetingYouAura.Enabled = true;
                DamageSourceLineOverlay.Enabled = true;
                NearestHostileLine.Enabled = true;
                HealReceivedPulse.Enabled = true;
                TazUOLifecycle.ApplyCombatOverlayDefaults(profile);

                EffectsBundle.Crit.Should().BeTrue();
                TargetingYouAura.Enabled.Should().BeTrue();
                DamageSourceLineOverlay.Enabled.Should().BeTrue();
                NearestHostileLine.Enabled.Should().BeTrue();
                HealReceivedPulse.Enabled.Should().BeTrue();
            }
            finally
            {
                EffectsBundle.Crit = false;
                TargetingYouAura.Enabled = false;
                DamageSourceLineOverlay.Enabled = false;
                NearestHostileLine.Enabled = false;
                HealReceivedPulse.Enabled = false;
            }
        }

        [Fact]
        public void LineDefaultsMigrationPreservesEarlierManualOverlayChoices()
        {
            var profile = new Profile { CombatOverlayDefaultsVersion = 1 };
            try
            {
                EffectsBundle.Crit = true;
                TargetingYouAura.Enabled = true;
                DamageSourceLineOverlay.Enabled = true;
                NearestHostileLine.Enabled = true;
                HealReceivedPulse.Enabled = true;

                TazUOLifecycle.ApplyCombatOverlayDefaults(profile);

                EffectsBundle.Crit.Should().BeTrue();
                TargetingYouAura.Enabled.Should().BeTrue();
                DamageSourceLineOverlay.Enabled.Should().BeFalse();
                NearestHostileLine.Enabled.Should().BeFalse();
                HealReceivedPulse.Enabled.Should().BeFalse();
                profile.CombatOverlayDefaultsVersion.Should().Be(3);
            }
            finally
            {
                EffectsBundle.Crit = false;
                TargetingYouAura.Enabled = false;
                DamageSourceLineOverlay.Enabled = false;
                NearestHostileLine.Enabled = false;
                HealReceivedPulse.Enabled = false;
            }
        }

        [Fact]
        public void FormerHealPulseDefaultIsDisabledOnce()
        {
            var profile = new Profile { CombatOverlayDefaultsVersion = 2 };
            try
            {
                DamageSourceLineOverlay.Enabled = true;
                NearestHostileLine.Enabled = true;
                HealReceivedPulse.Enabled = true;

                TazUOLifecycle.ApplyCombatOverlayDefaults(profile);

                DamageSourceLineOverlay.Enabled.Should().BeTrue();
                NearestHostileLine.Enabled.Should().BeTrue();
                HealReceivedPulse.Enabled.Should().BeFalse();
                profile.CombatOverlayDefaultsVersion.Should().Be(3);

                HealReceivedPulse.Enabled = true;
                TazUOLifecycle.ApplyCombatOverlayDefaults(profile);
                HealReceivedPulse.Enabled.Should().BeTrue();
            }
            finally
            {
                DamageSourceLineOverlay.Enabled = false;
                NearestHostileLine.Enabled = false;
                HealReceivedPulse.Enabled = false;
            }
        }
    }
}

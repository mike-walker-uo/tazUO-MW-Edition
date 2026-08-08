using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Data;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class CombatVisualEffectPolicyTests
    {
        [Theory]
        [InlineData(0x36E4, CombatVisualKind.MagicArrow)]
        [InlineData(0x36D4, CombatVisualKind.Fireball)]
        public void StandardHitSpellProjectilesAreClassified(
            ushort graphic,
            CombatVisualKind expected)
        {
            CombatVisualEffect.ClassifyMoving(graphic, 0).Should().Be(expected);
        }

        [Fact]
        public void MeteorSwarmDoesNotReceiveTheFireballTrail()
        {
            CombatVisualEffect
                .ClassifyMoving(0x36D4, 9501)
                .Should()
                .Be(CombatVisualKind.None);
            CombatVisualEffect
                .ClassifyMoving(0x36D4, 9502)
                .Should()
                .Be(CombatVisualKind.Fireball);
            CombatVisualEffect
                .ClassifyMoving(0x36D4, 0x49A)
                .Should()
                .Be(CombatVisualKind.None);
        }

        [Theory]
        [InlineData(0x374A, 1153, CombatVisualKind.Harm)]
        [InlineData(0x37BE, 0x0A, CombatVisualKind.LowerAttack)]
        [InlineData(0x37BE, 0x23, CombatVisualKind.LowerDefense)]
        [InlineData(0x3789, 0, CombatVisualKind.ManaDrain)]
        public void StandardFixedCombatSignaturesAreClassified(
            ushort graphic,
            ushort rawHue,
            CombatVisualKind expected)
        {
            CombatVisualEffect.ClassifyFixed(graphic, rawHue).Should().Be(expected);
        }

        [Theory]
        [InlineData(0x36E5, 0)]
        [InlineData(0x3779, 1160)]
        [InlineData(0x37BE, 0x30)]
        [InlineData(0x3789, 2062)]
        [InlineData(0x374A, 0)]
        [InlineData(0x374A, 97)]
        [InlineData(0x37C4, 0x66C)]
        public void UnrelatedFixedEffectsRemainGeneric(ushort graphic, ushort rawHue)
        {
            CombatVisualEffect
                .ClassifyFixed(graphic, rawHue)
                .Should()
                .Be(CombatVisualKind.None);
        }

        [Theory]
        [InlineData(CombatVisualKind.MagicArrow, true)]
        [InlineData(CombatVisualKind.LowerAttack, true)]
        [InlineData(CombatVisualKind.LowerDefense, true)]
        [InlineData(CombatVisualKind.ManaDrain, true)]
        [InlineData(CombatVisualKind.Harm, false)]
        [InlineData(CombatVisualKind.Fireball, true)]
        public void OnlyRequestedReplacementEffectsSuppressOriginalArt(
            CombatVisualKind kind,
            bool expected)
        {
            CombatVisualEffect.ReplacesOriginal(kind).Should().Be(expected);
        }

        [Fact]
        public void ReservedAuraVariantSelectorRetainsThreeAlternatives()
        {
            for (uint ticks = 0; ticks < 5000; ticks += 137)
            {
                CombatVisualEffect
                    .SelectReservedAuraVariant(0x12345678, 100, 200, ticks)
                    .Should()
                    .BeInRange(0, 2);
            }
        }

        [Theory]
        [InlineData(0x376A, 5048, true)]
        [InlineData(0x376A, 5029, false)]
        [InlineData(0x37CC, 5048, false)]
        public void NetherBlastVortexRequiresItsCreationParticle(
            ushort graphic,
            ushort particleEffect,
            bool expected)
        {
            NetherBlastVortexManager
                .IsFieldParticle(graphic, particleEffect)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(0x37CC, true)]
        [InlineData(0x3967, false)]
        [InlineData(0x3979, false)]
        public void NetherBlastFieldItemIsDistinctFromParalyzeField(
            ushort graphic,
            bool expected)
        {
            NetherBlastVortexManager.IsFieldItem(graphic).Should().Be(expected);
        }

        [Theory]
        [InlineData("off", 0)]
        [InlineData("void", 1)]
        [InlineData("cyclone", 2)]
        [InlineData("storm", 3)]
        [InlineData("rainbow", 4)]
        [InlineData("prismatic", 4)]
        [InlineData("4", 4)]
        [InlineData("pulse", 5)]
        [InlineData("netherwave", 5)]
        [InlineData("5", 5)]
        [InlineData("cosmic", 6)]
        [InlineData("portal", 6)]
        [InlineData("singularity", 6)]
        [InlineData("6", 6)]
        public void NetherBlastVortexStyleNamesAreAccepted(string value, int expected)
        {
            NetherBlastVortexManager.TryParseStyle(value, out int style).Should().BeTrue();
            style.Should().Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.Moving, 0x379F, 0, 3043, EnhancedSpellVisualKind.EnergyBolt)]
        [InlineData(GraphicEffectType.Moving, 0x379F, 0, 0, EnhancedSpellVisualKind.EnergyBolt)]
        [InlineData(GraphicEffectType.FixedFrom, 0x1B6C, 0x480, 0, EnhancedSpellVisualKind.Thunderstorm)]
        [InlineData(GraphicEffectType.Moving, 0x1363, 0, 0, EnhancedSpellVisualKind.Bombard)]
        [InlineData(GraphicEffectType.FixedFrom, 0x374A, 0x7A2, 5054, EnhancedSpellVisualKind.DeathRay)]
        [InlineData(GraphicEffectType.FixedFrom, 0x3779, 0x03, 0x26EC, EnhancedSpellVisualKind.WordOfDeath)]
        [InlineData(GraphicEffectType.Moving, 0x0F5F, 0x21, 0x251D, EnhancedSpellVisualKind.WordOfDeath)]
        [InlineData(GraphicEffectType.FixedFrom, 0x36BD, 0, 5044, EnhancedSpellVisualKind.Explosion)]
        [InlineData(GraphicEffectType.FixedXYZ, 0x36BD, 0, 5044, EnhancedSpellVisualKind.Explosion)]
        [InlineData(GraphicEffectType.Moving, 0x36D4, 9501, 0, EnhancedSpellVisualKind.MeteorSwarm)]
        [InlineData(GraphicEffectType.Moving, 0x36D4, 9501, 0x100, EnhancedSpellVisualKind.MeteorSwarm)]
        [InlineData(GraphicEffectType.Moving, 0xA1ED, 9501, 0, EnhancedSpellVisualKind.MeteorSwarm)]
        [InlineData(GraphicEffectType.Moving, 0x36D4, 0x49A, 0, EnhancedSpellVisualKind.NetherBolt)]
        [InlineData(GraphicEffectType.Moving, 0x407A, 0, 0, EnhancedSpellVisualKind.EagleStrike)]
        public void RequestedSpellPacketsHaveExactEnhancements(
            GraphicEffectType type,
            ushort graphic,
            ushort hue,
            ushort particle,
            EnhancedSpellVisualKind expected)
        {
            EnhancedSpellVisualPolicy
                .Classify(type, graphic, hue, particle)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(0x5CF, EnhancedSpellVisualKind.Wildfire)]
        [InlineData(0x64F, EnhancedSpellVisualKind.NetherCyclone)]
        [InlineData(0x5CE, EnhancedSpellVisualKind.None)]
        public void AreaSpellSoundsAreClassified(
            ushort sound,
            EnhancedSpellVisualKind expected)
        {
            EnhancedSpellVisualPolicy.ClassifySound(sound).Should().Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.Lightning, true)]
        [InlineData(GraphicEffectType.FixedFrom, false)]
        [InlineData(GraphicEffectType.Moving, false)]
        public void LightningPacketsUseTheSharedZeusRenderer(
            GraphicEffectType type,
            bool expected)
        {
            EnhancedSpellVisualPolicy.IsEnhancedLightning(type).Should().Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.FixedFrom, 0x0000, 2054, true)]
        [InlineData(GraphicEffectType.FixedFrom, 0x0000, 2055, false)]
        [InlineData(GraphicEffectType.FixedXYZ, 0x0000, 2054, false)]
        public void DeathRayCasterMarkerIsExact(
            GraphicEffectType type,
            ushort graphic,
            ushort particle,
            bool expected)
        {
            EnhancedSpellVisualPolicy
                .IsDeathRayCasterMarker(type, graphic, particle)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.FixedXYZ, 0x375A, 0x49A, true)]
        [InlineData(GraphicEffectType.FixedXYZ, 0x3779, 0x63, false)]
        [InlineData(GraphicEffectType.FixedFrom, 0x375A, 0x49A, false)]
        public void NetherCycloneRequiresItsAreaParticleAfterTheSharedSound(
            GraphicEffectType type,
            ushort graphic,
            ushort hue,
            bool expected)
        {
            EnhancedSpellVisualPolicy
                .IsNetherCycloneAreaParticle(type, graphic, hue)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.FixedXYZ, 0x3779, 0x63, true)]
        [InlineData(GraphicEffectType.FixedXYZ, 0x3779, 0x62, false)]
        [InlineData(GraphicEffectType.FixedFrom, 0x3779, 0x63, false)]
        [InlineData(GraphicEffectType.FixedXYZ, 0x375A, 0x49A, false)]
        public void HailStormRequiresItsAreaParticleAfterTheSharedSound(
            GraphicEffectType type,
            ushort graphic,
            ushort hue,
            bool expected)
        {
            EnhancedSpellVisualPolicy
                .IsHailStormAreaParticle(type, graphic, hue)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(0x398C, true)]
        [InlineData(0x3996, true)]
        [InlineData(0x3709, false)]
        public void WildfireRequiresARecentFieldItemNearTheSharedSound(
            ushort graphic,
            bool expected)
        {
            EnhancedSpellVisualPolicy
                .IsWildfireFieldGraphic(graphic)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.Moving, 0x379E, 0, 3043)]
        [InlineData(GraphicEffectType.Moving, 0x379F, 1, 3043)]
        [InlineData(GraphicEffectType.Moving, 0x379F, 0, 3044)]
        [InlineData(GraphicEffectType.FixedFrom, 0x1B6C, 0x481, 0)]
        [InlineData(GraphicEffectType.FixedFrom, 0x1B6C, 0x480, 1)]
        [InlineData(GraphicEffectType.FixedFrom, 0x3709, 0, 5052)]
        [InlineData(GraphicEffectType.FixedFrom, 0x3709, 2719, 5052)]
        [InlineData(GraphicEffectType.Moving, 0x1363, 1, 0)]
        [InlineData(GraphicEffectType.FixedFrom, 0x374A, 0x7A1, 5054)]
        [InlineData(GraphicEffectType.FixedFrom, 0x3779, 0x03, 5042)]
        [InlineData(GraphicEffectType.Moving, 0x0F5F, 0x22, 0x251D)]
        [InlineData(GraphicEffectType.FixedFrom, 0x36BD, 1, 5044)]
        [InlineData(GraphicEffectType.FixedFrom, 0x36BD, 0, 5045)]
        [InlineData(GraphicEffectType.Moving, 0x36BD, 0, 5044)]
        [InlineData(GraphicEffectType.Moving, 0x36D4, 9502, 0)]
        [InlineData(GraphicEffectType.Moving, 0xA1ED, 9502, 0)]
        [InlineData(GraphicEffectType.Moving, 0x36D4, 0x49A, 1)]
        [InlineData(GraphicEffectType.Moving, 0x36D4, 0x499, 0)]
        [InlineData(GraphicEffectType.Moving, 0x407A, 1, 0)]
        [InlineData(GraphicEffectType.FixedFrom, 0x407A, 0, 0)]
        public void NearMissSpellPacketsRemainGeneric(
            GraphicEffectType type,
            ushort graphic,
            ushort hue,
            ushort particle)
        {
            EnhancedSpellVisualPolicy
                .Classify(type, graphic, hue, particle)
                .Should()
                .Be(EnhancedSpellVisualKind.None);
        }

        [Fact]
        public void MeteorSwarmCreatesItsTargetBarrage()
        {
            EnhancedSpellVisualPolicy
                .CreatesImpact(EnhancedSpellVisualKind.MeteorSwarm)
                .Should()
                .BeTrue();
            EnhancedSpellVisualPolicy
                .ReplacesMovingProjectile(
                    EnhancedSpellVisualKind.MeteorSwarm
                )
                .Should()
                .BeTrue();
        }

        [Theory]
        [InlineData(55, true)]
        [InlineData(54, false)]
        [InlineData(56, false)]
        public void OnlyMeteorSwarmArmsItsCenterSound(
            int spell,
            bool expected)
        {
            EnhancedSpellVisualTrigger
                .IsMeteorSwarm(spell)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(0x160, true)]
        [InlineData(0x15F, false)]
        [InlineData(0x161, false)]
        public void MeteorSwarmCenterSoundIsRecognized(
            ushort sound,
            bool expected)
        {
            EnhancedSpellVisualTrigger
                .IsMeteorSwarmCenterSound(sound)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(605, true)]
        [InlineData(604, false)]
        [InlineData(720, false)]
        public void OnlyThunderstormArmsTheSharedSound(
            int spell,
            bool expected)
        {
            EnhancedSpellVisualTrigger
                .IsThunderstorm(spell)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(614, true)]
        [InlineData(613, false)]
        [InlineData(615, false)]
        public void OnlyWordOfDeathArmsItsImpactSound(
            int spell,
            bool expected)
        {
            EnhancedSpellVisualTrigger
                .IsWordOfDeath(spell)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(0x211, true)]
        [InlineData(0x210, false)]
        [InlineData(0x212, false)]
        public void WordOfDeathImpactSoundIsRecognized(
            ushort sound,
            bool expected)
        {
            EnhancedSpellVisualTrigger
                .IsWordOfDeathImpactSound(sound)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(GraphicEffectType.FixedFrom, 0x3779, true)]
        [InlineData(GraphicEffectType.FixedXYZ, 0x3779, true)]
        [InlineData(GraphicEffectType.Moving, 0x3779, false)]
        [InlineData(GraphicEffectType.FixedFrom, 0x3778, false)]
        public void WordOfDeathMarkerFallbackIsNarrowlyMatched(
            GraphicEffectType type,
            ushort graphic,
            bool expected)
        {
            EnhancedSpellVisualTrigger
                .IsWordOfDeathMarker(type, graphic)
                .Should()
                .Be(expected);
        }

        [Fact]
        public void WordOfDeathReplacesItsSmallMarkerAndCreatesItsTargetSkull()
        {
            EnhancedSpellVisualPolicy
                .CreatesImpact(EnhancedSpellVisualKind.WordOfDeath)
                .Should()
                .BeTrue();
            EnhancedSpellVisualPolicy
                .ReplacesMovingProjectile(
                    EnhancedSpellVisualKind.WordOfDeath
                )
                .Should()
                .BeTrue();
        }

        [Theory]
        [InlineData(EnhancedSpellVisualKind.EnergyBolt)]
        [InlineData(EnhancedSpellVisualKind.NetherBolt)]
        [InlineData(EnhancedSpellVisualKind.EagleStrike)]
        public void OtherEnhancedProjectilesRemainVisible(
            EnhancedSpellVisualKind kind)
        {
            EnhancedSpellVisualPolicy
                .ReplacesMovingProjectile(kind)
                .Should()
                .BeFalse();
        }

        [Theory]
        [InlineData(EnhancedSpellVisualKind.NetherBolt)]
        [InlineData(EnhancedSpellVisualKind.EagleStrike)]
        public void EnhancedProjectilesCreateTargetImpacts(
            EnhancedSpellVisualKind kind)
        {
            EnhancedSpellVisualPolicy.CreatesImpact(kind).Should().BeTrue();
        }
    }
}

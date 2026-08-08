using ClassicUO.Game;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class WaterEnhancementPolicyTests
    {
        [Theory]
        [InlineData(true, true, true, true, (byte)0)]
        [InlineData(false, true, true, true, WaterEnhancementManager.EDGE_NORTH)]
        [InlineData(true, false, true, false,
            (byte)(WaterEnhancementManager.EDGE_EAST | WaterEnhancementManager.EDGE_WEST))]
        [InlineData(false, false, false, false,
            (byte)(WaterEnhancementManager.EDGE_NORTH | WaterEnhancementManager.EDGE_EAST
            | WaterEnhancementManager.EDGE_SOUTH | WaterEnhancementManager.EDGE_WEST))]
        public void DryNeighboursBecomeShoreEdges(
            bool northWet, bool eastWet, bool southWet, bool westWet, byte expected)
        {
            WaterEnhancementManager.ComputeEdgeMask(northWet, eastWet, southWet, westWet)
                .Should().Be(expected);
        }

        [Theory]
        [InlineData(0, (byte)0)]
        [InlineData(1, (byte)0)]
        [InlineData(2, (byte)1)]
        [InlineData(5, (byte)4)]
        [InlineData(9, WaterEnhancementManager.MAX_VISUAL_DEPTH)]
        [InlineData(99, WaterEnhancementManager.MAX_VISUAL_DEPTH)]
        public void WaterDepthTracksDistanceFromShore(int firstDryRadius, byte expected)
        {
            WaterEnhancementManager.ComputeDepthFromFirstDryRadius(firstDryRadius)
                .Should().Be(expected);
        }

        [Theory]
        [InlineData(0f, 0.72f)]
        [InlineData(0.5f, 0.86f)]
        [InlineData(1f, 1f)]
        [InlineData(2f, 1f)]
        public void WaterRemainsVisibleAtLowAmbience(float ambience, float expected)
        {
            WaterEnhancementManager.CalculateVisibility(ambience).Should().BeApproximately(expected, 0.001f);
        }

        [Theory]
        [InlineData(800, 600, 1f, 0f, 0f, 22)]
        [InlineData(1680, 1240, 1f, 0f, 0f, 42)]
        [InlineData(3840, 2160, 1f, 0f, 0f, 91)]
        [InlineData(100, 100, 0.1f, 0f, 0f, 8)]
        [InlineData(800, 600, 1f, 44f, 44f, 24)]
        public void WaterCoverageIncludesTheViewportAndCameraOffset(
            int width, int height, float zoom, float offsetX, float offsetY, int expected)
        {
            WaterEnhancementManager.CalculateCoverageRadius(width, height, zoom, offsetX, offsetY)
                .Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 0f)]
        [InlineData(1, 0.18f)]
        [InlineData(2, 0.28f)]
        public void WaterMaterialQualityControlsMaximumMotion(
            int quality, float expectedMotionPixels)
        {
            WaterEnhancementManager.CalculateMotionPixels(quality)
                .Should().BeApproximately(expectedMotionPixels, 0.001f);
        }

        [Theory]
        [InlineData(0, 0f, 0f)]
        [InlineData(1, 0.18f, 0.034f)]
        [InlineData(2, 0.24f, 0.041f)]
        public void WaterMaterialBuildsStrengthGraduallyOffshore(
            int quality, float expectedShoreOpacity, float expectedDepthLayerOpacity)
        {
            WaterEnhancementManager.CalculateShoreArtworkOpacity(quality)
                .Should().BeApproximately(expectedShoreOpacity, 0.001f);
            WaterEnhancementManager.CalculateDepthLayerOpacity(quality)
                .Should().BeApproximately(expectedDepthLayerOpacity, 0.001f);
        }

        [Theory]
        [InlineData(0.24f, 0, 0f)]
        [InlineData(0.24f, 50, 0.12f)]
        [InlineData(0.24f, 100, 0.24f)]
        [InlineData(0.24f, 200, 0.48f)]
        [InlineData(0.24f, 300, 0.48f)]
        [InlineData(0.24f, -50, 0f)]
        public void WaterIntensityScalesMaterialOpacity(
            float opacity, int percent, float expected)
        {
            WaterEnhancementManager.ApplyIntensity(opacity, percent)
                .Should().BeApproximately(expected, 0.001f);
        }

        [Theory]
        [InlineData(0f, 0f, false, 0.25f)]
        [InlineData(4f, 0f, false, 0.60f)]
        [InlineData(0f, 1f, false, 0.50f)]
        [InlineData(4f, 1f, false, 0.85f)]
        [InlineData(-8f, 2f, true, 1f)]
        public void WaterMotionRespondsToWindAndWeather(
            float wind, float weatherIntensity, bool storm, float expected)
        {
            WaterEnhancementManager.CalculateMotionScale(wind, weatherIntensity, storm)
                .Should().BeApproximately(expected, 0.001f);
        }

        [Theory]
        [InlineData("natural", WaterEnhancementManager.WATER_STYLE_NATURAL)]
        [InlineData("WAVES", WaterEnhancementManager.WATER_STYLE_WAVES)]
        [InlineData(" choppy ", WaterEnhancementManager.WATER_STYLE_CHOPPY)]
        [InlineData("swell", WaterEnhancementManager.WATER_STYLE_SWELL)]
        [InlineData("gentle", WaterEnhancementManager.WATER_STYLE_SWELL)]
        [InlineData("Gentle Swell", WaterEnhancementManager.WATER_STYLE_SWELL)]
        [InlineData("storm", WaterEnhancementManager.WATER_STYLE_STORM)]
        [InlineData("Storm Swell", WaterEnhancementManager.WATER_STYLE_STORM)]
        [InlineData("moonlit", WaterEnhancementManager.WATER_STYLE_MOONLIT)]
        [InlineData("moon", WaterEnhancementManager.WATER_STYLE_MOONLIT)]
        [InlineData("Moonlit Swell", WaterEnhancementManager.WATER_STYLE_MOONLIT)]
        [InlineData("unknown", -1)]
        [InlineData("", -1)]
        public void WaterArtworkStylesParsePredictably(string value, int expected)
        {
            WaterEnhancementManager.ParseArtworkStyle(value).Should().Be(expected);
        }

        [Fact]
        public void StormWaterBecomesDeeperFasterAndFoamier()
        {
            WaterEnhancementManager.WaterEnvironment clear =
                WaterEnhancementManager.CalculateEnvironment(
                    null, false, false, false, 0f, 0f, 0f,
                    1f, 0f, 0f, 0f, 0f);
            WaterEnhancementManager.WaterEnvironment storm =
                WaterEnhancementManager.CalculateEnvironment(
                    WeatherType.WT_STORM_APPROACH, true, false, true, 1f, 1f, 0f,
                    0.30f, 0f, 0.20f, 0f, 4f);

            storm.DepthStrength.Should().BeGreaterThan(clear.DepthStrength);
            storm.MotionStrength.Should().BeGreaterThan(clear.MotionStrength);
            storm.WhitecapStrength.Should().BeGreaterThan(clear.WhitecapStrength);
            storm.GlintStrength.Should().BeLessThan(clear.GlintStrength);
            storm.OverlayStyle.Should().Be(WaterEnhancementManager.WATER_STYLE_STORM);
        }

        [Fact]
        public void NightAndFogChooseContinuousWaterAtmosphereProfiles()
        {
            WaterEnhancementManager.WaterEnvironment night =
                WaterEnhancementManager.CalculateEnvironment(
                    null, false, false, false, 0f, 0f, 0f,
                    0f, 0f, 1f, 1f, 0f);
            WaterEnhancementManager.WaterEnvironment fog =
                WaterEnhancementManager.CalculateEnvironment(
                    null, false, true, false, 1f, 0f, 0f,
                    0.45f, 0f, 0.20f, 0f, 0f);

            night.OverlayStyle.Should().Be(WaterEnhancementManager.WATER_STYLE_MOONLIT);
            night.GlintStrength.Should().BeGreaterThan(0f);
            fog.MistStrength.Should().BeGreaterThan(0.5f);
            fog.ReflectionStrength.Should().BeLessThan(night.ReflectionStrength);
        }
    }
}

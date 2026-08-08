using ClassicUO.Game;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class TerrainMaterialPolicyTests
    {
        [Theory]
        [InlineData(0x0016, "", false, 0f, ScenerySurface.Sand)]
        [InlineData(0x0018, "", false, 0f, ScenerySurface.Sand)]
        [InlineData(0x0005, "", false, 0f, ScenerySurface.Grass)]
        [InlineData(0x0000, "forest grass", false, 0f, ScenerySurface.Grass)]
        [InlineData(0x0000, "cave floor", true, 0f, ScenerySurface.Mine)]
        [InlineData(0x0000, "flagstone", true, 0f, ScenerySurface.Dungeon)]
        [InlineData(0x0000, "stone", false, 0f, ScenerySurface.Stone)]
        [InlineData(0x0016, "sand", false, 0.5f, ScenerySurface.Snow)]
        [InlineData(0x0000, "soil", false, 0f, ScenerySurface.Dirt)]
        public void LandMetadataSelectsExpectedMaterial(
            int graphic,
            string name,
            bool dungeon,
            float snowCover,
            ScenerySurface expected)
        {
            SceneryInteractionManager.ClassifyLandMaterial((ushort)graphic, name, dungeon, snowCover)
                .Should().Be(expected);
        }

        [Fact]
        public void RainDarkensDirtWithoutChangingLowQuality()
        {
            TerrainMaterialManager.CalculateOpacity(ScenerySurface.Dirt, 0, 1f, 0f)
                .Should().Be(0f);
            TerrainMaterialManager.CalculateOpacity(ScenerySurface.Dirt, 2, 1f, 0f)
                .Should().BeGreaterThan(
                    TerrainMaterialManager.CalculateOpacity(ScenerySurface.Dirt, 2, 0f, 0f));
        }

        [Theory]
        [InlineData(ScenerySurface.Sand, 0.62f)]
        [InlineData(ScenerySurface.Grass, 0.50f)]
        [InlineData(ScenerySurface.Mine, 0.64f)]
        [InlineData(ScenerySurface.Dungeon, 0.68f)]
        [InlineData(ScenerySurface.Dirt, 0.56f)]
        public void HighQualityMaterialStrengthIsVisiblyCalibrated(
            ScenerySurface surface, float expected)
        {
            TerrainMaterialManager.CalculateOpacity(surface, 2, 0f, 0f)
                .Should().BeApproximately(expected, 0.001f);
        }

        [Theory]
        [InlineData(0.20f, 0, 0f)]
        [InlineData(0.20f, 50, 0.10f)]
        [InlineData(0.20f, 100, 0.20f)]
        [InlineData(0.20f, 200, 0.40f)]
        [InlineData(0.20f, 300, 0.40f)]
        [InlineData(0.20f, -50, 0f)]
        public void TerrainIntensityScalesMaterialOpacity(
            float opacity, int percent, float expected)
        {
            TerrainMaterialManager.ApplyIntensity(opacity, percent)
                .Should().BeApproximately(expected, 0.001f);
        }

        [Theory]
        [InlineData(ScenerySurface.Sand, 4f, false, 1728u, 2)]
        [InlineData(ScenerySurface.Grass, 4f, false, 660u, 1)]
        [InlineData(ScenerySurface.Dirt, 4f, false, 1728u, 0)]
        [InlineData(ScenerySurface.Sand, 4f, true, 1728u, 0)]
        public void OnlyWindReactiveMaterialsMove(
            ScenerySurface surface,
            float wind,
            bool reducedMotion,
            uint ticks,
            int expected)
        {
            TerrainMaterialManager.CalculateMotionOffset(surface, wind, reducedMotion, ticks)
                .Should().Be(expected);
        }

        [Fact]
        public void RainTurnsDirtIntoDenserWetPatchesWithLessDust()
        {
            TerrainMaterialManager.TerrainEnvironment clear =
                TerrainMaterialManager.CalculateEnvironment(
                    ScenerySurface.Dirt, null, false, false, false,
                    0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f);
            TerrainMaterialManager.TerrainEnvironment rain =
                TerrainMaterialManager.CalculateEnvironment(
                    ScenerySurface.Dirt, WeatherType.WT_RAIN, true, false, false,
                    1f, 0f, 0f, 1f, 1f, 0f, 0f, 0f);

            rain.PatchDensity.Should().BeGreaterThan(clear.PatchDensity);
            rain.AirborneDust.Should().BeLessThan(clear.AirborneDust);
            rain.SparkleStrength.Should().BeGreaterThan(clear.SparkleStrength);
        }

        [Fact]
        public void StormAndSnowSelectDifferentTerrainDetailProfiles()
        {
            TerrainMaterialManager.TerrainEnvironment clearGrass =
                TerrainMaterialManager.CalculateEnvironment(
                    ScenerySurface.Grass, null, false, false, false,
                    0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f);
            TerrainMaterialManager.TerrainEnvironment stormGrass =
                TerrainMaterialManager.CalculateEnvironment(
                    ScenerySurface.Grass, WeatherType.WT_STORM_APPROACH, true, false, true,
                    1f, 0f, 0f, 1f, 0.35f, 0f, 0f, 0f);
            TerrainMaterialManager.TerrainEnvironment snow =
                TerrainMaterialManager.CalculateEnvironment(
                    ScenerySurface.Snow, WeatherType.WT_SNOW, true, false, false,
                    0f, 1f, 1f, 1f, 0.65f, 0f, 0f, 0f);

            stormGrass.DetailDensity.Should().BeLessThan(clearGrass.DetailDensity);
            stormGrass.ShadowStrength.Should().BeLessThan(clearGrass.ShadowStrength);
            snow.DetailDensity.Should().BeGreaterThan(1f);
            snow.WindParticles.Should().BeGreaterThan(1f);
        }
    }
}

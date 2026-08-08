using ClassicUO.Game;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class EnvironmentalShadowPolicyTests
    {
        [Theory]
        [InlineData(0, 0.95f, 0.05f)]
        [InlineData(7, 0.05f, 0.95f)]
        [InlineData(12, 0.00f, 0.95f)]
        public void ServerLightProducesExpectedDayOrDuskState(int light, float minimumDay, float minimumDuskOrNight)
        {
            EnvironmentalShadowManager.CalculateSolarState(light, out float day, out float dusk, out float night, out _);

            day.Should().BeGreaterOrEqualTo(minimumDay);
            System.Math.Max(dusk, night).Should().BeGreaterOrEqualTo(minimumDuskOrNight);
        }

        [Fact]
        public void DarkestServerLevelRepresentsMoonlessNight()
        {
            EnvironmentalShadowManager.CalculateSolarState(30, out _, out _, out float night, out float moon);

            night.Should().BeApproximately(1f, 0.001f);
            moon.Should().BeApproximately(0f, 0.001f);
        }

        [Theory]
        [InlineData(null, false, false, false, false, false, false, false, 1.00f)]
        [InlineData(WeatherType.WT_RAIN, true, false, false, false, false, false, false, 0.55f)]
        [InlineData(WeatherType.WT_STORM_APPROACH, true, false, false, false, true, false, false, 0.08f)]
        [InlineData(WeatherType.WT_SNOW, true, false, false, true, false, false, false, 0.10f)]
        [InlineData(null, false, true, false, false, false, false, false, 0.12f)]
        public void WeatherAttenuatesOutdoorShadows(
            WeatherType? type,
            bool active,
            bool fog,
            bool heavySnow,
            bool blizzard,
            bool tempest,
            bool hail,
            bool sleet,
            float expected)
        {
            EnvironmentalShadowManager.CalculateWeatherVisibility(
                type, active, fog, heavySnow, blizzard, tempest, hail, sleet
            ).Should().BeApproximately(expected, 0.001f);
        }

        [Theory]
        [InlineData(0.00, 0, DayCyclePhase.Day)]
        [InlineData(0.25, 6, DayCyclePhase.Dusk)]
        [InlineData(0.50, 30, DayCyclePhase.Night)]
        [InlineData(0.75, 6, DayCyclePhase.Dawn)]
        [InlineData(0.90, 0, DayCyclePhase.Day)]
        public void ContinuousDayCycleUsesDistinctDuskAndDawnPhases(
            double progress,
            int expectedLight,
            DayCyclePhase expectedPhase)
        {
            DayCyclePreviewManager.CalculateLight(progress, out DayCyclePhase phase)
                .Should().Be(expectedLight);
            phase.Should().Be(expectedPhase);
        }
    }
}

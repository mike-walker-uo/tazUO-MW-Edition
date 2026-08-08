using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class OverheadEffectSizeSettingsTests
    {
        [Theory]
        [InlineData("small", OverheadEffectSizePreset.Small, 0.25f)]
        [InlineData("normal", OverheadEffectSizePreset.Normal, 0.5f)]
        [InlineData("medium", OverheadEffectSizePreset.Normal, 0.5f)]
        [InlineData("large", OverheadEffectSizePreset.Large, 0.75f)]
        [InlineData("extralarge", OverheadEffectSizePreset.ExtraLarge, 1f)]
        [InlineData("xl", OverheadEffectSizePreset.ExtraLarge, 1f)]
        public void PresetNamesSelectExpectedScale(
            string value,
            OverheadEffectSizePreset expectedPreset,
            float expectedScale)
        {
            OverheadEffectSizeSettings
                .TryParse(value, out OverheadEffectSizePreset preset)
                .Should()
                .BeTrue();
            preset.Should().Be(expectedPreset);
            OverheadEffectSizeSettings
                .GetScale(preset)
                .Should()
                .BeApproximately(expectedScale, 0.001f);
        }

        [Fact]
        public void UnknownPresetIsRejected()
        {
            OverheadEffectSizeSettings
                .TryParse("huge", out _)
                .Should()
                .BeFalse();
        }
    }
}

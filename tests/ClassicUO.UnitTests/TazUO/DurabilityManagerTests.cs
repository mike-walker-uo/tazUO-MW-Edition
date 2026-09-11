using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class DurabilityManagerTests
    {
        [Theory]
        [InlineData("Durability ~1_val~ / ~2_val~", "Durability 26 / 248", 26, 248)]
        [InlineData("Haltbarkeit ~1_val~ / ~2_val~", "Haltbarkeit 71 / 239", 71, 239)]
        public void Parses_localized_durability_cliloc(string template, string data, int expectedCurrent, int expectedMaximum)
        {
            DurabilityManager.TryParseDurabilityValues(template, data, out int current, out int maximum)
                .Should().BeTrue();
            current.Should().Be(expectedCurrent);
            maximum.Should().Be(expectedMaximum);
        }

        [Theory]
        [InlineData("Charges 5 / 10")]
        [InlineData("Durability 250 / 100")]
        [InlineData("Durability 10 / 0")]
        public void Rejects_unrelated_or_invalid_ratios(string data)
        {
            DurabilityManager.TryParseDurabilityValues(
                "Durability ~1_val~ / ~2_val~",
                data,
                out _,
                out _).Should().BeFalse();
        }
    }
}

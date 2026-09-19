using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PetLoyaltyAlertManagerTests
    {
        [Theory]
        [InlineData("Hits: 120/120\nLoyalty: 89%\nDamage: 10", 89)]
        [InlineData("<basefont color=#ffffff>Loyalty: 90 / 100</basefont>", 90)]
        [InlineData("Loyalty: 100", 100)]
        [InlineData("Lykaon\n(bonded)\nLoyalty Rating: 100%\nLegendary", 100)]
        [InlineData("<basefont color=#ffffff>Loyalty Rating: 89%</basefont>", 89)]
        public void Parses_numeric_loyalty_property(string tooltip, int expected)
        {
            PetLoyaltyAlertManager.TryParseLoyalty(tooltip, out int value).Should().BeTrue();
            value.Should().Be(expected);
        }

        [Theory]
        [InlineData("Loyalty: Wonderfully Happy")]
        [InlineData("Loyalty: 101%")]
        [InlineData("Loyalty Rating: Legendary")]
        [InlineData("Hits: 89/100")]
        public void Rejects_non_numeric_or_unrelated_properties(string tooltip)
        {
            PetLoyaltyAlertManager.TryParseLoyalty(tooltip, out _).Should().BeFalse();
        }
    }
}

using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class SacredJourneyEffectTests
    {
        [Theory]
        [InlineData(210, true)]
        [InlineData(209, false)]
        [InlineData(207, false)]
        [InlineData(1, false)]
        public void OnlySacredJourneyArmsEffect(int spell, bool expected)
        {
            SacredJourneyEffect.IsSacredJourney(spell).Should().Be(expected);
        }

        [Theory]
        [InlineData(0x01FC, true)]
        [InlineData(0x01FB, false)]
        [InlineData(0x0055, false)]
        public void OnlyTravelSoundConfirmsEffect(int sound, bool expected)
        {
            SacredJourneyEffect
                .IsTravelConfirmation((ushort)sound)
                .Should()
                .Be(expected);
        }
    }
}

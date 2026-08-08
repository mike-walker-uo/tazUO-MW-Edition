using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class BandageSkillPolicyTests
    {
        [Theory]
        [InlineData(39.9f, false)]
        [InlineData(40.0f, true)]
        [InlineData(100.0f, true)]
        public void PlayerBandageStartsEnabledAtFortyHealing(float skill, bool expected)
        {
            AutoBandageManager.ShouldAutoEnable(skill).Should().Be(expected);
        }

        [Theory]
        [InlineData(39.9f, false)]
        [InlineData(40.0f, true)]
        [InlineData(100.0f, true)]
        public void PetBandageStartsEnabledAtFortyVeterinary(float skill, bool expected)
        {
            PetBandageManager.ShouldAutoEnable(skill).Should().Be(expected);
        }
    }
}

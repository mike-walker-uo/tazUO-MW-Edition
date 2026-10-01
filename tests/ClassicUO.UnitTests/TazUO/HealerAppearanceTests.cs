using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class HealerAppearanceTests
    {
        [Theory]
        [InlineData("the healer")]
        [InlineData("Wandering Healer")]
        [InlineData("John the wandering healer")]
        [InlineData("Jane the Healer")]
        [InlineData("John the evil wandering healer")]
        [InlineData("the Priest Of Mondain")]
        [InlineData("John a Gargish wandering healer")]
        [InlineData("<basefont color=#FFFF00>John the wandering healer</basefont>")]
        public void Recognizes_healer_npc_titles(string title)
        {
            HealerAppearanceManager.HasHealerTitle(title).Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("John")]
        [InlineData("John the blacksmith")]
        [InlineData("Grandmaster Healer")]
        [InlineData("Apprentice Healer")]
        [InlineData("Healer's Touch")]
        [InlineData("a healer robe")]
        [InlineData("Ask the healer for help")]
        public void Rejects_player_skill_titles_and_unrelated_names(string title)
        {
            HealerAppearanceManager.HasHealerTitle(title).Should().BeFalse();
        }
    }
}

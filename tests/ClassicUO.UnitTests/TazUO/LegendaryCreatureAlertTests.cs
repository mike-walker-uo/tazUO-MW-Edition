using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class LegendaryCreatureAlertTests
    {
        [Theory]
        [InlineData("* you sense a legendary creature has appeared nearby! *")]
        [InlineData("'you sense a legendary creature has appeared nearby!'")]
        [InlineData("Everyone senses a legendary creature nearby.")]
        [InlineData("EVERYONE SENSES A LEGENDARY CREATURE NEARBY!")]
        public void KnownShardMessagesTriggerAlert(string text)
        {
            LegendaryCreatureAlert
                .IsLegendarySpawnMessage(text)
                .Should()
                .BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("A legendary creature was tamed.")]
        [InlineData("You sense a creature nearby.")]
        [InlineData("Everyone senses danger nearby.")]
        public void UnrelatedMessagesDoNotTriggerAlert(string text)
        {
            LegendaryCreatureAlert
                .IsLegendarySpawnMessage(text)
                .Should()
                .BeFalse();
        }
    }
}

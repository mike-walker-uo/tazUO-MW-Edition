using ClassicUO.Game.Data;
using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PetCachePolicyTests
    {
        [Fact]
        public void DeadBondedPetsRemainAvailableForVeterinaryResurrection()
        {
            MobileCache.ShouldCachePet(false, true, true, NotorietyFlag.Innocent)
                .Should().BeTrue();
        }

        [Theory]
        [InlineData(true, false, true, NotorietyFlag.Innocent)]
        [InlineData(false, false, false, NotorietyFlag.Innocent)]
        [InlineData(false, false, true, NotorietyFlag.Enemy)]
        [InlineData(false, false, true, NotorietyFlag.Invulnerable)]
        public void NonPetsRemainExcluded(bool player, bool dead, bool renamable, NotorietyFlag notoriety)
        {
            MobileCache.ShouldCachePet(player, dead, renamable, notoriety).Should().BeFalse();
        }
    }
}

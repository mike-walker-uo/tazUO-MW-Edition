using ClassicUO.Game.Data;
using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class DivineFuryEffectTests
    {
        [Fact]
        public void DivineFuryBuffTriggersEffect()
        {
            DivineFuryEffect
                .IsDivineFury(BuffIconType.DivineFury)
                .Should()
                .BeTrue();
        }

        [Theory]
        [InlineData(BuffIconType.ConsecrateWeapon)]
        [InlineData(BuffIconType.EnemyOfOne)]
        [InlineData(BuffIconType.CurseWeapon)]
        public void OtherChivalryAndWeaponBuffsDoNotTriggerEffect(BuffIconType type)
        {
            DivineFuryEffect.IsDivineFury(type).Should().BeFalse();
        }
    }
}

using ClassicUO.Game.Data;
using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ConsecrateWeaponEffectTests
    {
        [Fact]
        public void ConsecrateWeaponBuffTriggersEffect()
        {
            ConsecrateWeaponEffect
                .IsConsecrateWeapon(BuffIconType.ConsecrateWeapon)
                .Should()
                .BeTrue();
        }

        [Theory]
        [InlineData(BuffIconType.DivineFury)]
        [InlineData(BuffIconType.EnemyOfOne)]
        [InlineData(BuffIconType.CurseWeapon)]
        public void OtherChivalryAndWeaponBuffsDoNotTriggerEffect(BuffIconType type)
        {
            ConsecrateWeaponEffect.IsConsecrateWeapon(type).Should().BeFalse();
        }
    }
}

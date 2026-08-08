using ClassicUO.Game.GameObjects;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class HitAreaEffectPolicyTests
    {
        [Theory]
        [InlineData(1160, 1)]
        [InlineData(2100, 2)]
        [InlineData(1166, 3)]
        [InlineData(120, 4)]
        public void StandardWeaponAreaProcHuesAreClassified(
            ushort rawHue,
            int expected)
        {
            HitAreaEffect.Classify(0x3779, rawHue).Should().Be((HitAreaElement)expected);
        }

        [Theory]
        [InlineData(0x3778, 1160)]
        [InlineData(0x3779, 50)]
        [InlineData(0x3779, 0)]
        public void UnrelatedEffectsRemainGeneric(ushort graphic, ushort rawHue)
        {
            HitAreaEffect.Classify(graphic, rawHue).Should().Be(HitAreaElement.None);
        }
    }
}

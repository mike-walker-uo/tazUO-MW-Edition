using ClassicUO.Game.GameObjects;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class LightningAnchorPolicyTests
    {
        [Fact]
        public void MobileLightningTargetsVisibleBodyCenter()
        {
            LightningEffect
                .CalculateMobileImpactPoint(
                    new Microsoft.Xna.Framework.Rectangle(100, 200, 40, 80),
                    0,
                    0
                )
                .Should()
                .Be(new Microsoft.Xna.Framework.Point(120, 240));
        }

        [Fact]
        public void MobileLightningHasStableFallbackBeforeFrameBoundsExist()
        {
            LightningEffect
                .CalculateMobileImpactPoint(
                    Microsoft.Xna.Framework.Rectangle.Empty,
                    100,
                    200
                )
                .Should()
                .Be(new Microsoft.Xna.Framework.Point(122, 168));
        }

        [Theory]
        [InlineData(0, 520)]
        [InlineData(30, 520)]
        [InlineData(64, 588)]
        [InlineData(120, 700)]
        public void SkyBoltHasBoundedDramaticLength(int visibleHeight, int expectedLength)
        {
            LightningEffect.CalculateBoltLength(visibleHeight).Should().Be(expectedLength);
        }

        [Theory]
        [InlineData(0, 1f)]
        [InlineData(1, 1f)]
        [InlineData(2, 0.68f)]
        [InlineData(3, 0.12f)]
        [InlineData(4, 0.82f)]
        [InlineData(6, 0.22f)]
        [InlineData(7, 0f)]
        public void SkyBoltUsesDoubleFlashTiming(int animationIndex, float expectedAlpha)
        {
            LightningEffect.CalculateBoltAlpha(animationIndex).Should().Be(expectedAlpha);
        }

        [Theory]
        [InlineData(0, 0.58f)]
        [InlineData(1, 4.2f)]
        [InlineData(2, 1.16f)]
        [InlineData(3, 0.34f)]
        [InlineData(4, 3.1f)]
        [InlineData(5, 1.22f)]
        [InlineData(6, 0.44f)]
        [InlineData(7, 0f)]
        public void SkyBoltUsesBriefLargeSizePulses(
            int animationIndex,
            float expectedScale)
        {
            LightningEffect.CalculateBoltSize(animationIndex).Should().Be(expectedScale);
        }
    }
}

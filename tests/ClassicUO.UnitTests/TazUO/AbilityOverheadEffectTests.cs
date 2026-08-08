using ClassicUO.Game.Data;
using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class AbilityOverheadEffectTests
    {
        [Theory]
        [InlineData(BuffIconType.EnemyOfOne, AbilityOverheadKind.EnemyOfOne)]
        [InlineData(BuffIconType.Confidence, AbilityOverheadKind.Confidence)]
        [InlineData(BuffIconType.Evasion, AbilityOverheadKind.Evasion)]
        [InlineData(BuffIconType.CounterAttack, AbilityOverheadKind.CounterAttack)]
        [InlineData(BuffIconType.LightningStrike, AbilityOverheadKind.None)]
        [InlineData(BuffIconType.MomentumStrike, AbilityOverheadKind.None)]
        public void BuffPolicyUsesActivationsButNotPreparedHits(
            BuffIconType buff,
            AbilityOverheadKind expected)
        {
            AbilityOverheadEffect.ClassifyBuff(buff).Should().Be(expected);
        }

        [Theory]
        [InlineData(201, 0x0208, AbilityOverheadKind.CleanseByFire)]
        [InlineData(202, 0x0202, AbilityOverheadKind.CloseWounds)]
        [InlineData(209, 0x00F6, AbilityOverheadKind.RemoveCurse)]
        [InlineData(209, 0x01F7, AbilityOverheadKind.RemoveCurse)]
        [InlineData(209, 0x01DF, AbilityOverheadKind.None)]
        [InlineData(201, 0x0202, AbilityOverheadKind.None)]
        public void TargetedSpellPolicyRequiresMatchingSuccessSound(
            int spell,
            int sound,
            AbilityOverheadKind expected)
        {
            AbilityOverheadEffect
                .ClassifyConfirmedSound(spell, (ushort)sound)
                .Should()
                .Be(expected);
        }

        [Theory]
        [InlineData(1063168, AbilityOverheadKind.LightningStrikeHit)]
        [InlineData(1063171, AbilityOverheadKind.MomentumStrikeHit)]
        [InlineData(1063167, AbilityOverheadKind.None)]
        [InlineData(1070757, AbilityOverheadKind.None)]
        public void BushidoHitPolicyRejectsReadyMessages(
            int cliloc,
            AbilityOverheadKind expected)
        {
            AbilityOverheadEffect
                .ClassifyHitMessage((uint)cliloc)
                .Should()
                .Be(expected);
        }
    }
}

using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class AutomationPolicyTests
    {
        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, true, false)]
        public void AutomaticTargetsNeverReplacePendingTargets(bool manual, bool queued, bool expected)
        {
            AutomationCoordinator.TargetIsAvailable(manual, queued).Should().Be(expected);
        }

        [Theory]
        [InlineData(999, 1000, false)]
        [InlineData(1000, 1000, true)]
        [InlineData(1001, 1000, true)]
        [InlineData(1001, 0, false)]
        public void QueuedAutomaticTargetsExpire(long now, long expiresAt, bool expected)
        {
            AutoTargetInfo.IsExpired(now, expiresAt).Should().Be(expected);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, false)]
        public void DeathPauseOnlyResumesPreviouslyEnabledAutomation(
            bool pausedForDeath,
            bool profileAllowsAutomation,
            bool expected)
        {
            AutoStopOnDeathManager.ShouldResumeAutomation(pausedForDeath, profileAllowsAutomation)
                .Should().Be(expected);
        }
    }
}

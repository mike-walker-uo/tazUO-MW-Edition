using ClassicUO.Game.Managers;
using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class HookIdempotenceTests
    {
        [Fact]
        public void RepeatedFeatureInitializationDoesNotDuplicateEventHooks()
        {
            DamageTypeTagManager.Hook();
            DamageTypeTagManager.Hook();
            ReflectCounterManager.Hook();
            ReflectCounterManager.Hook();
            HealReceivedPulse.Hook();
            HealReceivedPulse.Hook();

            DamageTypeTagManager.HookRegistrationCount.Should().Be(1);
            ReflectCounterManager.HookRegistrationCount.Should().Be(1);
            HealReceivedPulse.HookRegistrationCount.Should().Be(1);
        }
    }
}

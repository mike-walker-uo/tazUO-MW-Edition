using ClassicUO.Game.UI;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class OnslaughtDeliveryEffectTests
    {
        [Fact]
        public void DeliveryClilocTriggersEffect()
        {
            OnslaughtDeliveryEffect
                .IsConfirmedDelivery(OnslaughtDeliveryEffect.DELIVERY_CLILOC)
                .Should()
                .BeTrue();
        }

        [Fact]
        public void ReadyClilocDoesNotTriggerEffect()
        {
            OnslaughtDeliveryEffect
                .IsConfirmedDelivery(OnslaughtDeliveryEffect.READY_CLILOC)
                .Should()
                .BeFalse();
        }

        [Fact]
        public void UnrelatedClilocDoesNotTriggerEffect()
        {
            OnslaughtDeliveryEffect.IsConfirmedDelivery(0).Should().BeFalse();
        }
    }
}

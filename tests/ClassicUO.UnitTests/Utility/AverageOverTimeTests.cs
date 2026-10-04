using System;
using ClassicUO.Utility;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class AverageOverTimeTests
    {
        [Fact]
        public void Samples_expire_by_age_not_client_uptime()
        {
            var average = new AverageOverTime(TimeSpan.FromSeconds(15));
            average.AddValue(100_000, 10);
            average.AddValue(110_000, 30);

            Assert.Equal(20d, average.Average(115_000));
            Assert.Equal(30d, average.Average(115_001));
            Assert.Equal(0d, average.Average(125_001));
        }

        [Fact]
        public void Reading_DPS_after_combat_expires_old_damage()
        {
            var average = new AverageOverTime(TimeSpan.FromSeconds(15));
            average.AddValue(100_000, 100);

            Assert.Equal(100d, average.AveragePerSecond(100_000));
            Assert.Equal(10d, average.AveragePerSecond(110_000));
            Assert.Equal(0d, average.AveragePerSecond(115_001));
        }

        [Fact]
        public void Samples_survive_startup_and_expire_across_tick_wraparound()
        {
            var average = new AverageOverTime(TimeSpan.FromMilliseconds(1000));
            average.AddValue(0, 10);
            Assert.Equal(10d, average.LastAverage);

            var wrapped = new AverageOverTime(TimeSpan.FromMilliseconds(1000));
            wrapped.AddValue(uint.MaxValue - 499, 20);
            Assert.Equal(20d, wrapped.Average(500));
            Assert.Equal(0d, wrapped.Average(501));
        }
    }
}

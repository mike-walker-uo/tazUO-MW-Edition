using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Utility.ObjectPool
{
    public class ThreadSafety
    {
        [Fact]
        public void ConcurrentGetAndReturnDoesNotCorruptPool()
        {
            var pool = new ClassicUO.Utility.ObjectPool<object>(() => new object(), initialCapacity: 32)
            {
                MaxCapacity = 64
            };

            Parallel.For(0, 10_000, _ =>
            {
                object value = pool.Get();
                pool.Return(value);
            });

            pool.Count.Should().BeLessOrEqualTo(pool.MaxCapacity);
        }
    }
}

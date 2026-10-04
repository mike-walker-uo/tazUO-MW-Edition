using System.Linq;
using System.Reflection;
using ClassicUO.Utility.Collections;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class CollectionRetentionTests
    {
        [Fact]
        public void Bag_clear_releases_references_and_keeps_existing_capacity()
        {
            var bag = new Bag<object>(2);
            bag.Add(new object());
            bag.Add(new object());
            Assert.Equal(2, bag.Capacity);

            bag.Clear();

            Assert.Equal(0, bag.Count);
            Assert.Null(bag[0]);
            Assert.Null(bag[1]);
            bag.Add(new object());
            Assert.Equal(1, bag.Count);
        }

        [Fact]
        public void Deque_removals_release_references_and_preserve_wrapped_order()
        {
            for (int offset = 0; offset < 8; offset++)
            for (int start = 0; start < 8; start++)
            for (int count = 1; count <= 8 - start; count++)
            {
                var deque = new Deque<object>(8);
                for (int i = 0; i < offset; i++)
                {
                    deque.AddToBack(new object());
                    deque.RemoveFromFront();
                }
                var items = Enumerable.Range(0, 8).Select(_ => new object()).ToArray();
                foreach (object item in items) deque.AddToBack(item);

                deque.RemoveRange(start, count);

                Assert.Equal(items.Take(start).Concat(items.Skip(start + count)), deque);
                object[] buffer = Buffer(deque);
                foreach (object removed in items.Skip(start).Take(count))
                    Assert.DoesNotContain(removed, buffer);
                deque.Clear();
                Assert.All(buffer, Assert.Null);
                Assert.Empty(deque);
            }
        }

        [Fact]
        public void Removing_both_ends_releases_returned_objects()
        {
            var deque = new Deque<object>(4);
            var first = new object();
            var last = new object();
            deque.AddToBack(first);
            deque.AddToBack(last);

            Assert.Same(first, deque.RemoveFromFront());
            Assert.Same(last, deque.RemoveFromBack());
            Assert.All(Buffer(deque), Assert.Null);
        }

        private static object[] Buffer(Deque<object> deque) => (object[])typeof(Deque<object>)
            .GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(deque);
    }
}

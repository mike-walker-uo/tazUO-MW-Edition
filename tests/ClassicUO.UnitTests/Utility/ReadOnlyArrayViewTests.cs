using System;
using System.Linq;
using ClassicUO.Utility.Collections;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class ReadOnlyArrayViewTests
    {
        [Fact]
        public void Enumeration_includes_first_element_and_respects_slice()
        {
            var view = new ReadOnlyArrayView<int>(new[] { 10, 20, 30, 40 }, 1, 2);
            Assert.Equal(new[] { 20, 30 }, view.ToArray());
        }

        [Fact]
        public void Empty_view_never_advances_and_reset_restores_first_element()
        {
            var empty = new ReadOnlyArrayView<int>(new int[0], 0, 0).GetEnumerator();
            Assert.False(empty.MoveNext());
            Assert.False(empty.MoveNext());

            var enumerator = new ReadOnlyArrayView<int>(new[] { 10, 20 }, 1, 1).GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal(20, enumerator.Current);
            Assert.False(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
            enumerator.Reset();
            Assert.True(enumerator.MoveNext());
            Assert.Equal(20, enumerator.Current);
        }
    }
}

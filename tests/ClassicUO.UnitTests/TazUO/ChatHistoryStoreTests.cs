using System;
using System.Linq;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ChatHistoryStoreTests
    {
        private static ChatHistoryRecord Message => new ChatHistoryRecord(
            "Player", "Identical message", 0, DateTime.MinValue, MessageType.Regular);

        [Fact]
        public void Identical_messages_get_distinct_ordered_ids_and_eviction_precedes_notification()
        {
            var store = new ChatHistoryStore((MessageType _) => true, 2);
            long oldestAtNotification = 0;
            store.RecordAdded += _ => oldestAtNotification = store.OldestSequence;
            store.Add(Message);
            store.Add(Message);
            store.Add(Message);

            Assert.Equal(new long[] { 2, 3 }, store.All().Select(r => r.Sequence));
            Assert.Equal(2, oldestAtNotification);
        }

        [Fact]
        public void Long_sessions_keep_only_the_last_thousand_messages()
        {
            var store = new ChatHistoryStore((MessageType _) => true);
            for (int i = 0; i < 10000; i++) store.Add(Message);

            Assert.Equal(1000, store.Count);
            Assert.Equal(9001, store.OldestSequence);
            Assert.Equal(10000, store.All().Last().Sequence);
        }

        [Fact]
        public void Clear_notifies_views_and_does_not_reuse_record_ids()
        {
            var store = new ChatHistoryStore((MessageType _) => true, 2);
            int cleared = 0;
            store.HistoryCleared += () => cleared++;
            store.Add(Message);
            store.Clear();

            Assert.Equal(1, cleared);
            Assert.Empty(store.All());
            Assert.Equal(2, store.OldestSequence);
            store.Add(Message);
            Assert.Equal(2, store.All().Single().Sequence);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Nonpositive_capacity_is_rejected(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ChatHistoryStore((MessageType _) => true, capacity));
        }
    }
}

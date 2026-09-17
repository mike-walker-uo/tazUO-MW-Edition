using System.Collections.Generic;
using System.Text.Json;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class GridContainerSaveDataTests
    {
        [Fact]
        public void ReplaceSlotsKeepsCurrentCellsAndRemovesStaleItems()
        {
            var entry = new GridContainerEntry();
            entry.Slots[999] = new GridContainerSlotEntry
            {
                Serial = 999,
                Slot = 5,
                Locked = true
            };

            entry.ReplaceSlots
            (
                new Dictionary<int, uint>
                {
                    [2] = 100,
                    [5] = 200,
                    [9] = 999
                },
                new HashSet<uint> { 100, 200 },
                new HashSet<uint> { 200 }
            );

            entry.Slots.Keys.Should().BeEquivalentTo(100u, 200u);
            entry.Slots[100].Slot.Should().Be(2);
            entry.Slots[100].Locked.Should().BeFalse();
            entry.Slots[200].Slot.Should().Be(5);
            entry.Slots[200].Locked.Should().BeTrue();
        }

        [Fact]
        public void ItemCellsAndLocksRoundTripThroughJson()
        {
            var entry = new GridContainerEntry { Serial = 42 };
            entry.ReplaceSlots
            (
                new Dictionary<int, uint> { [7] = 100 },
                new HashSet<uint> { 100 },
                new HashSet<uint> { 100 }
            );

            string json = JsonSerializer.Serialize
            (
                new[] { entry },
                GridContainerSerializerContext.Default.GridContainerEntryArray
            );

            GridContainerEntry[] restored = JsonSerializer.Deserialize
            (
                json,
                GridContainerSerializerContext.Default.GridContainerEntryArray
            );

            restored.Should().ContainSingle();
            restored[0].Slots[100].Slot.Should().Be(7);
            restored[0].Slots[100].Locked.Should().BeTrue();
        }
    }
}

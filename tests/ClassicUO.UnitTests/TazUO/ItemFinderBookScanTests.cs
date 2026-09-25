using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ItemFinderBookScanTests
    {
        [Theory]
        [InlineData(0x2259)]
        [InlineData(0x22C5)]
        [InlineData(0x22C6)]
        [InlineData(0x9C16)]
        [InlineData(0x9C17)]
        public void Known_book_graphics_are_not_scanned_as_containers(int graphic)
        {
            ItemFinderManager.IsBookGraphic((ushort)graphic).Should().BeTrue();
        }

        [Theory]
        [InlineData("Bulk Order Book", true)]
        [InlineData("a spellbook", true)]
        [InlineData("Runebook", true)]
        [InlineData("Book of Lore", true)]
        [InlineData("Runic Atlas", true)]
        [InlineData("Wooden Bookcase", false)]
        [InlineData("Treasure Chest", false)]
        public void Only_book_item_names_are_excluded(string name, bool expected)
        {
            ItemFinderManager.IsBookName(name).Should().Be(expected);
        }
    }
}

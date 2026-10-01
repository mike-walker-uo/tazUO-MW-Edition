using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game.UI.Gumps;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class WorldExplorerAtlasTests
    {
        [Theory]
        [InlineData(46, 55, 20)]
        [InlineData(70, 85, 30)]
        public void Atlas_rows_follow_columns_even_when_positions_and_control_order_change(int x, int y, int spacing)
        {
            Point[] rows = Enumerable.Range(0, 16)
                .Select(slot => new Point(x + slot / 8 * 205, y + slot % 8 * spacing)).ToArray();
            var controls = new List<Point>(rows.Reverse());
            // Rename shares a column in some layouts; actions sit beneath the rune rows.
            controls.Add(new Point(x + 205, y - 41));
            controls.Add(new Point(x, y + 8 * spacing + 60));
            controls.Add(new Point(x, y + 8 * spacing + 80));
            controls.Add(new Point(x, y + 8 * spacing + 100));

            WorldExplorerGump.AtlasRowOrder(controls).Select(i => controls[i]).Should().Equal(rows);
        }

        [Fact]
        public void Missing_row_does_not_shift_destination_slots()
        {
            Point[] rows = Enumerable.Range(0, 16).Where(slot => slot != 3)
                .Select(slot => new Point(46 + slot / 8 * 205, 55 + slot % 8 * 20)).ToArray();

            WorldExplorerGump.AtlasRowOrder(rows).Should().BeEmpty();
        }

        [Fact]
        public void Misaligned_columns_do_not_form_an_atlas()
        {
            Point[] rows = Enumerable.Range(0, 16)
                .Select(slot => new Point(46 + slot / 8 * 205, 55 + slot % 8 * 20 + slot / 8)).ToArray();

            WorldExplorerGump.AtlasRowOrder(rows).Should().BeEmpty();
        }
    }
}

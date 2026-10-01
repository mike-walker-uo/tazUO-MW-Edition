using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Gumps;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class GridEquipmentLayerSortTests
    {
        [Fact]
        public void Weapons_and_clothing_group_by_equipment_slot_before_other_items()
        {
            var items = new[]
            {
                new StaticTiles { Name = "Reagent", Flags = TileFlag.None, Layer = 0 },
                new StaticTiles { Name = "Robe", Flags = TileFlag.Wearable, Layer = (byte)Layer.Robe },
                new StaticTiles { Name = "Boots", Flags = TileFlag.Wearable, Layer = (byte)Layer.Shoes },
                new StaticTiles { Name = "Sword", Flags = TileFlag.Weapon, Layer = (byte)Layer.OneHanded },
                new StaticTiles { Name = "Bracelet", Flags = TileFlag.Wearable, Layer = (byte)Layer.Bracelet }
            };

            items.OrderBy(GridContainer.GridSlotManager.EquipmentLayerSortKey).Select(item => item.Name)
                .Should().Equal("Sword", "Boots", "Bracelet", "Robe", "Reagent");
        }

        [Theory]
        [InlineData(TileFlag.None, Layer.OneHanded)]
        [InlineData(TileFlag.Wearable, Layer.Invalid)]
        [InlineData(TileFlag.Wearable, Layer.Backpack)]
        [InlineData(TileFlag.Wearable, Layer.Mount)]
        public void Non_equipment_metadata_sorts_after_equipment(TileFlag flags, Layer layer)
        {
            var data = new StaticTiles { Flags = flags, Layer = (byte)layer };

            GridContainer.GridSlotManager.EquipmentLayerSortKey(data).Should().Be(int.MaxValue);
        }
    }
}

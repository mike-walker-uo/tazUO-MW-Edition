using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PaperdollRefreshTests
    {
        private static readonly FieldInfo Dirty = typeof(PaperDollInteractable).GetField("_updateUI", BindingFlags.Instance | BindingFlags.NonPublic);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Equipment_refresh_survives_an_unchanged_drag_preview_state(bool preview)
        {
            var doll = new PaperDollInteractable(0, 0, 1, null);
            doll.SetFakeItem(preview);
            Dirty.SetValue(doll, false); // Previous visual update consumed.
            doll.RequestUpdate();
            doll.SetFakeItem(preview);
            Assert.True((bool)Dirty.GetValue(doll));
        }
        [Fact]
        public void Repeated_mouse_exit_keeps_the_pending_preview_removal()
        {
            var doll = new PaperDollInteractable(0, 0, 1, null);
            doll.SetFakeItem(true);
            Dirty.SetValue(doll, false);
            doll.SetFakeItem(false);
            doll.SetFakeItem(false);
            Assert.False(doll.HasFakeItem);
            Assert.True((bool)Dirty.GetValue(doll));
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Unchanged_preview_without_an_equipment_change_does_not_rebuild_each_frame(bool preview)
        {
            var doll = new PaperDollInteractable(0, 0, 1, null);
            doll.SetFakeItem(preview);
            Dirty.SetValue(doll, false);
            doll.SetFakeItem(preview);
            Assert.False((bool)Dirty.GetValue(doll));
        }
        [Fact]
        public void Weapon_graphic_is_invalid_as_soon_as_the_weapon_moves_into_a_backpack()
        {
            var weapon = (Item)FormatterServices.GetUninitializedObject(typeof(Item));
            weapon.Layer = Layer.OneHanded; weapon.Container = 1;
            Assert.True(PaperDollInteractable.MatchesEquipment(weapon, 1, Layer.OneHanded));
            weapon.Container = 0x40001000;
            Assert.False(PaperDollInteractable.MatchesEquipment(weapon, 1, Layer.OneHanded));
            weapon.Container = 1; weapon.Layer = Layer.TwoHanded;
            Assert.False(PaperDollInteractable.MatchesEquipment(weapon, 1, Layer.OneHanded));
            Assert.True(PaperDollInteractable.MatchesEquipment(weapon, 1, Layer.TwoHanded));
            Assert.False(PaperDollInteractable.MatchesEquipment(weapon, 2, Layer.TwoHanded));
            Assert.False(PaperDollInteractable.MatchesEquipment(null, 1, Layer.TwoHanded));
        }
    }
}

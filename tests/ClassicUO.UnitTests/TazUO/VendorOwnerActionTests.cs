using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class VendorOwnerActionTests
    {
        [Fact]
        public void Cancel_and_sales_history_keep_distinct_server_actions_when_skinned()
        {
            var gump = new Gump(0, 1);
            var history = Action(12, 300);
            var cancel = Action(0, 330);
            gump.Children.Add(history);
            gump.Children.Add(cancel);
            gump.Children.Add(Caption("View Sales History", 300));
            gump.Children.Add(Caption("CANCEL", 330));

            Assert.Same(history, VendorRefreshManager.Action(gump, "View Sales History"));
            history.IsVisible = false; // Skinning hides originals, but refresh still uses their reply IDs.
            Assert.Same(cancel, VendorRefreshManager.Action(gump, "CANCEL"));
            Assert.Same(history, VendorRefreshManager.Action(gump, "View Sales History"));
            cancel.ButtonAction = ButtonAction.SwitchPage;
            history.ButtonAction = ButtonAction.SwitchPage;
            Assert.Null(VendorRefreshManager.Action(gump, "CANCEL"));
        }

        [Fact]
        public void Tall_caption_bounds_do_not_shift_actions_to_the_next_button_row()
        {
            var gump = new Gump(0, 1);
            var goods = Action(1, 24);
            var customize = Action(2, 44);
            var rename = Action(3, 64);
            var caption = Caption("See goods", 21);
            caption.Height = 80;
            gump.Children.Add(goods); gump.Children.Add(customize); gump.Children.Add(rename); gump.Children.Add(caption);
            Assert.Same(goods, VendorRefreshManager.Action(gump, "See goods"));
            goods.ButtonAction = ButtonAction.SwitchPage;
            Assert.Null(VendorRefreshManager.Action(gump, "See goods"));
        }

        private static Button Action(int id, int y)
        {
            var button = (Button)FormatterServices.GetUninitializedObject(typeof(Button));
            typeof(Button).GetField("<ButtonID>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(button, id);
            button.IsFromServer = true; button.ButtonAction = ButtonAction.Activate;
            button.X = 600; button.Y = y; button.Width = 20; button.Height = 20;
            return button;
        }

        private static Label Caption(string text, int y)
        {
            // Exercise real action matching without loading fonts or a graphics device.
            var rendered = (RenderedText)FormatterServices.GetUninitializedObject(typeof(RenderedText));
            typeof(RenderedText).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rendered, text);
            var label = (Label)FormatterServices.GetUninitializedObject(typeof(Label));
            typeof(Label).GetField("_gText", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(label, rendered);
            label.IsFromServer = true; label.X = 630; label.Y = y; label.Width = 170; label.Height = 20;
            return label;
        }
    }
}

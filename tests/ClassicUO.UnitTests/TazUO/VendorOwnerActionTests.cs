using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
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

        [Theory]
        [InlineData(true, 2, 12)]
        [InlineData(false, 6, 1)]
        public void Refresh_prefers_history_but_uses_goods_when_history_is_unavailable(bool history, int step, int button)
        {
            VendorRefreshManager.Reset();
            try
            {
                var record = new VendorLedgerRecord { ShopName = "Shared vendor" };
                var gump = new Gump(0, 1);
                gump.Children.Add(Action(1, 24));
                gump.Children.Add(Caption("See goods", 24));
                if (history)
                {
                    gump.Children.Add(Action(12, 64));
                    gump.Children.Add(Caption("View Sales History", 64));
                }
                RefreshField("_current").SetValue(null, record);
                RefreshField("_step").SetValue(null, 1);
                RefreshField("_partial").SetValue(null, false); // NextVendor normally initializes this flag.
                long before = Time.Ticks;
                VendorRefreshManager.ObserveGump(gump, VendorGumpKind.Owner, record);

                Assert.Equal(step, (int)RefreshField("_step").GetValue(null));
                Assert.Equal(button, (int)RefreshField("_button").GetValue(null));
                Assert.Equal(!history, (bool)RefreshField("_partial").GetValue(null));
                Assert.InRange((long)RefreshField("_deadline").GetValue(null), before + 5000, (long)Time.Ticks + 5000);
            }
            finally { VendorRefreshManager.Reset(); RefreshField("_partial").SetValue(null, false); }
        }

        private static FieldInfo RefreshField(string name) => typeof(VendorRefreshManager).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);

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

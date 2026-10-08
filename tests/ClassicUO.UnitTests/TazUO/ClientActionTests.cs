using System;
using System.IO;
using System.Text;
using System.Xml;
using ClassicUO.Game.Managers;
using SDL3;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ClientActionTests
    {
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
        public void Every_action_survives_save_with_Unicode_and_XML_sensitive_names(int kind)
        {
            var action = new ClientAction((ClientActionKind)kind, 12, "Mage: Änne's <gear> & shield");
            Assert.True(ClientAction.TryParse(action.Serialize(), out ClientAction restored));
            Assert.True(action.SameTarget(restored));
            Assert.Equal(action.Name, restored.Name);
            Assert.Equal(action.ShortcutName, restored.ShortcutName);
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData("99:0:")]
        [InlineData("0:-1:")] [InlineData("1:0:bad base64!")] [InlineData("1:abc:")]
        public void Bad_action_payloads_are_rejected(string text) => Assert.False(ClientAction.TryParse(text, out _));

        [Fact]
        public void Localized_labels_do_not_change_spell_shortcut_identity()
        {
            var before = new ClientAction(ClientActionKind.Spell, 4, "Heal");
            var after = new ClientAction(ClientActionKind.Spell, 4, "Heilen");
            Assert.True(before.SameTarget(after));
            Assert.Equal(before.ShortcutName, after.ShortcutName);
            Assert.False(before.SameTarget(new ClientAction(ClientActionKind.Skill, 4, "Heal")));
            Assert.False(new ClientAction(ClientActionKind.Loadout, 0, "Mage").SameTarget(new ClientAction(ClientActionKind.Loadout, 0, "Warrior")));
        }

        [Fact]
        public void Shortcut_uses_existing_macro_XML_and_preserves_the_action_and_key()
        {
            var action = new ClientAction(ClientActionKind.Loadout, 0, "Mage <A&B>");
            var macro = new Macro(action.ShortcutName, SDL.SDL_Keycode.SDLK_F5, false, true, true)
            { Items = new MacroObjectString(MacroType.ContextAction, MacroSubType.MSC_NONE, action.Serialize()) };
            var text = new StringBuilder();
            using (var writer = new XmlTextWriter(new StringWriter(text))) { macro.Save(writer); }
            var xml = new XmlDocument(); xml.LoadXml(text.ToString());
            var restored = new Macro(xml.DocumentElement.GetAttribute("name")); restored.Load(xml.DocumentElement);
            Assert.Equal(macro.Key, restored.Key); Assert.True(restored.Ctrl); Assert.True(restored.Shift);
            var saved = Assert.IsType<MacroObjectString>(restored.Items);
            Assert.Equal(MacroType.ContextAction, saved.Code);
            Assert.True(ClientAction.TryParse(saved.Text, out ClientAction restoredAction));
            Assert.True(action.SameTarget(restoredAction));
        }

        [Fact]
        public void Existing_macro_codes_stay_unchanged_and_new_action_uses_a_string_payload()
        {
            Assert.Equal(79, (int)MacroType.UseCounterBar);
            Assert.Equal(100, (int)MacroType.RazorEnhancedHotkey);
            Assert.IsType<MacroObjectString>(Macro.Create(MacroType.ContextAction));
        }
    }
}

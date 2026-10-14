using System;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ExplorationChatTests : IDisposable
    {
        private readonly bool _previousMode = ExplorationMode.Active;
        private static readonly PropertyInfo ActiveMode = typeof(ExplorationMode).GetProperty(nameof(ExplorationMode.Active), BindingFlags.Static | BindingFlags.NonPublic);
        private readonly SystemChatControl _chat = (SystemChatControl)FormatterServices.GetUninitializedObject(typeof(SystemChatControl));
        private readonly StbTextBox _input = (StbTextBox)FormatterServices.GetUninitializedObject(typeof(StbTextBox));
        private readonly AlphaBlendControl _background = (AlphaBlendControl)FormatterServices.GetUninitializedObject(typeof(AlphaBlendControl));
        private static readonly FieldInfo ChatActive = typeof(SystemChatControl).GetField("_isActive", BindingFlags.Instance | BindingFlags.NonPublic);
        public ExplorationChatTests()
        {
            // Visibility/state checks need no font assets, renderer or client controller.
            typeof(SystemChatControl).GetField(nameof(SystemChatControl.TextBoxControl)).SetValue(_chat, _input);
            typeof(SystemChatControl).GetField("_trans", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_chat, _background);
        }
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void Camera_controls_keep_message_field_visible_without_enabling_typing(bool editing, bool exploration, bool visible)
        {
            ActiveMode.SetValue(null, exploration);
            ChatActive.SetValue(_chat, editing);
            _input.IsEditable = editing;
            _chat.RefreshInputVisibility();
            Assert.Equal(visible, _input.IsVisible);
            Assert.Equal(visible, _background.IsVisible);
            Assert.Equal(editing, _input.IsEditable);
        }
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void Returning_restores_previous_editing_or_retains_chat_opened_during_flight(bool previousEditing, bool currentEditing, bool expected)
        {
            ActiveMode.SetValue(null, false);
            ChatActive.SetValue(_chat, currentEditing);
            _chat.RestoreExplorationChat(previousEditing);
            Assert.Equal(expected, _chat.IsActive);
            Assert.Equal(expected, _input.IsVisible);
            Assert.Equal(expected, _input.IsEditable);
            Assert.Equal(expected, _background.IsVisible);
        }
        public void Dispose() => ActiveMode.SetValue(null, _previousMode);
    }
}

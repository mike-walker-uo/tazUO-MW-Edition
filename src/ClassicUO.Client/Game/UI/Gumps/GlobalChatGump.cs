#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System.Xml;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Persistent history for public chat channels, guild and party messages.
    /// Subscribed lazily on first access; survives gump open/close.
    /// </summary>
    internal static class GlobalChatHistory
    {
        public static readonly ChatHistoryStore Instance =
            new ChatHistoryStore((MessageEventArgs e) => GlobalChatChannels.IsChatMessage(e), 1000, GlobalChatChannels.RepeatKey);

        private static bool _autoOpenHooked;

        public static void EnsureHooked()
        {
            if (!_autoOpenHooked)
            {
                Instance.RecordAdded += record =>
                {
                    var profile = ProfileManager.CurrentProfile;
                    if (!GlobalChatChannels.Matches(record, GlobalChatChannel.Global,
                        profile?.GlobalChatIncludeGuild == true, profile?.GlobalChatIncludeParty == true)) return;
                    if (World.Player != null && ProfileManager.CurrentProfile?.AutoOpenGlobalChat == true &&
                        UIManager.GetGump<GlobalChatGump>() == null)
                        UIManager.Add(new GlobalChatGump());
                };
                _autoOpenHooked = true;
            }

            Instance.EnsureHooked();
        }
    }

    internal class GlobalChatGump : BaseChatGump
    {
        private static int _lastX = 200, _lastY = 200;
        private static int _lastW = DEFAULT_WIDTH, _lastH = DEFAULT_HEIGHT;
        private static int _fontSizeOverride = 0;
        private readonly Combobox _viewChannel;
        private readonly Combobox _sendChannel;

        public GlobalChatGump() : this(_lastX, _lastY) { }

        public GlobalChatGump(int x, int y)
            : base(x, y, _lastW, _lastH, _fontSizeOverride,
                   "Chat", "Send: [c",
                   GlobalChatHistory.Instance, null,
                   extraHeaderHeight: 54, colorChannelTags: true)
        {
            int left = PADDING + CustomThemeArt.ContentInset;
            Add(new Label("View:", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = left, Y = ExtraHeaderY + 4 });
            Add(_viewChannel = new Combobox(left + 36, ExtraHeaderY, 110, GlobalChatChannels.SelectionNames, 0,
                itemColor: index => GlobalChatChannels.DisplayColor(GlobalChatChannels.Selectable[index], ProfileManager.CurrentProfile, true)));
            Add(new Label("Send:", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = left + 154, Y = ExtraHeaderY + 4 });
            Add(_sendChannel = new Combobox(left + 194, ExtraHeaderY, 110, GlobalChatChannels.SelectionNames, 0,
                itemColor: index => GlobalChatChannels.DisplayColor(GlobalChatChannels.Selectable[index], ProfileManager.CurrentProfile, true)));
            _viewChannel.OnOptionSelected += (_, selected) =>
            {
                RefreshFilter();
                _sendChannel.SelectedIndex = selected;
            };
            _sendChannel.OnOptionSelected += (_, selected) =>
                SetSendPrompt("Send: " + GlobalChatChannels.Command(GlobalChatChannels.Selectable[selected]));
            _viewChannel.SetTooltip("Global combines public channels, including Pariah. Settings can also include your Guild and Party messages.");
            _sendChannel.SetTooltip("Channel used when you press Enter. An explicit channel command overrides this selection.");
            AddMenuButton(left, "Commands", 10);
            AddMenuButton(left + 100, "Settings", 11);
            RefreshFilter();
            RefreshChatAppearance();
        }

        protected override int MinimumWidth => 400;

        private void AddMenuButton(int x, string text, int id)
        {
            var button = new NiceButton(x, ExtraHeaderY + 28, 92, 20, ButtonAction.Activate, text)
            {
                IsSelectable = false, ButtonParameter = id
            };
            CustomGumpThemeManager.StyleDataButton(button);
            Add(button);
        }

        private void RefreshFilter()
        {
            SetRecordFilter(record => GlobalChatChannels.Matches(record, GlobalChatChannels.Selectable[_viewChannel.SelectedIndex],
                ProfileManager.CurrentProfile?.GlobalChatIncludeGuild == true,
                ProfileManager.CurrentProfile?.GlobalChatIncludeParty == true));
        }

        internal void SelectChannel(GlobalChatChannel channel)
        {
            _viewChannel.SelectedIndex = GlobalChatChannels.SelectionIndex((int)channel);
        }

        internal void SelectSendChannel(GlobalChatChannel channel)
        {
            _sendChannel.SelectedIndex = GlobalChatChannels.SelectionIndex((int)channel);
            FocusInput();
        }

        internal void RefreshSettings()
        {
            RefreshChatAppearance();
            _viewChannel.RefreshSelectedText();
            _sendChannel.RefreshSelectedText();
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 10 || buttonID == 11)
            {
                UIManager.GetGump<GlobalChatMenuGump>()?.Dispose();
                UIManager.Add(new GlobalChatMenuGump(this, buttonID == 11));
                return;
            }
            base.OnButtonClick(buttonID);
        }

        protected override void SendMessage(string text)
        {
            GlobalChatChannel channel = GlobalChatChannels.Selectable[_sendChannel.SelectedIndex];
            if (GlobalChatChannels.TryReadCommand(text, out GlobalChatChannel explicitChannel, out string message))
            {
                channel = explicitChannel;
                SelectSendChannel(channel);
                text = message;
                if (string.IsNullOrWhiteSpace(text)) return;
            }
            if (channel == GlobalChatChannel.Party)
            {
                if (World.Party.Leader != 0)
                    GameActions.SayParty(text);
                else
                    GameActions.Print("You are not in a party.", 0x21);
            }
            else if (channel == GlobalChatChannel.Guild)
                GameActions.Say(text, ProfileManager.CurrentProfile?.GuildMessageHue ?? (ushort)0x0044, MessageType.Guild);
            else
                GameActions.Say(GlobalChatChannels.Command(channel) + " " + text,
                    ProfileManager.CurrentProfile?.ChatMessageHue ?? (ushort)0x0481);
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);
            writer.WriteAttributeString("chatChannelsVersion", "2");
            writer.WriteAttributeString("channel", ((int)GlobalChatChannels.Selectable[_viewChannel.SelectedIndex]).ToString());
            writer.WriteAttributeString("sendChannel", ((int)GlobalChatChannels.Selectable[_sendChannel.SelectedIndex]).ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);
            if (int.TryParse(xml.GetAttribute("channel"), out int view))
                _viewChannel.SelectedIndex = GlobalChatChannels.SelectionIndex(view);
            if (int.TryParse(xml.GetAttribute("sendChannel"), out int send))
                _sendChannel.SelectedIndex = GlobalChatChannels.RestoreSendIndex(send, xml.GetAttribute("chatChannelsVersion") != "2");
        }

        public override GumpType GumpType => GumpType.GlobalChat;

        public static void OpenByUser(int x, int y)
        {
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.AutoOpenGlobalChat = true;

            UIManager.Add(new GlobalChatGump(x, y));
        }

        public void CloseByUser()
        {
            if (ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.AutoOpenGlobalChat = false;

            Dispose();
        }

        protected override void CloseWithRightClick()
        {
            base.CloseWithRightClick();

            if (IsDisposed && ProfileManager.CurrentProfile != null)
                ProfileManager.CurrentProfile.AutoOpenGlobalChat = false;
        }

        protected override int GetProfileFontSize() =>
            ProfileManager.CurrentProfile?.SelectedJournalFontSize ?? 16;

        protected override void StoreFontSizeOverride(int v) => _fontSizeOverride = v;

        protected override void StoreLastBounds(int x, int y, int w, int h)
        {
            _lastX = x; _lastY = y; _lastW = w; _lastH = h;
        }
    }
}

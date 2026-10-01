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

using System;
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
            new ChatHistoryStore((MessageEventArgs e) => GlobalChatChannels.IsChatMessage(e), 1000);

        private static bool _autoOpenHooked;

        public static void EnsureHooked()
        {
            if (!_autoOpenHooked)
            {
                Instance.RecordAdded += record =>
                {
                    if (!GlobalChatChannels.Matches(record, GlobalChatChannel.All)) return;
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
                   extraHeaderHeight: 28, colorChannelTags: true)
        {
            int left = PADDING + CustomThemeArt.ContentInset;
            Add(new Label("View:", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = left, Y = ExtraHeaderY + 4 });
            Add(_viewChannel = new Combobox(left + 36, ExtraHeaderY, 96, GlobalChatChannels.Names, 0,
                itemColor: index => index == 0 ? (Microsoft.Xna.Framework.Color?)null
                    : GlobalChatChannels.TagColor((GlobalChatChannel)index)));
            Add(new Label("Send:", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = left + 140, Y = ExtraHeaderY + 4 });
            Add(_sendChannel = new Combobox(left + 180, ExtraHeaderY, 96, GlobalChatChannels.SendNames, 0,
                itemColor: index => GlobalChatChannels.TagColor((GlobalChatChannel)(index + 1))));
            _viewChannel.OnOptionSelected += (_, selected) =>
            {
                SetRecordFilter(record => GlobalChatChannels.Matches(record, (GlobalChatChannel)_viewChannel.SelectedIndex));
                if (selected > 0) _sendChannel.SelectedIndex = selected - 1;
            };
            _sendChannel.OnOptionSelected += (_, selected) =>
                SetSendPrompt("Send: " + GlobalChatChannels.Command((GlobalChatChannel)(selected + 1)));
            _viewChannel.SetTooltip("All shows public channels. Guild and Party have separate views.");
            _sendChannel.SetTooltip("Channel used when you press Enter.");
            SetRecordFilter(record => GlobalChatChannels.Matches(record, GlobalChatChannel.All));
        }

        protected override void SendMessage(string text)
        {
            GlobalChatChannel channel = (GlobalChatChannel)(_sendChannel.SelectedIndex + 1);
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
            writer.WriteAttributeString("channel", _viewChannel.SelectedIndex.ToString());
            writer.WriteAttributeString("sendChannel", _sendChannel.SelectedIndex.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);
            if (int.TryParse(xml.GetAttribute("channel"), out int view))
                _viewChannel.SelectedIndex = Math.Max(0, Math.Min(GlobalChatChannels.Names.Length - 1, view));
            if (int.TryParse(xml.GetAttribute("sendChannel"), out int send))
                _sendChannel.SelectedIndex = Math.Max(0, Math.Min(GlobalChatChannels.SendNames.Length - 1, send));
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

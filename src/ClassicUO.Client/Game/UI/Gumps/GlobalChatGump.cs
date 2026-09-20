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

using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Persistent chat history store for MessageType.ChatSystem (the "[c" global chat).
    /// Subscribed lazily on first access; survives gump open/close.
    /// </summary>
    internal static class GlobalChatHistory
    {
        public static readonly ChatHistoryStore Instance =
            new ChatHistoryStore((MessageType t) => t == MessageType.ChatSystem, 1000);

        private static bool _autoOpenHooked;

        public static void EnsureHooked()
        {
            if (!_autoOpenHooked)
            {
                Instance.RecordAdded += _ =>
                {
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

        public GlobalChatGump() : this(_lastX, _lastY) { }

        public GlobalChatGump(int x, int y)
            : base(x, y, _lastW, _lastH, _fontSizeOverride,
                   "Global Chat", "Send: [c]",
                   GlobalChatHistory.Instance,
                   text => GameActions.Say("[c " + text,
                       ProfileManager.CurrentProfile?.ChatMessageHue ?? (ushort)0x0481))
        { }

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

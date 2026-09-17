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
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal static class NearbySpeechHistory
    {
        private const string SPIRIT_SPEAK_WORDS = "Anh Mi Sah Ko";

        public static readonly ChatHistoryStore Instance =
            new ChatHistoryStore(IsNearbySpeech, 100);

        public static void EnsureHooked() => Instance.EnsureHooked();

        private static bool IsNearbySpeech(MessageEventArgs e)
        {
            if (!(e?.Parent is Mobile mobile) || string.IsNullOrEmpty(e.Text))
                return false;

            if (e.Text.Trim().Equals(SPIRIT_SPEAK_WORDS, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(mobile.Name) && IgnoreManager.IgnoredCharsList.Contains(mobile.Name))
                return false;

            Profile profile = ProfileManager.CurrentProfile;
            if (profile?.ExcludeOwnSpeechHistory == true && mobile == World.Player)
                return false;

            if (profile?.ExcludePetSpeechHistory == true
                && mobile != World.Player
                && mobile.IsRenamable
                && mobile.NotorietyFlag != NotorietyFlag.Invulnerable
                && mobile.NotorietyFlag != NotorietyFlag.Enemy)
                return false;

            switch (e.Type)
            {
                case MessageType.Regular:
                case MessageType.Whisper:
                case MessageType.Yell:
                case MessageType.Emote:
                    return true;
                default:
                    return false;
            }
        }
    }

    internal class NearbySpeechGump : BaseChatGump
    {
        private static int _lastX = 240, _lastY = 240;
        private static int _lastW = DEFAULT_WIDTH, _lastH = DEFAULT_HEIGHT;
        private static int _fontSizeOverride;
        private readonly NiceButton _filterButton;

        public NearbySpeechGump() : this(_lastX, _lastY) { }

        public NearbySpeechGump(int x, int y)
            : base(x, y, _lastW, _lastH, _fontSizeOverride,
                  "Nearby Speech", "Say:",
                  NearbySpeechHistory.Instance,
                  null, false)
        {
            _filterButton = new NiceButton(Width - 118, 3, 60, 16, ButtonAction.Activate, "Filters")
            {
                IsSelectable = false
            };
            CustomGumpThemeManager.StyleDataButton(_filterButton);
            _filterButton.MouseUp += (sender, args) =>
            {
                if (args.Button != MouseButtonType.Left)
                    return;

                NearbySpeechOptionsGump existing = UIManager.GetGump<NearbySpeechOptionsGump>();
                if (existing != null)
                    existing.Dispose();
                else
                    UIManager.Add(new NearbySpeechOptionsGump(X + 40, Y + 30));
            };
            Add(_filterButton);
        }

        public override GumpType GumpType => GumpType.NearbySpeechHistory;

        protected override int GetProfileFontSize() =>
            ProfileManager.CurrentProfile?.SelectedJournalFontSize ?? 16;

        protected override void StoreFontSizeOverride(int value) =>
            _fontSizeOverride = value;

        protected override void StoreLastBounds(int x, int y, int width, int height)
        {
            _lastX = x;
            _lastY = y;
            _lastW = width;
            _lastH = height;
        }

        public override void Update()
        {
            _filterButton.X = Width - 118;
            base.Update();
        }
    }

    internal sealed class NearbySpeechOptionsGump : Gump
    {
        private const int WIDTH = 330;
        private const int HEIGHT = 142;

        internal NearbySpeechOptionsGump(int x, int y) : base(0, 0)
        {
            X = x;
            Y = y;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Add(CustomGumpThemeManager.CreateBackground(WIDTH, HEIGHT, 0.88f));
            Add(new Label("Nearby Speech Filters", true, CustomGumpThemeManager.TitleHue, font: 1)
            {
                X = 12,
                Y = 9
            });

            Profile profile = ProfileManager.CurrentProfile;
            AddToggle(12, 40, "Hide your speech", profile?.ExcludeOwnSpeechHistory == true, value =>
            {
                if (ProfileManager.CurrentProfile != null)
                    ProfileManager.CurrentProfile.ExcludeOwnSpeechHistory = value;
            });
            AddToggle(12, 69, "Hide owned-pet speech", profile?.ExcludePetSpeechHistory == true, value =>
            {
                if (ProfileManager.CurrentProfile != null)
                    ProfileManager.CurrentProfile.ExcludePetSpeechHistory = value;
            });

            Add(new Label("Applies to new messages. Use Clear to remove existing lines.", true,
                CustomGumpThemeManager.DimHue, WIDTH - 24, font: 1)
            {
                X = 12,
                Y = 105
            });
        }

        public override bool ShouldBeSaved => false;

        private void AddToggle(int x, int y, string text, bool enabled, System.Action<bool> changed)
        {
            NiceButton button = new NiceButton(x, y, WIDTH - 24, 23, ButtonAction.Activate,
                text + ": " + (enabled ? "ON" : "OFF"), font: 1, hue: enabled ? (ushort)0x44 : (ushort)0x21)
            {
                IsSelectable = false,
                DisplayBorder = true
            };
            CustomGumpThemeManager.StyleButton(button);
            button.MouseUp += (sender, args) =>
            {
                if (args.Button != MouseButtonType.Left)
                    return;

                changed(!enabled);
                int xPos = X;
                int yPos = Y;
                Dispose();
                UIManager.Add(new NearbySpeechOptionsGump(xPos, yPos));
            };
            Add(button);
        }
    }
}

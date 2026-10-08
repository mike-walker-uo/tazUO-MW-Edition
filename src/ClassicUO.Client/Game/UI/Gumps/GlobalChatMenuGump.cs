using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class GlobalChatMenuGump : Gump
    {
        private readonly GlobalChatGump _owner;
        private readonly Profile _profile;
        private readonly bool _settings;
        private readonly int _inset;
        private readonly Label[] _channelLabels = new Label[GlobalChatChannels.Selectable.Length];
        private readonly AlphaBlendControl _channelBackground;

        internal GlobalChatMenuGump(GlobalChatGump owner, bool settings) : base(0, 0)
        {
            _owner = owner;
            _profile = ProfileManager.CurrentProfile;
            _settings = settings;
            _inset = CustomThemeArt.ContentInset + 12;
            X = owner.X + 30;
            Y = owner.Y + 40;
            Width = 420 + _inset * 2;
            Height = (settings ? 460 : 370) + _inset * 2;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            var background = new AlphaBlendControl(0.98f) { Width = Width, Height = Height, ArtPanel = true };
            CustomGumpThemeManager.ApplyDataSurface(background, 0.98f);
            Add(background);
            Add(new Label(settings ? "Chat settings" : "Chat commands", true, CustomGumpThemeManager.DataTextHue, font: 1)
                { X = _inset, Y = _inset });

            if (settings)
            {
                AddCheck("Include my Guild messages in Global", 34, _profile?.GlobalChatIncludeGuild == true,
                    value => _profile.GlobalChatIncludeGuild = value);
                AddCheck("Include my Party messages in Global", 62, _profile?.GlobalChatIncludeParty == true,
                    value => _profile.GlobalChatIncludeParty = value);
                AddCheck("Light message background", 90, _profile?.GlobalChatLightMode == true,
                    value => _profile.GlobalChatLightMode = value);
                Add(new Label("Channel and message colors (current light/dark mode):", true,
                    CustomGumpThemeManager.DataTextHue, 410, font: 1) { X = _inset, Y = _inset + 128 });
            }
            else
                Add(new Label("Choose Write, then enter your message in Chat.", true,
                    CustomGumpThemeManager.DataTextHue, 410, font: 1) { X = _inset, Y = _inset + 32 });

            Add(_channelBackground = new AlphaBlendControl(1)
            {
                X = _inset, Y = _inset + (settings ? 164 : 62), Width = 330, Height = 244
            });
            for (int i = 0; i < GlobalChatChannels.Selectable.Length; i++)
            {
                int row = i;
                GlobalChatChannel channel = GlobalChatChannels.Selectable[row];
                int y = _inset + (settings ? 166 : 64) + row * 30;
                Add(_channelLabels[row] = new Label(ChannelText(channel), true, 0, 320, font: 1, ishtml: true)
                    { X = _inset, Y = y + 3 });
                AddButton(Width - _inset - 76, y, 76, settings ? "Color" : "Write", 100 + row);
            }
            if (settings)
                AddButton(_inset, Height - _inset - 26, 140, "Reset mode colors", 1);
            else
                Add(new Label("Global includes all public channels. Private channels stay optional.", true,
                    CustomGumpThemeManager.DataTextHue, 410, font: 1) { X = _inset, Y = _inset + 310 });
            AddButton(Width - _inset - 76, Height - _inset - 26, 76, "Close", 2);
            RefreshColors();
            SetInScreen();
        }

        private bool Active => !_owner.IsDisposed && _profile != null && _profile == ProfileManager.CurrentProfile;

        private void AddCheck(string text, int y, bool value, Action<bool> changed)
        {
            var checkbox = new Checkbox(0x00D2, 0x00D3, text, font: 1, color: CustomGumpThemeManager.DataTextHue)
                { X = _inset, Y = _inset + y, IsChecked = value };
            checkbox.ValueChanged += (_, __) =>
            {
                if (!Active) return;
                changed(checkbox.IsChecked);
                RefreshColors();
                _owner.RefreshSettings();
            };
            Add(checkbox);
        }

        private void AddButton(int x, int y, int width, string text, int id)
        {
            var button = new NiceButton(x, y, width, 24, ButtonAction.Activate, text)
                { IsSelectable = false, ButtonParameter = id };
            CustomGumpThemeManager.StyleDataButton(button);
            Add(button);
        }

        private string ChannelText(GlobalChatChannel channel)
        {
            var color = GlobalChatChannels.DisplayColor(channel, _profile);
            string text = GlobalChatChannels.Names[(int)channel];
            if (!_settings)
            {
                text += "   " + GlobalChatChannels.Command(channel);
                string alias = GlobalChatChannels.Alias(channel);
                if (alias != null) text += " / " + alias;
            }
            return $"<basefont color=\"#{color.R:X2}{color.G:X2}{color.B:X2}\">{text}</basefont>";
        }

        private void RefreshColors()
        {
            _channelBackground.BaseColor = _profile?.GlobalChatLightMode == true
                ? new Microsoft.Xna.Framework.Color(232, 227, 206) : new Microsoft.Xna.Framework.Color(32, 28, 27);
            for (int i = 0; i < _channelLabels.Length; i++)
                _channelLabels[i].Text = ChannelText(GlobalChatChannels.Selectable[i]);
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 2) { Dispose(); return; }
            if (!Active) { Dispose(); return; }
            if (buttonID == 1 && _settings)
            {
                foreach (GlobalChatChannel channel in GlobalChatChannels.Selectable)
                    _profile.GlobalChatChannelColors?.Remove(GlobalChatChannels.ColorKey(channel, _profile.GlobalChatLightMode));
                RefreshColors();
                _owner.RefreshSettings();
                return;
            }
            int row = buttonID - 100;
            if (row < 0 || row >= GlobalChatChannels.Selectable.Length) return;
            GlobalChatChannel selected = GlobalChatChannels.Selectable[row];
            if (!_settings)
            {
                _owner.SelectSendChannel(selected);
                Dispose();
                return;
            }
            bool light = _profile.GlobalChatLightMode;
            RGBColorPickerGump.Open(GlobalChatChannels.DisplayColor(selected, _profile), color =>
            {
                if (IsDisposed || !Active) return;
                if (_profile.GlobalChatChannelColors == null) _profile.GlobalChatChannelColors = new Dictionary<string, uint>();
                color.A = 255;
                _profile.GlobalChatChannelColors[GlobalChatChannels.ColorKey(selected, light)] = color.PackedValue;
                RefreshColors();
                _owner.RefreshSettings();
            });
        }

        public override void Update()
        {
            base.Update();
            if (!IsDisposed && !Active) Dispose();
        }
    }
}

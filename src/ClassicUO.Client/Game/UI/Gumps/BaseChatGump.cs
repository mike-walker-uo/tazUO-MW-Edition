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
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility.Collections;
using SDL3;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Shared layout + behavior for chat gumps (global, guild). Subclass supplies the
    /// history store, the displayed header label, and the send-action delegate.
    /// </summary>
    internal abstract class BaseChatGump : AnchorableGump
    {
        public const int DEFAULT_WIDTH = 520;
        public const int DEFAULT_HEIGHT = 300;
        protected const int INPUT_HEIGHT = 22;
        protected const int PADDING = 4;
        protected const int HEADER_H = 22;

        private readonly AlphaBlendControl _background;
        private readonly Label _header;
        private readonly NiceButton _clearButton;
        private readonly NiceButton _fontMinus;
        private readonly NiceButton _fontPlus;
        private readonly StbTextBox _search;
        private readonly Label _searchLabel;
        private readonly ChatBody _body;
        private readonly ScrollBar _scrollBar;
        private readonly StbTextBox _input;
        private readonly Label _inputPrompt;
        private readonly HitBox _resizeGrip;

        private bool _resizing;
        private int _resizeStartX, _resizeStartY, _startW, _startH;

        private readonly ChatHistoryStore _store;
        private readonly Action<string> _sendAction;

        protected BaseChatGump(int x, int y, int w, int h, int fontSizeOverride,
                               string headerText, string sendPromptText,
                               ChatHistoryStore store, Action<string> sendAction,
                               bool showInput = true) : base(0, 0)
        {
            _store = store;
            _sendAction = sendAction;
            _store.EnsureHooked();

            X = x;
            Y = y;
            Width = w;
            Height = h;
            FontSizeOverride = fontSizeOverride;

            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = Width;
            GroupMatrixHeight = Height;
            LayerOrder = UILayer.Over;

            Add(_background = new AlphaBlendControl(0.85f) { Width = Width, Height = Height });
            CustomGumpThemeManager.ApplyDataSurface(_background, 0.85f);

            Add(_header = new Label(headerText, true, CustomGumpThemeManager.DataTextHue, font: 1) { X = PADDING, Y = 3 });

            Add(_searchLabel = new Label("Find:", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = 90, Y = 3 });
            _search = new SearchBox(this, 0xFF, 64, 140, true, FontStyle.None, CustomGumpThemeManager.DataTextHue)
            {
                X = 122,
                Y = 3,
                Width = 140,
                Height = 18
            };
            AlphaBlendControl searchBackground = new AlphaBlendControl(0.6f) { Width = _search.Width, Height = _search.Height };
            CustomGumpThemeManager.ApplyInputSurface(searchBackground, 0.6f);
            _search.Add(searchBackground);
            Add(_search);

            Add(_fontMinus = new NiceButton(Width - 168, 3, 22, 16, ButtonAction.Activate, "-")
            {
                ButtonParameter = 2,
                IsSelectable = false
            });
            CustomGumpThemeManager.StyleDataButton(_fontMinus);
            _fontMinus.SetTooltip("Decrease font size");

            Add(_fontPlus = new NiceButton(Width - 144, 3, 22, 16, ButtonAction.Activate, "+")
            {
                ButtonParameter = 3,
                IsSelectable = false
            });
            CustomGumpThemeManager.StyleDataButton(_fontPlus);
            _fontPlus.SetTooltip("Increase font size");

            Add(_clearButton = new NiceButton(Width - 50 - PADDING, 3, 50, 16, ButtonAction.Activate, "Clear")
            {
                ButtonParameter = 1,
                IsSelectable = false
            });
            CustomGumpThemeManager.StyleDataButton(_clearButton);

            int bodyY = HEADER_H;
            int bodyH = Height - HEADER_H - (showInput ? INPUT_HEIGHT : 0) - PADDING;
            int bodyW = Width - 14 - (PADDING * 2);

            _scrollBar = new ScrollBar(Width - 14 - PADDING, bodyY, bodyH);
            Add(_scrollBar);

            _body = new ChatBody(_store, PADDING, bodyY, bodyW, bodyH, _scrollBar);
            Add(_body);

            if (showInput)
            {
                Add(_inputPrompt = new Label(sendPromptText, true, CustomGumpThemeManager.DataTextHue, font: 1)
                {
                    X = PADDING + 4,
                    Y = Height - INPUT_HEIGHT + 2
                });

                int promptWidth = Math.Max(60, _inputPrompt.Width + 8);
                int inputW = Width - PADDING * 2 - promptWidth - 8;
                _input = new ChatInput(this, 0xFF, 256, inputW, true, FontStyle.None, CustomGumpThemeManager.DataTextHue)
                {
                    X = PADDING + promptWidth,
                    Y = Height - INPUT_HEIGHT + 2,
                    Width = inputW,
                    Height = INPUT_HEIGHT - 4
                };
                AlphaBlendControl inputBackground = new AlphaBlendControl(0.6f) { Width = _input.Width, Height = _input.Height };
                CustomGumpThemeManager.ApplyInputSurface(inputBackground, 0.6f);
                _input.Add(inputBackground);
                Add(_input);
            }

            _resizeGrip = new HitBox(Width - 12, Height - 12, 12, 12, "Drag to resize", 0.5f);
            _resizeGrip.MouseDown += (s, e) =>
            {
                _resizing = true;
                _resizeStartX = Mouse.Position.X;
                _resizeStartY = Mouse.Position.Y;
                _startW = Width;
                _startH = Height;
            };
            _resizeGrip.MouseUp += (s, e) => _resizing = false;
            Add(_resizeGrip);

            _body.RebuildFromHistory(GetCurrentFontSize());
            _store.RecordAdded += OnRecordAdded;
        }

        protected int FontSizeOverride { get; private set; }

        protected abstract int GetProfileFontSize();
        protected abstract void StoreFontSizeOverride(int v);
        protected abstract void StoreLastBounds(int x, int y, int w, int h);

        public override void Dispose()
        {
            _store.RecordAdded -= OnRecordAdded;
            StoreLastBounds(X, Y, Width, Height);
            _body?.DisposeBoxes();
            base.Dispose();
        }

        private void OnRecordAdded(ChatHistoryRecord r)
        {
            if (IsDisposed || _body == null) return;
            _body.AppendRecord(r, GetCurrentFontSize(), _search.Text);
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1:
                    ClearHistory();
                    break;
                case 2:
                    SetFontSizeOverride(GetCurrentFontSize() - 1);
                    break;
                case 3:
                    SetFontSizeOverride(GetCurrentFontSize() + 1);
                    break;
                default:
                    base.OnButtonClick(buttonID);
                    break;
            }
        }

        public void ClearHistory()
        {
            _store.Clear();
            _body.RebuildFromHistory(GetCurrentFontSize(), _search.Text);
        }

        protected int GetCurrentFontSize()
        {
            int profileSize = GetProfileFontSize();
            int size = FontSizeOverride > 0 ? FontSizeOverride : profileSize;
            if (size < 8) size = 8;
            if (size > 36) size = 36;
            return size;
        }

        private void SetFontSizeOverride(int size)
        {
            if (size < 8) size = 8;
            if (size > 36) size = 36;
            FontSizeOverride = size;
            StoreFontSizeOverride(size);
            _body.RebuildFromHistory(GetCurrentFontSize(), filter: _search.Text);
        }

        public void OnSearchChanged(string filter)
        {
            _body.RebuildFromHistory(GetCurrentFontSize(), filter: filter);
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;

            if (_resizing)
            {
                if (Mouse.LButtonPressed)
                {
                    int newW = Math.Max(320, _startW + (Mouse.Position.X - _resizeStartX));
                    int newH = Math.Max(160, _startH + (Mouse.Position.Y - _resizeStartY));
                    if (newW != Width || newH != Height)
                        Resize(newW, newH);
                }
                else
                {
                    _resizing = false;
                }
            }
        }

        private void Resize(int w, int h)
        {
            Width = w;
            Height = h;
            _background.Width = w;
            _background.Height = h;

            _fontMinus.X = w - 168;
            _fontPlus.X = w - 144;
            _clearButton.X = w - 50 - PADDING;

            int bodyH = h - HEADER_H - (_input == null ? 0 : INPUT_HEIGHT) - PADDING;
            int bodyW = w - 14 - (PADDING * 2);

            _scrollBar.X = w - 14 - PADDING;
            _scrollBar.Height = bodyH;
            _body.Width = bodyW;
            _body.Height = bodyH;
            _body.OnResize();

            if (_input != null)
            {
                int promptWidth = Math.Max(60, _inputPrompt.Width + 8);
                _inputPrompt.Y = h - INPUT_HEIGHT + 2;
                _input.Y = h - INPUT_HEIGHT + 2;
                _input.Width = w - PADDING * 2 - promptWidth - 8;
            }
            _resizeGrip.X = w - 12;
            _resizeGrip.Y = h - 12;

            GroupMatrixWidth = w;
            GroupMatrixHeight = h;
            WantUpdateSize = false;
        }

        public void SubmitInputText()
        {
            if (_input == null || _sendAction == null)
                return;

            string text = _input.Text;
            if (string.IsNullOrWhiteSpace(text))
                return;
            _input.SetText(string.Empty);
            _sendAction(text);
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer); // base already writes type/x/y/serial/...
            writer.WriteAttributeString("w", Width.ToString());
            writer.WriteAttributeString("h", Height.ToString());
            writer.WriteAttributeString("font", FontSizeOverride.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml); // X/Y are restored by the outer RestoreGumps loop
            int w = Width, h = Height;
            if (!int.TryParse(xml.GetAttribute("w"), out w) || w < 320) w = Width;
            if (!int.TryParse(xml.GetAttribute("h"), out h) || h < 160) h = Height;
            int.TryParse(xml.GetAttribute("font"), out int fontOverride);
            FontSizeOverride = fontOverride;
            Resize(w, h);
            _body.RebuildFromHistory(GetCurrentFontSize());
        }

        // ────────────────────── inner controls ──────────────────────

        private sealed class ChatInput : StbTextBox
        {
            private readonly BaseChatGump _owner;
            public ChatInput(BaseChatGump owner, byte font, int maxChars, int maxWidth, bool unicode, FontStyle style, ushort hue)
                : base(font, maxChars, maxWidth, unicode, style, hue) { _owner = owner; }

            protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
            {
                if (key == SDL.SDL_Keycode.SDLK_RETURN || key == SDL.SDL_Keycode.SDLK_KP_ENTER)
                {
                    _owner.SubmitInputText();
                    return;
                }
                base.OnKeyDown(key, mod);
            }
        }

        private sealed class SearchBox : StbTextBox
        {
            private readonly BaseChatGump _owner;
            private string _last = string.Empty;

            public SearchBox(BaseChatGump owner, byte font, int maxChars, int maxWidth, bool unicode, FontStyle style, ushort hue)
                : base(font, maxChars, maxWidth, unicode, style, hue)
            {
                _owner = owner;
                TextChanged += (s, e) =>
                {
                    if (Text != _last)
                    {
                        _last = Text ?? string.Empty;
                        _owner.OnSearchChanged(_last);
                    }
                };
            }

            protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
            {
                if (key == SDL.SDL_Keycode.SDLK_ESCAPE)
                {
                    SetText(string.Empty);
                    _owner.OnSearchChanged(string.Empty);
                    return;
                }
                base.OnKeyDown(key, mod);
            }
        }

        private sealed class ChatLine
        {
            public TextBox Time;
            public TextBox NameBox; // null for system messages (no Name)
            public TextBox Box;     // message text (or whole message for system)
        }

        private sealed class ChatBody : Control
        {
            private readonly Deque<ChatLine> _lines = new Deque<ChatLine>();
            private readonly ScrollBar _scrollBar;
            private readonly ChatHistoryStore _store;
            private bool _stickToBottom = true;
            private string _activeFilter = string.Empty;
            private int _activeFontSize = 16;

            public ChatBody(ChatHistoryStore store, int x, int y, int w, int h, ScrollBar scrollBar)
            {
                _store = store;
                X = x; Y = y; Width = w; Height = h;
                _scrollBar = scrollBar;
                AcceptMouseInput = true;
                CanMove = true;
                WantUpdateSize = false;
            }

            public void OnResize() => RecalcScroll();

            private static bool MatchesFilter(ChatHistoryRecord r, string filter)
            {
                if (filter == "__none__") return false;
                if (string.IsNullOrEmpty(filter)) return true;
                if (r.Text != null && r.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (r.Name != null && r.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }

            public void RebuildFromHistory(int fontSize, string filter = null)
            {
                DisposeBoxes();
                _activeFilter = filter ?? string.Empty;
                _activeFontSize = fontSize;

                if (filter == "__none__") { RecalcScroll(); return; }

                var profile = ProfileManager.CurrentProfile;
                string font = profile?.SelectedTTFJournalFont;

                foreach (var r in _store.All())
                {
                    if (!MatchesFilter(r, _activeFilter)) continue;
                    AddVisualLineInternal(r, font, fontSize);
                }
                RecalcScroll();
                _stickToBottom = true;
                _scrollBar.Value = _scrollBar.MaxValue;
            }

            public void AppendRecord(ChatHistoryRecord r, int fontSize, string filter)
            {
                if (filter != _activeFilter || fontSize != _activeFontSize)
                {
                    RebuildFromHistory(fontSize, filter);
                    return;
                }
                if (filter == "__none__") return;
                if (!MatchesFilter(r, filter ?? string.Empty)) return;

                var profile = ProfileManager.CurrentProfile;
                string font = profile?.SelectedTTFJournalFont;
                AddVisualLineInternal(r, font, fontSize);
                RecalcScroll();
            }

            private void AddVisualLineInternal(ChatHistoryRecord r, string font, int fontSize)
            {
                int size = fontSize < 8 ? 8 : fontSize;

                // Alliance messages: render in light blue regardless of the server-assigned hue
                // so they read clearly against the dark background and are distinguishable from
                // Guild messages.
                ushort messageHue;
                if (r.MsgType == ClassicUO.Game.Data.MessageType.Alliance)
                    messageHue = 0x005A;
                else
                    messageHue = r.Hue == 0 ? (ushort)0x0481 : r.Hue;

                TextBox time = TextBox.GetOne($"{r.Time:HH:mm}", font, size - 2, 1150, TextBox.RTLOptions.Default());

                if (string.IsNullOrEmpty(r.Name))
                {
                    // System message — single coloured text after timestamp.
                    TextBox box = TextBox.GetOne(r.Text ?? string.Empty, font, size, messageHue,
                        new TextBox.RTLOptions { Width = Width - 4 - time.Width });
                    _lines.AddToBack(new ChatLine { Box = box, Time = time, NameBox = null });
                    return;
                }

                ushort nameHue = ChatNameHueMap.GetHueForName(r.Name);
                TextBox nameBox = TextBox.GetOne($"{r.Name}: ", font, size, nameHue, TextBox.RTLOptions.Default());

                int remaining = Width - 8 - time.Width - nameBox.Width;
                if (remaining < 80) remaining = 80;
                TextBox messageBox = TextBox.GetOne(r.Text ?? string.Empty, font, size, messageHue,
                    new TextBox.RTLOptions { Width = remaining });

                _lines.AddToBack(new ChatLine { Box = messageBox, Time = time, NameBox = nameBox });
            }

            public void DisposeBoxes()
            {
                foreach (var l in _lines)
                {
                    l.Box?.Dispose();
                    l.Time?.Dispose();
                    l.NameBox?.Dispose();
                }
                _lines.Clear();
                _scrollBar.Value = 0;
                _scrollBar.MaxValue = 0;
            }

            protected override void OnMouseWheel(MouseEventType delta)
            {
                _scrollBar.InvokeMouseWheel(delta);
                _stickToBottom = (_scrollBar.Value >= _scrollBar.MaxValue - 4);
            }

            private void RecalcScroll()
            {
                int total = 0;
                foreach (var l in _lines)
                {
                    if (l.Box == null) continue;
                    int h = l.Box.Height;
                    if (l.NameBox != null && l.NameBox.Height > h) h = l.NameBox.Height;
                    total += h;
                }
                int max = Math.Max(0, total - Height);
                bool atBottom = _scrollBar.Value >= _scrollBar.MaxValue - 4;
                _scrollBar.MaxValue = max;
                if (_stickToBottom || atBottom)
                {
                    _scrollBar.Value = _scrollBar.MaxValue;
                }
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                base.Draw(batcher, x, y);
                if (_lines.Count == 0) return true;
                if (!batcher.ClipBegin(x, y, Width, Height)) return true;

                int scroll = _scrollBar.Value;
                int my = -scroll;

                foreach (var l in _lines)
                {
                    if (l.Box == null) continue;
                    int h = l.Box.Height;
                    if (l.NameBox != null && l.NameBox.Height > h) h = l.NameBox.Height;
                    if (my + h < 0) { my += h; continue; }
                    if (my > Height) break;

                    int relY = y + my;
                    int cx = x;
                    l.Time?.Draw(batcher, cx, relY);
                    if (l.Time != null) cx += l.Time.Width + 4;
                    if (l.NameBox != null)
                    {
                        l.NameBox.Draw(batcher, cx, relY);
                        cx += l.NameBox.Width;
                    }
                    l.Box.Draw(batcher, cx, relY);
                    my += h;
                }

                batcher.ClipEnd();
                return true;
            }
        }
    }
}

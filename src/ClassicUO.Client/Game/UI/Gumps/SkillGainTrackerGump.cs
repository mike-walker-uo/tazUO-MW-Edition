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
using System.Collections.Generic;
using System.Xml;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Session-bound skill-gain tracker UI. Lists each skill that gained this
    /// session with total delta and event count, sorted desc by total gain.
    /// </summary>
    internal class SkillGainTrackerGump : AnchorableGump
    {
        private const int DEFAULT_WIDTH = 280;
        private const int DEFAULT_HEIGHT = 260;
        private const int ROW_HEIGHT = 18;
        private const int HEADER_H = 36;
        private const int MIN_WIDTH = 220;
        private const int MIN_HEIGHT = HEADER_H + ROW_HEIGHT * 3 + 4;
        private const int MAX_ROWS = 30;

        private static int _lastX = 200, _lastY = 200;
        private static int _lastW = DEFAULT_WIDTH, _lastH = DEFAULT_HEIGHT;

        private readonly AlphaBlendControl _bg;
        private readonly Label _header;
        private readonly Label _subHeader;
        private readonly NiceButton _resetButton;
        private readonly Label[] _rows = new Label[MAX_ROWS];
        private readonly HitBox _resizeGrip;

        private long _refreshTime;
        private bool _resizing;
        private int _resizeStartX, _resizeStartY, _startW, _startH;

        public SkillGainTrackerGump() : this(_lastX, _lastY) { }

        public SkillGainTrackerGump(int x, int y) : base(0, 0)
        {
            X = x;
            Y = y;
            Width = _lastW;
            Height = _lastH;

            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = Width;
            GroupMatrixHeight = Height;

            Add(_bg = new AlphaBlendControl(0.7f) { Width = Width, Height = Height });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.7f);
            Add(_header = new Label("Skill Gains", true, 0x0481, font: 1) { X = 6, Y = 4 });
            Add(_subHeader = new Label(string.Empty, true, 0x03B2, font: 1) { X = 6, Y = 18 });
            Add(_resetButton = new NiceButton(Width - 56, 4, 50, 16, ButtonAction.Activate, "Reset") { ButtonParameter = 1, IsSelectable = false });

            for (int i = 0; i < MAX_ROWS; i++)
            {
                _rows[i] = new Label(string.Empty, true, 0x0481, font: 1, maxwidth: Width - 12)
                {
                    X = 6,
                    Y = HEADER_H + i * ROW_HEIGHT
                };
                Add(_rows[i]);
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
        }

        public override GumpType GumpType => GumpType.SkillGainTracker;

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 1)
            {
                SkillGainTracker.Reset();
                _refreshTime = 0;
            }
            else
            {
                base.OnButtonClick(buttonID);
            }
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;

            if (_resizing)
            {
                if (Mouse.LButtonPressed)
                {
                    int newW = Math.Max(MIN_WIDTH, _startW + (Mouse.Position.X - _resizeStartX));
                    int newH = Math.Max(MIN_HEIGHT, _startH + (Mouse.Position.Y - _resizeStartY));
                    if (newW != Width || newH != Height) Resize(newW, newH);
                }
                else _resizing = false;
            }

            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 500;
            RenderRows();
        }

        private static readonly List<SkillGainTracker.Entry> _snapshot = new List<SkillGainTracker.Entry>();
        private static readonly Comparison<SkillGainTracker.Entry> _byGainDesc =
            (a, b) => b.TotalGain.CompareTo(a.TotalGain);

        private void RenderRows()
        {
            _snapshot.Clear();
            foreach (var e in SkillGainTracker.Snapshot()) _snapshot.Add(e);
            _snapshot.Sort(_byGainDesc);

            double secs = SkillGainTracker.SessionSeconds;
            double total = SkillGainTracker.TotalGain;
            string sub = $"Session: {total:0.0} gain / {secs:0}s ({_snapshot.Count} skill(s))";
            if (_subHeader.Text != sub) _subHeader.Text = sub;

            int maxVisibleRows = (Height - HEADER_H - 4) / ROW_HEIGHT;
            if (maxVisibleRows < 1) maxVisibleRows = 1;
            if (maxVisibleRows > MAX_ROWS) maxVisibleRows = MAX_ROWS;

            int shown = Math.Min(_snapshot.Count, maxVisibleRows);
            for (int i = 0; i < shown; i++)
            {
                var e = _snapshot[i];
                string text = $"{e.SkillName}  +{e.TotalGain:0.0}  ({e.Events})";
                if (_rows[i].Text != text) _rows[i].Text = text;
            }
            for (int i = shown; i < MAX_ROWS; i++)
                if (_rows[i].Text.Length != 0) _rows[i].Text = string.Empty;
        }

        private void Resize(int w, int h)
        {
            Width = w; Height = h;
            _lastW = w; _lastH = h;
            _bg.Width = w; _bg.Height = h;
            _resetButton.X = w - 56;
            _resizeGrip.X = w - 12; _resizeGrip.Y = h - 12;
            GroupMatrixWidth = w; GroupMatrixHeight = h;
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);
            writer.WriteAttributeString("w", Width.ToString());
            writer.WriteAttributeString("h", Height.ToString());
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);
            if (int.TryParse(xml.GetAttribute("w"), out int w) && w >= MIN_WIDTH &&
                int.TryParse(xml.GetAttribute("h"), out int h) && h >= MIN_HEIGHT)
                Resize(w, h);
        }

        public override void Dispose()
        {
            _lastX = X; _lastY = Y;
            base.Dispose();
        }
    }
}

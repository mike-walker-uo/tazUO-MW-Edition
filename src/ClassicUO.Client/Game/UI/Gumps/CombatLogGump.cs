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

using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Read-only combat log: most-recent damage events, top of list. Incoming
    /// hues red, outgoing green.
    /// </summary>
    internal class CombatLogGump : Gump
    {
        private const int W = 320;
        private const int H = 300;
        private const int HEADER_H = 24;
        private const int ROW_H = 14;
        private const int MAX_ROWS = 18;

        private static int _lastX = 360, _lastY = 360;

        private readonly AlphaBlendControl _bg;
        private readonly Label _header;
        private readonly NiceButton _clearButton;
        private readonly Label[] _rows = new Label[MAX_ROWS];
        private long _refreshTime;
        private int _lastCount = -1;

        public CombatLogGump() : this(_lastX, _lastY) { }

        public CombatLogGump(int x, int y) : base(0, 0)
        {
            X = x; Y = y; Width = W; Height = H;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(_bg = new AlphaBlendControl(0.65f) { Width = W, Height = H });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.65f);
            Add(_header = new Label("Combat Log (latest first)", true, 0x0481, font: 1) { X = 6, Y = 4 });
            Add(_clearButton = new NiceButton(W - 50, 4, 44, 16, ButtonAction.Activate, "Clear") { ButtonParameter = 1, IsSelectable = false });

            for (int i = 0; i < MAX_ROWS; i++)
            {
                _rows[i] = new Label(string.Empty, true, 0x0481, font: 1, maxwidth: W - 12)
                {
                    X = 6,
                    Y = HEADER_H + i * ROW_H
                };
                Add(_rows[i]);
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 1) { CombatLogManager.Clear(); _refreshTime = 0; _lastCount = -1; return; }
            base.OnButtonClick(buttonID);
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 500;
            int n = CombatLogManager.Count;
            if (n == _lastCount) return;
            _lastCount = n;

            var list = new List<CombatLogManager.Entry>();
            foreach (var e in CombatLogManager.All()) list.Add(e);

            int idx = 0;
            for (int i = list.Count - 1; i >= 0 && idx < MAX_ROWS; i--, idx++)
            {
                var e = list[i];
                string arrow = e.IsIncoming ? "← " : "→ ";
                ushort hue = e.IsIncoming ? (ushort)0x21 : (ushort)0x44;
                string text = $"{e.Time:HH:mm:ss}  {arrow}{e.TargetName}  {e.Damage}";
                if (_rows[idx].Text != text) _rows[idx].Text = text;
                if (_rows[idx].Hue != hue) _rows[idx].Hue = hue;
            }
            for (; idx < MAX_ROWS; idx++)
                if (_rows[idx].Text.Length != 0) _rows[idx].Text = string.Empty;
        }

        public override void Dispose()
        {
            _lastX = X; _lastY = Y;
            base.Dispose();
        }
    }
}

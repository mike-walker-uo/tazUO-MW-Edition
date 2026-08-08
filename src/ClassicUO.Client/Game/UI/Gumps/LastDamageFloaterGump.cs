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

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Tiny anchorable showing time since last incoming hit and its amount.
    /// Sources its data from CombatLogManager. Updated each frame.
    /// </summary>
    internal class LastDamageFloaterGump : AnchorableGump
    {
        private const int W = 180;
        private const int H = 22;

        private readonly AlphaBlendControl _bg;
        private readonly Label _label;
        private long _refreshTime;

        public LastDamageFloaterGump() : this(540, 60) { }

        public LastDamageFloaterGump(int x, int y) : base(0, 0)
        {
            X = x; Y = y; Width = W; Height = H;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = W; GroupMatrixHeight = H;

            Add(_bg = new AlphaBlendControl(0.7f) { Width = W, Height = H });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.7f);
            Add(_label = new Label("Last hit: n/a", true, 0x0481, font: 1) { X = 6, Y = 4 });
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 250;

            CombatLogManager.Entry latest = null;
            foreach (var e in CombatLogManager.All())
            {
                if (!e.IsIncoming) continue;
                latest = e; // last incoming
            }

            if (latest == null)
            {
                if (_label.Text != "Last hit: n/a") _label.Text = "Last hit: n/a";
                return;
            }

            double sec = (System.DateTime.Now - latest.Time).TotalSeconds;
            string t = $"Last hit: {sec:0.0}s ago / {latest.Damage} dmg";
            if (_label.Text != t) _label.Text = t;
            ushort hue = sec < 3 ? (ushort)0x21 : sec < 10 ? (ushort)0x35 : (ushort)0x0481;
            if (_label.Hue != hue) _label.Hue = hue;
        }
    }
}

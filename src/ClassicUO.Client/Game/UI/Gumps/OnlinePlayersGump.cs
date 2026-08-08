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
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Counts distinct human/gargoyle players the client has seen in
    /// `World.Mobiles` and whose serial is a player serial. Refreshes 1s.
    /// </summary>
    internal class OnlinePlayersGump : AnchorableGump
    {
        private const int W = 130;
        private const int H = 22;

        private readonly AlphaBlendControl _bg;
        private readonly Label _label;
        private long _refreshTime;

        public OnlinePlayersGump() : this(420, 60) { }

        public OnlinePlayersGump(int x, int y) : base(0, 0)
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
            Add(_label = new Label("Players: 0", true, 0x0481, font: 1) { X = 6, Y = 4 });
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 1000;

            int n = 0;
            var seen = new HashSet<uint>();
            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed) continue;
                if (!(m.IsHuman || m.IsGargoyle)) continue;
                // Player serials are below 0x40000000 typically; tolerate range.
                if (m.Serial == World.Player?.Serial) continue;
                if (seen.Add(m.Serial)) n++;
            }
            string t = $"Players: {n}";
            if (_label.Text != t) _label.Text = t;
        }
    }
}

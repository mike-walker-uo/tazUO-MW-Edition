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

using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Compact floater showing the worst-durability equipped item percentage.
    /// Hue shifts amber/red as it deteriorates.
    /// </summary>
    internal class DurabilityFloaterGump : AnchorableGump
    {
        private const int W = 160;
        private const int H = 22;

        private readonly AlphaBlendControl _bg;
        private readonly Label _label;
        private long _refreshTime;

        public DurabilityFloaterGump() : this(480, 100) { }

        public DurabilityFloaterGump(int x, int y) : base(0, 0)
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
            Add(_label = new Label("Durability: -", true, 0x0481, font: 1) { X = 6, Y = 4 });
        }

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 1000;
            if (World.DurabilityManager == null) return;

            int worstPct = int.MaxValue;
            string worstName = null;
            foreach (var d in World.DurabilityManager.Durabilities)
            {
                if (d.MaxDurabilty <= 0) continue;
                int pct = (int)(d.Percentage * 100);
                if (pct < worstPct)
                {
                    worstPct = pct;
                    var item = World.Items.Get((uint)d.Serial);
                    worstName = item != null && !string.IsNullOrEmpty(item.Name)
                        ? item.Name
                        : "?";
                }
            }

            if (worstName == null)
            {
                if (_label.Text != "Durability: (no data)") _label.Text = "Durability: (no data)";
                if (_label.Hue != 0x0481) _label.Hue = 0x0481;
                return;
            }

            string t = $"Worst: {worstName} {worstPct}%";
            if (_label.Text != t) _label.Text = t;
            ushort hue = worstPct < 30 ? (ushort)0x21
                       : worstPct < 60 ? (ushort)0x35
                       : (ushort)0x0481;
            if (_label.Hue != hue) _label.Hue = hue;
        }
    }
}

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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Compact floating gump: weight bar (cur/max) + bandage cooldown timer.
    /// Anchorable, persistent position. 250ms refresh on the labels.
    /// </summary>
    internal class PlayerInfoFloater : AnchorableGump
    {
        private const int W = 160;
        private const int H = 36;

        private static int _lastX = 320, _lastY = 60;

        private readonly AlphaBlendControl _bg;
        private readonly Label _weightLabel;
        private readonly Label _bandageLabel;
        private long _refreshTime;

        public PlayerInfoFloater() : this(_lastX, _lastY) { }

        public PlayerInfoFloater(int x, int y) : base(0, 0)
        {
            X = x; Y = y;
            Width = W; Height = H;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = W;
            GroupMatrixHeight = H;

            Add(_bg = new AlphaBlendControl(0.6f) { Width = W, Height = H });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.6f);
            Add(_weightLabel = new Label("Weight: 0/0", true, 0x0481, font: 1) { X = 4, Y = 2 });
            Add(_bandageLabel = new Label("Bandage: ready", true, 0x03B2, font: 1) { X = 4, Y = 18 });
        }

        public override GumpType GumpType => GumpType.PlayerInfoFloater;

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 250;
            if (World.Player == null) return;

            int w = World.Player.Weight, max = World.Player.WeightMax;
            int pct = max > 0 ? (int)((float)w / max * 100) : 0;
            string ws = $"Weight: {w}/{max} ({pct}%)";
            if (_weightLabel.Text != ws) _weightLabel.Text = ws;
            // Color shifts as weight rises.
            ushort wHue = pct >= 90 ? (ushort)0x21 : pct >= 70 ? (ushort)0x35 : (ushort)0x0481;
            if (_weightLabel.Hue != wHue) _weightLabel.Hue = wHue;

            // Bandage cooldown: AutoBandageManager exposes NextEligibleTime indirectly via Tick.
            // Use its private state via a tiny helper accessor (we just compute remaining here).
            long remaining = AutoBandageRemaining();
            string bs = remaining > 0 ? $"Bandage: {remaining / 1000.0:0.0}s" : "Bandage: ready";
            if (_bandageLabel.Text != bs) _bandageLabel.Text = bs;
        }

        // Best-effort: re-read AutoBandageManager. Since `_nextEligibleTime` is private,
        // we mirror it via a tiny accessor that gets injected at SetBandageUsed call.
        public static long LastBandageUsedAt;
        public const long BANDAGE_INTERVAL_MS = 9000;
        private static long AutoBandageRemaining()
        {
            long now = (long)Time.Ticks;
            long eligible = LastBandageUsedAt + BANDAGE_INTERVAL_MS;
            return eligible > now ? eligible - now : 0;
        }

        public override void Dispose()
        {
            _lastX = X; _lastY = Y;
            base.Dispose();
        }
    }
}

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
using ClassicUO.Network;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Compact "ping: Nms" badge in the top-left of the viewport, hued green
    /// (<80ms), yellow (<200ms), or red (≥200ms). 500 ms refresh.
    /// `-pinghud on|off`.
    /// </summary>
    public static class PingHudOverlay
    {
        public static bool Enabled;
        private const long REFRESH_MS = 500;
        private static long _nextRefresh;
        private static uint _ping;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;

            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + REFRESH_MS;
                _ping = NetClient.Socket?.Statistics?.Ping ?? 0;
            }

            Color c = _ping >= 200 ? new Color(255, 80, 80) :
                      _ping >= 80  ? new Color(240, 200, 80) :
                                     new Color(120, 220, 120);

            string txt = "ping " + _ping + "ms";
            var tb = OverlayTextCache.Get("pinghud", txt, 12, c, 100);
            int x = bounds.X + 8;
            int y = bounds.Y + 8;
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 160));
            batcher.Draw(bg, new Rectangle(x - 3, y - 1, tb.Width + 6, tb.Height + 2), hueNeutral);
            tb.Draw(batcher, x, y);
        }
    }
}

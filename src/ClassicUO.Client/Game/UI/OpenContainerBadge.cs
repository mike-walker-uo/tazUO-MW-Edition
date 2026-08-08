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
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Counts open container gumps (ContainerGump + GridContainer) and shows
    /// "C:N" in the bottom-left of the viewport so the player can spot
    /// orphaned containers. `-containerbadge on|off`.
    /// </summary>
    public static class OpenContainerBadge
    {
        public static bool Enabled;
        private const long REFRESH_MS = 750;
        private static long _nextRefresh;
        private static int _count;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + REFRESH_MS;
                _count = 0;
                if (UIManager.Gumps != null)
                {
                    foreach (var g in UIManager.Gumps)
                    {
                        if (g is ContainerGump || g is GridContainer) _count++;
                    }
                }
            }
            if (_count == 0) return;

            string txt = "C:" + _count;
            var tb = OverlayTextCache.Get("containerbadge", txt, 12, Color.White, 60);
            int x = bounds.X + 8;
            int y = bounds.Bottom - tb.Height - 8;
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 160));
            batcher.Draw(bg, new Rectangle(x - 3, y - 1, tb.Width + 6, tb.Height + 2), hueNeutral);
            tb.Draw(batcher, x, y);
        }
    }
}

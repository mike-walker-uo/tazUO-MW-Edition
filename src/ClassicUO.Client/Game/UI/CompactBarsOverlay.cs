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

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Three 80×3 HP/Mana/Stam bars stacked in the bottom-left of the
    /// viewport. Off by default. `-compactbars on|off`.
    /// </summary>
    public static class CompactBarsOverlay
    {
        public static bool Enabled;
        private const int W = 80, H = 3, GAP = 2;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 180));
            Texture2D red = SolidColorTextureCache.GetTexture(new Color(220, 60, 60, 230));
            Texture2D blue = SolidColorTextureCache.GetTexture(new Color(80, 150, 230, 230));
            Texture2D yellow = SolidColorTextureCache.GetTexture(new Color(230, 200, 80, 230));

            int x = bounds.X + 8;
            int y = bounds.Bottom - 60;

            DrawRow(batcher, hueNeutral, bg, red, x, y, World.Player.Hits, World.Player.HitsMax);
            DrawRow(batcher, hueNeutral, bg, blue, x, y + H + GAP, World.Player.Mana, World.Player.ManaMax);
            DrawRow(batcher, hueNeutral, bg, yellow, x, y + 2 * (H + GAP), World.Player.Stamina, World.Player.StaminaMax);
        }

        private static void DrawRow(UltimaBatcher2D batcher, Vector3 hueNeutral,
                                    Texture2D bg, Texture2D fg, int x, int y, int cur, int max)
        {
            batcher.Draw(bg, new Rectangle(x - 1, y - 1, W + 2, H + 2), hueNeutral);
            if (max <= 0) return;
            int fw = (int)(W * (cur / (float)max));
            if (fw < 1) fw = 1;
            batcher.Draw(fg, new Rectangle(x, y, fw, H), hueNeutral);
        }
    }
}

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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Compact party-HP list in top-right of viewport. One row per party
    /// member: name (12 chars) + 80×3 HP bar. Red below 35%. `-partyhud on|off`.
    /// </summary>
    public static class CompactPartyHud
    {
        public static bool Enabled;
        private const int W = 80, H = 3, ROW_H = 16;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (World.Party == null || !World.Party.InParty) return;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 170));
            Texture2D ok = SolidColorTextureCache.GetTexture(new Color(120, 220, 120, 230));
            Texture2D low = SolidColorTextureCache.GetTexture(new Color(220, 60, 60, 230));

            // Count valid rows first.
            int rows = 0;
            for (int i = 0; i < World.Party.Members.Length; i++)
            {
                var pm = World.Party.Members[i];
                if (pm == null || pm.Serial == 0) continue;
                rows++;
            }
            if (rows == 0) return;

            int x = bounds.Right - W - 30;
            int y = bounds.Y + 110;
            int totalH = rows * ROW_H + 4;
            batcher.Draw(bg, new Rectangle(x - 70, y - 2, W + 80, totalH), hueNeutral);

            int r = 0;
            for (int i = 0; i < World.Party.Members.Length; i++)
            {
                var pm = World.Party.Members[i];
                if (pm == null || pm.Serial == 0) continue;
                int ry = y + r * ROW_H;
                r++;

                string nm = pm.Name ?? "?";
                if (nm.Length > 14) nm = nm.Substring(0, 14);
                var tb = OverlayTextCache.Get("partyhud." + i, nm, 11, Color.White, 70);
                tb.Draw(batcher, x - 65, ry);

                int cur = 0, max = 0;
                var mob = World.Mobiles.Get(pm.Serial);
                if (mob != null) { cur = mob.Hits; max = mob.HitsMax; }
                if (max <= 0) max = 1; // avoid div-by-zero; bar will look full if no info yet

                bool isLow = cur * 100 / max < 35;
                batcher.Draw(Texture2D(), new Rectangle(x - 1, ry + 4 - 1, W + 2, H + 2), hueNeutral); // black border via bg
                int fw = (int)(W * (cur / (float)max));
                if (fw < 1) fw = 1;
                batcher.Draw(isLow ? low : ok, new Rectangle(x, ry + 4, fw, H), hueNeutral);
            }
        }

        // Tiny helper so we don't allocate a new Color each frame.
        private static Texture2D Texture2D()
            => SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 200));
    }
}

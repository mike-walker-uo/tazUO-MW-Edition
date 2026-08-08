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

using ClassicUO.Game.Data;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Tiny HP bar drawn over the head of any hostile/grey mob within Range
    /// that is below full HP. Skips innocents/allies/own pets. Quick PVM
    /// telemetry for kill priority. `-mobhp <range>` (0 = off).
    /// </summary>
    public static class CombatMobHpBars
    {
        public static int Range; // 0 = off
        private const int BAR_W = 28;
        private const int BAR_H = 3;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (Range <= 0) return;
            if (World.Player == null || !World.InGame) return;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 180));
            Texture2D red = SolidColorTextureCache.GetTexture(new Color(220, 30, 30, 220));
            Texture2D green = SolidColorTextureCache.GetTexture(new Color(60, 200, 60, 220));

            foreach (var m in MobileCache.Hostiles)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (m.Distance > Range) continue;
                if (m.HitsMax <= 0) continue;
                if (m.Hits >= m.HitsMax) continue;

                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent || n == NotorietyFlag.Invulnerable || n == NotorietyFlag.Ally) continue;

                Point p = PathPreview.TileToScreen(m.X, m.Y, m.Z);
                int x = p.X - BAR_W / 2;
                int y = p.Y - 32;

                batcher.Draw(bg, new Rectangle(x - 1, y - 1, BAR_W + 2, BAR_H + 2), hueNeutral);
                int fillW = (int)(BAR_W * (m.Hits / (float)m.HitsMax));
                if (fillW < 1) fillW = 1;
                bool low = m.Hits * 100 / m.HitsMax < 35;
                batcher.Draw(low ? red : green, new Rectangle(x, y, fillW, BAR_H), hueNeutral);
            }
        }
    }
}

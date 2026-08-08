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
    /// Small HP bar above any nearby pet (IsRenamable && notoriety not
    /// Enemy/Invuln) below full HP. Same filter PetStatusPanelGump uses.
    /// `-pethp on|off`.
    /// </summary>
    public static class PetHpBarsOverlay
    {
        public static bool Enabled;
        private const int W = 24;
        private const int H = 3;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int range = World.ClientViewRange;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 180));
            Texture2D ok = SolidColorTextureCache.GetTexture(new Color(120, 200, 255, 220));
            Texture2D low = SolidColorTextureCache.GetTexture(new Color(255, 140, 50, 220));

            foreach (var m in MobileCache.Pets)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (!m.IsRenamable) continue;
                if (m.NotorietyFlag == NotorietyFlag.Invulnerable || m.NotorietyFlag == NotorietyFlag.Enemy) continue;
                if (m.Distance > range) continue;
                if (m.HitsMax <= 0 || m.Hits >= m.HitsMax) continue;

                Point p = PathPreview.TileToScreen(m.X, m.Y, m.Z);
                int x = p.X - W / 2;
                int y = p.Y - 36;
                batcher.Draw(bg, new Rectangle(x - 1, y - 1, W + 2, H + 2), hueNeutral);
                int fw = (int)(W * (m.Hits / (float)m.HitsMax));
                if (fw < 1) fw = 1;
                bool isLow = m.Hits * 100 / m.HitsMax < 40;
                batcher.Draw(isLow ? low : ok, new Rectangle(x, y, fw, H), hueNeutral);
            }
        }
    }
}

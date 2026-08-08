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
    /// Pulsing red box outline around every hostile mob in view. Lighter than
    /// AggroIndicator (which only highlights recent aggressors). 28×40 rect
    /// roughly covering a typical mob sprite. `-hostilebox on|off`.
    /// </summary>
    public static class HostileEdgeHighlight
    {
        public static bool Enabled;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int range = World.ClientViewRange;
            long now = (long)Time.Ticks;
            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(now * 0.006f);
            int alpha = 60 + (int)(60 * pulse);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 50, 50, 255));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);

            const int W = 28, H = 40;
            foreach (var m in MobileCache.Hostiles)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent || n == NotorietyFlag.Invulnerable || n == NotorietyFlag.Ally) continue;
                if (m.Distance > range) continue;

                Point p = PathPreview.TileToScreen(m.X, m.Y, m.Z);
                int x = p.X - W / 2, y = p.Y - H + 4;
                // Four 1px edges.
                batcher.Draw(tex, new Rectangle(x, y, W, 1), hueNeutral);
                batcher.Draw(tex, new Rectangle(x, y + H - 1, W, 1), hueNeutral);
                batcher.Draw(tex, new Rectangle(x, y, 1, H), hueNeutral);
                batcher.Draw(tex, new Rectangle(x + W - 1, y, 1, H), hueNeutral);
            }
        }
    }
}

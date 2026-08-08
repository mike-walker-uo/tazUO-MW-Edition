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
    /// Draws a thin red line from player to the nearest visible hostile mob.
    /// Uses Bresenham-style 1px rectangles since UltimaBatcher2D has no
    /// native line primitive. Quick "where do I face" cue. `-hostileline on|off`.
    /// </summary>
    public static class NearestHostileLine
    {
        public static bool Enabled = false;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int range = World.ClientViewRange;
            GameObjects.Mobile best = null;
            int bestDist = int.MaxValue;
            foreach (var m in MobileCache.Hostiles)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent || n == NotorietyFlag.Invulnerable || n == NotorietyFlag.Ally) continue;
                int d = m.Distance;
                if (d > range) continue;
                if (d < bestDist) { bestDist = d; best = m; }
            }
            if (best == null) return;

            Point a = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);
            Point b = PathPreview.TileToScreen(best.X, best.Y, best.Z);
            DrawLine(batcher, a, b);
        }

        private static void DrawLine(UltimaBatcher2D batcher, Point a, Point b)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            int steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
            if (steps <= 0) return;
            // 2px quads stay contiguous with a 2px step — half the draw calls.
            int segments = System.Math.Min(steps / 2, 120);
            if (segments < 1) segments = 1;

            // Alpha is carried by the hue vector's 3rd component (the shader
            // ignores texture alpha for solid textures). Fades cubically from
            // ~0.6 at the player end to 0 at the mob.
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(120, 200, 255, 255));
            const float PEAK = 0.55f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;     // 0 at player, 1 at mob
                float falloff = 1f - t;
                float alpha = PEAK * falloff * falloff * falloff;
                if (alpha <= 0.02f) continue;
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
                int px = a.X + (int)(dx * t);
                int py = a.Y + (int)(dy * t);
                batcher.Draw(tex, new Rectangle(px - 1, py - 1, 2, 2), hue);
            }
        }
    }
}

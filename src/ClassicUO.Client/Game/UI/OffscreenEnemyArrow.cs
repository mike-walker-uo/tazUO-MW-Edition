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
    /// Finds the nearest non-innocent/ally mob in view-range, projects to
    /// screen, and if it lies outside the viewport draws a red square marker
    /// at the clamped edge. Helps spot ranged threats off-camera at zoom-in.
    /// `-offscreenarrow on|off`.
    /// </summary>
    public static class OffscreenEnemyArrow
    {
        public static bool Enabled = true;
        private const int MARKER_W = 14;
        private const int MARKER_H = 14;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;

            int range = World.ClientViewRange;
            ClassicUO.Game.GameObjects.Mobile best = null;
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

            Point p = PathPreview.TileToScreen(best.X, best.Y, best.Z);
            if (bounds.Contains(p)) return; // visible already

            // Clamp to viewport edge.
            int x = p.X;
            int y = p.Y;
            if (x < bounds.Left + MARKER_W / 2) x = bounds.Left + MARKER_W / 2;
            if (x > bounds.Right - MARKER_W / 2) x = bounds.Right - MARKER_W / 2;
            if (y < bounds.Top + MARKER_H / 2) y = bounds.Top + MARKER_H / 2;
            if (y > bounds.Bottom - MARKER_H / 2) y = bounds.Bottom - MARKER_H / 2;

            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(Time.Ticks * 0.006f);
            int alpha = 140 + (int)(115 * pulse);
            if (alpha > 255) alpha = 255;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 40, 40, 255));
            batcher.Draw(tex, new Rectangle(x - MARKER_W / 2, y - MARKER_H / 2, MARKER_W, MARKER_H), hueNeutral);
        }
    }
}

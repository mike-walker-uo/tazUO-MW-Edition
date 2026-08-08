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
    /// Persistent waypoint marker: if the target tile is on-screen, draws a
    /// pulsing yellow square on it; if off-screen, draws a 16×16 arrow at the
    /// viewport edge pointing toward it. `-wparrow set <x> <y>`, `-wparrow clear`.
    /// </summary>
    public static class WaypointArrowOverlay
    {
        public static int X, Y;
        public static bool HasTarget;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!HasTarget) return;
            if (World.Player == null || !World.InGame) return;

            Point p = PathPreview.TileToScreen(X, Y, World.Player.Z);
            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(Time.Ticks * 0.006f);
            int alpha = 140 + (int)(115 * pulse);
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 220, 80, 255));

            if (bounds.Contains(p))
            {
                // On-screen: pulsing 14×14 box.
                batcher.Draw(tex, new Rectangle(p.X - 7, p.Y - 7, 14, 14), hueNeutral);
                return;
            }

            // Off-screen: clamp to edge, draw direction marker.
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            int dx = p.X - cx, dy = p.Y - cy;
            // Scale (dx, dy) so the point lands on the bounds edge.
            float halfW = bounds.Width / 2f - 12, halfH = bounds.Height / 2f - 12;
            float t = 1f;
            if (dx != 0) t = System.Math.Min(t, halfW / System.Math.Abs(dx));
            if (dy != 0) t = System.Math.Min(t, halfH / System.Math.Abs(dy));
            int ex = cx + (int)(dx * t);
            int ey = cy + (int)(dy * t);
            batcher.Draw(tex, new Rectangle(ex - 8, ey - 8, 16, 16), hueNeutral);
        }

        public static void Set(int x, int y)
        {
            X = x; Y = y; HasTarget = true;
            GameActions.Print($"Waypoint arrow → {x},{y}.", 0x35);
        }

        public static void Clear() { HasTarget = false; GameActions.Print("Waypoint arrow cleared.", 0x21); }
    }
}

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
    /// Pulsing red border around the world viewport when player HP drops
    /// below ThresholdPct. Hard to miss even when zoomed-out into chaos.
    /// `-lowhp on|off|<pct>`.
    /// </summary>
    public static class LowHpVignette
    {
        public static bool Enabled = true;
        public static int ThresholdPct = 30;
        private const int BORDER_THICKNESS = 10;

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int max = World.Player.HitsMax;
            if (max <= 0) return;
            int pct = (int)(World.Player.Hits * 100f / max);
            if (pct >= ThresholdPct) return;

            // Pulse 0.4..1.0; faster as HP drops.
            float speed = 0.006f + 0.004f * (1f - pct / (float)ThresholdPct);
            float pulse = 0.4f + 0.6f * (0.5f + 0.5f * (float)System.Math.Sin(Time.Ticks * speed));
            int alpha = (int)(200 * pulse);
            if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;

            // Alpha must be on the hue vector — the shader ignores texture
            // alpha for solid textures.
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(200, 0, 0, 255));
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);

            int x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
            batcher.Draw(tex, new Rectangle(x, y, w, BORDER_THICKNESS), hue);
            batcher.Draw(tex, new Rectangle(x, y + h - BORDER_THICKNESS, w, BORDER_THICKNESS), hue);
            batcher.Draw(tex, new Rectangle(x, y, BORDER_THICKNESS, h), hue);
            batcher.Draw(tex, new Rectangle(x + w - BORDER_THICKNESS, y, BORDER_THICKNESS, h), hue);
        }
    }
}

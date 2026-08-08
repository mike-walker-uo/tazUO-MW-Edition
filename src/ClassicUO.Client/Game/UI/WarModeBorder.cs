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
    /// Thin pulsing red border drawn around the game viewport while the
    /// player has war mode toggled. Visual reminder to avoid mis-attacks
    /// when juggling other UI.
    /// </summary>
    public static class WarModeBorder
    {
        public static bool Enabled = false;
        private const int BORDER_THICKNESS = 3;

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"War-mode border {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (!World.Player.InWarMode) return;

            // Slow pulse 0.55..1.0 over ~1.6s.
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * (float)System.Math.Sin(Time.Ticks * 0.004f));
            int alpha = (int)(180 * pulse);
            if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(220, 30, 30, 255));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);

            int x = bounds.X;
            int y = bounds.Y;
            int w = bounds.Width;
            int h = bounds.Height;

            // Four edges.
            batcher.Draw(tex, new Rectangle(x, y, w, BORDER_THICKNESS), hueNeutral);
            batcher.Draw(tex, new Rectangle(x, y + h - BORDER_THICKNESS, w, BORDER_THICKNESS), hueNeutral);
            batcher.Draw(tex, new Rectangle(x, y, BORDER_THICKNESS, h), hueNeutral);
            batcher.Draw(tex, new Rectangle(x + w - BORDER_THICKNESS, y, BORDER_THICKNESS, h), hueNeutral);
        }
    }
}

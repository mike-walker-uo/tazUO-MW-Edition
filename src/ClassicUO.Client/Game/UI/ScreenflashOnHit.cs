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

using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Subscribes to CombatLogManager.Added; on an incoming damage event flashes
    /// a transparent red full-screen overlay that fades out over ~200 ms.
    /// Visceral cue that you got hit, useful when stats are off-screen.
    /// `-hitflash on|off`.
    /// </summary>
    public static class ScreenflashOnHit
    {
        public static bool Enabled;
        private const int FLASH_MS = 220;
        private static long _flashEndsAt;
        private static int _lastDmg;
        private static bool _hooked;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            CombatLogManager.Added += OnEntry;
            _hooked = true;
        }

        private static void OnEntry(CombatLogManager.Entry e)
        {
            if (!Enabled || e == null || !e.IsIncoming) return;
            _flashEndsAt = (long)Time.Ticks + FLASH_MS;
            _lastDmg = e.Damage;
        }

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled || _flashEndsAt == 0) return;
            long rem = _flashEndsAt - (long)Time.Ticks;
            if (rem <= 0) { _flashEndsAt = 0; return; }
            float pct = rem / (float)FLASH_MS;
            // Damage scales the peak alpha; band width scales with damage too.
            int strength = System.Math.Min(220, 30 + _lastDmg * 6);
            int peakAlpha = (int)(strength * pct);
            if (peakAlpha <= 0) return;
            int band = System.Math.Min(140, 40 + _lastDmg * 3);

            int x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;

            // Approximate radial fade by drawing several concentric edge bars
            // with decreasing alpha. Cheap (few draw calls) and keeps the
            // centre of the screen untouched.
            int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                int inset = (band * i) / steps;
                int thickness = (band / steps) + 1;
                int a = peakAlpha - (peakAlpha * i / steps);
                if (a <= 0) break;
                Texture2D tex = SolidColorTextureCache.GetTexture(new Color(220, 30, 30, 255));
                Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, a / 255f);
                // top
                batcher.Draw(tex, new Rectangle(x + inset, y + inset, w - 2 * inset, thickness), hueNeutral);
                // bottom
                batcher.Draw(tex, new Rectangle(x + inset, y + h - inset - thickness, w - 2 * inset, thickness), hueNeutral);
                // left
                batcher.Draw(tex, new Rectangle(x + inset, y + inset, thickness, h - 2 * inset), hueNeutral);
                // right
                batcher.Draw(tex, new Rectangle(x + w - inset - thickness, y + inset, thickness, h - 2 * inset), hueNeutral);
            }
        }
    }
}

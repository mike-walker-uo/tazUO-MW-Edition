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
    /// Draws a pulsing yellow diamond outline over TargetManager.LastAttack
    /// to make the current target unmistakable in messy combat. Toggle with
    /// `-targethighlight`.
    /// </summary>
    public static class LastTargetHighlight
    {
        public static bool Enabled = false;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            uint serial = TargetManager.LastAttack;
            if (serial == 0) return;
            if (World.Player != null && serial == World.Player.Serial) return;

            var mob = World.Mobiles.Get(serial);
            if (mob == null || mob.IsDestroyed) return;

            float pulse = 0.6f + 0.4f * (float)System.Math.Sin(Time.Ticks * 0.01f);
            int alpha = (int)(220 * pulse);
            if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 230, 90, 255));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);

            Point p = PathPreview.TileToScreen(mob.X, mob.Y, mob.Z);
            // Rectangle outline 38x38 centered around mob feet — visible without obscuring sprite.
            const int W = 40;
            const int H = 40;
            batcher.DrawRectangle(tex, p.X - W / 2, p.Y - H / 2, W, H, hueNeutral);
        }
    }
}

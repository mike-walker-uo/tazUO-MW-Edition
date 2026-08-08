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
    /// Crosshair over the last-targeted tile / static (TargetManager.LastTargetInfo).
    /// LastTargetHighlight already covers attack-target mobiles — this is for
    /// the "Last Target" used by spells / beneficials / static targets.
    /// `-tgtcross on|off`.
    /// </summary>
    public static class TargetCrosshair
    {
        public static bool Enabled;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            var lt = TargetManager.LastTargetInfo;
            if (lt == null || !lt.IsSet) return;
            if (!lt.IsLand && !lt.IsStatic) return; // mobile case handled elsewhere
            if (lt.X == 0xFFFF || lt.Y == 0xFFFF) return;

            Point p = PathPreview.TileToScreen(lt.X, lt.Y, lt.Z);
            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(Time.Ticks * 0.005f);
            int alpha = 110 + (int)(120 * pulse);
            if (alpha > 230) alpha = 230;
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 220, 120, 255));

            // Plus-shaped crosshair.
            batcher.Draw(tex, new Rectangle(p.X - 10, p.Y - 1, 20, 2), hueNeutral);
            batcher.Draw(tex, new Rectangle(p.X - 1, p.Y - 8, 2, 16), hueNeutral);
        }
    }
}

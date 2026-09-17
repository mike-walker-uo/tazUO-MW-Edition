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
    /// Thin shrinking bar above the player while MoveItem queue / global
    /// action cooldown is active. Lets the player see exactly when the next
    /// pickup/equip will fire. `-cdhud on|off`.
    /// </summary>
    public static class CooldownHud
    {
        public static bool Enabled = false;
        private const int BAR_W = 40;
        private const int BAR_H = 3;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (!GlobalActionCooldown.IsOnCooldown) return;

            long total = GlobalActionCooldown.CooldownDuration;
            if (total <= 0) return;
            long rem = GlobalActionCooldown.RemainingMs;
            float pct = 1f - (rem / (float)total);
            if (pct < 0f) pct = 0f;
            if (pct > 1f) pct = 1f;

            Point p = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);
            int x = p.X - BAR_W / 2;
            int y = p.Y - 52;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 180));
            Texture2D fg = SolidColorTextureCache.GetTexture(new Color(255, 200, 60, 220));

            batcher.Draw(bg, new Rectangle(x - 1, y - 1, BAR_W + 2, BAR_H + 2), hueNeutral);
            int w = (int)(BAR_W * pct);
            if (w < 1) w = 1;
            batcher.Draw(fg, new Rectangle(x, y, w, BAR_H), hueNeutral);
        }
    }
}

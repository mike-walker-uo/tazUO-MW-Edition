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

using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Small floating label that shows tile distance from the player to the
    /// object currently under the cursor. Toggleable with `-cursordist`.
    /// Off by default.
    /// </summary>
    public static class CursorDistanceOverlay
    {
        public static bool Enabled;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null) return;
            if (!(SelectedObject.Object is GameObject obj)) return;
            if (ReferenceEquals(obj, World.Player)) return;

            int dx = System.Math.Abs(obj.X - World.Player.X);
            int dy = System.Math.Abs(obj.Y - World.Player.Y);
            int cheb = System.Math.Max(dx, dy);

            // Draw a small "d=N" badge near the cursor.
            string txt = "d=" + cheb;
            var tb = OverlayTextCache.Get("cursordist", txt, 14, Color.White, 60);
            int x = Mouse.Position.X + 12;
            int y = Mouse.Position.Y + 12;

            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 160));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            batcher.Draw(bg, new Rectangle(x - 2, y - 1, tb.Width + 6, tb.Height + 2), hueNeutral);
            tb.Draw(batcher, x + 1, y);
        }
    }
}

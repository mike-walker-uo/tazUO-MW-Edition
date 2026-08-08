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
    /// 4×4 colored dot above every mob in view, hued by notoriety.
    /// Innocent=cyan, Ally=green, Gray=orange, Criminal=orange, Murderer=red,
    /// Invuln=yellow. Skips player. `-notdot on|off`.
    /// </summary>
    public static class NotorietyDotOverlay
    {
        public static bool Enabled;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int range = World.ClientViewRange;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);

            foreach (var m in MobileCache.All)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (m.Distance > range) continue;

                Color c = ColorFor(m.NotorietyFlag);
                Texture2D tex = SolidColorTextureCache.GetTexture(c);
                Point p = PathPreview.TileToScreen(m.X, m.Y, m.Z);
                batcher.Draw(tex, new Rectangle(p.X - 2, p.Y - 52, 4, 4), hueNeutral);
            }
        }

        private static Color ColorFor(NotorietyFlag n)
        {
            switch (n)
            {
                case NotorietyFlag.Innocent:    return new Color(120, 200, 255, 220);
                case NotorietyFlag.Ally:        return new Color(120, 230, 120, 220);
                case NotorietyFlag.Gray:        return new Color(200, 200, 200, 220);
                case NotorietyFlag.Criminal:    return new Color(255, 200, 80, 220);
                case NotorietyFlag.Enemy:       return new Color(255, 90, 90, 220);
                case NotorietyFlag.Murderer:    return new Color(255, 40, 40, 230);
                case NotorietyFlag.Invulnerable:return new Color(255, 230, 80, 220);
            }
            return new Color(200, 200, 200, 200);
        }
    }
}

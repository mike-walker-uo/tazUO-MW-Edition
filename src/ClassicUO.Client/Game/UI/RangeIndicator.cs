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
    /// Highlights the tile-radius square (Chebyshev distance) around the player
    /// — useful for verifying spell range, autoloot range, or PvP distance.
    /// Drawn after world tiles, before overhead text.
    /// </summary>
    public static class RangeIndicator
    {
        public static int Range; // 0 = off
        private const int TILE = 22;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (Range <= 0) return;
            if (World.Player == null) return;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(60, 160, 220, 90));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);

            int r = Range;
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    // Only draw the outer ring; interior would obscure terrain.
                    if (dx != -r && dx != r && dy != -r && dy != r) continue;

                    int tx = World.Player.X + dx;
                    int ty = World.Player.Y + dy;
                    Point p = PathPreview.TileToScreen(tx, ty, World.Player.Z);
                    batcher.Draw(tex, new Rectangle(p.X - TILE, p.Y - TILE, TILE * 2, TILE * 2), hueNeutral);
                }
            }
        }
    }
}

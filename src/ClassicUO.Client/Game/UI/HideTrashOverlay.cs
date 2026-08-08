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
    /// Dims ground items the autoloot list would NOT pick up (filtered "trash").
    /// Quick visual to spot meaningful drops in dungeon clutter.
    /// `-hidetrash <range>` (0 = off).
    /// </summary>
    public static class HideTrashOverlay
    {
        public static int Range; // 0 = off

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (Range <= 0) return;
            if (World.Player == null) return;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(20, 20, 20, 160));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);

            int r = Range;
            int px = World.Player.X, py = World.Player.Y;
            foreach (var it in MobileCache.GroundItems)
            {
                if (it == null || it.IsDestroyed || !it.OnGround) continue;
                if (it.IsCorpse) continue;
                if (!it.IsLootable) continue;
                if (!it.IsMovable) continue; // skip house-locked decorations
                // Only consider items that look like actual loot drops.
                if (!(it.ItemData.IsStackable || it.ItemData.IsWearable || it.IsCoin)) continue;
                int dx = System.Math.Abs(it.X - px), dy = System.Math.Abs(it.Y - py);
                if (System.Math.Max(dx, dy) > r) continue;

                bool keep = AutoLootManager.Instance != null && AutoLootManager.Instance.ItemIsOnLootList(it);
                if (keep) continue;

                Point p = PathPreview.TileToScreen(it.X, it.Y, it.Z);
                // 28x14 dim badge across the tile centre.
                batcher.Draw(tex, new Rectangle(p.X - 14, p.Y - 22, 28, 14), hueNeutral);
            }
        }
    }
}

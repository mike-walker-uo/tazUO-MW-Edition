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
    /// Marks empty corpses with a fading gray X. Tracks first-seen-empty
    /// time per serial so we can fade them out cosmetically before server
    /// despawn. Cheap O(open-corpse-items). `-corpsefade on|off`.
    /// </summary>
    public static class CorpseFadeOverlay
    {
        public static bool Enabled = false;
        private const int FADE_DURATION_MS = 60000; // 60s
        private static readonly System.Collections.Generic.Dictionary<uint, long> _emptySince
            = new System.Collections.Generic.Dictionary<uint, long>();

        public static void ResetSession() => _emptySince.Clear();

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int range = World.ClientViewRange;
            long now = (long)Time.Ticks;

            foreach (var it in MobileCache.GroundItems)
            {
                if (it == null || it.IsDestroyed) continue;
                if (!it.IsCorpse) continue;
                if (it.Distance > range) continue;
                if (!it.IsEmpty) { _emptySince.Remove(it.Serial); continue; }

                if (!_emptySince.TryGetValue(it.Serial, out long first))
                {
                    _emptySince[it.Serial] = now;
                    continue;
                }
                long age = now - first;
                if (age > FADE_DURATION_MS) age = FADE_DURATION_MS;
                float t = age / (float)FADE_DURATION_MS;
                int alpha = 60 + (int)(140 * t);

                Texture2D tex = SolidColorTextureCache.GetTexture(new Color(160, 160, 160, 255));
                Vector3 fadeHue = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
                Point p = PathPreview.TileToScreen(it.X, it.Y, it.Z);
                // Tiny "x" via two crossed bars.
                batcher.Draw(tex, new Rectangle(p.X - 6, p.Y - 6, 12, 2), fadeHue);
                batcher.Draw(tex, new Rectangle(p.X - 1, p.Y - 10, 2, 12), fadeHue);
            }
        }
    }
}

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
    /// Records the player's last N tile positions and draws fading dots over
    /// them. Cosmetic motion trail; useful in dark dungeons and demonstrates
    /// path coverage. Hooks EventSink.OnPositionChanged. `-trail on|off`.
    /// </summary>
    public static class MoveTrailOverlay
    {
        public static bool Enabled = true;
        private const int CAPACITY = 8;
        private static readonly (int x, int y, sbyte z, int colorIdx, long emitAt)[] _buf
            = new (int, int, sbyte, int, long)[CAPACITY];
        private const long DOT_LIFETIME_MS = 2500;
        private static int _count;
        private static int _head;
        private static bool _hooked;
        private static int _lastX = int.MinValue, _lastY = int.MinValue;
        private static int _nextColor;

        // Small blood-drip graphics. Wider rotation than just three ids so
        // the trail doesn't read as a stamped repeat. 0x122A is the big puddle
        // (skipped); 0x122B-0x122F are progressively smaller variants. Each
        // entry pairs the art id with a base scale so the slightly larger
        // sprites still read as "small drops".
        private static readonly (ushort graphic, float scale)[] BloodPalette =
        {
            (0x122F, 0.55f),
            (0x122E, 0.55f),
            (0x122D, 0.50f),
            (0x122F, 0.42f),
            (0x122E, 0.45f),
            (0x122D, 0.40f),
            (0x122C, 0.32f),  // slightly larger drip — occasional
            (0x122B, 0.28f),  // small puddle — rare
        };
        // Color array kept for buffer-struct compatibility; not drawn anymore.
        private static readonly Color[] Palette = { new Color(180, 30, 30) };

        public static void EnsureHooked()
        {
            if (_hooked) return;
            ClassicUO.Game.Managers.EventSink.OnPositionChanged += (s, e) => Push();
            _hooked = true;
        }

        private static void Push()
        {
            if (World.Player == null) return;
            int x = World.Player.X, y = World.Player.Y;
            if (x == _lastX && y == _lastY) return;
            _lastX = x; _lastY = y;
            _buf[_head] = (x, y, World.Player.Z, _nextColor, (long)Time.Ticks);
            _nextColor = (_nextColor + 1) % Palette.Length;
            _head = (_head + 1) % CAPACITY;
            if (_count < CAPACITY) _count++;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled || _count == 0) return;
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);

            long now = (long)Time.Ticks;
            // Walk from oldest to newest so newer drops paint over older.
            for (int i = 0; i < _count; i++)
            {
                int age = _count - 1 - i; // 0 = newest
                int idx = ((_head - 1 - age) % CAPACITY + CAPACITY) % CAPACITY;
                var (x, y, z, ci, emitAt) = _buf[idx];
                long ageMs = now - emitAt;
                if (ageMs >= DOT_LIFETIME_MS) continue; // faded out
                float lifePct = 1f - ageMs / (float)DOT_LIFETIME_MS; // 1 fresh → 0 gone
                int alpha = (int)(220 * lifePct);
                if (alpha <= 0) continue;

                // Deterministic per-emit pick + jitter (seed from emitAt so a
                // given drop renders identically every frame).
                int seed = unchecked((int)(emitAt ^ (long)ci));
                int paletteIdx = ((seed >> 3) & 0x7fffffff) % BloodPalette.Length;
                var entry = BloodPalette[paletteIdx];
                ref readonly var art = ref Client.Game.Arts.GetArt(entry.graphic);
                if (art.Texture == null) continue;

                // ±10% scale jitter and ±3px position jitter, derived from seed.
                float scaleJitter = 1f + ((((seed >> 11) & 31) - 15) / 150f); // ~0.9..1.1
                int jx = (((seed >> 17) & 7) - 3); // -3..+3
                int jy = (((seed >> 23) & 5) - 2); // -2..+2

                Vector3 hueVec = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
                Point p = PathPreview.TileToScreen(x, y, z);
                int w = (int)(art.UV.Width * entry.scale * scaleJitter);
                int h = (int)(art.UV.Height * entry.scale * scaleJitter);
                if (w < 4) w = 4; if (h < 3) h = 3;
                batcher.Draw(art.Texture,
                    new Rectangle(p.X - w / 2 + jx, p.Y - h / 2 + jy, w, h),
                    art.UV, hueVec);
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Move trail {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void ResetSession()
        {
            _count = 0;
            _head = 0;
            _lastX = _lastY = int.MinValue;
            _nextColor = 0;
        }
    }
}

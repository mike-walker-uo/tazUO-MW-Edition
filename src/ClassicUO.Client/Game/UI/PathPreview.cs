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

using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Highlights a list of tiles in the world to visualise a planned path.
    /// Public API: callable from RE / Razor Enhanced via reflection or direct
    /// reference to the ClassicUO.Client assembly.
    /// </summary>
    public static class PathPreview
    {
        public readonly struct Tile
        {
            public readonly int X, Y, Z;
            public Tile(int x, int y, int z) { X = x; Y = y; Z = z; }
        }

        private static readonly List<Tile> _tiles = new List<Tile>();
        private static long _expireTicks;
        private static ushort _hue = 0x0035; // light green by default
        private static bool _enabled;

        public static IReadOnlyList<Tile> Tiles => _tiles;
        public static bool HasActivePath =>
            _enabled && _tiles.Count > 0 && Time.Ticks < _expireTicks;

        /// <summary>
        /// Show a path. Each call replaces the previous one.
        /// </summary>
        public static void Show(IEnumerable<Tile> tiles, uint durationMs = 30000, ushort hue = 0x0035)
        {
            _tiles.Clear();
            if (tiles != null)
                _tiles.AddRange(tiles);
            _expireTicks = (long)Time.Ticks + durationMs;
            _hue = hue;
            _enabled = true;
        }

        /// <summary>
        /// Convenience overload taking flat int triples.
        /// </summary>
        public static void Show(IEnumerable<(int x, int y, int z)> coords, uint durationMs = 30000, ushort hue = 0x0035)
        {
            _tiles.Clear();
            if (coords != null)
                foreach (var c in coords)
                    _tiles.Add(new Tile(c.x, c.y, c.z));
            _expireTicks = (long)Time.Ticks + durationMs;
            _hue = hue;
            _enabled = true;
        }

        public static void Clear()
        {
            _tiles.Clear();
            _enabled = false;
        }

        public static ushort Hue
        {
            get
            {
                // Profile value wins once a profile is loaded.
                var prof = ClassicUO.Configuration.ProfileManager.CurrentProfile;
                if (prof != null && prof.PathPreviewHue != 0) return prof.PathPreviewHue;
                return _hue;
            }
            set
            {
                _hue = value;
                var prof = ClassicUO.Configuration.ProfileManager.CurrentProfile;
                if (prof != null) prof.PathPreviewHue = value;
            }
        }

        // Convert UO tile coords to screen-space using the same iso math
        // QuestArrowGump uses, then apply Camera.WorldToScreen.
        internal static Point TileToScreen(int tx, int ty, int tz)
        {
            var camera = Client.Game.Scene.Camera;
            int gox = World.Player.X - tx;
            int goy = World.Player.Y - ty;

            int x = (camera.Bounds.Width >> 1) - (gox - goy) * 22;
            int y = (camera.Bounds.Height >> 1) - (gox + goy) * 22;

            x -= (int)World.Player.Offset.X;
            y -= (int)(World.Player.Offset.Y - World.Player.Offset.Z);
            y += World.Player.Z << 2;
            y -= tz << 2;

            Point p = new Point(x, y);
            p = camera.WorldToScreen(p);
            p.X += camera.Bounds.X;
            p.Y += camera.Bounds.Y;
            return p;
        }

        /// <summary>
        /// Render hook called from GameScene.DrawOverheads while the world
        /// overlay batcher is in screen-space mode. Draws a coloured quad
        /// per active waypoint.
        /// </summary>
        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!HasActivePath) return;
            if (World.Player == null) return;

            const int W = 44;
            const int H = 44;
            const float BASE_FILL_ALPHA = 28f;   // 0..255 — much fainter than before
            const float BASE_BORDER_ALPHA = 70f;
            const float MIN_TAIL_FACTOR = 0.15f; // tail never goes fully invisible
            const float PULSE_AMP = 0.35f;       // ±35% modulation

            // Sin-based pulse, ~1.5 Hz, value in [1-PULSE_AMP, 1+PULSE_AMP].
            float pulse = 1f + PULSE_AMP * (float)System.Math.Sin(Time.Ticks * 0.006f);

            int count = _tiles.Count;
            for (int i = 0; i < count; i++)
            {
                var t = _tiles[i];
                Point p = TileToScreen(t.X, t.Y, t.Z);
                int rx = p.X - W / 2;
                int ry = p.Y - H / 2;

                // Head (i=0) = brightest, tail (i=count-1) fades to MIN_TAIL_FACTOR.
                float fade = count <= 1
                    ? 1f
                    : 1f - (1f - MIN_TAIL_FACTOR) * (i / (float)(count - 1));
                float intensity = fade * pulse;

                int fillA = (int)(BASE_FILL_ALPHA * intensity);
                int borderA = (int)(BASE_BORDER_ALPHA * intensity);
                if (fillA < 0) fillA = 0; else if (fillA > 255) fillA = 255;
                if (borderA < 0) borderA = 0; else if (borderA > 255) borderA = 255;

                Texture2D fillTex = SolidColorTextureCache.GetTexture(new Color(0, 200, 0, 255));
                Texture2D borderTex = SolidColorTextureCache.GetTexture(new Color(0, 255, 0, 255));
                Vector3 fillHue = ShaderHueTranslator.GetHueVector(0, false, fillA / 255f);
                Vector3 borderHue = ShaderHueTranslator.GetHueVector(0, false, borderA / 255f);

                batcher.Draw(fillTex, new Rectangle(rx, ry, W, H), fillHue);
                batcher.DrawRectangle(borderTex, rx, ry, W, H, borderHue);
            }
        }
    }
}

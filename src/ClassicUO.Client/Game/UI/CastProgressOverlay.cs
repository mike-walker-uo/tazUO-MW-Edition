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
    /// Small horizontal progress bar drawn above the player while a spell
    /// is being cast. Hooks EventSink.SpellCastBegin; estimates duration
    /// from spell circle (0.5s per circle). Visual cue you don't have on
    /// stock UO. `-castbar on|off`.
    /// </summary>
    public static class CastProgressOverlay
    {
        public static bool Enabled = true;
        private static bool _hooked;
        private static long _startedAt;
        private static long _endsAt;

        public static bool IsActive => _endsAt != 0 && (long)Time.Ticks < _endsAt;
        private const int BAR_W = 60;
        private const int BAR_H = 4;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.SpellCastBegin += OnCast;
            _hooked = true;
        }

        private static void OnCast(object sender, int spellId)
        {
            // Magery has 8 circles; spells 1..64. Circle = (id-1)/8 + 1. Add fixed prep.
            int circle = (spellId - 1) / 8 + 1;
            if (circle < 1) circle = 1;
            if (circle > 8) circle = 8;
            int durationMs = 250 + circle * 250; // 500ms .. 2250ms
            _startedAt = (long)Time.Ticks;
            _endsAt = _startedAt + durationMs;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (_endsAt == 0 || Time.Ticks >= _endsAt + 250) return;

            long now = (long)Time.Ticks;
            long total = _endsAt - _startedAt;
            if (total <= 0) return;
            float pct = (now - _startedAt) / (float)total;
            if (pct < 0f) pct = 0f;
            if (pct > 1f) pct = 1f;

            Point p = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);
            int x = p.X - BAR_W / 2;
            int y = p.Y - 60;

            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);
            Texture2D bg = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 200));
            Texture2D fill = SolidColorTextureCache.GetTexture(new Color(120, 160, 255, 230));

            batcher.Draw(bg, new Rectangle(x - 1, y - 1, BAR_W + 2, BAR_H + 2), hueNeutral);
            int w = (int)(BAR_W * pct);
            if (w < 1) w = 1;
            batcher.Draw(fill, new Rectangle(x, y, w, BAR_H), hueNeutral);
        }
    }
}

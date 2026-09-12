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
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Sprays blood splatter sprites around damaged mobs. Emit rate + sprite
    /// size scale with how hurt the mob is (lower HP% → more drops, slightly
    /// larger). Splatters persist on the ground for SPLATTER_LIFETIME_MS and
    /// fade alpha out. Capped count to keep the overlay cheap.
    /// `-mobblood on|off`.
    /// </summary>
    public static class MobBloodOverlay
    {
        public static bool Enabled = false;

        private struct Splatter
        {
            public int X, Y;
            public sbyte Z;
            public long EmitAt;
            public byte GIdx;     // index into BloodPalette
            public float Scale;
        }

        private const long POLL_INTERVAL_MS = 500;
        private const long SPLATTER_LIFETIME_MS = 9000;
        private const int  MAX_SPLATTERS = 250;
        private const int  HP_THRESHOLD_PCT = 80;     // only emit when below this

        private static long _nextPoll;
        private static readonly List<Splatter> _splatters = new List<Splatter>(MAX_SPLATTERS);
        private static readonly System.Random _rng = new System.Random();

        // Small blood-drip graphics (same set the MoveTrailOverlay uses).
        private static readonly (ushort g, float scale)[] BloodPalette =
        {
            (0x122F, 0.55f),
            (0x122E, 0.55f),
            (0x122D, 0.50f),
            (0x122F, 0.42f),
            (0x122E, 0.45f),
            (0x122D, 0.40f),
            (0x122C, 0.32f),
            (0x122B, 0.28f),
        };

        public static void Tick()
        {
            if (!Enabled || !SpellAbilityEffectSettings.CustomEffectsEnabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            int range = World.ClientViewRange;
            foreach (var m in MobileCache.All)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (m.HitsMax <= 0) continue;
                if (m.Distance > range) continue;
                // Innocents / allies don't bleed visibly.
                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent || n == NotorietyFlag.Invulnerable || n == NotorietyFlag.Ally) continue;

                int pct = m.Hits * 100 / m.HitsMax;
                if (pct >= HP_THRESHOLD_PCT) continue;

                // Emit probability + count scales with damage.
                //   80% hp → ~0.05 / poll, 1 drop at most
                //    1% hp → ~1.00 / poll, up to 3 drops
                float dmg = (HP_THRESHOLD_PCT - pct) / (float)HP_THRESHOLD_PCT; // 0..1
                int emits = 1 + (int)(2 * dmg * _rng.NextDouble());
                for (int i = 0; i < emits; i++)
                {
                    if (_rng.NextDouble() > 0.25 + 0.75 * dmg) continue;
                    SpawnSplatter(m, dmg);
                }
            }

            // Trim by lifetime + cap.
            long now = (long)Time.Ticks;
            for (int i = _splatters.Count - 1; i >= 0; i--)
                if (now - _splatters[i].EmitAt > SPLATTER_LIFETIME_MS)
                    _splatters.RemoveAt(i);
            while (_splatters.Count > MAX_SPLATTERS) _splatters.RemoveAt(0);
        }

        private static void SpawnSplatter(GameObjects.Mobile m, float dmg)
        {
            int dx = _rng.Next(-1, 2);
            int dy = _rng.Next(-1, 2);
            byte idx = (byte)_rng.Next(BloodPalette.Length);
            // Larger drops on lower HP — bias toward bigger palette entries.
            if (dmg > 0.6f && _rng.NextDouble() < 0.4) idx = (byte)_rng.Next(3); // 0..2 = larger
            float jitter = 0.85f + (float)_rng.NextDouble() * 0.4f; // 0.85..1.25
            _splatters.Add(new Splatter
            {
                X = m.X + dx,
                Y = m.Y + dy,
                Z = m.Z,
                EmitAt = (long)Time.Ticks,
                GIdx = idx,
                Scale = jitter,
            });
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled ||
                !Enabled || _splatters.Count == 0) return;
            long now = (long)Time.Ticks;
            for (int i = 0; i < _splatters.Count; i++)
            {
                var sp = _splatters[i];
                long age = now - sp.EmitAt;
                if (age >= SPLATTER_LIFETIME_MS) continue;
                float lifePct = 1f - age / (float)SPLATTER_LIFETIME_MS;
                int alpha = (int)(220 * lifePct);
                if (alpha <= 0) continue;

                var pal = BloodPalette[sp.GIdx];
                ref readonly var art = ref Client.Game.Arts.GetArt(pal.g);
                if (art.Texture == null) continue;
                Vector3 hueVec = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
                Point p = PathPreview.TileToScreen(sp.X, sp.Y, sp.Z);
                int w = (int)(art.UV.Width * pal.scale * sp.Scale);
                int h = (int)(art.UV.Height * pal.scale * sp.Scale);
                if (w < 4) w = 4; if (h < 3) h = 3;
                batcher.Draw(art.Texture,
                    new Rectangle(p.X - w / 2, p.Y - h / 2, w, h),
                    art.UV, hueVec);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on) _splatters.Clear();
            GameActions.Print($"Mob blood {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void ResetSession()
        {
            _nextPoll = 0;
            _splatters.Clear();
        }
    }
}

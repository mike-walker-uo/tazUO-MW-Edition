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

using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Spawns a small spark / glow effect on every visible paragon mob so
    /// they're unmistakably "glowing" beyond just the hue override. Paragons
    /// detected by hue 0x0501 or "(Paragon)" name substring.
    /// `-paragonglow on|off | hue <hex> | graphic <hex>`.
    /// </summary>
    public static class ParagonGlowManager
    {
        public static bool Enabled = true;
        public static ushort GlowHue = 0x0021;     // bright red
        public static ushort GlowGraphic = 0x376A; // sparkles

        private const long POLL_INTERVAL_MS = 600;
        private const long PER_MOB_GAP_MS = 1100;
        private static long _nextPoll;
        private static readonly Dictionary<uint, long> _last = new Dictionary<uint, long>();

        public static void Tick()
        {
            if (!Enabled || !SpellAbilityEffectSettings.CustomEffectsEnabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            int range = World.ClientViewRange;
            long now = (long)Time.Ticks;

            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m.Distance > range) continue;
                bool paragon = m.Hue == BodyScaleManager.PARAGON_HUE
                            || (!string.IsNullOrEmpty(m.Name)
                                && m.Name.IndexOf("(Paragon)", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!paragon) continue;
                if (_last.TryGetValue(m.Serial, out long t) && now - t < PER_MOB_GAP_MS) continue;
                _last[m.Serial] = now;

                World.SpawnEffect(
                    GraphicEffectType.FixedFrom,
                    m.Serial, m.Serial,
                    GlowGraphic, GlowHue,
                    (ushort)m.X, (ushort)m.Y, m.Z,
                    (ushort)m.X, (ushort)m.Y, m.Z,
                    5, 10,
                    true, false, false,
                    GraphicEffectBlendMode.Normal);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Paragon glow {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Adds a glow trail to projectiles fired by the player. World.SpawnEffect
    /// calls MaybeGlow after the real effect is created; if source == player
    /// and the graphic is in the arrow/bolt whitelist, a second moving glow
    /// is spawned on the same path. `-arrowglow on|off`.
    /// </summary>
    public static class ArrowGlowManager
    {
        public static bool Enabled = true;
        public static bool Debug; // prints every Moving effect from player so we can identify projectile graphics
        // Arrow on-screen sprite scale (View.DrawStaticRotated honors this).
        // 1.0 = stock, >1 = bigger arrow.
        public static float ArrowScale = 1.8f;
        // Glowing magical hue (high-band). `-arrowglow hue <hex>` to switch
        // (red 0x0026, orange 0x002B, yellow 0x0035, green 0x0044, blue 0x0058).
        public static ushort GlowHue = 0x0481;
        // Legacy field; kept for command compat. The glow now uses the
        // arrow's own graphic so it overlays exactly on top.
        public static ushort GlowGraphic = 0x0F42;

        // Common projectile graphics the server uses in moving-effect packets.
        // Add to this set via `-arrowglow add <hex>` if your shard uses
        // something custom.
        public static readonly HashSet<ushort> ArrowGraphics = new HashSet<ushort>
        {
            0x1BFE, 0x1BFB, 0x1BFC, 0x1BFD, 0x1BFF, // arrow/bolt in-flight variants
            0x0F3F, 0x0F4F,                          // arrow/bolt static
            0x0F42,                                  // UO Alive arrow projectile
            0x26B4, 0x26C2, 0x26C3,                  // SE bows/bolts
            0x2D1E, 0x2D1F, 0x2D26, 0x2D29,          // gargoyle / cyclone style
        };

        public static void MaybeGlow(
            GraphicEffectType type,
            uint source,
            uint target,
            ushort graphic,
            ushort srcX, ushort srcY, sbyte srcZ,
            ushort dstX, ushort dstY, sbyte dstZ,
            byte speed, int duration)
        {
            if (!Enabled || !SpellAbilityEffectSettings.CustomEffectsEnabled) return;
            if (World.Player == null) return;
            if (type != GraphicEffectType.Moving) return;

            if (Debug && source == World.Player.Serial)
                GameActions.Print($"[arrowglow debug] Moving fx graphic=0x{graphic:X4} from player → target 0x{target:X8}", 0x44);

            if (source != World.Player.Serial) return;
            if (!ArrowGraphics.Contains(graphic)) return;
            if (_inGlow) return; // re-entry guard — we re-spawn the same
                                  // graphic, which would otherwise recurse.

            // Spawn a duplicate of the arrow on the EXACT same path with a
            // saturated hue → renders as a bright colored arrow at the
            // arrow's position. Speed/duration matched so it overlays the
            // real projectile cleanly.
            _inGlow = true;
            try
            {
                World.SpawnEffect(
                    GraphicEffectType.Moving,
                    source, target,
                    graphic, GlowHue,
                    srcX, srcY, srcZ,
                    dstX, dstY, dstZ,
                    speed, duration,
                    true, false, false,
                    GraphicEffectBlendMode.Normal);
            }
            finally { _inGlow = false; }
        }

        private static bool _inGlow;

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Arrow glow {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

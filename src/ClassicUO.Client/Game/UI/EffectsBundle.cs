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
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Single home for thirteen small visual effects so we don't blow up the
    /// `Game/UI` folder. Each effect is independently toggled via `-fx <name>
    /// on|off`. All draw from GameScene.DrawOverheads via Effects.DrawWorld.
    /// </summary>
    public static class EffectsBundle
    {
        // ------------- toggles -------------
        // Defaults: most on; Crit and CastAura intentionally off.
        public static bool Crit = false, Heal = true, Death = true, CastAura = false,
                           LockRing = true, Stealth = false, StatusAura = true,
                           WalkDust = false, SpeedLines = true, Knockback = true,
                           LootPillar = true,
                           DarkAura = false;

        // ------------- shared state -------------
        // per-(serial, tag) re-spawn throttle so persistent FX don't flood EffectManager.
        private static readonly Dictionary<(uint, byte), long> _fxThrottle =
            new Dictionary<(uint, byte), long>();

        // walk dust trail (player only) — last position + emit time
        private static long _dustEmitAt;
        private static int _dustX, _dustY;
        private static sbyte _dustZ;

        // speed-lines: per-mob last-position sampling
        private static readonly Dictionary<uint, (int x, int y, long t)> _lastPos =
            new Dictionary<uint, (int, int, long)>();

        // knockback detection: per-mob last-hit time
        private static readonly Dictionary<uint, long> _lastHit = new Dictionary<uint, long>();

        // death detection: previously-alive set
        private static readonly Dictionary<uint, bool> _wasAlive = new Dictionary<uint, bool>();

        // heal detection: player last HP sample
        private static int _lastPlayerHp = -1;

        private static bool _hooked;
        private const int CRIT_THRESHOLD = 40;

        public static void ResetSession()
        {
            _fxThrottle.Clear();
            _lastPos.Clear();
            _lastHit.Clear();
            _wasAlive.Clear();
            _dustEmitAt = 0;
            _dustX = _dustY = 0;
            _dustZ = 0;
            _lastPlayerHp = -1;
        }

        public static void EnsureHooked()
        {
            if (_hooked) return;
            CombatLogManager.Added += OnCombat;
            EventSink.OnPositionChanged += (s, e) => OnPlayerMoved();
            _hooked = true;
        }

        private static void OnCombat(CombatLogManager.Entry e)
        {
            if (e == null || e.TargetSerial == 0) return;
            long now = (long)Time.Ticks;
            _lastHit[e.TargetSerial] = now;
            if (Crit && e.Damage >= CRIT_THRESHOLD)
            {
                // Real UO explosion graphic — looks like part of the game.
                SpawnFx(e.TargetSerial, 0x36BD /*explosion*/, 0x0021 /*red*/, 9);
            }
        }

        // Throttled re-spawn: only fires if (serial,tag) hasn't spawned within intervalMs.
        private static void SpawnFxThrottled(uint serial, ushort graphic, ushort hue,
                                             int durationFrames, long intervalMs, byte tag)
        {
            var key = (serial, tag);
            long now = (long)Time.Ticks;
            if (_fxThrottle.TryGetValue(key, out long last) && now - last < intervalMs) return;
            _fxThrottle[key] = now;
            SpawnFx(serial, graphic, hue, durationFrames);
        }

        // Spawn a fixed-from spell-style effect anchored on a mobile.
        private static void SpawnFx(uint serial, ushort graphic, ushort hue, int durationFrames)
        {
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled) return;

            Entity ent = serial == (World.Player?.Serial ?? 0)
                ? (Entity)World.Player
                : World.Mobiles.Get(serial);
            if (ent == null) return;
            World.SpawnEffect(
                GraphicEffectType.FixedFrom,
                serial, serial,
                graphic, hue,
                (ushort)ent.X, (ushort)ent.Y, ent.Z,
                (ushort)ent.X, (ushort)ent.Y, ent.Z,
                5, durationFrames,
                true, false, false,
                GraphicEffectBlendMode.Normal);
        }

        private static void OnPlayerMoved()
        {
            if (!WalkDust || World.Player == null) return;
            _dustEmitAt = (long)Time.Ticks;
            _dustX = World.Player.X;
            _dustY = World.Player.Y;
            _dustZ = World.Player.Z;
        }

        // ------------- main draw -------------
        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled ||
                World.Player == null || !World.InGame) return;
            long now = (long)Time.Ticks;
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, 1f);

            // Heal sparkle on player when HP rises.
            if (Heal)
            {
                int hp = World.Player.Hits;
                if (_lastPlayerHp < 0) _lastPlayerHp = hp;
                if (hp > _lastPlayerHp + 1)
                    SpawnFx(World.Player.Serial, 0x376A /*heal sparkles*/, 0x0481 /*soft green*/, 8);
                _lastPlayerHp = hp;
            }

            // Death stars on mob IsDead rising edge.
            if (Death)
            {
                foreach (var m in MobileCache.All)
                {
                    if (m == null || m.IsDestroyed) continue;
                    bool alive = !m.IsDead;
                    _wasAlive.TryGetValue(m.Serial, out bool wasAlive);
                    if (wasAlive && !alive)
                        SpawnFx(m.Serial, 0x3735 /*death stars*/, 0x0388 /*pale*/, 10);
                    _wasAlive[m.Serial] = alive;
                }
            }

            // Knockback puff (small explosion) when a mob moves within 250 ms of being hit.
            if (Knockback)
            {
                foreach (var kv in _lastHit)
                {
                    var m = World.Mobiles.Get(kv.Key);
                    if (m == null) continue;
                    if (now - kv.Value > 250) continue;
                    if (_lastPos.TryGetValue(m.Serial, out var prev))
                    {
                        if ((prev.x != m.X || prev.y != m.Y) && now - prev.t < 250)
                            SpawnFx(m.Serial, 0x36CB /*small explosion*/, 0x0494 /*orange*/, 6);
                    }
                }
            }

            // Maintain lastPos table for both speedlines and knockback.
            if (SpeedLines || Knockback)
            {
                foreach (var m in MobileCache.All)
                {
                    if (m == null || m.IsDestroyed) continue;
                    _lastPos.TryGetValue(m.Serial, out var prev);
                    if (prev.x != m.X || prev.y != m.Y)
                        _lastPos[m.Serial] = (m.X, m.Y, now);
                }
            }

            // Walk dust.
            if (WalkDust) DrawWalkDust(batcher, hueNeutral, now);

            // Spell-cast aura — uses CastProgressOverlay state for timing.
            if (CastAura) DrawCastAura(batcher, hueNeutral);

            // Target lock ring.
            if (LockRing) DrawLockRing(batcher, hueNeutral, now);

            // Stealth shimmer on player.
            if (Stealth && World.Player.IsHidden) DrawStealthShimmer(batcher, hueNeutral, now);

            // Status auras + bleed.
            if (StatusAura) DrawStatusAuras(batcher, hueNeutral, now);

            // Speed lines on mobs that moved this tick.
            if (SpeedLines) DrawSpeedLines(batcher, hueNeutral, now);

            // Loot pillar over corpses with items.
            if (LootPillar) DrawLootPillars(batcher, hueNeutral, now);

            // Dark shimmering aura around the player — slow pulsing dark glow.
            if (DarkAura) DrawDarkAura(batcher, hueNeutral, now);
        }

        // ----------------------------------------------------

private static void DrawWalkDust(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            long age = now - _dustEmitAt;
            if (_dustEmitAt == 0 || age > 400) return;
            float pct = 1f - age / 400f;
            int alpha = (int)(150 * pct);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(180, 160, 120, 255));
            Vector3 dustHue = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
            Point p = PathPreview.TileToScreen(_dustX, _dustY, _dustZ);
            int s = 6 + (int)(8 * (1f - pct));
            batcher.Draw(tex, new Rectangle(p.X - s / 2, p.Y - 2, s, 3), dustHue);
        }

        private static void DrawCastAura(UltimaBatcher2D batcher, Vector3 hueNeutral)
        {
            if (!CastProgressOverlay.IsActive) return;
            // Magic-reflect sparkles on the player every 700 ms during cast.
            SpawnFxThrottled(World.Player.Serial, 0x375A /*magic sparkles*/, 0x0481 /*soft blue/green*/, 6, 700, 11);
        }

        private static void DrawLockRing(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            uint t = TargetManager.LastAttack;
            if (t == 0) return;
            var m = World.Mobiles.Get(t);
            if (m == null || m.IsDestroyed || m.IsDead) return;
            // Periodic magic-glow effect anchored on target.
            SpawnFxThrottled(t, 0x37C4 /*glow*/, 0x0021 /*red*/, 6, 900, 10);
        }

        private static void DrawDarkAura(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            // Soft pulsing dark ring around the player's feet, painted as 3
            // concentric ellipse-bands with slow sine pulse. Dark purple-blue
            // base so it reads as a moody aura rather than a debuff.
            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(now * 0.0025f);
            Point p = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);

            // Outer faint band — wide, very transparent.
            int outerW = 80 + (int)(12 * pulse);
            int outerA = 30 + (int)(35 * pulse);
            Texture2D outer = SolidColorTextureCache.GetTexture(new Color(40, 20, 60, 255));
            DrawBand(batcher, ShaderHueTranslator.GetHueVector(0, false, outerA / 255f), outer, p.X, p.Y, outerW, 18);

            // Middle band — purple-blue.
            int midW = 56 + (int)(6 * pulse);
            int midA = 55 + (int)(50 * pulse);
            Texture2D mid = SolidColorTextureCache.GetTexture(new Color(60, 40, 110, 255));
            DrawBand(batcher, ShaderHueTranslator.GetHueVector(0, false, midA / 255f), mid, p.X, p.Y, midW, 12);

            // Inner faint glow.
            int innerW = 34;
            int innerA = 70 + (int)(70 * pulse);
            Texture2D inner = SolidColorTextureCache.GetTexture(new Color(90, 70, 150, 255));
            DrawBand(batcher, ShaderHueTranslator.GetHueVector(0, false, innerA / 255f), inner, p.X, p.Y, innerW, 8);
        }

        private static void DrawBand(UltimaBatcher2D batcher, Vector3 hueNeutral,
                                     Texture2D tex, int cx, int cy, int w, int h)
        {
            // 3-rect ellipse approximation.
            batcher.Draw(tex, new Rectangle(cx - w / 2 + 4, cy - 1, w - 8, 2), hueNeutral);
            batcher.Draw(tex, new Rectangle(cx - w / 2, cy, w, h - 4), hueNeutral);
            batcher.Draw(tex, new Rectangle(cx - w / 2 + 4, cy + h - 4, w - 8, 2), hueNeutral);
        }

        private static void DrawStealthShimmer(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            // Periodic faint sparkles around the player.
            SpawnFxThrottled(World.Player.Serial, 0x376A /*sparkles*/, 0x0481 /*pale*/, 6, 1800, 24);
        }

        private static void DrawStatusAuras(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            int range = World.ClientViewRange;
            foreach (var m in MobileCache.All)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m.Distance > range) continue;
                if (m.IsPoisoned)
                    SpawnFxThrottled(m.Serial, 0x36CC /*poison cloud*/, 0x0041 /*green*/, 14, 2200, 20);
                if (m.IsParalyzed)
                    SpawnFxThrottled(m.Serial, 0x376A /*sparkles*/, 0x05DE /*blue*/, 10, 2200, 21);
            }
            // Player bleed — small periodic red explosion under feet.
            if (World.Player.BuffIcons != null &&
                World.Player.BuffIcons.ContainsKey(BuffIconType.Bleed))
            {
                SpawnFxThrottled(World.Player.Serial, 0x36CB /*small explosion*/, 0x0021 /*red*/, 4, 1500, 22);
            }
        }

        private static void DrawSpeedLines(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            foreach (var kv in _lastPos)
            {
                if (now - kv.Value.t > 350) continue;
                var m = World.Mobiles.Get(kv.Key);
                if (m == null || m.IsDestroyed) continue;
                int dx = m.X - kv.Value.x;
                int dy = m.Y - kv.Value.y;
                if (dx == 0 && dy == 0) continue;
                Point pcur = PathPreview.TileToScreen(m.X, m.Y, m.Z);
                Point pprev = PathPreview.TileToScreen(kv.Value.x, kv.Value.y, m.Z);
                int alpha = 120;
                Texture2D tex = SolidColorTextureCache.GetTexture(new Color(220, 220, 220, 255));
                Vector3 speedHue = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
                // Thin segment from prev to cur centre.
                int sx = (pcur.X + pprev.X) / 2;
                int sy = (pcur.Y + pprev.Y) / 2 - 12;
                batcher.Draw(tex, new Rectangle(sx - 6, sy, 12, 1), speedHue);
            }
        }

        private static void DrawLootPillars(UltimaBatcher2D batcher, Vector3 hueNeutral, long now)
        {
            int range = World.ClientViewRange;
            foreach (var it in MobileCache.GroundItems)
            {
                if (it == null || it.IsDestroyed) continue;
                if (!it.IsCorpse) continue;
                if (it.IsEmpty) continue;
                if (it.Distance > range) continue;
                // Periodic golden glow on the corpse — uses UO light-glow graphic.
                SpawnFxThrottled(it.Serial, 0x37C4 /*glow*/, 0x0035 /*gold*/, 14, 3000, 23);
            }
        }
    }
}

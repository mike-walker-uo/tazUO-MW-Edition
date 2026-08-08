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
using ClassicUO.Assets;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// When a mob dies, captures its body + direction and renders a
    /// translucent ghost of it rising upward, shrinking, and fading over
    /// LIFETIME_MS. Pure visual; the actual corpse Item still spawns
    /// normally. `-ghostfade on|off`.
    /// </summary>
    public static class GhostFadeOverlay
    {
        public static bool Enabled = true;
        private const long LIFETIME_MS = 4500;
        // Rises far enough to clear the viewport top before the ghost
        // finishes fading.
        private const int  RISE_PIXELS = 600;

        private struct Ghost
        {
            public uint Serial;
            public long EmitAt;
            public int X, Y;
            public sbyte Z;
            public ushort Body;
            public byte Dir;
            public bool Mirror;
        }

        private static readonly List<Ghost> _ghosts = new List<Ghost>(64);
        // Tracks last-known position/body/dir for every visible mob so we
        // can spawn a ghost both on (a) IsDead rising edge AND (b) the mob
        // disappearing from World.Mobiles (server replaces dead mob with a
        // corpse Item — the Mobile itself often vanishes before IsDead is
        // ever observed).
        private struct Snapshot
        {
            public int X, Y;
            public sbyte Z;
            public ushort Body;
            public byte Dir;
            public bool Mirror;
            public bool Seen;       // present in last Tick()
            public bool IsPet;      // exclude from ghost emission
        }
        private static readonly Dictionary<uint, Snapshot> _tracked = new Dictionary<uint, Snapshot>();
        // Serials we already emitted a ghost for — prevents duplicate emit
        // when both the IsDead rising edge AND the later vanish-from-Mobiles
        // fire for the same kill.
        private static readonly HashSet<uint> _ghosted = new HashSet<uint>();
        // Reused scratch buffers so Tick() allocates zero per call.
        private static readonly List<uint> _scratchKeys = new List<uint>(64);
        private static readonly List<uint> _scratchStale = new List<uint>(16);

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;

            // Mark all tracked as not-seen, refresh as we walk live mobiles.
            // Snapshot keys into a reusable scratch list (zero per-tick alloc).
            _scratchKeys.Clear();
            foreach (var k in _tracked.Keys) _scratchKeys.Add(k);
            for (int i = 0; i < _scratchKeys.Count; i++)
            {
                uint k = _scratchKeys[i];
                var s = _tracked[k]; s.Seen = false; _tracked[k] = s;
            }

            foreach (var m in MobileCache.All)
            {
                if (m == null || m.IsDestroyed) continue;
                if (m == World.Player) continue;
                if (_ghosted.Contains(m.Serial)) continue; // already emitted

                byte dir = (byte)((byte)m.Direction & 7);
                bool mirror = false;
                try { AnimationsLoader.Instance.GetAnimDirection(ref dir, ref mirror); } catch { }

                bool hadPrev = _tracked.TryGetValue(m.Serial, out Snapshot prev);

                bool isPet = m.IsRenamable
                          && m.NotorietyFlag != Data.NotorietyFlag.Enemy
                          && m.NotorietyFlag != Data.NotorietyFlag.Invulnerable;

                // IsDead rising edge → spawn now while we still have position.
                if (hadPrev && m.IsDead)
                {
                    if (!prev.IsPet && !isPet)
                        SpawnGhost(m.Serial, prev.X, prev.Y, prev.Z, prev.Body, prev.Dir, prev.Mirror);
                    _tracked.Remove(m.Serial);
                    _ghosted.Add(m.Serial);
                    continue;
                }

                Snapshot snap = default;
                snap.X = m.X; snap.Y = m.Y; snap.Z = m.Z;
                snap.Body = m.Graphic;
                snap.Dir = dir; snap.Mirror = mirror;
                snap.Seen = true;
                snap.IsPet = isPet;
                _tracked[m.Serial] = snap;
            }

            // Anything still not-seen vanished → assume kill, spawn ghost
            // from the last known snapshot.
            _scratchStale.Clear();
            foreach (var kv in _tracked)
                if (!kv.Value.Seen) _scratchStale.Add(kv.Key);
            foreach (var serial in _scratchStale)
            {
                var s = _tracked[serial];
                // Range filter: skip range-exits (mob walked off-screen ≠ kill).
                int dx = s.X - World.Player.X, dy = s.Y - World.Player.Y;
                int chebyshev = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
                if (chebyshev <= 12 && !s.IsPet)
                    SpawnGhost(serial, s.X, s.Y, s.Z, s.Body, s.Dir, s.Mirror);
                _tracked.Remove(serial);
                _ghosted.Add(serial);
            }

            // Prune the dedupe set so serials get reusable after ghost expires.
            if (_ghosted.Count > 256)
            {
                _ghosted.Clear();
            }

            // Prune expired ghosts.
            long now = (long)Time.Ticks;
            for (int i = _ghosts.Count - 1; i >= 0; i--)
                if (now - _ghosts[i].EmitAt >= LIFETIME_MS)
                    _ghosts.RemoveAt(i);
        }

        private static void SpawnGhost(uint serial, int x, int y, sbyte z, ushort body, byte dir, bool mirror)
        {
            long now = (long)Time.Ticks;
            // Hard dedupe: never two live ghosts for one serial, AND no two
            // ghosts at the same tile within 800ms (suppresses the
            // rider/mount pair that "dies" together).
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i];
                if (g.Serial == serial) return;
                if (g.X == x && g.Y == y && now - g.EmitAt < 800) return;
            }

            _ghosts.Add(new Ghost
            {
                Serial = serial,
                EmitAt = (long)Time.Ticks,
                X = x, Y = y, Z = z,
                Body = body, Dir = dir, Mirror = mirror,
            });
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled || _ghosts.Count == 0) return;
            long now = (long)Time.Ticks;

            for (int i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i];
                long age = now - g.EmitAt;
                if (age >= LIFETIME_MS) continue;
                float t = age / (float)LIFETIME_MS;     // 0 → 1
                float scale = 1f - 0.7f * t;            // 1.0 → 0.3 (shrinks as it ascends)
                float alpha = (1f - t) * (1f - t) * 0.8f; // quadratic fade — stays visible longer mid-flight then fades hard
                if (alpha <= 0.01f) continue;
                // Eased rise: accelerates upward (t²) — like a soul lifting off.
                int yOffset = -(int)(RISE_PIXELS * t * (2f - t));

                byte animGroup = 0; // best-effort idle
                try { animGroup = (byte)Client.Game.Animations.GetAnimType(g.Body); } catch { }

                var frames = Client.Game.Animations.GetAnimationFrames(
                    g.Body, animGroup, g.Dir,
                    out _, out _,
                    isEquip: false, isCorpse: false, forceUOP: false);
                if (frames.Length == 0) continue;
                ref var sprite = ref frames[0];
                if (sprite.Texture == null) continue;

                Point p = PathPreview.TileToScreen(g.X, g.Y, g.Z);
                p.Y += yOffset;

                int sx = g.Mirror
                    ? p.X - (int)((sprite.UV.Width - sprite.Center.X) * scale)
                    : p.X - (int)(sprite.Center.X * scale);
                int sy = p.Y - (int)((sprite.UV.Height + sprite.Center.Y) * scale);

                // Slight cyan-white tint via hue 0x044E (pale blue), partial off.
                Vector3 hueVec = ShaderHueTranslator.GetHueVector(0x044E, false, alpha);

                batcher.Draw(
                    sprite.Texture,
                    new Vector2(sx, sy),
                    sprite.UV,
                    hueVec,
                    0f,
                    Vector2.Zero,
                    scale,
                    g.Mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                    0f);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on) _ghosts.Clear();
            GameActions.Print($"Ghost fade {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void ResetSession()
        {
            _ghosts.Clear();
            _tracked.Clear();
            _ghosted.Clear();
            _scratchKeys.Clear();
            _scratchStale.Clear();
        }
    }
}

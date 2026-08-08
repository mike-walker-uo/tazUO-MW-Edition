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
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Tracks recent attackers on the player. A serial counts as an aggressor
    /// when its damage was last seen against the player within AGGRO_WINDOW_MS.
    /// Drawn as a small red triangle just above each aggressor's overhead.
    /// </summary>
    public static class AggroIndicatorManager
    {
        private const long AGGRO_WINDOW_MS = 8000;

        // serial -> last-hit tick
        private static readonly Dictionary<uint, long> _aggressors = new Dictionary<uint, long>();
        private static long _lastPrune;

        // Toggle the visual triangle/ring/threat-bar overlay (-aggrobars). The
        // aggressor tracking itself stays on regardless so other systems can
        // consult IsAggressor.
        public static bool DrawEnabled = false;
        public static bool PlayAlertSound = false;
        public const int ALERT_SOUND_ID = 0x01D6; // "Cry for Help" / alert chime
        private const long MIN_ALERT_GAP_MS = 4000;
        private static long _lastAlertAt;

        public static void ResetSession()
        {
            _aggressors.Clear();
            _lastPrune = 0;
            _lastAlertAt = 0;
        }

        public static void OnPlayerHit(uint attackerSerial)
        {
            if (attackerSerial == 0) return;
            if (World.Player != null && attackerSerial == World.Player.Serial) return;
            bool isNew = !_aggressors.ContainsKey(attackerSerial);
            _aggressors[attackerSerial] = (long)Time.Ticks;
            // Feed damage-source-line overlay (4.7).
            UI.DamageSourceLineOverlay.RegisterHit(attackerSerial);

            if (PlayAlertSound && isNew && Time.Ticks - _lastAlertAt > MIN_ALERT_GAP_MS)
            {
                _lastAlertAt = (long)Time.Ticks;
                try { Client.Game?.Audio?.PlaySound(ALERT_SOUND_ID); } catch { }
            }
        }

        public static IReadOnlyDictionary<uint, long> GetAll() => _aggressors;

        public static bool IsAggressor(uint serial)
        {
            if (!_aggressors.TryGetValue(serial, out long t)) return false;
            return Time.Ticks - t < AGGRO_WINDOW_MS;
        }

        public static void Tick()
        {
            if (Time.Ticks - _lastPrune < 2000) return;
            _lastPrune = (long)Time.Ticks;
            var toRemove = new List<uint>();
            foreach (var kv in _aggressors)
                if (Time.Ticks - kv.Value >= AGGRO_WINDOW_MS) toRemove.Add(kv.Key);
            for (int i = 0; i < toRemove.Count; i++) _aggressors.Remove(toRemove[i]);
        }

        // Threat tiers by HitsMax. Tune per shard if needed.
        private const int THREAT_HIGH = 500;
        private const int THREAT_MED  = 150;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!DrawEnabled) return;
            if (World.Player == null || _aggressors.Count == 0) return;

            float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Time.Ticks * 0.008f);
            Texture2D markerTex = SolidColorTextureCache.GetTexture(new Color(255, 40, 40, 255));
            Vector3 hueNeutral = ShaderHueTranslator.GetHueVector(0, false, pulse * 0.78f);

            foreach (var kv in _aggressors)
            {
                if (Time.Ticks - kv.Value >= AGGRO_WINDOW_MS) continue;
                var mob = World.Mobiles.Get(kv.Key);
                if (mob == null || mob.IsDestroyed) continue;

                Point p = ClassicUO.Game.UI.PathPreview.TileToScreen(mob.X, mob.Y, mob.Z);
                // Aggression marker 12x6 just above tile.
                int w = 12, h = 6;
                batcher.Draw(markerTex, new Rectangle(p.X - w / 2, p.Y - 36, w, h), hueNeutral);

                // Pulsing red ring at the mob's feet (3-rect ellipse approximation).
                Texture2D ringTex = SolidColorTextureCache.GetTexture(new Color(255, 30, 30, 255));
                const int RW = 38, RH = 10;
                batcher.Draw(ringTex, new Rectangle(p.X - RW / 2 + 2, p.Y - 1, RW - 4, 2), hueNeutral);
                batcher.Draw(ringTex, new Rectangle(p.X - RW / 2, p.Y, RW, RH - 6), hueNeutral);
                batcher.Draw(ringTex, new Rectangle(p.X - RW / 2 + 2, p.Y + RH - 6, RW - 4, 2), hueNeutral);

                // Threat indicator — small colored bar above marker based on HitsMax.
                int maxHp = mob.HitsMax;
                if (maxHp >= THREAT_MED)
                {
                    Color tier = maxHp >= THREAT_HIGH
                        ? new Color(255, 60, 60, 220)   // red
                        : new Color(255, 200, 60, 220); // amber
                    Texture2D tierTex = SolidColorTextureCache.GetTexture(tier);
                    int tw = maxHp >= THREAT_HIGH ? 16 : 12;
                    batcher.Draw(tierTex, new Rectangle(p.X - tw / 2, p.Y - 46, tw, 4), hueNeutral);
                }
            }
        }
    }
}

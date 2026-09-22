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
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Passively watches the player's tame followers (best-effort detection via
    /// IsRenamable + non-Enemy/Invulnerable, same heuristic as PetStatusPanel).
    /// Toasts when a pet drops below an HP threshold or strays beyond a tile
    /// distance from the player. Cooldown per-pet to prevent spam.
    /// </summary>
    public static class PetWatcherManager
    {
        public static bool Enabled;
        public static int HpThresholdPercent = 30;
        public static int DistanceWarn = 8;
        private const long CHECK_INTERVAL_MS = 1500;
        private const long REPEAT_COOLDOWN_MS = 8000;

        private static long _nextCheck;
        private static readonly Dictionary<uint, long> _hpWarnAt = new Dictionary<uint, long>();
        private static readonly Dictionary<uint, long> _distWarnAt = new Dictionary<uint, long>();

        public static void ResetSession()
        {
            _nextCheck = 0;
            _hpWarnAt.Clear();
            _distWarnAt.Clear();
        }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextCheck) return;
            _nextCheck = (long)Time.Ticks + CHECK_INTERVAL_MS;

            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed) continue;
                if (m == World.Player) continue;
                if (!m.IsRenamable) continue;
                if (m.NotorietyFlag == NotorietyFlag.Invulnerable) continue;
                if (m.NotorietyFlag == NotorietyFlag.Enemy) continue;

                // HP check.
                if (m.HitsMax > 0)
                {
                    int pct = (int)((float)m.Hits / m.HitsMax * 100);
                    if (pct <= HpThresholdPercent && !m.IsDead)
                    {
                        if (!_hpWarnAt.TryGetValue(m.Serial, out long last) ||
                            Time.Ticks - last > REPEAT_COOLDOWN_MS)
                        {
                            _hpWarnAt[m.Serial] = (long)Time.Ticks;
                            string nm = string.IsNullOrEmpty(m.Name) ? "Pet" : m.Name;
                            ToastManager.Show($"{nm} HP {pct}%", 0x21, 4000, "pet-health",
                                AlertCategory.Pets, AlertSeverity.Warning);
                        }
                    }
                    else if (pct > HpThresholdPercent + 10)
                    {
                        _hpWarnAt.Remove(m.Serial);
                    }
                }

                // Distance check.
                int dist = m.Distance;
                if (dist > DistanceWarn)
                {
                    if (!_distWarnAt.TryGetValue(m.Serial, out long last) ||
                        Time.Ticks - last > REPEAT_COOLDOWN_MS)
                    {
                        _distWarnAt[m.Serial] = (long)Time.Ticks;
                        string nm = string.IsNullOrEmpty(m.Name) ? "Pet" : m.Name;
                        ToastManager.Show($"{nm} is {dist} tiles away", 0x35, 4000, "pet-distance",
                            AlertCategory.Pets, AlertSeverity.Warning);
                    }
                }
                else if (dist < DistanceWarn - 2)
                {
                    _distWarnAt.Remove(m.Serial);
                }
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print(
                $"Pet watcher {(on ? "ON" : "OFF")} (HP<{HpThresholdPercent}%, dist>{DistanceWarn}).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void SetHpThreshold(int pct)
        {
            if (pct < 1) pct = 1;
            if (pct > 99) pct = 99;
            HpThresholdPercent = pct;
        }

        public static void SetDistance(int d)
        {
            if (d < 2) d = 2;
            if (d > 30) d = 30;
            DistanceWarn = d;
        }
    }
}

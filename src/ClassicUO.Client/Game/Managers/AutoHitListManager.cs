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
using System.IO;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// User-managed list of name substrings. When a hostile/grey mob whose name
    /// contains any pattern enters view and the player has no current
    /// LastAttack target (or current target is invalid), auto-set LastAttack.
    /// Optional auto-fire of Attack via `-autohit attackmode on`.
    /// </summary>
    public static class AutoHitListManager
    {
        private const string FILENAME = "autohit.tsv";
        public static bool Enabled;
        public static bool AlsoAttack;
        private static readonly HashSet<string> _patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        private const long CHECK_INTERVAL_MS = 800;
        private static long _nextCheck;

        public static IReadOnlyCollection<string> Patterns => _patterns;

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "autohit.tsv");

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
                if (!string.IsNullOrWhiteSpace(line)) _patterns.Add(line.Trim());
        }

        public static void Save()
        {
            ProfileDataStore.WriteAllLines(FILENAME, _patterns);
        }

        public static void ResetForProfile()
        {
            _patterns.Clear();
            _loaded = false;
            _nextCheck = 0;
            Enabled = false;
            AlsoAttack = false;
        }

        public static void AddPattern(string pat) { EnsureLoaded(); if (!string.IsNullOrWhiteSpace(pat)) { _patterns.Add(pat.Trim()); Save(); } }
        public static void RemovePattern(string pat) { EnsureLoaded(); if (_patterns.Remove(pat)) Save(); }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextCheck) return;
            _nextCheck = (long)Time.Ticks + CHECK_INTERVAL_MS;
            EnsureLoaded();
            if (_patterns.Count == 0) return;

            // Skip if current target is alive in view.
            uint last = TargetManager.LastAttack;
            if (last != 0)
            {
                var cur = World.Mobiles.Get(last);
                if (cur != null && !cur.IsDestroyed && !cur.IsDead && cur.Distance <= World.ClientViewRange)
                    return;
            }

            // Find nearest match.
            GameObjects.Mobile best = null;
            int bestDist = int.MaxValue;
            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (string.IsNullOrEmpty(m.Name)) continue;
                if (m.NotorietyFlag == Data.NotorietyFlag.Innocent ||
                    m.NotorietyFlag == Data.NotorietyFlag.Invulnerable ||
                    m.NotorietyFlag == Data.NotorietyFlag.Ally) continue;

                bool matched = false;
                foreach (var p in _patterns)
                {
                    if (m.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) { matched = true; break; }
                }
                if (!matched) continue;

                int d = m.Distance;
                if (d > World.ClientViewRange) continue;
                if (d < bestDist) { bestDist = d; best = m; }
            }

            if (best != null)
            {
                if (!AutomationCoordinator.TryAcquire("AutoHit", 250, requiresTarget: true)) return;
                TargetManager.LastAttack = best.Serial;
                if (AlsoAttack) GameActions.Attack(best.Serial);
            }
        }
    }
}

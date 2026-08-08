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

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// When the player's LastAttack target dies or vanishes, automatically
    /// picks the nearest hostile mobile and sets it as LastAttack. Optionally
    /// fires Attack on the new target.
    /// </summary>
    public static class AutoRespawnTargetManager
    {
        public static bool Enabled;
        public static bool AlsoAttack;
        private const long CHECK_INTERVAL_MS = 400;
        private static long _nextCheck;

        public static void ResetSession() => _nextCheck = 0;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextCheck) return;
            _nextCheck = (long)Time.Ticks + CHECK_INTERVAL_MS;

            uint last = TargetManager.LastAttack;
            // Trigger when last target is invalid OR dead OR out of range.
            if (last == 0 || last == World.Player.Serial)
                return;

            var current = World.Mobiles.Get(last);
            if (current != null && !current.IsDestroyed && !current.IsDead &&
                current.Distance <= World.ClientViewRange)
                return;

            // Find next hostile.
            Mobile best = null;
            int bestDist = int.MaxValue;
            int range = World.ClientViewRange;
            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed) continue;
                if (m == World.Player) continue;
                if (m.IsDead) continue;
                var n = m.NotorietyFlag;
                if (n != NotorietyFlag.Criminal &&
                    n != NotorietyFlag.Enemy &&
                    n != NotorietyFlag.Murderer &&
                    n != NotorietyFlag.Gray)
                    continue;
                int d = m.Distance;
                if (d > range) continue;
                if (d < bestDist) { bestDist = d; best = m; }
            }

            if (best == null)
            {
                // No replacement target found; clear so we don't keep checking.
                TargetManager.LastAttack = 0;
                return;
            }

            if (!AutomationCoordinator.TryAcquire("AutoRespawnTarget", 250, requiresTarget: true)) return;
            TargetManager.LastAttack = best.Serial;
            if (AlsoAttack)
                GameActions.Attack(best.Serial);
            UI.Gumps.ToastManager.Show($"Target switched: {best.Name ?? "<unknown>"} (d={bestDist})", 0x35, 2500);
        }

        public static void SetEnabled(bool on, bool alsoAttack)
        {
            Enabled = on;
            AlsoAttack = alsoAttack;
            GameActions.Print(
                $"Auto-respawn-target {(on ? "ON" : "OFF")}{(alsoAttack ? " (+ attack)" : "")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

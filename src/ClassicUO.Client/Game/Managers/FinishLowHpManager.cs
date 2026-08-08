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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Picks the lowest-HP hostile mob in view-range and sets it as LastAttack;
    /// optionally fires Attack via `-finishlow attack`. Helps focus-fire on
    /// nearly-dead mobs to clear waves faster. One-shot, not a tick.
    /// </summary>
    public static class FinishLowHpManager
    {
        public static void Trigger(bool alsoAttack)
        {
            if (World.Player == null || !World.InGame) return;
            int range = World.ClientViewRange;
            GameObjects.Mobile best = null;
            int bestHpPct = int.MaxValue;

            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (m.HitsMax <= 0) continue;
                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent || n == NotorietyFlag.Invulnerable || n == NotorietyFlag.Ally) continue;
                if (m.Distance > range) continue;

                int hpPct = m.Hits * 100 / m.HitsMax;
                if (hpPct >= 100) continue; // skip untouched mobs
                if (hpPct < bestHpPct) { bestHpPct = hpPct; best = m; }
            }

            if (best == null)
            {
                GameActions.Print("No damaged hostile in range.", 0x21);
                return;
            }
            TargetManager.LastAttack = best.Serial;
            GameActions.Print($"Finish: {best.Name ?? "?"} ({bestHpPct}%)", 0x44);
            if (alsoAttack) GameActions.Attack(best.Serial);
        }
    }
}

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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Cycles LastTargetInfo through living party members. Each `-partycycle`
    /// call picks the next member after the currently-targeted one (or first
    /// member if none). Combined with macros, lets the player heal-cycle.
    /// </summary>
    public static class PartyCycleManager
    {
        public static void Next()
        {
            if (World.Player == null || World.Party == null || !World.Party.InParty)
            {
                GameActions.Print("Not in a party.", 0x21);
                return;
            }

            var members = World.Party.Members;
            if (members == null) return;

            // Build serial list of valid members (self last so we cycle teammates first).
            var list = new System.Collections.Generic.List<uint>(members.Length);
            for (int i = 0; i < members.Length; i++)
            {
                var pm = members[i];
                if (pm == null) continue;
                uint ser = pm.Serial;
                if (ser == 0) continue;
                var mob = World.Mobiles.Get(ser);
                if (mob == null || mob.IsDestroyed || mob.IsDead) continue;
                if (ser == World.Player.Serial) continue;
                list.Add(ser);
            }
            // Include self at the end so single-player parties still target self.
            list.Add(World.Player.Serial);
            if (list.Count == 0) return;

            uint cur = TargetManager.LastTargetInfo?.Serial ?? 0;
            int idx = list.IndexOf(cur);
            int next = (idx < 0) ? 0 : (idx + 1) % list.Count;
            uint pick = list[next];

            TargetManager.LastTargetInfo.SetEntity(pick);
            var picked = World.Mobiles.Get(pick);
            string nm = picked?.Name ?? $"0x{pick:X8}";
            GameActions.Print($"Party target → {nm}", 0x44);
        }
    }
}

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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// `-jump <name>` pathfinds toward a party member or visible mobile whose
    /// name matches the substring. Cheap travel helper for grouping up after
    /// a teleport. Falls back to "no match" if no candidate found.
    /// </summary>
    public static class JumpToPlayerManager
    {
        public static void JumpTo(string namePart)
        {
            if (string.IsNullOrWhiteSpace(namePart) || World.Player == null) return;

            // Party first (often the most desired).
            if (World.Party != null && World.Party.InParty)
            {
                foreach (var pm in World.Party.Members)
                {
                    if (pm == null) continue;
                    var mob = World.Mobiles.Get(pm.Serial);
                    if (mob == null || mob.IsDestroyed) continue;
                    string nm = mob.Name ?? pm.Name;
                    if (string.IsNullOrEmpty(nm)) continue;
                    if (nm.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Pathfinder.WalkTo(mob.X, mob.Y, mob.Z, 1);
                    GameActions.Print($"Jumping to party {nm}.", 0x35);
                    return;
                }
            }

            // Any visible mobile.
            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (string.IsNullOrEmpty(m.Name)) continue;
                if (m.Name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) < 0) continue;
                Pathfinder.WalkTo(m.X, m.Y, m.Z, 1);
                GameActions.Print($"Jumping to {m.Name}.", 0x35);
                return;
            }

            GameActions.Print($"No mobile matching '{namePart}'.", 0x21);
        }
    }
}

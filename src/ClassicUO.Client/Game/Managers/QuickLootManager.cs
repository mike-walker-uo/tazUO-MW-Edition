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

using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Single-keypress corpse looter: find the nearest reachable corpse,
    /// double-click to open it, then queue all lootable items through
    /// MoveItemQueue once contents arrive from the server.
    /// </summary>
    public static class QuickLootManager
    {
        private static uint _pendingCorpse;
        private static long _pendingExpire;
        private static bool _hooked;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OnOpenContainer += OnContainerOpened;
            _hooked = true;
        }

        public static void Trigger()
        {
            if (World.Player == null || !World.InGame)
            {
                GameActions.Print("Not in game.", 0x21);
                return;
            }

            int maxRange = ProfileManager.CurrentProfile?.AutoOpenCorpseRange ?? 3;

            Item best = null;
            int bestDist = int.MaxValue;
            foreach (var it in World.Items.Values)
            {
                if (it == null || it.IsDestroyed) continue;
                if (!it.IsCorpse) continue;
                int d = it.Distance;
                if (d > maxRange) continue;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = it;
                }
            }

            if (best == null)
            {
                GameActions.Print($"No corpse within range {maxRange}.", 0x21);
                return;
            }

            _pendingCorpse = best.Serial;
            _pendingExpire = (long)Time.Ticks + 5000;

            // Double-click triggers server to send container contents. The
            // OnOpenContainer event fires when contents arrive; we then loot.
            GameActions.DoubleClick(best.Serial);
            GameActions.Print($"Quick-loot armed for corpse (d={bestDist}).", 0x35);
        }

        private static void OnContainerOpened(object sender, uint serial)
        {
            if (_pendingCorpse == 0) return;
            if (serial != _pendingCorpse) return;
            if (Time.Ticks > _pendingExpire)
            {
                _pendingCorpse = 0;
                return;
            }

            _pendingCorpse = 0;
            _pendingExpire = 0;

            var corpse = World.Items.Get(serial);
            if (corpse == null || corpse.IsDestroyed) return;

            int looted = 0;
            for (LinkedObject i = corpse.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                if (!it.IsLootable) continue;
                MoveItemQueue.Instance?.EnqueueQuick(it);
                looted++;
            }

            GameActions.Print($"Quick-loot queued {looted} item(s).", 0x35);
        }
    }
}

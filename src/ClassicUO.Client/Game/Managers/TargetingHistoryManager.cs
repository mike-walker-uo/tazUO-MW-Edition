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
    /// Maintains a ring of the last 5 attacked serials, populated by polling
    /// `TargetManager.LastAttack` for changes. `-lastenemy <0..4>` re-sets it
    /// as the current LastAttack so the player can rotate among multiple
    /// targets without re-clicking. `-lastenemy list` prints them.
    /// </summary>
    public static class TargetingHistoryManager
    {
        public const int CAPACITY = 5;
        private static readonly uint[] _ring = new uint[CAPACITY];
        private static int _count;
        private static uint _lastSeen;
        private const long POLL_INTERVAL_MS = 500;
        private static long _nextPoll;

        public static void ResetSession()
        {
            System.Array.Clear(_ring, 0, _ring.Length);
            _count = 0;
            _lastSeen = 0;
            _nextPoll = 0;
        }

        public static void Tick()
        {
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            uint cur = TargetManager.LastAttack;
            if (cur == 0 || cur == _lastSeen) return;
            _lastSeen = cur;

            // Skip if already at index 0.
            if (_count > 0 && _ring[0] == cur) return;
            // Shift right and put at front.
            for (int i = CAPACITY - 1; i > 0; i--) _ring[i] = _ring[i - 1];
            _ring[0] = cur;
            if (_count < CAPACITY) _count++;
        }

        public static void Recall(int index)
        {
            if (index < 0 || index >= _count) { GameActions.Print($"No history #{index}.", 0x21); return; }
            uint ser = _ring[index];
            var m = World.Mobiles.Get(ser);
            if (m == null || m.IsDestroyed || m.IsDead)
            {
                GameActions.Print($"#{index} no longer in view.", 0x21);
                return;
            }
            TargetManager.LastAttack = ser;
            GameActions.Print($"Re-targeted #{index}: {m.Name ?? "?"}", 0x44);
        }

        public static void List()
        {
            for (int i = 0; i < _count; i++)
            {
                var m = World.Mobiles.Get(_ring[i]);
                string nm = m?.Name ?? "(out of view)";
                GameActions.Print($"  {i}: 0x{_ring[i]:X8} {nm}", 0x44);
            }
            if (_count == 0) GameActions.Print("  (empty)", 0x21);
        }
    }
}

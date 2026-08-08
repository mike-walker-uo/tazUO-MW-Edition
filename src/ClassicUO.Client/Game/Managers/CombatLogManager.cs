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
using ClassicUO.Utility.Collections;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Rolling log of all damage events the client sees. Direction = +outgoing
    /// / -incoming (relative to player). Capped at 200 entries.
    /// </summary>
    public static class CombatLogManager
    {
        public class Entry
        {
            public DateTime Time;
            public uint TargetSerial;
            public string TargetName;
            public int Damage;
            public bool IsIncoming;
        }

        public const int CAPACITY = 200;
        private static readonly Deque<Entry> _entries = new Deque<Entry>(CAPACITY);
        public static event Action<Entry> Added;

        public static System.Collections.Generic.IEnumerable<Entry> All() => _entries;
        public static int Count => _entries.Count;

        public static void Record(uint targetSerial, int damage)
        {
            if (damage <= 0) return;
            while (_entries.Count >= CAPACITY) _entries.RemoveFromFront();
            bool incoming = World.Player != null && targetSerial == World.Player.Serial;
            string name = incoming
                ? (World.Player?.Name ?? "Me")
                : World.Mobiles.Get(targetSerial)?.Name ?? "0x" + targetSerial.ToString("X");
            var e = new Entry
            {
                Time = DateTime.Now,
                TargetSerial = targetSerial,
                TargetName = name,
                Damage = damage,
                IsIncoming = incoming
            };
            _entries.AddToBack(e);
            Added?.Invoke(e);
        }

        public static void Clear() => _entries.Clear();
    }
}

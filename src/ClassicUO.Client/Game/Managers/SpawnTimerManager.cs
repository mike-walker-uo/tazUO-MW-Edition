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
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// User-managed list of named countdowns. When a timer reaches zero a
    /// toast fires once. Useful for tracking mob respawn windows, alchemy
    /// cooldowns, raid timers — any non-server-tracked event.
    /// </summary>
    public static class SpawnTimerManager
    {
        public class Timer
        {
            public string Name;
            public DateTime ExpireAt;
            public bool Fired;
        }

        private static readonly List<Timer> _timers = new List<Timer>();
        public static IReadOnlyList<Timer> All => _timers;
        public static bool HasPending => _timers.Count != 0;

        public static void Add(string name, int seconds)
        {
            if (seconds <= 0) return;
            _timers.Add(new Timer { Name = name ?? "Timer", ExpireAt = DateTime.Now.AddSeconds(seconds) });
        }

        public static bool Remove(string name)
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_timers[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _timers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public static void Clear() => _timers.Clear();

        public static void Tick()
        {
            DateTime now = DateTime.Now;
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                var t = _timers[i];
                if (!t.Fired && now >= t.ExpireAt)
                {
                    t.Fired = true;
                    ToastManager.Show($"⏰ {t.Name}", 0x35, 6000);
                }
                // Auto-prune 30s after fire.
                if (t.Fired && (now - t.ExpireAt).TotalSeconds > 30)
                {
                    _timers.RemoveAt(i);
                }
            }
        }
    }
}

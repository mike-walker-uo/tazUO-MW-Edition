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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Detects a non-innocent/ally mob staying within `Range` tiles for at
    /// least `DwellSeconds` continuously and toasts once per offender. Re-arms
    /// after they leave range. Useful "stalker / griefer" early warning.
    /// `-loiterwarn <range> <seconds>` or `-loiterwarn off`.
    /// </summary>
    public static class HostileLoiteringWarner
    {
        public static bool Enabled;
        public static int Range = 6;
        public static int DwellSeconds = 8;
        private const long POLL_INTERVAL_MS = 1000;
        private static long _nextPoll;
        // serial -> first-time-seen-in-range
        private static readonly Dictionary<uint, long> _dwell = new Dictionary<uint, long>();
        private static readonly HashSet<uint> _alerted = new HashSet<uint>();

        public static void ResetSession()
        {
            _nextPoll = 0;
            _dwell.Clear();
            _alerted.Clear();
        }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;
            long now = (long)Time.Ticks;

            var inRange = new HashSet<uint>();
            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent || n == NotorietyFlag.Invulnerable || n == NotorietyFlag.Ally) continue;
                if (m.Distance > Range) continue;

                inRange.Add(m.Serial);
                if (!_dwell.ContainsKey(m.Serial)) _dwell[m.Serial] = now;

                if (!_alerted.Contains(m.Serial) && now - _dwell[m.Serial] >= DwellSeconds * 1000L)
                {
                    _alerted.Add(m.Serial);
                    try { UI.Gumps.ToastManager.Show($"Loitering: {m.Name ?? "?"}", 0x21, 3500); } catch { }
                }
            }

            // Drop entries that left range; clear alerted flag for them so a
            // re-entry can re-warn.
            if (_dwell.Count > 0)
            {
                var gone = new List<uint>();
                foreach (var kv in _dwell) if (!inRange.Contains(kv.Key)) gone.Add(kv.Key);
                for (int i = 0; i < gone.Count; i++) { _dwell.Remove(gone[i]); _alerted.Remove(gone[i]); }
            }
        }

        public static void Configure(int range, int seconds)
        {
            Range = System.Math.Max(1, range);
            DwellSeconds = System.Math.Max(1, seconds);
            Enabled = true;
            _dwell.Clear(); _alerted.Clear();
            GameActions.Print($"Loiter warn ON: range {Range}, dwell {DwellSeconds}s.", 0x35);
        }

        public static void Disable()
        {
            Enabled = false;
            _dwell.Clear(); _alerted.Clear();
            GameActions.Print("Loiter warn OFF.", 0x21);
        }
    }
}

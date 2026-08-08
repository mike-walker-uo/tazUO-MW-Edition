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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Aggregates skill gains from skill-update packets over the current session.
    /// Independent of journal message visibility and language.
    /// </summary>
    public static class SkillGainTracker
    {
        public class Entry
        {
            public string SkillName;
            public double TotalGain;
            public int Events;
            public DateTime LastGainTime;
        }

        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static long _sessionStartTicks = (long)Time.Ticks;

        public static event Action<Entry, double> Gained;

        public static void RecordGain(string skillName, double amount)
        {
            if (string.IsNullOrWhiteSpace(skillName) || amount <= 0) return;

            if (!_entries.TryGetValue(skillName, out var entry))
            {
                entry = new Entry { SkillName = skillName };
                _entries[skillName] = entry;
            }
            entry.TotalGain += amount;
            entry.Events++;
            entry.LastGainTime = DateTime.Now;

            Gained?.Invoke(entry, amount);
        }

        public static IEnumerable<Entry> Snapshot() => _entries.Values;
        public static int SkillCount => _entries.Count;

        public static double SessionSeconds
        {
            get
            {
                long ms = (long)Time.Ticks - _sessionStartTicks;
                if (ms < 1000) ms = 1000;
                return ms / 1000.0;
            }
        }

        public static double TotalGain
        {
            get
            {
                double total = 0;
                foreach (var e in _entries.Values) total += e.TotalGain;
                return total;
            }
        }

        public static void Reset()
        {
            _entries.Clear();
            _sessionStartTicks = (long)Time.Ticks;
        }
    }
}

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
using ClassicUO.Utility.Collections;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Rolling session log of auto-looted items. Manually populated via
    /// LootHistoryManager.Record(item, sourceName) from the auto-loot flow.
    /// </summary>
    public static class LootHistoryManager
    {
        public class Entry
        {
            public DateTime Time;
            public string ItemName;
            public ushort Graphic;
            public ushort Hue;
            public int Amount;
            public string SourceName;
        }

        public const int CAPACITY = 500;
        private static readonly Deque<Entry> _entries = new Deque<Entry>(CAPACITY);

        public static IEnumerable<Entry> All() => _entries;
        public static int Count => _entries.Count;

        public static event Action<Entry> Added;

        public static void Record(string itemName, ushort graphic, ushort hue, int amount, string sourceName)
        {
            while (_entries.Count >= CAPACITY) _entries.RemoveFromFront();
            var e = new Entry
            {
                Time = DateTime.Now,
                ItemName = itemName ?? "<unknown>",
                Graphic = graphic,
                Hue = hue,
                Amount = amount,
                SourceName = sourceName ?? string.Empty
            };
            _entries.AddToBack(e);
            Added?.Invoke(e);
        }

        public static void Clear() => _entries.Clear();

        public static void ExportCsv(string path)
        {
            using (var sw = new System.IO.StreamWriter(path))
            {
                sw.WriteLine("time,item,graphic,hue,amount,source");
                foreach (var e in _entries)
                {
                    string safeItem = (e.ItemName ?? string.Empty).Replace(",", " ");
                    string safeSrc = (e.SourceName ?? string.Empty).Replace(",", " ");
                    sw.WriteLine($"{e.Time:yyyy-MM-dd HH:mm:ss},{safeItem},0x{e.Graphic:X4},{e.Hue},{e.Amount},{safeSrc}");
                }
            }
        }
    }
}

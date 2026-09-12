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
using ClassicUO.Game.Data;
using ClassicUO.Utility.Collections;

namespace ClassicUO.Game.Managers
{
    public readonly struct ChatHistoryRecord
    {
        public readonly string Name;
        public readonly string Text;
        public readonly ushort Hue;
        public readonly DateTime Time;
        public readonly MessageType MsgType;

        public ChatHistoryRecord(string name, string text, ushort hue, DateTime time, MessageType msgType)
        {
            Name = name;
            Text = text;
            Hue = hue;
            Time = time;
            MsgType = msgType;
        }

        public string Display => string.IsNullOrEmpty(Name) ? Text : $"{Name}: {Text}";
    }

    /// <summary>
    /// Stable per-username hue map. Hash the name into a curated palette of
    /// distinct, readable hues so the same name always renders in the same color.
    /// </summary>
    public static class ChatNameHueMap
    {
        // Vivid, mostly-saturated UO hues that read against a dark background.
        // Excludes near-grey and near-black hues. Order doesn't matter; we hash
        // a username into this array.
        private static readonly ushort[] _palette =
        {
            0x0026, 0x002B, 0x002F, 0x0035, 0x0038, 0x003E,
            0x0044, 0x0048, 0x004F, 0x0053, 0x0058, 0x005B,
            0x005F, 0x0062, 0x0067, 0x006A, 0x006F, 0x0072,
            0x0099, 0x00B1, 0x00C9, 0x0166, 0x01CB, 0x021E,
            0x0234, 0x025F, 0x02B2, 0x033B, 0x03AF, 0x040C
        };

        public static ushort GetHueForName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0x0481;

            // Stable hash — string.GetHashCode varies per-process; roll our own.
            uint h = 2166136261u;
            for (int i = 0; i < name.Length; i++)
            {
                h ^= name[i];
                h *= 16777619u;
            }
            return _palette[h % (uint)_palette.Length];
        }
    }

    /// <summary>
    /// Persistent rolling-buffer store for chat lines of a particular MessageType
    /// (or any predicate). Subscribes lazily to EventSink.RawMessageReceived once.
    /// Survives gump open/close.
    /// </summary>
    public class ChatHistoryStore
    {
        public int Capacity { get; }
        private readonly Deque<ChatHistoryRecord> _records = new Deque<ChatHistoryRecord>();
        private readonly Predicate<MessageEventArgs> _filter;
        private bool _hooked;

        public event Action<ChatHistoryRecord> RecordAdded;

        public ChatHistoryStore(Predicate<MessageType> filter, int capacity = 1000)
            : this(e => filter(e.Type), capacity)
        {
        }

        public ChatHistoryStore(Predicate<MessageEventArgs> filter, int capacity = 1000)
        {
            _filter = filter;
            Capacity = capacity;
        }

        public void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnRaw;
            _hooked = true;
        }

        private void OnRaw(object sender, MessageEventArgs e)
        {
            if (e == null || !_filter(e)) return;
            string name = string.IsNullOrEmpty(e.Name) ? e.Parent?.Name : e.Name;
            var r = new ChatHistoryRecord(name, e.Text, e.Hue, DateTime.Now, e.Type);
            while (_records.Count >= Capacity) _records.RemoveFromFront();
            _records.AddToBack(r);
            RecordAdded?.Invoke(r);
        }

        public IEnumerable<ChatHistoryRecord> All() => _records;
        public int Count => _records.Count;
        public void Clear() => _records.Clear();
    }
}

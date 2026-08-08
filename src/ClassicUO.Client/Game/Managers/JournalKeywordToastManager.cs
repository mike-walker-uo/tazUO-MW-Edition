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
using System.IO;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// User-managed keyword list (`{ProfilePath}/keywords.tsv`). Any inbound
    /// journal/raw message containing a keyword (case-insensitive substring)
    /// fires a toast. Useful for "your spell fizzles", boss yells, etc.
    /// `-keyword add|del|list|on|off`.
    /// </summary>
    public static class JournalKeywordToastManager
    {
        private const string FILENAME = "keywords.tsv";
        public static bool Enabled;
        private static readonly HashSet<string> _patterns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _hooked;
        private static bool _loaded;
        private static long _lastToastAt;
        private const long MIN_GAP_MS = 1500;

        public static IReadOnlyCollection<string> Patterns => _patterns;

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "keywords.tsv");

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
                if (!string.IsNullOrWhiteSpace(line)) _patterns.Add(line.Trim());
        }

        private static void Save()
        {
            ProfileDataStore.WriteAllLines(FILENAME, _patterns);
        }

        public static void ResetForProfile()
        {
            _patterns.Clear();
            _loaded = false;
            _lastToastAt = 0;
            Enabled = false;
        }

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMsg;
            _hooked = true;
        }

        public static void Add(string p) { EnsureLoaded(); _patterns.Add(p.Trim()); Save(); }
        public static void Del(string p) { EnsureLoaded(); if (_patterns.Remove(p)) Save(); }

        private static void OnMsg(object sender, MessageEventArgs e)
        {
            if (!Enabled || e == null || string.IsNullOrEmpty(e.Text)) return;
            if (_patterns.Count == 0) return;
            foreach (var kw in _patterns)
            {
                if (e.Text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Time.Ticks - _lastToastAt < MIN_GAP_MS) return;
                _lastToastAt = (long)Time.Ticks;
                try { UI.Gumps.ToastManager.Show(kw, 0x44, 3500); } catch { }
                return;
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked(); EnsureLoaded();
            Enabled = on;
            GameActions.Print($"Keyword toast {(on ? "ON" : "OFF")} ({_patterns.Count} patterns).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

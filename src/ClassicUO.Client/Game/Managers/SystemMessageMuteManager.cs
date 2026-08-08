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
    /// User-managed substring list. Any inbound system-message / journal text
    /// containing a pattern is checked by `IsMuted` callers and can be
    /// suppressed at display time. Persistence: `{ProfilePath}/mutes.tsv`.
    /// `-mute add|del|list|on|off`.
    ///
    /// NOTE: the current implementation only maintains the list and exposes
    /// the predicate. Actual journal-suppression would require hooking into
    /// the message-rendering path — left for a focused follow-up so we don't
    /// break existing chat history side-channels here.
    /// </summary>
    public static class SystemMessageMuteManager
    {
        private const string FILENAME = "mutes.tsv";
        public static bool Enabled;
        private static readonly HashSet<string> _patterns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static IReadOnlyCollection<string> Patterns { get { EnsureLoaded(); return _patterns; } }

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "mutes.tsv");

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
            Enabled = false;
        }

        public static bool IsMuted(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text)) return false;
            EnsureLoaded();
            foreach (var p in _patterns)
                if (text.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static void Add(string p) { EnsureLoaded(); _patterns.Add(p.Trim()); Save(); }
        public static void Del(string p) { EnsureLoaded(); if (_patterns.Remove(p)) Save(); }
        public static void SetEnabled(bool on)
        {
            EnsureLoaded();
            Enabled = on;
            GameActions.Print($"Mute {(on ? "ON" : "OFF")} ({_patterns.Count} patterns; current build doesn't yet suppress display, list is callable via SystemMessageMuteManager.IsMuted).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

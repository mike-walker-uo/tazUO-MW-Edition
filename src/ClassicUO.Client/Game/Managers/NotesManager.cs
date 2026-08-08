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
using System.IO;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Persistent text notes stored as plain lines in
    /// {ProfilePath}/notes.txt. Useful for shopping lists, reminders,
    /// raid call-outs. Notes are unindexed; the user references them by line
    /// number from `-note list`.
    /// </summary>
    public static class NotesManager
    {
        private const string FILENAME = "notes.txt";
        private static readonly List<string> _notes = new List<string>();
        private static bool _loaded;

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "notes.txt");

        public static IReadOnlyList<string> All => _notes;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _notes.AddRange(ProfileDataStore.ReadAllLines(FILENAME));
        }

        public static void Save()
        {
            ProfileDataStore.WriteAllLines(FILENAME, _notes);
        }

        public static void ResetForProfile()
        {
            _notes.Clear();
            _loaded = false;
        }

        public static void Add(string text)
        {
            EnsureLoaded();
            _notes.Add(text ?? string.Empty);
            Save();
        }

        public static bool DeleteAt(int idx)
        {
            EnsureLoaded();
            if (idx < 0 || idx >= _notes.Count) return false;
            _notes.RemoveAt(idx);
            Save();
            return true;
        }
    }
}

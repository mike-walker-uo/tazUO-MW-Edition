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
using ClassicUO.Game.Data;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Persists the recent journal entries to disk so the journal isn't empty
    /// after a re-login. Snapshot of last MAX_PERSIST entries written to
    /// {ProfilePath}/journal_state.tsv on demand. Restored on first call to
    /// LoadOnce() (idempotent).
    /// </summary>
    public static class JournalPersistence
    {
        private const int MAX_PERSIST = 500;
        private const string FILENAME = "journal_state.tsv";

        private static bool _loaded;
        private static long _lastSaveTime;
        private static bool _hookedAdd;

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath) ? null :
            Path.Combine(ProfileManager.ProfilePath, FILENAME);

        public static void EnsureHookedAndLoad()
        {
            LoadOnce();
            if (_hookedAdd) return;
            EventSink.JournalEntryAdded += OnEntryAdded;
            _hookedAdd = true;
        }

        private static void OnEntryAdded(object sender, JournalEntry e)
        {
            // Coarse-grained autosave: every 30s during normal play.
            if (Time.Ticks - _lastSaveTime > 30000)
            {
                _lastSaveTime = (long)Time.Ticks;
                SaveSnapshot();
            }
        }

        public static void SaveSnapshot()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                int count = JournalManager.Entries.Count;
                int start = count > MAX_PERSIST ? count - MAX_PERSIST : 0;
                for (int i = start; i < count; i++)
                {
                    var e = JournalManager.Entries[i];
                    if (e == null) continue;
                    string name = (e.Name ?? string.Empty).Replace("\t", " ");
                    string text = (e.Text ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                    sw.Write(e.Time.ToBinary());
                    sw.Write('\t');
                    sw.Write(e.Hue);
                    sw.Write('\t');
                    sw.Write((int)e.MessageType);
                    sw.Write('\t');
                    sw.Write((int)e.TextType);
                    sw.Write('\t');
                    sw.Write(name);
                    sw.Write('\t');
                    sw.WriteLine(text);
                }
            });
        }

        public static void LoadOnce()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string[] lines = ProfileDataStore.ReadAllLines(FILENAME);
                int restored = 0;
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 6) continue;

                    if (!long.TryParse(parts[0], out long binTime)) continue;
                    if (!ushort.TryParse(parts[1], out ushort hue)) continue;
                    if (!int.TryParse(parts[2], out int mtype)) continue;
                    if (!int.TryParse(parts[3], out int ttype)) continue;
                    string name = parts[4];
                    string text = parts[5];

                    var entry = new JournalEntry
                    {
                        Text = "[old] " + text,
                        Hue = hue,
                        Name = name,
                        IsUnicode = true,
                        Font = 0,
                        Time = DateTime.FromBinary(binTime),
                        TextType = (TextType)ttype,
                        MessageType = (MessageType)mtype
                    };

                    JournalManager.Entries.AddToBack(entry);
                    EventSink.InvokeJournalEntryAdded(null, entry);
                    restored++;
                    if (JournalManager.Entries.Count > Constants.MAX_JOURNAL_HISTORY_COUNT)
                        JournalManager.Entries.RemoveFromFront();
                }
                if (restored > 0)
                    GameActions.Print($"Journal: restored {restored} previous entries.", 0x35);
            }
            catch (Exception ex)
            {
                Log.Error("JournalPersistence load: " + ex);
            }
        }

        public static void ResetForProfile()
        {
            _loaded = false;
            _lastSaveTime = 0;
        }
    }
}

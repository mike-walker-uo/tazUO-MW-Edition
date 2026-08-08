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
    /// Stable in-memory map from guild-abbreviation substrings (matched against
    /// a mobile's OPL data) to a custom name hue. Persisted to
    /// {ProfilePath}/guild_hues.tsv. NameOverhead / HealthBar gumps can consult
    /// `ResolveHue(mob, defaultHue)` to override the default notoriety hue.
    /// </summary>
    public static class GuildHueMap
    {
        private const string FILENAME = "guild_hues.tsv";
        private static readonly Dictionary<string, ushort> _map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static IReadOnlyDictionary<string, ushort> Entries => _map;

        public static string FilePath
        {
            get
            {
                if (string.IsNullOrEmpty(ProfileManager.ProfilePath)) return null;
                return Path.Combine(ProfileManager.ProfilePath, "guild_hues.tsv");
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length < 2) continue;
                if (!ushort.TryParse(parts[1], out ushort h)) continue;
                _map[parts[0]] = h;
            }
        }

        public static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                foreach (var kv in _map)
                    sw.WriteLine($"{kv.Key}\t{kv.Value}");
            });
        }

        public static void ResetForProfile()
        {
            _map.Clear();
            _loaded = false;
        }

        public static void Set(string guildSubstring, ushort hue)
        {
            EnsureLoaded();
            _map[guildSubstring] = hue;
            Save();
        }

        public static void Remove(string guildSubstring)
        {
            EnsureLoaded();
            _map.Remove(guildSubstring);
            Save();
        }

        /// <summary>
        /// Returns true and the override hue when the mobile's OPL contains
        /// any registered guild-substring. Otherwise false.
        /// </summary>
        public static bool TryResolveHue(uint serial, out ushort hue)
        {
            hue = 0;
            if (_map.Count == 0) return false;
            EnsureLoaded();
            if (!World.OPL.TryGetNameAndData(serial, out string _, out string data)) return false;
            if (string.IsNullOrEmpty(data)) return false;

            foreach (var kv in _map)
            {
                if (data.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hue = kv.Value;
                    return true;
                }
            }
            return false;
        }
    }
}

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
    /// Named serial bindings. `-pin <name>` cursor-targets an entity / item and
    /// stores its serial. `-use <name>` DoubleClicks the stored serial.
    /// `-pins` lists. Persisted to `{ProfilePath}/pins.tsv`.
    /// </summary>
    public static class PinnedSerialManager
    {
        private const string FILENAME = "pins.tsv";
        private static readonly Dictionary<string, uint> _pins =
            new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static IReadOnlyDictionary<string, uint> All { get { EnsureLoaded(); return _pins; } }

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "pins.tsv");

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                var bits = line.Split('\t');
                if (bits.Length != 2) continue;
                if (!uint.TryParse(bits[1], System.Globalization.NumberStyles.HexNumber, null, out uint ser)) continue;
                _pins[bits[0]] = ser;
            }
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                foreach (var kv in _pins) sw.WriteLine(kv.Key + "\t" + kv.Value.ToString("X"));
            });
        }

        public static void ResetForProfile()
        {
            _pins.Clear();
            _loaded = false;
        }

        public static async void Pin(string name)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(name)) { GameActions.Print("Give a name.", 0x21); return; }
            GameActions.Print($"Pin '{name}': click target...", 0x35);
            await TargetHelper.TargetAsync();
            var lt = TargetManager.LastTargetInfo;
            if (lt == null || !lt.IsEntity) { GameActions.Print("Need an entity.", 0x21); return; }
            _pins[name.Trim()] = lt.Serial;
            Save();
            GameActions.Print($"Pinned '{name}' = 0x{lt.Serial:X8}.", 0x35);
        }

        public static void Use(string name)
        {
            EnsureLoaded();
            if (!_pins.TryGetValue(name?.Trim() ?? string.Empty, out uint ser))
            {
                GameActions.Print($"No pin '{name}'.", 0x21);
                return;
            }
            GameActions.DoubleClick(ser);
        }

        public static void Remove(string name)
        {
            EnsureLoaded();
            if (_pins.Remove(name?.Trim() ?? string.Empty)) { Save(); GameActions.Print($"Pin '{name}' removed.", 0x21); }
        }
    }
}

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
    /// Per-body hue overrides. Lets the player tint specific creatures (every
    /// dragon a different palette, or every paragon a fixed colour). Stored
    /// per-profile in `bodyhues.tsv`. `-bodyhue <body-hex> <hue-hex> | del <hex>`.
    /// Returns 0 (no override) by default. Paragons get a fixed shimmer hue
    /// when no per-body override is set.
    /// </summary>
    public static class BodyHueManager
    {
        private const string FILENAME = "bodyhues.tsv";
        private static readonly Dictionary<ushort, ushort> _overrides =
            new Dictionary<ushort, ushort>();
        private static bool _loaded;

        // Vivid red — recolors the whole body. 0x0026 was too muted on dark
        // sprites (dragons read as brown). 0x0033 is the standard "bright red"
        // hue and visibly glows on every body. Override at runtime with
        // `-bodyhue paragon <hex>`.
        public static ushort ParagonAutoHue = 0x0033;

        public static IReadOnlyDictionary<ushort, ushort> All
        { get { EnsureLoaded(); return _overrides; } }

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "bodyhues.tsv");

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                var bits = line.Split('\t');
                if (bits.Length != 2) continue;
                if (!ushort.TryParse(bits[0], System.Globalization.NumberStyles.HexNumber, null, out ushort body)) continue;
                if (!ushort.TryParse(bits[1], System.Globalization.NumberStyles.HexNumber, null, out ushort hue)) continue;
                _overrides[body] = hue;
            }
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                foreach (var kv in _overrides)
                    sw.WriteLine(kv.Key.ToString("X") + "\t" + kv.Value.ToString("X"));
            });
        }

        public static void ResetForProfile()
        {
            _overrides.Clear();
            _toastedParagons.Clear();
            _loaded = false;
            Debug = false;
            ParagonAutoHue = 0x0033;
        }

        public static bool Debug;
        private static readonly HashSet<string> _toastedParagons = new HashSet<string>();

        /// <summary>Returns override hue or 0 (no override).</summary>
        public static ushort Get(ushort body, ushort currentHue, string name)
        {
            EnsureLoaded();
            if (_overrides.TryGetValue(body, out ushort h)) return h;
            bool paragon = currentHue == BodyScaleManager.PARAGON_HUE
                        || (!string.IsNullOrEmpty(name)
                            && name.IndexOf("(Paragon)", StringComparison.OrdinalIgnoreCase) >= 0);
            if (paragon && ParagonAutoHue != 0)
            {
                if (Debug && !string.IsNullOrEmpty(name) && _toastedParagons.Add(name))
                {
                    try { UI.Gumps.ToastManager.Show($"Paragon hue applied: {name} → 0x{ParagonAutoHue:X}", 0x35, 4000); } catch { }
                }
                return ParagonAutoHue;
            }
            return 0;
        }

        public static void Set(ushort body, ushort hue)
        {
            EnsureLoaded();
            _overrides[body] = hue;
            Save();
            GameActions.Print($"Body 0x{body:X} hue = 0x{hue:X}.", 0x35);
        }

        public static void Remove(ushort body)
        {
            EnsureLoaded();
            if (_overrides.Remove(body)) { Save(); GameActions.Print($"Body 0x{body:X} hue cleared.", 0x21); }
        }
    }
}

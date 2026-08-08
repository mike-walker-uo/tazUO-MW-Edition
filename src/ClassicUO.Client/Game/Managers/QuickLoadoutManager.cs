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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Save / load equipment loadouts by name. Snapshots all wearable layers
    /// (OneHanded/TwoHanded + armor + jewelry, skips Backpack/Bank/Mount/Hair).
    /// On load, walks each saved layer; if the matching serial is in pack,
    /// queues an Equip via MoveItemQueue. Persisted to `loadouts.tsv` per profile.
    /// `-loadout save <name>|load <name>|list|del <name>`.
    /// </summary>
    public static class QuickLoadoutManager
    {
        private const string FILENAME = "loadouts.tsv";
        private static readonly Dictionary<string, Dictionary<Layer, uint>> _sets =
            new Dictionary<string, Dictionary<Layer, uint>>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        // Layers worth tracking for combat loadouts.
        private static readonly Layer[] EquipLayers =
        {
            Layer.OneHanded, Layer.TwoHanded,
            Layer.Helmet, Layer.Necklace, Layer.Ring, Layer.Bracelet, Layer.Earrings,
            Layer.Arms, Layer.Gloves, Layer.Tunic, Layer.Torso, Layer.Robe, Layer.Cloak,
            Layer.Skirt, Layer.Legs, Layer.Pants, Layer.Shoes,
            Layer.Waist, Layer.Talisman,
        };

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "loadouts.tsv");

        public static IEnumerable<string> Names { get { EnsureLoaded(); return _sets.Keys; } }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length < 2) continue;
                string name = parts[0];
                var map = new Dictionary<Layer, uint>();
                for (int i = 1; i < parts.Length; i++)
                {
                    var kv = parts[i].Split('=');
                    if (kv.Length != 2) continue;
                    if (!Enum.TryParse(kv[0], out Layer lay)) continue;
                    if (!uint.TryParse(kv[1], System.Globalization.NumberStyles.HexNumber, null, out uint ser)) continue;
                    map[lay] = ser;
                }
                if (map.Count > 0) _sets[name] = map;
            }
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                foreach (var kv in _sets)
                {
                    sw.Write(kv.Key);
                    foreach (var pair in kv.Value)
                        sw.Write("\t" + pair.Key + "=" + pair.Value.ToString("X"));
                    sw.WriteLine();
                }
            });
        }

        public static void ResetForProfile()
        {
            _sets.Clear();
            _loaded = false;
        }

        public static void SaveCurrent(string name)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(name) || World.Player == null) return;
            var map = new Dictionary<Layer, uint>();
            foreach (var lay in EquipLayers)
            {
                var it = World.Player.FindItemByLayer(lay);
                if (it != null && it.Serial != 0) map[lay] = it.Serial;
            }
            if (map.Count == 0) { GameActions.Print("Nothing to save.", 0x21); return; }
            _sets[name.Trim()] = map;
            Save();
            GameActions.Print($"Loadout '{name}' saved ({map.Count} pieces).", 0x35);
        }

        public static void LoadSet(string name)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(name) || World.Player == null) return;
            if (!_sets.TryGetValue(name.Trim(), out var map))
            {
                GameActions.Print($"Loadout '{name}' not found.", 0x21);
                return;
            }
            int queued = 0;
            foreach (var pair in map)
            {
                Layer lay = pair.Key;
                uint serial = pair.Value;
                // Skip if already wearing this exact serial.
                var cur = World.Player.FindItemByLayer(lay);
                if (cur != null && cur.Serial == serial) continue;
                var it = World.Items.Get(serial);
                if (it == null || it.IsDestroyed) continue;
                MoveItemQueue.Instance.EnqueueEquipSingle(serial, lay);
                queued++;
            }
            GameActions.Print($"Loadout '{name}': queued {queued} equip(s).", 0x35);
        }

        public static void Delete(string name)
        {
            EnsureLoaded();
            if (_sets.Remove(name?.Trim() ?? string.Empty)) { Save(); GameActions.Print($"Loadout '{name}' removed.", 0x21); }
        }
    }
}

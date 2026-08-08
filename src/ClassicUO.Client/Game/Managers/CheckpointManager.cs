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
    /// Named coordinate bookmarks. `-mark <name>` saves current x,y,z;
    /// `-recall <name>` pathfinds back; `-marks list|del`. Persisted to
    /// `{ProfilePath}/checkpoints.tsv`. Cheap alternative to runebook spam.
    /// </summary>
    public static class CheckpointManager
    {
        private const string FILENAME = "checkpoints.tsv";
        private static readonly Dictionary<string, (int x, int y, sbyte z, int map)> _points =
            new Dictionary<string, (int, int, sbyte, int)>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static IEnumerable<string> Names { get { EnsureLoaded(); return _points.Keys; } }

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "checkpoints.tsv");

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                var bits = line.Split('\t');
                if (bits.Length < 5) continue;
                if (!int.TryParse(bits[1], out int x)) continue;
                if (!int.TryParse(bits[2], out int y)) continue;
                if (!sbyte.TryParse(bits[3], out sbyte z)) continue;
                if (!int.TryParse(bits[4], out int map)) continue;
                _points[bits[0]] = (x, y, z, map);
            }
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                foreach (var kv in _points)
                    sw.WriteLine($"{kv.Key}\t{kv.Value.x}\t{kv.Value.y}\t{kv.Value.z}\t{kv.Value.map}");
            });
        }

        public static void ResetForProfile()
        {
            _points.Clear();
            _loaded = false;
        }

        public static void Mark(string name)
        {
            EnsureLoaded();
            if (World.Player == null) return;
            if (string.IsNullOrWhiteSpace(name)) { GameActions.Print("Give a name.", 0x21); return; }
            _points[name.Trim()] = (World.Player.X, World.Player.Y, World.Player.Z, World.MapIndex);
            Save();
            GameActions.Print($"Marked '{name}' = {World.Player.X},{World.Player.Y} (map {World.MapIndex}).", 0x35);
        }

        public static void Recall(string name)
        {
            EnsureLoaded();
            if (!_points.TryGetValue(name?.Trim() ?? string.Empty, out var pt))
            {
                GameActions.Print($"Mark '{name}' not found.", 0x21);
                return;
            }
            if (pt.map != World.MapIndex)
            {
                GameActions.Print($"Mark is on map {pt.map}; you're on {World.MapIndex}.", 0x21);
                return;
            }
            Pathfinder.WalkTo(pt.x, pt.y, pt.z, 0);
            GameActions.Print($"Walking to '{name}'.", 0x35);
        }

        public static void Delete(string name)
        {
            EnsureLoaded();
            if (_points.Remove(name?.Trim() ?? string.Empty)) { Save(); GameActions.Print($"Mark '{name}' removed.", 0x21); }
        }
    }
}

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
    /// Per-body sprite-scale overrides applied by MobileView.DrawInternal.
    /// Lets the player make greater-dragons / ancient-wyrms larger than the
    /// stock animation. Persisted to `{ProfilePath}/bodyscales.tsv`.
    /// Default seed: 0x003B (Greater Dragon) = 1.3. Edit via `-bodyscale`.
    /// </summary>
    public static class BodyScaleManager
    {
        private const string FILENAME = "bodyscales.tsv";
        private static readonly Dictionary<ushort, float> _scales = new Dictionary<ushort, float>();
        private static bool _loaded;

        public static bool Enabled { get; private set; } = true;

        // Built-in explicit overrides. User overrides via `-bodyscale` still win.
        // Demon family pinned because named-boss demons aren't in MobHpTable,
        // so HP-tier lookup misses them.
        private static readonly Dictionary<ushort, float> _defaults = new Dictionary<ushort, float>
        {
            { 0x003E, 1.5f },  // Wyvern
            { 0x0009, 1.5f },  //Daemon / Fire Daemon / Minion of Scelestus
            { 0x0028, 1.5f },  //Arch Daemon / Balron
            { 0x0132, 1.5f },  //Impaler / The Butcher
            { 0x0134, 1.5f },  //Bone Demon
            { 0x002B, 1.5f },  //Ice Fiend / Pit Fiend
            { 0x013E, 1.5f },  //Demon Knight
        };

        // Bodies excluded from automatic HP-tier scaling. Empty — all bodies
        // (dragons included) scale via HP / name lookup.
        private static readonly HashSet<ushort> _noAutoScale = new HashSet<ushort>();

        // Mobile.HitsMax over the wire is normalized to a small range for non-
        // player mobs (0..25-ish), so we can't tier on it. Real HP comes from
        // the OPL tooltip ("Hits: 599/599"). We cache per-serial and per-body.
        private static readonly Dictionary<uint, int> _serialHp = new Dictionary<uint, int>();
        private static readonly Dictionary<ushort, int> _bodyMaxHp = new Dictionary<ushort, int>();
        private static bool _oplHooked;

        public static void EnsureOplHooked()
        {
            if (_oplHooked) return;
            EventSink.OPLOnReceive += OnOPL;
            _oplHooked = true;
        }

        private static void OnOPL(object sender, OPLEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.Data)) return;
            int hp = ParseHpMax(e.Data);
            if (hp <= 0) return;
            _serialHp[e.Serial] = hp;
            // Map body for the mobile if we can find it.
            var mob = World.Mobiles.Get(e.Serial);
            if (mob != null)
            {
                ushort body = mob.Graphic;
                if (!_bodyMaxHp.TryGetValue(body, out int prev) || prev < hp)
                    _bodyMaxHp[body] = hp;
            }
        }

        // Parses "Hits: 599 / 599" or "Hits: 599/599" from OPL data lines.
        private static int ParseHpMax(string data)
        {
            int i = data.IndexOf("Hits:", System.StringComparison.OrdinalIgnoreCase);
            if (i < 0) i = data.IndexOf("HP:", System.StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0;
            int slash = data.IndexOf('/', i);
            if (slash < 0) return 0;
            int j = slash + 1;
            while (j < data.Length && !char.IsDigit(data[j])) j++;
            int k = j;
            while (k < data.Length && char.IsDigit(data[k])) k++;
            if (k == j) return 0;
            int.TryParse(data.Substring(j, k - j), out int n);
            return n;
        }

        public static int RecallHpBySerial(uint serial)
            => _serialHp.TryGetValue(serial, out int hp) ? hp : 0;

        public static int RecallHpByBody(ushort body)
            => _bodyMaxHp.TryGetValue(body, out int hp) ? hp : 0;

        /// <summary>Global hard cap on any final scale (HP-tier × paragon × pet).</summary>
        public const float MAX_SCALE = 2.0f;

        /// <summary>HP-tier scale used when no explicit override exists.</summary>
        public static float ScaleFromHp(int hpMax)
        {
            // Rescaled so the cap of 2.0× lines up with the strongest mobs,
            // with smooth gradation under that ceiling.
            if (hpMax > 25000) return 1.90f;
            if (hpMax > 10000) return 1.70f;
            if (hpMax >  1000) return 1.50f;
            if (hpMax >   500) return 1.30f;
            if (hpMax >   250) return 1.15f;
            return 1f;
        }

        public static IReadOnlyDictionary<ushort, float> All { get { EnsureLoaded(); return _scales; } }

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "bodyscales.tsv");

        // Standard OSI paragon hue. Detect via mob.Hue or "(Paragon)" name suffix.
        public const ushort PARAGON_HUE = 0x0501;
        public const float PARAGON_SCALE = 1.15f;
        // Extra factor applied to the player's own pets / mounts.
        public const float PET_SCALE = 1.15f;

        /// <summary>Body-only lookup (user override → explicit default → 1.0).</summary>
        public static float Get(ushort body)
        {
            if (!_loaded) EnsureLoaded();
            if (!Enabled) return 1f;
            if (_scales.TryGetValue(body, out float s)) return s;
            if (_defaults.TryGetValue(body, out float d)) return d;
            return 1f;
        }

        /// <summary>
        /// Full lookup factoring an explicit override, automatic HP-tier scale
        /// (skipped for excluded bodies like dragons), and a paragon multiplier.
        /// </summary>
        public static float Get(ushort body, ushort hue, string name, int hpMax)
        {
            if (!_loaded) EnsureLoaded();
            if (!Enabled) return 1f;
            float s;
            if (_scales.TryGetValue(body, out s) || _defaults.TryGetValue(body, out s))
            {
                // explicit override wins; skip HP-tier
            }
            else if (_noAutoScale.Contains(body))
            {
                s = 1f;
            }
            else
            {
                // Priority for HP source:
                //   1. caller-supplied hpMax (OPL-derived true value).
                //   2. hardcoded MobHpTable lookup by name.
                //   3. OPL-populated body cache.
                int effectiveHp = hpMax > 0 ? hpMax : MobHpTable.Get(name);
                if (effectiveHp <= 0) effectiveHp = RecallHpByBody(body);
                s = ScaleFromHp(effectiveHp);
            }

            bool paragon = hue == PARAGON_HUE
                        || (!string.IsNullOrEmpty(name)
                            && name.IndexOf("(Paragon)", System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (paragon) s *= PARAGON_SCALE;
            if (s > MAX_SCALE) s = MAX_SCALE;
            return s;
        }

        // Convenience overloads.
        public static float Get(ushort body, ushort hue, string name)
            => Get(body, hue, name, 0);

        public static float Get(ushort body, ClassicUO.Game.GameObjects.Mobile owner)
            => Get(body, owner, isMount: false);

        /// <summary>
        /// Mobile-aware lookup. When `isMount` is true and the rider is the
        /// player, the draw is the player's mount sprite — pets get the
        /// PET_SCALE bump. Standalone-mob path applies the same bump when the
        /// mob looks like a tame (IsRenamable + non-Enemy/Invuln notoriety).
        /// </summary>
        public static float Get(ushort body, ClassicUO.Game.GameObjects.Mobile owner, bool isMount)
        {
            if (!_loaded) EnsureLoaded();
            if (!Enabled) return 1f;
            if (owner == null) return Get(body);
            int trueHp = RecallHpBySerial(owner.Serial);
            float s = Get(body, owner.Hue, owner.Name, trueHp);

            bool isPet;
            if (isMount)
            {
                // Player's mount sprite (rider is player).
                isPet = World.Player != null && owner.Serial == World.Player.Serial;
            }
            else
            {
                isPet = owner.IsRenamable
                     && owner.NotorietyFlag != Data.NotorietyFlag.Enemy
                     && owner.NotorietyFlag != Data.NotorietyFlag.Invulnerable
                     && owner != World.Player;
            }
            if (isPet) s *= PET_SCALE;
            if (s > MAX_SCALE) s = MAX_SCALE;
            return s;
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            foreach (var line in ProfileDataStore.ReadAllLines(FILENAME))
            {
                var bits = line.Split('\t');
                if (bits.Length != 2) continue;
                if (bits[0].Equals("enabled", System.StringComparison.OrdinalIgnoreCase))
                {
                    Enabled = bits[1] != "0";
                    continue;
                }
                if (!ushort.TryParse(bits[0], System.Globalization.NumberStyles.HexNumber, null, out ushort body)) continue;
                if (!float.TryParse(bits[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale)) continue;
                _scales[body] = scale;
            }
        }

        private static void Save()
        {
            ProfileDataStore.Write(FILENAME, sw =>
            {
                sw.WriteLine($"enabled\t{(Enabled ? 1 : 0)}");
                foreach (var kv in _scales)
                    sw.WriteLine($"{kv.Key:X}\t{kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            });
        }

        public static void ResetForProfile()
        {
            _scales.Clear();
            _serialHp.Clear();
            _bodyMaxHp.Clear();
            Enabled = true;
            _loaded = false;
        }

        public static void SetEnabled(bool enabled)
        {
            EnsureLoaded();
            Enabled = enabled;
            Save();
            GameActions.Print($"Body scaling {(enabled ? "ON" : "OFF")}.", (ushort)(enabled ? 0x35 : 0x21));
        }

        public static void Set(ushort body, float scale)
        {
            EnsureLoaded();
            if (scale <= 0.1f || scale > MAX_SCALE) { GameActions.Print($"Scale must be 0.1..{MAX_SCALE:0.0}.", 0x21); return; }
            _scales[body] = scale;
            Save();
            GameActions.Print($"Body 0x{body:X} scale = {scale}.", 0x35);
        }

        public static void Remove(ushort body)
        {
            EnsureLoaded();
            if (_scales.Remove(body)) { Save(); GameActions.Print($"Body 0x{body:X} reset.", 0x21); }
        }
    }
}

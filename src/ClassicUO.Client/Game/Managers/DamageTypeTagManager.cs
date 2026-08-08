#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Inspects raw chat lines for damage-type adjectives ("burning",
    /// "freezing", etc) and remembers the last type a mob dealt against you.
    /// Other gumps query <see cref="GetLastTypeFor"/> for display.
    /// `-dmgtype on|off`.
    /// </summary>
    public static class DamageTypeTagManager
    {
        public static bool Enabled = true;

        public enum DmgType { Unknown, Physical, Fire, Cold, Poison, Energy }

        // Keyword → type. Matches common UO English damage flavor lines.
        private static readonly (string kw, DmgType t)[] _kw =
        {
            ("burning", DmgType.Fire), ("flame", DmgType.Fire), ("fire", DmgType.Fire),
            ("freezing", DmgType.Cold), ("frost", DmgType.Cold), ("cold", DmgType.Cold),
            ("poison", DmgType.Poison), ("venom", DmgType.Poison),
            ("shock", DmgType.Energy), ("energy", DmgType.Energy), ("lightning", DmgType.Energy),
            ("physical", DmgType.Physical), ("crushing", DmgType.Physical),
        };

        private static readonly Dictionary<uint, DmgType> _lastBySource = new Dictionary<uint, DmgType>();
        private static bool _hooked;
        internal static int HookRegistrationCount { get; private set; }

        public static void Hook()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMsg;
            _hooked = true;
            HookRegistrationCount++;
        }

        public static void ResetSession() => _lastBySource.Clear();

        private static void OnMsg(object sender, MessageEventArgs e)
        {
            if (!Enabled) return;
            if (string.IsNullOrEmpty(e.Text)) return;
            string s = e.Text.ToLowerInvariant();
            DmgType t = DmgType.Unknown;
            for (int i = 0; i < _kw.Length; i++)
                if (s.IndexOf(_kw[i].kw, StringComparison.Ordinal) >= 0) { t = _kw[i].t; break; }
            if (t == DmgType.Unknown) return;
            // Without a structured source, attribute to the last aggressor we have.
            uint src = LastAggressor();
            if (src != 0) _lastBySource[src] = t;
        }

        private static uint LastAggressor()
        {
            long newest = 0; uint who = 0;
            foreach (var kv in AggroIndicatorManager.GetAll())
                if (kv.Value > newest) { newest = kv.Value; who = kv.Key; }
            return who;
        }

        public static DmgType GetLastTypeFor(uint serial)
            => _lastBySource.TryGetValue(serial, out var t) ? t : DmgType.Unknown;

        public static string Short(DmgType t)
        {
            switch (t)
            {
                case DmgType.Fire: return "F";
                case DmgType.Cold: return "C";
                case DmgType.Poison: return "P";
                case DmgType.Energy: return "E";
                case DmgType.Physical: return "Ph";
                default: return "";
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on) _lastBySource.Clear();
            GameActions.Print($"Damage-type tag {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

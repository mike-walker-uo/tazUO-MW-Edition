#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    public static class PetGuardTintManager
    {
        public static bool Enabled;
        private const long REFRESH_MS = 15_000;
        private static readonly Dictionary<uint, bool> _guarding = new Dictionary<uint, bool>();
        private static bool _hooked;
        private static long _nextRefresh;

        public static void ResetSession()
        {
            _guarding.Clear();
            _nextRefresh = 0;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (on) EnsureHooked();
            else ResetSession();
            GameActions.Print($"Pet guard tint {(on ? "ON" : "OFF")} (gray when not guarding).", (ushort)(on ? 0x35 : 0x21));
        }

        public static void Tick()
        {
            if (!Enabled || !World.InGame || World.Player == null || !World.ClientFeatures.TooltipsEnabled)
                return;

            EnsureHooked();
            if (Time.Ticks < _nextRefresh) return;
            _nextRefresh = (long)Time.Ticks + REFRESH_MS;

            foreach (Mobile pet in World.Mobiles.Values)
            {
                if (!IsPet(pet) || pet.Distance > World.ClientViewRange) continue;

                if (!_guarding.ContainsKey(pet.Serial)
                    && World.OPL.TryGetNameAndData(pet.Serial, out _, out string data))
                    Update(pet.Serial, data);

                PacketHandlers.AddMegaClilocRequest(pet.Serial);
            }
        }

        public static bool ShouldTint(Mobile pet) =>
            Enabled && IsPet(pet) && _guarding.TryGetValue(pet.Serial, out bool guarding) && !guarding;

        private static bool IsPet(Mobile pet) =>
            pet != null && !pet.IsDestroyed && !pet.IsDead && pet != World.Player && pet.IsRenamable
            && pet.NotorietyFlag != NotorietyFlag.Enemy
            && pet.NotorietyFlag != NotorietyFlag.Invulnerable;

        private static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OPLOnReceive += OnProperties;
            _hooked = true;
        }

        private static void OnProperties(object sender, OPLEventArgs e)
        {
            if (!Enabled || e == null || !IsPet(World.Mobiles.Get(e.Serial))) return;
            Update(e.Serial, e.Data);
        }

        private static void Update(uint serial, string data)
        {
            if (data == null)
            {
                _guarding.Remove(serial);
                return;
            }
            string plain = Regex.Replace(data, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            plain = WebUtility.HtmlDecode(Regex.Replace(plain, @"<[^>]*>", string.Empty));
            foreach (string line in plain.Split('\n'))
            {
                if (string.Equals(line.Trim(), "Guarding", StringComparison.OrdinalIgnoreCase))
                {
                    _guarding[serial] = true;
                    return;
                }
            }
            _guarding[serial] = false;
        }
    }
}

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
using System.Net;
using System.Text.RegularExpressions;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Watches numeric loyalty in pet tooltips and warns when feeding is due.
    /// Also keeps the existing server-message alerts for shards without numeric loyalty.
    /// </summary>
    public static class PetLoyaltyAlertManager
    {
        public static bool Enabled;
        private static bool _hooked;
        private static long _lastToastAt;
        private static long _nextTooltipRefresh;
        private const int FEED_BELOW = 90;
        private const long TOOLTIP_REFRESH_MS = 60_000;
        private const long REMINDER_GAP_MS = 10 * 60_000;
        private static readonly Dictionary<uint, int> _loyalty = new Dictionary<uint, int>();
        private static readonly Dictionary<uint, long> _lastReminderAt = new Dictionary<uint, long>();
        private static readonly Regex LoyaltyLine = new Regex(@"^\s*Loyalty(?:\s+Rating)?\s*[:：]?\s*(\d{1,3})(?:\.\d+)?\s*(?:%|/\s*100)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public static void ResetSession()
        {
            _lastToastAt = 0;
            _nextTooltipRefresh = 0;
            _loyalty.Clear();
            _lastReminderAt.Clear();
        }
        private const long MIN_GAP_MS = 5000;

        private static readonly string[] Phrases =
        {
            "looks unhappy",        // initial drop
            "looks extremely unhappy",
            "looks like it may wander",
            "decides it would rather wander",
            "rather be on its own",
            "wandered off",
        };

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMessage;
            EventSink.OPLOnReceive += OnProperties;
            _hooked = true;
        }

        public static void Tick()
        {
            if (!Enabled || !World.InGame || World.Player == null || !World.ClientFeatures.TooltipsEnabled)
                return;

            EnsureHooked();
            if (Time.Ticks < _nextTooltipRefresh)
                return;
            _nextTooltipRefresh = (long)Time.Ticks + TOOLTIP_REFRESH_MS;

            foreach (Mobile pet in World.Mobiles.Values)
            {
                if (!IsNearbyMobile(pet))
                    continue;

                if (World.OPL.TryGetNameAndData(pet.Serial, out _, out string data))
                    UpdateLoyalty(pet, data);

                if (pet.IsRenamable || _loyalty.ContainsKey(pet.Serial))
                    PacketHandlers.AddMegaClilocRequest(pet.Serial);
            }
        }

        private static bool IsNearbyMobile(Mobile pet) =>
            pet != null && !pet.IsDestroyed && !pet.IsDead && pet != World.Player &&
            pet.Distance <= World.ClientViewRange &&
            pet.NotorietyFlag != NotorietyFlag.Enemy &&
            pet.NotorietyFlag != NotorietyFlag.Invulnerable;

        private static void OnProperties(object sender, OPLEventArgs e)
        {
            if (!Enabled || !World.InGame || World.Player == null || e == null)
                return;

            Mobile pet = World.Mobiles.Get(e.Serial);
            if (IsNearbyMobile(pet))
                UpdateLoyalty(pet, e.Data);
        }

        private static void UpdateLoyalty(Mobile pet, string data)
        {
            if (!TryParseLoyalty(data, out int value))
            {
                _loyalty.Remove(pet.Serial);
                _lastReminderAt.Remove(pet.Serial);
                return;
            }

            _loyalty[pet.Serial] = value;
            if (value >= FEED_BELOW)
            {
                _lastReminderAt.Remove(pet.Serial);
                return;
            }

            long now = (long)Time.Ticks;
            if (_lastReminderAt.TryGetValue(pet.Serial, out long last) && now - last < REMINDER_GAP_MS)
                return;

            _lastReminderAt[pet.Serial] = now;
            string name = string.IsNullOrWhiteSpace(pet.Name) ? "Your pet" : pet.Name;
            UI.Gumps.ToastManager.Show($"Feed {name}: loyalty {value}%", 0x21, 6000);
            Client.Game?.Audio?.PlaySound(0x0055);
        }

        internal static bool TryParseLoyalty(string data, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(data)) return false;

            string plain = Regex.Replace(data, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            plain = WebUtility.HtmlDecode(Regex.Replace(plain, @"<[^>]*>", string.Empty));
            Match match = LoyaltyLine.Match(plain);
            return match.Success && int.TryParse(match.Groups[1].Value, out value) && value <= 100;
        }

        public static void PrintStatus()
        {
            int count = 0;
            foreach (Mobile pet in World.Mobiles.Values)
            {
                if (!IsNearbyMobile(pet))
                    continue;

                if (!_loyalty.TryGetValue(pet.Serial, out int value))
                {
                    if (!World.OPL.TryGetNameAndData(pet.Serial, out _, out string data)
                        || !TryParseLoyalty(data, out value))
                        continue;
                    _loyalty[pet.Serial] = value;
                }

                GameActions.Print($"{pet.Name}: last observed loyalty {value}%" + (value < FEED_BELOW ? " — feed now" : string.Empty),
                    (ushort)(value < FEED_BELOW ? 0x21 : 0x35));
                count++;
            }

            if (count == 0)
                GameActions.Print("No nearby pet loyalty values received yet.", 0x35);
        }

        private static void OnMessage(object sender, MessageEventArgs e)
        {
            if (!Enabled) return;
            if (string.IsNullOrEmpty(e?.Text)) return;
            string t = e.Text;
            for (int i = 0; i < Phrases.Length; i++)
            {
                if (t.IndexOf(Phrases[i], StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Time.Ticks - _lastToastAt < MIN_GAP_MS) return;
                _lastToastAt = (long)Time.Ticks;
                try { UI.Gumps.ToastManager.Show("Pet loyalty: " + Phrases[i], 0x21, 5000); } catch { }
                try { Client.Game?.Audio?.PlaySound(0x0055); } catch { }
                return;
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            if (!on) ResetSession();
            else _nextTooltipRefresh = 0;
            GameActions.Print($"Pet loyalty reminder {(on ? "ON" : "OFF")} (feed below {FEED_BELOW}%).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

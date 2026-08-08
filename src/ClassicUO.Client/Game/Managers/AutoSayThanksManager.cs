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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// When journal mentions the player being healed/resurrected, auto-says
    /// "thanks" once per cooldown window. Off by default — opt-in via
    /// `-autothanks on|off`.
    /// </summary>
    public static class AutoSayThanksManager
    {
        public static bool Enabled;
        public static string Phrase = "thanks";
        private const long MIN_GAP_MS = 30000; // 30s
        private static bool _hooked;
        private static long _lastSayAt;

        public static void ResetSession() => _lastSayAt = 0;

        private static readonly string[] Triggers =
        {
            "has resurrected you",
            "you feel refreshed",
            "you have been healed",
            "casts a beneficial",
        };

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMsg;
            _hooked = true;
        }

        private static void OnMsg(object sender, MessageEventArgs e)
        {
            if (!Enabled || string.IsNullOrEmpty(e?.Text)) return;
            string t = e.Text;
            for (int i = 0; i < Triggers.Length; i++)
            {
                if (t.IndexOf(Triggers[i], StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (Time.Ticks - _lastSayAt < MIN_GAP_MS) return;
                if (!AutomationCoordinator.TryAcquire("AutoSayThanks", 250)) return;
                _lastSayAt = (long)Time.Ticks;
                GameActions.Say(Phrase);
                return;
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Auto-thanks {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

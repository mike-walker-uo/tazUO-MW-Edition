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
using ClassicUO.Configuration;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// When enabled, replies once per N seconds to incoming regular-speech /
    /// guild / alliance messages from other players with a customizable
    /// "AFK" message. Per-sender cooldown prevents spam.
    /// </summary>
    public static class AfkReplyManager
    {
        public static bool Enabled;
        public static string Message = "AFK — back soon.";
        private const long PER_SENDER_COOLDOWN_MS = 60_000;
        private static bool _hooked;
        private static readonly Dictionary<string, long> _lastReplyTo = new Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase);

        public static void ResetSession() => _lastReplyTo.Clear();

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMessage;
            _hooked = true;
        }

        private static void OnMessage(object sender, MessageEventArgs e)
        {
            if (!Enabled) return;
            if (e == null || string.IsNullOrEmpty(e.Text) || string.IsNullOrEmpty(e.Name)) return;
            // Only react to player-spoken types.
            if (e.Type != MessageType.Regular && e.Type != MessageType.Whisper && e.Type != MessageType.Yell)
                return;
            // Don't reply to self.
            if (World.Player != null && string.Equals(e.Name, World.Player.Name, System.StringComparison.OrdinalIgnoreCase))
                return;

            string who = e.Name;
            if (_lastReplyTo.TryGetValue(who, out long last) && Time.Ticks - last < PER_SENDER_COOLDOWN_MS)
                return;
            if (!AutomationCoordinator.TryAcquire("AfkReply", 250)) return;
            _lastReplyTo[who] = (long)Time.Ticks;

            // Send as regular speech.
            GameActions.Say(Message, ProfileManager.CurrentProfile?.SpeechHue ?? (ushort)0x35);
        }

        public static void SetEnabled(bool on, string msg = null)
        {
            EnsureHooked();
            Enabled = on;
            if (!string.IsNullOrEmpty(msg)) Message = msg;
            GameActions.Print($"AFK auto-reply {(on ? "ON" : "OFF")}: \"{Message}\"",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

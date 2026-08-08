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
    /// Watches RawMessage for hunger / thirst clilocs ("hungry", "thirsty",
    /// "starving") and toasts. Cheap, one regex-free string check.
    /// `-hungeralert on|off`.
    /// </summary>
    public static class HungerThirstAlertManager
    {
        public static bool Enabled;
        private static bool _hooked;
        private static long _lastToastAt;

        public static void ResetSession() => _lastToastAt = 0;
        private const long MIN_GAP_MS = 30000; // 30s

        private static readonly string[] Phrases =
        {
            "you are hungry",
            "you are thirsty",
            "you are starving",
            "you are dying of thirst",
        };

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMessage;
            _hooked = true;
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
                try { UI.Gumps.ToastManager.Show(Phrases[i], 0x53, 3500); } catch { }
                return;
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Hunger/thirst alert {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

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
    /// Hooks RawMessageReceived for "apply the bandages" → starts a ~11s timer.
    /// Toasts when the bandage finishes (heal/cure landed) so the player knows
    /// the next bandage is ready. `-bandagetimer on|off|<seconds>`.
    /// </summary>
    public static class BandageTimerManager
    {
        public static bool Enabled;
        public static int Duration = 11; // seconds; tunable per shard
        private static bool _hooked;
        private static long _readyAt;
        private static bool _toastedReady;

        public static bool InProgress => _readyAt > Time.Ticks;

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
            if (t.IndexOf("apply the bandage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _readyAt = (long)Time.Ticks + Duration * 1000L;
                _toastedReady = false;
            }
            // Some shards announce "You finish applying"; treat as ready cue too.
            else if (t.IndexOf("finish applying", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ReadyNow();
            }
        }

        public static void Tick()
        {
            if (!Enabled || _toastedReady || _readyAt == 0) return;
            if (Time.Ticks < _readyAt) return;
            ReadyNow();
        }

        private static void ReadyNow()
        {
            _toastedReady = true;
            _readyAt = 0;
            try { UI.Gumps.ToastManager.Show("Bandage ready", 0x44, 2500, "bandage-ready",
                AlertCategory.Supplies, AlertSeverity.Info); } catch { }
            try { Client.Game?.Audio?.PlaySound(0x0055); } catch { }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Bandage timer {(on ? "ON" : "OFF")} ({Duration}s).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

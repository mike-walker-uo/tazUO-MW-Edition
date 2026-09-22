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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Polls Player.IsPoisoned; on rising edge toasts + plays a short alert.
    /// Cheap status-flag check, no message-parsing fragility.
    /// `-poisonalert on|off`.
    /// </summary>
    public static class PoisonAlertManager
    {
        public static bool Enabled;
        private static bool _wasPoisoned;
        private const long POLL_INTERVAL_MS = 750;
        private static long _nextPoll;

        public static void ResetSession() { _wasPoisoned = false; _nextPoll = 0; }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            bool p = World.Player.IsPoisoned;
            if (p && !_wasPoisoned)
            {
                try { UI.Gumps.ToastManager.Show("POISONED!", 0x44, 4000, "poisoned",
                    AlertCategory.Combat, AlertSeverity.Warning); } catch { }
                try { Client.Game?.Audio?.PlaySound(0x0064); } catch { }
            }
            _wasPoisoned = p;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Poison alert {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

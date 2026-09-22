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
    /// Toast on the rising edge of HP returning to max — "Full HP". Useful
    /// when bandaging in a corner waiting to fight again. Re-arms whenever
    /// HP drops below max again. `-fullhptoast on|off`.
    /// </summary>
    public static class FullHpToastManager
    {
        public static bool Enabled;
        private const long POLL_INTERVAL_MS = 750;
        private static long _nextPoll;
        private static bool _wasFull;

        public static void ResetSession() { _nextPoll = 0; _wasFull = false; }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame || World.Player.IsDead) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            int max = World.Player.HitsMax;
            if (max <= 0) return;
            bool full = World.Player.Hits >= max;
            if (full && !_wasFull)
            {
                try { UI.Gumps.ToastManager.Show("Full HP", 0x44, 2000, "full-health",
                    AlertCategory.Combat, AlertSeverity.Info); } catch { }
            }
            _wasFull = full;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            _wasFull = false;
            GameActions.Print($"Full-HP toast {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

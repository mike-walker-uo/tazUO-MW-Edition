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

using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Polls player NotorietyFlag for changes; toasts on transition. Catches
    /// "now criminal" / "now murderer" status events even when the journal
    /// message is missed. `-notorietywatch on|off`.
    /// </summary>
    public static class NotorietyChangeWatcher
    {
        public static bool Enabled;
        private const long POLL_INTERVAL_MS = 800;
        private static long _nextPoll;
        private static NotorietyFlag _last = NotorietyFlag.Unknown;
        private static bool _seeded;

        public static void ResetSession() { _nextPoll = 0; _last = NotorietyFlag.Unknown; _seeded = false; }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            var cur = World.Player.NotorietyFlag;
            if (!_seeded) { _last = cur; _seeded = true; return; }
            if (cur == _last) return;
            ushort hue = HueFor(cur);
            try { UI.Gumps.ToastManager.Show($"Notoriety → {cur}", hue, 3000); } catch { }
            _last = cur;
        }

        private static ushort HueFor(NotorietyFlag n)
        {
            switch (n)
            {
                case NotorietyFlag.Innocent: return 0x44;
                case NotorietyFlag.Ally: return 0x53;
                case NotorietyFlag.Gray:
                case NotorietyFlag.Criminal: return 0x53;
                case NotorietyFlag.Murderer: return 0x21;
                case NotorietyFlag.Enemy: return 0x21;
            }
            return 0x35;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            _seeded = false;
            GameActions.Print($"Notoriety watch {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

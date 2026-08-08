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
    /// Derives "in combat" from CombatLogManager.Added (any damage in/out
    /// within OutOfCombatMs). Toasts on entering / leaving combat. Lets the
    /// player know it's safe to mount, hide, or use combat-restricted items.
    /// `-combatstate on|off`.
    /// </summary>
    public static class CombatStateManager
    {
        public static bool Enabled;
        public static int OutOfCombatMs = 6000;
        private static long _lastEventAt;
        private static bool _inCombat;
        private static bool _hooked;

        public static bool InCombat => _inCombat;

        public static void ResetSession() { _lastEventAt = 0; _inCombat = false; }

        public static void EnsureHooked()
        {
            if (_hooked) return;
            CombatLogManager.Added += e => { if (Enabled) _lastEventAt = (long)Time.Ticks; };
            _hooked = true;
        }

        public static void Tick()
        {
            if (!Enabled) return;
            bool nowIn = (long)Time.Ticks - _lastEventAt < OutOfCombatMs && _lastEventAt > 0;
            if (nowIn && !_inCombat)
            {
                _inCombat = true;
                try { UI.Gumps.ToastManager.Show("In combat", 0x21, 1500); } catch { }
            }
            else if (!nowIn && _inCombat)
            {
                _inCombat = false;
                try { UI.Gumps.ToastManager.Show("Out of combat", 0x44, 2000); } catch { }
            }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Combat-state alerts {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

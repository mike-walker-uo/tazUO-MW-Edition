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
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Polls Player.BuffIcons for a configured buff. When it disappears,
    /// re-casts a configured spell by name. Recast throttled so we don't
    /// spam fizzles. Watch one buff/spell pair at a time (KISS).
    /// `-autobuff <buffname> <spellname>` to arm, `-autobuff off` to clear.
    /// </summary>
    public static class AutoBuffManager
    {
        public static bool Enabled;
        public static BuffIconType Watch = (BuffIconType)(-1); // none
        public static string SpellName = string.Empty;
        public static string WatchLabel = string.Empty;

        private const long POLL_INTERVAL_MS = 500;
        private const long RECAST_COOLDOWN_MS = 4500;
        private static long _nextPoll;
        private static long _lastCastAt;
        private static bool _wasPresent;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Watch == (BuffIconType)(-1) || string.IsNullOrEmpty(SpellName)) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            bool present = World.Player.IsBuffIconExists(Watch);
            if (present)
            {
                _wasPresent = true;
                return;
            }

            if (!_wasPresent) return; // never seen up; don't pre-cast
            if (Time.Ticks - _lastCastAt < RECAST_COOLDOWN_MS) return;
            if (!AutomationCoordinator.TryAcquire("AutoBuff", 750)) return;
            _lastCastAt = (long)Time.Ticks;
            GameActions.CastSpellByName(SpellName);
        }

        public static void Arm(string buffName, string spellName)
        {
            if (!TryParseBuff(buffName, out var b))
            {
                GameActions.Print($"Unknown buff '{buffName}'.", 0x21);
                return;
            }
            Watch = b;
            WatchLabel = buffName;
            SpellName = spellName;
            Enabled = true;
            _wasPresent = false;
            GameActions.Print($"AutoBuff watching {buffName} -> {spellName}.", 0x35);
        }

        public static void Disable()
        {
            Enabled = false;
            GameActions.Print("AutoBuff OFF.", 0x21);
        }

        private static bool TryParseBuff(string name, out BuffIconType buff)
        {
            buff = default;
            if (string.IsNullOrWhiteSpace(name)) return false;
            return Enum.TryParse(name.Replace(" ", string.Empty), true, out buff);
        }
    }
}

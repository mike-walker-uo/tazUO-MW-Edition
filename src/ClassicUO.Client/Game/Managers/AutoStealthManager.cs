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
    /// Periodically attempts to hide / stealth when the player is idle (no
    /// recent damage or movement). Skill IDs are OSI defaults: Hiding=21,
    /// Stealth=47. Off by default. `-autostealth on|off`.
    /// </summary>
    public static class AutoStealthManager
    {
        public const int HIDING_SKILL = 21;
        public const int STEALTH_SKILL = 47;
        private const long IDLE_THRESHOLD_MS = 6000;
        private const long MIN_INTERVAL_MS = 12000;

        public static bool Enabled;
        private static long _lastUsedAt;
        private static long _lastMoveAt;
        private static bool _hooked;

        public static void ResetSession()
        {
            _lastUsedAt = 0;
            _lastMoveAt = 0;
        }

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OnPositionChanged += (s, e) => _lastMoveAt = (long)Time.Ticks;
            _hooked = true;
        }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (World.Player.IsDead) return;
            if (Time.Ticks - _lastMoveAt < IDLE_THRESHOLD_MS) return;
            if (Time.Ticks - _lastUsedAt < MIN_INTERVAL_MS) return;
            if (!AutomationCoordinator.TryAcquire("AutoStealth", 250)) return;
            _lastUsedAt = (long)Time.Ticks;

            // If visible, hide. Otherwise stealth.
            if (!World.Player.IsHidden)
                GameActions.UseSkill(HIDING_SKILL);
            else
                GameActions.UseSkill(STEALTH_SKILL);
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Auto-stealth {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

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
    /// When player HP drops below ThresholdPct, casts a configured heal spell
    /// targeted at self. Cooldown prevents fizzle-spam. Off by default — opt-in.
    /// `-emergencyheal on|off|<pct> [spell]`. Default spell "Greater Heal".
    /// </summary>
    public static class EmergencyHealManager
    {
        public static bool Enabled;
        public static int ThresholdPct = 25;
        public static string SpellName = "Greater Heal";
        private const long POLL_INTERVAL_MS = 500;
        private const long CAST_COOLDOWN_MS = 6000;
        private static long _nextPoll;
        private static long _lastCastAt;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame || World.Player.IsDead) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            int max = World.Player.HitsMax;
            if (max <= 0) return;
            int pct = World.Player.Hits * 100 / max;
            if (pct >= ThresholdPct) return;
            if (Time.Ticks - _lastCastAt < CAST_COOLDOWN_MS) return;
            if (!AutomationCoordinator.TryAcquire("EmergencyHeal", 750, requiresTarget: true)) return;
            _lastCastAt = (long)Time.Ticks;

            TargetManager.SetAutoTarget(World.Player.Serial, TargetType.Beneficial, CursorTarget.Object);
            GameActions.CastSpellByName(SpellName);
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"EmergencyHeal {(on ? "ON" : "OFF")} (<{ThresholdPct}% → {SpellName}).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

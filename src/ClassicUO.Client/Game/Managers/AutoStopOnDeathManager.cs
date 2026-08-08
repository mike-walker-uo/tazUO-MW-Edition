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
    /// Pauses the automation coordinator while the player is dead, then restores
    /// it after resurrection when it was active before death. `-autostop on|off`.
    /// </summary>
    public static class AutoStopOnDeathManager
    {
        public static bool Enabled = true;
        private const long POLL_INTERVAL_MS = 500;
        private static long _nextPoll;
        private static bool _wasDead;
        private static bool _resumeAfterDeath;

        public static void ResetSession()
        {
            _nextPoll = 0;
            _wasDead = false;
            _resumeAfterDeath = false;
        }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            bool d = World.Player.IsDead;
            if (d && !_wasDead)
            {
                // Only restore after resurrection if death paused an active session.
                _resumeAfterDeath = AutomationCoordinator.Enabled;
                if (_resumeAfterDeath)
                    AutomationCoordinator.SetEnabled(false, announce: false);

                AutoFollowManager.Set(0); // also clears Pathfinder
                if (_resumeAfterDeath)
                    UI.Gumps.ToastManager.Show("Automations paused (dead)", 0x21, 3000);
            }
            else if (!d && _wasDead)
            {
                bool profileAllowsAutomation = ClassicUO.Configuration.ProfileManager.CurrentProfile?.AutomationEnabled ?? true;
                if (ShouldResumeAutomation(_resumeAfterDeath, profileAllowsAutomation))
                {
                    AutomationCoordinator.SetEnabled(true, announce: false);
                    UI.Gumps.ToastManager.Show("Automations resumed", 0x35, 2500);
                }

                _resumeAfterDeath = false;
            }
            _wasDead = d;
        }

        internal static bool ShouldResumeAutomation(bool pausedForDeath, bool profileAllowsAutomation)
            => pausedForDeath && profileAllowsAutomation;

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Auto-stop-on-death {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

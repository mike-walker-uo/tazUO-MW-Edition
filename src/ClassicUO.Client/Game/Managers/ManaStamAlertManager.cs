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
    /// Polls Mana / Stamina; rising-edge toast when either drops below pct.
    /// Re-armed when value climbs back above threshold + 10pp hysteresis.
    /// `-manaalert <pct>` and `-stamalert <pct>` (0 = off).
    /// </summary>
    public static class ManaStamAlertManager
    {
        public static int ManaPct;  // 0 = off
        public static int StamPct;  // 0 = off
        private const long POLL_INTERVAL_MS = 750;
        private static long _nextPoll;
        private static bool _manaAlerted, _stamAlerted;

        public static void ResetSession()
        {
            _nextPoll = 0;
            _manaAlerted = _stamAlerted = false;
        }

        public static void Tick()
        {
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            if (ManaPct > 0 && World.Player.ManaMax > 0)
            {
                int cur = World.Player.Mana * 100 / World.Player.ManaMax;
                if (!_manaAlerted && cur < ManaPct)
                {
                    _manaAlerted = true;
                    try { UI.Gumps.ToastManager.Show($"Low mana ({cur}%)", 0x53, 3000, "low-mana",
                        AlertCategory.Combat, AlertSeverity.Warning); } catch { }
                }
                else if (_manaAlerted && cur >= ManaPct + 10) _manaAlerted = false;
            }

            if (StamPct > 0 && World.Player.StaminaMax > 0)
            {
                int cur = World.Player.Stamina * 100 / World.Player.StaminaMax;
                if (!_stamAlerted && cur < StamPct)
                {
                    _stamAlerted = true;
                    try { UI.Gumps.ToastManager.Show($"Low stamina ({cur}%)", 0x49, 3000, "low-stamina",
                        AlertCategory.Combat, AlertSeverity.Warning); } catch { }
                }
                else if (_stamAlerted && cur >= StamPct + 10) _stamAlerted = false;
            }
        }
    }
}

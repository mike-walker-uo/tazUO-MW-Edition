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

using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Toasts a warning once whenever the LastAttack target moves outside the
    /// configured distance from the player. Re-arms when target moves back
    /// inside the range. Default 8 tiles. `-rangewarn on|off|<dist>`.
    /// </summary>
    public static class TargetRangeWarnManager
    {
        public static bool Enabled;
        public static int WarnDistance = 8;
        private const long CHECK_INTERVAL_MS = 800;

        private static long _nextCheck;
        private static bool _warned;
        private static uint _warnedSerial;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextCheck) return;
            _nextCheck = (long)Time.Ticks + CHECK_INTERVAL_MS;

            uint last = TargetManager.LastAttack;
            if (last == 0) { _warned = false; return; }

            var mob = World.Mobiles.Get(last);
            if (mob == null || mob.IsDestroyed) { _warned = false; return; }

            int d = mob.Distance;
            if (d > WarnDistance)
            {
                if (!_warned || _warnedSerial != last)
                {
                    _warned = true;
                    _warnedSerial = last;
                    ToastManager.Show($"{mob.Name ?? "Target"} out of range (d={d}).", 0x21, 3000);
                }
            }
            else if (d <= WarnDistance - 1)
            {
                if (_warned) _warned = false;
            }
        }

        public static void SetEnabled(bool on) { Enabled = on; GameActions.Print($"Range-warn {(on ? "ON" : "OFF")} (>{WarnDistance}).", (ushort)(on ? 0x35 : 0x21)); }
        public static void SetDistance(int d) { if (d < 2) d = 2; if (d > 50) d = 50; WarnDistance = d; GameActions.Print($"Range-warn distance = {d}.", 0x35); }
    }
}

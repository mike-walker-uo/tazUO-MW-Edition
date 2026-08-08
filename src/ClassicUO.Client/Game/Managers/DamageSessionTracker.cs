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

using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Session-scoped cumulative damage totals per target serial.
    /// Reset is manual (gump button) — does not auto-clear on map change or login.
    /// Hooked from WorldTextManager.AddDamage so it sees all damage events the client renders.
    /// </summary>
    internal static class DamageSessionTracker
    {
        private static readonly Dictionary<uint, long> _totals = new Dictionary<uint, long>();
        private static long _sessionStartTicks = (long)Time.Ticks;
        private static long _grandTotal;
        private static long _outgoingTotal;
        private static long _incomingTotal;

        public static void OnDamage(uint serial, int damage)
        {
            if (damage <= 0) return;

            if (_totals.TryGetValue(serial, out long cur))
                _totals[serial] = cur + damage;
            else
                _totals[serial] = damage;

            _grandTotal += damage;

            if (World.Player != null && serial == World.Player.Serial)
                _incomingTotal += damage;
            else
                _outgoingTotal += damage;
        }

        public static long GetTotal(uint serial)
        {
            return _totals.TryGetValue(serial, out long v) ? v : 0;
        }

        public static long GrandTotal => _grandTotal;
        public static long OutgoingTotal => _outgoingTotal;
        public static long IncomingTotal => _incomingTotal;

        /// <summary>
        /// Seconds since last Reset(). Min 1 to avoid divide-by-zero.
        /// </summary>
        public static double SessionSeconds
        {
            get
            {
                long elapsedMs = (long)Time.Ticks - _sessionStartTicks;
                if (elapsedMs < 1000) elapsedMs = 1000;
                return elapsedMs / 1000.0;
            }
        }

        public static void Reset()
        {
            _totals.Clear();
            _grandTotal = 0;
            _outgoingTotal = 0;
            _incomingTotal = 0;
            _sessionStartTicks = (long)Time.Ticks;
        }
    }
}

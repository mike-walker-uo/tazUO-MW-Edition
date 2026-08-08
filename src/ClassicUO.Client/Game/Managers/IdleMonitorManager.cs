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

using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Tracks mouse-position changes as a proxy for activity. After
    /// `IdleSeconds` with no movement, toasts once. Re-arms on next input.
    /// `-idlewarn <seconds>` (0 = off).
    /// </summary>
    public static class IdleMonitorManager
    {
        public static int IdleSeconds; // 0 = off
        private const long POLL_INTERVAL_MS = 1000;
        private static long _nextPoll;
        private static long _lastActiveAt;
        private static Point _lastPos;
        private static bool _alerted;

        public static void ResetSession()
        {
            _nextPoll = 0;
            _lastActiveAt = 0;
            _lastPos = default;
            _alerted = false;
        }

        public static void Tick()
        {
            if (IdleSeconds <= 0) return;
            if (!World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            Point cur = Mouse.Position;
            if (cur != _lastPos)
            {
                _lastPos = cur;
                _lastActiveAt = (long)Time.Ticks;
                _alerted = false;
                return;
            }
            if (_alerted) return;

            long idleMs = Time.Ticks - _lastActiveAt;
            if (idleMs / 1000 >= IdleSeconds)
            {
                _alerted = true;
                try { UI.Gumps.ToastManager.Show($"Idle {IdleSeconds}s", 0x53, 4000); } catch { }
            }
        }
    }
}

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
    /// Auto-follows a target mobile. Each tick, if distance > KeepDistance,
    /// fires Pathfinder.WalkTo toward its current position. Stops when the
    /// target dies / leaves view / `-follow off`. 1s pathing throttle.
    /// `-follow target|<serial>|off`.
    /// </summary>
    public static class AutoFollowManager
    {
        public static uint TargetSerial;
        public static int KeepDistance = 1;
        private const long POLL_INTERVAL_MS = 1000;
        private static long _nextPoll;

        public static bool Active => TargetSerial != 0;

        public static void ResetSession()
        {
            TargetSerial = 0;
            _nextPoll = 0;
            Pathfinder.StopAutoWalk();
        }

        public static void Set(uint serial)
        {
            TargetSerial = serial;
            if (serial == 0)
            {
                Pathfinder.StopAutoWalk();
                GameActions.Print("AutoFollow OFF.", 0x21);
            }
            else
            {
                GameActions.Print($"AutoFollow → 0x{serial:X8}.", 0x35);
            }
        }

        public static void Tick()
        {
            if (TargetSerial == 0) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            var m = World.Mobiles.Get(TargetSerial);
            if (m == null || m.IsDestroyed || m.IsDead)
            {
                Set(0);
                return;
            }
            if (m.Distance <= KeepDistance) return;
            if (!AutomationCoordinator.TryAcquire("AutoFollow", 250)) return;
            Pathfinder.WalkTo(m.X, m.Y, m.Z, KeepDistance);
        }
    }
}

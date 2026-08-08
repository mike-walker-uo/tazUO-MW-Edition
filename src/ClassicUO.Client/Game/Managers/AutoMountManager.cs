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

using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Tracks the last serial occupying Layer.Mount and, if the player is
    /// dismounted while not in war mode and not riding a vehicle, double-clicks
    /// it to remount. Saves time after deaths/forced dismounts.
    /// `-automount on|off`. Use `-automount pick` to (re)target a saved mount.
    /// </summary>
    public static class AutoMountManager
    {
        public static bool Enabled;
        public static uint MountSerial;
        private const long POLL_INTERVAL_MS = 1000;
        private const long ACTION_COOLDOWN_MS = 4000;
        private static long _nextPoll;
        private static long _lastActionAt;

        public static void ResetSession() { _nextPoll = 0; _lastActionAt = 0; }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            // Remember currently-equipped mount if any.
            var equipped = World.Player.FindItemByLayer(Layer.Mount);
            if (equipped != null && equipped.Serial != 0)
            {
                MountSerial = equipped.Serial;
                return;
            }

            if (MountSerial == 0) return;
            if (World.Player.InWarMode) return;
            if (World.Player.IsDead) return;
            if (Time.Ticks - _lastActionAt < ACTION_COOLDOWN_MS) return;

            var item = World.Items.Get(MountSerial);
            if (item == null || item.IsDestroyed) return;
            // Only auto-remount if it's in our pack (still owned). Skip when in world.
            if (!item.OnGround && item.RootContainer == World.Player)
            {
                if (!AutomationCoordinator.TryAcquire("AutoMount", 750)) return;
                _lastActionAt = (long)Time.Ticks;
                GameActions.DoubleClick(MountSerial);
            }
        }

        public static void Pick()
        {
            var equipped = World.Player?.FindItemByLayer(Layer.Mount);
            if (equipped != null && equipped.Serial != 0)
            {
                MountSerial = equipped.Serial;
                GameActions.Print($"AutoMount saved current mount 0x{MountSerial:X8}.", 0x35);
            }
            else
            {
                GameActions.Print("Not mounted. Mount up first, then run -automount pick.", 0x21);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"AutoMount {(on ? "ON" : "OFF")} (mount=0x{MountSerial:X8}).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

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
    /// Remembers the last serials equipped in OneHanded / TwoHanded. If either
    /// layer becomes empty unexpectedly (disarm, blood oath drop), queues a
    /// re-equip via MoveItemQueue. Cooldown to avoid flooding.
    /// `-autorearm on|off|pick`.
    /// </summary>
    public static class AutoRearmManager
    {
        public static bool Enabled;
        public static uint OneHandedSerial;
        public static uint TwoHandedSerial;
        private const long POLL_INTERVAL_MS = 700;
        private const long ACTION_COOLDOWN_MS = 1500;
        private static long _nextPoll;
        private static long _lastActionAt;

        public static void ResetSession() { _nextPoll = 0; _lastActionAt = 0; }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame || World.Player.IsDead) return;
            if (MoveItemQueue.Instance != null && !MoveItemQueue.Instance.IsEmpty) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            // Always remember whatever is currently in hand.
            var oh = World.Player.FindItemByLayer(Layer.OneHanded);
            var th = World.Player.FindItemByLayer(Layer.TwoHanded);
            if (oh != null && oh.Serial != 0) OneHandedSerial = oh.Serial;
            if (th != null && th.Serial != 0) TwoHandedSerial = th.Serial;

            if (Time.Ticks - _lastActionAt < ACTION_COOLDOWN_MS) return;

            // If both hands empty but we remember something, try to put it back.
            if (oh == null && th == null)
            {
                uint pick = TwoHandedSerial != 0 ? TwoHandedSerial : OneHandedSerial;
                if (pick == 0) return;
                var item = World.Items.Get(pick);
                if (item == null || item.IsDestroyed) return;
                if (item.RootContainer != World.Player) return;
                if (!AutomationCoordinator.TryAcquire("AutoRearm", 750)) return;
                _lastActionAt = (long)Time.Ticks;
                Layer lay = TwoHandedSerial != 0 ? Layer.TwoHanded : Layer.OneHanded;
                MoveItemQueue.Instance.EnqueueEquipSingle(pick, lay);
            }
        }

        public static void Pick()
        {
            if (World.Player == null) return;
            var oh = World.Player.FindItemByLayer(Layer.OneHanded);
            var th = World.Player.FindItemByLayer(Layer.TwoHanded);
            OneHandedSerial = oh?.Serial ?? 0;
            TwoHandedSerial = th?.Serial ?? 0;
            GameActions.Print($"AutoRearm saved: 1H=0x{OneHandedSerial:X8} 2H=0x{TwoHandedSerial:X8}.", 0x35);
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"AutoRearm {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

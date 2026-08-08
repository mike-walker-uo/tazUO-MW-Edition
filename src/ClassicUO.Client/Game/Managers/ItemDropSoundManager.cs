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

using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Plays a short chime when a new lootable on-ground item appears within
    /// range of the player. Subscribed to EventSink.OnItemCreated. Min 2s gap
    /// between chimes to avoid spam.
    /// </summary>
    public static class ItemDropSoundManager
    {
        public static bool Enabled;
        public static int Range = 8;
        public const int CHIME_SOUND_ID = 0x0055; // soft chime
        private const long MIN_GAP_MS = 2000;
        private static long _lastSoundAt;
        private static bool _hooked;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OnItemCreated += OnItemCreated;
            _hooked = true;
        }

        private static void OnItemCreated(object sender, System.EventArgs e)
        {
            if (!Enabled) return;
            if (World.Player == null) return;
            if (!(sender is Item item)) return;
            if (!item.OnGround) return;
            if (item.IsCorpse) return;
            if (!item.IsLootable) return;
            if (!item.IsMovable) return;
            if (!(item.ItemData.IsStackable || item.ItemData.IsWearable || item.IsCoin)) return;
            if (item.Distance > Range) return;
            if (Time.Ticks - _lastSoundAt < MIN_GAP_MS) return;
            _lastSoundAt = (long)Time.Ticks;
            try { Client.Game?.Audio?.PlaySound(CHIME_SOUND_ID); } catch { }
        }

        public static void SetEnabled(bool on)
        {
            EnsureHooked();
            Enabled = on;
            GameActions.Print($"Item-drop sound {(on ? "ON" : "OFF")} (range {Range}).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

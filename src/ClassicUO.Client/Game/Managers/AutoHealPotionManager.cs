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
    /// When player HP% drops below ThresholdPct, finds the first heal potion
    /// (graphic 0x0F0C) in pack and DoubleClicks it. 10 s drink cooldown.
    /// Off by default. `-autopot heal on|off|<pct>`.
    /// </summary>
    public static class AutoHealPotionManager
    {
        public static bool Enabled;
        public static int ThresholdPct = 50;
        public const ushort HEAL_POT_GRAPHIC = 0x0F0C;
        private const long POLL_INTERVAL_MS = 500;
        private const long DRINK_COOLDOWN_MS = 10000;
        private static long _nextPoll;
        private static long _lastDrinkAt;

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
            if (Time.Ticks - _lastDrinkAt < DRINK_COOLDOWN_MS) return;

            var bp = World.Player.FindItemByLayer(Layer.Backpack);
            if (bp == null) return;
            for (var node = bp.Items; node != null; node = node.Next)
            {
                if (!(node is GameObjects.Item it)) continue;
                if (it.Graphic != HEAL_POT_GRAPHIC) continue;
                if (!AutomationCoordinator.TryAcquire("AutoHealPotion", 750)) return;
                _lastDrinkAt = (long)Time.Ticks;
                GameActions.DoubleClick(it.Serial);
                return;
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Auto heal-pot {(on ? "ON" : "OFF")} (<{ThresholdPct}%).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

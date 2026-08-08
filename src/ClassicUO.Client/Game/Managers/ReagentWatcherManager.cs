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
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Periodically counts standard reagents in the player's worn-bag tree and
    /// raises a toast notification when any drop below the configured threshold.
    /// Each reagent only toasts once per "stocked-then-depleted" cycle to avoid
    /// repeat spam.
    /// </summary>
    public static class ReagentWatcherManager
    {
        public static bool Enabled;
        public static int Threshold = 10;
        private const long CHECK_INTERVAL_MS = 5000;
        private const long REPEAT_COOLDOWN_MS = 60_000;

        // Standard 8 OSI reagents.
        private static readonly (ushort Graphic, string Name)[] _reagents =
        {
            (0x0F7A, "Black Pearl"),
            (0x0F7B, "Blood Moss"),
            (0x0F86, "Mandrake Root"),
            (0x0F84, "Garlic"),
            (0x0F85, "Ginseng"),
            (0x0F88, "Nightshade"),
            (0x0F8C, "Sulfurous Ash"),
            (0x0F8D, "Spider's Silk"),
        };

        private static readonly Dictionary<ushort, long> _lastWarnTime = new Dictionary<ushort, long>();
        private static long _nextCheckTime;

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextCheckTime) return;
            _nextCheckTime = (long)Time.Ticks + CHECK_INTERVAL_MS;

            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (backpack == null) return;

            for (int i = 0; i < _reagents.Length; i++)
            {
                ushort g = _reagents[i].Graphic;
                int count = CountIn(backpack, g);
                if (count < Threshold)
                {
                    if (!_lastWarnTime.TryGetValue(g, out long last) ||
                        Time.Ticks - last > REPEAT_COOLDOWN_MS)
                    {
                        _lastWarnTime[g] = (long)Time.Ticks;
                        ToastManager.Show($"Low reagent: {_reagents[i].Name} ({count})", 0x35, 4000);
                    }
                }
                else
                {
                    // Reset cooldown so next depletion warns immediately.
                    _lastWarnTime.Remove(g);
                }
            }
        }

        private static int CountIn(Item parent, ushort graphic)
        {
            int total = 0;
            for (LinkedObject i = parent.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                if (it.Graphic == graphic && it.Exists) total += it.Amount;
                if (it.ItemData.IsContainer && !it.IsEmpty) total += CountIn(it, graphic);
            }
            return total;
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Reagent watcher {(on ? "ON" : "OFF")} (threshold {Threshold}).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void SetThreshold(int t)
        {
            if (t < 1) t = 1;
            if (t > 999) t = 999;
            Threshold = t;
            GameActions.Print($"Reagent threshold = {t}.", 0x35);
        }
    }
}

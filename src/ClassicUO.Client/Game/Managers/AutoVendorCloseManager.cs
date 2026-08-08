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
    /// When the player walks > MaxDistance tiles from the vendor mob owning
    /// an open ShopGump, auto-closes it. Eliminates leftover shop windows.
    /// `-vendorclose on|off`.
    /// </summary>
    public static class AutoVendorCloseManager
    {
        public static bool Enabled;
        public static int MaxDistance = 4;
        private const long POLL_INTERVAL_MS = 750;
        private static long _nextPoll;

        public static void ResetSession() => _nextPoll = 0;

        public static void Tick()
        {
            if (!AutomationCoordinator.Enabled) return;
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;
            if (UIManager.Gumps == null) return;

            var doomed = new System.Collections.Generic.List<Gump>();
            foreach (var g in UIManager.Gumps)
            {
                if (!(g is ShopGump sg)) continue;
                uint vendor = g.LocalSerial;
                if (vendor == 0) continue;
                var m = World.Mobiles.Get(vendor);
                if (m == null || m.IsDestroyed) { doomed.Add(g); continue; }
                if (m.Distance > MaxDistance) doomed.Add(g);
            }
            foreach (var g in doomed) g.Dispose();
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Auto-close vendor shop {(on ? "ON" : "OFF")} (dist {MaxDistance}).",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

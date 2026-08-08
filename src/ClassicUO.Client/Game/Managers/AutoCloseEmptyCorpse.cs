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
    /// Scans open ContainerGumps / GridContainers tied to corpses. When the
    /// corpse contains zero items for more than CloseDelay ms, disposes the
    /// gump. Keeps the field clean during PVM grinds. `-autoclosecorpse on|off`.
    /// </summary>
    public static class AutoCloseEmptyCorpse
    {
        public static bool Enabled;
        public static int CloseDelayMs = 1500;
        private const long POLL_INTERVAL_MS = 500;
        private static long _nextPoll;
        // serial -> first-time-seen-empty (ticks)
        private static readonly System.Collections.Generic.Dictionary<uint, long> _emptySince
            = new System.Collections.Generic.Dictionary<uint, long>();

        public static void ResetSession() { _nextPoll = 0; _emptySince.Clear(); }

        public static void Tick()
        {
            if (!AutomationCoordinator.Enabled) return;
            if (!Enabled) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;
            if (UIManager.Gumps == null) return;

            long now = (long)Time.Ticks;
            // Iterate a copy because Dispose mutates the gump list.
            var snapshot = new System.Collections.Generic.List<Gump>();
            foreach (var g in UIManager.Gumps)
            {
                if (g is ContainerGump c) snapshot.Add(c);
                else if (g is GridContainer gc) snapshot.Add(gc);
            }

            foreach (var g in snapshot)
            {
                uint serial = g.LocalSerial;
                if (serial == 0) continue;
                var item = World.Items.Get(serial);
                if (item == null || item.IsDestroyed) { _emptySince.Remove(serial); continue; }
                if (!item.IsCorpse) { _emptySince.Remove(serial); continue; }

                if (!item.IsEmpty) { _emptySince.Remove(serial); continue; }

                if (!_emptySince.TryGetValue(serial, out long first)) { _emptySince[serial] = now; continue; }
                if (now - first < CloseDelayMs) continue;

                _emptySince.Remove(serial);
                g.Dispose();
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            _emptySince.Clear();
            GameActions.Print($"Auto-close empty corpse {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

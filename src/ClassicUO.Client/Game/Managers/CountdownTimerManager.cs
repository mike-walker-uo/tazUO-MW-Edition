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
    /// Lightweight one-off timer queue. `-countdown <seconds> <label>` adds
    /// a deadline; when reached, toasts the label and plays a chime.
    /// Different from SpawnTimerManager (which is wall-clock + named/cancellable
    /// list) — this is fire-and-forget. `-countdowns` lists pending.
    /// </summary>
    public static class CountdownTimerManager
    {
        private struct Entry { public long DueAt; public string Label; }
        private static readonly List<Entry> _q = new List<Entry>();
        public static bool HasPending => _q.Count != 0;

        public static void Schedule(int seconds, string label)
        {
            if (seconds <= 0) return;
            _q.Add(new Entry { DueAt = (long)Time.Ticks + seconds * 1000L, Label = label ?? "(timer)" });
            GameActions.Print($"Countdown +{seconds}s: {label}", 0x35);
        }

        public static void Tick()
        {
            if (_q.Count == 0) return;
            long now = (long)Time.Ticks;
            for (int i = _q.Count - 1; i >= 0; i--)
            {
                if (_q[i].DueAt > now) continue;
                string label = _q[i].Label;
                _q.RemoveAt(i);
                try { UI.Gumps.ToastManager.Show(label, 0x44, 4000, "countdown",
                    AlertCategory.System, AlertSeverity.Info); } catch { }
                try { Client.Game?.Audio?.PlaySound(0x0055); } catch { }
            }
        }

        public static void List()
        {
            long now = (long)Time.Ticks;
            if (_q.Count == 0) { GameActions.Print("No countdowns pending.", 0x21); return; }
            foreach (var e in _q)
            {
                long rem = (e.DueAt - now) / 1000;
                if (rem < 0) rem = 0;
                GameActions.Print($"  {rem}s — {e.Label}", 0x44);
            }
        }

        public static void Clear() { _q.Clear(); GameActions.Print("Countdowns cleared.", 0x21); }
    }
}

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

using System;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Watches Player.IsDead rising edge. On death, prints a recap of the last
    /// N seconds of incoming damage from CombatLogManager (already capturing
    /// all damage). Helps diagnose what killed you. `-deathrecap on|off`.
    /// </summary>
    public static class DeathRecapManager
    {
        public static bool Enabled = true;
        public static int WindowSeconds = 12;
        private const long POLL_INTERVAL_MS = 500;
        private static long _nextPoll;
        private static bool _wasDead;

        public static void ResetSession() { _nextPoll = 0; _wasDead = false; }

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            bool dead = World.Player.IsDead;
            if (dead && !_wasDead) Dump();
            _wasDead = dead;
        }

        private static void Dump()
        {
            DateTime cutoff = DateTime.Now.AddSeconds(-WindowSeconds);
            int total = 0;
            var counts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in CombatLogManager.All())
            {
                if (!e.IsIncoming) continue;
                if (e.Time < cutoff) continue;
                total += e.Damage;
                string src = e.TargetName ?? "?"; // CombatLog stores target = self for incoming; no aggressor.
                // No aggressor info available — bucket by 4s window so we can show pattern.
                int bucket = (int)((DateTime.Now - e.Time).TotalSeconds / 4);
                string key = $"{bucket * 4}-{bucket * 4 + 4}s ago";
                counts.TryGetValue(key, out int v); counts[key] = v + e.Damage;
            }

            GameActions.Print("--- Death recap ---", 0x21);
            GameActions.Print($"Total incoming last {WindowSeconds}s: {total}", 0x21);
            foreach (var kv in counts) GameActions.Print($"  {kv.Key}: {kv.Value} dmg", 0x21);
            GameActions.Print("-------------------", 0x21);
            try { UI.Gumps.ToastManager.Show($"DEATH: {total} dmg in {WindowSeconds}s", 0x21, 6000,
                "death-recap", AlertCategory.Combat, AlertSeverity.Critical); } catch { }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Death recap {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

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
using System.IO;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Appends a line to `{ProfilePath}/deaths.log` each time Player.IsDead
    /// flips true. Independent of DeathRecapManager (which prints to journal).
    /// `-deathlog on|off`, `-deathlog show <n>` prints last N entries.
    /// </summary>
    public static class DeathLogManager
    {
        public static bool Enabled = true;
        private const long POLL_INTERVAL_MS = 500;
        private static long _nextPoll;
        private static bool _wasDead;

        public static void ResetSession() { _nextPoll = 0; _wasDead = false; }

        private static string FilePath =>
            string.IsNullOrEmpty(ProfileManager.ProfilePath)
                ? null
                : Path.Combine(ProfileManager.ProfilePath, "deaths.log");

        public static void Tick()
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            if (Time.Ticks < _nextPoll) return;
            _nextPoll = (long)Time.Ticks + POLL_INTERVAL_MS;

            bool d = World.Player.IsDead;
            if (d && !_wasDead) Append();
            _wasDead = d;
        }

        private static void Append()
        {
            string p = FilePath;
            if (string.IsNullOrEmpty(p)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.AppendAllText(p,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{World.Player.X}\t{World.Player.Y}\t{World.Player.Z}\tmap={World.MapIndex}\n");
            }
            catch (Exception ex) { FeatureDiagnostics.RecordFailure("DeathLog", ex); }
        }

        public static void Show(int last)
        {
            string p = FilePath;
            if (string.IsNullOrEmpty(p) || !File.Exists(p)) { GameActions.Print("No death log yet.", 0x21); return; }
            try
            {
                var all = File.ReadAllLines(p);
                int n = Math.Min(last, all.Length);
                for (int i = all.Length - n; i < all.Length; i++)
                    GameActions.Print("  " + all[i], 0x44);
            }
            catch (Exception ex)
            {
                FeatureDiagnostics.RecordFailure("DeathLog", ex);
                GameActions.Print("Failed to read log.", 0x21);
            }
        }
    }
}

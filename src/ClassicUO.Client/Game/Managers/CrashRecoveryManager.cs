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
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Periodically copies the active profile.json and gumps.xml into a
    /// crash_recovery/ subfolder with a timestamped filename. Lets the user
    /// roll back if a setting change or gump-save corruption causes problems
    /// at next login. Best-effort — silently skips on IO failure.
    /// </summary>
    public static class CrashRecoveryManager
    {
        private const long INTERVAL_MS = 5 * 60 * 1000; // 5 min
        private const int MAX_SNAPSHOTS = 6;
        private static long _nextSnapshot;

        public static void ResetForProfile() => _nextSnapshot = 0;

        public static void Tick()
        {
            if (string.IsNullOrEmpty(ProfileManager.ProfilePath)) return;
            if (Time.Ticks < _nextSnapshot) return;
            _nextSnapshot = (long)Time.Ticks + INTERVAL_MS;
            try { Snapshot(); } catch (Exception ex) { Log.Error("CrashRecovery snapshot: " + ex); }
        }

        public static void Snapshot()
        {
            string profilePath = ProfileManager.ProfilePath;
            if (string.IsNullOrEmpty(profilePath)) return;

            string recoveryDir = Path.Combine(profilePath, "crash_recovery");
            Directory.CreateDirectory(recoveryDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string srcProfile = Path.Combine(profilePath, "profile.json");
            string srcGumps = Path.Combine(profilePath, "gumps.xml");

            if (File.Exists(srcProfile))
            {
                File.Copy(srcProfile, Path.Combine(recoveryDir, $"profile_{stamp}.json"), overwrite: true);
            }
            if (File.Exists(srcGumps))
            {
                File.Copy(srcGumps, Path.Combine(recoveryDir, $"gumps_{stamp}.xml"), overwrite: true);
            }

            PruneOld(recoveryDir);
        }

        private static void PruneOld(string dir)
        {
            try
            {
                // Keep MAX_SNAPSHOTS most-recent profile_*.json and same number of gumps_*.xml.
                PruneByPrefix(dir, "profile_", MAX_SNAPSHOTS);
                PruneByPrefix(dir, "gumps_", MAX_SNAPSHOTS);
            }
            catch (Exception ex) { Log.Error("CrashRecovery prune: " + ex); }
        }

        private static void PruneByPrefix(string dir, string prefix, int keep)
        {
            string[] files = Directory.GetFiles(dir, prefix + "*");
            if (files.Length <= keep) return;
            Array.Sort(files);
            for (int i = 0; i < files.Length - keep; i++)
            {
                try { File.Delete(files[i]); }
                catch (Exception ex) { FeatureDiagnostics.RecordFailure("CrashRecovery", ex); }
            }
        }
    }
}

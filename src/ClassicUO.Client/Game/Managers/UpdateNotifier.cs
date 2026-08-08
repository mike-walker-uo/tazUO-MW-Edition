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
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// On scene-load, fires a single best-effort GitHub Releases query against
    /// the TazUO repo. If the latest release tag parses as a higher version
    /// than the running build, raises a main-thread toast. Failures are
    /// recorded in the shared feature diagnostics registry.
    /// </summary>
    public static class UpdateNotifier
    {
        private const string RELEASES_URL = "https://api.github.com/repos/PlayTazUO/TazUO/releases/latest";
        private static bool _checked;

        public static void CheckOnce()
        {
            if (_checked) return;
            _checked = true;
            _ = Task.Run(DoCheck);
        }

        private static async Task DoCheck()
        {
            try
            {
                using var http = new HttpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TazUO-UpdateCheck/1.0");
                var resp = await http.GetAsync(RELEASES_URL, timeout.Token);
                if (!resp.IsSuccessStatusCode) return;
                string body = await resp.Content.ReadAsStringAsync();

                // Cheap parse: look for "tag_name":"vX.Y.Z..."
                var m = Regex.Match(body, "\"tag_name\"\\s*:\\s*\"v?(?<v>[0-9.]+)\"");
                if (!m.Success) return;
                string remoteStr = m.Groups["v"].Value;
                if (!Version.TryParse(remoteStr, out Version remote)) return;
                var local = CUOEnviroment.Version;
                if (local == null) return;
                if (remote > local)
                {
                    MainThreadQueue.EnqueueAction(() =>
                    {
                        if (World.InGame)
                            ToastManager.Show($"TazUO update available: v{remote} (running v{local})", 0x35, 8000);
                    });
                }
            }
            catch (Exception ex)
            {
                FeatureDiagnostics.RecordFailure("UpdateNotifier", ex);
            }
        }
    }
}

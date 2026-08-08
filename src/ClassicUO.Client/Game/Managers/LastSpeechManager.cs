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

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Captures the player's last spoken line by hooking RawMessageReceived
    /// and filtering on Parent == World.Player. `-lastmsg` re-broadcasts it.
    /// Tiny convenience for shopkeeper "vendor buy"-style repeats.
    /// </summary>
    public static class LastSpeechManager
    {
        public static string Last;
        private static bool _hooked;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMsg;
            _hooked = true;
        }

        private static void OnMsg(object sender, MessageEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.Text)) return;
            if (e.Parent == null || World.Player == null) return;
            if (e.Parent.Serial != World.Player.Serial) return;
            // Skip messages that look like client commands or starts-with prefixes.
            if (e.Text.StartsWith("-") || e.Text.StartsWith(",") || e.Text.StartsWith("[")) return;
            Last = e.Text;
        }

        public static void Repeat()
        {
            EnsureHooked();
            if (string.IsNullOrEmpty(Last)) { GameActions.Print("No prior message.", 0x21); return; }
            GameActions.Say(Last);
        }
    }
}

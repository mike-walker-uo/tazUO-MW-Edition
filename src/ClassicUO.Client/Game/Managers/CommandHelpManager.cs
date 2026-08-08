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

using System.Linq;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// `-help` lists all registered `-cmd` chat commands alphabetically.
    /// `-help <pat>` filters by substring. With ~80+ TazUO commands now,
    /// memory help is appreciated.
    /// </summary>
    public static class CommandHelpManager
    {
        public static void Print(string filter)
        {
            var names = CommandManager.Commands.Keys.ToList();
            names.Sort(System.StringComparer.OrdinalIgnoreCase);
            int shown = 0;

            GameActions.Print("--- chat commands ---", 0x35);
            foreach (var n in names)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    n.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                GameActions.Print("  -" + n, 0x44);
                shown++;
            }
            GameActions.Print($"--- {shown} command(s) ---", 0x35);
        }
    }
}

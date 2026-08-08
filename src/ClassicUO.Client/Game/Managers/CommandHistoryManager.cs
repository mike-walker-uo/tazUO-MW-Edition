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
    /// In-memory ring of the most recently invoked chat commands. Populated
    /// by CommandManager.Execute. `-history` prints the last N; `-history N`
    /// re-executes the n-th most-recent entry.
    /// </summary>
    public static class CommandHistoryManager
    {
        public const int CAPACITY = 25;
        private static readonly LinkedList<string> _history = new LinkedList<string>();

        public static void Record(string name, string[] args)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (name.Equals("history", System.StringComparison.OrdinalIgnoreCase)) return; // don't pollute
            string line = args == null || args.Length <= 1
                ? "-" + name
                : "-" + name + " " + string.Join(" ", args, 1, args.Length - 1);
            _history.AddLast(line);
            while (_history.Count > CAPACITY) _history.RemoveFirst();
        }

        public static void Print(int n)
        {
            if (_history.Count == 0) { GameActions.Print("History empty.", 0x21); return; }
            int shown = 0;
            int total = _history.Count;
            var node = _history.Last;
            while (node != null && shown < n)
            {
                GameActions.Print($"  {total - shown}: {node.Value}", 0x44);
                node = node.Previous;
                shown++;
            }
        }

        public static string GetByIndex(int oneBased)
        {
            if (oneBased < 1 || oneBased > _history.Count) return null;
            int idx = _history.Count - oneBased;
            int i = 0;
            foreach (var s in _history) { if (i++ == idx) return s; }
            return null;
        }

        public static void Clear() => _history.Clear();
    }
}

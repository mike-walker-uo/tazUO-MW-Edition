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
using System.Linq;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// `-countall [topN]` walks the backpack (recursively into sub-containers)
    /// and prints a histogram of graphic → total amount, sorted descending.
    /// Useful "what loot do I actually have" overview.
    /// </summary>
    public static class CountAllManager
    {
        public static void Print(int topN)
        {
            if (World.Player == null) return;
            var bp = World.Player.FindItemByLayer(Layer.Backpack);
            if (bp == null) { GameActions.Print("No backpack.", 0x21); return; }

            var counts = new Dictionary<ushort, int>();
            Walk(bp, counts);
            if (counts.Count == 0) { GameActions.Print("Pack is empty.", 0x21); return; }

            GameActions.Print($"--- pack contents (top {topN}) ---", 0x35);
            int shown = 0;
            foreach (var kv in counts.OrderByDescending(k => k.Value))
            {
                if (shown++ >= topN) break;
                GameActions.Print($"  0x{kv.Key:X4} ×{kv.Value}", 0x44);
            }
        }

        private static void Walk(GameObjects.Item parent, Dictionary<ushort, int> counts)
        {
            for (var node = parent.Items; node != null; node = node.Next)
            {
                if (!(node is GameObjects.Item it)) continue;
                counts.TryGetValue(it.Graphic, out int v);
                counts[it.Graphic] = v + (it.Amount > 0 ? it.Amount : 1);
                if (it.ItemData.IsContainer && !it.IsEmpty) Walk(it, counts);
            }
        }
    }
}

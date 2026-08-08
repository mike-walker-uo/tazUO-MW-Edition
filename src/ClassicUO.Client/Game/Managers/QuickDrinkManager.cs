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
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Single-keypress potion use: finds the first potion of a given type in
    /// the player's worn-bag tree and double-clicks it. Standard OSI potion
    /// hues are used to disambiguate when graphic 0x0F0E (a generic potion)
    /// covers multiple types.
    /// </summary>
    public static class QuickDrinkManager
    {
        // Potion graphic IDs vary slightly across eras; OSI uses 0x0F06-0x0F0E.
        // Map each "alias" → (graphic, hue) tuples. First match in bag wins.
        private static readonly Dictionary<string, (ushort graphic, ushort hue)[]> _kinds = new Dictionary<string, (ushort, ushort)[]>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["heal"]       = new[] { ((ushort)0x0F0C, (ushort)0), ((ushort)0x0F0E, (ushort)0) },
            ["greaterheal"] = new[] { ((ushort)0x0F0C, (ushort)0) },
            ["cure"]       = new[] { ((ushort)0x0F07, (ushort)0) },
            ["refresh"]    = new[] { ((ushort)0x0F0B, (ushort)0) },
            ["greaterrefresh"] = new[] { ((ushort)0x0F0B, (ushort)0) },
            ["agility"]    = new[] { ((ushort)0x0F08, (ushort)0) },
            ["strength"]   = new[] { ((ushort)0x0F09, (ushort)0) },
            ["mana"]       = new[] { ((ushort)0x0F0D, (ushort)0) },
            ["nightsight"] = new[] { ((ushort)0x0F06, (ushort)0) },
            ["explosion"]  = new[] { ((ushort)0x0F0D, (ushort)0) },
        };

        public static bool Drink(string kind)
        {
            if (World.Player == null || !World.InGame) return false;
            if (!_kinds.TryGetValue(kind, out var graphics))
            {
                GameActions.Print($"Unknown potion '{kind}'. Try: heal, cure, refresh, agility, strength, mana, nightsight.", 0x21);
                return false;
            }

            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (backpack == null) { GameActions.Print("No backpack.", 0x21); return false; }

            foreach (var (graphic, hue) in graphics)
            {
                var found = FindRecursive(backpack, graphic, hue);
                if (found != null)
                {
                    GameActions.DoubleClick(found.Serial);
                    GameActions.Print($"Used potion: {kind}.", 0x35);
                    return true;
                }
            }

            GameActions.Print($"No '{kind}' potion in pack.", 0x21);
            return false;
        }

        private static Item FindRecursive(Item parent, ushort graphic, ushort hue)
        {
            for (LinkedObject i = parent.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                if (it.Graphic == graphic && (hue == 0 || it.Hue == hue) && it.Exists && it.Amount > 0)
                    return it;
                if (it.ItemData.IsContainer && !it.IsEmpty)
                {
                    var nested = FindRecursive(it, graphic, hue);
                    if (nested != null) return nested;
                }
            }
            return null;
        }
    }
}

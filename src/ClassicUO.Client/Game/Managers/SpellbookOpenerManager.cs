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
    /// Finds the first spellbook in the player's backpack and double-clicks it.
    /// Spellbook graphic IDs vary (0x0EFA magery, 0x2253 necromancy, etc.),
    /// so we use the same graphic set as SpellbookGump.
    /// `-spellbook` to open.
    /// </summary>
    public static class SpellbookOpenerManager
    {
        // Spellbook graphics handled by SpellbookGump.
        private static readonly ushort[] BookGraphics =
        {
            0x0EFA, // magery
            0x2253, // necromancy
            0x2252, // chivalry
            0x238C, // bushido
            0x23A0, // ninjitsu
            0x2D50, // spellweaving
            0x2D9D, // mysticism
            0x225A, // mastery
            0x225B, // mastery alternate
        };

        public static bool IsSpellbook(GameObjects.Item item)
        {
            if (item == null || item.IsDestroyed)
            {
                return false;
            }

            ushort graphic = item.Graphic;
            ushort originalGraphic = item.OriginalGraphic;

            for (int i = 0; i < BookGraphics.Length; i++)
            {
                if (graphic == BookGraphics[i] || originalGraphic == BookGraphics[i])
                {
                    return true;
                }
            }

            return false;
        }

        public static void Open()
        {
            if (World.Player == null) return;
            var bp = World.Player.FindItemByLayer(Data.Layer.Backpack);
            if (bp == null) { GameActions.Print("No backpack.", 0x21); return; }

            GameObjects.Item spellbook = FindSpellbook(bp);

            if (spellbook != null)
            {
                GameActions.DoubleClick(spellbook.Serial);
                GameActions.Print($"Opening spellbook 0x{spellbook.Serial:X8}.", 0x35);
                return;
            }

            GameActions.Print("No spellbook found in backpack.", 0x21);
        }

        private static GameObjects.Item FindSpellbook(GameObjects.Item container)
        {
            for (var node = container.Items; node != null; node = node.Next)
            {
                if (!(node is GameObjects.Item item) || item.IsDestroyed)
                {
                    continue;
                }

                if (IsSpellbook(item))
                {
                    return item;
                }

                if (item.ItemData.IsContainer)
                {
                    GameObjects.Item nested = FindSpellbook(item);

                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }
    }
}

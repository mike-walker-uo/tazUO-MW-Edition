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
    /// Finds first clean-bandage stack in player pack (graphic 0x0E21),
    /// DoubleClicks it, and arms SetAutoTarget on the player so the bandage
    /// target prompt auto-applies to self. `-bandage` for self-heal; or
    /// `-bandage <serial>` to target a specific mobile.
    /// </summary>
    public static class BandageQuickUseManager
    {
        public const ushort BANDAGE_GRAPHIC = 0x0E21;

        public static void UseSelf() => UseOn(World.Player?.Serial ?? 0);

        public static void UseOn(uint targetSerial)
        {
            if (World.Player == null) return;
            var bp = World.Player.FindItemByLayer(Data.Layer.Backpack);
            if (bp == null) { GameActions.Print("No backpack.", 0x21); return; }
            for (var node = bp.Items; node != null; node = node.Next)
            {
                if (!(node is GameObjects.Item it)) continue;
                if (it.Graphic != BANDAGE_GRAPHIC) continue;
                if (targetSerial != 0)
                    TargetManager.SetAutoTarget(targetSerial, TargetType.Beneficial, CursorTarget.Object);
                GameActions.DoubleClick(it.Serial);
                return;
            }
            GameActions.Print("No clean bandages in pack.", 0x21);
        }
    }
}

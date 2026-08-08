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

using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Single button that casts a configured spell + auto-targets the player.
    /// Default is Greater Heal (Magery spell 29). Right-click cycles between
    /// Heal (4) and Greater Heal (29).
    /// </summary>
    internal class HealSelfButtonGump : AnchorableGump
    {
        private const int W = 60;
        private const int H = 22;
        private static int _spellId = 29;

        private readonly AlphaBlendControl _bg;
        private readonly NiceButton _btn;

        public HealSelfButtonGump() : this(360, 60) { }

        public HealSelfButtonGump(int x, int y) : base(0, 0)
        {
            X = x; Y = y;
            Width = W; Height = H;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            AnchorType = ANCHOR_TYPE.NONE;
            GroupMatrixWidth = W;
            GroupMatrixHeight = H;

            Add(_bg = new AlphaBlendControl(0.7f) { Width = W, Height = H });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.7f);
            Add(_btn = new NiceButton(1, 1, W - 2, H - 2, ButtonAction.Activate, SpellLabel()) { ButtonParameter = 1, IsSelectable = false });
            _btn.SetTooltip("Click: cast on self.\nRight-click the gump to close.\nAlt+click: cycle Heal / Greater Heal.");
        }

        public override GumpType GumpType => GumpType.HealSelfButton;

        private static string SpellLabel() => _spellId == 4 ? "Heal" : "G.Heal";

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID != 1) { base.OnButtonClick(buttonID); return; }

            if (Keyboard.Alt)
            {
                _spellId = _spellId == 4 ? 29 : 4;
                _btn.TextLabel.Text = SpellLabel();
                return;
            }

            // Cast + auto-target self.
            TargetManager.SetAutoTarget(World.Player?.Serial ?? 0, TargetType.Beneficial, CursorTarget.Object);
            GameActions.CastSpell(_spellId);
        }
    }
}

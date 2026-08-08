#region license
// TazUO addition.
#endregion

using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Compact floating bar with one-click buttons for the common pet
    /// commands. Each button says the corresponding "all <verb>" phrase
    /// to address every nearby pet. Opened via `-petbar`.
    /// </summary>
    internal class PetCommandBarGump : Gump
    {
        private const int BTN_W = 50;
        private const int BTN_H = 22;
        private const int GAP = 4;
        private const int PAD = 6;

        private static readonly (string label, string verb, string tip)[] BUTTONS =
        {
            ("Kill",    "kill",    "Say 'all kill' — pets attack your LastAttack."),
            ("Guard",   "guard",   "Say 'all guard' — pets guard you."),
            ("Follow",  "follow",  "Say 'all follow' — pets follow you."),
            ("Come",    "come",    "Say 'all come' — pets come to your tile."),
            ("Stay",    "stay",    "Say 'all stay' — pets hold position."),
            ("Stop",    "stop",    "Say 'all stop' — pets cease current action."),
            ("Release", "release", "Say 'all release' — release pet ownership (use with care)."),
        };

        public PetCommandBarGump() : base(0, 0)
        {
            X = 200; Y = 60;
            int w = PAD * 2 + BUTTONS.Length * BTN_W + (BUTTONS.Length - 1) * GAP;
            int h = PAD * 2 + BTN_H;
            Width = w; Height = h;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            var background = new AlphaBlendControl(0.78f) { Width = w, Height = h };
            CustomGumpThemeManager.ApplyDataSurface(background, 0.78f);
            Add(background);

            int x = PAD;
            foreach (var (label, verb, tip) in BUTTONS)
            {
                var btn = new NiceButton(x, PAD, BTN_W, BTN_H, ButtonAction.Activate, label, font: 1) { IsSelectable = false };
                btn.SetTooltip(tip);
                string v = verb;
                btn.MouseUp += (s, e) => { GameActions.Say($"all {v}", 0x33, MessageType.Regular); };
                Add(btn);
                x += BTN_W + GAP;
            }
        }

        public override GumpType GumpType => GumpType.None;

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);
            return true;
        }
    }
}

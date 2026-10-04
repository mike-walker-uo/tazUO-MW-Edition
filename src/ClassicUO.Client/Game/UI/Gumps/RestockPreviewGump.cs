using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class RestockPreviewGump : Gump
    {
        internal RestockPreviewGump() : base(0, 0)
        {
            X = 220; Y = 140; Width = 650; Height = 460;
            CanMove = true; CanCloseWithRightClick = true;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("Restock preview", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 18, Y = 16 });
            Add(new Label("Loaded container contents only. Stock is checked again when you execute.", true,
                CustomGumpThemeManager.DimHue, 614, font: 1) { X = 18, Y = 40 });
            var scroll = new ScrollArea(18, 74, 614, 330, true);
            Add(scroll);
            RestockAgentManager.RestockPlan plan = RestockAgentManager.Preview();
            int y = 0;
            if (plan.Error != null) plan.Lines.Add(plan.Error);
            foreach (string line in plan.Lines)
            {
                var label = new Label(line, true, CustomGumpThemeManager.TextHue, 590, font: 1) { Y = y };
                scroll.Add(label); y += label.Height + 8;
            }
            foreach (RestockAgentManager.PlannedMove move in plan.Moves)
            {
                var label = new Label($"Move {move.Amount} {move.Name}: item 0x{move.Item:X8} → container 0x{move.Target:X8}",
                    true, CustomGumpThemeManager.DimHue, 590, font: 1) { Y = y };
                scroll.Add(label); y += label.Height + 6;
            }
            var run = new NiceButton(18, 420, 170, 24, ButtonAction.Activate, "Execute restock") { IsSelectable = false,
                IsEnabled = plan.Error == null && plan.Moves.Count > 0 };
            CustomGumpThemeManager.StyleDataButton(run);
            run.MouseUp += (_, e) => { if (e.Button == ClassicUO.Input.MouseButtonType.Left) { RestockAgentManager.Run(); Dispose(); } };
            Add(run);
            SetInScreen();
        }
    }
}

using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ContainerSearchHelpGump : Gump
    {
        private readonly GridContainer _owner;
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly StbTextBox _query;
        private readonly Label _status;

        internal static void Open(GridContainer owner)
        {
            UIManager.GetGump<ContainerSearchHelpGump>()?.Dispose();
            UIManager.Add(new ContainerSearchHelpGump(owner));
        }

        private ContainerSearchHelpGump(GridContainer owner) : base(0, 0)
        {
            _owner = owner;
            int inset = CustomThemeArt.ContentInset + 12;
            Width = 460 + inset * 2; Height = 340 + inset * 2;
            X = owner.X + 30; Y = owner.Y + 30;
            CanMove = AcceptMouseInput = CanCloseWithRightClick = true; WantUpdateSize = false;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Text("CONTAINER SEARCH", inset, inset);
            Text("Build a query, then Apply to this container.", inset, inset + 28);
            var surface = new AlphaBlendControl(0.65f) { X = inset, Y = inset + 56, Width = 448, Height = 26 };
            CustomGumpThemeManager.ApplyInputSurface(surface, 0.65f, false); Add(surface);
            Add(_query = new StbTextBox(1, 256, 436, true, hue: CustomGumpThemeManager.DataTextHue)
                { X = inset + 6, Y = inset + 60, Width = 436, Height = 20, Multiline = false });
            string[] tokens = { "AND", "OR", "NOT", "(", ")", "\"phrase\"" };
            for (int i = 0; i < tokens.Length; i++) Button(inset + i * 75, inset + 94, 70, tokens[i], i + 1);
            Text("NOT first, then AND, then OR. Parentheses override this.\nPlain text matches a phrase; double quotes keep operators literal.", inset, inset + 130);
            Button(inset, inset + 184, 142, "Clean weapons", 10);
            Button(inset + 152, inset + 184, 142, "Slayer books", 11);
            Button(inset + 304, inset + 184, 144, "Reagents", 12);
            Text("Missing tooltip properties stay pending, not confirmed absent.\nPending items are skipped by both selection actions.\nHover container counts for hidden selections and loading details.", inset, inset + 222);
            Button(inset, inset + 290, 142, "Apply", 20);
            Button(inset + 152, inset + 290, 142, "Clear query", 21);
            Button(inset + 304, inset + 290, 144, "Close", 22);
            Add(_status = new Label("", true, CustomGumpThemeManager.DataTextHue, 448, font: 1)
                { X = inset, Y = inset + 320 });
            _query.TextChanged += (_, __) => Validate();
            _query.SetText(owner.SearchText); Validate(); SetInScreen();
        }

        private void Text(string text, int x, int y) => Add(new Label(text, true, CustomGumpThemeManager.DataTextHue, 448, font: 1) { X = x, Y = y });
        private void Button(int x, int y, int width, string text, int id)
        {
            var button = new NiceButton(x, y, width, 24, ButtonAction.Activate, text, font: 1)
                { ButtonParameter = id, IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button); Add(button);
        }
        private void Validate() => _status.Text = ContainerSearchQuery.Parse(_query.Text).Error ?? "Ready. Apply changes the filter; it does not select or move items.";

        public override void OnButtonClick(int id)
        {
            if (_owner.IsDisposed || _profile != ProfileManager.CurrentProfile) return;
            if (id >= 1 && id <= 6)
            {
                string[] tokens = { " AND ", " OR ", " NOT ", "(", ")", "\"phrase\"" };
                if (_query.Text.Length + tokens[id - 1].Length <= 256) _query.AppendText(tokens[id - 1]);
                else _status.Text = "Search is limited to 256 characters.";
                UIManager.KeyboardFocusControl = _query;
            }
            else switch (id)
            {
                case 10: _query.SetText("(bow OR sword) AND NOT cursed"); break;
                case 11: _query.SetText("spellbook AND slayer"); break;
                case 12: _query.SetText("black pearl OR mandrake"); break;
                case 20:
                    if (ContainerSearchQuery.Parse(_query.Text).IsValid) { _owner.SetSearchQuery(_query.Text); _status.Text = "Filter applied to container."; }
                    else Validate();
                    break;
                case 21: _query.ClearText(); break;
                case 22: Dispose(); break;
            }
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (_owner.IsDisposed || _profile != ProfileManager.CurrentProfile || !World.InGame) { Dispose(); return; }
            base.Update();
        }
    }
}

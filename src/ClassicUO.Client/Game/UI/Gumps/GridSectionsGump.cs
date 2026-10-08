using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class GridSectionsGump : Gump
    {
        private readonly Profile _profile;
        private readonly GridSectionsConfig[] _drafts;
        private readonly List<RuleRow> _rows = new List<RuleRow>();
        private Checkbox _enabled;
        private int _scope;
        private Label _status;
        private GridSectionsConfig Config => _drafts[_scope];

        internal GridSectionsGump(GridContainer owner, int scope) : base(0, 0)
        {
            _profile = ProfileManager.CurrentProfile; _scope = scope;
            _drafts = new[] { Clone(_profile.BackpackSections), Clone(_profile.CorpseSections), Clone(_profile.ContainerSections) };
            Width = 700; Height = 485;
            X = owner.X + 24; Y = owner.Y + 24;
            AcceptMouseInput = true;
            CanMove = true; CanCloseWithRightClick = true; CanCloseWithEsc = true;
            WantUpdateSize = false;
            Build(); SetInScreen();
        }

        private static GridSectionsConfig Clone(GridSectionsConfig config) => new GridSectionsConfig
        {
            Enabled = config.Enabled,
            Rules = config.Rules.Select(rule => new GridSectionRule { Name = rule.Name, Match = rule.Match,
                Value = rule.Value, Text = rule.Text, Hue = rule.Hue }).ToList()
        };

        private void Build()
        {
            Clear(); _rows.Clear();
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("CONTAINER SECTIONS", true, CustomGumpThemeManager.TitleHue, font: 1) { X = 18, Y = 16 });
            var scope = new Combobox(18, 48, 200, new[] { "Backpack", "Corpses", "Other containers" }, _scope) { X = 18, Y = 48 };
            scope.OnOptionSelected += (sender, e) =>
            {
                if (!ReadRows(out var rules)) return;
                Config.Rules = rules; Config.Enabled = _enabled.IsChecked;
                _scope = scope.SelectedIndex; Build();
            };
            Add(scope);
            Add(_enabled = new Checkbox(0x00D2, 0x00D3, "Enable for this category", 1, CustomGumpThemeManager.TextHue) { X = 236, Y = 50, IsChecked = Config.Enabled });
            Add(new Label("First matching rule wins. Unmatched items go in Other. Locked slot assignments stay intact.", true, CustomGumpThemeManager.TextHue, 660, font: 1) { X = 18, Y = 84 });
            Add(new Label("Name                  Match                  Layer / graphic / name text       Hue", true, CustomGumpThemeManager.TextHue, 660, font: 1) { X = 18, Y = 119 });
            var scroll = new ScrollArea(18, 143, 664, 260, true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            Add(scroll);
            foreach (GridSectionRule rule in Config.Rules)
            {
                var row = new RuleRow(rule, _rows.Count * 35);
                _rows.Add(row); scroll.Add(row);
            }
            AddButton(18, 416, 115, "Add rule", () =>
            {
                if (!ReadRows(out var rules)) return;
                if (rules.Count >= 24) { _status.Text = "Maximum 24 sections."; return; }
                Config.Rules = rules; Config.Enabled = _enabled.IsChecked;
                Config.Rules.Add(new GridSectionRule { Match = GridSectionMatch.Graphic }); Build();
            });
            AddButton(147, 416, 115, "Save", Save);
            AddButton(276, 416, 115, "Close", Dispose);
            Add(_status = new Label("Changes apply after Save. Layer numbers and graphics accept decimal or 0x hex.", true, CustomGumpThemeManager.TextHue, 660, font: 1) { X = 18, Y = 454 });
        }

        private void AddButton(int x, int y, int width, string text, Action action)
        {
            var button = new NiceButton(x, y, width, 28, ButtonAction.Activate, text) { IsSelectable = false };
            button.MouseUp += (sender, e) => { if (e.Button == ClassicUO.Input.MouseButtonType.Left) action(); };
            CustomGumpThemeManager.StyleDataButton(button); Add(button);
        }

        private bool ReadRows(out List<GridSectionRule> rules)
        {
            rules = new List<GridSectionRule>();
            foreach (RuleRow row in _rows.Where(row => !row.Deleted))
            {
                if (!row.TryRead(out GridSectionRule rule)) { _status.Text = "Enter a name, valid layer/graphic and hue (0-65535)."; return false; }
                rules.Add(rule);
            }
            return true;
        }

        private void Save()
        {
            if (!ReadRows(out var rules)) return;
            Config.Rules = rules; Config.Enabled = _enabled.IsChecked;
            _profile.BackpackSections = Clone(_drafts[0]);
            _profile.CorpseSections = Clone(_drafts[1]);
            _profile.ContainerSections = Clone(_drafts[2]);
            _profile.Save(ProfileManager.ProfilePath, false);
            foreach (GridContainer grid in UIManager.Gumps.OfType<GridContainer>()) grid.RefreshSections();
            _status.Text = "Saved. Section rules apply to this category.";
        }

        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            base.Update();
        }

        private sealed class RuleRow : Control
        {
            private readonly StbTextBox _name, _value, _hue;
            private readonly Combobox _match;
            internal bool Deleted;
            internal RuleRow(GridSectionRule rule, int y)
            {
                Y = y; Width = 640; Height = 32; WantUpdateSize = false;
                _name = Input(0, 155, rule.Name, 40);
                Add(_match = new Combobox(164, 0, 155, Enum.GetNames(typeof(GridSectionMatch)), (int)rule.Match) { X = 164 });
                _value = Input(329, 182, rule.Match == GridSectionMatch.NameContains ? rule.Text : rule.Value.ToString(), 80);
                _hue = Input(520, 65, "0x" + rule.Hue.ToString("X4"), 6);
                var remove = new NiceButton(595, 0, 28, 25, ButtonAction.Activate, "X") { IsSelectable = false };
                remove.MouseUp += (sender, e) => { if (e.Button == ClassicUO.Input.MouseButtonType.Left) { Deleted = true; IsVisible = false; } };
                Add(remove);
            }
            private StbTextBox Input(int x, int width, string text, int limit)
            {
                Add(new AlphaBlendControl(0.6f) { X = x, Width = width, Height = 27 });
                var input = new StbTextBox(1, limit, width - 8, true, hue: CustomGumpThemeManager.TextHue) { X = x + 4, Y = 4, Width = width - 8, Height = 22 };
                input.Text = text ?? string.Empty; Add(input); return input;
            }
            private static bool Number(string text, out int number) => text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number) : int.TryParse(text, out number);
            internal bool TryRead(out GridSectionRule rule)
            {
                rule = null;
                var match = (GridSectionMatch)_match.SelectedIndex;
                int value = 0;
                if (string.IsNullOrWhiteSpace(_name.Text) || !Number(_hue.Text, out int hue) || hue < 0 || hue > ushort.MaxValue ||
                    ((match == GridSectionMatch.Graphic || match == GridSectionMatch.EquipmentLayer) && (!Number(_value.Text, out value) || value < 0 || value > ushort.MaxValue))) return false;
                rule = new GridSectionRule { Name = _name.Text.Trim(), Match = match, Value = value, Text = _value.Text, Hue = (ushort)hue };
                return true;
            }
        }
    }
}

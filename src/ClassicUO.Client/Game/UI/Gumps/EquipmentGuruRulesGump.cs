// TazUO addition: locked equipment slots, excluded candidates and advanced goals.

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EquipmentGuruRulesGump : Gump
    {
        private const int WIDTH = 540;
        private const int HEIGHT = 480;
        private readonly VBoxContainer _rows;
        private readonly EquipmentGuruGump _owner;
        private readonly Label _excludedCount;
        private List<uint> _excludedSerials = new List<uint>();

        internal EquipmentGuruRulesGump(EquipmentGuruGump owner) : base(0, 0)
        {
            _owner = owner;
            X = owner?.X + 50 ?? 210;
            Y = owner?.Y + 50 ?? 130;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Add(FeatureGumpArtwork.CreateBackground(WIDTH, HEIGHT,
                FeatureGumpArtworkKind.EquipmentGuru, 0.97f));
            Add(new Label("EQUIPMENT GURU  •  RULES", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru), 430, font: 1)
            { X = 24, Y = 18 });
            Add(new Label("Lock equipped slots or exclude catalog items from all recommendations.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), 460, font: 1)
            { X = 24, Y = 40 });
            Add(Button(488, 16, 28, 28, "X", 99));
            Add(FeatureGumpArtwork.CreateSurface(24, 72, 492, 330,
                FeatureGumpArtworkKind.EquipmentGuru, 0.42f));
            var scroll = new ScrollArea(28, 76, 484, 322, true)
            { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            scroll.Add(_rows = new VBoxContainer(scroll.Width - scroll.ScrollBarWidth() - 3, 0, 2));
            Add(scroll);
            Add(Button(24, 420, 150, 30, "Clear exclusions", 1));
            Add(_excludedCount = new Label(string.Empty, true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), 300, font: 1)
            { X = 190, Y = 427 });
            Rebuild();
            SetInScreen();
        }

        public override bool ShouldBeSaved => false;

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99) { Dispose(); return; }
            if (buttonID == 1) EquipmentGuruManager.ClearExclusions();
            else if (buttonID >= 2000)
            {
                int index = buttonID - 2000;
                if (index >= 0 && index < _excludedSerials.Count)
                    EquipmentGuruManager.ToggleExcluded(_excludedSerials[index]);
            }
            else if (buttonID >= 1000)
                EquipmentGuruManager.ToggleLayerLock((Layer)(buttonID - 1000));
            Rebuild();
            _owner?.RulesChanged();
        }

        private void Rebuild()
        {
            _rows.Clear();
            _excludedCount.Text = $"Excluded items: {EquipmentGuruManager.ExcludedItems.Count}";
            if (World.Player == null) return;
            for (var node = World.Player.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed
                    || !EquipmentGuruManager.CanLockLayer(item.Layer)) continue;
                bool locked = EquipmentGuruManager.IsLayerLocked(item.Layer);
                _rows.Add(new RuleRow(item, locked, _rows.Width - 4));
            }

            _excludedSerials = EquipmentGuruManager.ExcludedItems.OrderBy(serial => serial).ToList();
            if (_excludedSerials.Count > 0)
            {
                _rows.Add(new Label("EXCLUDED CANDIDATES", true,
                    FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru),
                    _rows.Width - 8, font: 1) { X = 8, Y = 8 });
                for (int i = 0; i < _excludedSerials.Count; i++)
                    _rows.Add(new ExcludedRow(_excludedSerials[i], i, _rows.Width - 4));
            }
        }

        private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
            FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.EquipmentGuru);

        private sealed class RuleRow : Control
        {
            internal RuleRow(Item item, bool locked, int width)
            {
                Width = width;
                Height = 48;
                Add(FeatureGumpArtwork.CreateSurface(0, 0, width, 46,
                    FeatureGumpArtworkKind.EquipmentGuru, 0.36f));
                string name = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.ItemData.Name;
                Add(new Label($"{item.Layer}: {name}", true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru), width - 122,
                    font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                { X = 8, Y = 13 });
                Add(Button(width - 112, 9, 104, 28, locked ? "UNLOCK" : "LOCK", 1000 + (int)item.Layer));
            }

            private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
                FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                    FeatureGumpArtworkKind.EquipmentGuru);
        }

        private sealed class ExcludedRow : Control
        {
            internal ExcludedRow(uint serial, int index, int width)
            {
                Width = width;
                Height = 44;
                Add(FeatureGumpArtwork.CreateSurface(0, 0, width, 42,
                    FeatureGumpArtworkKind.EquipmentGuru, 0.30f));
                Add(new Label(ItemFinderManager.CatalogItemName(serial), true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru), width - 126,
                    font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                { X = 8, Y = 12 });
                Add(Button(width - 116, 7, 108, 28, "ALLOW", 2000 + index));
            }

            private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
                FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                    FeatureGumpArtworkKind.EquipmentGuru);
        }
    }

    internal sealed class EquipmentGuruAdvancedGoalsGump : Gump
    {
        private readonly EquipmentGuruManager.EquipmentGuruBuild _build;
        private readonly Dictionary<string, StbTextBox> _inputs = new Dictionary<string, StbTextBox>();
        private readonly Dictionary<string, Checkbox> _mustInputs = new Dictionary<string, Checkbox>();
        private readonly Dictionary<string, Checkbox> _preserveInputs = new Dictionary<string, Checkbox>();
        private readonly Checkbox _preferInsured;
        private readonly Checkbox _preferDurability;

        internal EquipmentGuruAdvancedGoalsGump(EquipmentGuruManager.EquipmentGuruBuild build) : base(0, 0)
        {
            _build = build == EquipmentGuruManager.EquipmentGuruBuild.Auto
                ? EquipmentGuruManager.DetectBuild() : build;
            X = 260; Y = 170; Width = 430; Height = 350;
            CanMove = true; CanCloseWithRightClick = true; AcceptMouseInput = true; WantUpdateSize = false;
            Add(FeatureGumpArtwork.CreateBackground(Width, Height,
                FeatureGumpArtworkKind.EquipmentGuru, 0.97f));
            Add(new Label("ADVANCED GOALS", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.EquipmentGuru), 330, font: 1)
            { X = 24, Y = 18 });
            Add(Button(378, 16, 28, 28, "X", 99));
            EquipmentGuruManager.EquipmentGuruGoals goals = EquipmentGuruManager.GetGoals(_build);
            AddRow("Faster Casting", "FC", "FasterCasting", goals.FasterCasting, goals, 76);
            AddRow("Faster Cast Recovery", "FCR", "FasterCastRecovery", goals.FasterCastRecovery, goals, 116);
            AddRow("Luck", "Luck", "Luck", goals.Luck, goals, 156);
            Add(_preferInsured = new Checkbox(0x00D2, 0x00D3,
                "Prefer insured items when scores are equal", 1,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru))
            { X = 24, Y = 202, IsChecked = goals.PreferInsured });
            Add(_preferDurability = new Checkbox(0x00D2, 0x00D3,
                "Prefer higher durability when scores are equal", 1,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru))
            { X = 24, Y = 230, IsChecked = goals.PreferDurability });
            Add(new Label("MIN enforces the target. KEEP preserves the current value. Zero disables a goal.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.EquipmentGuru), 380, font: 1)
            { X = 24, Y = 264 });
            Add(Button(264, 300, 142, 30, "Save goals", 1));
            SetInScreen();
        }

        public override bool ShouldBeSaved => false;
        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99) { Dispose(); return; }
            if (buttonID != 1) return;
            EquipmentGuruManager.EquipmentGuruGoals goals = EquipmentGuruManager.GetGoals(_build);
            goals.FasterCasting = Value("FC"); goals.FasterCastRecovery = Value("FCR"); goals.Luck = Value("Luck");
            SetRequirement(goals, "FasterCasting", _mustInputs["FasterCasting"].IsChecked,
                _preserveInputs["FasterCasting"].IsChecked);
            SetRequirement(goals, "FasterCastRecovery", _mustInputs["FasterCastRecovery"].IsChecked,
                _preserveInputs["FasterCastRecovery"].IsChecked);
            SetRequirement(goals, "Luck", _mustInputs["Luck"].IsChecked,
                _preserveInputs["Luck"].IsChecked);
            goals.PreferInsured = _preferInsured.IsChecked;
            goals.PreferDurability = _preferDurability.IsChecked;
            EquipmentGuruManager.SaveGoals(_build, goals);
            Dispose();
        }

        private int Value(string key) => int.TryParse(_inputs[key].Text, out int value) ? Math.Max(0, value) : 0;
        private void AddRow(string label, string key, string mustKey, int value,
            EquipmentGuruManager.EquipmentGuruGoals goals, int y)
        {
            Add(new Label(label, true, FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru), 140, font: 1)
            { X = 24, Y = y + 6 });
            var must = new Checkbox(0x00D2, 0x00D3, "MIN", 1,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru))
            {
                X = 166,
                Y = y + 6,
                IsChecked = goals.MustTargets.Contains(mustKey)
                    && !goals.PreserveTargets.Contains(mustKey)
            };
            must.SetTooltip("Minimum: reject a loadout below the entered target.");
            _mustInputs[mustKey] = must;
            Add(must);
            var preserve = new Checkbox(0x00D2, 0x00D3, "KEEP", 1,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.EquipmentGuru))
            { X = 220, Y = y + 6, IsChecked = goals.PreserveTargets.Contains(mustKey) };
            preserve.SetTooltip("Keep: reject a loadout below your current value.");
            _preserveInputs[mustKey] = preserve;
            Add(preserve);
            must.ValueChanged += (sender, e) =>
            {
                if (must.IsChecked) preserve.IsChecked = false;
            };
            preserve.ValueChanged += (sender, e) =>
            {
                if (preserve.IsChecked) must.IsChecked = false;
            };
            Add(FeatureGumpArtwork.CreateSurface(286, y, 120, 30,
                FeatureGumpArtworkKind.EquipmentGuru, 0.68f, true));
            var input = new StbTextBox(1, 9999, 112, true,
                hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.EquipmentGuru))
            { X = 294, Y = y + 5, Width = 102, Height = 20, NumbersOnly = true };
            input.SetText(value.ToString()); _inputs[key] = input; Add(input);
        }

        private static void SetRequirement(EquipmentGuruManager.EquipmentGuruGoals goals,
            string key, bool minimum, bool preserve)
        {
            if (minimum) goals.MustTargets.Add(key);
            else goals.MustTargets.Remove(key);
            if (preserve) goals.PreserveTargets.Add(key);
            else goals.PreserveTargets.Remove(key);
        }

        private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
            FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.EquipmentGuru);
    }
}

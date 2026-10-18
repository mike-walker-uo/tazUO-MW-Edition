using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EquipmentSwitcherLegendGump : Gump
    {
        private readonly PaperDollBackpackEquipmentGump _bar;
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly ScrollArea _rows;

        internal static void Open(PaperDollBackpackEquipmentGump bar)
        {
            UIManager.GetGump<EquipmentSwitcherLegendGump>()?.Dispose();
            UIManager.Add(new EquipmentSwitcherLegendGump(bar));
        }

        private EquipmentSwitcherLegendGump(PaperDollBackpackEquipmentGump bar) : base(0, 0)
        {
            _bar = bar;
            int inset = CustomThemeArt.ContentInset + 12;
            Width = 420 + inset * 2; Height = 370 + inset * 2;
            X = bar.X + 30; Y = bar.Y + 30;
            CanMove = AcceptMouseInput = CanCloseWithRightClick = true; WantUpdateSize = false;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("SLAYER COLOR LEGEND", true, CustomGumpThemeManager.TitleHue, font: 1) { X = inset, Y = inset });
            Add(new Label("Unconfigured slayers use the default background.\nFor multiple slayers, the first configured tooltip line wins.\nHover a cell's small corner marker to see its applied rule.",
                true, CustomGumpThemeManager.DataTextHue, 408, font: 1) { X = inset, Y = inset + 30 });
            Add(_rows = new ScrollArea(inset, inset + 104, 408, 220, true));
            var options = new NiceButton(inset, inset + 336, 196, 24, ButtonAction.Activate, "Edit hues / options", font: 1)
                { ButtonParameter = 1, IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(options); Add(options);
            var close = new NiceButton(inset + 208, inset + 336, 196, 24, ButtonAction.Activate, "Close", font: 1)
                { ButtonParameter = 2, IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(close); Add(close);
            Refresh(); SetInScreen();
        }

        internal void Refresh()
        {
            _rows.Clear(); _rows.Children.RemoveAll(c => c.IsDisposed);
            var rules = new List<KeyValuePair<string, ushort>> { new KeyValuePair<string, ushort>("Default / unmatched equipment", _profile.SlayerBarDefaultHue) };
            if (_profile.SlayerBarSlayerHues != null) rules.AddRange(_profile.SlayerBarSlayerHues.Keys
                .Select(PaperDollBackpackEquipmentGump.CanonicalSlayer).Distinct(System.StringComparer.OrdinalIgnoreCase).OrderBy(n => n)
                .Select(n => new KeyValuePair<string, ushort>(n, PaperDollBackpackEquipmentGump.BackgroundHue(_profile, n))));
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var swatch = new AlphaBlendControl(0.85f) { X = 4, Y = i * 30 + 3, Width = 24, Height = 20 };
                if (rule.Value == 0) CustomGumpThemeManager.ApplyDataSurface(swatch, 0.85f, false);
                else { swatch.BaseColor = Color.White; swatch.Hue = rule.Value; }
                _rows.Add(swatch);
                string name = string.Equals(rule.Key, "Undead Slayer", System.StringComparison.OrdinalIgnoreCase) ? "Undead Slayer / Silver" : rule.Key;
                var label = new Label(name + " / " + (rule.Value == 0 ? "theme" : "0x" + rule.Value.ToString("X4")),
                    true, CustomGumpThemeManager.DataTextHue, 350, font: 1, style: FontStyle.Cropped)
                    { X = 38, Y = i * 30 + 4, AcceptMouseInput = true };
                label.SetTooltip(name + "\n" + (rule.Value == 0 ? "Theme background; no hue applied." : "Background hue 0x" + rule.Value.ToString("X4"))
                    + "\nOnly cell backgrounds change; item hues stay unchanged.");
                _rows.Add(label);
            }
        }

        public override void OnButtonClick(int id)
        {
            if (_profile != ProfileManager.CurrentProfile || _bar.IsDisposed) return;
            if (id == 1) EquipmentSwitcherOptionsGump.Open(_bar);
            else if (id == 2) Dispose();
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile || _bar.IsDisposed) { Dispose(); return; }
            base.Update();
        }
    }
}

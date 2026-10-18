using System;
using System.Globalization;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EquipmentSwitcherOptionsGump : Gump
    {
        private readonly PaperDollBackpackEquipmentGump _bar;
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly string[] _slayers;
        private readonly Combobox _slayer;
        private readonly StbTextBox _hue;
        private readonly AlphaBlendControl _preview;
        private readonly Label _status;
        private readonly NiceButton _dock, _rotate, _lock;
        private bool _lastDetached, _lastHorizontal, _lastLocked;
        private string SelectedSlayer => _slayer.SelectedIndex > 0
            ? PaperDollBackpackEquipmentGump.CanonicalSlayer(_slayers[_slayer.SelectedIndex - 1]) : null;

        internal static void Open(PaperDollBackpackEquipmentGump bar)
        {
            UIManager.GetGump<EquipmentSwitcherOptionsGump>()?.Dispose();
            UIManager.Add(new EquipmentSwitcherOptionsGump(bar));
        }

        private EquipmentSwitcherOptionsGump(PaperDollBackpackEquipmentGump bar) : base(0, 0)
        {
            _bar = bar;
            int inset = CustomThemeArt.ContentInset + 12;
            Width = 380 + inset * 2; Height = 346 + inset * 2;
            X = bar.X + 30; Y = bar.Y + 30;
            CanMove = true; AcceptMouseInput = true; CanCloseWithRightClick = true; WantUpdateSize = false;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("WEAPON SWITCHER", true, CustomGumpThemeManager.TitleHue, font: 1) { X = inset, Y = inset });
            _dock = Button(inset, inset + 30, 150, "", 1);
            _rotate = Button(inset + 164, inset + 30, 200, "", 2);
            _lock = Button(inset, inset + 64, 176, "", 7);
            _lock.SetTooltip("Lock the detached position. Equipment use and right-click closing remain available.");
            Button(inset + 188, inset + 64, 176, "Slayer color legend", 8);
            Add(new Label("Background hue for slayer / equipment:", true, CustomGumpThemeManager.DataTextHue, 364, font: 1) { X = inset, Y = inset + 109 });
            _slayers = PaperDollBackpackEquipmentGump.AvailableSlayers();
            string[] choices = new[] { "Default / unmatched equipment" }.Concat(_slayers).ToArray();
            Add(_slayer = new Combobox(inset, inset + 135, 364, choices, 0, maxHeight: 180));
            _slayer.SetTooltip("Silver Slayer and Undead Slayer share one background hue. Editing or resetting either applies to both.");
            _slayer.OnOptionSelected += (_, __) => LoadHue();
            Add(new Label("Hue (decimal or 0x hex):", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = inset, Y = inset + 173 });
            var inputBackground = new AlphaBlendControl(0.65f) { X = inset, Y = inset + 195, Width = 104, Height = 26 };
            CustomGumpThemeManager.ApplyInputSurface(inputBackground, 0.65f, false); Add(inputBackground);
            Add(_hue = new StbTextBox(1, 7, 96, true, hue: CustomGumpThemeManager.DataTextHue)
                { X = inset + 4, Y = inset + 198, Width = 96, Height = 22, Multiline = false });
            Add(_preview = new AlphaBlendControl(0.85f) { X = inset + 118, Y = inset + 195, Width = 48, Height = 26 });
            _hue.TextChanged += (_, __) => UpdatePreview();
            Button(inset + 180, inset + 195, 184, "Pick hue...", 3);
            Add(new Label("Applies to weapons, shields, talismans and spellbooks.\nFirst configured slayer in the tooltip wins. Hue 0 uses the theme.",
                true, CustomGumpThemeManager.DataTextHue, 364, font: 1) { X = inset, Y = inset + 237 });
            Button(inset, inset + 289, 116, "Apply hue", 4);
            Button(inset + 124, inset + 289, 116, "Reset selected", 5);
            Button(inset + 248, inset + 289, 116, "Close", 6);
            Add(_status = new Label("Hue changes apply after Apply hue.", true, CustomGumpThemeManager.DataTextHue, 364, font: 1)
                { X = inset, Y = inset + 322 });
            RefreshLayoutButtons(); LoadHue(); SetInScreen();
        }

        private NiceButton Button(int x, int y, int width, string text, int id)
        {
            var button = new NiceButton(x, y, width, 24, ButtonAction.Activate, text, font: 1)
                { ButtonParameter = id, IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button); Add(button); return button;
        }

        private void RefreshLayoutButtons()
        {
            _lastDetached = _profile.SlayerBarDetached; _lastHorizontal = _profile.SlayerBarHorizontal; _lastLocked = _profile.SlayerBarLocked;
            _lock.SetText(_lastLocked ? "Position locked" : "Position unlocked");
            _dock.SetText(_lastDetached ? "Attach to paperdoll" : "Detach / move freely");
            _rotate.SetText(_lastHorizontal ? "Layout: horizontal (rotate)" : "Layout: vertical (rotate)");
        }

        private void LoadHue()
        {
            ushort hue = SelectedSlayer == null ? _profile.SlayerBarDefaultHue
                : PaperDollBackpackEquipmentGump.BackgroundHue(_profile, SelectedSlayer);
            _hue.SetText("0x" + hue.ToString("X4"));
            UpdatePreview();
        }

        internal static bool ParseHue(string text, int maximum, out ushort hue)
        {
            text = (text ?? "").Trim();
            bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (ushort.TryParse(hex ? text.Substring(2) : text, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
                CultureInfo.InvariantCulture, out hue) && hue <= maximum) return true;
            hue = 0; return false;
        }

        private void UpdatePreview()
        {
            if (!ParseHue(_hue.Text, HuesLoader.Instance.HuesCount, out ushort hue)) return;
            _preview.BaseColor = hue == 0 ? Color.DarkGray : Color.White;
            _preview.Hue = hue;
        }

        public override void OnButtonClick(int buttonID)
        {
            if (_profile != ProfileManager.CurrentProfile || _bar.IsDisposed) return;
            switch (buttonID)
            {
                case 1: _bar.OnButtonClick(3); RefreshLayoutButtons(); break;
                case 2: _bar.OnButtonClick(4); RefreshLayoutButtons(); break;
                case 3:
                    UIManager.Add(new ModernColorPicker(hue =>
                    {
                        if (!IsDisposed && _profile == ProfileManager.CurrentProfile) _hue.SetText("0x" + hue.ToString("X4"));
                    }));
                    break;
                case 4:
                    if (!ParseHue(_hue.Text, HuesLoader.Instance.HuesCount, out ushort selected))
                    { _status.Text = "Enter a hue from 0 to " + HuesLoader.Instance.HuesCount + "."; return; }
                    StoreHue(selected); break;
                case 5: StoreHue(null); LoadHue(); break;
                case 6: Dispose(); break;
                case 7: _bar.OnButtonClick(6); RefreshLayoutButtons(); break;
                case 8: EquipmentSwitcherLegendGump.Open(_bar); break;
            }
        }

        private void StoreHue(ushort? hue)
        {
            string slayer = SelectedSlayer;
            if (slayer == null) _profile.SlayerBarDefaultHue = hue ?? 0;
            else
            {
                _profile.SlayerBarSlayerHues ??= new System.Collections.Generic.Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
                foreach (string existing in _profile.SlayerBarSlayerHues.Keys.Where(k => string.Equals(
                    PaperDollBackpackEquipmentGump.CanonicalSlayer(k), slayer, StringComparison.OrdinalIgnoreCase)).ToArray())
                    _profile.SlayerBarSlayerHues.Remove(existing);
                if (hue.HasValue) _profile.SlayerBarSlayerHues[slayer] = hue.Value;
            }
            _profile.Save(ProfileManager.ProfilePath, false, true);
            PaperDollBackpackEquipmentGump.UpdateAllOptions();
            _status.Text = hue.HasValue ? "Background hue saved." : "Selected override reset.";
        }

        public override void Update()
        {
            if (!World.InGame || _profile != ProfileManager.CurrentProfile || _bar.IsDisposed) { Dispose(); return; }
            if (_lastDetached != _profile.SlayerBarDetached || _lastHorizontal != _profile.SlayerBarHorizontal || _lastLocked != _profile.SlayerBarLocked) RefreshLayoutButtons();
            base.Update();
        }
    }
}

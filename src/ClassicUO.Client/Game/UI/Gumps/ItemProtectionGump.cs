using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ItemProtectionGump : Gump
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private readonly DataBox _rows = new DataBox(0, 0, 0, 0);
        private readonly List<ProtectedItemRecord> _visible = new List<ProtectedItemRecord>();
        private readonly Label _pageLabel;
        private readonly AlphaBlendControl _colorPreview;
        private ushort _previewHue;
        private int _page;
        private const int PAGE_SIZE = 8;

        internal static void Open()
        {
            if (!World.InGame) return;
            var current = UIManager.GetGump<ItemProtectionGump>();
            if (current != null) { current.Refresh(); current.BringOnTop(); return; }
            UIManager.Add(new ItemProtectionGump());
        }
        internal static void RefreshOpen() => UIManager.GetGump<ItemProtectionGump>()?.Refresh();

        private ItemProtectionGump() : base(0, 0)
        {
            int inset = CustomThemeArt.ContentInset + 12;
            Width = 580 + inset * 2; Height = 526 + inset * 2;
            CanMove = AcceptMouseInput = CanCloseWithRightClick = true; WantUpdateSize = false;
            Add(CustomGumpThemeManager.CreateBackground(Width, Height, 0.95f));
            Add(new Label("PROTECTED ITEMS", true, CustomGumpThemeManager.TitleHue, font: 1) { X = inset, Y = inset });
            Add(new Label("Protected items and bags containing them cannot be moved normally.\nThe weapon switcher can still swap equipment within your character.\nColored padlocks mark protection. Use or open items normally.",
                true, CustomGumpThemeManager.DataTextHue, 560, font: 1) { X = inset, Y = inset + 30 });
            Button(inset, inset + 98, 176, "Protect: target item", 1);
            Button(inset + 188, inset + 98, 176, "Unprotect: target item", 2);
            Button(inset + 376, inset + 98, 176, "Refresh", 3);
            Add(new Label("Marker color:", true, CustomGumpThemeManager.DataTextHue, font: 1) { X = inset, Y = inset + 138 });
            Add(_colorPreview = new AlphaBlendControl(0.95f) { X = inset + 128, Y = inset + 134, Width = 24, Height = 24 });
            Button(inset + 164, inset + 134, 188, "Pick color...", 7);
            Button(inset + 364, inset + 134, 188, "Reset to gold", 8);
            Add(_rows); _rows.X = inset; _rows.Y = inset + 174; _rows.Width = 560; _rows.Height = 288;
            Button(inset, inset + 476, 104, "Previous", 4);
            Button(inset + 116, inset + 476, 104, "Next", 5);
            Button(inset + 448, inset + 476, 104, "Close", 6);
            Add(_pageLabel = new Label("", true, CustomGumpThemeManager.DataTextHue, 210, font: 1)
                { X = inset + 234, Y = inset + 480 });
            Refresh(); CenterXInScreen(); CenterYInScreen(); SetInScreen();
        }

        private void Button(int x, int y, int width, string text, int id, Control parent = null)
        {
            var button = new NiceButton(x, y, width, 24, ButtonAction.Activate, text, font: 1)
                { ButtonParameter = id, IsSelectable = false };
            CustomGumpThemeManager.StyleDataButton(button); (parent ?? this).Add(button);
        }

        private void Refresh()
        {
            if (_profile != ProfileManager.CurrentProfile) { Dispose(); return; }
            UpdateColorPreview();
            _rows.Clear(); _rows.Children.RemoveAll(c => c.IsDisposed); _visible.Clear();
            var records = ItemProtectionManager.Records.OrderBy(r => r.Name).ThenBy(r => r.Serial).ToList();
            int pages = System.Math.Max(1, (records.Count + PAGE_SIZE - 1) / PAGE_SIZE);
            _page = System.Math.Min(_page, pages - 1);
            _visible.AddRange(records.Skip(_page * PAGE_SIZE).Take(PAGE_SIZE));
            for (int i = 0; i < _visible.Count; i++)
            {
                var record = _visible[i];
                var item = World.Items.Get(record.Serial);
                string state = item == null || item.IsDestroyed ? "not currently visible" : ItemProtectionManager.Owned(item)
                    ? item.Container == World.Player.Serial ? "equipped" : "backpack" : "elsewhere";
                var name = new Label(string.IsNullOrWhiteSpace(record.Name) ? "Item" : record.Name, true,
                    CustomGumpThemeManager.DataTextHue, 312, font: 1, style: FontStyle.Cropped)
                    { X = 0, Y = i * 36, AcceptMouseInput = true };
                name.SetTooltip(record.Name + $"\n0x{record.Serial:X8} / {state}\nProtection stays saved when an item is not visible.");
                _rows.Add(name);
                _rows.Add(new Label(state, true, CustomGumpThemeManager.DimHue, 130, font: 1, style: FontStyle.Cropped)
                    { X = 316, Y = i * 36 });
                Button(452, i * 36, 100, "Unprotect", 100 + i, _rows);
            }
            if (records.Count == 0) _rows.Add(new Label("No protected items. Use Protect, then target your equipment or a backpack item.",
                true, CustomGumpThemeManager.DataTextHue, 550, font: 1));
            _pageLabel.Text = $"{records.Count} items / page {_page + 1} of {pages}";
        }

        private void UpdateColorPreview()
        {
            _previewHue = _profile.ProtectedItemHue;
            _colorPreview.BaseColor = _previewHue == 0 ? Color.Gold : Color.White;
            _colorPreview.Hue = _previewHue;
        }

        private void SetMarkerHue(ushort hue)
        {
            if (IsDisposed || _profile != ProfileManager.CurrentProfile || !World.InGame) return;
            _profile.ProtectedItemHue = hue;
            _profile.Save(ProfileManager.ProfilePath, false, true);
            UpdateColorPreview();
        }

        public override void OnButtonClick(int id)
        {
            if (_profile != ProfileManager.CurrentProfile || !World.InGame) return;
            if (id >= 100 && id - 100 < _visible.Count) { ItemProtectionManager.Remove(_visible[id - 100].Serial); return; }
            switch (id)
            {
                case 1: ItemProtectionManager.Target(true); break;
                case 2: ItemProtectionManager.Target(false); break;
                case 3: Refresh(); break;
                case 4: _page = System.Math.Max(0, _page - 1); Refresh(); break;
                case 5: _page++; Refresh(); break;
                case 6: Dispose(); break;
                case 7: UIManager.Add(new ModernColorPicker(SetMarkerHue)); break;
                case 8: SetMarkerHue(0); break;
            }
        }
        public override bool ShouldBeSaved => false;
        public override void Update()
        {
            if (_profile != ProfileManager.CurrentProfile || !World.InGame) { Dispose(); return; }
            if (_previewHue != _profile.ProtectedItemHue) UpdateColorPreview();
            base.Update();
        }
    }
}

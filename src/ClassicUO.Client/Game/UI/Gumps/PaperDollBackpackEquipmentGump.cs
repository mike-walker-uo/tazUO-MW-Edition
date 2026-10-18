// Backpack equipment switcher, docked to the paperdoll or placed independently.
using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PaperDollBackpackEquipmentGump : Gump
    {
        private const int CELL = 30, HEADER_HEIGHT = 18, MAX_ROWS = 9;
        private const int PADDING = 5, CATEGORY_GAP = 4, OWNER_GAP = 3, TOOLBAR_HEIGHT = 44;
        private const uint SCAN_INTERVAL = 500;
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private PaperDollGump _owner;
        private ThemedGumpBackground _background;
        private readonly DataBox _contents = new DataBox(0, 0, 0, 0);
        private readonly StbTextBox _search;
        private readonly AlphaBlendControl _searchBackground;
        private readonly Label _dragHandle;
        private readonly NiceButton _collapse, _clearSearch;
        private readonly SwitcherIconButton _options, _detach, _rotate, _lock, _legend;
        private uint _nextScan;
        private int _contentSignature = int.MinValue;
        private uint _displacedShieldSerial;
        private bool _attachPending;
        internal bool Detached => _profile.SlayerBarDetached;
        internal static bool CanMoveDetached(Profile profile) => profile.SlayerBarDetached && !profile.SlayerBarLocked;

        public PaperDollBackpackEquipmentGump(PaperDollGump owner) : base(0, 0)
        {
            _owner = owner;
            CanBeLocked = false;
            WantUpdateSize = false;
            _collapse = ToolbarButton(4, "-", 1, "Collapse / expand weapon switcher");
            _options = IconButton(24, SwitcherIcon.Options, 2, "Options: slayer background hues, docking and orientation");
            _detach = IconButton(44, SwitcherIcon.Dock, 3, "Detach / attach to paperdoll");
            _rotate = IconButton(64, SwitcherIcon.Rotate, 4, "Rotate between vertical and horizontal layout");
            _lock = IconButton(84, SwitcherIcon.Lock, 6, "Lock / unlock detached position");
            _legend = IconButton(104, SwitcherIcon.Legend, 7, "Slayer color legend");
            _dragHandle = new Label("::", true, CustomGumpThemeManager.DataTextHue, 18, font: 1)
                { X = 126, Y = 3, CanMove = true, AcceptMouseInput = true };
            _dragHandle.SetTooltip("Drag to move the detached weapon switcher");
            Add(_dragHandle);
            _searchBackground = new AlphaBlendControl(0.65f) { X = PADDING, Y = 24, Height = 18 };
            CustomGumpThemeManager.ApplyInputSurface(_searchBackground, 0.65f, false);
            Add(_searchBackground);
            _search = new StbTextBox(1, 64, 80, true, hue: CustomGumpThemeManager.DataTextHue)
                { X = PADDING + 3, Y = 24, Width = 80, Height = 18, Multiline = false };
            _search.SetTooltip("Search equipment by name, slayer or tooltip property");
            _search.TextChanged += (_, __) => BuildContents();
            Add(_search);
            _clearSearch = ToolbarButton(0, "X", 5, "Clear equipment search");
            _clearSearch.Y = 24;
            Add(_contents);
            if (Detached) Location = _profile.SlayerBarPosition ?? new Point(100, 100);
            BuildContents();
            if (Detached) SetInScreen();
        }

        internal static PaperDollBackpackEquipmentGump EnsureFor(PaperDollGump owner)
        {
            var bar = UIManager.GetGump<PaperDollBackpackEquipmentGump>();
            if (bar == null)
            {
                bar = new PaperDollBackpackEquipmentGump(owner);
                UIManager.Add(bar);
            }
            bar._owner = owner;
            return bar;
        }

        internal static void RestoreDetached()
        {
            if (ProfileManager.CurrentProfile?.SlayerBarDetached == true && UIManager.GetGump<PaperDollBackpackEquipmentGump>() == null)
                UIManager.Add(new PaperDollBackpackEquipmentGump(null));
        }

        internal static void Open()
        {
            if (!World.InGame) return;
            var bar = UIManager.GetGump<PaperDollBackpackEquipmentGump>();
            if (bar != null) { bar.BringOnTop(); return; }
            if (ProfileManager.CurrentProfile.SlayerBarDetached) { RestoreDetached(); return; }
            var owner = UIManager.GetGump<PaperDollGump>(World.Player.Serial);
            if (owner != null) EnsureFor(owner);
            else GameActions.OpenPaperdoll(World.Player.Serial);
        }

        internal void ReleaseOwner(PaperDollGump owner)
        {
            if (_owner != owner) return;
            _owner = null;
            if (!Detached) Dispose();
        }

        public override bool ShouldBeSaved => false;

        public override void Update()
        {
            if (World.Player == null || _profile != ProfileManager.CurrentProfile)
            { Dispose(); return; }
            if (_attachPending)
            {
                var owner = UIManager.GetGump<PaperDollGump>(World.Player.Serial);
                if (owner != null) { _owner = owner; SetDetached(false); }
            }
            if (!Detached && (_owner == null || _owner.IsDisposed)) { Dispose(); return; }
            IsVisible = Detached || _owner.IsVisible && !_owner.IsMinimized;
            if (!IsVisible) return;
            if (Time.Ticks >= _nextScan)
            {
                _nextScan = Time.Ticks + SCAN_INTERVAL;
                if (GetContentSignature() != _contentSignature) BuildContents();
            }
            UpdatePosition();
            base.Update();
        }

        private void UpdatePosition()
        {
            AcceptMouseInput = CanCloseWithRightClick = Detached;
            CanMove = CanMoveDetached(_profile);
            _dragHandle.CanMove = CanMove;
            _dragHandle.Alpha = _profile.SlayerBarLocked ? 0.5f : 1f;
            _dragHandle.SetTooltip(_profile.SlayerBarLocked ? "Position locked. Use the padlock to unlock." : "Drag to move the detached weapon switcher");
            _dragHandle.IsVisible = Detached && !_profile.SlayerBarCollapsed;
            if (Detached || _owner == null) return;
            X = _owner.X - Width - OWNER_GAP;
            Y = _owner.Y + Math.Max(0, (_owner.Height - Height) / 2);
        }

        private void SaveOptions()
        {
            if (_profile == ProfileManager.CurrentProfile) _profile.Save(ProfileManager.ProfilePath, false, true);
        }

        protected override void OnDragEnd(int x, int y)
        {
            base.OnDragEnd(x, y);
            if (Detached && _profile == ProfileManager.CurrentProfile)
            { SetInScreen(); _profile.SlayerBarPosition = Location; SaveOptions(); }
        }

        private void SetDetached(bool detached)
        {
            if (Detached) _profile.SlayerBarPosition = Location;
            _profile.SlayerBarDetached = detached;
            _attachPending = false;
            if (detached) Location = _profile.SlayerBarPosition ?? Location;
            BuildContents();
            if (detached) { SetInScreen(); _profile.SlayerBarPosition = Location; }
            SaveOptions();
        }

        public override void OnButtonClick(int buttonID)
        {
            if (_profile != ProfileManager.CurrentProfile) return;
            switch (buttonID)
            {
                case 1: _profile.SlayerBarCollapsed = !_profile.SlayerBarCollapsed; BuildContents(); SaveOptions(); break;
                case 2: EquipmentSwitcherOptionsGump.Open(this); break;
                case 3:
                    if (!Detached) SetDetached(true);
                    else
                    {
                        _owner = UIManager.GetGump<PaperDollGump>(World.Player.Serial);
                        if (_owner != null) SetDetached(false);
                        else
                        {
                            _attachPending = true;
                            GameActions.OpenPaperdoll(World.Player.Serial);
                            GameActions.Print("Opening your paperdoll to attach the weapon switcher.", 0x35);
                        }
                    }
                    break;
                case 4:
                    _profile.SlayerBarHorizontal = !_profile.SlayerBarHorizontal;
                    BuildContents();
                    if (Detached) { SetInScreen(); _profile.SlayerBarPosition = Location; }
                    SaveOptions(); break;
                case 5: _search.ClearText(); UIManager.SystemChat?.SetFocus(); break;
                case 6: _profile.SlayerBarLocked = !_profile.SlayerBarLocked; BuildContents(); SaveOptions(); break;
                case 7: EquipmentSwitcherLegendGump.Open(this); break;
            }
        }

        private NiceButton ToolbarButton(int x, string text, int id, string tooltip)
        {
            var button = new NiceButton(x, 3, 18, 18, ButtonAction.Activate, text, font: 1)
                { ButtonParameter = id, IsSelectable = false, AlwaysShowBackground = true, DisplayBorder = true };
            CustomGumpThemeManager.StyleDataButton(button); button.SetTooltip(tooltip); Add(button); return button;
        }

        private SwitcherIconButton IconButton(int x, SwitcherIcon icon, int id, string tooltip)
        {
            var button = new SwitcherIconButton(x, icon) { ButtonParameter = id };
            CustomGumpThemeManager.StyleDataButton(button); button.SetTooltip(tooltip); Add(button); return button;
        }

        // Only item cells are rebuilt; the search box stays alive and keeps keyboard focus.
        private void BuildContents()
        {
            var weapons = new List<Item>(); var shields = new List<Item>();
            var talismans = new List<Item>(); var spellbooks = new List<Item>();
            CollectBackpackItems(weapons, shields, talismans, spellbooks);
            int signature = CalculateSignature(weapons, shields, talismans, spellbooks);
            var groups = new[] { weapons, shields, talismans, spellbooks };
            string query = _search.Text.Trim();
            foreach (var group in groups) group.RemoveAll(item => !MatchesSearch(item, query));
            bool collapsed = _profile.SlayerBarCollapsed, horizontal = _profile.SlayerBarHorizontal;
            int[] counts = groups.Select(g => g.Count).ToArray();
            Point size = ContentSize(counts, horizontal);
            Width = collapsed ? 24 : Math.Max(148, PADDING * 2 + size.X);
            Height = collapsed ? 24 : TOOLBAR_HEIGHT + PADDING * 2 + Math.Max(18, size.Y);
            if (_background != null) { _background.Dispose(); Children.Remove(_background); }
            _background = CustomGumpThemeManager.CreateBackground(Width, Height, 0.88f, false);
            _background.OpacityScaleOverride = GetOpacityScale();
            Add(_background); Children.Remove(_background); Children.Insert(0, _background);
            _collapse.SetText(collapsed ? "+" : "-");
            _detach.Active = Detached;
            _lock.Active = _profile.SlayerBarLocked;
            _detach.SetTooltip(Detached ? "Attach to paperdoll" : "Detach and move freely");
            _lock.SetTooltip(_profile.SlayerBarLocked ? "Position locked. Click to unlock dragging." : "Lock detached position (equipment clicks still work)");
            _options.IsVisible = _detach.IsVisible = _rotate.IsVisible = _lock.IsVisible = _legend.IsVisible = _search.IsVisible = _searchBackground.IsVisible = _clearSearch.IsVisible = _contents.IsVisible = !collapsed;
            _searchBackground.Width = Width - PADDING * 2 - 20;
            _search.Width = _searchBackground.Width - 6;
            _clearSearch.X = Width - PADDING - 18;
            _search.Hue = CustomGumpThemeManager.DataTextHue;
            _dragHandle.Hue = CustomGumpThemeManager.DataTextHue;
            CustomGumpThemeManager.ApplyInputSurface(_searchBackground, 0.65f, false);
            foreach (var button in new NiceButton[] { _collapse, _options, _detach, _rotate, _lock, _legend, _clearSearch }) CustomGumpThemeManager.StyleDataButton(button);
            _contents.Clear(); _contents.Children.RemoveAll(c => c.IsDisposed);
            _contents.X = PADDING + Math.Max(0, (Width - PADDING * 2 - size.X) / 2);
            _contents.Y = TOOLBAR_HEIGHT + PADDING;
            _contents.Width = size.X;
            _contents.Height = Math.Max(18, size.Y);
            string[] captions = { "W", "S", "T", "B" }, names = { "Weapons", "Shields", "Talismans", "Spellbooks" };
            int offset = 0;
            for (int group = 0; group < groups.Length; group++)
            {
                if (collapsed || groups[group].Count == 0) continue;
                AddCategory(captions[group], names[group], groups[group], (EquipmentKind)group, offset, horizontal);
                offset += Lanes(groups[group].Count) * CELL + CATEGORY_GAP;
            }
            if (!collapsed && counts.Sum() == 0)
            {
                _contents.X = PADDING;
                _contents.Width = Width - PADDING * 2;
                _contents.Add(new Label("No matches.", true, CustomGumpThemeManager.DataTextHue, Width - PADDING * 2, font: 1));
            }
            _contentSignature = signature;
            UpdatePosition();
            if (Detached && UIManager.DraggingControl != this)
            { SetInScreen(); _profile.SlayerBarPosition = Location; }
        }

        private static int Lanes(int count) => (count + MAX_ROWS - 1) / MAX_ROWS;
        internal static Point ContentSize(int[] counts, bool horizontal)
        {
            int lanes = counts.Sum(Lanes), groups = counts.Count(n => n > 0);
            int length = Math.Min(MAX_ROWS, counts.Length == 0 ? 0 : counts.Max()) * CELL;
            int breadth = lanes * CELL + Math.Max(0, groups - 1) * CATEGORY_GAP;
            return horizontal ? new Point(HEADER_HEIGHT + length, breadth) : new Point(breadth, HEADER_HEIGHT + length);
        }
        internal static Point CellPosition(int index, int offset, bool horizontal) => horizontal
            ? new Point(HEADER_HEIGHT + index % MAX_ROWS * CELL + 2, offset + index / MAX_ROWS * CELL + 2)
            : new Point(offset + index / MAX_ROWS * CELL + 2, HEADER_HEIGHT + index % MAX_ROWS * CELL + 2);
        internal static bool MatchesSearch(Item item, string query) => item != null && !item.IsDestroyed
            && (string.IsNullOrWhiteSpace(query) || GridContainer.GridSlotManager.SearchItemNameAndProps(query, item));

        private void AddCategory(string caption, string tooltip, List<Item> items, EquipmentKind kind, int offset, bool horizontal)
        {
            int span = Lanes(items.Count) * CELL;
            var header = new HitBox(horizontal ? 0 : offset, horizontal ? offset : 0,
                horizontal ? HEADER_HEIGHT : span, horizontal ? span : HEADER_HEIGHT) { CanMove = CanMoveDetached(_profile) };
            header.SetTooltip(tooltip); _contents.Add(header);
            var label = new Label(caption, true, CustomGumpThemeManager.TitleHue, font: 1) { AcceptMouseInput = false };
            label.X = header.X + (header.Width - label.Width) / 2;
            label.Y = header.Y + (header.Height - label.Height) / 2;
            _contents.Add(label);
            for (int i = 0; i < items.Count; i++)
            {
                Point position = CellPosition(i, offset, horizontal);
                _contents.Add(new QuickEquipmentItemControl(items[i], kind, this) { X = position.X, Y = position.Y });
            }
        }

        private static void CollectBackpackItems(
            List<Item> weapons,
            List<Item> shields,
            List<Item> talismans,
            List<Item> spellbooks)
        {
            Item backpack = World.Player?.FindItemByLayer(Layer.Backpack);

            if (backpack != null)
            {
                CollectContainer(backpack, weapons, shields, talismans, spellbooks, new HashSet<uint>());
            }
        }

        private static void CollectContainer(
            Item container,
            List<Item> weapons,
            List<Item> shields,
            List<Item> talismans,
            List<Item> spellbooks,
            HashSet<uint> visited)
        {
            if (!visited.Add(container.Serial)) return;
            for (var node = container.Items; node != null; node = node.Next)
            {
                if (!(node is Item item) || item.IsDestroyed)
                {
                    continue;
                }

                Layer layer = (Layer)item.ItemData.Layer;

                if (SpellbookOpenerManager.IsSpellbook(item))
                {
                    spellbooks.Add(item);
                    World.OPL.Contains(item);
                }
                else if (IsShield(item))
                {
                    shields.Add(item);
                    World.OPL.Contains(item);
                }
                else if (item.ItemData.IsWeapon && (layer == Layer.OneHanded || layer == Layer.TwoHanded))
                {
                    weapons.Add(item);
                    World.OPL.Contains(item);
                }
                else if (layer == Layer.Talisman)
                {
                    talismans.Add(item);
                    World.OPL.Contains(item);
                }

                if (item.ItemData.IsContainer && !SpellbookOpenerManager.IsSpellbook(item))
                {
                    CollectContainer(item, weapons, shields, talismans, spellbooks, visited);
                }
            }
        }

        private static int GetContentSignature()
        {
            List<Item> weapons = new List<Item>();
            List<Item> shields = new List<Item>();
            List<Item> talismans = new List<Item>();
            List<Item> spellbooks = new List<Item>();
            CollectBackpackItems(weapons, shields, talismans, spellbooks);
            return CalculateSignature(weapons, shields, talismans, spellbooks);
        }

        private static int CalculateSignature(
            List<Item> weapons,
            List<Item> shields,
            List<Item> talismans,
            List<Item> spellbooks)
        {
            unchecked
            {
                int hash = 17;
                hash = AddItemsToSignature(hash, weapons, 1);
                hash = AddItemsToSignature(hash, shields, 2);
                hash = AddItemsToSignature(hash, talismans, 3);
                hash = AddItemsToSignature(hash, spellbooks, 4);
                hash = hash * 31 + (int)CustomGumpThemeManager.Current;
                hash = hash * 31 + (ProfileManager.CurrentProfile?.SlayerBarCollapsed == true ? 1 : 0);
                hash = hash * 31 + (ProfileManager.CurrentProfile?.SlayerBarHorizontal == true ? 1 : 0);
                hash = hash * 31 + (ProfileManager.CurrentProfile?.SlayerBarLocked == true ? 1 : 0);
                hash = hash * 31 + (ProfileManager.CurrentProfile?.SlayerBarOpacity ?? 100);
                return hash;
            }
        }

        private static float GetOpacityScale() => (ProfileManager.CurrentProfile?.SlayerBarOpacity ?? 100) / 100f;
        internal static void UpdateAllOptions()
        {
            foreach (var gump in UIManager.Gumps.OfType<PaperDollBackpackEquipmentGump>())
                if (gump._profile == ProfileManager.CurrentProfile) gump.BuildContents();
            UIManager.GetGump<EquipmentSwitcherLegendGump>()?.Refresh();
        }
        private static int AddItemsToSignature(int hash, List<Item> items, int category)
        {
            unchecked
            {
                hash = hash * 31 + category;
                hash = hash * 31 + items.Count;

                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];
                    hash = hash * 31 + (int)item.Serial;
                    hash = hash * 31 + item.DisplayedGraphic;
                    hash = hash * 31 + item.Hue;
                    hash = hash * 31 + (item.Name?.GetHashCode() ?? 0);
                    if (World.OPL.TryGetNameAndData(item.Serial, out string name, out string data))
                    {
                        hash = hash * 31 + (name?.GetHashCode() ?? 0);
                        hash = hash * 31 + (data?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
        }

        internal static IEnumerable<string> SlayerNames(string data)
        {
            if (string.IsNullOrEmpty(data)) yield break;
            foreach (string raw in data.Split('\n'))
            {
                string name = raw.Trim(' ', '\r', '\t', '*');
                if (string.Equals(name, "Silver", StringComparison.OrdinalIgnoreCase)) name = "Silver Slayer";
                if (name.StartsWith("Slayer:", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(7).Trim();
                    if (!name.EndsWith("Slayer", StringComparison.OrdinalIgnoreCase)) name += " Slayer";
                }
                if (name.IndexOf("slayer", StringComparison.OrdinalIgnoreCase) >= 0) yield return name;
            }
        }
        internal static ushort BackgroundHue(Profile profile, string data)
            => BackgroundRule(profile, data).Value;

        internal static string CanonicalSlayer(string name)
            => string.Equals(name, "Silver", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Silver Slayer", StringComparison.OrdinalIgnoreCase) ? "Undead Slayer" : name;

        internal static KeyValuePair<string, ushort> BackgroundRule(Profile profile, string data)
        {
            if (profile.SlayerBarSlayerHues != null)
                foreach (string slayer in SlayerNames(data))
                {
                    string canonical = CanonicalSlayer(slayer);
                    // Prefer the shared Undead setting over an older Silver override.
                    foreach (var choice in profile.SlayerBarSlayerHues)
                        if (string.Equals(choice.Key, canonical, StringComparison.OrdinalIgnoreCase)) return new KeyValuePair<string, ushort>(slayer, choice.Value);
                    foreach (var choice in profile.SlayerBarSlayerHues)
                        if (string.Equals(CanonicalSlayer(choice.Key), canonical, StringComparison.OrdinalIgnoreCase)) return new KeyValuePair<string, ushort>(slayer, choice.Value);
                }
            return new KeyValuePair<string, ushort>("Default / unmatched equipment", profile.SlayerBarDefaultHue);
        }

        internal static string HueExplanation(Profile profile, string data)
        {
            var rule = BackgroundRule(profile, data);
            string[] slayers = SlayerNames(data).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return "Cell background: " + rule.Key + " / " + (rule.Value == 0 ? "theme" : "0x" + rule.Value.ToString("X4"))
                + (slayers.Length > 0 ? "\nSlayers: " + string.Join(", ", slayers) : "\nNo slayer override matched the available properties.")
                + "\nFirst configured slayer in tooltip order wins. Hue 0 uses the theme.";
        }
        internal static string[] AvailableSlayers()
        {
            var names = new HashSet<string>(GridHighlightRules.SuperSlayerProperties, StringComparer.OrdinalIgnoreCase);
            names.Add("Silver Slayer");
            names.UnionWith(GridHighlightRules.SlayerProperties);
            if (ProfileManager.CurrentProfile.SlayerBarSlayerHues != null) names.UnionWith(ProfileManager.CurrentProfile.SlayerBarSlayerHues.Keys);
            var weapons = new List<Item>(); var shields = new List<Item>();
            var talismans = new List<Item>(); var books = new List<Item>();
            CollectBackpackItems(weapons, shields, talismans, books);
            foreach (Item item in weapons.Concat(shields).Concat(talismans).Concat(books))
                if (World.OPL.TryGetNameAndData(item.Serial, out _, out string data)) names.UnionWith(SlayerNames(data));
            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static bool IsShield(Item item)
        {
            if (
                item == null
                || (Layer)item.ItemData.Layer != Layer.TwoHanded
                || !item.ItemData.IsWearable
                || item.ItemData.IsLight
            )
            {
                return false;
            }

            if (!item.ItemData.IsWeapon)
            {
                return true;
            }

            string tileName = item.ItemData.Name;
            return !string.IsNullOrEmpty(tileName)
                && (
                    tileName.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0
                    || tileName.IndexOf("buckler", StringComparison.OrdinalIgnoreCase) >= 0
                );
        }

        private enum EquipmentKind : byte
        {
            Weapon,
            Shield,
            Talisman,
            Spellbook
        }

        private enum SwitcherIcon { Options, Dock, Rotate, Lock, Legend }

        // Small vector icons remain sharp and do not depend on optional UO artwork or font glyphs.
        private sealed class SwitcherIconButton : NiceButton
        {
            private readonly SwitcherIcon _icon;
            internal bool Active;
            internal SwitcherIconButton(int x, SwitcherIcon icon) : base(x, 3, 18, 18, ButtonAction.Activate, "", font: 1)
            { _icon = icon; IsSelectable = false; AlwaysShowBackground = true; DisplayBorder = true; }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                if (!base.Draw(batcher, x, y)) return false;
                var texture = SolidColorTextureCache.GetTexture(CustomGumpThemeManager.TitleHue == 0 ? Color.Black : Color.White);
                Vector3 hue = ShaderHueTranslator.GetHueVector(CustomGumpThemeManager.TitleHue, false, Alpha);
                void Line(float x1, float y1, float x2, float y2) => batcher.DrawLine(texture,
                    new Vector2(x + x1, y + y1), new Vector2(x + x2, y + y2), hue, 1f);
                void Box(int bx, int by, int w, int h) => batcher.DrawRectangle(texture, x + bx, y + by, w, h, hue);
                switch (_icon)
                {
                    case SwitcherIcon.Options:
                        Box(5, 5, 8, 8); Box(8, 8, 2, 2);
                        for (int i = 0; i < 8; i++)
                        {
                            double angle = i * Math.PI / 4;
                            Line(9 + (float)Math.Cos(angle) * 5, 9 + (float)Math.Sin(angle) * 5,
                                9 + (float)Math.Cos(angle) * 7, 9 + (float)Math.Sin(angle) * 7);
                        }
                        break;
                    case SwitcherIcon.Dock:
                        Box(3, 3, 9, 12); Line(4, 6, 11, 6);
                        if (Active) { Line(16, 10, 8, 10); Line(8, 10, 11, 7); Line(8, 10, 11, 13); }
                        else { Line(8, 10, 16, 10); Line(16, 10, 13, 7); Line(16, 10, 13, 13); }
                        break;
                    case SwitcherIcon.Rotate:
                        Line(4, 8, 4, 5); Line(4, 5, 13, 5); Line(13, 5, 10, 2); Line(13, 5, 10, 8);
                        Line(14, 10, 14, 13); Line(14, 13, 5, 13); Line(5, 13, 8, 10); Line(5, 13, 8, 16);
                        break;
                    case SwitcherIcon.Lock:
                        Box(5, 9, 9, 7); Line(9, 11, 9, 14);
                        if (Active) { Line(6, 9, 6, 4); Line(6, 4, 12, 4); Line(12, 4, 12, 9); }
                        else { Line(12, 9, 12, 4); Line(12, 4, 6, 4); Line(6, 4, 6, 6); }
                        break;
                    case SwitcherIcon.Legend:
                        for (int row = 0; row < 3; row++) { Box(3, 3 + row * 5, 4, 3); Line(10, 4 + row * 5, 15, 4 + row * 5); }
                        break;
                }
                return true;
            }
        }

        private sealed class QuickEquipmentItemControl : Control
        {
            private readonly EquipmentKind _kind;
            private readonly PaperDollBackpackEquipmentGump _equipmentBar;

            public QuickEquipmentItemControl(Item item, EquipmentKind kind, PaperDollBackpackEquipmentGump equipmentBar)
            {
                LocalSerial = item.Serial;
                _kind = kind;
                _equipmentBar = equipmentBar;
                Width = CELL - 4;
                Height = CELL - 4;
                AcceptMouseInput = true;
                CanMove = false;

                AlphaBlendControl background = new AlphaBlendControl(0.60f * GetOpacityScale())
                {
                    Width = Width,
                    Height = Height
                };
                CustomGumpThemeManager.ApplyDataSurface(background, background.Alpha, false);
                World.OPL.TryGetNameAndData(item.Serial, out _, out string data);
                ushort backgroundHue = BackgroundHue(equipmentBar._profile, data);
                if (backgroundHue != 0)
                {
                    background.ArtPanel = false;
                    background.MaterialGraphic = 0;
                    background.MaterialStyle = AlphaBlendMaterialStyle.None;
                    background.MaterialAlpha = 0;
                    background.BaseColor = Color.White;
                    background.Hue = backgroundHue;
                }
                Add(background);
                SetTooltip(item.Serial);
                var hueHint = new HitBox(Width - 8, Height - 8, 8, 8, HueExplanation(equipmentBar._profile, data), 0.5f);
                hueHint.MouseUp += (_, e) => OnMouseUp(0, 0, e.Button);
                Add(hueHint);
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                base.OnMouseUp(x, y, button);

                if (button != MouseButtonType.Left)
                {
                    return;
                }

                Item item = World.Items.Get(LocalSerial);

                if (item == null || item.IsDestroyed)
                {
                    Dispose();
                    return;
                }

                if (TargetManager.IsTargeting)
                {
                    TargetManager.Target(item.Serial);
                    return;
                }

                Equip(item, _kind);
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                Item item = World.Items.Get(LocalSerial);

                if (item == null || item.IsDestroyed)
                {
                    Dispose();
                    return false;
                }

                base.Draw(batcher, x, y);

                bool equipped = World.Player != null && item.Container == World.Player.Serial;
                Color borderColor = equipped ? Color.LimeGreen : MouseIsOver ? Color.Gold : Color.Gray;
                Vector3 borderHue = ShaderHueTranslator.GetHueVector(0, false, equipped || MouseIsOver ? 0.9f : 0.45f);
                batcher.DrawRectangle(
                    SolidColorTextureCache.GetTexture(borderColor),
                    x,
                    y,
                    Width,
                    Height,
                    borderHue
                );

                ref readonly var texture = ref Client.Game.Arts.GetArt((uint)item.DisplayedGraphic);
                Rectangle bounds = Client.Game.Arts.GetRealArtBounds((uint)item.DisplayedGraphic);

                if (texture.Texture == null || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return true;
                }

                int available = Width - 4;
                float scale = Math.Min(1f, Math.Min((float)available / bounds.Width, (float)available / bounds.Height));
                int drawWidth = Math.Max(1, (int)(bounds.Width * scale));
                int drawHeight = Math.Max(1, (int)(bounds.Height * scale));
                int drawX = x + (Width - drawWidth) / 2;
                int drawY = y + (Height - drawHeight) / 2;
                Vector3 hue = ShaderHueTranslator.GetHueVector(
                    MouseIsOver ? (ushort)0x0035 : item.Hue,
                    item.ItemData.IsPartialHue,
                    1,
                    true
                );

                batcher.Draw(
                    texture.Texture,
                    new Rectangle(drawX, drawY, drawWidth, drawHeight),
                    new Rectangle(texture.UV.X + bounds.X, texture.UV.Y + bounds.Y, bounds.Width, bounds.Height),
                    hue
                );

                batcher.DrawRectangle(SolidColorTextureCache.GetTexture(CustomGumpThemeManager.TitleHue == 0 ? Color.Black : Color.White), x + Width - 7, y + Height - 7,
                    5, 5, ShaderHueTranslator.GetHueVector(CustomGumpThemeManager.TitleHue, false, 0.8f));
                ItemProtectionManager.DrawMarker(batcher, item.Serial, x + 2, y + 2);

                return true;
            }

            private void Equip(Item item, EquipmentKind kind)
            {
                if (World.Player == null || World.Player.IsDead || MoveItemQueue.Instance == null)
                {
                    return;
                }

                Item backpack = World.Player.FindItemByLayer(Layer.Backpack);

                if (backpack == null)
                {
                    GameActions.Print("No backpack.", 0x21);
                    return;
                }

                Layer targetLayer = kind == EquipmentKind.Spellbook
                    ? Layer.OneHanded
                    : kind == EquipmentKind.Shield
                        ? Layer.TwoHanded
                        : (Layer)item.ItemData.Layer;

                if (kind == EquipmentKind.Spellbook)
                {
                    QueueUnequip(Layer.OneHanded, item.Serial, backpack.Serial);
                    Item offHand = World.Player.FindItemByLayer(Layer.TwoHanded);
                    if (offHand != null && offHand.ItemData.IsWeapon && !IsShield(offHand))
                    {
                        QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                        AutoRearmManager.TwoHandedSerial = 0;
                    }
                    AutoRearmManager.OneHandedSerial = item.Serial;
                }
                else if (kind == EquipmentKind.Weapon)
                {
                    if (targetLayer == Layer.OneHanded)
                    {
                        QueueUnequip(Layer.OneHanded, item.Serial, backpack.Serial);

                        Item offHand = World.Player.FindItemByLayer(Layer.TwoHanded);

                        if (offHand != null && offHand.ItemData.IsWeapon && !IsShield(offHand))
                        {
                            QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                            AutoRearmManager.TwoHandedSerial = 0;
                        }

                        AutoRearmManager.OneHandedSerial = item.Serial;
                    }
                    else
                    {
                        Item shield = World.Player.FindItemByLayer(Layer.TwoHanded);
                        if (IsShield(shield)) _equipmentBar._displacedShieldSerial = shield.Serial;
                        QueueUnequip(Layer.OneHanded, item.Serial, backpack.Serial);
                        QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                        AutoRearmManager.OneHandedSerial = 0;
                        AutoRearmManager.TwoHandedSerial = item.Serial;
                    }
                }
                else if (kind == EquipmentKind.Shield)
                {
                    _equipmentBar._displacedShieldSerial = 0;
                    QueueUnequip(Layer.TwoHanded, item.Serial, backpack.Serial);
                    AutoRearmManager.TwoHandedSerial = item.Serial;
                }
                else
                {
                    QueueUnequip(Layer.Talisman, item.Serial, backpack.Serial);
                }

                if (item.Container != World.Player.Serial)
                {
                    MoveItemQueue.Instance.EnqueueSwitcherEquip(item.Serial, targetLayer);
                }

                if (targetLayer == Layer.OneHanded)
                {
                    Item offHand = World.Player.FindItemByLayer(Layer.TwoHanded);
                    Item shield = World.Items.Get(_equipmentBar._displacedShieldSerial);
                    if ((offHand == null || offHand.ItemData.IsWeapon && !IsShield(offHand))
                        && IsShield(shield) && !shield.IsDestroyed && shield.RootContainer == World.Player.Serial
                        && shield.Container != World.Player.Serial)
                    {
                        MoveItemQueue.Instance.EnqueueSwitcherEquip(shield.Serial, Layer.TwoHanded);
                        AutoRearmManager.TwoHandedSerial = shield.Serial;
                    }
                    _equipmentBar._displacedShieldSerial = 0;
                }
            }

            private static void QueueUnequip(Layer layer, uint selectedSerial, uint backpackSerial)
            {
                Item equipped = World.Player.FindItemByLayer(layer);

                if (equipped != null && equipped.Serial != selectedSerial)
                {
                    MoveItemQueue.Instance.EnqueueSwitcherUnequip(equipped.Serial, backpackSerial);
                }
            }
        }
    }
}

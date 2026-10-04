#region license
// TazUO addition. Live selector for shared custom gump themes.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class GumpThemeSelectorGump : Gump
    {
        private const int W = 620;
        private const int H = 640;
        private const int PAD = 12;
        private const int CARD_W = 284;
        private const int CARD_H = 82;
        private int _lastX;
        private int _lastY;
        private readonly ScrollArea _scroll;
        private string _searchText;
        internal string SearchText => _searchText;

        private static readonly string[] _names =
        {
            "Minimal", "Classic UO", "Runestone", "Oak & Iron", "Dark", "Royal",
            "Forest", "Dungeon", "Water", "Snow", "Heartwood Sanctuary", "Ter Mur",
            "Kotl", "TazUO", "Britannia", "Trinsic", "Minoc", "Blackthorn",
            "Obsidian", "Doom", "Midnight", "Necromancer's Crypt", "Ornate",
            "Britannian Chronicle", "Moonglow Arcane", "Ter Mur Relic",
            "Mariner's Chart", "Gilded Grove", "Aetherglass", "Celestial",
            "Exodus", "Blood Oath", "Hildebrandt", "HD Stone", "HD Wood", "HD Metal", "HD Marble",
            "HD Glass", "HD Stained Glass"
        };

        private static readonly string[] _descriptions =
        {
            "Clean translucent black",
            "Classic gray stone and brass",
            "Runes cut into granite and brass",
            "Weathered oak and ironwork",
            "Blue-black steel",
            "Indigo and gold",
            "Deep woodland green",
            "Charcoal and blood-red",
            "Navy and cyan",
            "Frosted slate and ice",
            "Living elven wood and emerald",
            "Amethyst gargoyle stone",
            "Ancient metal and bronze",
            "Original TazUO panels",
            "Crimson court and gold",
            "Ivory stone and brass",
            "Forged iron and oak",
            "Black iron and crimson",
            "Pitch-black volcanic glass",
            "Black stone and blood iron",
            "Moonlit navy and silver",
            "Bone, iron and grave magic",
            "Dark vellum and engraved brass",
            "Parchment, lapis and gold",
            "Moonlit stone and silver",
            "Basalt, copper and crystal",
            "Oak, brass and old charts",
            "Dark green and gilded filigree",
            "Iridescent glass and indigo",
            "Silver stars and midnight blue",
            "Arcane bronze and red crystal",
            "Black iron and bloodstone",
            "Painted heroic fantasy and gold",
            "Dark stone and brass",
            "Oak and iron",
            "Steel and silver",
            "White marble and graphite",
            "Smoked glass and silver",
            "Jewel glass and dark lead"
        };

        internal GumpThemeSelectorGump(string search = "")
            : base(0, 0)
        {
            Point position = ProfileManager.CurrentProfile?.GumpThemeSelectorPosition
                ?? new Point(180, 100);

            X = position.X;
            Y = position.Y;
            _lastX = X;
            _lastY = Y;
            Width = W;
            Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.92f));
            Add(new Label(
                "Gump Themes",
                true,
                CustomGumpThemeManager.TitleHue,
                font: 1)
            {
                X = PAD,
                Y = 9
            });
            Add(new Label(
                $"Current: {DisplayName(CustomGumpThemeManager.Current)} — click a style to apply it immediately.",
                true,
                CustomGumpThemeManager.DimHue,
                W - PAD * 2,
                font: 1)
            {
                X = PAD,
                Y = 29
            });

            Add(new Label("Find:", true, CustomGumpThemeManager.TextHue, font: 1) { X = PAD, Y = 61 });
            var searchBox = new StbTextBox(1, 64, W - PAD * 2 - 44, hue: CustomGumpThemeManager.TextHue)
            {
                X = PAD + 44, Y = 56, Width = W - PAD * 2 - 44, Height = 24, Multiline = false
            };
            var searchBackground = new AlphaBlendControl(0.8f) { Width = searchBox.Width, Height = searchBox.Height };
            CustomGumpThemeManager.ApplyInputSurface(searchBackground, 0.8f);
            searchBox.Add(searchBackground);
            _searchText = search ?? string.Empty;
            searchBox.SetText(_searchText);
            searchBox.TextChanged += (sender, args) =>
            {
                _searchText = searchBox.Text;
                RebuildCards();
            };
            Add(searchBox);
            var dailyRotation = new Checkbox(0x00D2, 0x00D3, "Rotate themes daily", font: 1,
                color: CustomGumpThemeManager.TextHue)
            {
                X = PAD, Y = 86,
                IsChecked = ProfileManager.CurrentProfile?.RotateGumpThemesDaily == true
            };
            dailyRotation.SetTooltip("Switch to the next available theme each local calendar day, or on your next login. Per-window theme overrides stay active.");
            dailyRotation.ValueChanged += (_, __) => CustomGumpThemeManager.SetDailyRotation(dailyRotation.IsChecked);
            Add(dailyRotation);
            _scroll = new ScrollArea(PAD, 114, W - PAD * 2, H - 166, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            Add(_scroll);
            RebuildCards();

            Add(new Label(
                "Applies to custom HUD, utility gumps, dialogs, containers, journal and modern chat.",
                true,
                CustomGumpThemeManager.DimHue,
                W - PAD * 2 - 86,
                font: 1)
            {
                X = PAD,
                Y = H - 28
            });
            AddButton(W - PAD - 76, H - 31, 76, 21, "Close", Dispose, 0x35);
            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;

        public override void Update()
        {
            base.Update();

            if (X != _lastX || Y != _lastY)
            {
                _lastX = X;
                _lastY = Y;

                if (ProfileManager.CurrentProfile != null)
                {
                    ProfileManager.CurrentProfile.GumpThemeSelectorPosition =
                        new Point(X, Y);
                }
            }
        }

        public override void Dispose()
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (profile != null)
            {
                profile.GumpThemeSelectorPosition = new Point(X, Y);

                if (!string.IsNullOrEmpty(ProfileManager.ProfilePath))
                {
                    profile.Save(ProfileManager.ProfilePath, false);
                }
            }

            base.Dispose();
        }

        private void AddThemeCard(ScrollArea scroll, CustomGumpTheme theme, int index)
        {
            int column = index % 2;
            int row = index / 2;
            int x = column * (CARD_W + 12);
            int y = row * (CARD_H + 6);
            bool selected = theme == CustomGumpThemeManager.Current;
            ushort textHue = CustomGumpThemeManager.GetTextHue(theme);
            using var preview = CustomGumpThemeManager.ForPreview(theme);
            var card = new ThemePreviewCard(theme) { X = x, Y = y };
            scroll.Add(card);

            card.Add(new ThemedGumpBackground(CARD_W, CARD_H, 0.90f, theme, true));
            card.Add(new Label(DisplayName(theme), true, textHue, CARD_W - 90, font: 1)
            {
                X = 10,
                Y = 8
            });
            card.Add(new Label(Description(theme), true, textHue, CARD_W - 58, font: 1)
            {
                X = 10,
                Y = 36
            });
            AddButton(
                CARD_W - 76,
                9,
                66,
                22,
                selected ? "ACTIVE" : "Use",
                () => SelectTheme(theme),
                selected ? (ushort)0x44 : (ushort)0x35,
                selected,
                card
            );
            AddButton(CARD_W - 34, 55, 24, 20,
                IsFavorite(theme) ? "*" : "+", () => ToggleFavorite(theme), 0x35, false, card);
        }

        private sealed class ThemePreviewCard : Control
        {
            private readonly CustomGumpTheme _theme;

            internal ThemePreviewCard(CustomGumpTheme theme)
            {
                _theme = theme;
                Width = CARD_W;
                Height = CARD_H;
                WantUpdateSize = false;
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                using (CustomGumpThemeManager.ForPreview(_theme))
                    return base.Draw(batcher, x, y);
            }
        }

        private void RebuildCards()
        {
            _scroll.Clear();
            _scroll.ResetScrollbarPosition();
            int index = 0;
            for (int group = 0; group < 2; group++)
            foreach (CustomGumpTheme theme in CustomGumpThemeManager.AvailableThemes)
            {
                if (IsFavorite(theme) != (group == 0)) continue;
                if (MatchesSearch(theme, _searchText)) AddThemeCard(_scroll, theme, index++);
            }
            if (index == 0)
                _scroll.Add(new Label("No matching themes.", true, CustomGumpThemeManager.TextHue, font: 1));
        }

        internal static bool MatchesSearch(CustomGumpTheme theme, string search)
        {
            string query = (search ?? string.Empty).Trim();
            return DisplayName(theme).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || Description(theme).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFavorite(CustomGumpTheme theme) =>
            ProfileManager.CurrentProfile?.FavoriteGumpThemes?.Contains((byte)theme) == true;

        private void ToggleFavorite(CustomGumpTheme theme)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null) return;
            if (profile.FavoriteGumpThemes == null) profile.FavoriteGumpThemes = new System.Collections.Generic.List<byte>();
            if (!profile.FavoriteGumpThemes.Remove((byte)theme)) profile.FavoriteGumpThemes.Add((byte)theme);
            profile.Save(ProfileManager.ProfilePath, false);
            RebuildCards();
        }

        private static string Description(CustomGumpTheme theme)
        {
            PremiumGumpTheme premium = PremiumGumpThemes.Get(theme);
            int index = (int)theme;
            return premium != null ? premium.Description
                : (uint)index < _descriptions.Length ? _descriptions[index] : string.Empty;
        }

        internal static string DisplayName(CustomGumpTheme theme)
        {
            PremiumGumpTheme premium = PremiumGumpThemes.Get(theme);
            if (premium != null) return premium.Name;
            int index = (int)theme;
            return index >= 0 && index < _names.Length
                ? _names[index]
                : theme.ToString();
        }

        private static void SelectTheme(CustomGumpTheme theme)
        {
            CustomGumpThemeManager.SetTheme(theme);
            GameActions.Print($"Gump theme: {DisplayName(theme)}.", 0x35);
        }

        private void AddButton(
            int x,
            int y,
            int width,
            int height,
            string text,
            Action action,
            ushort hue,
            bool disabled = false,
            Control parent = null)
        {
            var button = new NiceButton(
                x,
                y,
                width,
                height,
                ButtonAction.Activate,
                text,
                font: 1,
                hue: hue)
            {
                IsSelectable = false,
                DisplayBorder = true,
                AcceptMouseInput = !disabled
            };
            CustomGumpThemeManager.StyleButton(button);

            if (!disabled)
            {
                button.MouseUp += (sender, e) =>
                {
                    if (e.Button == MouseButtonType.Left)
                    {
                        action();
                    }
                };
            }

            if (parent == null)
                Add(button);
            else
                parent.Add(button);
        }
    }
}

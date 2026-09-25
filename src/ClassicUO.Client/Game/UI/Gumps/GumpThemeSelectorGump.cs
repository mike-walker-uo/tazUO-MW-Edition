#region license
// TazUO addition. Live selector for shared custom gump themes.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class GumpThemeSelectorGump : Gump
    {
        private const int W = 620;
        private const int H = 640;
        private const int PAD = 12;
        private const int CARD_W = 284;
        private const int CARD_H = 37;
        private int _lastX;
        private int _lastY;

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

        internal GumpThemeSelectorGump()
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

            var scroll = new ScrollArea(PAD, 48, W - PAD * 2, H - 100, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            Add(scroll);
            for (int i = 0; i < CustomGumpThemeManager.ThemeCount; i++)
            {
                AddThemeCard(scroll, (CustomGumpTheme)i, i);
            }

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
            int y = row * 38;
            bool selected = theme == CustomGumpThemeManager.Current;
            ushort textHue = CustomGumpThemeManager.GetTextHue(theme);

            scroll.Add(new ThemedGumpBackground(CARD_W, CARD_H, 0.90f, theme, true)
            {
                X = x,
                Y = y
            });
            scroll.Add(new Label(DisplayName(theme), true, textHue, font: 1)
            {
                X = x + 10,
                Y = y + 8
            });
            scroll.Add(new Label(_descriptions[index], true, textHue, 190, font: 1)
            {
                X = x + 10,
                Y = y + 24
            });
            AddButton(
                x + CARD_W - 76,
                y + 9,
                66,
                22,
                selected ? "ACTIVE" : "Use",
                () => SelectTheme(theme),
                selected ? (ushort)0x44 : (ushort)0x35,
                selected,
                scroll
            );
        }

        internal static string DisplayName(CustomGumpTheme theme)
        {
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
            ScrollArea scroll = null)
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

            if (scroll == null)
                Add(button);
            else
                scroll.Add(button);
        }
    }
}

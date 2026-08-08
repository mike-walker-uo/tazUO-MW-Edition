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
        private const int H = 610;
        private const int PAD = 12;
        private const int CARD_W = 292;
        private const int CARD_H = 44;
        private int _lastX;
        private int _lastY;

        private static readonly string[] _names =
        {
            "Minimal", "Classic UO", "Stone", "Wood", "Dark", "Royal",
            "Forest", "Dungeon", "Water", "Snow", "Heartwood", "Ter Mur",
            "Kotl", "TazUO", "Britannia", "Trinsic", "Minoc", "Blackthorn",
            "Obsidian", "Doom", "Midnight", "Necropolis"
        };

        private static readonly string[] _descriptions =
        {
            "Clean translucent black",
            "Classic gray stone and brass",
            "Cool carved stone",
            "Warm timber and brass",
            "Blue-black steel",
            "Indigo and gold",
            "Deep woodland green",
            "Charcoal and blood-red",
            "Navy and cyan",
            "Frosted slate and ice",
            "Living wood and elven gold",
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
            "Ancient black stone and bone"
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

            for (int i = 0; i < CustomGumpThemeManager.ThemeCount; i++)
            {
                AddThemeCard((CustomGumpTheme)i, i);
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

        private void AddThemeCard(CustomGumpTheme theme, int index)
        {
            int column = index % 2;
            int row = index / 2;
            int x = PAD + column * (CARD_W + 12);
            int y = 54 + row * 45;
            bool selected = theme == CustomGumpThemeManager.Current;
            ushort textHue = CustomGumpThemeManager.GetTextHue(theme);

            Add(new ThemedGumpBackground(CARD_W, CARD_H, 0.90f, theme)
            {
                X = x,
                Y = y
            });
            Add(new Label(DisplayName(theme), true, textHue, font: 1)
            {
                X = x + 10,
                Y = y + 8
            });
            Add(new Label(_descriptions[index], true, textHue, 190, font: 1)
            {
                X = x + 10,
                Y = y + 24
            });
            AddButton(
                x + CARD_W - 76,
                y + 11,
                66,
                22,
                selected ? "ACTIVE" : "Use",
                () => SelectTheme(theme),
                selected ? (ushort)0x44 : (ushort)0x35,
                selected
            );
        }

        private static string DisplayName(CustomGumpTheme theme)
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
            bool disabled = false)
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

            Add(button);
        }
    }
}

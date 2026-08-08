#region license
// TazUO addition. Custom spell/ability effect controls and local previews.
#endregion

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class SpellAbilityEffectsGump : Gump
    {
        private const int W = 640;
        private const int H = 520;
        private const int PAD = 14;
        private const int ROW_H = 31;
        private const int ROWS_PER_PAGE = 12;
        private static ushort TITLE_HUE => CustomGumpThemeManager.TitleHue;
        private static ushort TEXT_HUE => CustomGumpThemeManager.TextHue;
        private static ushort DIM_HUE => CustomGumpThemeManager.DimHue;

        private readonly int _page;

        internal SpellAbilityEffectsGump(int page = 0)
            : base(0, 0)
        {
            SpellAbilityEffectEntry[] entries =
                SpellAbilityEffectSettings.Entries;
            int pageCount = Math.Max(
                1,
                (entries.Length + ROWS_PER_PAGE - 1) / ROWS_PER_PAGE
            );
            _page = Math.Max(0, Math.Min(pageCount - 1, page));

            X = 170;
            Y = 80;
            Width = W;
            Height = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            Add(CustomGumpThemeManager.CreateBackground(W, H, 0.9f));
            Add(
                new Label(
                    "Spell & Ability Effects",
                    true,
                    TITLE_HUE,
                    font: 1
                )
                {
                    X = PAD,
                    Y = 9
                }
            );
            Add(
                new Label(
                    "Toggles persist per character. Preview is local only: no cast, target, mana, or ownership required.",
                    true,
                    DIM_HUE,
                    W - PAD * 2,
                    font: 1
                )
                {
                    X = PAD,
                    Y = 29
                }
            );

            AddButton(
                PAD,
                53,
                104,
                21,
                "Enable all",
                () =>
                {
                    SpellAbilityEffectSettings.SetAll(true);
                    Reopen();
                },
                0x44
            );
            AddButton(
                PAD + 110,
                53,
                104,
                21,
                "Disable all",
                () =>
                {
                    SpellAbilityEffectSettings.SetAll(false);
                    Reopen();
                },
                0x21
            );
            AddButton(
                PAD + 220,
                53,
                128,
                21,
                SpellAbilityEffectSettings.VisualSilenceEnabled
                    ? "Silence: ON"
                    : "Silence: OFF",
                () =>
                {
                    SpellAbilityEffectSettings.SetVisualSilence(
                        !SpellAbilityEffectSettings.VisualSilenceEnabled
                    );
                    Reopen();
                },
                SpellAbilityEffectSettings.VisualSilenceEnabled
                    ? (ushort)0x21
                    : (ushort)0x35,
                tooltip: "Suppress all original and custom spell/combat effect graphics. Audio is unchanged."
            );
            AddButton(
                PAD + 354,
                53,
                128,
                21,
                SpellAbilityEffectSettings.ClassicEffectsOnlyEnabled
                    ? "Classic: ON"
                    : "Classic: OFF",
                () =>
                {
                    SpellAbilityEffectSettings.SetClassicEffectsOnly(
                        !SpellAbilityEffectSettings.ClassicEffectsOnlyEnabled
                    );
                    Reopen();
                },
                SpellAbilityEffectSettings.ClassicEffectsOnlyEnabled
                    ? (ushort)0x44
                    : (ushort)0x35,
                tooltip: "Suppress TazUO spell and ability enhancements while retaining original UO effects."
            );
            Add(
                new Label(
                    $"Page {_page + 1}/{pageCount}",
                    true,
                    TEXT_HUE,
                    font: 1
                )
                {
                    X = W - PAD - 80,
                    Y = 57
                }
            );

            int first = _page * ROWS_PER_PAGE;
            int last = Math.Min(entries.Length, first + ROWS_PER_PAGE);
            int y = 84;

            for (int i = first; i < last; i++)
            {
                AddEntry(entries[i], y);
                y += ROW_H;
            }

            int footerY = H - 35;
            AddButton(
                PAD,
                footerY,
                76,
                22,
                "< Previous",
                () => OpenPage(_page - 1),
                0x35,
                _page == 0
            );
            AddButton(
                PAD + 82,
                footerY,
                76,
                22,
                "Next >",
                () => OpenPage(_page + 1),
                0x35,
                _page >= pageCount - 1
            );
            AddButton(
                W - PAD - 90,
                footerY,
                90,
                22,
                "Close",
                Dispose,
                0x35
            );
        }

        public override GumpType GumpType => GumpType.None;
        internal int CurrentPage => _page;

        private void AddEntry(SpellAbilityEffectEntry entry, int y)
        {
            bool enabled =
                SpellAbilityEffectSettings.IsConfiguredEnabled(entry.Id);

            Add(
                new Label(entry.Name, true, TEXT_HUE, 300, font: 1)
                {
                    X = PAD,
                    Y = y + 2
                }
            );
            Add(
                new Label(entry.Category, true, DIM_HUE, 150, font: 1)
                {
                    X = 300,
                    Y = y + 2
                }
            );

            AddButton(
                452,
                y,
                72,
                22,
                enabled ? "ON" : "OFF",
                () =>
                {
                    SpellAbilityEffectSettings.SetEnabled(
                        entry.Id,
                        !enabled
                    );
                    Reopen();
                },
                enabled ? (ushort)0x44 : (ushort)0x21
            );
            AddButton(
                530,
                y,
                94,
                22,
                "Preview",
                () =>
                {
                    if (!SpellAbilityEffectSettings.CustomEffectsEnabled)
                    {
                        GameActions.Print(
                            "Disable Visual Silence and Classic Effects to preview custom effects.",
                            0x35
                        );
                        return;
                    }

                    if (!SpellAbilityEffectSettings.Preview(entry.Id))
                    {
                        GameActions.Print(
                            "Effect previews require an active character.",
                            0x35
                        );
                    }
                },
                0x35
            );

            Add(
                new AlphaBlendControl(0.18f)
                {
                    X = PAD,
                    Y = y + 26,
                    Width = W - PAD * 2,
                    Height = 1
                }
            );
        }

        private void Reopen()
        {
            OpenPage(_page);
        }

        private void OpenPage(int page)
        {
            int x = X;
            int y = Y;

            Dispose();
            UIManager.Add(
                new SpellAbilityEffectsGump(page)
                {
                    X = x,
                    Y = y
                }
            );
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
            string tooltip = null)
        {
            var button = new NiceButton(
                x,
                y,
                width,
                height,
                ButtonAction.Activate,
                text,
                font: 1,
                hue: disabled ? DIM_HUE : hue
            )
            {
                IsSelectable = false,
                DisplayBorder = true,
                AcceptMouseInput = !disabled
            };
            CustomGumpThemeManager.StyleButton(button);

            if (!string.IsNullOrEmpty(tooltip))
            {
                button.SetTooltip(tooltip);
            }

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

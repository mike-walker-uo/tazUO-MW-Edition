// TazUO addition: Item Finder syntax and live searchable-property reference.

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ItemFinderHelpGump : Gump
    {
        private const int WIDTH = 760;
        private const int HEIGHT = 600;
        private const int LEFT_WIDTH = 326;
        private readonly List<ItemFinderManager.SearchableProperty> _properties;
        private readonly VBoxContainer _propertyBox;
        private readonly Label _propertyTitle;
        private readonly StbTextBox _propertySearch;

        internal ItemFinderHelpGump(ItemFinderGump finder) : base(0, 0)
        {
            X = finder?.X + 18 ?? 240;
            Y = finder?.Y + 18 ?? 140;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            Add(FeatureGumpArtwork.CreateBackground(
                WIDTH, HEIGHT, FeatureGumpArtworkKind.ItemFinder, 0.97f));
            AddSurface(20, 10, WIDTH - 40, 50, 0.54f);
            Add(new Label("ITEM FINDER  •  SEARCH HELP", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 80, font: 1)
            {
                X = 24,
                Y = 16
            });
            Add(new Label("Build text, numeric, exclusion and count filters in one query.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 80, font: 1)
            {
                X = 24,
                Y = 38
            });
            Add(CreateButton(708, 16, 28, 26, "X", 99));
            AddAccent(24, 61, WIDTH - 48);

            Add(new Label("SYNTAX", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), LEFT_WIDTH, font: 1)
            {
                X = 24,
                Y = 78
            });
            AddSurface(24, 102, LEFT_WIDTH, 458, 0.42f);

            var syntaxScroll = new ScrollArea(28, 106, LEFT_WIDTH - 8, 450, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            var syntax = new VBoxContainer(
                syntaxScroll.Width - syntaxScroll.ScrollBarWidth() - 4, 0, 0);
            syntaxScroll.Add(syntax);
            Add(syntaxScroll);

            AddSection(syntax, "TEXT / TYPE",
                "Words match the item name, type, or any loaded tooltip property. "
                + "Every normal word must match.\n"
                + "Examples: ring animated  slayer  repond slayer");
            AddSection(syntax, "ALL ITEMS",
                "Use * alone to list the complete catalog.");
            AddSection(syntax, "NUMERIC PROPERTY",
                "Use key + operator + value. Spaces are removed from property names.\n"
                + "Examples: hci>=10  physicalresist>=15\n"
                + "Operators: >=  >  =  <  <=\n"
                + "The comparison uses the first number on the tooltip line.");
            AddSection(syntax, "NOT",
                "not:text or !text excludes tooltip text.\n"
                + "not:hci>=10 or !hci>=10 negates a numeric test.\n"
                + "Example: ring not:cursed");
            AddSection(syntax, "COUNT GROUP",
                "Nof(...) requires any N conditions in the group.\n"
                + "Example: ring 2of(hci>=10,di>=20,ssi>=10)");
            AddSection(syntax, "ALIASES",
                "HCI  Hit Chance Increase\n"
                + "DCI  Defense Chance Increase\n"
                + "DI  Damage Increase\n"
                + "SSI  Swing Speed Increase\n"
                + "LMC  Lower Mana Cost\n"
                + "LRC  Lower Reagent Cost\n"
                + "FC  Faster Casting\n"
                + "FCR  Faster Cast Recovery\n"
                + "SDI  Spell Damage Increase\n"
                + "MR  Mana Regeneration\n"
                + "HPREGEN  Hit Point Regeneration\n"
                + "STAMREGEN  Stamina Regeneration");

            Add(new AlphaBlendControl(0.55f)
            {
                X = 365,
                Y = 77,
                Width = 1,
                Height = 483,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder)
            });

            _properties = ItemFinderManager.GetSearchableProperties();
            Add(_propertyTitle = new Label(string.Empty, true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), 370, font: 1)
            {
                X = 382,
                Y = 78
            });
            Add(new Label("Filter by property name or exact search key.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), 354, font: 1)
            {
                X = 382,
                Y = 98
            });
            AddSurface(382, 122, 354, 32, 0.68f);
            Add(_propertySearch = new StbTextBox(1, 40, 338, true,
                hue: FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder))
            {
                X = 390,
                Y = 128,
                Width = 338,
                Height = 20
            });
            _propertySearch.SetTooltip("Type part of a property name or search key, for example resist or hci");
            _propertySearch.TextChanged += PropertySearch_TextChanged;
            AddSurface(382, 160, 354, 400, 0.42f);

            var propertyScroll = new ScrollArea(386, 164, 346, 392, true)
            {
                ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways
            };
            _propertyBox = new VBoxContainer(
                propertyScroll.Width - propertyScroll.ScrollBarWidth() - 4, 0, 0);
            propertyScroll.Add(_propertyBox);
            Add(propertyScroll);
            RebuildProperties();

            AddAccent(24, 574, WIDTH - 48);
            Add(new Label("The list reflects numeric properties found in this character's current catalog.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), WIDTH - 48, font: 1)
            {
                X = 24,
                Y = 579
            });

            SetInScreen();
        }

        public override GumpType GumpType => GumpType.None;
        public override bool ShouldBeSaved => false;

        private void PropertySearch_TextChanged(object sender, EventArgs e)
        {
            RebuildProperties();
        }

        private void RebuildProperties()
        {
            _propertyBox.Clear();
            string filter = _propertySearch.Text?.Trim() ?? string.Empty;
            int shown = 0;

            foreach (ItemFinderManager.SearchableProperty property in _properties)
            {
                if (filter.Length > 0
                    && property.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                    && property.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _propertyBox.Add(new PropertyRow(property, _propertyBox.Width - 4));
                shown++;
            }

            _propertyTitle.Text = $"CATALOG PROPERTIES  •  {shown}/{_properties.Count}";
            if (shown == 0)
            {
                _propertyBox.Add(new Label(_properties.Count == 0
                        ? "Scan an area to discover searchable properties."
                        : "No catalog property matches this filter.", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder),
                    _propertyBox.Width - 8, font: 1));
            }
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99)
            {
                Dispose();
            }
        }

        private void AddSection(VBoxContainer box, string title, string text)
        {
            var section = new HitBox(
                0,
                0,
                box.Width - 4,
                30 + CountLines(text) * 18,
                null,
                0f)
            {
                AcceptMouseInput = false
            };
            section.Add(new Label(title, true,
                FeatureGumpArtwork.AccentHue(FeatureGumpArtworkKind.ItemFinder),
                section.Width - 12, font: 1)
            {
                X = 6,
                Y = 5
            });
            section.Add(new Label(text, true,
                FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder),
                section.Width - 12, font: 1)
            {
                X = 6,
                Y = 25
            });
            box.Add(section);
        }

        private static int CountLines(string text)
        {
            int lines = 1;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    lines++;
                }
            }

            return lines;
        }

        private void AddSurface(int x, int y, int width, int height, float alpha)
        {
            Add(FeatureGumpArtwork.CreateSurface(x, y, width, height,
                FeatureGumpArtworkKind.ItemFinder, alpha));
        }

        private void AddAccent(int x, int y, int width)
        {
            Add(new AlphaBlendControl(0.75f)
            {
                X = x,
                Y = y,
                Width = width,
                Height = 1,
                BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder)
            });
        }

        private NiceButton CreateButton(int x, int y, int width, int height, string text, int id)
        {
            return FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.ItemFinder);
        }

        private sealed class PropertyRow : Control
        {
            internal PropertyRow(ItemFinderManager.SearchableProperty property, int width)
            {
                Width = width;
                Height = 42;
                AcceptMouseInput = false;

                Add(new Label(property.Name, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder),
                    width - 12, font: 1)
                {
                    X = 6,
                    Y = 3
                });
                Add(new Label($"{property.Key}  •  {property.ItemCount} item(s)", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder),
                    width - 12, font: 1)
                {
                    X = 6,
                    Y = 22
                });
                Add(new AlphaBlendControl(0.35f)
                {
                    X = 6,
                    Y = 40,
                    Width = width - 12,
                    Height = 1,
                    BaseColor = FeatureGumpArtwork.BorderColor(FeatureGumpArtworkKind.ItemFinder)
                });
            }
        }
    }
}

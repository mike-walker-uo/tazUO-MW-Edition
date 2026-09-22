// TazUO addition: source and age maintenance for the persistent item catalog.

using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class ItemFinderCatalogGump : Gump
    {
        private const int WIDTH = 650;
        private const int HEIGHT = 520;
        private readonly VBoxContainer _rows;
        private readonly Label _title;
        private readonly Label _status;
        private List<ItemFinderManager.CatalogSource> _sources;

        internal ItemFinderCatalogGump(ItemFinderGump owner) : base(0, 0)
        {
            X = owner?.X + 30 ?? 240;
            Y = owner?.Y + 30 ?? 140;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Add(FeatureGumpArtwork.CreateBackground(WIDTH, HEIGHT,
                FeatureGumpArtworkKind.ItemFinder, 0.97f));
            Add(_title = new Label("ITEM CATALOG", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.ItemFinder), 500, font: 1)
            { X = 24, Y = 18 });
            Add(new Label("Stored container locations remain available when another house is out of range.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), 560, font: 1)
            { X = 24, Y = 40 });
            Add(Button(598, 16, 28, 28, "X", 99));
            Add(FeatureGumpArtwork.CreateSurface(24, 72, 602, 390,
                FeatureGumpArtworkKind.ItemFinder, 0.42f));
            var scroll = new ScrollArea(28, 76, 594, 382, true)
            { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            scroll.Add(_rows = new VBoxContainer(scroll.Width - scroll.ScrollBarWidth() - 3, 0, 2));
            Add(scroll);
            Add(_status = new Label("Forget removes only the selected stored source.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), 600, font: 1)
            { X = 24, Y = 482 });
            Rebuild();
            SetInScreen();
        }

        public override bool ShouldBeSaved => false;

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99) Dispose();
            else if (buttonID >= 100)
            {
                int index = buttonID - 100;
                if (index >= 0 && index < _sources.Count)
                {
                    int removed = ItemFinderManager.ForgetCatalogSource(_sources[index].RootSerial);
                    _status.Text = $"Removed {removed} stored item(s).";
                    Rebuild();
                }
            }
        }

        private void Rebuild()
        {
            _rows.Clear();
            _sources = ItemFinderManager.GetCatalogSources();
            _title.Text = $"ITEM CATALOG  •  {ItemFinderManager.CatalogCount} ITEMS  •  {_sources.Count} SOURCES";
            if (_sources.Count == 0)
            {
                _rows.Add(new Label("The catalog is empty. Use Scan area in Item Finder.", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder), _rows.Width - 12, font: 1));
                return;
            }

            for (int i = 0; i < _sources.Count; i++)
                _rows.Add(new SourceRow(_sources[i], i, _rows.Width - 4));
        }

        private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
            FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.ItemFinder);

        private sealed class SourceRow : Control
        {
            internal SourceRow(ItemFinderManager.CatalogSource source, int index, int width)
            {
                Width = width;
                Height = 58;
                Add(FeatureGumpArtwork.CreateSurface(0, 0, width, Height,
                    FeatureGumpArtworkKind.ItemFinder, 0.38f));
                string location = string.IsNullOrWhiteSpace(source.Location)
                    ? $"Container 0x{source.RootSerial:X8}" : source.Location;
                Add(new Label(location, true,
                    FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.ItemFinder), width - 116,
                    font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                { X = 8, Y = 7 });
                string position = source.HasWorldPosition
                    ? $"{Facet(source.MapIndex)} {source.RootX}, {source.RootY}" : "No stored house position";
                Add(new Label($"{source.ItemCount} items  •  {Age(source.LastSeenUtcTicks)}  •  {position}", true,
                    AgeHue(source.LastSeenUtcTicks), width - 116, font: 1,
                    style: FontStyle.BlackBorder | FontStyle.Cropped)
                { X = 8, Y = 31 });
                Add(Button(width - 102, 14, 94, 30, "Forget", 100 + index));
            }

            private static string Age(long ticks)
            {
                if (ticks <= 0) return "age unknown";
                TimeSpan age = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
                if (age.TotalMinutes < 2) return "seen now";
                if (age.TotalHours < 2) return $"seen {(int)age.TotalMinutes}m ago";
                if (age.TotalDays < 2) return $"seen {(int)age.TotalHours}h ago";
                return $"seen {(int)age.TotalDays}d ago";
            }

            private static ushort AgeHue(long ticks)
            {
                if (ticks <= 0) return 0x0035;
                return DateTime.UtcNow.Ticks - ticks > TimeSpan.TicksPerDay * 30
                    ? (ushort)0x0021 : FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.ItemFinder);
            }

            private static string Facet(int map) => map == 0 ? "Felucca" : map == 1 ? "Trammel"
                : map == 2 ? "Ilshenar" : map == 3 ? "Malas" : map == 4 ? "Tokuno"
                : map == 5 ? "Ter Mur" : $"Facet {map}";

            private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
                FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                    FeatureGumpArtworkKind.ItemFinder);
        }
    }
}

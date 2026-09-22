// TazUO addition: ordered restock sources; first source is consumed first.

using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class RestockSourcePriorityGump : Gump
    {
        private const int WIDTH = 560;
        private const int HEIGHT = 430;
        private readonly VBoxContainer _rows;
        private readonly Label _title;
        private readonly RestockAgentGump _owner;

        internal RestockSourcePriorityGump(RestockAgentGump owner) : base(0, 0)
        {
            _owner = owner;
            X = owner?.X + 45 ?? 260;
            Y = owner?.Y + 45 ?? 160;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Add(FeatureGumpArtwork.CreateBackground(WIDTH, HEIGHT,
                FeatureGumpArtworkKind.RestockAgent, 0.97f));
            Add(_title = new Label("SOURCE PRIORITY", true,
                FeatureGumpArtwork.TitleHue(FeatureGumpArtworkKind.RestockAgent), 450, font: 1)
            { X = 24, Y = 18 });
            Add(new Label("Restock consumes the first available source from top to bottom.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 480, font: 1)
            { X = 24, Y = 40 });
            Add(Button(508, 16, 28, 28, "X", 99));
            Add(FeatureGumpArtwork.CreateSurface(24, 72, 512, 304,
                FeatureGumpArtworkKind.RestockAgent, 0.40f));
            var scroll = new ScrollArea(28, 76, 504, 296, true)
            { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            scroll.Add(_rows = new VBoxContainer(scroll.Width - scroll.ScrollBarWidth() - 3, 0, 2));
            Add(scroll);
            Add(new Label("Source order is stored with each saved loadout.", true,
                FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), 500, font: 1)
            { X = 24, Y = 394 });
            Rebuild();
            SetInScreen();
        }

        public override bool ShouldBeSaved => false;

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == 99) { Dispose(); return; }
            int action = buttonID / 1000;
            int index = buttonID % 1000;
            bool changed = action == 1 ? RestockAgentManager.MoveSource(index, -1)
                : action == 2 ? RestockAgentManager.MoveSource(index, 1)
                : action == 3 && RestockAgentManager.RemoveSourceAt(index);
            if (changed)
            {
                Rebuild();
                _owner?.RefreshLoadoutState("Restock source priority updated.");
            }
        }

        private void Rebuild()
        {
            _rows.Clear();
            _title.Text = $"SOURCE PRIORITY  •  {RestockAgentManager.Settings.SourceSerials.Count}";
            for (int i = 0; i < RestockAgentManager.Settings.SourceSerials.Count; i++)
                _rows.Add(new SourceRow(RestockAgentManager.Settings.SourceSerials[i], i, _rows.Width - 4));
            if (RestockAgentManager.Settings.SourceSerials.Count == 0)
                _rows.Add(new Label("No sources selected.", true,
                    FeatureGumpArtwork.DimHue(FeatureGumpArtworkKind.RestockAgent), _rows.Width - 8, font: 1));
        }

        private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
            FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                FeatureGumpArtworkKind.RestockAgent);

        private sealed class SourceRow : Control
        {
            internal SourceRow(uint serial, int index, int width)
            {
                Width = width;
                Height = 50;
                Add(FeatureGumpArtwork.CreateSurface(0, 0, width, 48,
                    FeatureGumpArtworkKind.RestockAgent, 0.36f));
                Item item = World.Items.Get(serial);
                string name = item == null ? $"Unavailable 0x{serial:X8}"
                    : !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.ItemData.Name;
                Add(new Label($"{index + 1}. {name}", true,
                    item == null ? (ushort)0x0021 : FeatureGumpArtwork.TextHue(FeatureGumpArtworkKind.RestockAgent),
                    width - 160, font: 1, style: FontStyle.BlackBorder | FontStyle.Cropped)
                { X = 8, Y = 14 });
                Add(Button(width - 150, 10, 42, 28, "↑", 1000 + index));
                Add(Button(width - 104, 10, 42, 28, "↓", 2000 + index));
                Add(Button(width - 56, 10, 48, 28, "X", 3000 + index));
            }

            private static NiceButton Button(int x, int y, int width, int height, string text, int id) =>
                FeatureGumpArtwork.CreateButton(x, y, width, height, text, id,
                    FeatureGumpArtworkKind.RestockAgent);
        }
    }
}

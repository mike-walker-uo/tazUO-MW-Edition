// TazUO addition: paperdoll-native launcher and on-demand feature menu.

using System;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PaperDollFeatureToolsGump : Gump
    {
        private const int WIDTH = 308;
        private const int HEIGHT = 190;
        private readonly Profile _profile;
        private readonly string _profilePath;
        private int _lastX;
        private int _lastY;
        private CustomGumpTheme _lastTheme;
        private long _nextRefresh;
        private int _lastAlertCount = -1;
        private bool _lastReady;

        internal PaperDollFeatureToolsGump(Gump owner) : base(0, 0)
        {
            _profile = ProfileManager.CurrentProfile;
            _profilePath = ProfileManager.ProfilePath;
            CanMove = true;
            CanCloseWithRightClick = true;
            CanBeLocked = false;
            AcceptMouseInput = true;
            Width = WIDTH;
            Height = HEIGHT;
            WantUpdateSize = false;

            Point saved = _profile?.PaperdollToolsPosition ?? Point.Zero;
            if (saved != Point.Zero)
            {
                Location = saved;
            }
            else if (owner != null)
            {
                int right = owner.X + owner.Width + 4;
                X = right + WIDTH <= Client.Game.Window.ClientBounds.Width
                    ? right
                    : System.Math.Max(0, owner.X - WIDTH - 4);
                Y = owner.Y + 18;
            }

            SetInScreen();
            _lastX = X;
            _lastY = Y;
            Build();
        }

        public override bool ShouldBeSaved => false;

        public override void Update()
        {
            if (World.Player == null)
            {
                Dispose();
                return;
            }

            if (CustomGumpThemeManager.Current != _lastTheme)
                Build();

            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + 1000;
                int alerts = AlertCenterManager.History.Count(entry => entry.Active);
                bool ready = ReadinessCheckManager.Evaluate().IsReady;

                if (alerts != _lastAlertCount || ready != _lastReady)
                    Build();
            }

            if (X != _lastX || Y != _lastY)
            {
                _lastX = X;
                _lastY = Y;
                if (_profile != null)
                    _profile.PaperdollToolsPosition = Location;
            }

            base.Update();
        }

        public override void Dispose()
        {
            if (_profile != null)
            {
                _profile.PaperdollToolsPosition = Location;
                if (!string.IsNullOrEmpty(_profilePath))
                    _profile.Save(_profilePath, false);
            }

            base.Dispose();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case 1: Open<WorldExplorerGump>(() => new WorldExplorerGump()); break;
                case 2: Open<ItemFinderGump>(() => new ItemFinderGump()); break;
                case 3: Open<AlertCenterGump>(() => new AlertCenterGump()); break;
                case 4: Open<RestockAgentGump>(() => new RestockAgentGump()); break;
                case 5: Open<EquipmentGuruGump>(() => new EquipmentGuruGump()); break;
                case 9: Dispose(); break;
                default: return;
            }
        }

        private void Build()
        {
            Clear();
            int alerts = AlertCenterManager.History.Count(entry => entry.Active);
            bool ready = ReadinessCheckManager.Evaluate().IsReady;
            _lastAlertCount = alerts;
            _lastReady = ready;
            _lastTheme = CustomGumpThemeManager.Current;

            Add(CustomGumpThemeManager.CreateBackground(WIDTH, HEIGHT, 0.90f));
            Add(new Label("TOOLS", true, CustomGumpThemeManager.TitleHue, 220, font: 1)
            {
                X = 24,
                Y = 18,
                AcceptMouseInput = false
            });
            Add(Button(264, 16, 20, 22, "X", 9, "Close tools panel"));

            Add(Button(24, 64, 126, 30, "World Explorer", 1));
            Add(Button(158, 64, 126, 30, "Item Finder", 2));
            Add(Button(24, 100, 126, 30, alerts == 0 ? "Alert Center" : $"Alerts ({alerts})", 3));
            Add(Button(158, 100, 126, 30, "Restock Agent", 4,
                ready ? "Readiness check: ready" : "Readiness check: attention needed"));
            Add(Button(24, 136, 260, 30, "Equipment Guru", 5));
        }

        private static NiceButton Button(
            int x, int y, int width, int height, string text, int id, string tooltip = null)
        {
            NiceButton button = new NiceButton(x, y, width, height, ButtonAction.Activate,
                text, hue: CustomGumpThemeManager.TextHue, font: 1)
            {
                ButtonParameter = id,
                IsSelectable = false,
                AlwaysShowBackground = true,
                DisplayBorder = true,
                BackgroundColor = CustomGumpThemeManager.OptionsSurfaceColor,
                BorderColor = CustomGumpThemeManager.OptionsSelectionColor,
                Alpha = 0.92f,
                HoverOverlayColor = Color.White,
                HoverOverlayAlpha = 0.25f
            };
            CustomGumpThemeManager.StyleButton(button);
            if (!string.IsNullOrEmpty(tooltip))
                button.SetTooltip(tooltip);
            return button;
        }

        private static void Open<T>(System.Func<T> factory) where T : Gump
        {
            T existing = UIManager.GetGump<T>();
            if (existing != null && !existing.IsDisposed)
            {
                existing.BringOnTop();
                return;
            }

            UIManager.Add(factory());
        }
    }

    internal sealed class PaperDollToolsLauncherButton : NiceButton
    {
        private static Texture2D _caption;
        private static bool _captionAttempted;

        internal PaperDollToolsLauncherButton(int x, int y, int width, int height, int id)
            : base(x, y, NativeWidth(width), NativeHeight(height), ButtonAction.Activate, "TOOLS",
                hue: 0xFFFF, font: 1)
        {
            ButtonParameter = id;
            IsSelectable = false;
            AlwaysShowBackground = false;
            DisplayBorder = false;
            Alpha = 0f;
            SetTooltip("Open feature tools");
            TextLabel.IsVisible = GetCaption() == null;
        }

        private static int NativeWidth(int fallback)
        {
            ref readonly var art = ref Client.Game.Gumps.GetGump(0x07EF);
            return art.Texture != null ? art.UV.Width : fallback;
        }

        private static int NativeHeight(int fallback)
        {
            ref readonly var art = ref Client.Game.Gumps.GetGump(0x07EF);
            return art.Texture != null ? art.UV.Height : fallback;
        }

        public override Control ScaleWidthAndHeight(double scale)
        {
            base.ScaleWidthAndHeight(scale);
            TextLabel.Width = Width;
            TextLabel.Y = (Height - TextLabel.Height) >> 1;
            return this;
        }

        public override void Update()
        {
            base.Update();
            if (_caption != null && _caption.IsDisposed)
                TextLabel.IsVisible = GetCaption() == null;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            // Paperdoll HELP artwork has the matching blue oval and gold frame.
            // Its caption is baked into the gump, so repeat a clean interior column.
            ref readonly var art = ref Client.Game.Gumps.GetGump(
                MouseIsOver ? (ushort)0x07F1 : (ushort)0x07EF);
            if (art.Texture != null)
            {
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
                batcher.Draw(art.Texture, new Rectangle(x, y, Width, Height), art.UV, hue);

                int left = Width * 23 / 100;
                int right = Width * 77 / 100;
                var cleanColumn = new Rectangle(
                    art.UV.X + art.UV.Width * 23 / 100,
                    art.UV.Y, 1, art.UV.Height);
                batcher.Draw(art.Texture,
                    new Rectangle(x + left, y, right - left, Height),
                    cleanColumn, hue);

                if (_caption != null && !_caption.IsDisposed)
                {
                    int captionWidth = _caption.Width * Width / art.UV.Width;
                    int captionHeight = _caption.Height * Height / art.UV.Height;
                    batcher.Draw(_caption, new Rectangle(
                        x + (Width - captionWidth) / 2 + 1,
                        y + (Height - captionHeight) / 2 + 2,
                        captionWidth, captionHeight), hue);
                }
            }

            TextLabel.IsVisible = art.Texture == null || _caption == null || _caption.IsDisposed;
            return base.Draw(batcher, x, y);
        }

        private static Texture2D GetCaption()
        {
            if (_caption != null && !_caption.IsDisposed)
                return _caption;
            if (_caption != null && _caption.IsDisposed)
                _captionAttempted = false;
            if (_captionAttempted)
                return null;

            _captionAttempted = true;
            // Letter pixels come from the paperdoll's STATUS, OPTIONS and SKILLS art.
            // Offsets are relative to each caption's first letter at native gump size.
            if (!TryGlyph(2027, 7, 9, out Glyph t)
                || !TryGlyph(2006, 0, 9, out Glyph o)
                || !TryGlyph(2015, 26, 9, out Glyph l)
                || !TryGlyph(2027, 0, 7, out Glyph s))
                return null;

            Glyph[] letters = { t, o, o, l, s };
            int gap = Math.Max(1, t.SourceWidth * 2 / 94);
            int width = letters.Sum(glyph => glyph.Width) + gap * 4 + 1;
            int height = letters.Max(glyph => glyph.Height) + 1;
            var pixels = new Color[width * height];
            int pen = 0;

            foreach (Glyph glyph in letters)
            {
                for (int gy = 0; gy < glyph.Height; gy++)
                {
                    for (int gx = 0; gx < glyph.Width; gx++)
                    {
                        Color color = glyph.Pixels[gy * glyph.Width + gx];
                        if (color.A == 0) continue;
                        pixels[(gy + 1) * width + pen + gx + 1] = new Color(0, 0, 0, 190);
                        pixels[gy * width + pen + gx] = color;
                    }
                }
                pen += glyph.Width + gap;
            }

            _caption = new Texture2D(Client.Game.GraphicsDevice, width, height,
                false, SurfaceFormat.Color);
            _caption.SetData(pixels);
            return _caption;
        }

        private static bool TryGlyph(ushort graphic, int offsetAt94, int widthAt94,
            out Glyph glyph)
        {
            glyph = default;
            ref readonly var art = ref Client.Game.Gumps.GetGump(graphic);
            if (art.Texture == null || art.UV.Width < 30 || art.UV.Height < 12)
                return false;

            int width = art.UV.Width;
            int height = art.UV.Height;
            var source = new Color[width * height];
            art.Texture.GetData(0, art.UV, source, 0, source.Length);

            int firstX = -1;
            int top = height;
            int yMin = height / 4;
            int yMax = height * 3 / 4;
            for (int sx = width / 10; sx < width * 9 / 10; sx++)
            {
                int bright = 0;
                for (int sy = yMin; sy < yMax; sy++)
                    if (IsLetterPixel(source[sy * width + sx])) bright++;
                if (bright < 3) continue;
                firstX = sx;
                break;
            }
            if (firstX < 0) return false;

            int firstLetterEnd = Math.Min(width, firstX + Math.Max(6, width * 12 / 94));
            for (int sx = firstX; sx < firstLetterEnd; sx++)
                for (int sy = yMin; sy < yMax; sy++)
                    if (IsLetterPixel(source[sy * width + sx]))
                        top = Math.Min(top, sy);
            if (top == height) return false;

            int glyphX = firstX + (offsetAt94 * width + 47) / 94;
            int glyphWidth = Math.Max(3, (widthAt94 * width + 47) / 94);
            int glyphHeight = Math.Min(height - top,
                Math.Max(4, (height * 11 + 29) / 30));
            if (glyphX + glyphWidth > width) return false;

            var glyphPixels = new Color[glyphWidth * glyphHeight];
            int visiblePixels = 0;
            for (int gy = 0; gy < glyphHeight; gy++)
                for (int gx = 0; gx < glyphWidth; gx++)
                {
                    Color color = source[(top + gy) * width + glyphX + gx];
                    if (IsLetterPixel(color))
                    {
                        glyphPixels[gy * glyphWidth + gx] = color;
                        visiblePixels++;
                    }
                }
            if (visiblePixels == 0) return false;

            glyph = new Glyph(glyphWidth, glyphHeight, width, glyphPixels);
            return true;
        }

        private static bool IsLetterPixel(Color color)
        {
            int low = Math.Min(color.R, Math.Min(color.G, color.B));
            int high = Math.Max(color.R, Math.Max(color.G, color.B));
            return color.A > 100 && low > 130 && high - low < 75;
        }

        private readonly struct Glyph
        {
            internal readonly int Width;
            internal readonly int Height;
            internal readonly int SourceWidth;
            internal readonly Color[] Pixels;

            internal Glyph(int width, int height, int sourceWidth, Color[] pixels)
            {
                Width = width;
                Height = height;
                SourceWidth = sourceWidth;
                Pixels = pixels;
            }
        }
    }
}

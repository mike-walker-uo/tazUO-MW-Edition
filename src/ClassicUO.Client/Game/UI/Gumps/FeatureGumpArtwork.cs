// TazUO addition: fixed feature-specific artwork independent of the global gump theme.

using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal enum FeatureGumpArtworkKind
    {
        ItemFinder,
        RestockAgent,
        AlertCenter,
        EquipmentGuru
    }

    internal sealed class FeatureGumpArtwork : Control
    {
        private static readonly Texture2D[] _textures = new Texture2D[4];
        private readonly FeatureGumpArtworkKind _kind;
        private readonly float _alpha;

        internal FeatureGumpArtwork(
            int width,
            int height,
            FeatureGumpArtworkKind kind,
            float alpha)
        {
            Width = width;
            Height = height;
            _kind = kind;
            _alpha = alpha;
            AcceptMouseInput = false;
        }

        internal static Control CreateBackground(
            int width,
            int height,
            FeatureGumpArtworkKind kind,
            float alpha)
        {
            return new FeatureGumpArtwork(width, height, kind, alpha);
        }

        internal static ushort TitleHue(FeatureGumpArtworkKind kind) => CustomGumpThemeManager.HasWindowOverride ? CustomGumpThemeManager.TitleHue : (ushort)0xFFFF;
        internal static ushort TextHue(FeatureGumpArtworkKind kind) => CustomGumpThemeManager.HasWindowOverride ? CustomGumpThemeManager.TextHue : (ushort)0x0481;
        internal static ushort DimHue(FeatureGumpArtworkKind kind) => CustomGumpThemeManager.HasWindowOverride ? CustomGumpThemeManager.DimHue : (ushort)0x03B2;

        internal static ushort AccentHue(FeatureGumpArtworkKind kind)
        {
            return kind == FeatureGumpArtworkKind.AlertCenter ? (ushort)0x0035 : (ushort)0x0044;
        }

        internal static Color BorderColor(FeatureGumpArtworkKind kind)
        {
            switch (kind)
            {
                case FeatureGumpArtworkKind.RestockAgent: return new Color(137, 101, 48);
                case FeatureGumpArtworkKind.AlertCenter: return new Color(151, 45, 42);
                case FeatureGumpArtworkKind.EquipmentGuru: return new Color(58, 145, 151);
                default: return new Color(102, 151, 145);
            }
        }

        internal static AlphaBlendControl CreateSurface(
            int x, int y, int width, int height, FeatureGumpArtworkKind kind,
            float alpha, bool input = false)
        {
            Color color;

            switch (kind)
            {
                case FeatureGumpArtworkKind.RestockAgent: color = new Color(20, 13, 7); break;
                case FeatureGumpArtworkKind.AlertCenter: color = new Color(20, 5, 8); break;
                case FeatureGumpArtworkKind.EquipmentGuru: color = new Color(8, 18, 20); break;
                default: color = new Color(8, 13, 14); break;
            }

            var surface = new AlphaBlendControl(
                MathHelper.Clamp(alpha * CustomGumpThemeManager.OpacityScale, 0f, 1f))
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                BaseColor = color,
                AcceptMouseInput = false
            };
            if (CustomGumpThemeManager.HasWindowOverride) CustomGumpThemeManager.ApplyDataSurface(surface, surface.Alpha, false);
            return surface;
        }

        internal static NiceButton CreateButton(
            int x, int y, int width, int height, string text, int id,
            FeatureGumpArtworkKind kind)
        {
            var button = new FeatureButton(x, y, width, height, text, id, kind);
            if (CustomGumpThemeManager.HasWindowOverride) CustomGumpThemeManager.StyleDataButton(button);
            return button;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (CustomGumpThemeManager.HasWindowOverride)
            {
                var theme = CustomGumpThemeManager.Current;
                if (CustomGumpThemeManager.IsArtTheme(theme)) CustomThemeArt.DrawPanel(batcher, x, y, Width, Height, theme, _alpha * CustomGumpThemeManager.OpacityScale);
                else CustomThemeArt.DrawPlain(batcher, x, y, Width, Height, theme, _alpha * CustomGumpThemeManager.OpacityScale, false);
                return true;
            }
            if (ProfileManager.CurrentProfile?.ReducedThemeDecoration == true)
            {
                Color surface = _kind == FeatureGumpArtworkKind.RestockAgent ? new Color(20, 13, 7)
                    : _kind == FeatureGumpArtworkKind.AlertCenter ? new Color(20, 5, 8)
                    : _kind == FeatureGumpArtworkKind.EquipmentGuru ? new Color(8, 18, 20) : new Color(8, 13, 14);
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, _alpha * CustomGumpThemeManager.OpacityScale);
                batcher.Draw(SolidColorTextureCache.GetTexture(surface), new Rectangle(x, y, Width, Height), hue);
                batcher.DrawRectangle(SolidColorTextureCache.GetTexture(BorderColor(_kind)), x, y, Width, Height, hue);
                return true;
            }
            Texture2D texture = GetTexture(_kind);

            if (texture != null)
            {
                float alpha = MathHelper.Clamp(
                    _alpha * CustomGumpThemeManager.OpacityScale,
                    0f,
                    1f
                );
                batcher.Draw(
                    texture,
                    new Rectangle(x, y, Width, Height),
                    ShaderHueTranslator.GetHueVector(0, false, alpha)
                );
            }

            return true;
        }

        private static Texture2D GetTexture(FeatureGumpArtworkKind kind)
        {
            int index = (int)kind;

            if (_textures[index] != null && !_textures[index].IsDisposed)
            {
                return _textures[index];
            }

            string folder;

            switch (kind)
            {
                case FeatureGumpArtworkKind.RestockAgent:
                    folder = "RestockAgent";
                    break;
                case FeatureGumpArtworkKind.AlertCenter:
                    folder = "AlertCenter";
                    break;
                case FeatureGumpArtworkKind.EquipmentGuru:
                    folder = "EquipmentGuru";
                    break;
                default:
                    folder = "ItemFinder";
                    break;
            }

            string name = $"ClassicUO.Resources.{folder}.panel.png";

            using (Stream stream = typeof(FeatureGumpArtwork).Assembly.GetManifestResourceStream(name))
            {
                if (stream != null)
                {
                    _textures[index] = Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                    OptionalTextureCache.Register(_textures[index], () => _textures[index] = null);
                }
            }

            return _textures[index];
        }
    }

    internal sealed class FeatureButton : NiceButton
    {
        private readonly FeatureGumpArtworkKind _kind;

        internal FeatureButton(int x, int y, int width, int height, string text, int id,
            FeatureGumpArtworkKind kind)
            : base(x, y, width, height, ButtonAction.Activate, text,
                hue: FeatureGumpArtwork.TextHue(kind), font: 1)
        {
            _kind = kind;
            ButtonParameter = id;
            IsSelectable = false;
            AlwaysShowBackground = false;
            DisplayBorder = false;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (CustomGumpThemeManager.HasWindowOverride) return base.Draw(batcher, x, y);
            ArtStyle = false; AlwaysShowBackground = false; DisplayBorder = false;
            Color fill;
            Color inner;

            switch (_kind)
            {
                case FeatureGumpArtworkKind.RestockAgent:
                    fill = MouseIsOver ? new Color(76, 50, 20) : new Color(35, 24, 13);
                    inner = new Color(173, 126, 52);
                    break;
                case FeatureGumpArtworkKind.AlertCenter:
                    fill = MouseIsOver ? new Color(82, 18, 28) : new Color(36, 10, 15);
                    inner = new Color(174, 49, 53);
                    break;
                case FeatureGumpArtworkKind.EquipmentGuru:
                    fill = MouseIsOver ? new Color(20, 62, 66) : new Color(11, 31, 34);
                    inner = new Color(65, 164, 170);
                    break;
                default:
                    fill = MouseIsOver ? new Color(28, 59, 58) : new Color(15, 30, 30);
                    inner = new Color(94, 151, 143);
                    break;
            }

            float alpha = MathHelper.Clamp(CustomGumpThemeManager.OpacityScale, 0f, 1f);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            batcher.Draw(SolidColorTextureCache.GetTexture(fill),
                new Rectangle(x, y, Width, Height), hue);
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(
                FeatureGumpArtwork.BorderColor(_kind)), x, y, Width, Height, hue);

            if (Width > 4 && Height > 4 && ProfileManager.CurrentProfile?.ReducedThemeDecoration != true)
            {
                batcher.DrawRectangle(SolidColorTextureCache.GetTexture(inner),
                    x + 2, y + 2, Width - 4, Height - 4,
                    ShaderHueTranslator.GetHueVector(0, false, alpha * 0.45f));

                Texture2D metal = SolidColorTextureCache.GetTexture(inner);
                batcher.Draw(metal, new Rectangle(x + 4, y + 4, 2, 2), hue);
                batcher.Draw(metal, new Rectangle(x + Width - 6, y + 4, 2, 2), hue);
                batcher.Draw(metal, new Rectangle(x + 4, y + Height - 6, 2, 2), hue);
                batcher.Draw(metal,
                    new Rectangle(x + Width - 6, y + Height - 6, 2, 2), hue);
            }

            return base.Draw(batcher, x, y);
        }
    }
}

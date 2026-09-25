using System.IO;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps.Login
{
    internal static class LoginArt
    {
        private static Texture2D _chest;
        private static Texture2D _logo;
        private static Texture2D _globe;
        private static Texture2D _dragonLogo;
        private static Texture2D _wood;
        private static Texture2D _stone;
        private static Texture2D _button;

        internal static Texture2D Chest => _chest ?? (_chest = Load("login-chest-dim"));
        internal static Texture2D Logo => _logo ?? (_logo = Load("login-logo-clean-240"));
        internal static Texture2D Globe => _globe ?? (_globe = Load("shard-globe"));
        internal static Texture2D DragonLogo => _dragonLogo ?? (_dragonLogo = Load("entering-dragon-logo"));
        internal static Texture2D Wood => _wood ?? (_wood = Load("wood-board"));
        internal static Texture2D Stone => _stone ?? (_stone = Load("stone-panel"));
        internal static Texture2D Button => _button ?? (_button = Load("metal-button"));

        private static Texture2D Load(string name)
        {
            using (Stream stream = typeof(LoginArt).Assembly.GetManifestResourceStream(
                "ClassicUO.Resources.HdLoginScreens." + name + ".png"))
                return stream == null ? null : Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
        }
    }

    internal class LoginArtImage : Control
    {
        private readonly Texture2D _texture;

        internal LoginArtImage(Texture2D texture, int x, int y, int width, int height)
        {
            _texture = texture;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            AcceptMouseInput = false;
            WantUpdateSize = false;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (_texture == null || _texture.IsDisposed)
                return false;

            batcher.Draw(_texture, new Rectangle(x, y, Width, Height), _texture.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, Alpha));
            return base.Draw(batcher, x, y);
        }
    }

    internal class LoginStonePanel : Control
    {
        internal LoginStonePanel(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            AcceptMouseInput = false;
            WantUpdateSize = false;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            Texture2D texture = LoginArt.Stone;
            if (texture == null || texture.IsDisposed)
                return false;

            const int sourceEdge = 150;
            int edge = System.Math.Min(36, System.Math.Min(Width, Height) / 4);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, Alpha);

            for (int row = 0; row < 3; row++)
            {
                int sy = row == 0 ? 0 : row == 1 ? sourceEdge : texture.Height - sourceEdge;
                int sh = row == 1 ? texture.Height - sourceEdge * 2 : sourceEdge;
                int dy = row == 0 ? y : row == 1 ? y + edge : y + Height - edge;
                int dh = row == 1 ? Height - edge * 2 : edge;
                for (int col = 0; col < 3; col++)
                {
                    int sx = col == 0 ? 0 : col == 1 ? sourceEdge : texture.Width - sourceEdge;
                    int sw = col == 1 ? texture.Width - sourceEdge * 2 : sourceEdge;
                    int dx = col == 0 ? x : col == 1 ? x + edge : x + Width - edge;
                    int dw = col == 1 ? Width - edge * 2 : edge;
                    batcher.Draw(texture, new Rectangle(dx, dy, dw, dh),
                        new Rectangle(sx, sy, sw, sh), hue);
                }
            }

            return base.Draw(batcher, x, y);
        }
    }

    internal class LoginArtButton : Control
    {
        private readonly int _buttonId;
        private readonly TextBox _label;
        private bool _pressed;

        internal LoginArtButton(int buttonId, string caption, int x, int y, int width, int height)
        {
            _buttonId = buttonId;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            CanMove = false;
            WantUpdateSize = false;
            Add(_label = TextBox.GetOne(caption, "avadonian", 15,
                new Color(239, 225, 199), TextBox.RTLOptions.Default()));
            _label.X = (width - _label.MeasuredSize.X) / 2;
            _label.Y = (height - _label.MeasuredSize.Y) / 2;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            Texture2D texture = LoginArt.Button;
            if (texture != null && !texture.IsDisposed)
            {
                int edge = System.Math.Min(12, Width / 4);
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, _pressed ? 0.78f : Alpha);
                batcher.Draw(texture, new Rectangle(x, y, edge, Height),
                    new Rectangle(0, 145, 280, 500), hue);
                batcher.Draw(texture, new Rectangle(x + edge, y, Width - edge * 2, Height),
                    new Rectangle(280, 145, texture.Width - 560, 500), hue);
                batcher.Draw(texture, new Rectangle(x + Width - edge, y, edge, Height),
                    new Rectangle(texture.Width - 280, 145, 280, 500), hue);
            }
            else
            {
                batcher.Draw(SolidColorTextureCache.GetTexture(new Color(47, 42, 38)),
                    new Rectangle(x, y, Width, Height), new Rectangle(0, 0, 1, 1),
                    ShaderHueTranslator.GetHueVector(0, false, Alpha));
            }
            if (MouseIsOver)
                batcher.DrawRectangle(SolidColorTextureCache.GetTexture(new Color(203, 168, 95)),
                    x + 2, y + 2, Width - 4, Height - 4,
                    ShaderHueTranslator.GetHueVector(0, false, Alpha));
            return base.Draw(batcher, x, y);
        }

        protected override void OnMouseDown(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
                _pressed = true;
        }

        protected override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            bool wasPressed = _pressed;
            _pressed = false;
            if (button == MouseButtonType.Left && wasPressed && MouseIsOver)
            {
                OnButtonClick(_buttonId);
                Mouse.LastLeftButtonClickTime = 0;
                Mouse.CancelDoubleClick = true;
            }
        }
    }

    internal class LoginArtSurface : Control
    {
        private readonly Color _fill;

        internal LoginArtSurface(int x, int y, int width, int height, Color fill)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            _fill = fill;
            AcceptMouseInput = false;
            WantUpdateSize = false;
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            batcher.Draw(SolidColorTextureCache.GetTexture(_fill),
                new Rectangle(x, y, Width, Height),
                new Rectangle(0, 0, 1, 1), ShaderHueTranslator.GetHueVector(0, false, Alpha));
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(new Color(173, 143, 92)),
                x, y, Width, Height, ShaderHueTranslator.GetHueVector(0, false, Alpha));
            return base.Draw(batcher, x, y);
        }
    }
}

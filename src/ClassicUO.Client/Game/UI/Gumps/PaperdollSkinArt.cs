// TazUO addition: selectable high-resolution paperdoll frames and buttons.

using System;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal enum PaperdollSkin : byte
    {
        Classic,
        FollowTheme,
        Stone,
        Wood,
        Metal,
        Marble,
        Glass,
        Obsidian,
        Water,
        Ice,
        Emerald,
        Gold,
        StainedGlass
    }

    internal static class PaperdollSkinArt
    {
        private static readonly Texture2D[] _frames = new Texture2D[(int)PaperdollSkin.StainedGlass - (int)PaperdollSkin.Stone + 1];
        private static readonly Texture2D[] _materialButtons = new Texture2D[(int)PaperdollSkin.StainedGlass - (int)PaperdollSkin.Marble + 1];
        private static Texture2D _button;
        private static Texture2D _buttonHover;

        internal static PaperdollSkin Current
        {
            get
            {
                byte value = ProfileManager.CurrentProfile?.PaperdollSkin ?? 0;
                return value <= (byte)PaperdollSkin.StainedGlass ? (PaperdollSkin)value : PaperdollSkin.Classic;
            }
        }

        internal static void DrawPanel(UltimaBatcher2D batcher, int x, int y, int width, int height,
            PaperdollSkin skin, float alpha)
        {
            Texture2D frame = GetFrame(skin);
            if (frame == null)
            {
                DrawFlat(batcher, x, y, width, height, alpha);
                return;
            }

            // Crop transparent margins while keeping the paperdoll's 260:320 layout.
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(frame, new Rectangle(x, y, width, height),
                new Rectangle(65, 8, frame.Width - 130, frame.Height - 48),
                ShaderHueTranslator.GetHueVector(0, false, alpha));
            batcher.SetSampler(null);
        }

        internal static void DrawButton(UltimaBatcher2D batcher, int x, int y, int width, int height,
            PaperdollSkin skin, float alpha, bool hovered = false)
        {
            if (skin == PaperdollSkin.FollowTheme && CustomGumpThemeManager.IsArtTheme(CustomGumpThemeManager.Current))
                CustomThemeArt.DrawButton(batcher, x, y, width, height, alpha);
            else if (skin >= PaperdollSkin.Marble)
                DrawMaterialButton(batcher, x, y, width, height, skin, alpha, hovered);
            else if (skin >= PaperdollSkin.Stone)
            {
                Texture2D button = GetButton(hovered);
                if (button == null)
                    DrawFlat(batcher, x, y, width, height, alpha);
                else
                {
                    batcher.SetSampler(SamplerState.LinearClamp);
                    batcher.Draw(button, new Rectangle(x, y, width, height),
                        new Rectangle(65, 165, button.Width - 130, 445),
                        ShaderHueTranslator.GetHueVector(0, false, alpha));
                    batcher.SetSampler(null);
                }
            }
            else
                DrawFlat(batcher, x, y, width, height, alpha);

            if (hovered && skin == PaperdollSkin.FollowTheme)
                batcher.Draw(SolidColorTextureCache.GetTexture(Color.White),
                    new Rectangle(x, y, width, height), ShaderHueTranslator.GetHueVector(0, false, 0.25f));
        }

        private static void DrawFlat(UltimaBatcher2D batcher, int x, int y, int width, int height, float alpha)
        {
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            batcher.Draw(SolidColorTextureCache.GetTexture(CustomGumpThemeManager.OptionsSurfaceColor),
                new Rectangle(x, y, width, height), hue);
            batcher.DrawRectangle(SolidColorTextureCache.GetTexture(CustomGumpThemeManager.OptionsSelectionColor),
                x, y, width, height, hue);
        }

        internal static Color TitleTextColor(PaperdollSkin skin)
        {
            return skin == PaperdollSkin.Marble || skin == PaperdollSkin.Ice || skin == PaperdollSkin.Gold
                ? new Color(32, 33, 37)
                : Color.WhiteSmoke;
        }

        internal static Color ButtonTextColor(PaperdollSkin skin)
        {
            return skin == PaperdollSkin.Obsidian
                ? new Color(32, 33, 37)
                : Color.WhiteSmoke;
        }

        private static void DrawMaterialButton(UltimaBatcher2D batcher, int x, int y, int width,
            int height, PaperdollSkin skin, float alpha, bool hovered)
        {
            Texture2D button = GetMaterialButton(skin);
            if (button == null)
            {
                DrawFlat(batcher, x, y, width, height, alpha);
                return;
            }

            Rectangle source = new Rectangle(65, 165, button.Width - 130, 445);
            Rectangle destination = new Rectangle(x, y, width, height);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(button, destination, source, ShaderHueTranslator.GetHueVector(0, false, alpha));
            if (hovered)
                batcher.Draw(button, destination, source,
                    ShaderHueTranslator.GetHueVector(0x0481, false, alpha * 0.22f));
            batcher.SetSampler(null);
        }

        private static Texture2D GetFrame(PaperdollSkin skin)
        {
            if ((byte)skin < (byte)PaperdollSkin.Stone || (byte)skin > (byte)PaperdollSkin.StainedGlass)
                return null;

            int index = (int)skin - (int)PaperdollSkin.Stone;
            if (_frames[index] != null)
                return _frames[index];

            string name = skin == PaperdollSkin.Gold ? "gold-polished" : skin.ToString().ToLowerInvariant();
            using (Stream stream = typeof(PaperdollSkinArt).Assembly.GetManifestResourceStream(
                "ClassicUO.Resources.PaperdollSkins." + name + ".png"))
            {
                Texture2D frame = stream == null ? null : Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                if (skin == PaperdollSkin.Marble && frame != null)
                    CleanMarbleEdge(frame);
                return _frames[index] = frame;
            }
        }

        private static void CleanMarbleEdge(Texture2D frame)
        {
            // Remove the PNG's pale transparent fringe before linear filtering and alpha blending.
            Color[] pixels = new Color[frame.Width * frame.Height];
            frame.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                if (pixel.A < 16)
                    pixels[i] = Color.Transparent;
                else if (pixel.A < 255)
                    pixels[i] = new Color(
                        (byte)(pixel.R * pixel.A / 255),
                        (byte)(pixel.G * pixel.A / 255),
                        (byte)(pixel.B * pixel.A / 255), pixel.A);
            }
            frame.SetData(pixels);
        }

        private static Texture2D GetButton(bool hovered)
        {
            if (hovered ? _buttonHover != null : _button != null)
                return hovered ? _buttonHover : _button;

            string name = hovered ? "button-hover" : "button";
            using (Stream stream = typeof(PaperdollSkinArt).Assembly.GetManifestResourceStream(
                "ClassicUO.Resources.PaperdollSkins." + name + ".png"))
            {
                Texture2D button = stream == null ? null : Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
                if (hovered)
                    _buttonHover = button;
                else
                    _button = button;
                return button;
            }
        }

        private static Texture2D GetMaterialButton(PaperdollSkin skin)
        {
            int index = (int)skin - (int)PaperdollSkin.Marble;
            if (_materialButtons[index] != null)
                return _materialButtons[index];

            string name = skin == PaperdollSkin.Water || skin == PaperdollSkin.Emerald || skin == PaperdollSkin.Gold
                ? "button-graphite.png"
                : "button-" + skin.ToString().ToLowerInvariant() + ".png";
            using (Stream stream = typeof(PaperdollSkinArt).Assembly.GetManifestResourceStream(
                "ClassicUO.Resources.PaperdollSkins." + name))
                return _materialButtons[index] = stream == null ? null : Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
        }
    }
}

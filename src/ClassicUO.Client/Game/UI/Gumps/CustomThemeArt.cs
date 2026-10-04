using System;
using System.IO;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal static class CustomThemeArt
    {
        private static readonly Texture2D[] _panels = new Texture2D[CustomGumpThemeManager.ThemeSlotCount];
        private static readonly Texture2D[] _buttons = new Texture2D[CustomGumpThemeManager.ThemeSlotCount];
        private static readonly bool[] _panelLoaded = new bool[CustomGumpThemeManager.ThemeSlotCount];
        private static readonly bool[] _buttonLoaded = new bool[CustomGumpThemeManager.ThemeSlotCount];

        private static bool UsesSheet(CustomGumpTheme theme) =>
            theme == CustomGumpTheme.Stone || theme == CustomGumpTheme.Wood
            || theme == CustomGumpTheme.Heartwood || theme == CustomGumpTheme.Necropolis
            || theme >= CustomGumpTheme.Celestial;

        private static bool UsesLargeBorder(CustomGumpTheme theme) =>
            UsesSheet(theme) || theme == CustomGumpTheme.GildedGrove
            || theme == CustomGumpTheme.Aetherglass
            || theme == CustomGumpTheme.BritannianChronicle
            || theme == CustomGumpTheme.MoonglowArcane
            || theme == CustomGumpTheme.TerMurRelic
            || theme == CustomGumpTheme.MarinersChart;

        internal static int ContentInset => UsesLargeBorder(CustomGumpThemeManager.Current) ? 18 : 0;

        private static int SheetSplit(CustomGumpTheme theme, int height)
        {
            PremiumGumpTheme premium = PremiumGumpThemes.Get(theme);
            if (premium != null) return height * premium.SheetSplit / 1024;
            int row;
            switch (theme)
            {
                case CustomGumpTheme.Stone: row = 782; break;
                case CustomGumpTheme.Wood: row = 768; break;
                case CustomGumpTheme.Heartwood: row = 800; break;
                case CustomGumpTheme.Celestial: row = 796; break;
                case CustomGumpTheme.Exodus: row = 783; break;
                case CustomGumpTheme.BloodOath: row = 782; break;
                case CustomGumpTheme.Necropolis: row = 800; break;
                case CustomGumpTheme.HdStone: row = 796; break;
                case CustomGumpTheme.HdMarble: row = 796; break;
                case CustomGumpTheme.HdGlass:
                case CustomGumpTheme.HdStainedGlass: row = 796; break;
                case CustomGumpTheme.HdWood:
                case CustomGumpTheme.HdMetal: row = 768; break;
                default: row = 796; break;
            }
            return height * row / 1024;
        }

        private static string ResourceFolder(CustomGumpTheme theme)
        {
            switch (theme)
            {
                case CustomGumpTheme.Stone: return "Runestone";
                case CustomGumpTheme.Wood: return "OakAndIron";
                case CustomGumpTheme.Heartwood: return "HeartwoodSanctuary";
                case CustomGumpTheme.Necropolis: return "NecromancersCrypt";
                case CustomGumpTheme.Celestial: return "Celestial";
                case CustomGumpTheme.Exodus: return "Exodus";
                case CustomGumpTheme.BloodOath: return "BloodOath";
                case CustomGumpTheme.Hildebrandt: return "Hildebrandt";
                case CustomGumpTheme.HdStone: return "HdStone";
                case CustomGumpTheme.HdWood: return "HdWood";
                case CustomGumpTheme.HdMetal: return "HdMetal";
                case CustomGumpTheme.HdMarble: return "HdMarble";
                case CustomGumpTheme.HdGlass: return "HdGlass";
                case CustomGumpTheme.HdStainedGlass: return "HdStainedGlass";
                case CustomGumpTheme.BritannianChronicle: return "BritannianChronicle";
                case CustomGumpTheme.MoonglowArcane: return "MoonglowArcane";
                case CustomGumpTheme.TerMurRelic: return "TerMurRelic";
                case CustomGumpTheme.MarinersChart: return "MarinersChart";
                case CustomGumpTheme.GildedGrove: return "GildedGroveTheme";
                case CustomGumpTheme.Aetherglass: return "AetherglassTheme";
                default: return "OrnateTheme";
            }
        }

        private static Texture2D GetPanel(CustomGumpTheme theme)
        {
            int index = (int)theme;
            if (_panelLoaded[index] && _panels[index]?.IsDisposed != true) return _panels[index];
            string name = PremiumGumpThemes.Get(theme) != null
                ? $"ClassicUO.Resources.PremiumThemes.{theme}.png"
                : $"ClassicUO.Resources.{ResourceFolder(theme)}.{(UsesSheet(theme) ? "sheet.png" : "panel.png")}";
            _panels[index] = Load(name);
            OptionalTextureCache.Register(_panels[index], () => { _panels[index] = null; _panelLoaded[index] = false; });
            _panelLoaded[index] = true;
            return _panels[index];
        }

        private static Texture2D GetButton(CustomGumpTheme theme)
        {
            if (UsesSheet(theme))
                return GetPanel(theme);
            int index = (int)theme;
            if (_buttonLoaded[index] && _buttons[index]?.IsDisposed != true) return _buttons[index];
            string name = theme == CustomGumpTheme.Ornate
                ? "ClassicUO.Resources.WorldExplorer.classic-button.png"
                : $"ClassicUO.Resources.{ResourceFolder(theme)}.button.png";
            _buttons[index] = Load(name);
            OptionalTextureCache.Register(_buttons[index], () => { _buttons[index] = null; _buttonLoaded[index] = false; });
            _buttonLoaded[index] = true;
            return _buttons[index];
        }

        private static Texture2D Load(string name)
        {
            using (Stream stream = typeof(CustomThemeArt).Assembly.GetManifestResourceStream(name))
                return stream == null ? null : Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
        }

        internal static void DrawPanel(UltimaBatcher2D batcher, int x, int y, int width, int height,
            CustomGumpTheme theme, float alpha = 1f)
        {
            if (ReducedDecoration) { DrawPlain(batcher, x, y, width, height, theme, alpha, false); return; }
            Texture2D texture = GetPanel(theme);
            if (texture == null)
                return;
            bool largeBorder = UsesLargeBorder(theme);
            int edge = PremiumGumpThemes.Get(theme) != null
                ? Math.Min(12, Math.Max(4, Math.Min(width, height) / 8))
                : largeBorder ? Math.Min(56, Math.Max(12, Math.Min(width, height) / 7))
                : Math.Min(18, Math.Max(6, Math.Min(width, height) / 8));
            int sourceEdge = PremiumGumpThemes.Get(theme) != null ? texture.Width * PremiumGumpThemes.Get(theme).PanelEdge / 1536
                : largeBorder ? 260 : theme == CustomGumpTheme.Ornate ? 96 : 160;
            Rectangle source = new Rectangle(0, 0, texture.Width,
                UsesSheet(theme) ? SheetSplit(theme, texture.Height) : texture.Height);
            DrawSlices(batcher, texture, source, x, y, width, height, sourceEdge, edge, false, alpha);
        }

        internal static void DrawPanel(UltimaBatcher2D batcher, int x, int y, int width, int height, float alpha = 1f) =>
            DrawPanel(batcher, x, y, width, height, CustomGumpThemeManager.Current, alpha);

        internal static bool DrawNameplateFill(UltimaBatcher2D batcher, int x, int y, int width, int height,
            CustomGumpTheme theme, float alpha)
        {
            if (ReducedDecoration) { DrawPlain(batcher, x, y, width, height, theme, alpha, false); return true; }
            Texture2D texture = GetPanel(theme);
            if (texture == null || width < 1 || height < 1)
                return false;

            int panelHeight = UsesSheet(theme) ? SheetSplit(theme, texture.Height) : texture.Height;
            int edge = PremiumGumpThemes.Get(theme) != null ? texture.Width * PremiumGumpThemes.Get(theme).PanelEdge / 1536
                : UsesLargeBorder(theme) ? 260 : theme == CustomGumpTheme.Ornate ? 96 : 160;
            int sourceWidth = Math.Min(width * 2, texture.Width - edge * 2);
            int sourceHeight = Math.Min(height * 2, panelHeight - edge * 2);
            if (sourceWidth < 1 || sourceHeight < 1)
                return false;

            Rectangle source = new Rectangle((texture.Width - sourceWidth) / 2,
                (panelHeight - sourceHeight) / 2, sourceWidth, sourceHeight);
            batcher.Draw(texture, new Rectangle(x, y, width, height), source,
                ShaderHueTranslator.GetHueVector(0, false, alpha));
            return true;
        }

        internal static void DrawFrame(UltimaBatcher2D batcher, int x, int y, int width, int height,
            CustomGumpTheme theme, float alpha = 1f, int maxEdge = 56)
        {
            if (ReducedDecoration) { DrawPlain(batcher, x, y, width, height, theme, alpha, true); return; }
            Texture2D texture = GetPanel(theme);
            if (texture == null)
                return;
            bool largeBorder = UsesLargeBorder(theme);
            int edge = Math.Min(maxEdge, PremiumGumpThemes.Get(theme) != null
                ? Math.Min(12, Math.Max(4, Math.Min(width, height) / 8))
                : largeBorder ? Math.Min(56, Math.Max(12, Math.Min(width, height) / 7))
                : Math.Min(18, Math.Max(6, Math.Min(width, height) / 8)));
            int sourceEdge = PremiumGumpThemes.Get(theme) != null ? texture.Width * PremiumGumpThemes.Get(theme).PanelEdge / 1536
                : largeBorder ? 260 : theme == CustomGumpTheme.Ornate ? 96 : 160;
            Rectangle source = new Rectangle(0, 0, texture.Width,
                UsesSheet(theme) ? SheetSplit(theme, texture.Height) : texture.Height);
            DrawSlices(batcher, texture, source, x, y, width, height, sourceEdge, edge, true, alpha);
        }

        internal static void DrawFrame(UltimaBatcher2D batcher, int x, int y, int width, int height, float alpha = 1f, int maxEdge = 56) =>
            DrawFrame(batcher, x, y, width, height, CustomGumpThemeManager.Current, alpha, maxEdge);

        internal static void DrawButton(UltimaBatcher2D batcher, int x, int y, int width, int height,
            CustomGumpTheme theme, float alpha = 1f)
        {
            if (ReducedDecoration) { DrawPlain(batcher, x, y, width, height, theme, alpha, false); return; }
            Texture2D texture = GetButton(theme);
            if (texture == null || width < 8 || height < 8)
                return;

            // Chronicle's button has wider transparent margins and a central jewel
            // that obscure text when the artwork is shrunk to a compact control.
            bool sheet = UsesSheet(theme);
            int split = sheet ? SheetSplit(theme, texture.Height) : 0;
            Rectangle source = sheet
                ? new Rectangle(0, split, texture.Width, texture.Height - split)
                : theme == CustomGumpTheme.BritannianChronicle
                ? new Rectangle(0, 160, texture.Width, 360)
                : new Rectangle(0, texture.Height / 7, texture.Width, texture.Height * 5 / 7);
            bool premium = PremiumGumpThemes.Get(theme) != null;
            int sourceEdge = Math.Min(premium ? texture.Width * 220 / 1536
                : sheet ? 300 : theme == CustomGumpTheme.Ornate ? 190 : 400, source.Width / 3);
            int edge = Math.Min(14, width / 3);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            for (int col = 0; col < 3; col++)
            {
                int sx = col == 0 ? source.X : col == 1 && premium
                    ? source.X + sourceEdge : col == 1 && sheet
                    ? source.X + 450 : col == 1
                    ? source.X + sourceEdge + (theme == CustomGumpTheme.BritannianChronicle ? 100 : 0)
                    : source.Right - sourceEdge;
                int sw = col == 1
                    ? premium ? source.Width - sourceEdge * 2
                    : sheet ? 250 : theme == CustomGumpTheme.BritannianChronicle ? 400 : source.Width - sourceEdge * 2
                    : sourceEdge;
                int dx = col == 0 ? x : col == 1 ? x + edge : x + width - edge;
                int dw = col == 1 ? width - edge * 2 : edge;
                batcher.Draw(texture, new Rectangle(dx, y, dw, height),
                    new Rectangle(sx, source.Y, sw, source.Height), hue);
            }
        }

        internal static void DrawButton(UltimaBatcher2D batcher, int x, int y, int width, int height, float alpha = 1f) =>
            DrawButton(batcher, x, y, width, height, CustomGumpThemeManager.Current, alpha);

        private static void DrawSlices(UltimaBatcher2D batcher, Texture2D texture, Rectangle source,
            int x, int y, int width, int height, int sourceEdge, int edge,
            bool frameOnly, float alpha)
        {
            if (texture == null || width < 2 || height < 2)
                return;

            int dxEdge = Math.Min(edge, width / 3);
            int dyEdge = Math.Min(edge, height / 3);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            for (int row = 0; row < 3; row++)
            {
                int sy = row == 0 ? source.Y : row == 1 ? source.Y + sourceEdge : source.Bottom - sourceEdge;
                int sh = row == 1 ? source.Height - sourceEdge * 2 : sourceEdge;
                int dy = row == 0 ? y : row == 1 ? y + dyEdge : y + height - dyEdge;
                int dh = row == 1 ? height - dyEdge * 2 : dyEdge;
                for (int col = 0; col < 3; col++)
                {
                    if (frameOnly && row == 1 && col == 1)
                        continue;

                    int sx = col == 0 ? source.X : col == 1 ? source.X + sourceEdge : source.Right - sourceEdge;
                    int sw = col == 1 ? source.Width - sourceEdge * 2 : sourceEdge;
                    int dx = col == 0 ? x : col == 1 ? x + dxEdge : x + width - dxEdge;
                    int dw = col == 1 ? width - dxEdge * 2 : dxEdge;
                    batcher.Draw(texture, new Rectangle(dx, dy, dw, dh),
                        new Rectangle(sx, sy, sw, sh), hue);
                }
            }
        }

        private static bool ReducedDecoration => ClassicUO.Configuration.ProfileManager.CurrentProfile?.ReducedThemeDecoration == true;

        internal static void DrawPlain(UltimaBatcher2D batcher, int x, int y, int width, int height,
            CustomGumpTheme theme, float alpha, bool frameOnly)
        {
            if (width < 2 || height < 2) return;
            PremiumGumpTheme premium = PremiumGumpThemes.Get(theme);
            Color surface = premium?.Surface ?? CustomGumpThemeManager.GetOptionsSurfaceColor(theme);
            Color accent = premium?.Accent ?? CustomGumpThemeManager.GetCompactBorderColor(theme);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            if (!frameOnly) batcher.Draw(SolidColorTextureCache.GetTexture(surface), new Rectangle(x, y, width, height), hue);
            Texture2D edge = SolidColorTextureCache.GetTexture(accent);
            batcher.Draw(edge, new Rectangle(x, y, width, 1), hue);
            batcher.Draw(edge, new Rectangle(x, y + height - 1, width, 1), hue);
            batcher.Draw(edge, new Rectangle(x, y, 1, height), hue);
            batcher.Draw(edge, new Rectangle(x + width - 1, y, 1, height), hue);
        }
    }
}

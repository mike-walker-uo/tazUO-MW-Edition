#region license
// TazUO addition. Shared visual themes for custom utility gumps.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    internal enum CustomGumpTheme : byte
    {
        Minimal,
        Classic,
        Stone,
        Wood,
        Dark,
        Royal,
        Forest,
        Dungeon,
        Water,
        Snow,
        Heartwood,
        TerMur,
        Kotl,
        TazUO,
        Britannia,
        Trinsic,
        Minoc,
        Blackthorn,
        Obsidian,
        Doom,
        Midnight,
        Necropolis,
        Ornate,
        BritannianChronicle,
        MoonglowArcane,
        TerMurRelic,
        MarinersChart,
        GildedGrove,
        Aetherglass,
        Celestial,
        Exodus,
        BloodOath,
        Hildebrandt
    }

    internal static class CustomGumpThemeManager
    {
        internal static CustomGumpTheme Current
        {
            get
            {
                byte value = ProfileManager.CurrentProfile?.CustomGumpTheme ?? 0;
                return value <= (byte)CustomGumpTheme.Hildebrandt
                    ? (CustomGumpTheme)value
                    : CustomGumpTheme.Minimal;
            }
        }

        internal static int ThemeCount => (int)CustomGumpTheme.Hildebrandt + 1;

        internal static bool IsArtTheme(CustomGumpTheme theme) =>
            theme == CustomGumpTheme.Stone || theme == CustomGumpTheme.Wood
            || theme == CustomGumpTheme.Heartwood || theme == CustomGumpTheme.Necropolis
            || theme >= CustomGumpTheme.Ornate && theme <= CustomGumpTheme.Hildebrandt;

        internal static float OpacityScale
        {
            get
            {
                int opacity = ProfileManager.CurrentProfile?.CustomGumpOpacity ?? 100;
                return Math.Max(20, Math.Min(100, opacity)) / 100f;
            }
        }

        internal static ushort TitleHue => Current == CustomGumpTheme.BritannianChronicle
            ? (ushort)0
            : Current == CustomGumpTheme.Classic ? (ushort)0x0386 : (ushort)0x0481;
        internal static ushort TextHue => GetTextHue(Current);
        internal static ushort DimHue => Current == CustomGumpTheme.Minimal
            || Current == CustomGumpTheme.TazUO
            ? (ushort)0x0386
            : Current == CustomGumpTheme.BritannianChronicle ? (ushort)0
            : Current == CustomGumpTheme.Classic ? (ushort)0x0453 : (ushort)0x0481;
        internal static ushort GroupHue => Current == CustomGumpTheme.BritannianChronicle
            ? (ushort)0
            : Current == CustomGumpTheme.Classic ? (ushort)0x0386 : (ushort)0x0044;
        internal static ushort ButtonHue => Current == CustomGumpTheme.Minimal
            ? (ushort)0x0386
            : Current == CustomGumpTheme.Classic || Current == CustomGumpTheme.BritannianChronicle
                ? (ushort)0
                : PanelHue;
        internal static ushort DataTextHue => GetTextHue(Current);
        internal static ushort CompactBorderHue => Current == CustomGumpTheme.Minimal ? (ushort)0 : PanelHue;

        internal static Color OptionsSurfaceColor
        {
            get
            {
                switch (Current)
                {
                    case CustomGumpTheme.Stone: return new Color(39, 40, 39);
                    case CustomGumpTheme.Wood: return new Color(45, 30, 21);
                    case CustomGumpTheme.Heartwood: return new Color(31, 46, 27);
                    case CustomGumpTheme.Necropolis: return new Color(29, 35, 31);
                    case CustomGumpTheme.BritannianChronicle: return new Color(232, 217, 184);
                    case CustomGumpTheme.MoonglowArcane: return new Color(22, 33, 49);
                    case CustomGumpTheme.TerMurRelic: return new Color(24, 40, 42);
                    case CustomGumpTheme.MarinersChart: return new Color(48, 36, 26);
                    case CustomGumpTheme.GildedGrove: return new Color(28, 43, 27);
                    case CustomGumpTheme.Aetherglass: return new Color(20, 29, 69);
                    case CustomGumpTheme.Celestial: return new Color(24, 37, 65);
                    case CustomGumpTheme.Exodus: return new Color(36, 31, 29);
                    case CustomGumpTheme.BloodOath: return new Color(43, 20, 20);
                    case CustomGumpTheme.Hildebrandt: return new Color(23, 38, 74);
                    default: return new Color(43, 32, 24);
                }
            }
        }

        internal static Color OptionsSelectionColor
        {
            get
            {
                switch (Current)
                {
                    case CustomGumpTheme.Stone: return new Color(78, 79, 76);
                    case CustomGumpTheme.Wood: return new Color(93, 63, 38);
                    case CustomGumpTheme.Heartwood: return new Color(67, 93, 52);
                    case CustomGumpTheme.Necropolis: return new Color(60, 78, 63);
                    case CustomGumpTheme.BritannianChronicle: return new Color(196, 171, 129);
                    case CustomGumpTheme.MoonglowArcane: return new Color(40, 68, 86);
                    case CustomGumpTheme.TerMurRelic: return new Color(43, 76, 75);
                    case CustomGumpTheme.MarinersChart: return new Color(102, 75, 45);
                    case CustomGumpTheme.GildedGrove: return new Color(56, 81, 43);
                    case CustomGumpTheme.Aetherglass: return new Color(65, 72, 153);
                    case CustomGumpTheme.Celestial: return new Color(55, 80, 125);
                    case CustomGumpTheme.Exodus: return new Color(101, 67, 48);
                    case CustomGumpTheme.BloodOath: return new Color(112, 42, 48);
                    case CustomGumpTheme.Hildebrandt: return new Color(65, 86, 151);
                    default: return new Color(89, 68, 49);
                }
            }
        }

        private static readonly List<ThemeSurface> _themeSurfaces = new List<ThemeSurface>();
        private static readonly List<ThemeColorBox> _themeColorBoxes = new List<ThemeColorBox>();
        private static readonly List<ThemeFrame> _themeFrames = new List<ThemeFrame>();

        private sealed class ThemeSurface
        {
            internal readonly WeakReference<AlphaBlendControl> Control;
            internal readonly Color OriginalColor;
            internal readonly ushort OriginalHue;
            internal readonly ushort OriginalMaterialGraphic;
            internal readonly ushort OriginalMaterialHue;
            internal readonly float OriginalMaterialAlpha;
            internal readonly AlphaBlendMaterialStyle OriginalMaterialStyle;
            internal readonly Color OriginalMaterialAccent;
            internal float Alpha;
            internal bool PreserveHue;
            internal bool ApplyCustomOpacity;

            internal ThemeSurface(AlphaBlendControl control, float alpha, bool applyCustomOpacity)
            {
                Control = new WeakReference<AlphaBlendControl>(control);
                OriginalColor = control.BaseColor;
                OriginalHue = control.Hue;
                OriginalMaterialGraphic = control.MaterialGraphic;
                OriginalMaterialHue = control.MaterialHue;
                OriginalMaterialAlpha = control.MaterialAlpha;
                OriginalMaterialStyle = control.MaterialStyle;
                OriginalMaterialAccent = control.MaterialAccent;
                Alpha = alpha;
                ApplyCustomOpacity = applyCustomOpacity;
            }
        }

        private sealed class ThemeColorBox
        {
            internal readonly WeakReference<ColorBox> Control;
            internal readonly ushort OriginalHue;

            internal ThemeColorBox(ColorBox control)
            {
                Control = new WeakReference<ColorBox>(control);
                OriginalHue = control.Hue;
            }
        }

        private sealed class ThemeFrame
        {
            internal readonly WeakReference<ResizePic> Control;
            internal readonly ushort OriginalHue;

            internal ThemeFrame(ResizePic control)
            {
                Control = new WeakReference<ResizePic>(control);
                OriginalHue = control.Hue;
            }
        }

        internal static Color CompactBorderColor
        {
            get
            {
                switch (Current)
                {
                    case CustomGumpTheme.Classic: return new Color(104, 96, 73);
                    case CustomGumpTheme.Stone: return new Color(138, 143, 147);
                    case CustomGumpTheme.Wood: return new Color(188, 126, 52);
                    case CustomGumpTheme.Dark: return new Color(42, 101, 132);
                    case CustomGumpTheme.Royal: return new Color(212, 169, 56);
                    case CustomGumpTheme.Forest: return new Color(72, 145, 71);
                    case CustomGumpTheme.Dungeon: return new Color(118, 34, 31);
                    case CustomGumpTheme.Water: return new Color(34, 146, 176);
                    case CustomGumpTheme.Snow: return new Color(166, 220, 235);
                    case CustomGumpTheme.Heartwood: return new Color(164, 142, 63);
                    case CustomGumpTheme.TerMur: return new Color(145, 86, 184);
                    case CustomGumpTheme.Kotl: return new Color(171, 118, 39);
                    case CustomGumpTheme.Britannia: return new Color(196, 151, 48);
                    case CustomGumpTheme.Trinsic: return new Color(210, 187, 116);
                    case CustomGumpTheme.Minoc: return new Color(153, 94, 45);
                    case CustomGumpTheme.Blackthorn: return new Color(145, 32, 42);
                    case CustomGumpTheme.Obsidian: return new Color(105, 72, 145);
                    case CustomGumpTheme.Doom: return new Color(145, 35, 31);
                    case CustomGumpTheme.Midnight: return new Color(48, 101, 148);
                    case CustomGumpTheme.Necropolis: return new Color(82, 124, 67);
                    case CustomGumpTheme.Ornate: return new Color(185, 143, 69);
                    case CustomGumpTheme.BritannianChronicle: return new Color(165, 120, 48);
                    case CustomGumpTheme.MoonglowArcane: return new Color(119, 167, 192);
                    case CustomGumpTheme.TerMurRelic: return new Color(91, 165, 159);
                    case CustomGumpTheme.MarinersChart: return new Color(184, 140, 67);
                    case CustomGumpTheme.GildedGrove: return new Color(128, 177, 87);
                    case CustomGumpTheme.Aetherglass: return new Color(101, 210, 245);
                    case CustomGumpTheme.Celestial: return new Color(147, 182, 217);
                    case CustomGumpTheme.Exodus: return new Color(174, 115, 67);
                    case CustomGumpTheme.BloodOath: return new Color(181, 55, 64);
                    case CustomGumpTheme.Hildebrandt: return new Color(218, 174, 76);
                    case CustomGumpTheme.TazUO: return Color.Gray;
                    default: return Color.Gray;
                }
            }
        }

        private static ushort PanelHue => GetPanelHue(Current);

        internal static ushort GetTextHue(CustomGumpTheme theme)
        {
            return theme == CustomGumpTheme.BritannianChronicle
                ? (ushort)0
                : theme == CustomGumpTheme.Classic ? (ushort)0x0386 : (ushort)0xFFFF;
        }

        internal static ushort GetPanelHue(CustomGumpTheme theme)
        {
            switch (theme)
            {
                // Preserve the native blue, brass and carved-stone colors in the
                // original UO gump art. Applying a hue here washes the artwork out.
                case CustomGumpTheme.Classic: return 0;
                case CustomGumpTheme.Stone: return 0x0386;
                case CustomGumpTheme.Wood: return 0x0455;
                case CustomGumpTheme.Dark: return 0x03B2;
                case CustomGumpTheme.Royal: return 0x048D;
                case CustomGumpTheme.Forest: return 0x044E;
                case CustomGumpTheme.Dungeon: return 0x03B2;
                case CustomGumpTheme.Water: return 0x0058;
                case CustomGumpTheme.Snow: return 0x0481;
                case CustomGumpTheme.Heartwood: return 0x0455;
                case CustomGumpTheme.TerMur: return 0x048D;
                case CustomGumpTheme.Kotl: return 0x0386;
                case CustomGumpTheme.Britannia: return 0x048D;
                case CustomGumpTheme.Trinsic: return 0x0481;
                case CustomGumpTheme.Minoc: return 0x0455;
                case CustomGumpTheme.Blackthorn: return 0x03B2;
                case CustomGumpTheme.Obsidian: return 0x048D;
                case CustomGumpTheme.Doom: return 0x03B2;
                case CustomGumpTheme.Midnight: return 0x0058;
                case CustomGumpTheme.Necropolis: return 0x044E;
                case CustomGumpTheme.Ornate: return 0;
                case CustomGumpTheme.BritannianChronicle:
                case CustomGumpTheme.MoonglowArcane:
                case CustomGumpTheme.TerMurRelic:
                case CustomGumpTheme.MarinersChart:
                case CustomGumpTheme.GildedGrove:
                case CustomGumpTheme.Aetherglass:
                case CustomGumpTheme.Celestial:
                case CustomGumpTheme.Exodus:
                case CustomGumpTheme.BloodOath:
                case CustomGumpTheme.Hildebrandt: return 0;
                default: return 0;
            }
        }

        private static float MinimumSurfaceAlpha(CustomGumpTheme theme)
        {
            switch (theme)
            {
                case CustomGumpTheme.Minimal:
                case CustomGumpTheme.TazUO:
                    return 0f;
                case CustomGumpTheme.Classic:
                case CustomGumpTheme.Snow:
                case CustomGumpTheme.Trinsic:
                    return theme == CustomGumpTheme.Classic ? 0.98f : 0.94f;
                case CustomGumpTheme.Dark:
                case CustomGumpTheme.Dungeon:
                case CustomGumpTheme.Kotl:
                case CustomGumpTheme.Blackthorn:
                case CustomGumpTheme.Obsidian:
                case CustomGumpTheme.Doom:
                case CustomGumpTheme.Midnight:
                case CustomGumpTheme.Necropolis:
                case CustomGumpTheme.Ornate:
                case CustomGumpTheme.Stone:
                case CustomGumpTheme.Wood:
                case CustomGumpTheme.Heartwood:
                case CustomGumpTheme.Celestial:
                case CustomGumpTheme.Exodus:
                case CustomGumpTheme.BloodOath:
                case CustomGumpTheme.Hildebrandt:
                case CustomGumpTheme.BritannianChronicle:
                case CustomGumpTheme.MoonglowArcane:
                case CustomGumpTheme.TerMurRelic:
                case CustomGumpTheme.MarinersChart:
                case CustomGumpTheme.GildedGrove:
                case CustomGumpTheme.Aetherglass:
                    return 0.95f;
                default:
                    return 0.90f;
            }
        }

        internal static ThemedGumpBackground CreateBackground(
            int width,
            int height,
            float minimalAlpha,
            bool applyCustomOpacity = true)
        {
            return new ThemedGumpBackground(width, height, minimalAlpha, Current, applyCustomOpacity);
        }

        internal static void StyleButton(NiceButton button)
        {
            if (button == null || Current == CustomGumpTheme.Minimal || Current == CustomGumpTheme.TazUO)
            {
                if (button != null)
                {
                    button.ArtStyle = false;
                    button.TextLabel.SetFontStyle(FontStyle.BlackBorder | FontStyle.Cropped);
                }
                return;
            }

            button.AlwaysShowBackground = true;
            button.Hue = ButtonHue;
            button.DisplayBorder = true;
            button.ArtStyle = IsArtTheme(Current);

            if (Current == CustomGumpTheme.Classic)
            {
                button.Alpha = 0.90f;
                button.BackgroundColor = new Color(28, 51, 82);
                button.BorderColor = new Color(170, 133, 55);
                button.TextLabel.Hue = 0x0481;
            }
            else if (Current == CustomGumpTheme.BritannianChronicle)
            {
                button.TextLabel.Hue = DataTextHue;
            }

            button.TextLabel.SetFontStyle(Current == CustomGumpTheme.BritannianChronicle
                ? FontStyle.Cropped
                : FontStyle.BlackBorder | FontStyle.Cropped);
            button.TextLabel.Y = (button.Height - button.TextLabel.Height) >> 1;
        }

        internal static void StyleDataButton(NiceButton button, ushort minimalTextHue = 0xFFFF)
        {
            if (button == null)
            {
                return;
            }

            if (Current == CustomGumpTheme.Minimal || Current == CustomGumpTheme.TazUO)
            {
                button.ArtStyle = false;
                button.AlwaysShowBackground = false;
                button.DisplayBorder = false;
                button.TextLabel.Hue = minimalTextHue;
                button.TextLabel.SetFontStyle(FontStyle.BlackBorder | FontStyle.Cropped);
                return;
            }

            button.AlwaysShowBackground = true;
            button.DisplayBorder = true;
            button.ArtStyle = IsArtTheme(Current);
            button.Hue = ButtonHue;
            button.TextLabel.Hue = Current == CustomGumpTheme.Classic
                ? (ushort)0x0481
                : DataTextHue;
            button.TextLabel.SetFontStyle(Current == CustomGumpTheme.BritannianChronicle
                ? FontStyle.Cropped
                : FontStyle.BlackBorder | FontStyle.Cropped);
            button.TextLabel.Y = (button.Height - button.TextLabel.Height) >> 1;

            if (Current == CustomGumpTheme.Classic)
            {
                button.Alpha = 0.90f;
                button.BackgroundColor = new Color(28, 51, 82);
                button.BorderColor = new Color(170, 133, 55);
            }
        }

        internal static void ApplyDataSurface(
            AlphaBlendControl control,
            float alpha,
            bool applyCustomOpacity = true)
        {
            if (control == null)
            {
                return;
            }

            ThemeSurface surface = RegisterSurface(control, alpha, applyCustomOpacity);
            float opacityScale = applyCustomOpacity ? OpacityScale : 1f;
            float surfaceAlpha = applyCustomOpacity
                ? Math.Max(alpha, MinimumSurfaceAlpha(Current))
                : alpha;
            control.Alpha = surfaceAlpha * opacityScale;

            if (Current == CustomGumpTheme.TazUO)
            {
                control.BaseColor = surface.OriginalColor;
                control.Hue = surface.OriginalHue;
                control.MaterialGraphic = surface.OriginalMaterialGraphic;
                control.MaterialHue = surface.OriginalMaterialHue;
                control.MaterialAlpha = surface.OriginalMaterialAlpha;
                control.MaterialStyle = surface.OriginalMaterialStyle;
                control.MaterialAccent = surface.OriginalMaterialAccent;
                return;
            }

            if (Current == CustomGumpTheme.Minimal)
            {
                control.BaseColor = Color.Black;
                ApplyMaterial(control, CustomGumpTheme.Minimal);
                return;
            }

            control.Hue = 0;
            ApplyMaterial(control, Current);

            switch (Current)
            {
                case CustomGumpTheme.Classic:
                    control.BaseColor = new Color(158, 156, 145);
                    break;
                case CustomGumpTheme.Stone:
                    control.BaseColor = new Color(24, 27, 30);
                    break;
                case CustomGumpTheme.Wood:
                    control.BaseColor = new Color(42, 21, 8);
                    break;
                case CustomGumpTheme.Royal:
                    control.BaseColor = new Color(20, 9, 35);
                    break;
                case CustomGumpTheme.Forest:
                    control.BaseColor = new Color(5, 24, 10);
                    break;
                case CustomGumpTheme.Dungeon:
                    control.BaseColor = new Color(20, 11, 10);
                    break;
                case CustomGumpTheme.Water:
                    control.BaseColor = new Color(2, 27, 43);
                    break;
                case CustomGumpTheme.Snow:
                    control.BaseColor = new Color(34, 44, 55);
                    break;
                case CustomGumpTheme.Heartwood:
                    control.BaseColor = new Color(22, 27, 10);
                    break;
                case CustomGumpTheme.TerMur:
                    control.BaseColor = new Color(18, 10, 29);
                    break;
                case CustomGumpTheme.Kotl:
                    control.BaseColor = new Color(19, 22, 21);
                    break;
                case CustomGumpTheme.Britannia:
                    control.BaseColor = new Color(42, 20, 24);
                    break;
                case CustomGumpTheme.Trinsic:
                    control.BaseColor = new Color(47, 39, 26);
                    break;
                case CustomGumpTheme.Minoc:
                    control.BaseColor = new Color(45, 29, 20);
                    break;
                case CustomGumpTheme.Blackthorn:
                    control.BaseColor = new Color(17, 15, 18);
                    break;
                case CustomGumpTheme.Obsidian:
                    control.BaseColor = new Color(7, 5, 11);
                    break;
                case CustomGumpTheme.Doom:
                    control.BaseColor = new Color(16, 4, 4);
                    break;
                case CustomGumpTheme.Midnight:
                    control.BaseColor = new Color(2, 7, 16);
                    break;
                case CustomGumpTheme.Necropolis:
                    control.BaseColor = new Color(6, 11, 6);
                    break;
                case CustomGumpTheme.Ornate:
                    control.BaseColor = new Color(37, 27, 20);
                    break;
                case CustomGumpTheme.BritannianChronicle:
                    control.BaseColor = new Color(221, 207, 177);
                    break;
                case CustomGumpTheme.MoonglowArcane:
                    control.BaseColor = new Color(23, 33, 48);
                    break;
                case CustomGumpTheme.TerMurRelic:
                    control.BaseColor = new Color(20, 35, 38);
                    break;
                case CustomGumpTheme.MarinersChart:
                    control.BaseColor = new Color(48, 37, 27);
                    break;
                case CustomGumpTheme.GildedGrove:
                    control.BaseColor = new Color(29, 42, 26);
                    break;
                case CustomGumpTheme.Aetherglass:
                    control.BaseColor = new Color(21, 30, 69);
                    break;
                case CustomGumpTheme.Celestial:
                    control.BaseColor = new Color(22, 34, 62);
                    break;
                case CustomGumpTheme.Exodus:
                    control.BaseColor = new Color(31, 27, 25);
                    break;
                case CustomGumpTheme.BloodOath:
                    control.BaseColor = new Color(36, 15, 17);
                    break;
                case CustomGumpTheme.Hildebrandt:
                    control.BaseColor = new Color(21, 33, 70);
                    break;
                default:
                    control.BaseColor = new Color(3, 8, 13);
                    break;
            }
        }

        internal static void ApplyMaterial(AlphaBlendControl control, CustomGumpTheme theme)
        {
            if (control == null)
            {
                return;
            }

            switch (theme)
            {
                case CustomGumpTheme.Wood:
                case CustomGumpTheme.Forest:
                case CustomGumpTheme.Heartwood:
                case CustomGumpTheme.Minoc:
                    control.MaterialGraphic = 0x13C2;
                    break;
                case CustomGumpTheme.Classic:
                    control.MaterialGraphic = 0x13F0;
                    break;
                case CustomGumpTheme.Stone:
                case CustomGumpTheme.Dungeon:
                case CustomGumpTheme.Snow:
                case CustomGumpTheme.TerMur:
                case CustomGumpTheme.Trinsic:
                case CustomGumpTheme.Doom:
                case CustomGumpTheme.Necropolis:
                    control.MaterialGraphic = 0x13F0;
                    break;
                case CustomGumpTheme.Dark:
                case CustomGumpTheme.Royal:
                case CustomGumpTheme.Water:
                case CustomGumpTheme.Kotl:
                case CustomGumpTheme.Britannia:
                case CustomGumpTheme.Blackthorn:
                case CustomGumpTheme.Obsidian:
                case CustomGumpTheme.Midnight:
                    control.MaterialGraphic = 0x0BBC;
                    break;
                default:
                    control.MaterialGraphic = 0;
                    break;
            }

            control.MaterialHue = GetPanelHue(theme);

            switch (theme)
            {
                case CustomGumpTheme.Wood:
                case CustomGumpTheme.Forest:
                case CustomGumpTheme.Heartwood:
                case CustomGumpTheme.Minoc:
                    control.MaterialStyle = AlphaBlendMaterialStyle.Wood;
                    break;
                case CustomGumpTheme.Classic:
                    control.MaterialStyle = AlphaBlendMaterialStyle.Parchment;
                    break;
                case CustomGumpTheme.Stone:
                case CustomGumpTheme.Dungeon:
                case CustomGumpTheme.Snow:
                case CustomGumpTheme.TerMur:
                case CustomGumpTheme.Trinsic:
                case CustomGumpTheme.Doom:
                case CustomGumpTheme.Necropolis:
                    control.MaterialStyle = AlphaBlendMaterialStyle.Stone;
                    break;
                case CustomGumpTheme.Dark:
                case CustomGumpTheme.Kotl:
                case CustomGumpTheme.Blackthorn:
                case CustomGumpTheme.Obsidian:
                case CustomGumpTheme.Midnight:
                    control.MaterialStyle = AlphaBlendMaterialStyle.Metal;
                    break;
                case CustomGumpTheme.Royal:
                case CustomGumpTheme.Britannia:
                    control.MaterialStyle = AlphaBlendMaterialStyle.Fabric;
                    break;
                case CustomGumpTheme.Water:
                    control.MaterialStyle = AlphaBlendMaterialStyle.Water;
                    break;
                default:
                    control.MaterialStyle = AlphaBlendMaterialStyle.None;
                    break;
            }

            switch (theme)
            {
                case CustomGumpTheme.Classic: control.MaterialAccent = new Color(133, 105, 49); break;
                case CustomGumpTheme.Stone: control.MaterialAccent = new Color(132, 138, 142); break;
                case CustomGumpTheme.Wood: control.MaterialAccent = new Color(211, 139, 61); break;
                case CustomGumpTheme.Dark: control.MaterialAccent = new Color(54, 94, 116); break;
                case CustomGumpTheme.Royal: control.MaterialAccent = new Color(154, 105, 196); break;
                case CustomGumpTheme.Forest: control.MaterialAccent = new Color(70, 134, 72); break;
                case CustomGumpTheme.Dungeon: control.MaterialAccent = new Color(111, 48, 42); break;
                case CustomGumpTheme.Water: control.MaterialAccent = new Color(66, 164, 190); break;
                case CustomGumpTheme.Snow: control.MaterialAccent = new Color(182, 224, 237); break;
                case CustomGumpTheme.Heartwood: control.MaterialAccent = new Color(172, 148, 65); break;
                case CustomGumpTheme.TerMur: control.MaterialAccent = new Color(151, 96, 183); break;
                case CustomGumpTheme.Kotl: control.MaterialAccent = new Color(158, 110, 44); break;
                case CustomGumpTheme.Britannia: control.MaterialAccent = new Color(201, 154, 52); break;
                case CustomGumpTheme.Trinsic: control.MaterialAccent = new Color(218, 197, 134); break;
                case CustomGumpTheme.Minoc: control.MaterialAccent = new Color(163, 99, 48); break;
                case CustomGumpTheme.Blackthorn: control.MaterialAccent = new Color(153, 38, 49); break;
                case CustomGumpTheme.Obsidian: control.MaterialAccent = new Color(105, 72, 145); break;
                case CustomGumpTheme.Doom: control.MaterialAccent = new Color(145, 35, 31); break;
                case CustomGumpTheme.Midnight: control.MaterialAccent = new Color(48, 101, 148); break;
                case CustomGumpTheme.Necropolis: control.MaterialAccent = new Color(82, 124, 67); break;
                default: control.MaterialAccent = Color.Gray; break;
            }

            switch (theme)
            {
                case CustomGumpTheme.Wood: control.MaterialAlpha = 0.42f; break;
                case CustomGumpTheme.Minoc: control.MaterialAlpha = 0.40f; break;
                case CustomGumpTheme.Heartwood: control.MaterialAlpha = 0.38f; break;
                case CustomGumpTheme.Classic: control.MaterialAlpha = 0.26f; break;
                case CustomGumpTheme.Trinsic: control.MaterialAlpha = 0.30f; break;
                case CustomGumpTheme.Stone:
                case CustomGumpTheme.Forest:
                case CustomGumpTheme.Dungeon:
                case CustomGumpTheme.TerMur: control.MaterialAlpha = 0.32f; break;
                case CustomGumpTheme.Snow:
                case CustomGumpTheme.Kotl:
                case CustomGumpTheme.Britannia:
                case CustomGumpTheme.Blackthorn: control.MaterialAlpha = 0.30f; break;
                case CustomGumpTheme.Dark:
                case CustomGumpTheme.Royal:
                case CustomGumpTheme.Water: control.MaterialAlpha = 0.28f; break;
                case CustomGumpTheme.Obsidian:
                case CustomGumpTheme.Doom:
                case CustomGumpTheme.Midnight:
                case CustomGumpTheme.Necropolis: control.MaterialAlpha = 0.26f; break;
                default: control.MaterialAlpha = 0f; break;
            }
        }

        internal static void ApplyDataSurfacePreservingHue(AlphaBlendControl control, float alpha)
        {
            if (control == null)
            {
                return;
            }

            ushort hue = control.Hue;
            ThemeSurface surface = RegisterSurface(control, alpha, true);
            surface.PreserveHue = true;
            ApplyDataSurface(control, alpha);
            control.Hue = hue;
        }

        private static ThemeSurface RegisterSurface(
            AlphaBlendControl control,
            float alpha,
            bool applyCustomOpacity)
        {
            for (int i = _themeSurfaces.Count - 1; i >= 0; i--)
            {
                if (!_themeSurfaces[i].Control.TryGetTarget(out AlphaBlendControl target) || target.IsDisposed)
                {
                    _themeSurfaces.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(target, control))
                {
                    _themeSurfaces[i].Alpha = alpha;
                    _themeSurfaces[i].ApplyCustomOpacity = applyCustomOpacity;
                    return _themeSurfaces[i];
                }
            }

            var surface = new ThemeSurface(control, alpha, applyCustomOpacity);
            _themeSurfaces.Add(surface);
            return surface;
        }

        private static void RefreshRegisteredSurfaces()
        {
            for (int i = _themeSurfaces.Count - 1; i >= 0; i--)
            {
                ThemeSurface surface = _themeSurfaces[i];

                if (!surface.Control.TryGetTarget(out AlphaBlendControl control) || control.IsDisposed)
                {
                    _themeSurfaces.RemoveAt(i);
                    continue;
                }

                if (surface.PreserveHue)
                {
                    ushort hue = control.Hue;
                    ApplyDataSurface(control, surface.Alpha, surface.ApplyCustomOpacity);
                    control.Hue = hue;
                }
                else
                {
                    ApplyDataSurface(control, surface.Alpha, surface.ApplyCustomOpacity);
                }
            }
        }

        internal static void ApplyInputSurface(
            AlphaBlendControl control,
            float alpha = 0.65f,
            bool applyCustomOpacity = true)
        {
            ApplyDataSurface(control, alpha, applyCustomOpacity);

            if (control != null && Current != CustomGumpTheme.Minimal && Current != CustomGumpTheme.TazUO)
            {
                switch (Current)
                {
                    case CustomGumpTheme.Classic: control.BaseColor = new Color(184, 181, 168); break;
                    case CustomGumpTheme.Wood: control.BaseColor = new Color(62, 32, 12); break;
                    case CustomGumpTheme.Royal: control.BaseColor = new Color(43, 22, 65); break;
                    case CustomGumpTheme.Forest: control.BaseColor = new Color(20, 55, 30); break;
                    case CustomGumpTheme.Dungeon: control.BaseColor = new Color(50, 22, 18); break;
                    case CustomGumpTheme.Water: control.BaseColor = new Color(8, 52, 67); break;
                    case CustomGumpTheme.Snow: control.BaseColor = new Color(43, 58, 72); break;
                    case CustomGumpTheme.Heartwood: control.BaseColor = new Color(48, 45, 19); break;
                    case CustomGumpTheme.TerMur: control.BaseColor = new Color(44, 25, 61); break;
                    case CustomGumpTheme.Kotl: control.BaseColor = new Color(54, 43, 19); break;
                    case CustomGumpTheme.Britannia: control.BaseColor = new Color(72, 35, 35); break;
                    case CustomGumpTheme.Trinsic: control.BaseColor = new Color(69, 58, 38); break;
                    case CustomGumpTheme.Minoc: control.BaseColor = new Color(70, 43, 26); break;
                    case CustomGumpTheme.Blackthorn: control.BaseColor = new Color(45, 21, 25); break;
                    case CustomGumpTheme.Obsidian: control.BaseColor = new Color(27, 17, 39); break;
                    case CustomGumpTheme.Doom: control.BaseColor = new Color(47, 12, 11); break;
                    case CustomGumpTheme.Midnight: control.BaseColor = new Color(9, 23, 42); break;
                    case CustomGumpTheme.Necropolis: control.BaseColor = new Color(18, 34, 15); break;
                    case CustomGumpTheme.Ornate: control.BaseColor = new Color(47, 34, 24); break;
                    case CustomGumpTheme.BritannianChronicle: control.BaseColor = new Color(237, 222, 188); break;
                    case CustomGumpTheme.MoonglowArcane: control.BaseColor = new Color(31, 45, 64); break;
                    case CustomGumpTheme.TerMurRelic: control.BaseColor = new Color(30, 50, 52); break;
                    case CustomGumpTheme.MarinersChart: control.BaseColor = new Color(64, 48, 33); break;
                    case CustomGumpTheme.GildedGrove: control.BaseColor = new Color(39, 56, 35); break;
                    case CustomGumpTheme.Aetherglass: control.BaseColor = new Color(31, 44, 91); break;
                    case CustomGumpTheme.Celestial: control.BaseColor = new Color(33, 49, 78); break;
                    case CustomGumpTheme.Exodus: control.BaseColor = new Color(49, 37, 31); break;
                    case CustomGumpTheme.BloodOath: control.BaseColor = new Color(61, 26, 29); break;
                    case CustomGumpTheme.Hildebrandt: control.BaseColor = new Color(32, 48, 91); break;
                    default: control.BaseColor = new Color(14, 21, 27); break;
                }
            }
        }

        internal static void ApplyColorSurface(ColorBox control)
        {
            if (control == null)
            {
                return;
            }

            ThemeColorBox surface = RegisterColorBox(control);
            control.Hue = Current == CustomGumpTheme.Minimal || Current == CustomGumpTheme.TazUO
                ? surface.OriginalHue
                : PanelHue;
        }

        internal static void ApplyFrame(ResizePic control)
        {
            if (control == null)
            {
                return;
            }

            ThemeFrame frame = RegisterFrame(control);
            control.Hue = Current == CustomGumpTheme.Minimal || Current == CustomGumpTheme.TazUO
                ? frame.OriginalHue
                : PanelHue;
        }

        private static ThemeColorBox RegisterColorBox(ColorBox control)
        {
            for (int i = _themeColorBoxes.Count - 1; i >= 0; i--)
            {
                if (!_themeColorBoxes[i].Control.TryGetTarget(out ColorBox target) || target.IsDisposed)
                {
                    _themeColorBoxes.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(target, control))
                {
                    return _themeColorBoxes[i];
                }
            }

            var surface = new ThemeColorBox(control);
            _themeColorBoxes.Add(surface);
            return surface;
        }

        private static ThemeFrame RegisterFrame(ResizePic control)
        {
            for (int i = _themeFrames.Count - 1; i >= 0; i--)
            {
                if (!_themeFrames[i].Control.TryGetTarget(out ResizePic target) || target.IsDisposed)
                {
                    _themeFrames.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(target, control))
                {
                    return _themeFrames[i];
                }
            }

            var frame = new ThemeFrame(control);
            _themeFrames.Add(frame);
            return frame;
        }

        internal static bool TryParse(string value, out CustomGumpTheme theme)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "minimal":
                case "simple":
                    theme = CustomGumpTheme.Minimal;
                    return true;
                case "classic":
                case "uo":
                    theme = CustomGumpTheme.Classic;
                    return true;
                case "stone":
                case "stones":
                case "runestone":
                    theme = CustomGumpTheme.Stone;
                    return true;
                case "wood":
                case "wooden":
                case "oakandiron":
                    theme = CustomGumpTheme.Wood;
                    return true;
                case "dark":
                    theme = CustomGumpTheme.Dark;
                    return true;
                case "royal":
                    theme = CustomGumpTheme.Royal;
                    return true;
                case "forest":
                    theme = CustomGumpTheme.Forest;
                    return true;
                case "dungeon":
                    theme = CustomGumpTheme.Dungeon;
                    return true;
                case "water":
                    theme = CustomGumpTheme.Water;
                    return true;
                case "snow":
                    theme = CustomGumpTheme.Snow;
                    return true;
                case "heartwood":
                case "heart-wood":
                case "heartwoodsanctuary":
                    theme = CustomGumpTheme.Heartwood;
                    return true;
                case "termur":
                case "ter-mur":
                case "ter_mur":
                    theme = CustomGumpTheme.TerMur;
                    return true;
                case "kotl":
                    theme = CustomGumpTheme.Kotl;
                    return true;
                case "tazuo":
                case "original":
                    theme = CustomGumpTheme.TazUO;
                    return true;
                case "britannia":
                case "britain":
                    theme = CustomGumpTheme.Britannia;
                    return true;
                case "trinsic":
                    theme = CustomGumpTheme.Trinsic;
                    return true;
                case "minoc":
                    theme = CustomGumpTheme.Minoc;
                    return true;
                case "blackthorn":
                    theme = CustomGumpTheme.Blackthorn;
                    return true;
                case "obsidian":
                    theme = CustomGumpTheme.Obsidian;
                    return true;
                case "doom":
                    theme = CustomGumpTheme.Doom;
                    return true;
                case "midnight":
                    theme = CustomGumpTheme.Midnight;
                    return true;
                case "necropolis":
                case "necro":
                case "necromancerscrypt":
                    theme = CustomGumpTheme.Necropolis;
                    return true;
                case "ornate":
                    theme = CustomGumpTheme.Ornate;
                    return true;
                case "britannianchronicle":
                case "chronicle":
                    theme = CustomGumpTheme.BritannianChronicle;
                    return true;
                case "moonglowarcane":
                case "arcane":
                    theme = CustomGumpTheme.MoonglowArcane;
                    return true;
                case "termurrelic":
                case "relic":
                    theme = CustomGumpTheme.TerMurRelic;
                    return true;
                case "marinerschart":
                case "mariner":
                case "chart":
                    theme = CustomGumpTheme.MarinersChart;
                    return true;
                case "gildedgrove":
                    theme = CustomGumpTheme.GildedGrove;
                    return true;
                case "aetherglass":
                    theme = CustomGumpTheme.Aetherglass;
                    return true;
                case "celestial":
                    theme = CustomGumpTheme.Celestial;
                    return true;
                case "exodus":
                    theme = CustomGumpTheme.Exodus;
                    return true;
                case "blood":
                case "bloodoath":
                    theme = CustomGumpTheme.BloodOath;
                    return true;
                case "hildebrandt":
                    theme = CustomGumpTheme.Hildebrandt;
                    return true;
                default:
                    theme = CustomGumpTheme.Minimal;
                    return false;
            }
        }

        internal static void SetTheme(CustomGumpTheme theme)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (profile == null)
            {
                return;
            }

            profile.CustomGumpTheme = (byte)theme;

            if (!string.IsNullOrEmpty(ProfileManager.ProfilePath))
            {
                profile.Save(ProfileManager.ProfilePath, false);
            }

            RefreshOpenGumps();
        }

        internal static void SetOpacity(int opacity)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null)
                return;

            profile.CustomGumpOpacity = (byte)Math.Max(20, Math.Min(100, opacity));
            RefreshRegisteredSurfaces();
            DurabilitysGump.UpdateAllOpacity();
        }

        private static void RefreshOpenGumps()
        {
            RefreshRegisteredSurfaces();
            RefreshRegisteredColorBoxes();
            RefreshRegisteredFrames();
            Refresh(UIManager.GetGump<CommandPaletteGump>(), () => new CommandPaletteGump());
            Refresh(UIManager.GetGump<EnvironmentControlGump>(), () => new EnvironmentControlGump());
            Refresh(UIManager.GetGump<MaterialIntensityGump>(), () => new MaterialIntensityGump());
            Refresh(UIManager.GetGump<BandageOptionsGump>(), () => new BandageOptionsGump());
            Refresh(UIManager.GetGump<GlobalChatGump>(), () => new GlobalChatGump());
            Refresh(UIManager.GetGump<GuildChatGump>(), () => new GuildChatGump());
            Refresh(UIManager.GetGump<NearbySpeechGump>(), () => new NearbySpeechGump());
            Refresh(UIManager.GetGump<GumpThemeSelectorGump>(), () => new GumpThemeSelectorGump());
            Refresh(UIManager.GetGump<DurabilitysGump>(), () => new DurabilitysGump());
            RefreshItemFinder();
            Refresh(UIManager.GetGump<RestockAgentGump>(), () => new RestockAgentGump());
            RefreshAlertCenter();
            TopBarGump.RefreshTheme();

            RefreshOptionsGump();

            CounterBarGump.CurrentCounterBarGump?.ApplyTheme();
            InfoBarGump.UpdateAllThemes();
            UIManager.GetGump<BossHealthBarGump>()?.ApplyTheme();

            foreach (Gump gump in UIManager.Gumps)
            {
                if (gump is NineSliceGump nineSlice)
                {
                    nineSlice.ApplyTheme();
                }
            }

            GridContainer.UpdateAllGridContainers();
            ResizableJournal.UpdateJournalOptions();
            UIManager.SystemChat?.ApplyTheme();
            PinnedCommandManager.RefreshGumps();

            SpellAbilityEffectsGump spellEffects = UIManager.GetGump<SpellAbilityEffectsGump>();
            if (spellEffects != null && !spellEffects.IsDisposed)
            {
                int x = spellEffects.X;
                int y = spellEffects.Y;
                int page = spellEffects.CurrentPage;
                spellEffects.Dispose();
                UIManager.Add(new SpellAbilityEffectsGump(page) { X = x, Y = y });
            }

            MusicPlayerGump musicPlayer = UIManager.GetGump<MusicPlayerGump>();
            if (musicPlayer != null && !musicPlayer.IsDisposed)
            {
                int x = musicPlayer.X;
                int y = musicPlayer.Y;
                bool compact = musicPlayer.IsCompact;
                musicPlayer.Dispose();
                UIManager.Add(new MusicPlayerGump(compact) { X = x, Y = y });
            }
        }

        private static void RefreshItemFinder()
        {
            ItemFinderGump gump = UIManager.GetGump<ItemFinderGump>();

            if (gump == null || gump.IsDisposed)
            {
                return;
            }

            int x = gump.X;
            int y = gump.Y;
            string query = gump.CurrentQuery;
            gump.Dispose();
            UIManager.Add(new ItemFinderGump(query) { X = x, Y = y });
        }

        private static void RefreshAlertCenter()
        {
            AlertCenterGump gump = UIManager.GetGump<AlertCenterGump>();

            if (gump == null || gump.IsDisposed)
            {
                return;
            }

            int x = gump.X;
            int y = gump.Y;
            bool settingsPage = gump.SettingsPage;
            bool activeOnly = gump.ActiveOnly;
            int severityFilter = gump.SeverityFilter;
            int categoryFilter = gump.CategoryFilter;
            gump.Dispose();
            UIManager.Add(new AlertCenterGump(
                settingsPage,
                activeOnly,
                severityFilter,
                categoryFilter)
            {
                X = x,
                Y = y
            });
        }

        internal static void RefreshOptionsGump()
        {
            ModernOptionsGump options = UIManager.GetGump<ModernOptionsGump>();
            if (options != null && !options.IsDisposed)
            {
                int x = options.X;
                int y = options.Y;
                string page = options.GetPageString();
                options.Dispose();
                var replacement = new ModernOptionsGump { X = x, Y = y };
                replacement.GoToPage(page);
                UIManager.Add(replacement);
            }
        }

        private static void RefreshRegisteredColorBoxes()
        {
            for (int i = _themeColorBoxes.Count - 1; i >= 0; i--)
            {
                ThemeColorBox surface = _themeColorBoxes[i];

                if (!surface.Control.TryGetTarget(out ColorBox control) || control.IsDisposed)
                {
                    _themeColorBoxes.RemoveAt(i);
                    continue;
                }

                control.Hue = Current == CustomGumpTheme.Minimal || Current == CustomGumpTheme.TazUO
                    ? surface.OriginalHue
                    : PanelHue;
            }
        }

        private static void RefreshRegisteredFrames()
        {
            for (int i = _themeFrames.Count - 1; i >= 0; i--)
            {
                ThemeFrame frame = _themeFrames[i];

                if (!frame.Control.TryGetTarget(out ResizePic control) || control.IsDisposed)
                {
                    _themeFrames.RemoveAt(i);
                    continue;
                }

                control.Hue = Current == CustomGumpTheme.Minimal || Current == CustomGumpTheme.TazUO
                    ? frame.OriginalHue
                    : PanelHue;
            }
        }

        private static void Refresh<T>(T gump, Func<T> factory) where T : Gump
        {
            if (gump == null || gump.IsDisposed)
            {
                return;
            }

            int x = gump.X;
            int y = gump.Y;
            gump.Dispose();
            T replacement = factory();
            replacement.X = x;
            replacement.Y = y;
            UIManager.Add(replacement);
        }
    }

    internal sealed class ThemedGumpBackground : Control
    {
        private readonly ResizePic _frame;
        private readonly AlphaBlendControl _fill;
        private readonly AlphaBlendControl _header;
        private readonly AlphaBlendControl[] _edges;
        private readonly int _margin;

        private readonly bool _applyCustomOpacity;
        private readonly CustomGumpTheme _artTheme;
        private readonly float _fillAlpha;
        private float _lastOpacityScale = -1f;
        internal float OpacityScaleOverride { get; set; } = 1f;

        internal ThemedGumpBackground(
            int width,
            int height,
            float minimalAlpha,
            CustomGumpTheme theme,
            bool applyCustomOpacity)
        {
            Width = width;
            Height = height;
            AcceptMouseInput = false;
            _applyCustomOpacity = applyCustomOpacity;
            _artTheme = CustomGumpThemeManager.IsArtTheme(theme) ? theme : CustomGumpTheme.Minimal;

            if (CustomGumpThemeManager.IsArtTheme(theme))
            {
                _fillAlpha = 1f;
                return;
            }

            ushort frameGraphic = 0;
            float fillAlpha = minimalAlpha;
            Color fillColor = Color.Black;
            Color accentColor = Color.Transparent;

            switch (theme)
            {
                case CustomGumpTheme.Classic:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.99f;
                    fillColor = new Color(158, 156, 145);
                    accentColor = new Color(160, 124, 48);
                    _margin = 10;
                    break;
                case CustomGumpTheme.Stone:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.95f;
                    fillColor = new Color(24, 27, 30);
                    accentColor = new Color(138, 143, 147);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Wood:
                    frameGraphic = 0x13BE;
                    fillAlpha = 0.95f;
                    fillColor = new Color(42, 21, 8);
                    accentColor = new Color(188, 126, 52);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Dark:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.90f;
                    fillColor = new Color(3, 8, 13);
                    accentColor = new Color(42, 101, 132);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Royal:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.92f;
                    fillColor = new Color(20, 9, 35);
                    accentColor = new Color(212, 169, 56);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Forest:
                    frameGraphic = 0x13BE;
                    fillAlpha = 0.95f;
                    fillColor = new Color(5, 24, 10);
                    accentColor = new Color(72, 145, 71);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Dungeon:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.92f;
                    fillColor = new Color(20, 11, 10);
                    accentColor = new Color(118, 34, 31);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Water:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.88f;
                    fillColor = new Color(2, 27, 43);
                    accentColor = new Color(34, 146, 176);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Snow:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.95f;
                    fillColor = new Color(34, 44, 55);
                    accentColor = new Color(166, 220, 235);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Heartwood:
                    frameGraphic = 0x13BE;
                    fillAlpha = 0.95f;
                    fillColor = new Color(22, 27, 10);
                    accentColor = new Color(164, 142, 63);
                    _margin = 8;
                    break;
                case CustomGumpTheme.TerMur:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.96f;
                    fillColor = new Color(18, 10, 29);
                    accentColor = new Color(145, 86, 184);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Kotl:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.93f;
                    fillColor = new Color(19, 22, 21);
                    accentColor = new Color(171, 118, 39);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Britannia:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.90f;
                    fillColor = new Color(42, 20, 24);
                    accentColor = new Color(196, 151, 48);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Trinsic:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.95f;
                    fillColor = new Color(47, 39, 26);
                    accentColor = new Color(210, 187, 116);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Minoc:
                    frameGraphic = 0x13BE;
                    fillAlpha = 0.88f;
                    fillColor = new Color(45, 29, 20);
                    accentColor = new Color(153, 94, 45);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Blackthorn:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.93f;
                    fillColor = new Color(17, 15, 18);
                    accentColor = new Color(145, 32, 42);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Obsidian:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.97f;
                    fillColor = new Color(7, 5, 11);
                    accentColor = new Color(105, 72, 145);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Doom:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.97f;
                    fillColor = new Color(16, 4, 4);
                    accentColor = new Color(145, 35, 31);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Midnight:
                    frameGraphic = 0x0BB8;
                    fillAlpha = 0.97f;
                    fillColor = new Color(2, 7, 16);
                    accentColor = new Color(48, 101, 148);
                    _margin = 8;
                    break;
                case CustomGumpTheme.Necropolis:
                    frameGraphic = 0x13EC;
                    fillAlpha = 0.97f;
                    fillColor = new Color(6, 11, 6);
                    accentColor = new Color(82, 124, 67);
                    _margin = 8;
                    break;
                default:
                    _margin = 0;
                    break;
            }

            if (frameGraphic != 0)
            {
                _frame = new ResizePic(frameGraphic)
                {
                    Width = width,
                    Height = height,
                    Hue = CustomGumpThemeManager.GetPanelHue(theme),
                    AcceptMouseInput = false,
                    CanMove = false,
                    CanCloseWithRightClick = false
                };
                Add(_frame);
            }

            _fill = new AlphaBlendControl(fillAlpha)
            {
                X = _margin,
                Y = _margin,
                Width = Math.Max(1, width - _margin * 2),
                Height = Math.Max(1, height - _margin * 2),
                BaseColor = fillColor
            };
            CustomGumpThemeManager.ApplyMaterial(_fill, theme);
            _fillAlpha = fillAlpha;
            Add(_fill);

            if (theme != CustomGumpTheme.Minimal && theme != CustomGumpTheme.TazUO)
            {
                _header = new AlphaBlendControl(0.20f)
                {
                    X = _margin,
                    Y = _margin,
                    Width = Math.Max(1, width - _margin * 2),
                    Height = 28,
                    BaseColor = accentColor
                };
                Add(_header);

                _edges = new[]
                {
                    Edge(accentColor), Edge(accentColor), Edge(accentColor), Edge(accentColor)
                };
                for (int i = 0; i < _edges.Length; i++) Add(_edges[i]);
                SyncSize();
            }

            ApplyOpacity();
        }

        public override void Update()
        {
            ApplyOpacity();
            SyncSize();
            base.Update();
        }

        private void ApplyOpacity()
        {
            if (CustomGumpThemeManager.IsArtTheme(_artTheme))
                return;

            float scale = (_applyCustomOpacity ? CustomGumpThemeManager.OpacityScale : 1f)
                * MathHelper.Clamp(OpacityScaleOverride, 0f, 1f);
            if (Math.Abs(scale - _lastOpacityScale) < 0.001f)
                return;

            _lastOpacityScale = scale;
            _fill.Alpha = _fillAlpha * scale;
            if (_frame != null) _frame.Alpha = scale;
            if (_header != null) _header.Alpha = 0.20f * scale;
            if (_edges != null)
            {
                for (int i = 0; i < _edges.Length; i++)
                    _edges[i].Alpha = 0.55f * scale;
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (CustomGumpThemeManager.IsArtTheme(_artTheme))
            {
                float alpha = (_applyCustomOpacity ? CustomGumpThemeManager.OpacityScale : 1f)
                    * MathHelper.Clamp(OpacityScaleOverride, 0f, 1f);
                CustomThemeArt.DrawPanel(batcher, x, y, Width, Height, _artTheme, alpha);
            }
            return base.Draw(batcher, x, y);
        }

        private static AlphaBlendControl Edge(Color color)
        {
            return new AlphaBlendControl(0.55f) { BaseColor = color };
        }

        private void SyncSize()
        {
            if (CustomGumpThemeManager.IsArtTheme(_artTheme))
                return;

            if (_frame != null)
            {
                _frame.Width = Width;
                _frame.Height = Height;
            }

            _fill.X = _margin;
            _fill.Y = _margin;
            _fill.Width = Math.Max(1, Width - _margin * 2);
            _fill.Height = Math.Max(1, Height - _margin * 2);

            if (_header == null || _edges == null)
            {
                return;
            }

            int innerWidth = Math.Max(1, Width - _margin * 2);
            int innerHeight = Math.Max(1, Height - _margin * 2);
            _header.X = _margin;
            _header.Y = _margin;
            _header.Width = innerWidth;

            _edges[0].X = _margin;
            _edges[0].Y = _margin;
            _edges[0].Width = innerWidth;
            _edges[0].Height = 1;
            _edges[1].X = _margin;
            _edges[1].Y = _margin + innerHeight - 1;
            _edges[1].Width = innerWidth;
            _edges[1].Height = 1;
            _edges[2].X = _margin;
            _edges[2].Y = _margin;
            _edges[2].Width = 1;
            _edges[2].Height = innerHeight;
            _edges[3].X = _margin + innerWidth - 1;
            _edges[3].Y = _margin;
            _edges[3].Width = 1;
            _edges[3].Height = innerHeight;
        }
    }
}

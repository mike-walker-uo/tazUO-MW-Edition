using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PremiumGumpThemeTests
    {
        [Fact]
        public void NewThemesAppendWithoutChangingSavedThemeIds()
        {
            Assert.Equal(34, (int)CustomGumpTheme.HdWood);
            Assert.Equal(38, (int)CustomGumpTheme.HdStainedGlass);
            Assert.Equal(39, (int)CustomGumpTheme.SovereignGold);
            Assert.Equal(59, (int)CustomGumpTheme.EternalEclipse);
            Assert.Equal(44, CustomGumpThemeManager.ThemeCount);
            Assert.Equal(60, CustomGumpThemeManager.ThemeSlotCount);
            Assert.Equal(21, PremiumGumpThemes.Entries.Length);
            var names = new HashSet<string>();
            for (int i = 0; i < PremiumGumpThemes.Entries.Length; i++)
            {
                PremiumGumpTheme entry = PremiumGumpThemes.Entries[i];
                Assert.Equal(39 + i, (int)entry.Theme);
                Assert.Same(entry, PremiumGumpThemes.Get(entry.Theme));
                Assert.True(names.Add(entry.Name));
                Assert.True(CustomGumpThemeManager.IsArtTheme(entry.Theme));
            }
            Assert.Null(PremiumGumpThemes.Get(CustomGumpTheme.HdStainedGlass));
            Assert.Null(PremiumGumpThemes.Get((CustomGumpTheme)255));
        }

        [Fact]
        public void EveryThemeCanBeSelectedFromItsPickerName()
        {
            foreach (CustomGumpTheme theme in CustomGumpThemeManager.AvailableThemes)
            {
                string name = GumpThemeSelectorGump.DisplayName(theme);
                Assert.True(CustomGumpThemeManager.TryParse(name, out CustomGumpTheme parsed));
                Assert.Equal(theme, parsed);
                Assert.Contains(theme.ToString(), CustomGumpThemeManager.CommandUsage);
            }
        }

        [Fact]
        public void PreviewCardsKeepTheirOwnThemeInsideWindowRendering()
        {
            CustomGumpTheme original = CustomGumpThemeManager.Current;
            using (CustomGumpThemeManager.ForPreview(CustomGumpTheme.EternalEclipse))
            {
                foreach (CustomGumpTheme theme in CustomGumpThemeManager.AvailableThemes)
                {
                    using (CustomGumpThemeManager.ForPreview(theme))
                    using (CustomGumpThemeManager.ForWindow(null))
                    {
                        Assert.Equal(theme, CustomGumpThemeManager.Current);
                        Assert.Equal(CustomGumpThemeManager.GetTextHue(theme), CustomGumpThemeManager.TextHue);
                        Assert.Equal(CustomGumpThemeManager.GetOptionsSurfaceColor(theme), CustomGumpThemeManager.OptionsSurfaceColor);
                    }
                    Assert.Equal(CustomGumpTheme.EternalEclipse, CustomGumpThemeManager.Current);
                }
            }
            Assert.Equal(original, CustomGumpThemeManager.Current);
        }

        [Fact]
        public void PreviewThemeIsRestoredWhenDrawingFails()
        {
            CustomGumpTheme original = CustomGumpThemeManager.Current;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (CustomGumpThemeManager.ForPreview(CustomGumpTheme.BritannianChronicle))
                    throw new InvalidOperationException();
            });
            Assert.Equal(original, CustomGumpThemeManager.Current);
        }

        [Theory]
        [InlineData("lunar-silver")]
        [InlineData("LUNAR_SILVER")]
        [InlineData("  Lunar Silver  ")]
        public void ThemeCommandAcceptsReadableNameVariants(string name)
        {
            Assert.True(CustomGumpThemeManager.TryParse(name, out CustomGumpTheme theme));
            Assert.Equal(CustomGumpTheme.LunarSilver, theme);
        }

        [Fact]
        public void InvalidThemeNamesAreRejected()
        {
            Assert.False(CustomGumpThemeManager.TryParse("255", out _));
            Assert.False(CustomGumpThemeManager.TryParse("unknown style", out _));
        }

        [Fact]
        public void PaletteOnlyThemesAreUnavailableAndSavedIdsFallBackToMinimal()
        {
            string[] advertised = CustomGumpThemeManager.CommandUsage.Trim('[', ']').Split('|');
            int retired = 0;
            foreach (CustomGumpTheme theme in Enum.GetValues(typeof(CustomGumpTheme)))
            {
                if (CustomGumpThemeManager.IsSelectable(theme))
                {
                    Assert.Contains(theme, CustomGumpThemeManager.AvailableThemes);
                    Assert.Equal(theme, CustomGumpThemeManager.ResolveSavedTheme((byte)theme));
                    continue;
                }
                retired++;
                Assert.DoesNotContain(theme, CustomGumpThemeManager.AvailableThemes);
                Assert.DoesNotContain(theme.ToString(), advertised);
                Assert.False(CustomGumpThemeManager.TryParse(theme.ToString(), out _));
                Assert.False(CustomGumpThemeManager.TryParse(GumpThemeSelectorGump.DisplayName(theme), out _));
                Assert.Equal(CustomGumpTheme.Minimal, CustomGumpThemeManager.ResolveSavedTheme((byte)theme));
            }
            Assert.Equal(16, retired);
            Assert.Contains(CustomGumpTheme.Minimal, CustomGumpThemeManager.AvailableThemes);
            Assert.Contains(CustomGumpTheme.TazUO, CustomGumpThemeManager.AvailableThemes);
            Assert.Equal(CustomGumpTheme.Minimal, CustomGumpThemeManager.ResolveSavedTheme(255));
        }

        [Theory]
        [InlineData("uo")]
        [InlineData("britain")]
        [InlineData("ter-mur")]
        [InlineData("ter_mur")]
        public void RetiredThemeAliasesAreRejected(string name)
        {
            Assert.False(CustomGumpThemeManager.TryParse(name, out _));
        }

        [Fact]
        public void CyclingVisitsEveryAvailableThemeAndSkipsRetiredIds()
        {
            var seen = new HashSet<CustomGumpTheme>();
            CustomGumpTheme theme = CustomGumpTheme.Minimal;
            for (int i = 0; i < CustomGumpThemeManager.ThemeCount; i++)
            {
                Assert.True(seen.Add(theme));
                Assert.True(CustomGumpThemeManager.IsSelectable(theme));
                theme = CustomGumpThemeManager.NextTheme(theme);
            }
            Assert.Equal(CustomGumpTheme.Minimal, theme);
            Assert.Contains(CustomGumpTheme.TazUO, seen);
            Assert.Contains(CustomGumpTheme.EternalEclipse, seen);
            Assert.Equal(CustomGumpTheme.Minimal, CustomGumpThemeManager.NextTheme((CustomGumpTheme)255));
        }

        [Theory]
        [InlineData("silver", true)]
        [InlineData("MOONSTONE", true)]
        [InlineData("gold", false)]
        [InlineData("  ", true)]
        public void ThemeSearchMatchesNameAndMaterial(string search, bool expected)
        {
            Assert.Equal(expected, GumpThemeSelectorGump.MatchesSearch(CustomGumpTheme.LunarSilver, search));
        }

        [Fact]
        public void EveryNewThemeHasEmbeddedHighResolutionArtwork()
        {
            var assembly = typeof(CustomGumpThemeManager).Assembly;
            foreach (PremiumGumpTheme entry in PremiumGumpThemes.Entries)
            {
                using Stream stream = assembly.GetManifestResourceStream($"ClassicUO.Resources.PremiumThemes.{entry.Theme}.png");
                Assert.NotNull(stream);
                var header = new byte[24];
                Assert.Equal(header.Length, stream.Read(header, 0, header.Length));
                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, new ArraySegment<byte>(header, 0, 8));
                int width = ReadBigEndian(header, 16);
                int height = ReadBigEndian(header, 20);
                Assert.True(width >= 1024 && height >= 1024);
                int split = height * entry.SheetSplit / 1024;
                int sourceEdge = width * entry.PanelEdge / 1536;
                Assert.True(split > sourceEdge * 2 && split < height);
                Assert.True(height - split >= 128);
            }
        }

        [Fact]
        public void WhiteTextHasStrongContrastOnAllNewSurfaces()
        {
            foreach (PremiumGumpTheme entry in PremiumGumpThemes.Entries)
            {
                Assert.True(ContrastAgainstWhite(entry.Surface) >= 7);
                Assert.True(ContrastAgainstWhite(entry.Selection) >= 7);
            }
        }

        [Fact]
        public void CommandAliasesKeepTheirTargetUsage()
        {
            Assert.Equal(CommandMetadata.Get("nearbychat").Usage, CommandMetadata.Get("speechhistory").Usage);
            Assert.Equal(CommandMetadata.Get("alertcenter").Category, CommandMetadata.Get("alerts").Category);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ZeroOrNegativeParticleCountNeverCreatesParticles(int count)
        {
            Assert.Equal(0, SceneryInteractionManager.ScaleCount(count));
        }

        private static int ReadBigEndian(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        private static double ContrastAgainstWhite(Color color)
        {
            double luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
            return 1.05 / (luminance + 0.05);
        }

        private static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
    }
}

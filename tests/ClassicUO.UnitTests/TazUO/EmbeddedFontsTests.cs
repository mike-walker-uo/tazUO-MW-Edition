using System;
using ClassicUO.Assets;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class EmbeddedFontsTests
    {
        [Theory]
        [InlineData("ChakraPetch-Regular")]
        [InlineData("NotoSansSymbols2-Regular")]
        [InlineData("Roboto-Bold")]
        [InlineData("Roboto-Mono")]
        [InlineData("ibm-plex")]
        public void RequiredFontIsEmbedded(string fontName)
        {
            string[] resources = typeof(TrueTypeLoader).Assembly.GetManifestResourceNames();

            resources.Should().Contain
            (
                name => name.EndsWith
                (
                    $".fonts.{fontName}.ttf",
                    StringComparison.Ordinal
                )
            );
        }
    }
}

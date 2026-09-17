using System;
using ClassicUO.Assets;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class EmbeddedFontsTests
    {
        [Fact]
        public void ChakraPetchRegularIsEmbedded()
        {
            string[] resources = typeof(TrueTypeLoader).Assembly.GetManifestResourceNames();

            resources.Should().Contain
            (
                name => name.EndsWith
                (
                    ".fonts.ChakraPetch-Regular.ttf",
                    StringComparison.Ordinal
                )
            );
        }
    }
}

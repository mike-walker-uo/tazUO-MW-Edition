using System;
using ClassicUO.Assets;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class GumpArtworkDecodeTests
    {
        // Two rows: each offset is measured in four-byte blocks from the start.
        private static readonly byte[] Valid =
        {
            2, 0, 0, 0, 3, 0, 0, 0,
            0, 0, 2, 0, 0xff, 0x7f, 2, 0
        };

        [Fact]
        public void Decodes_rows_and_transparency()
        {
            Assert.True(GumpsLoader.TryDecodeGump(Valid, 2, 2, 0, out var pixels));
            Assert.Equal(4, pixels.Length);
            Assert.Equal(0u, pixels[0]); Assert.Equal(0u, pixels[1]);
            Assert.NotEqual(0u, pixels[2]); Assert.Equal(pixels[2], pixels[3]);
        }

        [Fact]
        public void Rejects_invalid_row_offsets_and_runs()
        {
            var beforeTable = (byte[])Valid.Clone(); beforeTable[0] = 1;
            var beyondEnd = (byte[])Valid.Clone(); beyondEnd[4] = 5;
            var backwards = (byte[])Valid.Clone(); backwards[0] = 3; backwards[4] = 2;
            var tooWide = (byte[])Valid.Clone(); tooWide[10] = 3;
            Assert.False(GumpsLoader.TryDecodeGump(beforeTable, 2, 2, 0, out _));
            Assert.False(GumpsLoader.TryDecodeGump(beyondEnd, 2, 2, 0, out _));
            Assert.False(GumpsLoader.TryDecodeGump(backwards, 2, 2, 0, out _));
            Assert.False(GumpsLoader.TryDecodeGump(tooWide, 2, 2, 0, out _));
            Assert.False(GumpsLoader.TryDecodeGump(Valid.AsSpan(0, 4), 2, 2, 0, out _));
            Assert.False(GumpsLoader.TryDecodeGump(Valid, 0, 2, 0, out _));
            Assert.False(GumpsLoader.TryDecodeGump(Valid, 2, uint.MaxValue, 0, out _));
        }
    }
}

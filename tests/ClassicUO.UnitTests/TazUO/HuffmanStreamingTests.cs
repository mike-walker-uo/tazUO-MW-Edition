using ClassicUO.Network;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class HuffmanStreamingTests
    {
        [Fact]
        public void Complete_compressed_packet_decodes_into_reused_output()
        {
            var decoder = new Huffman();
            var output = new byte[] { 99, 99, 99, 99 };
            int size = output.Length;

            // Three zero-byte codes followed by the packet terminator.
            Assert.True(decoder.Decompress(new byte[] { 0x03, 0x40 }, output, ref size));
            Assert.Equal(3, size);
            Assert.Equal((byte)0, output[0]);
            Assert.Equal((byte)0, output[1]);
            Assert.Equal((byte)0, output[2]);

            size = output.Length;
            Assert.True(decoder.Decompress(new byte[] { 0x34 }, output, ref size));
            Assert.Equal(1, size);
            Assert.Equal((byte)0, output[0]);
        }

        [Fact]
        public void Packet_terminator_can_arrive_in_a_later_network_chunk()
        {
            var decoder = new Huffman();
            var output = new byte[4];
            int size = output.Length;
            Assert.True(decoder.Decompress(new byte[] { 0x03 }, output, ref size));
            Assert.Equal(3, size);

            size = output.Length;
            Assert.True(decoder.Decompress(new byte[] { 0x40 }, output, ref size));
            Assert.Equal(0, size);
        }

        [Fact]
        public void Excess_decompressed_output_is_rejected()
        {
            var decoder = new Huffman();
            var output = new byte[2];
            int size = output.Length;
            Assert.False(decoder.Decompress(new byte[] { 0x03, 0x40 }, output, ref size));
        }
    }
}

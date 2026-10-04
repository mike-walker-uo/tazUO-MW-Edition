using System;
using System.Text;
using ClassicUO.IO;
using Xunit;

namespace ClassicUO.UnitTests.IO
{
    public class StackDataWriterFieldTests
    {
        [Theory]
        [InlineData("éé", 3, "é")]
        [InlineData("世界", 5, "世")]
        [InlineData("😀x", 3, "")]
        [InlineData("😀x", 4, "😀")]
        [InlineData("éé", 4, "éé")]
        [InlineData("abc", 0, "")]
        [InlineData(null, 3, "")]
        public void UTF8_fields_obey_byte_limits_without_splitting_characters(string text, int length, string expected)
        {
            var writer = new StackDataWriter(1);
            try
            {
                writer.WriteUInt8(0xA1);
                writer.WriteUTF8(text, length);
                writer.WriteUInt8(0xB2);

                byte[] encoded = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(length + 2, writer.BytesWritten);
                Assert.Equal((byte)0xA1, writer.BufferWritten[0]);
                Assert.True(encoded.AsSpan().SequenceEqual(writer.BufferWritten.Slice(1, encoded.Length)));
                for (int i = encoded.Length; i < length; i++)
                    Assert.Equal((byte)0, writer.BufferWritten[i + 1]);
                Assert.Equal((byte)0xB2, writer.BufferWritten[length + 1]);
            }
            finally { writer.Dispose(); }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        public void Empty_ASCII_fields_write_exactly_the_requested_width(int length)
        {
            var writer = new StackDataWriter(1);
            try
            {
                writer.WriteUInt8(0xA1);
                writer.WriteASCII(null, length);
                writer.WriteUInt8(0xB2);
                Assert.Equal(length + 2, writer.BytesWritten);
                Assert.Equal((byte)0xB2, writer.BufferWritten[length + 1]);
                for (int i = 0; i < length; i++)
                    Assert.Equal((byte)0, writer.BufferWritten[i + 1]);
            }
            finally { writer.Dispose(); }
        }
    }
}

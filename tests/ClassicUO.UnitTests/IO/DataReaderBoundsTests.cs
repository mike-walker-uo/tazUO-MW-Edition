using System;
using ClassicUO.IO;
using Xunit;

namespace ClassicUO.UnitTests.IO
{
    public class DataReaderBoundsTests
    {
        [Fact]
        public void Released_reader_cannot_access_its_previous_buffer()
        {
            var reader = new DataReader();
            reader.SetData(new byte[] { 1, 2, 3 }, 3);
            reader.ReleaseData();
            Assert.Equal(IntPtr.Zero, reader.StartAddress);
            Assert.Equal(0L, reader.Length);
            Assert.True(reader.IsEOF);
            Assert.Throws<IndexOutOfRangeException>(() => reader.ReadByte());
            reader.ReleaseData();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void Managed_buffer_cannot_claim_more_bytes_than_it_contains(long length)
        {
            var reader = new DataReader();
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.SetData(new byte[] { 1, 2, 3 }, length));
            Assert.Equal(IntPtr.Zero, reader.StartAddress);
        }

        [Fact]
        public void Truncated_reads_throw_before_reading_unmanaged_memory()
        {
            var reader = new DataReader();
            reader.SetData(new byte[] { 1, 2, 3 }, 3);
            try
            {
                Assert.Throws<IndexOutOfRangeException>(() => reader.ReadInt());
                Assert.Equal(0L, reader.Position);
                Assert.Equal((byte)1, reader.ReadByte());
                Assert.Equal((ushort)770, reader.ReadUShort());
                Assert.Throws<IndexOutOfRangeException>(() => reader.ReadByte());
            }
            finally { reader.ReleaseData(); }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        [InlineData(long.MaxValue)]
        public void Invalid_positions_cannot_bypass_bounds_check(long position)
        {
            var reader = new DataReader();
            reader.SetData(new byte[] { 1, 2, 3 }, 3);
            try
            {
                reader.Position = position;
                Assert.Throws<IndexOutOfRangeException>(() => reader.ReadByte());
                reader.Position = 2;
                reader.Skip(-1);
                Assert.Equal((byte)2, reader.ReadByte());
            }
            finally { reader.ReleaseData(); }
        }
    }
}

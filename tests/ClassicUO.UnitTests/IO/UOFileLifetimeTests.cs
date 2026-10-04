using System;
using System.IO;
using ClassicUO.IO;
using Xunit;

namespace ClassicUO.UnitTests.IO
{
    public class UOFileLifetimeTests
    {
        public UOFileLifetimeTests() => TestLogging.EnsureInitialized();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Missing_or_empty_file_can_be_disposed_repeatedly(bool createEmpty)
        {
            string path = Path.Combine(Path.GetTempPath(), "tazuo-audit-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (createEmpty) File.WriteAllBytes(path, new byte[0]);
                var file = new UOFile(path, true);
                file.Dispose();
                file.Dispose();
                Assert.Equal(IntPtr.Zero, file.StartAddress);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Mapped_file_exposes_only_file_bytes_and_releases_its_reader()
        {
            string path = Path.Combine(Path.GetTempPath(), "tazuo-audit-" + Guid.NewGuid().ToString("N"));
            UOFile file = null;
            try
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
                file = new UOFile(path, true);
                Assert.Equal(3L, file.Length);
                file.Skip(3);
                Assert.Throws<IndexOutOfRangeException>(() => file.ReadByte());
                file.Dispose();
                Assert.Throws<IndexOutOfRangeException>(() => file.ReadByte());
            }
            finally
            {
                file?.Dispose();
                File.Delete(path);
            }
        }
    }
}

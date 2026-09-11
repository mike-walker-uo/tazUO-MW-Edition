using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class LogFileTests
    {
        [Fact]
        public void WritePreservesUtf8Text()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                string path;
                using (var log = new LogFile(directory, "unicode.log"))
                {
                    path = log.ToString();
                    log.Write("Größe – beschädigt");
                }

                Assert.Equal("Größe – beschädigt\n", File.ReadAllText(path, Encoding.UTF8));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Fact]
        public async Task WriteAsyncPreservesUtf8Text()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                string path;
                using (var log = new LogFile(directory, "unicode-async.log"))
                {
                    path = log.ToString();
                    await log.WriteAsync("Größe – beschädigt");
                }

                Assert.Equal("Größe – beschädigt\n", File.ReadAllText(path, Encoding.UTF8));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Fact]
        public void SizeLimitedLogRetainsNewestEntryWithinLimit()
        {
            const int maximumLength = 256;
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                string path;
                using (var log = new LogFile(directory, "bounded.log", maximumLength))
                {
                    path = log.ToString();
                    log.Write(new string('a', 250), false);
                    log.Write("newest", false);
                }

                string contents = File.ReadAllText(path, Encoding.UTF8);
                Assert.True(new FileInfo(path).Length <= maximumLength);
                Assert.Contains("newest", contents);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Fact]
        public void SizeLimitedLogTruncatesOversizedUtf8EntrySafely()
        {
            const int maximumLength = 128;
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                string path;
                using (var log = new LogFile(directory, "bounded-unicode.log", maximumLength))
                {
                    path = log.ToString();
                    log.Write(new string('ä', 200) + "ENDE", false);
                }

                string contents = File.ReadAllText(path, Encoding.UTF8);
                Assert.True(new FileInfo(path).Length <= maximumLength);
                Assert.EndsWith("ENDE\n", contents);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}

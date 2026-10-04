using System.Reflection;
using System.Threading;
using ClassicUO.Utility.Logging;
using Xunit;

namespace ClassicUO.UnitTests.Utility
{
    public class LoggingLifetimeTests
    {
        [Fact]
        public void Restart_retires_previous_writer_before_creating_new_logger()
        {
            TestLogging.EnsureInitialized();
            var loggerField = typeof(Log).GetField("_logger", BindingFlags.Static | BindingFlags.NonPublic);
            var writerField = typeof(Logger).GetField("_consoleWriterThread", BindingFlags.Instance | BindingFlags.NonPublic);
            var original = (Logger)loggerField.GetValue(null);
            var writer = (Thread)writerField.GetValue(original);

            Log.Start(LogTypes.None);

            var replacement = (Logger)loggerField.GetValue(null);
            Assert.NotSame(original, replacement);
            Assert.False(writer.IsAlive);
            Assert.True(((Thread)writerField.GetValue(replacement)).IsAlive);
        }
    }
}

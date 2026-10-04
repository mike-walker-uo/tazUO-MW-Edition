using ClassicUO.Utility.Logging;

namespace ClassicUO.UnitTests
{
    internal static class TestLogging
    {
        static TestLogging() => Log.Start(LogTypes.None);

        internal static void EnsureInitialized() { }
    }
}

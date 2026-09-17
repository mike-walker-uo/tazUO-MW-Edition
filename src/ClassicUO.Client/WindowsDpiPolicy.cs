namespace ClassicUO
{
    internal static class WindowsDpiPolicy
    {
        internal const string AwarenessEnvironmentVariable = "SDL_WINDOWS_DPI_AWARENESS";
        internal const string SystemScaledAwareness = "unaware";
        internal const string NativeAwareness = "permonitorv2";

        internal static string ResolveAwareness(bool useNativeDpi, string configuredAwareness)
        {
            if (useNativeDpi)
            {
                return NativeAwareness;
            }

            return string.IsNullOrWhiteSpace(configuredAwareness)
                ? SystemScaledAwareness
                : configuredAwareness;
        }
    }
}

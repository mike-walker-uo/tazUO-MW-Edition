// TazUO MW Edition addition.
using System.IO;

namespace ClassicUO
{
    internal static class StartupHealthCheck
    {
        public static bool TryValidate(string uoPath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(uoPath) || !Directory.Exists(uoPath))
                error = "Ultima Online installation folder does not exist.";
            else if (!File.Exists(Path.Combine(uoPath, "tiledata.mul")))
                error = "Missing tiledata.mul. Select a complete Classic Client installation.";
            else if (!HasEither(uoPath, "art.mul", "artLegacyMUL.uop"))
                error = "Missing artwork data (art.mul or artLegacyMUL.uop). Repair the Classic Client installation.";
            else if (!HasEither(uoPath, "gumpart.mul", "gumpartLegacyMUL.uop"))
                error = "Missing gump data (gumpart.mul or gumpartLegacyMUL.uop). Repair the Classic Client installation.";
            else if (!HasEither(uoPath, "map0.mul", "map0LegacyMUL.uop"))
                error = "Missing map data (map0.mul or map0LegacyMUL.uop). Repair the Classic Client installation.";

            return error == null;
        }

        private static bool HasEither(string path, string first, string second)
            => File.Exists(Path.Combine(path, first)) || File.Exists(Path.Combine(path, second));
    }
}

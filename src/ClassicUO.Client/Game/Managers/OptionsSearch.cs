using System;

namespace ClassicUO.Game.Managers
{
    internal static class OptionsSearch
    {
        private static readonly string[][] Synonyms =
        {
            new[] { "healthbar", "health bar", "hp bar", "hpbar", "nameplate" },
            new[] { "viewport", "game window", "game world" },
            new[] { "backpack", "bag", "container" },
            new[] { "hotkey", "keybind", "shortcut" },
            new[] { "graphics", "visual", "rendering" },
            new[] { "sound", "audio", "volume" },
            new[] { "transparency", "opacity", "alpha" },
            new[] { "zoom", "scale", "scaling" },
            new[] { "weather", "rain", "snow", "fog" }
        };

        internal static bool Matches(string label, string query)
        {
            label = label ?? string.Empty;
            query = (query ?? string.Empty).Trim();
            if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (string[] group in Synonyms)
            {
                if (!Array.Exists(group, word => string.Equals(query, word, StringComparison.OrdinalIgnoreCase))) continue;
                if (Array.Exists(group, word => label.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            }
            return false;
        }
    }
}

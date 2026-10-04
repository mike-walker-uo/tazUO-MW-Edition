using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Configuration
{
    internal static class SettingsHistory
    {
        internal sealed class Entry
        {
            public Entry() { }
            public string Time { get; set; }
            public Dictionary<string, string> Before { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> After { get; set; } = new Dictionary<string, string>();
        }

        private static bool _undoing;
        private static readonly JsonSerializerOptions HistoryOptions = new JsonSerializerOptions { WriteIndented = true };
        private static string PathFor(string profilePath) => Path.Combine(profilePath, "settings-history.json");

        internal static List<Entry> Read(string profilePath)
        {
            string path = PathFor(profilePath);
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path), HistoryOptions)
                    ?? new List<Entry>() : new List<Entry>();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                Log.Warn("Cannot read settings history: " + ex.Message);
                return new List<Entry>();
            }
        }

        internal static void Record(Profile profile, string profileFile)
        {
            if (_undoing || !File.Exists(profileFile)) return;
            try
            {
                Profile oldProfile = JsonSerializer.Deserialize(File.ReadAllText(profileFile), ProfileJsonContext.DefaultToUse.Profile);
                if (oldProfile == null) return;
                using (JsonDocument before = JsonDocument.Parse(JsonSerializer.Serialize(oldProfile, ProfileJsonContext.DefaultToUse.Profile)))
                using (JsonDocument after = JsonDocument.Parse(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile)))
                {
                    Entry entry = Changes(before.RootElement, after.RootElement);
                    if (entry.Before.Count == 0) return;
                    string folder = Path.GetDirectoryName(profileFile);
                    List<Entry> history = Read(folder);
                    history.Add(entry);
                    if (history.Count > 50) history.RemoveRange(0, history.Count - 50);
                    File.WriteAllText(PathFor(folder), JsonSerializer.Serialize(history, HistoryOptions));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                Log.Warn("Cannot record settings history: " + ex.Message);
            }
        }

        internal static Entry Changes(JsonElement before, JsonElement after)
        {
            var entry = new Entry { Time = DateTime.UtcNow.ToString("u") };
            foreach (JsonProperty property in after.EnumerateObject())
                if (before.TryGetProperty(property.Name, out JsonElement old) && old.GetRawText() != property.Value.GetRawText())
                {
                    // Runtime placement and the daily rotation stamp are not option edits.
                    if (property.Name.StartsWith("game_window_") || property.Name.EndsWith("_position")
                        || property.Name == "last_gump_theme_rotation_day") continue;
                    entry.Before[property.Name] = old.GetRawText();
                    entry.After[property.Name] = property.Value.GetRawText();
                }
            return entry;
        }

        internal static bool Undo(Entry entry)
        {
            Profile current = ProfileManager.CurrentProfile;
            if (current == null || entry == null) return false;
            UIManager.GetGump<ModernOptionsGump>()?.Dispose();
            // Revert only this edit's fields, retaining newer unrelated options and identity.
            using (JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(current, ProfileJsonContext.DefaultToUse.Profile)))
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        if (entry.Before.TryGetValue(property.Name, out string old))
                        {
                            using (JsonDocument value = JsonDocument.Parse(old)) value.RootElement.WriteTo(writer);
                        }
                        else property.Value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                Profile restored = JsonSerializer.Deserialize(Encoding.UTF8.GetString(stream.ToArray()), ProfileJsonContext.DefaultToUse.Profile);
                foreach (PropertyInfo property in typeof(Profile).GetProperties())
                {
                    if (!property.CanRead || !property.CanWrite || property.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                    string name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                        ?? ProfileJsonContext.DefaultToUse.Options.PropertyNamingPolicy.ConvertName(property.Name);
                    if (entry.Before.ContainsKey(name)) property.SetValue(current, property.GetValue(restored));
                }
            }
            _undoing = true;
            try { current.Save(ProfileManager.ProfilePath, false, true); }
            finally { _undoing = false; }
            Client.Game.GetScene<GameScene>()?.SetPostProcessingSettings();
            AmbientWeatherManager.Configure(current.AmbientWeatherEnabled);
            CustomGumpThemeManager.SetOpacity(current.CustomGumpOpacity);
            CustomGumpThemeManager.SetTheme(CustomGumpThemeManager.ResolveSavedTheme(current.CustomGumpTheme));
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClassicUO.Configuration
{
    internal static class RazorPluginSettings
    {
        internal static bool IsRazorPath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return string.Equals(name, "Razor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "RazorEnhanced", StringComparison.OrdinalIgnoreCase);
        }

        internal static string[] ReplaceRazor(string[] plugins, string selectedPath)
        {
            var result = new List<string>();
            bool added = false;
            foreach (string plugin in plugins ?? Array.Empty<string>())
            {
                if (IsRazorPath(plugin) || string.Equals(plugin, selectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!added) result.Add(selectedPath);
                    added = true;
                }
                else result.Add(plugin);
            }
            if (!added) result.Add(selectedPath);
            return result.ToArray();
        }

        internal static string UpdateJson(string json, string selectedPath, out string[] plugins)
        {
            var settings = JsonNode.Parse(json) as JsonObject
                ?? throw new JsonException("Settings must be a JSON object.");
            string[] previous = settings["plugins"]?.Deserialize<string[]>() ?? Array.Empty<string>();
            plugins = ReplaceRazor(previous, selectedPath);
            settings["plugins"] = JsonSerializer.SerializeToNode(plugins);
            return settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        internal static void Save(string selectedPath)
        {
            string path = Path.GetFullPath(selectedPath);
            string extension = Path.GetExtension(path);
            if (!File.Exists(path) || (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Select the Razor Enhanced plugin DLL or EXE.");

            // Inspect metadata only: selecting a plugin must not start a second assistant.
            Assembly assembly = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));
            Type engine = assembly.GetType("Assistant.Engine", false);
            MethodInfo install = engine?.GetMethod("Install", BindingFlags.Public | BindingFlags.Static);
            if (install == null)
                throw new ArgumentException("This file does not expose the Razor plugin entry point. Select RazorEnhanced.exe or its plugin DLL.");

            string settingsPath = Settings.GetSettingsFilepath();
            string json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath)
                : JsonSerializer.Serialize(Settings.GlobalSettings, typeof(Settings), SettingsJsonContext.RealDefault);
            string updated = UpdateJson(json, path, out string[] plugins);
            string temporary = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, updated);
                if (File.Exists(settingsPath)) File.Replace(temporary, settingsPath, null);
                else File.Move(temporary, settingsPath);
                Settings.GlobalSettings.Plugins = plugins;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}

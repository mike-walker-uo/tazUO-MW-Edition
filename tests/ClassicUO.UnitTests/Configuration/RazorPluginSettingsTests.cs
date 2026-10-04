using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.Configuration
{
    public class RazorPluginSettingsTests
    {
        [Fact]
        public void Replaces_old_Razor_entries_and_preserves_other_plugins_in_order()
        {
            string selected = @"C:\Program Files\Razor Enhanced\RazorEnhanced.exe";
            string[] previous = { "Before.dll", "./Assistant/Razor.dll", "After.dll", "RazorEnhanced.EXE" };

            Assert.Equal(new[] { "Before.dll", selected, "After.dll" }, RazorPluginSettings.ReplaceRazor(previous, selected));
        }

        [Fact]
        public void Adds_Razor_when_no_assistant_is_configured()
        {
            Assert.Equal(new[] { "Other.dll", "RazorEnhanced.dll" },
                RazorPluginSettings.ReplaceRazor(new[] { "Other.dll" }, "RazorEnhanced.dll"));
            Assert.Equal(new[] { "RazorEnhanced.dll" }, RazorPluginSettings.ReplaceRazor(null, "RazorEnhanced.dll"));
        }

        [Fact]
        public void Selecting_same_custom_plugin_does_not_duplicate_it()
        {
            string selected = @"C:\Plugins\CustomAssistant.dll";
            Assert.Equal(new[] { selected, "Other.dll" },
                RazorPluginSettings.ReplaceRazor(new[] { selected.ToUpperInvariant(), "Other.dll" }, selected));
        }

        [Fact]
        public void Only_plugins_change_and_paths_with_spaces_and_Unicode_round_trip()
        {
            const string original = "{\"ip\":\"login.uoalive.com\",\"username\":\"Player\",\"fps\":90,\"extra\":{\"enabled\":true},\"plugins\":[\"Other.dll\",\"Razor.dll\"]}";
            string selected = @"C:\Spiele\Jörg\Razor Enhanced\RazorEnhanced.exe";
            string updated = RazorPluginSettings.UpdateJson(original, selected, out string[] plugins);

            using var after = JsonDocument.Parse(updated);
            Assert.Equal("login.uoalive.com", after.RootElement.GetProperty("ip").GetString());
            Assert.Equal("Player", after.RootElement.GetProperty("username").GetString());
            Assert.Equal(90, after.RootElement.GetProperty("fps").GetInt32());
            Assert.True(after.RootElement.GetProperty("extra").GetProperty("enabled").GetBoolean());
            Assert.Equal(new[] { "Other.dll", selected }, plugins);
            Assert.Equal(selected, after.RootElement.GetProperty("plugins")[1].GetString());
        }

        [Fact]
        public void Missing_plugins_field_is_added_without_discarding_settings()
        {
            string updated = RazorPluginSettings.UpdateJson("{\"fps\":90}", "RazorEnhanced.exe", out string[] plugins);
            using var json = JsonDocument.Parse(updated);
            Assert.Equal(90, json.RootElement.GetProperty("fps").GetInt32());
            Assert.Equal(new[] { "RazorEnhanced.exe" }, plugins);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("{\"plugins\":42}")]
        public void Invalid_settings_are_rejected(string json)
        {
            Assert.Throws<JsonException>(() => RazorPluginSettings.UpdateJson(json, "RazorEnhanced.exe", out _));
        }

        [Fact]
        public void Managed_file_without_Razor_entry_point_is_rejected_before_settings_change()
        {
            string[] previous = Settings.GlobalSettings.Plugins;
            Assert.Throws<ArgumentException>(() => RazorPluginSettings.Save(typeof(RazorPluginSettings).Assembly.Location));
            Assert.Same(previous, Settings.GlobalSettings.Plugins);
        }

        [Fact]
        public void Missing_file_is_rejected_before_settings_change()
        {
            string[] previous = Settings.GlobalSettings.Plugins;
            string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "RazorEnhanced.exe");
            Assert.Throws<ArgumentException>(() => RazorPluginSettings.Save(missing));
            Assert.Same(previous, Settings.GlobalSettings.Plugins);
        }
    }
}

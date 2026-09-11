using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.Configuration
{
    public class ConfigurationResolverTests
    {
        [Fact]
        public void SessionLogDefaultsToDisabledWhenMissing()
        {
            Settings settings = JsonSerializer.Deserialize
            (
                "{}",
                typeof(Settings),
                SettingsJsonContext.RealDefault
            ) as Settings;

            Assert.False(settings.SessionLog);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SessionLogDeserializesExplicitValue(bool enabled)
        {
            Settings settings = JsonSerializer.Deserialize
            (
                $"{{\"session_log\":{enabled.ToString().ToLowerInvariant()}}}",
                typeof(Settings),
                SettingsJsonContext.RealDefault
            ) as Settings;

            Assert.Equal(enabled, settings.SessionLog);
        }

        [Fact]
        public void SessionLogSerializesWithStableFieldName()
        {
            string json = JsonSerializer.Serialize
            (
                new Settings { SessionLog = true },
                typeof(Settings),
                SettingsJsonContext.RealDefault
            );

            using JsonDocument document = JsonDocument.Parse(json);
            Assert.True(document.RootElement.GetProperty("session_log").GetBoolean());
        }

        [Fact]
        public void SaveCreatesNewFile()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "settings.json");

            try
            {
                ConfigurationResolver.Save
                (
                    new Settings { Username = "new-user" },
                    path,
                    SettingsJsonContext.RealDefault
                );

                Settings saved = ConfigurationResolver.Load<Settings>(path, SettingsJsonContext.RealDefault);
                Assert.Equal("new-user", saved.Username);
                Assert.Empty(Directory.GetFiles(directory, ".settings.json.*.tmp"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void SaveReplacesExistingFile()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "settings.json");

            try
            {
                File.WriteAllText(path, "old contents");

                ConfigurationResolver.Save
                (
                    new Settings { Username = "replacement" },
                    path,
                    SettingsJsonContext.RealDefault
                );

                Settings saved = ConfigurationResolver.Load<Settings>(path, SettingsJsonContext.RealDefault);
                Assert.Equal("replacement", saved.Username);
                Assert.Empty(Directory.GetFiles(directory, ".settings.json.*.tmp"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void SerializationFailurePreservesExistingFile()
        {
            string directory = CreateTempDirectory();
            string path = Path.Combine(directory, "settings.json");

            try
            {
                const string original = "original contents";
                File.WriteAllText(path, original);

                Assert.Throws<InvalidOperationException>
                (
                    () => ConfigurationResolver.Save(new UnsupportedSettings(), path, SettingsJsonContext.RealDefault)
                );

                Assert.Equal(original, File.ReadAllText(path));
                Assert.Empty(Directory.GetFiles(directory, ".settings.json.*.tmp"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string CreateTempDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private sealed class UnsupportedSettings
        {
        }
    }
}

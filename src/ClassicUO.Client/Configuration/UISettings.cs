using Microsoft.Xna.Framework;
using System;
using ClassicUO.Utility.Logging;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Configuration
{
    internal abstract class UISettings
    {
        private static string savePath { get { return Path.Combine(CUOEnviroment.ExecutablePath, "Data", "UI"); } }
        private static readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions() { WriteIndented = true };
        private static readonly ConcurrentDictionary<string, string> preload =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string ReadJsonFile(string name)
        {
            string fp = Path.Combine(savePath, name + ".json");

            if (File.Exists(fp))
            {
                try
                {
                    return File.ReadAllText(fp);
                }
                catch { }
            }

            return string.Empty;
        }

        public static UISettings Load<T>(string name)
        {
            string jsonData;

            if (preload.TryRemove(name, out var value))
            {
                jsonData = value;
            }
            else
            {
                jsonData = ReadJsonFile(name);
            }

            if (string.IsNullOrEmpty(jsonData))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(jsonData) as UISettings;
            }
            catch (JsonException ex)
            {
                Log.Warn($"Unable to load UI settings '{name}': {ex.Message}");
                return null;
            }
        }

        public static void Save<T>(string name, object settings)
        {
            string fileSaveData = JsonSerializer.Serialize((T)settings, serializerOptions);

            try
            {
                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

                File.WriteAllText(Path.Combine(savePath, name + ".json"), fileSaveData);
            }
            catch { }
        }

        public static void Preload()
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                if (Directory.Exists(savePath))
                {
                    string[] allFiles = Directory.GetFiles(savePath, "*.json");
                    foreach (string file in allFiles)
                    {
                        try
                        {
                            preload[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file);
                        }
                        catch { }
                    }
                }
                else
                {
                    Directory.CreateDirectory(savePath);
                }
            });
        }
    }

    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String && reader.TokenType != JsonTokenType.Null)
                throw new JsonException("Color must be a string.");
            string value = reader.GetString();
            if (string.IsNullOrEmpty(value)) return default;

            string[] parts = value.Split(':');
            if (parts.Length != 4
                || !byte.TryParse(parts[0], out byte r)
                || !byte.TryParse(parts[1], out byte g)
                || !byte.TryParse(parts[2], out byte b)
                || !byte.TryParse(parts[3], out byte a))
                throw new JsonException("Color must contain four byte values separated by colons.");

            return new Color(r, g, b, a);
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) => writer.WriteStringValue($"{value.R}:{value.G}:{value.B}:{value.A}");
    }
}

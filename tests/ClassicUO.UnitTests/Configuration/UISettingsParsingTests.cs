using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Configuration;
using ClassicUO.Configuration.Json;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Configuration
{
    public class UISettingsParsingTests
    {
        public UISettingsParsingTests() => TestLogging.EnsureInitialized();

        [Theory]
        [InlineData("{\"X\":12,\"Y\":34}")]
        [InlineData("{\"Y\":34,\"X\":12}")]
        [InlineData("{\"Extra\":{\"Nested\":[1,2]},\"Y\":34,\"X\":12}")]
        public void Point_properties_are_read_by_name_and_unknown_values_are_skipped(string json)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Point2Converter());
            options.Converters.Add(new NullablePoint2Converter());

            Assert.Equal(new Point(12, 34), JsonSerializer.Deserialize<Point>(json, options));
            Assert.Equal((Point?)new Point(12, 34), JsonSerializer.Deserialize<Point?>(json, options));
            Assert.Null(JsonSerializer.Deserialize<Point?>("null", options));
        }

        [Theory]
        [InlineData("{\"X\":\"12\",\"Y\":34}")]
        [InlineData("{\"X\":2147483648,\"Y\":34}")]
        [InlineData("[12,34]")]
        [InlineData("{\"X\":12")]
        public void Malformed_points_report_JSON_errors(string json)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Point2Converter());
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Point>(json, options));
        }

        [Theory]
        [InlineData("\"255:128:0:64\"", 255, 128, 0, 64)]
        [InlineData("\"0:0:0:0\"", 0, 0, 0, 0)]
        public void Color_values_round_trip_without_changing_channels(string json, byte r, byte g, byte b, byte a)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ColorJsonConverter());
            var color = new Color(r, g, b, a);
            Assert.Equal(color, JsonSerializer.Deserialize<Color>(json, options));
            Assert.Equal(json, JsonSerializer.Serialize(color, options));
        }

        [Theory]
        [InlineData("\"1:2\"")]
        [InlineData("\"1:2:3\"")]
        [InlineData("\"1:2:3:256\"")]
        [InlineData("\"-1:2:3:4\"")]
        [InlineData("\"1:two:3:4\"")]
        [InlineData("123")]
        public void Invalid_UI_color_falls_back_to_default_settings(string color)
        {
            string name = "audit-" + Guid.NewGuid().ToString("N");
            var preload = (ConcurrentDictionary<string, string>)typeof(UISettings)
                .GetField("preload", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            preload[name] = "{\"Color\":" + color + "}";

            Assert.Null(UISettings.Load<TestSettings>(name));
            Assert.False(preload.ContainsKey(name));
        }

        internal sealed class TestSettings : UISettings
        {
            public TestSettings() { }
            [JsonConverter(typeof(ColorJsonConverter))]
            public Color Color { get; set; }
        }
    }
}

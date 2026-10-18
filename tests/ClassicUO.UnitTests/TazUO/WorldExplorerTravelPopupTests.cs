using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Gumps;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class WorldExplorerTravelPopupTests
    {
        [Fact]
        public void All_four_generated_styles_are_embedded_as_distinct_RGBA_images()
        {
            var hashes = new HashSet<string>();
            for (int i = 0; i < WorldExplorerTravelCard.StyleNames.Length; i++)
            {
                using (var stream = typeof(WorldExplorerTravelGump).Assembly.GetManifestResourceStream(WorldExplorerTravelCard.ResourceName(i)))
                {
                    Assert.NotNull(stream);
                    var header = new byte[26];
                    Assert.Equal(header.Length, stream.Read(header, 0, header.Length));
                    Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, new[]
                        { header[0], header[1], header[2], header[3], header[4], header[5], header[6], header[7] });
                    int width = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
                    int height = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
                    Assert.Equal(height * 3, width);
                    Assert.Equal((byte)6, header[25]);
                    stream.Position = 0;
                    using (var hash = SHA256.Create())
                        Assert.True(hashes.Add(System.Convert.ToBase64String(hash.ComputeHash(stream))));
                }
            }
            Assert.Equal(4, hashes.Count);
        }

        [Theory]
        [InlineData((byte)0)]
        [InlineData((byte)1)]
        [InlineData((byte)2)]
        [InlineData((byte)3)]
        public void Selected_style_and_position_survive_profile_serialization(byte style)
        {
            var profile = new Profile { WorldExplorerTravelStyle = style, WorldExplorerTravelPosition = new Point(style * 100, style * 50) };
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(style, restored.WorldExplorerTravelStyle);
            Assert.Equal(profile.WorldExplorerTravelPosition, restored.WorldExplorerTravelPosition);
        }

        [Fact]
        public void Existing_profiles_default_to_moongate_and_invalid_style_has_a_safe_fallback()
        {
            var old = JsonSerializer.Deserialize("{}", ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal((byte)1, old.WorldExplorerTravelStyle);
            Assert.Null(old.WorldExplorerTravelPosition);
            Assert.Equal(1, WorldExplorerTravelCard.StyleIndex(255));
        }

        [Theory]
        [InlineData("rune", (byte)0, "Recall")]
        [InlineData("book", (byte)1, "Sacred Journey")]
        [InlineData("atlas", (byte)2, "Recall (charges)")]
        [InlineData("portal", (byte)0, "Portal phrase")]
        [InlineData("portal", (byte)2, "Portal phrase")]
        public void Notification_identifies_the_actual_travel_method(string kind, byte method, string expected) =>
            Assert.Equal(expected, WorldExplorerTravelGump.MethodName(kind, method));
    }
}

using System.Text.Json;
using ClassicUO.Configuration;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class WorldQualityTests
    {
        [Fact]
        public void OlderProfilesKeepTheirFilterAndReceiveOptionalQualityDefaults()
        {
            Profile profile = JsonSerializer.Deserialize(
                "{\"post_processing_type\":3,\"visual_enhancements\":{\"linear_light\":true}}",
                ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal((ushort)3, profile.PostProcessingType);
            Assert.True(profile.VisualEnhancements.LinearLight);
            Assert.False(profile.VisualEnhancements.WorldAntiAliasing);
            Assert.Equal(35, profile.VisualEnhancements.WorldAntiAliasingStrength);
            Assert.Equal(75, profile.VisualEnhancements.PixelArtSharpness);
            Assert.True(profile.MagnifierSmooth);
        }

        [Fact]
        public void WorldAndMagnifierChoicesPersistIndependently()
        {
            var profile = new Profile { PostProcessingType = 4, MagnifierSmooth = false, MagnifierZoom = 3 };
            profile.VisualEnhancements.WorldAntiAliasing = true;
            profile.VisualEnhancements.WorldAntiAliasingStrength = 60;
            profile.VisualEnhancements.PixelArtSharpness = 90;
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            Profile loaded = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal((ushort)4, loaded.PostProcessingType);
            Assert.True(loaded.VisualEnhancements.WorldAntiAliasing);
            Assert.Equal(60, loaded.VisualEnhancements.WorldAntiAliasingStrength);
            Assert.Equal(90, loaded.VisualEnhancements.PixelArtSharpness);
            Assert.False(loaded.MagnifierSmooth);
            Assert.Equal(3, loaded.MagnifierZoom);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ExistingFilterSelectionsKeepTheirSavedValues(int selection)
        {
            var profile = new Profile { PostProcessingType = (ushort)selection };
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(profile.PostProcessingType,
                JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile).PostProcessingType);
        }
    }
}

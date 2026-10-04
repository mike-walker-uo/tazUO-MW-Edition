using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI;
using ClassicUO.Renderer;
using ClassicUO.Input;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ClientEnhancementTests
    {
        [Theory]
        [InlineData("HP bar", "Show healthbar")]
        [InlineData("viewport", "Lock game window")]
        [InlineData("keybind", "Shortcut modifier")]
        [InlineData("bag", "Container grid")]
        [InlineData("audio", "Sound volume")]
        public void OptionSynonymsFindExistingLabels(string query, string label) => Assert.True(OptionsSearch.Matches(label, query));

        [Fact]
        public void UnrelatedSearchDoesNotMatch() => Assert.False(OptionsSearch.Matches("Container grid", "weather"));

        [Theory]
        [InlineData("Hello Alice!", "Alice", "", "", true)]
        [InlineData("Malice is here", "Alice", "", "", false)]
        [InlineData("[ABC] meet at bank", "", "ABC", "", true)]
        [InlineData("Need a RESCUE", "", "", "help, rescue", true)]
        [InlineData("Nothing special", "", "", " , ; ", false)]
        [InlineData("@Änne: hello", "Änne", "", "", true)]
        public void MentionsUseWholeNamesAndWords(string text, string name, string guild, string words, bool expected) =>
            Assert.Equal(expected, ChatMentions.Matches(text, name, guild, words));

        [Fact]
        public void VisualBudgetFallsUnderLoadAndRecoversAtNormalFrameTime()
        {
            float factor = 1f;
            for (int i = 0; i < 100; i++) factor = VisualBudget.Adjust(factor, 30, 16.67);
            Assert.Equal(0.25f, factor);
            for (int i = 0; i < 100; i++) factor = VisualBudget.Adjust(factor, 16.67, 16.67);
            Assert.Equal(1f, factor);
        }

        [Fact]
        public void WeatherScheduleRespectsClimateAndIndoorBiomes()
        {
            for (int roll = 0; roll < 100; roll++)
            {
                Assert.Equal("clear", AmbientWeatherManager.Choose(AmbienceOverlay.AmbientBiome.Dungeon, Season.Winter, 1, 0, roll));
                Assert.DoesNotContain("snow", AmbientWeatherManager.Choose(AmbienceOverlay.AmbientBiome.Desert, Season.Winter, 1, 0, roll));
                Assert.DoesNotContain("snow", AmbientWeatherManager.Choose(AmbienceOverlay.AmbientBiome.Forest, Season.Summer, 0, 0, roll));
            }
            Assert.Equal("snowarc", AmbientWeatherManager.Choose(AmbienceOverlay.AmbientBiome.Forest, Season.Winter, 0, 0, 0));
        }

        [Fact]
        public void OverlappingRestockRulesCannotReserveSameUnitsTwice()
        {
            var reserved = new Dictionary<uint, int>();
            Assert.Equal(70, RestockAgentManager.ReserveUnits(reserved, 123, 100, 70));
            Assert.Equal(30, RestockAgentManager.ReserveUnits(reserved, 123, 100, 50));
            Assert.Equal(0, RestockAgentManager.ReserveUnits(reserved, 123, 100, 1));
            Assert.Equal(10, RestockAgentManager.ReserveUnits(reserved, 456, 10, 50));
            Assert.Equal(100, reserved[123]);
        }

        [Fact]
        public void HistoryIncludesOnlyChangedSettings()
        {
            using var before = JsonDocument.Parse("{\"sound_volume\":100,\"music_volume\":80,\"game_window_size\":[800,600]}");
            using var after = JsonDocument.Parse("{\"sound_volume\":50,\"music_volume\":80,\"game_window_size\":[900,700]}");
            SettingsHistory.Entry edit = SettingsHistory.Changes(before.RootElement, after.RootElement);
            Assert.Single(edit.Before); Assert.Equal("100", edit.Before["sound_volume"]); Assert.Equal("50", edit.After["sound_volume"]);
        }

        [Fact]
        public void ArtPackAssetsStayInsideTheirFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "art-pack");
            Assert.Equal(Path.Combine(folder, "sprites", "chair.png"), LocalArtPack.AssetPath(folder, Path.Combine("sprites", "chair.png")));
            Assert.Throws<InvalidDataException>(() => LocalArtPack.AssetPath(folder, "../outside.png"));
        }

        [Fact]
        public void InterfaceCoordinatesScaleWithoutChangingWorldInput()
        {
            Assert.Equal(new Point(200, 120), Mouse.ScalePoint(new Point(300, 180), 1.5f));
            Assert.Equal(new Point(300, 180), Mouse.ScalePoint(new Point(300, 180), 1f));
        }

        [Fact]
        public void EffectPreviewScopeRestoresRendererState()
        {
            float opacity = UltimaBatcher2D.OptionalEffectOpacity, glow = UltimaBatcher2D.OptionalEffectGlow;
            using (EffectPresentation.For(SpellAbilityEffectId.Fireball, 0))
            {
                Assert.True(EffectPresentation.IsPreview);
                Assert.Equal(45, EffectPresentation.Count(100));
                using (EffectPresentation.For(SpellAbilityEffectId.Lightning, 2)) Assert.Equal(100, EffectPresentation.Count(100));
                Assert.Equal(45, EffectPresentation.Count(100));
            }
            Assert.False(EffectPresentation.IsPreview);
            Assert.Equal(opacity, UltimaBatcher2D.OptionalEffectOpacity); Assert.Equal(glow, UltimaBatcher2D.OptionalEffectGlow);
        }

        [Fact]
        public void HistoryAndArtManifestRecordsDeserialize()
        {
            var entry = JsonSerializer.Deserialize<SettingsHistory.Entry>("{\"Time\":\"2026-10-03\",\"Before\":{\"sound_volume\":\"80\"}}");
            Assert.Equal("80", entry.Before["sound_volume"]);
            var sprite = JsonSerializer.Deserialize<LocalArtPack.Sprite>("{\"Graphic\":100,\"File\":\"chair.png\",\"Scale\":0.5}");
            Assert.Equal(-1, sprite.Body); Assert.Equal(0.5f, sprite.Scale);
        }

        [Fact]
        public void MaskedArtUsesPartialHueWithoutChangingSpectralOrUncoloredArt()
        {
            var sprite = new LocalArtPack.Sprite { HueMask = "mask.png" };
            Assert.Equal((float)ShaderHueTranslator.SHADER_PARTIAL_HUED, LocalArtPack.MaskHue(sprite, new Vector3(5, ShaderHueTranslator.SHADER_HUED, 0.8f)).Y);
            Assert.Equal((float)ShaderHueTranslator.SHADER_NONE, LocalArtPack.MaskHue(sprite, new Vector3(0, ShaderHueTranslator.SHADER_NONE, 1)).Y);
            Assert.Equal((float)ShaderHueTranslator.SHADER_SPECTRAL, LocalArtPack.MaskHue(sprite, new Vector3(5, ShaderHueTranslator.SHADER_SPECTRAL, 1)).Y);
        }

        [Fact]
        public void NewProfileOptionsRoundTrip()
        {
            var profile = new Profile { ReducedThemeDecoration = true, ChatMentionWords = "help,rescue", InterfaceScaling = true, InterfaceScale = 1.5f };
            profile.FavoriteGumpThemes.Add(59); profile.GumpThemeOverrides["GlobalChatGump"] = 39;
            profile.VisualEnhancements.Effects["Fireball"] = new EffectDetailSettings { Intensity = 80, Density = 50, Glow = 25 };
            profile.VisualEnhancements.MapParticleIntensity[1] = 40;
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            Profile loaded = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.True(loaded.ReducedThemeDecoration); Assert.Equal("help,rescue", loaded.ChatMentionWords);
            Assert.True(loaded.InterfaceScaling); Assert.Equal(1.5f, loaded.InterfaceScale);
            Assert.Contains((byte)59, loaded.FavoriteGumpThemes); Assert.Equal(39, loaded.GumpThemeOverrides["GlobalChatGump"]);
            Assert.Equal(50, loaded.VisualEnhancements.Effects["Fireball"].Density); Assert.Equal(40, loaded.VisualEnhancements.MapParticleIntensity[1]);
        }
    }
}

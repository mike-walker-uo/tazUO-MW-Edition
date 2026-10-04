using System;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class DailyThemeRotationTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 3);
        private static int Day(DateTime date) => (int)(date.Date.Ticks / TimeSpan.TicksPerDay);

        [Fact]
        public void RotationIsOffByDefaultAndLeavesThemeAndStampAlone()
        {
            var profile = new Profile { CustomGumpTheme = (byte)CustomGumpTheme.HdWood };
            Assert.False(profile.RotateGumpThemesDaily);
            Assert.False(CustomGumpThemeManager.RotateForDate(profile, Today));
            Assert.Equal(0, profile.LastGumpThemeRotationDay);
            Assert.Equal((byte)CustomGumpTheme.HdWood, profile.CustomGumpTheme);
        }

        [Fact]
        public void FirstCheckStartsScheduleWithoutChangingCurrentTheme()
        {
            var profile = new Profile { RotateGumpThemesDaily = true, CustomGumpTheme = (byte)CustomGumpTheme.HdWood };
            Assert.False(CustomGumpThemeManager.RotateForDate(profile, Today));
            Assert.Equal(Day(Today), profile.LastGumpThemeRotationDay);
            Assert.Equal((byte)CustomGumpTheme.HdWood, profile.CustomGumpTheme);
        }

        [Fact]
        public void NewDayAdvancesOnceAndPreservesWindowOverrides()
        {
            var profile = new Profile
            {
                RotateGumpThemesDaily = true,
                LastGumpThemeRotationDay = Day(Today.AddDays(-1)),
                CustomGumpTheme = (byte)CustomGumpTheme.HdWood
            };
            profile.GumpThemeOverrides["GlobalChatGump"] = (byte)CustomGumpTheme.Minimal;
            Assert.True(CustomGumpThemeManager.RotateForDate(profile, Today));
            byte rotated = (byte)CustomGumpThemeManager.NextTheme(CustomGumpTheme.HdWood);
            Assert.Equal(rotated, profile.CustomGumpTheme);
            Assert.False(CustomGumpThemeManager.RotateForDate(profile, Today.AddHours(23)));
            Assert.Equal(rotated, profile.CustomGumpTheme);
            Assert.Equal((byte)CustomGumpTheme.Minimal, profile.GumpThemeOverrides["GlobalChatGump"]);
        }

        [Fact]
        public void EveryAvailableThemeRotatesToAnotherAvailableThemeIncludingWraparound()
        {
            foreach (CustomGumpTheme theme in CustomGumpThemeManager.AvailableThemes)
            {
                var profile = new Profile
                {
                    RotateGumpThemesDaily = true,
                    LastGumpThemeRotationDay = Day(Today.AddDays(-1)),
                    CustomGumpTheme = (byte)theme
                };
                Assert.True(CustomGumpThemeManager.RotateForDate(profile, Today));
                Assert.NotEqual((byte)theme, profile.CustomGumpTheme);
                Assert.Contains((CustomGumpTheme)profile.CustomGumpTheme, CustomGumpThemeManager.AvailableThemes);
                if (theme == CustomGumpTheme.EternalEclipse)
                    Assert.Equal((byte)CustomGumpTheme.Minimal, profile.CustomGumpTheme);
            }
        }

        [Fact]
        public void ReturningAfterSeveralDaysAdvancesOnceAndBackwardClockDoesNotRotate()
        {
            var profile = new Profile
            {
                RotateGumpThemesDaily = true,
                LastGumpThemeRotationDay = Day(Today.AddDays(-14)),
                CustomGumpTheme = (byte)CustomGumpTheme.HdWood
            };
            Assert.True(CustomGumpThemeManager.RotateForDate(profile, Today));
            Assert.Equal((byte)CustomGumpThemeManager.NextTheme(CustomGumpTheme.HdWood), profile.CustomGumpTheme);
            Assert.False(CustomGumpThemeManager.RotateForDate(profile, Today.AddDays(-1)));
            Assert.Equal(Day(Today), profile.LastGumpThemeRotationDay);
        }

        [Fact]
        public void SavedScheduleDoesNotRotateAgainWhenProfileReloadsSameDay()
        {
            var profile = new Profile { RotateGumpThemesDaily = true, LastGumpThemeRotationDay = Day(Today) };
            string json = JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile);
            var restored = JsonSerializer.Deserialize(json, ProfileJsonContext.DefaultToUse.Profile);
            Assert.True(restored.RotateGumpThemesDaily);
            Assert.False(CustomGumpThemeManager.RotateForDate(restored, Today));
            Assert.Equal(profile.CustomGumpTheme, restored.CustomGumpTheme);
            Assert.True(CustomGumpThemeManager.RotateForDate(restored, Today.AddDays(1)));
        }

        [Fact]
        public void RotationStampIsExcludedFromOptionHistoryButThemeChangeIsTracked()
        {
            using var before = JsonDocument.Parse("{\"last_gump_theme_rotation_day\":1,\"custom_gump_theme\":34}");
            using var after = JsonDocument.Parse("{\"last_gump_theme_rotation_day\":2,\"custom_gump_theme\":35}");
            SettingsHistory.Entry edit = SettingsHistory.Changes(before.RootElement, after.RootElement);
            Assert.Single(edit.Before);
            Assert.Equal("34", edit.Before["custom_gump_theme"]);
            Assert.Equal("35", edit.After["custom_gump_theme"]);
        }
    }
}

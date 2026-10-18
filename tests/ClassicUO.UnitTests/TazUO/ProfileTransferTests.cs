using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.Gumps;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class ProfileTransferTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "tazuo-transfer-" + Guid.NewGuid().ToString("N"));
        private string Folder(string character)
        {
            string path = Path.Combine(_root, "account", "shard", character);
            Directory.CreateDirectory(path);
            return path;
        }
        private static HashSet<ProfileTransferCategory> Pick(params ProfileTransferCategory[] categories) => new HashSet<ProfileTransferCategory>(categories);

        [Fact]
        public void Discovery_excludes_current_profile_and_non_profile_folders()
        {
            string current = Folder("current"), first = Folder("first"), second = Folder("second");
            Folder("empty");
            foreach (string path in new[] { current, first, second }) File.WriteAllText(Path.Combine(path, "profile.json"), "{}");
            var found = ProfileTransfer.FindDestinations(_root, current + Path.DirectorySeparatorChar);
            Assert.Equal(new[] { first, second }, found.Select(d => d.Path));
            Assert.All(found, d => Assert.Contains("shard / account", d.Label));
            Assert.Empty(ProfileTransfer.FindDestinations(Path.Combine(_root, "missing"), current));
        }

        [Fact]
        public void Audio_only_merge_preserves_other_options_unknown_fields_and_identity()
        {
            string source = "{\"sound_volume\":7,\"music_volume\":8,\"custom_gump_theme\":9,\"username\":\"source\"}";
            string target = "{\"sound_volume\":70,\"custom_gump_theme\":2,\"future_option\":true,\"username\":\"target\"}";
            using (var merged = JsonDocument.Parse(ProfileTransfer.Merge(source, target, Pick(ProfileTransferCategory.Audio))))
            {
                Assert.Equal(7, merged.RootElement.GetProperty("sound_volume").GetInt32());
                Assert.Equal(8, merged.RootElement.GetProperty("music_volume").GetInt32());
                Assert.Equal(2, merged.RootElement.GetProperty("custom_gump_theme").GetInt32());
                Assert.True(merged.RootElement.GetProperty("future_option").GetBoolean());
                Assert.Equal("target", merged.RootElement.GetProperty("username").GetString());
            }
        }

        [Fact]
        public void All_categories_transfer_every_serialized_profile_option()
        {
            string source = JsonSerializer.Serialize(new Profile { SoundVolume = 7, CustomGumpTheme = 3,
                DisableHotkeys = true, GridContainersScale = 150, ChatMentionWords = "test", Username = "source" }, ProfileJsonContext.DefaultToUse.Profile);
            using (var expected = JsonDocument.Parse(source))
            using (var merged = JsonDocument.Parse(ProfileTransfer.Merge(source, "{\"character_name\":\"target\"}", Pick(ProfileTransferCatalog.All))))
            {
                foreach (var option in expected.RootElement.EnumerateObject())
                {
                    if (option.Name == "protected_items") continue;
                    Assert.True(ProfileTransferCatalog.JsonCategories.ContainsKey(option.Name));
                    Assert.Equal(JsonSerializer.Serialize(option.Value), JsonSerializer.Serialize(merged.RootElement.GetProperty(option.Name)));
                }
                Assert.False(merged.RootElement.TryGetProperty("username", out _));
                Assert.False(merged.RootElement.TryGetProperty("protected_items", out _));
                Assert.Equal("target", merged.RootElement.GetProperty("character_name").GetString());
            }
        }

        [Fact]
        public void Layout_is_separate_from_audio_and_map_options()
        {
            Assert.Equal(ProfileTransferCategory.Audio, ProfileTransferCatalog.JsonCategories["music_volume"]);
            Assert.Equal(ProfileTransferCategory.GumpLayout, ProfileTransferCatalog.JsonCategories["music_player_position"]);
            Assert.Equal(ProfileTransferCategory.Maps, ProfileTransferCatalog.JsonCategories["world_map_show_coordinates"]);
            Assert.Equal(ProfileTransferCategory.GumpLayout, ProfileTransferCatalog.JsonCategories["world_map_position"]);
            Assert.Equal(ProfileTransferCategory.Enhancements, ProfileTransferCatalog.JsonCategories["visual_enhancements"]);
            Assert.Equal(ProfileTransferCategory.SpellIcons, ProfileTransferCatalog.JsonCategories["spell_icon_scale"]);
            Assert.Equal(ProfileTransferCategory.SpellIcons, ProfileTransferCatalog.JsonCategories["spell_icon__display_hotkey"]);
            Assert.Equal(ProfileTransferCategory.SpellIcons, ProfileTransferCatalog.JsonCategories["spell_icon__hotkey_hue"]);
            Assert.Equal(ProfileTransferCatalog.All.Length, ProfileTransferCatalog.Labels.Length);
        }

        private static string Gumps(string origin) => $"<gumps>"
            + $"<gump type='{(int)GumpType.SkillButton}' id='21' x='10' y='20' isLocked='True' origin='{origin}' />"
            + $"<gump type='{(int)GumpType.SpellButton}' id='29' x='100' y='200' origin='{origin}' />"
            + $"<gump type='{(int)GumpType.WorldMap}' x='300' y='400' origin='{origin}' />"
            + $"<anchored_group_gump matrix_w='4' matrix_h='3'>"
            + $"<gump type='{(int)GumpType.SkillButton}' id='35' x='45' y='90' matrix_x='0' matrix_y='0' isLocked='True' origin='{origin}' />"
            + $"<gump type='{(int)GumpType.SpellButton}' id='30' x='133' y='90' matrix_x='2' matrix_y='0' origin='{origin}' />"
            + $"<gump type='{(int)GumpType.AbilityButton}' id='1' x='45' y='134' matrix_x='0' matrix_y='1' origin='{origin}' />"
            + "</anchored_group_gump></gumps>";

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void Skill_buttons_spell_icons_and_other_layouts_transfer_independently(int selected)
        {
            string source = Folder("source"), target = Folder("target");
            string original = Gumps("target");
            File.WriteAllText(Path.Combine(source, "gumps.xml"), Gumps("source"));
            File.WriteAllText(Path.Combine(target, "gumps.xml"), original);
            File.WriteAllText(Path.Combine(target, "profile.json"), "{\"spell_icon_scale\":100,\"spell_icon__display_hotkey\":true,\"spell_icon__hotkey_hue\":1}");
            File.WriteAllText(Path.Combine(target, "skillsgroups.xml"), "keep skill groups");
            File.WriteAllText(Path.Combine(target, "macros.xml"), "keep macros");
            var categories = Pick(ProfileTransferCategory.Audio);
            if ((selected & 1) != 0) categories.Add(ProfileTransferCategory.GumpLayout);
            if ((selected & 2) != 0) categories.Add(ProfileTransferCategory.SkillButtons);
            if ((selected & 4) != 0) categories.Add(ProfileTransferCategory.SpellIcons);
            var snapshot = ProfileTransfer.SnapshotFrom(source,
                "{\"spell_icon_scale\":150,\"spell_icon__display_hotkey\":false,\"spell_icon__hotkey_hue\":55}", categories);
            var result = ProfileTransfer.Apply(snapshot, target);
            var document = new XmlDocument(); document.Load(Path.Combine(target, "gumps.xml"));
            var elements = document.SelectNodes("/gumps/gump | /gumps/anchored_group_gump/gump").OfType<XmlElement>().ToArray();
            Assert.Equal(6, elements.Length);
            foreach (var gump in elements)
            {
                var type = (GumpType)int.Parse(gump.GetAttribute("type"));
                int flag = type == GumpType.SkillButton ? 2 : type == GumpType.SpellButton ? 4 : 1;
                Assert.Equal((selected & flag) != 0 ? "source" : "target", gump.GetAttribute("origin"));
                if (gump.GetAttribute("id") == "30")
                {
                    Assert.Equal("133", gump.GetAttribute("x")); Assert.Equal("90", gump.GetAttribute("y"));
                    Assert.Equal("2", gump.GetAttribute("matrix_x")); Assert.Equal("0", gump.GetAttribute("matrix_y"));
                }
            }
            foreach (XmlElement group in document.SelectNodes("/gumps/anchored_group_gump"))
            {
                Assert.Equal("4", group.GetAttribute("matrix_w")); Assert.Equal("3", group.GetAttribute("matrix_h"));
                Assert.NotEmpty(group.SelectNodes("gump").OfType<XmlElement>());
                Assert.All(group.SelectNodes("gump").OfType<XmlElement>(), g => Assert.True(g.HasAttribute("matrix_x") && g.HasAttribute("matrix_y")));
                Assert.Single(group.SelectNodes("gump").OfType<XmlElement>().Select(g => g.GetAttribute("origin")).Distinct());
            }
            Assert.All(elements.Where(e => e.GetAttribute("type") == ((int)GumpType.SkillButton).ToString()),
                skill => Assert.Equal("True", skill.GetAttribute("isLocked")));
            using var profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, "profile.json")));
            Assert.Equal((selected & 4) != 0 ? 150 : 100, profile.RootElement.GetProperty("spell_icon_scale").GetInt32());
            Assert.Equal((selected & 4) == 0, profile.RootElement.GetProperty("spell_icon__display_hotkey").GetBoolean());
            Assert.Equal((selected & 4) != 0 ? 55 : 1, profile.RootElement.GetProperty("spell_icon__hotkey_hue").GetInt32());
            Assert.Equal("keep skill groups", File.ReadAllText(Path.Combine(target, "skillsgroups.xml")));
            Assert.Equal("keep macros", File.ReadAllText(Path.Combine(target, "macros.xml")));
            if (selected != 0) Assert.Equal(original, File.ReadAllText(Path.Combine(result.BackupPath, "gumps.xml")));
            Assert.Equal(0, ProfileTransfer.Apply(snapshot, target).ChangedFiles);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Empty_or_missing_source_icons_clear_only_selected_destination_icons(bool missing)
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(target, "profile.json"), "{}");
            File.WriteAllText(Path.Combine(target, "gumps.xml"), Gumps("target"));
            if (!missing) File.WriteAllText(Path.Combine(source, "gumps.xml"), "<gumps />");
            ProfileTransfer.Apply(ProfileTransfer.SnapshotFrom(source, "{}", Pick(ProfileTransferCategory.SpellIcons)), target);
            var document = new XmlDocument(); document.Load(Path.Combine(target, "gumps.xml"));
            var elements = document.SelectNodes("//gump").OfType<XmlElement>().ToArray();
            Assert.Equal(4, elements.Length);
            Assert.DoesNotContain(elements, e => e.GetAttribute("type") == ((int)GumpType.SpellButton).ToString());
            Assert.All(elements, e => Assert.Equal("target", e.GetAttribute("origin")));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Icon_transfer_creates_missing_layout_without_copying_other_gump_types(bool skills)
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(source, "gumps.xml"), Gumps("source"));
            File.WriteAllText(Path.Combine(target, "profile.json"), "{}");
            var category = skills ? ProfileTransferCategory.SkillButtons : ProfileTransferCategory.SpellIcons;
            ProfileTransfer.Apply(ProfileTransfer.SnapshotFrom(source, "{}", Pick(category)), target);
            var document = new XmlDocument(); document.Load(Path.Combine(target, "gumps.xml"));
            var gumps = document.SelectNodes("//gump").OfType<XmlElement>().ToArray();
            Assert.Equal(2, gumps.Length);
            Assert.All(gumps, g => Assert.Equal(((int)(skills ? GumpType.SkillButton : GumpType.SpellButton)).ToString(), g.GetAttribute("type")));
        }

        [Fact]
        public void Removing_last_selected_member_removes_empty_anchor_group()
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(target, "profile.json"), "{}");
            File.WriteAllText(Path.Combine(target, "gumps.xml"), $"<gumps><anchored_group_gump matrix_w='1' matrix_h='1'>"
                + $"<gump type='{(int)GumpType.SpellButton}' id='29' matrix_x='0' matrix_y='0' />"
                + $"</anchored_group_gump><gump type='{(int)GumpType.WorldMap}' x='123' y='456' /></gumps>");
            ProfileTransfer.Apply(ProfileTransfer.SnapshotFrom(source, "{}", Pick(ProfileTransferCategory.SpellIcons)), target);
            var document = new XmlDocument(); document.Load(Path.Combine(target, "gumps.xml"));
            Assert.Empty(document.SelectNodes("//anchored_group_gump").OfType<XmlElement>());
            var remaining = Assert.Single(document.SelectNodes("//gump").OfType<XmlElement>());
            Assert.Equal("123", remaining.GetAttribute("x")); Assert.Equal("456", remaining.GetAttribute("y"));
        }

        [Theory]
        [InlineData("broken xml")]
        [InlineData("<wrong-root />")]
        public void Invalid_partial_layout_does_not_overwrite_destination_or_other_selected_options(string xml)
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(source, "gumps.xml"), xml);
            File.WriteAllText(Path.Combine(target, "profile.json"), "{\"sound_volume\":70}");
            File.WriteAllText(Path.Combine(target, "gumps.xml"), Gumps("target"));
            Assert.ThrowsAny<Exception>(() => ProfileTransfer.Apply(ProfileTransfer.SnapshotFrom(source, "{\"sound_volume\":7}",
                Pick(ProfileTransferCategory.Audio, ProfileTransferCategory.SkillButtons)), target));
            Assert.Equal("{\"sound_volume\":70}", File.ReadAllText(Path.Combine(target, "profile.json")));
            Assert.Equal(Gumps("target"), File.ReadAllText(Path.Combine(target, "gumps.xml")));
            Assert.False(Directory.Exists(Path.Combine(target, "transfer-backups")));
        }

        [Fact]
        public void Only_picked_destinations_change_and_original_files_are_backed_up()
        {
            string source = Folder("source"), picked = Folder("picked"), untouched = Folder("untouched");
            foreach (string path in new[] { source, picked, untouched }) File.WriteAllText(Path.Combine(path, "profile.json"), "{\"sound_volume\":70}");
            var snapshot = ProfileTransfer.SnapshotFrom(source, "{\"sound_volume\":7}", Pick(ProfileTransferCategory.Audio));
            var result = ProfileTransfer.Apply(snapshot, picked);
            Assert.Equal(1, result.ChangedFiles);
            Assert.Equal("{\"sound_volume\":70}", File.ReadAllText(Path.Combine(result.BackupPath, "profile.json")));
            Assert.Equal("{\"sound_volume\":70}", File.ReadAllText(Path.Combine(untouched, "profile.json")));
            Assert.Equal("{\"sound_volume\":70}", File.ReadAllText(Path.Combine(source, "profile.json")));
            Assert.Equal(0, ProfileTransfer.Apply(snapshot, picked).ChangedFiles);
            Assert.Throws<InvalidOperationException>(() => ProfileTransfer.Apply(snapshot, source));
        }

        [Fact]
        public void Full_transfer_includes_old_exclusions_but_not_journal_history_or_backups()
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(target, "profile.json"), "{}");
            string[] names = { "macros.xml", "skillsgroups.xml", "infobar.xml", "grid_containers.json", "gumps.xml", "bodyscales.tsv", "tazuo_features.tsv" };
            foreach (string name in names) File.WriteAllText(Path.Combine(source, name), "source " + name);
            File.WriteAllText(Path.Combine(source, "journal_state.tsv"), "private source history");
            File.WriteAllText(Path.Combine(target, "journal_state.tsv"), "target history");
            File.WriteAllText(Path.Combine(source, "settings-history.json"), "source settings history");
            File.WriteAllText(Path.Combine(source, "profile.json.bak1"), "old source profile");
            var snapshot = ProfileTransfer.SnapshotFrom(source, "{}", Pick(ProfileTransferCatalog.All));
            // Snapshot bytes are frozen before the worker starts.
            File.WriteAllText(Path.Combine(source, "macros.xml"), "edited later");
            ProfileTransfer.Apply(snapshot, target);
            foreach (string name in names) Assert.Equal("source " + name, File.ReadAllText(Path.Combine(target, name)));
            Assert.Equal("target history", File.ReadAllText(Path.Combine(target, "journal_state.tsv")));
            Assert.False(File.Exists(Path.Combine(target, "settings-history.json")));
            Assert.False(File.Exists(Path.Combine(target, "profile.json.bak1")));
        }

        [Fact]
        public void Missing_selected_source_files_reset_destination_but_unselected_files_stay()
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(target, "profile.json"), "{}");
            File.WriteAllText(Path.Combine(target, "macros.xml"), "old macros");
            File.WriteAllText(Path.Combine(target, "bodyscales.tsv"), "keep scales");
            var result = ProfileTransfer.Apply(ProfileTransfer.SnapshotFrom(source, "{}", Pick(ProfileTransferCategory.Macros)), target);
            Assert.False(File.Exists(Path.Combine(target, "macros.xml")));
            Assert.Equal("old macros", File.ReadAllText(Path.Combine(result.BackupPath, "macros.xml")));
            Assert.Equal("keep scales", File.ReadAllText(Path.Combine(target, "bodyscales.tsv")));
        }

        [Fact]
        public void Failed_write_rolls_back_earlier_files_in_that_destination()
        {
            string source = Folder("source"), target = Folder("target");
            File.WriteAllText(Path.Combine(target, "profile.json"), "{}");
            File.WriteAllText(Path.Combine(source, "macros.xml"), "new macros");
            File.WriteAllText(Path.Combine(source, "aliases.tsv"), "new aliases");
            File.WriteAllText(Path.Combine(target, "macros.xml"), "old macros");
            Directory.CreateDirectory(Path.Combine(target, "aliases.tsv"));
            Assert.Throws<AggregateException>(() => ProfileTransfer.Apply(
                ProfileTransfer.SnapshotFrom(source, "{}", Pick(ProfileTransferCategory.Macros)), target));
            Assert.Equal("old macros", File.ReadAllText(Path.Combine(target, "macros.xml")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(target, "profile.json")));
            Assert.Empty(Directory.GetFiles(target, "*.tmp"));
        }

        [Fact]
        public void Invalid_destination_json_and_empty_selection_do_not_write()
        {
            string source = Folder("source"), target = Folder("target");
            string file = Path.Combine(target, "profile.json");
            File.WriteAllText(file, "invalid json");
            Assert.ThrowsAny<JsonException>(() => ProfileTransfer.Apply(
                ProfileTransfer.SnapshotFrom(source, "{}", Pick(ProfileTransferCategory.Audio)), target));
            Assert.Equal("invalid json", File.ReadAllText(file));
            Assert.False(Directory.Exists(Path.Combine(target, "transfer-backups")));
            Assert.Throws<InvalidOperationException>(() => ProfileTransfer.SnapshotFrom(source, "{}", Pick()));
        }

        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}

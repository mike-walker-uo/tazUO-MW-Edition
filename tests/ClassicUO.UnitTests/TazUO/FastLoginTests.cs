using System;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Utility;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class FastLoginTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "tazuo-fastlogin-" + Guid.NewGuid().ToString("N"));
        private readonly string _oldSettingsPath = Settings.CustomSettingsFilepath;
        private FastLoginManager.Store NewStore() => new FastLoginManager.Store(_root);
        private static Settings Entry(string name = "Character") => new Settings
        {
            Username = "test-account", Password = Crypter.Encrypt("not-a-real-password"),
            IP = "127.0.0.1", Port = 2593, LastServerName = "Test shard", LastServerNum = 1,
            FastLoginCharacter = name, ClientVersion = "7.0.0.0", AutoLogin = true, SaveAccount = true,
            FPS = 73, UltimaOnlineDirectory = "assets", Plugins = new[] { "Assistant/Razor.dll" }
        };

        [Fact]
        public void Five_fixed_slots_remain_separate_and_reject_out_of_range_indices()
        {
            var store = NewStore();
            Assert.Equal(5, FastLoginManager.SlotCount);
            for (int i = 0; i < 5; i++) store.Save(i, Entry("Character " + i));
            var reloaded = NewStore(); reloaded.Reload();
            for (int i = 0; i < 5; i++) Assert.Equal("Character " + i, reloaded.Get(i).FastLoginCharacter);
            Assert.Equal(5, Directory.GetFiles(_root, "slot-*.json").Length);
            Assert.Throws<ArgumentOutOfRangeException>(() => store.FilePath(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.FilePath(5));
        }

        [Fact]
        public void Snapshot_preserves_settings_and_encoded_password_without_aliasing_source()
        {
            var store = NewStore(); Settings source = Entry("Mage Ω");
            store.Save(0, source);
            source.Username = "changed"; source.FastLoginCharacter = "Other"; source.Plugins[0] = "other.dll";
            store.Reload(); Settings saved = store.Get(0);
            Assert.Equal("test-account", saved.Username); Assert.Equal("Mage Ω", saved.FastLoginCharacter);
            Assert.Equal("Assistant/Razor.dll", saved.Plugins[0]); Assert.Equal(73, saved.FPS);
            Assert.Equal("not-a-real-password", Crypter.Decrypt(saved.Password));
            string json = File.ReadAllText(store.FilePath(0));
            Assert.DoesNotContain("not-a-real-password", json);
            using var document = JsonDocument.Parse(json);
            Assert.Equal("Mage Ω", document.RootElement.GetProperty("fast_login_character").GetString());
            Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        }

        [Fact]
        public void Replace_and_clear_change_only_the_chosen_slot()
        {
            var store = NewStore(); store.Save(0, Entry("First")); store.Save(1, Entry("Second"));
            string second = File.ReadAllText(store.FilePath(1));
            store.Save(0, Entry("Replacement"));
            Assert.Equal("Replacement", store.Get(0).FastLoginCharacter);
            store.Clear(0); store.Clear(0); store.Reload();
            Assert.Null(store.Get(0)); Assert.Null(store.Error(0));
            Assert.Equal(second, File.ReadAllText(store.FilePath(1)));
            Assert.Equal("Second", store.Get(1).FastLoginCharacter);
        }

        [Fact]
        public void Corrupt_and_incomplete_slots_do_not_disable_other_slots_or_get_treated_as_valid()
        {
            var store = NewStore(); store.Save(2, Entry("Valid"));
            File.WriteAllText(store.FilePath(0), "{bad json");
            File.WriteAllText(store.FilePath(1), "{\"fast_login_character\":\"Missing credentials\"}");
            store.Reload();
            Assert.Null(store.Get(0)); Assert.NotNull(store.Error(0));
            Assert.Null(store.Get(1)); Assert.NotNull(store.Error(1));
            Assert.Equal("Valid", store.Get(2).FastLoginCharacter);
            Assert.Null(store.Get(3)); Assert.Null(store.Error(3));
        }

        [Fact]
        public void Incomplete_replacement_is_rejected_before_touching_previous_file()
        {
            var store = NewStore(); store.Save(0, Entry("Keep"));
            string original = File.ReadAllText(store.FilePath(0));
            var bad = Entry(); bad.Password = "";
            Assert.Throws<InvalidDataException>(() => store.Save(0, bad));
            Assert.Equal(original, File.ReadAllText(store.FilePath(0)));
            Assert.Equal("Keep", store.Get(0).FastLoginCharacter);
        }

        [Theory]
        [InlineData("Character", 2)]
        [InlineData("Other", 1)]
        [InlineData("Missing", -1)]
        [InlineData("character", -1)]
        [InlineData(null, 1)]
        public void Requested_character_never_falls_back_to_another_character(string requested, int expected)
        {
            Assert.Equal(expected, FastLoginManager.FindCharacter(new[] { "", "Other", "Character" }, requested));
        }

        [Fact]
        public void Empty_character_lists_do_not_select_or_create_a_character_for_a_saved_request()
        {
            Assert.Equal(-1, FastLoginManager.FindCharacter(null, "Character"));
            Assert.Equal(-1, FastLoginManager.FindCharacter(Array.Empty<string>(), "Character"));
            Assert.Equal(-1, FastLoginManager.FindCharacter(new[] { "", "" }, "Character"));
        }

        [Fact]
        public void Connection_copy_does_not_replace_running_client_assets_plugins_or_preferences()
        {
            Settings runtime = Entry("Normal"); runtime.FPS = 90; runtime.ClientVersion = "runtime";
            Settings slot = Entry("Saved"); slot.Username = "other-account"; slot.IP = "example.invalid";
            slot.ClientVersion = "saved"; slot.FPS = 12; slot.Plugins = new[] { "not-loaded.dll" };
            FastLoginManager.CopyConnection(slot, runtime);
            Assert.Equal("other-account", runtime.Username); Assert.Equal("Saved", runtime.FastLoginCharacter);
            Assert.Equal("example.invalid", runtime.IP); Assert.Equal("runtime", runtime.ClientVersion);
            Assert.Equal(90, runtime.FPS); Assert.Equal("Assistant/Razor.dll", runtime.Plugins[0]);
            Assert.False(FastLoginManager.Compatible(slot, runtime));
            slot.ClientVersion = runtime.ClientVersion; Assert.True(FastLoginManager.Compatible(slot, runtime));
            slot.ShardType = 2; Assert.False(FastLoginManager.Compatible(slot, runtime));
        }

        [Fact]
        public void Shortcut_contains_only_quoted_executable_and_valid_slot_number()
        {
            string target = FastLoginManager.ShortcutTarget(@"D:\My Games\ClassicUO.exe", 4);
            Assert.Equal("\"D:\\My Games\\ClassicUO.exe\" -fastlogin 5", target);
            Assert.DoesNotContain("password", target); Assert.DoesNotContain("username", target);
            Assert.Throws<ArgumentOutOfRangeException>(() => FastLoginManager.ShortcutTarget("client.exe", 5));
        }

        [Theory]
        [InlineData("1", 0)]
        [InlineData("5", 4)]
        [InlineData("0", -2)]
        [InlineData("6", -2)]
        [InlineData("bad", -2)]
        public void Startup_slot_is_validated_and_consumed_once(string argument, int expected)
        {
            FastLoginManager.SetStartupSlot(argument);
            Assert.True(FastLoginManager.ConsumeStartupSlot(out int selected)); Assert.Equal(expected, selected);
            Assert.False(FastLoginManager.ConsumeStartupSlot(out _));
        }

        [Fact]
        public void Routine_settings_save_keeps_snapshot_immutable_but_normal_settings_still_save()
        {
            var store = NewStore(); store.Save(0, Entry("Pinned"));
            string original = File.ReadAllText(store.FilePath(0));
            Settings.CustomSettingsFilepath = store.FilePath(0);
            Settings snapshot = Entry("Different"); snapshot.FastLoginSnapshot = true; snapshot.Save();
            Assert.Equal(original, File.ReadAllText(store.FilePath(0)));
            string encoded = JsonSerializer.Serialize(snapshot, typeof(Settings), SettingsJsonContext.RealDefault);
            Assert.DoesNotContain("FastLoginSnapshot", encoded);
            Settings.CustomSettingsFilepath = Path.Combine(_root, "normal.json");
            Settings normal = Entry("Normal"); normal.Save();
            Assert.True(File.Exists(Settings.CustomSettingsFilepath));
        }

        public void Dispose()
        {
            Settings.CustomSettingsFilepath = _oldSettingsPath;
            FastLoginManager.ConsumeStartupSlot(out _);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }
}

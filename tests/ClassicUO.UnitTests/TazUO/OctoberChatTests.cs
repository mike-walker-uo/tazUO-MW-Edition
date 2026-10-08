using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class OctoberChatTests
    {
        [Theory]
        [InlineData(MessageType.Guild, true, false, true)]
        [InlineData(MessageType.Guild, false, true, false)]
        [InlineData(MessageType.Party, false, true, true)]
        [InlineData(MessageType.Party, true, false, false)]
        public void Private_global_inclusion_is_independent_and_opt_in(MessageType type, bool guild, bool party, bool expected)
        {
            var record = new ChatHistoryRecord("Tez", "private message", 0, DateTime.MinValue, type);
            Assert.Equal(expected, GlobalChatChannels.Matches(record, GlobalChatChannel.Global, guild, party));
            Assert.False(GlobalChatChannels.Matches(record, GlobalChatChannel.Trade, guild, party));
        }

        [Theory]
        [InlineData("[cp hello", "Pariah", "hello")]
        [InlineData(" [PARIAH  hello", "Pariah", "hello")]
        [InlineData("[chat hello", "Global", "hello")]
        [InlineData("[trade WTB ore", "Trade", "WTB ore")]
        [InlineData("[event champ", "Events", "champ")]
        [InlineData("[help where?", "Help", "where?")]
        [InlineData("[lfg", "LFG", "")]
        [InlineData("\\ hello", "Guild", "hello")]
        [InlineData("/ hello", "Party", "hello")]
        public void Composer_recognizes_commands_and_aliases_without_double_prefixes(string input, string channel, string expected)
        {
            Assert.True(GlobalChatChannels.TryReadCommand(input, out GlobalChatChannel selected, out string text));
            Assert.Equal((GlobalChatChannel)Enum.Parse(typeof(GlobalChatChannel), channel), selected);
            Assert.Equal(expected, text);
        }

        [Theory]
        [InlineData("[cpish hello")]
        [InlineData("ask in [pariah")]
        [InlineData("[helpful")]
        [InlineData("/add")]
        [InlineData(null)]
        public void Ordinary_text_and_other_commands_do_not_switch_channel(string text) =>
            Assert.False(GlobalChatChannels.TryReadCommand(text, out _, out _));

        [Fact]
        public void Old_saved_selections_keep_their_channel_and_All_maps_to_Global()
        {
            Assert.Equal(GlobalChatChannel.Global, GlobalChatChannels.Selectable[GlobalChatChannels.SelectionIndex(0)]);
            for (int id = 1; id <= 7; id++)
            {
                Assert.Equal((GlobalChatChannel)id, GlobalChatChannels.Selectable[GlobalChatChannels.SelectionIndex(id)]);
                Assert.Equal((GlobalChatChannel)id, GlobalChatChannels.Selectable[GlobalChatChannels.RestoreSendIndex(id - 1, true)]);
            }
            Assert.Equal(GlobalChatChannel.Pariah, GlobalChatChannels.Selectable[GlobalChatChannels.RestoreSendIndex(8, false)]);
            Assert.Equal(0, GlobalChatChannels.SelectionIndex(999));
            Assert.DoesNotContain("All", GlobalChatChannels.SelectionNames);
        }

        [Fact]
        public void Both_palettes_have_distinct_nonblack_colors_and_custom_colors_are_mode_specific()
        {
            foreach (bool light in new[] { false, true })
            {
                var colors = new HashSet<Color>();
                foreach (GlobalChatChannel channel in GlobalChatChannels.Selectable)
                {
                    Color color = GlobalChatChannels.TagColor(channel, light);
                    Assert.NotEqual(Color.Black, color);
                    Assert.True(colors.Add(color));
                }
            }
            var profile = new Profile();
            profile.GlobalChatChannelColors[GlobalChatChannels.ColorKey(GlobalChatChannel.Pariah, false)] = Color.Gold.PackedValue;
            Assert.Equal(Color.Gold, GlobalChatChannels.DisplayColor(GlobalChatChannel.Pariah, profile));
            profile.GlobalChatLightMode = true;
            Assert.Equal(GlobalChatChannels.TagColor(GlobalChatChannel.Pariah, true), GlobalChatChannels.DisplayColor(GlobalChatChannel.Pariah, profile));
            profile.GlobalChatChannelColors = null;
            Assert.Equal(255, GlobalChatChannels.DisplayColor(GlobalChatChannel.Pariah, profile).A);
        }

        [Fact]
        public void Chat_settings_round_trip_without_enabling_private_channels_by_default()
        {
            var profile = new Profile();
            Assert.False(profile.GlobalChatIncludeGuild);
            Assert.False(profile.GlobalChatIncludeParty);
            Assert.False(profile.GlobalChatLightMode);
            profile.GlobalChatIncludeGuild = true;
            profile.GlobalChatIncludeParty = true;
            profile.GlobalChatLightMode = true;
            profile.GlobalChatChannelColors["Light:Pariah"] = Color.Red.PackedValue;
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile),
                ProfileJsonContext.DefaultToUse.Profile);
            Assert.True(restored.GlobalChatIncludeGuild);
            Assert.True(restored.GlobalChatIncludeParty);
            Assert.True(restored.GlobalChatLightMode);
            Assert.Equal(Color.Red.PackedValue, restored.GlobalChatChannelColors["Light:Pariah"]);
        }

        private static ChatHistoryStore Store(int capacity = 1000) =>
            new ChatHistoryStore((MessageEventArgs _) => true, capacity, GlobalChatChannels.RepeatKey);

        private static ChatHistoryRecord Record(string text, string name = "System", MessageType type = MessageType.System) =>
            new ChatHistoryRecord(name, text, 0, DateTime.MinValue, type);

        [Fact]
        public void Repeats_consolidate_by_normalized_sender_channel_and_text_and_move_to_latest_position()
        {
            var store = Store();
            var removed = new List<long>();
            store.RecordRemoved += id => removed.Add(id);
            store.Add(Record("[Pariah] Tez: hello"));
            store.Add(Record("[Trade] Tez: hello"));
            store.Add(Record("hello", "[Pariah] Tez", MessageType.ChatSystem));
            Assert.Equal(new long[] { 2, 3 }, store.All().Select(r => r.Sequence));
            Assert.Equal(new long[] { 1 }, removed);
            Assert.Equal(2, store.All().Last().RepeatCount);
            store.Add(Record("[Pariah] Other: hello"));
            store.Add(Record("[Pariah] Tez: different"));
            Assert.Equal(4, store.Count);
        }

        [Fact]
        public void Repeat_index_is_bounded_by_capacity_and_cleared_with_session_history()
        {
            var store = Store(2);
            store.Add(Record("[Global] Tez: first"));
            store.Add(Record("[Global] Tez: second"));
            store.Add(Record("[Global] Tez: third"));
            store.Add(Record("[Global] Tez: first"));
            Assert.Equal(1, store.All().Last().RepeatCount);
            Assert.Equal(2, store.Count);
            store.Clear();
            store.Add(Record("[Global] Tez: first"));
            Assert.Equal(1, store.All().Single().RepeatCount);
            Assert.Equal(5, store.All().Single().Sequence);
        }

        [Fact]
        public void Matching_text_in_private_and_public_channels_is_never_merged()
        {
            var store = Store();
            store.Add(Record("hello", "Tez", MessageType.ChatSystem));
            store.Add(Record("hello", "Tez", MessageType.Guild));
            store.Add(Record("hello", "Tez", MessageType.Party));
            Assert.Equal(3, store.Count);
            Assert.All(store.All(), r => Assert.Equal(1, r.RepeatCount));
        }
    }
}

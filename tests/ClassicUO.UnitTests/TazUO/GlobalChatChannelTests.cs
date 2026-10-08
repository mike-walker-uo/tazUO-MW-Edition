using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class GlobalChatChannelTests
    {
        [Theory]
        [InlineData("[Global] Bowyer Longarm", "buying ore", MessageType.ChatSystem, "Global", "Bowyer Longarm", "buying ore")]
        [InlineData("  [trade] Bowyer Longarm", "price: 250gp", MessageType.Regular, "Trade", "Bowyer Longarm", "price: 250gp")]
        [InlineData("System", "[Global] Bowyer Longarm: buying ore", MessageType.System, "Global", "Bowyer Longarm", "buying ore")]
        [InlineData(null, "[Trade] Bowyer Longarm: price: 250gp", MessageType.System, "Trade", "Bowyer Longarm", "price: 250gp")]
        [InlineData("Bowyer Longarm", "[Events] Meet at 21:00", MessageType.ChatSystem, "Events", "Bowyer Longarm", "Meet at 21:00")]
        [InlineData("System", "[Help] No helpers online", MessageType.System, "Help", null, "No helpers online")]
        [InlineData("Bowyer Longarm", "[Global] quoted text", MessageType.Guild, "Guild", "Bowyer Longarm", "[Global] quoted text")]
        [InlineData("System", "[Pariah] [P] Tez: exploring", MessageType.System, "Pariah", "[P] Tez", "exploring")]
        [InlineData("[pariah] Tez", "hello", MessageType.ChatSystem, "Pariah", "Tez", "hello")]
        [InlineData("[Pariah] Tez", "[Global] quoted text", MessageType.ChatSystem, "Pariah", "Tez", "[Global] quoted text")]
        [InlineData("System", "  [Help ] Tez: where?", MessageType.System, "Help", "Tez", "where?")]
        [InlineData("  [ LFG ] Tez", "join us", MessageType.Regular, "LFG", "Tez", "join us")]
        public void Display_separates_channel_and_player_without_changing_message_content(
            string name, string text, MessageType type, string expectedChannel, string expectedName, string expectedText)
        {
            var record = new ChatHistoryRecord(name, text, 0, DateTime.MinValue, type);

            GlobalChatChannels.GetDisplayParts(record, out string player, out string message).Should()
                .Be((GlobalChatChannel)Enum.Parse(typeof(GlobalChatChannel), expectedChannel));
            player.Should().Be(expectedName);
            message.Should().Be(expectedText);
        }

        [Fact]
        public void Player_color_stays_the_same_across_channels_and_channel_colors_are_distinct()
        {
            var colors = new System.Collections.Generic.HashSet<Color>();
            for (int i = 1; i < GlobalChatChannels.Names.Length; i++)
            {
                var channel = (GlobalChatChannel)i;
                var record = new ChatHistoryRecord("[" + GlobalChatChannels.Names[i] + "] Bowyer Longarm", "hello",
                    (ushort)i, DateTime.MinValue, MessageType.ChatSystem);
                GlobalChatChannels.GetDisplayParts(record, out string player, out _).Should().Be(channel);
                ChatNameHueMap.GetHueForName(player).Should().Be(ChatNameHueMap.GetHueForName("Bowyer Longarm"));
                Color color = GlobalChatChannels.TagColor(channel);
                color.Should().NotBe(Color.Black);
                colors.Add(color).Should().BeTrue();
            }
        }

        [Theory]
        [InlineData("[Global] Far.Cast: hello", "Global", "[c")]
        [InlineData("[Trade] Tez: anybody typing here?", "Trade", "[tc")]
        [InlineData("[Events] Tez: event starting", "Events", "[ec")]
        [InlineData("[Help] Tez: where is New Haven?", "Help", "[hc")]
        [InlineData("[LFG] Tez: looking for a group", "LFG", "[lfg")]
        [InlineData("  [trade] Tez: buying ore", "Trade", "[tc")]
        [InlineData("[Pariah] Tez: hello", "Pariah", "[cp")]
        public void Public_messages_are_captured_and_routed_to_the_matching_command(string text, string channel, string command)
        {
            var message = new MessageEventArgs(null, text, "System", 0, MessageType.System, 3, TextType.SYSTEM);
            GlobalChatChannels.IsChatMessage(message).Should().BeTrue();
            GlobalChatChannel selected = (GlobalChatChannel)Enum.Parse(typeof(GlobalChatChannel), channel);
            GlobalChatChannels.GetChannel(message.Name, text, message.Type).Should().Be(selected);
            GlobalChatChannels.Command(selected).Should().Be(command);
            var record = new ChatHistoryRecord(message.Name, text, 0, DateTime.MinValue, message.Type);
            GlobalChatChannels.Matches(record, selected).Should().BeTrue();
            GlobalChatChannels.Matches(record, GlobalChatChannel.All).Should().BeTrue();
            GlobalChatChannels.Matches(record, GlobalChatChannel.Global).Should().BeTrue();
            GlobalChatChannels.Matches(record, GlobalChatChannel.Party).Should().BeFalse();
        }

        [Theory]
        [InlineData("Try [Trade] for that")]
        [InlineData("[Trader] hello")]
        [InlineData("[2] hello")]
        [InlineData("[Global hello")]
        [InlineData("Ordinary system message")]
        [InlineData("[P] Tez: a path tag is not a channel")]
        [InlineData("[Pariah-like] hello")]
        public void Unrelated_system_messages_do_not_enter_channel_history(string text)
        {
            var message = new MessageEventArgs(null, text, "System", 0, MessageType.System, 3, TextType.SYSTEM);
            GlobalChatChannels.IsChatMessage(message).Should().BeFalse();
        }

        [Fact]
        public void Tag_in_sender_name_is_recognized_and_legacy_chat_still_works()
        {
            var message = new MessageEventArgs(null, "hello", "[Trade] Tez", 0, MessageType.Regular, 3, TextType.SYSTEM);
            GlobalChatChannels.IsChatMessage(message).Should().BeTrue();
            GlobalChatChannels.GetChannel("[Trade] Tez", "hello", MessageType.Regular).Should().Be(GlobalChatChannel.Trade);
            GlobalChatChannels.GetChannel("Tez", "hello", MessageType.ChatSystem).Should().Be(GlobalChatChannel.Global);
        }

        [Theory]
        [InlineData(MessageType.Guild, "Guild")]
        [InlineData(MessageType.Party, "Party")]
        public void Private_messages_are_visible_only_in_their_channel(MessageType type, string channel)
        {
            var message = new MessageEventArgs(null, "[Global] quoted text", "Tez", 0, type, 3, TextType.SYSTEM);
            GlobalChatChannels.IsChatMessage(message).Should().BeTrue();
            var record = new ChatHistoryRecord(message.Name, message.Text, 0, DateTime.MinValue, type);
            GlobalChatChannels.Matches(record, GlobalChatChannel.All).Should().BeFalse();
            GlobalChatChannels.Matches(record, GlobalChatChannel.Global).Should().BeFalse();
            GlobalChatChannels.Matches(record, (GlobalChatChannel)Enum.Parse(typeof(GlobalChatChannel), channel)).Should().BeTrue();
        }

        [Fact]
        public void Trade_view_excludes_global_messages_even_when_they_mention_trade()
        {
            var record = new ChatHistoryRecord("Tez", "[Global] please use [Trade]", 0, DateTime.MinValue, MessageType.System);
            GlobalChatChannels.Matches(record, GlobalChatChannel.Trade).Should().BeFalse();
        }
    }
}

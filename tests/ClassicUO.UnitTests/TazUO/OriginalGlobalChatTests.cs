using System;
using ClassicUO.Network;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class OriginalGlobalChatTests : IDisposable
    {
        private const string ChatText = "UOAlive Chat Global [tc Trade [lfg LFG";
        private readonly uint _originalTicks = Time.Ticks;

        public OriginalGlobalChatTests()
        {
            Time.Ticks = 1000;
            PacketHandlers.ResetOriginalGlobalChatRequest();
        }

        public void Dispose()
        {
            PacketHandlers.ResetOriginalGlobalChatRequest();
            Time.Ticks = _originalTicks;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Without_manual_request_suppression_follows_preference(bool nativeReplacement)
        {
            Assert.Equal(nativeReplacement,
                PacketHandlers.ShouldSuppressGlobalChat(nativeReplacement, true, ChatText));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Manual_open_allows_one_response_in_either_mode(bool nativeReplacement)
        {
            PacketHandlers.AllowNextOriginalGlobalChat();
            Time.Ticks += 500;

            Assert.False(PacketHandlers.ShouldSuppressGlobalChat(nativeReplacement, true, ChatText));
            Assert.Equal(nativeReplacement,
                PacketHandlers.ShouldSuppressGlobalChat(nativeReplacement, true, ChatText));
        }

        [Theory]
        [InlineData(true, "Guild Chat: Lobby")]
        [InlineData(true, "")]
        [InlineData(false, ChatText)]
        public void Other_gumps_do_not_consume_manual_request(bool isFromServer, string text)
        {
            PacketHandlers.AllowNextOriginalGlobalChat();

            Assert.False(PacketHandlers.ShouldSuppressGlobalChat(true, isFromServer, text));
            Assert.False(PacketHandlers.ShouldSuppressGlobalChat(true, true, ChatText));
            Assert.True(PacketHandlers.ShouldSuppressGlobalChat(true, true, ChatText));
        }

        [Fact]
        public void Unanswered_request_expires()
        {
            PacketHandlers.AllowNextOriginalGlobalChat();
            Time.Ticks += 10001;

            Assert.True(PacketHandlers.ShouldSuppressGlobalChat(true, true, ChatText));
        }

        [Fact]
        public void Session_reset_clears_request()
        {
            PacketHandlers.AllowNextOriginalGlobalChat();
            PacketHandlers.ResetOriginalGlobalChatRequest();

            Assert.True(PacketHandlers.ShouldSuppressGlobalChat(true, true, ChatText));
        }
    }
}

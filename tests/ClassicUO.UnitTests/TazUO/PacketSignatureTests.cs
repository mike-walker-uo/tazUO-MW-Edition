using ClassicUO.Network;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PacketSignatureTests
    {
        [Theory]
        [InlineData("Global Chat: Lobby", true)]
        [InlineData("  Global Chat: Lobby", true)]
        [InlineData("Message containing Global Chat: later", true)]
        [InlineData("Guild Chat: Lobby", false)]
        [InlineData("", false)]
        public void GlobalChatReplacementRequiresFirstLabel(string text, bool expected)
        {
            PacketHandlers.IsGlobalChatSignature(text).Should().Be(expected);
        }

        [Theory]
        [InlineData("Tinkering Blacksmithing Repair All", true)]
        [InlineData("Tinkering Repair All", false)]
        [InlineData("Blacksmithing Repair All", false)]
        [InlineData("", false)]
        public void RepairAutomationRequiresCompleteSignature(string text, bool expected)
        {
            PacketHandlers.IsRepairBenchSignature(text).Should().Be(expected);
        }

        [Fact]
        public void WeatherSeverityUsesConfiguredBoundaries()
        {
            PacketHandlers.IsHeavySnow(40, 40, 55).Should().BeTrue();
            PacketHandlers.IsHeavySnow(55, 40, 55).Should().BeFalse();
            PacketHandlers.IsBlizzard(55, 55).Should().BeTrue();
            PacketHandlers.IsTempest(54, 55).Should().BeFalse();
            PacketHandlers.IsSleet(31, 32).Should().BeTrue();
            PacketHandlers.IsSleet(0, 32).Should().BeFalse();
        }
    }
}

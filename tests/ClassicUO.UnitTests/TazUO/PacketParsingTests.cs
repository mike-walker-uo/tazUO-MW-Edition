using ClassicUO.IO;
using ClassicUO.Network;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PacketParsingTests
    {
        [Fact]
        public void VariableLengthHeaderDistinguishesIncompleteAndMalformedPackets()
        {
            var buffer = new CircularBuffer();
            buffer.Enqueue(new byte[] { 0x03, 0x00 });

            PacketHandlers.GetPacketInfo(buffer, buffer.Length, out _, out _, out _)
                .Should().Be(PacketHandlers.PacketReadStatus.Incomplete);

            buffer.Enqueue(new byte[] { 0x00 });

            PacketHandlers.GetPacketInfo(buffer, buffer.Length, out _, out int offset, out int length)
                .Should().Be(PacketHandlers.PacketReadStatus.Malformed);
            offset.Should().Be(3);
            length.Should().Be(0);
        }

        [Fact]
        public void IncompletePacketBodyRemainsBufferedUntilCompleted()
        {
            var handler = new PacketHandlers();
            handler.Append(new byte[] { 0x03, 0x00, 0x05, 0xAA }, false);

            handler.ParsePendingPackets(1).Should().Be(0);
            handler.PendingBytes.Should().Be(4);

            handler.Append(new byte[] { 0xBB }, false);

            handler.ParsePendingPackets(1).Should().Be(1);
            handler.PendingBytes.Should().Be(0);
        }

        [Fact]
        public void PacketLimitCountsPacketsAndLeavesRemainderInOrder()
        {
            var handler = new PacketHandlers();
            handler.Append(new byte[] { 0x29, 0x29 }, false);

            handler.ParsePendingPackets(1).Should().Be(1);
            handler.PendingBytes.Should().Be(1);
            handler.ParsePendingPackets(1).Should().Be(1);
            handler.PendingBytes.Should().Be(0);
        }

        [Fact]
        public void PluginPacketsDrainWithoutNewServerData()
        {
            var handler = new PacketHandlers();
            handler.Append(new byte[] { 0x29 }, true);

            handler.ParsePendingPackets(1).Should().Be(1);
            handler.PendingBytes.Should().Be(0);
        }

        [Fact]
        public void OnePacketCallsAlternateBetweenBackloggedServerAndPluginQueues()
        {
            var handler = new PacketHandlers();
            var payloads = new List<byte>();
            handler.Add(0x73, (ref StackDataReader packet) => payloads.Add(packet.ReadUInt8()));
            handler.Append(new byte[] { 0x73, 0x01, 0x73, 0x02 }, false);
            handler.Append(new byte[] { 0x73, 0x11, 0x73, 0x12 }, true);

            handler.ParsePendingPackets(1).Should().Be(1);
            handler.ParsePendingPackets(1).Should().Be(1);
            handler.ParsePendingPackets(1).Should().Be(1);
            handler.ParsePendingPackets(1).Should().Be(1);

            payloads.Should().Equal(0x01, 0x11, 0x02, 0x12);
            handler.PendingBytes.Should().Be(0);
        }

        [Fact]
        public void IncompletePreferredQueueDoesNotBlockOtherQueue()
        {
            var handler = new PacketHandlers();
            var payloads = new List<byte>();
            handler.Add(0x73, (ref StackDataReader packet) => payloads.Add(packet.ReadUInt8()));
            handler.Append(new byte[] { 0x73 }, false);
            handler.Append(new byte[] { 0x73, 0x11 }, true);

            handler.ParsePendingPackets(1).Should().Be(1);
            payloads.Should().Equal(0x11);
            handler.PendingBytes.Should().Be(1);

            handler.Append(new byte[] { 0x01 }, false);
            handler.ParsePendingPackets(1).Should().Be(1);

            payloads.Should().Equal(0x11, 0x01);
            handler.PendingBytes.Should().Be(0);
        }
    }
}

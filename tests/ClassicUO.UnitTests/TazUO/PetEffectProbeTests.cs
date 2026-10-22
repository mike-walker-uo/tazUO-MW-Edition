using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.IO;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PetEffectProbeTests
    {
        private const uint Pet = 0x12345678;

        [Fact]
        public void Buff_packets_require_pet_serial_and_do_not_consume_reader()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            var other = new StackDataReader(BuffPacket(Pet + 1, 0x7FFF, 1, 20)); other.Seek(3);
            session.ReadBuff(ref other, 1000);
            Assert.Empty(session.Buffs); Assert.Empty(session.History); Assert.Equal(3, other.Position);

            var reader = new StackDataReader(BuffPacket(Pet, 0x7FFF, 1, 20)); reader.Seek(3);
            session.ReadBuff(ref reader, 1000);
            Assert.Equal(3, reader.Position);
            Assert.Equal("Unknown 0x7FFF", session.Buffs[0x7FFF].Name);
            Assert.Contains("20s", session.Buffs[0x7FFF].Describe(1000));
            Assert.Contains("awaiting removal", session.Buffs[0x7FFF].Describe(21000));
            Assert.Single(session.Buffs); // Timer elapsed does not invent a removal packet.

            reader = new StackDataReader(BuffPacket(Pet, 0x7FFF, 0, 0)); reader.Seek(3);
            session.ReadBuff(ref reader, 22000);
            Assert.Empty(session.Buffs); Assert.Equal(3, reader.Position);
        }

        [Fact]
        public void Data_updates_unknown_duration_and_malformed_buffs_are_safe()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            var reader = new StackDataReader(BuffPacket(Pet, (ushort)BuffIconType.Curse, 2, 0)); reader.Seek(3);
            session.ReadBuff(ref reader, 1000);
            Assert.Contains("duration unknown", session.Buffs[(ushort)BuffIconType.Curse].Describe(9000));
            byte[] truncated = BuffPacket(Pet, (ushort)BuffIconType.Curse, 1, 0);
            Array.Resize(ref truncated, 12);
            reader = new StackDataReader(truncated); reader.Seek(3);
            session.ReadBuff(ref reader, 10000);
            Assert.Single(session.Buffs);
            Assert.Contains("truncated", session.History[1].Text);
        }

        [Fact]
        public void Shared_visual_signature_is_ambiguous_and_never_confirms_active_buff()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            var reader = new StackDataReader(EffectPacket(0xC7, Pet, 0, 5002)); reader.Seek(1);
            session.ReadEffect(ref reader, 1000, 100, 100, 0);
            Assert.Equal(1, reader.Position);
            Assert.Empty(session.Buffs); Assert.Single(session.Candidates);
            Assert.Contains("Clumsy / Feeblemind / Weaken", session.Candidates[0].Text);
            Assert.Contains("anim speed/duration=10/255", session.History[0].Text);
            Assert.Contains("current status or duration", session.Report(999999));
            Assert.Single(session.Candidates); // Animation duration does not expire an inferred status.
        }

        [Theory]
        [InlineData(0x70, Pet, 0, 0)]
        [InlineData(0xC0, Pet, 0, 0)]
        [InlineData(0xC7, Pet + 1, Pet, 5002)]
        [InlineData(0xC7, Pet + 1, 0, 5002)]
        public void Legacy_or_nonattached_effects_stay_raw_even_if_graphic_matches(int packet, uint source, uint target, int particle)
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            var reader = new StackDataReader(EffectPacket((byte)packet, source, target, (ushort)particle)); reader.Seek(1);
            session.ReadEffect(ref reader, 1000, 100, 100, 0);
            Assert.Empty(session.Candidates); Assert.Empty(session.Buffs); Assert.Single(session.History);
        }

        [Fact]
        public void Sound_is_positional_only_and_distant_sound_is_ignored()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            session.Sound(0x1DF, 100, 100, 0, 1000, 100, 100, 0);
            session.Sound(0x1DF, 102, 100, 0, 1000, 100, 100, 0);
            session.Sound(0x1DF, 100, 100, 30, 1000, 100, 100, 0);
            Assert.Single(session.History); Assert.Contains("position only", session.History[0].Text);
            Assert.Empty(session.Candidates); Assert.Empty(session.Buffs);
        }

        [Fact]
        public void Truncated_particle_packet_does_not_match_and_distant_unrelated_effect_is_ignored()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            byte[] bytes = EffectPacket(0xC7, Pet, 0, 5002);
            Array.Resize(ref bytes, 40);
            var reader = new StackDataReader(bytes); reader.Seek(1);
            session.ReadEffect(ref reader, 1, 100, 100, 0);
            Assert.Empty(session.History); Assert.Empty(session.Candidates); Assert.Equal(1, reader.Position);
            bytes = EffectPacket(0xC7, Pet + 1, 0, 5002);
            reader = new StackDataReader(bytes); reader.Seek(1);
            session.ReadEffect(ref reader, 1, 200, 200, 0);
            Assert.Empty(session.History); Assert.Empty(session.Candidates);
        }

        [Fact]
        public void Pause_clears_status_and_capture_resumes_only_after_explicit_resume()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            var reader = new StackDataReader(BuffPacket(Pet, (ushort)BuffIconType.Curse, 1, 30)); reader.Seek(3);
            session.ReadBuff(ref reader, 1000);
            session.Pause(2000);
            session.ReadBuff(ref reader, 3000);
            Assert.Empty(session.Buffs); Assert.True(session.Paused);
            session.Pause(4000);
            session.ReadBuff(ref reader, 5000);
            Assert.Single(session.Buffs); Assert.False(session.Paused);
            session.Clear(); Assert.Empty(session.Buffs); Assert.Empty(session.History);
        }

        [Fact]
        public void History_is_bounded_and_empty_packets_or_unselected_session_are_ignored()
        {
            var session = new PetEffectProbe.Session { Serial = Pet };
            for (int i = 0; i < 1000; i++) session.Sound(0xFFFF, 100, 100, 0, (uint)i, 100, 100, 0);
            Assert.Equal(PetEffectProbe.Session.HistoryLimit, session.History.Count);
            var reader = new StackDataReader(Array.Empty<byte>());
            session.ReadEffect(ref reader, 1, 100, 100, 0);
            session.ReadBuff(ref reader, 1);
            session.Serial = 0;
            session.Clear();
            session.Sound(0x1DF, 100, 100, 0, 1, 100, 100, 0);
            Assert.Empty(session.History);
        }

        [Fact]
        public void Effect_match_requires_particle_and_hue_and_buff_timer_handles_tick_wrap()
        {
            Assert.NotNull(PetEffectProbe.MatchEffect(0x373A, 9913, 67));
            Assert.Null(PetEffectProbe.MatchEffect(0x373A, 9913, 0));
            Assert.Null(PetEffectProbe.MatchEffect(0x373A, 0, 67));
            var buff = new PetEffectProbe.Buff { SeenAt = uint.MaxValue - 999, Seconds = 5 };
            Assert.Contains("4s", buff.Describe(0));
        }

        private static byte[] BuffPacket(uint serial, ushort icon, ushort action, ushort seconds)
        {
            var bytes = new byte[action == 0 ? 11 : 36]; bytes[0] = 0xDF;
            U16(bytes, 1, (ushort)bytes.Length); U32(bytes, 3, serial);
            U16(bytes, 7, icon); U16(bytes, 9, action);
            if (action != 0) { U16(bytes, 15, icon); U16(bytes, 23, seconds); U32(bytes, 28, 12345); }
            return bytes;
        }

        private static byte[] EffectPacket(byte packet, uint source, uint target, ushort particle)
        {
            var bytes = new byte[packet == 0x70 ? 28 : packet == 0xC0 ? 36 : 49];
            bytes[0] = packet; bytes[1] = (byte)GraphicEffectType.FixedFrom;
            U32(bytes, 2, source); U32(bytes, 6, target); U16(bytes, 10, 0x3779);
            U16(bytes, 12, 100); U16(bytes, 14, 100); bytes[22] = 10; bytes[23] = 255;
            if (packet == 0xC7) U16(bytes, 36, particle);
            return bytes;
        }

        private static void U16(byte[] bytes, int offset, ushort value)
        { bytes[offset] = (byte)(value >> 8); bytes[offset + 1] = (byte)value; }
        private static void U32(byte[] bytes, int offset, uint value)
        { U16(bytes, offset, (ushort)(value >> 16)); U16(bytes, offset + 2, (ushort)value); }
    }
}

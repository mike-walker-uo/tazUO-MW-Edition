using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.IO;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class PetBuffMonitorTests
    {
        private const uint First = 0x0008610A, Second = 0x05E6D77D;
        private static PetBuffMonitor.Tracker TwoPets()
        {
            var tracker = new PetBuffMonitor.Tracker();
            tracker.BeginPets();
            tracker.UpdatePet(First, "First", 100, 100, 0, false, false, 1000);
            tracker.UpdatePet(Second, "Second", 100, 100, 0, false, false, 1000);
            tracker.EndPets();
            return tracker;
        }
        private static PetBuffMonitor.Visual Cue(uint serial, ushort graphic, uint hue, byte speed, byte duration, uint now = 1000)
            => new PetBuffMonitor.Visual { Serial = serial, Graphic = graphic, Hue = hue, Speed = speed, Duration = duration,
                Packet = 0xC0, Type = GraphicEffectType.FixedFrom, SeenAt = now, X = 100, Y = 100, Z = 0 };

        [Fact]
        public void Distinct_cues_track_two_stacked_pets_independently_without_exact_timers()
        {
            var t = TwoPets();
            t.Effect(Cue(First, 0x377A, 31, 244, 25));
            t.Effect(Cue(Second, 0x36CB, 67, 1, 9));
            var bleed = t.Pets[First].Statuses[BuffIconType.Bleed];
            Assert.Equal(PetBuffMonitor.Evidence.Observed, bleed.Source);
            Assert.Contains("duration unknown", bleed.Detail(2000));
            Assert.Single(t.Pets[Second].Statuses); Assert.True(t.Pets[Second].Statuses.ContainsKey(BuffIconType.Strangle));
            Assert.False(t.Pets[First].Statuses.ContainsKey(BuffIconType.Strangle));
            Assert.True(bleed.ShowIcon(10999)); Assert.False(bleed.ShowIcon(11000));
            t.Advance(31000);
            Assert.Empty(t.Pets[First].Statuses); Assert.Empty(t.Pets[Second].Statuses);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Curse_accepts_sound_before_or_after_visual_but_waits_for_full_window(bool soundFirst)
        {
            var t = TwoPets();
            if (soundFirst) t.Hear(0x1E1, 100, 100, 0, 900);
            t.Effect(Cue(First, 0x374A, 0, 10, 15));
            if (!soundFirst) t.Hear(0x1E1, 100, 100, 0, 1100);
            t.Advance(1300); Assert.Empty(t.Pets[First].Statuses);
            t.Advance(1800);
            Assert.Contains("Curse / Mass Curse", t.Pets[First].Statuses[BuffIconType.Curse].Name);
            Assert.Empty(t.Pets[Second].Statuses);
        }

        [Fact]
        public void Stacked_pet_and_npc_competitors_do_not_get_assigned_a_shared_sound_even_on_later_ticks()
        {
            foreach (uint competitor in new[] { Second, 0x123456u })
            {
                var t = TwoPets();
                t.Effect(Cue(First, 0x374A, 0, 10, 15, 1000));
                t.Hear(0x1E1, 100, 100, 0, 1100);
                t.Effect(Cue(competitor, 0x374A, 0, 10, 15, 1250));
                t.Advance(1800); t.Advance(2000); t.Advance(2600);
                Assert.Empty(t.Pets[First].Statuses); Assert.Empty(t.Pets[Second].Statuses);
            }
        }

        [Fact]
        public void Shared_visual_or_sound_alone_wrong_position_and_old_sound_do_not_create_status()
        {
            var t = TwoPets();
            t.Hear(0x1E1, 100, 100, 0, 1);
            t.Hear(0x1E1, 101, 100, 0, 1000);
            t.Hear(0x1E1, 100, 100, 1, 1000);
            t.Effect(Cue(First, 0x374A, 0, 10, 15));
            t.Advance(1800);
            Assert.Empty(t.Pets[First].Statuses);
            t.Hear(0x1E1, 100, 100, 0, 3000); t.Advance(3800);
            Assert.Empty(t.Pets[First].Statuses);
        }

        [Fact]
        public void Competing_visual_after_the_sound_window_of_first_visual_still_blocks_assignment()
        {
            var t = TwoPets();
            t.Effect(Cue(First, 0x374A, 0, 10, 15, 1000));
            t.Hear(0x1E1, 100, 100, 0, 1300);
            t.Advance(1400); Assert.Empty(t.Pets[First].Statuses);
            t.Effect(Cue(Second, 0x374A, 0, 10, 15, 1600));
            t.Advance(1800); t.Advance(2400);
            Assert.Empty(t.Pets[First].Statuses); Assert.Empty(t.Pets[Second].Statuses);
        }

        [Fact]
        public void C0_stat_buffs_can_be_distinguished_by_separate_sound_cues()
        {
            var t = TwoPets();
            t.Effect(Cue(First, 0x375A, 0, 10, 15, 1000));
            t.Effect(Cue(First, 0x375A, 0, 10, 15, 1100));
            t.Hear(0x1E7, 100, 100, 0, 1000); t.Hear(0x1EE, 100, 100, 0, 1100);
            t.Advance(1900);
            Assert.True(t.Pets[First].Statuses.ContainsKey(BuffIconType.Agility));
            Assert.True(t.Pets[First].Statuses.ContainsKey(BuffIconType.Strength));
        }

        [Fact]
        public void Known_particle_source_competes_with_stacked_legacy_visual_for_its_sound()
        {
            var t = TwoPets();
            t.Effect(Cue(First, 0x375A, 0, 10, 15));
            var other = Cue(Second, 0x375A, 0, 10, 15); other.Packet = 0xC7; other.Particle = 5010;
            t.Effect(other); t.Hear(0x1E7, 100, 100, 0, 1000); t.Advance(1800);
            Assert.Empty(t.Pets[First].Statuses);
            Assert.True(t.Pets[Second].Statuses.ContainsKey(BuffIconType.Agility));
        }

        [Theory]
        [InlineData(0x374A, 1153, 10, 30, 0xFC)] // Harm, not Evil Omen
        [InlineData(0x374A, 1181, 10, 15, 0x213)] // Mind Blast
        [InlineData(0x3709, 0, 10, 30, 0x208)] // Flame Strike
        [InlineData(0x37B9, 0, 10, 16, 0x52E)] // Evasion reaction, not buff application
        [InlineData(0x374A, 97, 1, 15, 0x1FB)] // shared damage visual
        [InlineData(0x374A, 0, 10, 15, 0x205)] // Poison cast attempt, wait for status flags
        public void Damage_and_reaction_effects_are_excluded(int graphic, int hue, int speed, int duration, int sound)
        {
            var t = TwoPets();
            t.Effect(Cue(First, (ushort)graphic, (uint)hue, (byte)speed, (byte)duration));
            t.Hear((ushort)sound, 100, 100, 0, 1000); t.Advance(1800);
            Assert.Empty(t.Pets[First].Statuses);
        }

        [Fact]
        public void Flag_statuses_follow_current_mobile_state_and_clearing_flags_removes_them()
        {
            var t = TwoPets();
            t.UpdatePet(First, "First", 100, 100, 0, true, true, 1100);
            Assert.Equal(PetBuffMonitor.Evidence.Flag, t.Pets[First].Statuses[BuffIconType.Poison].Source);
            Assert.True(t.Pets[First].Statuses.ContainsKey(BuffIconType.Paralyze));
            int revision = t.Revision;
            t.UpdatePet(First, "First", 100, 100, 0, true, true, 1200);
            Assert.Equal(revision, t.Revision); // No redraw/allocation for unchanged flags.
            t.UpdatePet(First, "First", 100, 100, 0, false, false, 1300);
            Assert.Empty(t.Pets[First].Statuses);
        }

        [Fact]
        public void Server_status_overrides_inference_expires_only_from_server_timer_and_removal_blocks_pending_cue()
        {
            var t = TwoPets();
            t.Effect(Cue(First, 0x377A, 31, 244, 25));
            t.Buff(First, (ushort)BuffIconType.Bleed, 1, 5, 1100);
            t.Effect(Cue(First, 0x377A, 31, 244, 25, 1200));
            Assert.Equal(PetBuffMonitor.Evidence.Server, t.Pets[First].Statuses[BuffIconType.Bleed].Source);
            t.Advance(6000); Assert.True(t.Pets[First].Statuses.ContainsKey(BuffIconType.Bleed));
            t.Advance(6100); Assert.Empty(t.Pets[First].Statuses);
            t.Effect(Cue(First, 0x374A, 0, 10, 15, 7000)); t.Hear(0x1E1, 100, 100, 0, 7000);
            t.Buff(First, (ushort)BuffIconType.Curse, 0, 0, 7100); t.Advance(7800);
            Assert.Empty(t.Pets[First].Statuses);
            t.Buff(Second, (ushort)BuffIconType.Bless, 2, 0, 8000); t.Advance(100000);
            Assert.Contains("duration unknown", t.Pets[Second].Statuses[BuffIconType.Bless].Detail(100000));
        }

        [Fact]
        public void Removed_or_dead_pet_leaves_no_status_and_reappearing_pet_starts_fresh()
        {
            var t = TwoPets(); t.Effect(Cue(First, 0x377A, 31, 244, 25));
            t.BeginPets(); t.UpdatePet(Second, "Second", 100, 100, 0, false, false, 1200); t.EndPets();
            Assert.False(t.Pets.ContainsKey(First));
            t.BeginPets(); t.UpdatePet(First, "First", 100, 100, 0, false, false, 1400); t.EndPets();
            Assert.Empty(t.Pets[First].Statuses); Assert.False(t.Pets.ContainsKey(Second));
            t.Clear(); Assert.Empty(t.Pets);
        }

        [Fact]
        public void Particle_ids_distinguish_stat_buffs_but_curse_still_requires_sound()
        {
            var t = TwoPets();
            var v = Cue(First, 0x375A, 0, 10, 15); v.Packet = 0xC7; v.Particle = 5011;
            t.Effect(v); Assert.True(t.Pets[First].Statuses.ContainsKey(BuffIconType.Cunning));
            v = Cue(Second, 0x374A, 0, 10, 15); v.Packet = 0xC7; v.Particle = 5028;
            t.Effect(v); t.Advance(1800); Assert.Empty(t.Pets[Second].Statuses);
        }

        [Fact]
        public void Moving_xyz_legacy_and_other_mobile_visuals_never_apply_status_to_pet()
        {
            var t = TwoPets();
            var v = Cue(First, 0x377A, 31, 244, 25); v.Type = GraphicEffectType.Moving; t.Effect(v);
            v.Type = GraphicEffectType.FixedXYZ; t.Effect(v);
            v.Type = GraphicEffectType.FixedFrom; v.Packet = 0x70; t.Effect(v);
            v.Packet = 0xC0; v.Serial = 123456; t.Effect(v);
            Assert.Empty(t.Pets[First].Statuses); Assert.Empty(t.Pets[Second].Statuses);
        }

        [Fact]
        public void Raw_readers_preserve_position_parse_signed_z_and_reject_truncation()
        {
            byte[] bytes = new byte[49]; bytes[0] = 0xC7; bytes[1] = (byte)GraphicEffectType.FixedFrom;
            U32(bytes, 2, First); U16(bytes, 10, 0x377A); U16(bytes, 12, 100); U16(bytes, 14, 100);
            bytes[16] = unchecked((byte)-40); bytes[22] = 244; bytes[23] = 25;
            U32(bytes, 28, 31); U16(bytes, 36, 9950);
            var reader = new StackDataReader(bytes); reader.Seek(1);
            Assert.True(PetBuffMonitor.TryReadVisual(ref reader, 1000, out var visual));
            Assert.Equal(1, reader.Position); Assert.Equal(-40, visual.Z); Assert.Equal((ushort)9950, visual.Particle);
            Assert.Equal(First, visual.Serial); Assert.Equal((byte)244, visual.Speed);
            bytes[0] = 0xC0; Array.Resize(ref bytes, 36); reader = new StackDataReader(bytes); reader.Seek(1);
            Assert.True(PetBuffMonitor.TryReadVisual(ref reader, 1000, out visual));
            Assert.Equal((ushort)0, visual.Particle); Assert.Equal(31u, visual.Hue); Assert.Equal(1, reader.Position);
            bytes[0] = 0xC7;
            Array.Resize(ref bytes, 40); reader = new StackDataReader(bytes); reader.Seek(1);
            Assert.False(PetBuffMonitor.TryReadVisual(ref reader, 1000, out _)); Assert.Equal(1, reader.Position);

            var t = TwoPets(); bytes = new byte[36]; bytes[0] = 0xDF;
            U32(bytes, 3, First); U16(bytes, 7, (ushort)BuffIconType.Bleed); U16(bytes, 9, 1); U16(bytes, 23, 5);
            reader = new StackDataReader(bytes); reader.Seek(3); t.ReadBuff(ref reader, 1000);
            Assert.Equal(3, reader.Position); Assert.Equal((ushort)5, t.Pets[First].Statuses[BuffIconType.Bleed].Seconds);
            U32(bytes, 3, 0x9999); reader = new StackDataReader(bytes); reader.Seek(3); t.ReadBuff(ref reader, 2000);
            Assert.Single(t.Pets[First].Statuses); Assert.Empty(t.Pets[Second].Statuses);
        }

        [Fact]
        public void Observation_windows_handle_tick_wrap()
        {
            var t = TwoPets();
            t.Effect(Cue(First, 0x377A, 31, 244, 25, uint.MaxValue - 999));
            var status = t.Pets[First].Statuses[BuffIconType.Bleed];
            Assert.True(status.ShowIcon(0)); Assert.Contains("1s ago", status.Detail(0));
            t.Advance(30000); Assert.Empty(t.Pets[First].Statuses);
        }

        private static void U16(byte[] bytes, int offset, ushort value)
        { bytes[offset] = (byte)(value >> 8); bytes[offset + 1] = (byte)value; }
        private static void U32(byte[] bytes, int offset, uint value)
        { U16(bytes, offset, (ushort)(value >> 16)); U16(bytes, offset + 2, (ushort)value); }
    }
}

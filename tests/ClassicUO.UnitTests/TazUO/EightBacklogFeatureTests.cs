using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class EightBacklogFeatureTests
    {
        [Fact]
        public void Comparison_aligns_union_without_treating_missing_properties_as_zero()
        {
            var candidate = new ItemPropertiesData("Candidate\nDamage Increase 30%\nLuck 100");
            var equipped = new ItemPropertiesData("Equipped\nDamage Increase 20%\nMana Regeneration 2");
            var rows = ComparisonPropertyRows.Build(new[] { candidate, equipped });
            var damage = rows.Single(r => r.Name == "Damage Increase");
            Assert.Equal("30% (+10)", damage.Values[0]); Assert.Equal("20%", damage.Values[1]); Assert.True(damage.Changed);
            var luck = rows.Single(r => r.Name == "Luck");
            Assert.Equal("100", luck.Values[0]); Assert.Equal("Not listed (unknown)", luck.Values[1]);
        }
        [Fact]
        public void Comparison_keeps_original_units_and_damage_range_punctuation()
        {
            var rows = ComparisonPropertyRows.Build(new[] { new ItemPropertiesData("Sword\nDamage 10-20"), new ItemPropertiesData("Sword\nDamage 5-10") });
            Assert.Equal("10-20 (+5)", rows[0].Values[0]); Assert.Equal("5-10", rows[0].Values[1]);
        }
        [Fact]
        public void Comparison_unknown_OPL_stays_unknown_and_duplicate_property_rows_are_retained()
        {
            var known = new ItemPropertiesData("Ring\nResist 10\nResist 15");
            var unknown = new ItemPropertiesData("");
            var rows = ComparisonPropertyRows.Build(new[] { known, unknown });
            Assert.Equal(2, rows.Count); Assert.All(rows, r => Assert.Equal("Unknown (OPL pending)", r.Values[1]));
        }
        [Fact]
        public void Comparison_empty_known_OPL_has_no_blank_present_row()
        {
            Assert.Empty(ComparisonPropertyRows.Build(new[] { new ItemPropertiesData("Ring"), new ItemPropertiesData("Ring") }));
        }
        [Fact]
        public void Duplicate_names_remain_distinct_sources_and_preferences_do_not_change_serials()
        {
            var a = new WorldExplorerPin { Kind = "atlas", Name = " Bank ", Serial = 42, Slot = 0 };
            var b = new WorldExplorerPin { Kind = "atlas", Name = "bank", Serial = 43, Slot = 0 };
            Assert.Equal(TravelDestinationIdentity.NameKey(a), TravelDestinationIdentity.NameKey(b));
            Assert.NotEqual(TravelDestinationIdentity.SourceKey(a), TravelDestinationIdentity.SourceKey(b));
            b.Serial = a.Serial; b.Slot = 1;
            Assert.NotEqual(TravelDestinationIdentity.SourceKey(a), TravelDestinationIdentity.SourceKey(b));
            Assert.Equal(42u, a.Serial); Assert.Equal(0, a.Slot);
        }
        [Fact]
        public void Old_saved_destinations_show_unknown_observation_data_instead_of_guessed_facet()
        {
            string description = TravelDestinationIdentity.Observation(new WorldExplorerPin { Serial = 42 });
            Assert.Contains("hue unknown", description); Assert.Contains("Last scan: unknown", description); Assert.Contains("facet unknown", description);
        }
        [Fact]
        public void New_settings_and_observed_source_metadata_round_trip_through_profile_JSON()
        {
            var profile = new Profile();
            profile.VisualEnhancements.RegionalSoundscape = 2; profile.VisualEnhancements.LocalSoundscapePack = true;
            profile.VisualEnhancements.SoundscapePackFolder = @"C:\UO\Sounds";
            var pin = new WorldExplorerPin { Kind = "atlas", Serial = 42, Slot = 3, Name = "Bank", SourceName = "Town atlas", ObservedHue = 0x35, LastScannedUtcTicks = 123 };
            profile.WorldExplorerPins.Add(pin);
            profile.WorldExplorerPreferredSources[TravelDestinationIdentity.NameKey(pin)] = TravelDestinationIdentity.SourceKey(pin);
            var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, ProfileJsonContext.DefaultToUse.Profile), ProfileJsonContext.DefaultToUse.Profile);
            Assert.Equal(2, restored.VisualEnhancements.RegionalSoundscape);
            Assert.True(restored.VisualEnhancements.LocalSoundscapePack); Assert.Equal(profile.VisualEnhancements.SoundscapePackFolder, restored.VisualEnhancements.SoundscapePackFolder);
            Assert.Equal(pin.SourceName, restored.WorldExplorerPins[0].SourceName); Assert.Equal(pin.ObservedHue, restored.WorldExplorerPins[0].ObservedHue);
            Assert.Equal(123, restored.WorldExplorerPins[0].LastScannedUtcTicks);
            Assert.Equal(TravelDestinationIdentity.SourceKey(pin), restored.WorldExplorerPreferredSources["BANK"]);
        }
        [Fact]
        public void Cancellation_is_idempotent_preserves_sent_count_and_stops_collection()
        {
            int stopped = 0;
            var operation = new QueuedOperation("Move") { Total = 18, Sent = 7, CancelSource = () => stopped++ };
            operation.Cancel(); operation.Cancel();
            Assert.Equal(1, stopped); Assert.True(operation.Finished); Assert.Contains("Sent 7/18", operation.Summary);
            Assert.Contains("cannot be undone", operation.Summary);
        }
        [Fact]
        public void Sent_requests_and_verified_supplies_are_distinct_results()
        {
            var operation = new QueuedOperation("Restock") { Total = 2, Sent = 2, SubmissionComplete = true, Verifying = true };
            Assert.False(operation.Finished);
            operation.Verifying = false;
            Assert.Contains("server acceptance is not tracked", operation.Summary);
            operation.Verified = true;
            Assert.Contains("Supplies verified", operation.Summary);
        }
        [Fact]
        public void Completed_queue_result_retains_verification_failure_reason()
        {
            var operation = new QueuedOperation("Restock") { Total = 2, Sent = 2, SubmissionComplete = true, Result = "Restock incomplete: 1/3 targets ready" };
            Assert.True(operation.Finished); Assert.Contains("incomplete: 1/3", operation.Summary);
        }
        [Fact]
        public void Valid_native_format_WAV_decodes_exact_PCM_bytes()
        {
            var pcm = new byte[] { 1, 2, 3, 4, 5, 6 };
            Assert.Equal(pcm, LocalSoundscapePack.DecodeWave(Wave(pcm)));
        }
        [Theory]
        [InlineData(2, 22050, 16)] [InlineData(1, 44100, 16)] [InlineData(1, 22050, 8)]
        public void Unsupported_WAV_format_is_rejected_for_native_fallback(int channels, int rate, int bits) =>
            Assert.Throws<InvalidDataException>(() => LocalSoundscapePack.DecodeWave(Wave(new byte[4], channels, rate, bits)));
        [Fact]
        public void Truncated_or_oversized_WAV_cannot_allocate_unbounded_decoded_PCM()
        {
            var truncated = Wave(new byte[4]); Array.Resize(ref truncated, truncated.Length - 1);
            Assert.Throws<InvalidDataException>(() => LocalSoundscapePack.DecodeWave(truncated));
            Assert.Throws<InvalidDataException>(() => LocalSoundscapePack.DecodeWave(Wave(new byte[30 * 44100 + 2])));
            var badChunk = Wave(new byte[4]); BitConverter.GetBytes(int.MaxValue).CopyTo(badChunk, 16);
            Assert.Throws<InvalidDataException>(() => LocalSoundscapePack.DecodeWave(badChunk));
        }
        [Fact]
        public void Regional_presets_reuse_known_UO_sounds_and_quiet_preserves_region_identity()
        {
            var classic = RegionalSoundscapes.For(0, AmbienceOverlay.AmbientBiome.Swamp);
            var regional = RegionalSoundscapes.For(1, AmbienceOverlay.AmbientBiome.Swamp);
            var quiet = RegionalSoundscapes.For(2, AmbienceOverlay.AmbientBiome.Swamp);
            Assert.Equal(0x01B, classic.Sound); Assert.Equal(0x266, regional.Sound); Assert.Equal(regional.Sound, quiet.Sound);
            Assert.True(quiet.Gain < regional.Gain); Assert.True(quiet.Pause > regional.Pause);
            Assert.Equal((byte)0, RegionalSoundscapes.Normalize(255));
        }
        private static byte[] Wave(byte[] pcm, int channels = 1, int rate = 22050, int bits = 16)
        {
            using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((ushort)1);
            writer.Write((ushort)channels); writer.Write(rate); writer.Write(rate * channels * bits / 8);
            writer.Write((ushort)(channels * bits / 8)); writer.Write((ushort)bits);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
            writer.Flush(); return memory.ToArray();
        }
    }
    [Collection("Client session regression")]
    public class EightBacklogQueueTests
    {
        [Fact]
        public void Cancelled_requests_drain_without_audio_graphics_socket_or_object_delay()
        {
            var original = MoveItemQueue.Instance; var queue = new MoveItemQueue();
            try
            {
                var operation = new QueuedOperation("Move");
                queue.EnqueueTracked(42, 99, 1, 0, 0, 0, operation); operation.SubmissionComplete = true;
                operation.Cancel(); queue.ProcessQueue();
                Assert.True(queue.IsEmpty); Assert.Equal(0, operation.Sent);
            }
            finally { queue.Clear(); typeof(MoveItemQueue).GetProperty(nameof(MoveItemQueue.Instance)).SetValue(null, original); }
        }
        [Fact]
        public void Cancelling_one_operation_does_not_cancel_foreign_queue_requests()
        {
            var original = MoveItemQueue.Instance; var queue = new MoveItemQueue();
            try
            {
                var own = new QueuedOperation("First"); var other = new QueuedOperation("Other");
                queue.EnqueueTracked(42, 99, 1, 0, 0, 0, own); queue.EnqueueTracked(43, 99, 1, 0, 0, 0, other);
                own.Cancel(); Assert.False(other.Cancelled); Assert.Equal(1, other.Total); Assert.False(queue.IsEmpty);
            }
            finally { queue.Clear(); typeof(MoveItemQueue).GetProperty(nameof(MoveItemQueue.Instance)).SetValue(null, original); }
        }
    }
}

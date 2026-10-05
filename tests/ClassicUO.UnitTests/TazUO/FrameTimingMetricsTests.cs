using System;
using System.Diagnostics;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    [Collection("Client session regression")]
    public class FrameTimingMetricsTests : IDisposable
    {
        public FrameTimingMetricsTests() => FrameTimingMetrics.Reset();
        public void Dispose() => FrameTimingMetrics.Reset();

        [Fact]
        public void One_percent_low_averages_the_slowest_frames_instead_of_using_p99()
        {
            for (int i = 0; i < 198; i++) FrameTimingMetrics.Record(10);
            FrameTimingMetrics.Record(50);
            FrameTimingMetrics.Record(100);

            FrameTimingMetrics.Snapshot(out double average, out int low);

            Assert.Equal(10.65, average, 2);
            Assert.Equal(13, low); // Two slowest frames average 75 ms.
        }

        [Fact]
        public void Draw_intervals_exclude_the_initial_timestamp()
        {
            long start = Stopwatch.Frequency * 5;
            FrameTimingMetrics.RecordDraw(start);
            FrameTimingMetrics.Snapshot(out double empty, out int noFrames);
            Assert.Equal(0, empty);
            Assert.Equal(0, noFrames);

            FrameTimingMetrics.RecordDraw(start + Stopwatch.Frequency * 20 / 1000);
            FrameTimingMetrics.RecordDraw(start + Stopwatch.Frequency * 50 / 1000);
            FrameTimingMetrics.Snapshot(out double average, out int low);

            Assert.Equal(25, average, 2);
            Assert.Equal(33, low);
        }

        [Fact]
        public void Reset_does_not_measure_time_spent_between_sessions()
        {
            FrameTimingMetrics.RecordDraw(Stopwatch.Frequency);
            FrameTimingMetrics.Record(100);
            FrameTimingMetrics.Reset();
            FrameTimingMetrics.RecordDraw(Stopwatch.Frequency * 500);
            FrameTimingMetrics.Snapshot(out double average, out int low);

            Assert.Equal(0, average);
            Assert.Equal(0, low);
        }
    }
}

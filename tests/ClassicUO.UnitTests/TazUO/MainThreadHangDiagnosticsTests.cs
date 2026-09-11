using ClassicUO.Game.Managers;
using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class MainThreadHangDiagnosticsTests
    {
        [Fact]
        public void ShortStallStartsAtFiftyMillisecondsOfMeasuredWork()
        {
            long threshold = (Stopwatch.Frequency * 50 + 999) / 1000;

            MainThreadHangDiagnostics.IsShortStall(threshold - 1).Should().BeFalse();
            MainThreadHangDiagnostics.IsShortStall(threshold).Should().BeTrue();
        }

        [Fact]
        public void PendingStallsFlushAtThirtySecondsEvenAfterNormalFrames()
        {
            long lastReport = Stopwatch.Frequency;
            long interval = Stopwatch.Frequency * 30;

            MainThreadHangDiagnostics.IsShortStallReportDue(
                lastReport + interval - 1, lastReport, 1).Should().BeFalse();
            MainThreadHangDiagnostics.IsShortStallReportDue(
                lastReport + interval, lastReport, 1).Should().BeTrue();
            MainThreadHangDiagnostics.IsShortStallReportDue(
                lastReport + interval, lastReport, 0).Should().BeFalse();
        }

        [Fact]
        public void AggregateRetainsOnlyStrictlyWorseFrame()
        {
            MainThreadHangDiagnostics.ShouldReplaceWorst(200, 100).Should().BeTrue();
            MainThreadHangDiagnostics.ShouldReplaceWorst(100, 100).Should().BeFalse();
            MainThreadHangDiagnostics.ShouldReplaceWorst(50, 100).Should().BeFalse();
        }
    }
}

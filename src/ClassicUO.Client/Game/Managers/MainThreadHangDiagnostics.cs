using ClassicUO.Network;
using ClassicUO.Utility.Logging;
using System;
using System.Diagnostics;
using System.Threading;

namespace ClassicUO.Game.Managers
{
    internal static class MainThreadHangDiagnostics
    {
        private const int STALL_THRESHOLD_MS = 5000;
        private const int REPORT_INTERVAL_MS = 30000;
        private const int SHORT_STALL_THRESHOLD_MS = 50;

        internal enum FrameStage
        {
            Other,
            Network,
            Razor,
            UI,
            Scene,
            Render,
            Count
        }

        private static Timer _timer;
        private static string _stage = "Not started";
        private static long _lastProgress;
        private static long _lastReport;
        private static int _watching;
        private static readonly long[] _frameStageTicks = new long[(int)FrameStage.Count];
        private static readonly long[] _worstFrameStageTicks = new long[(int)FrameStage.Count];
        private static long _stageStarted;
        private static long _lastShortStallReport;
        private static long _worstFrameTicks;
        private static int _shortStallCount;
        private static FrameStage _frameStage;
        private static bool _measuringStage;
        private static string _frameKind;
        private static string _worstFrameKind;

        public static void Start(string graphicsAdapter)
        {
            Stop();
            _stage = "Starting";
            _lastProgress = Stopwatch.GetTimestamp();
            _lastReport = 0;
            _lastShortStallReport = Stopwatch.GetTimestamp();
            _shortStallCount = 0;
            _worstFrameTicks = 0;

            string processArchitecture = Environment.GetEnvironmentVariable(
                "PROCESSOR_ARCHITECTURE"
            );
            string nativeArchitecture = Environment.GetEnvironmentVariable(
                "PROCESSOR_ARCHITEW6432"
            );
            Log.Info(
                $"Runtime diagnostics: process={(Environment.Is64BitProcess ? "64-bit" : "32-bit")}, "
                + $"OS={(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}, "
                + $"PROCESSOR_ARCHITECTURE={processArchitecture ?? "unknown"}, "
                + $"PROCESSOR_ARCHITEW6432={nativeArchitecture ?? "unset"}, "
                + $"graphics={graphicsAdapter ?? "unknown"}."
            );

            _timer = new Timer(CheckForStall, null, 1000, 1000);
        }

        public static void Mark(string stage)
        {
            Interlocked.Exchange(ref _stage, stage);
            Interlocked.Exchange(ref _lastProgress, Stopwatch.GetTimestamp());
        }

        public static void BeginFrame(string frameKind)
        {
            Array.Clear(_frameStageTicks, 0, _frameStageTicks.Length);
            _frameKind = frameKind;
            _measuringStage = false;
        }

        public static void BeginStage(FrameStage stage)
        {
            EndStage();
            _frameStage = stage;
            _stageStarted = Stopwatch.GetTimestamp();
            _measuringStage = true;
        }

        public static void EndStage()
        {
            if (!_measuringStage)
                return;

            _frameStageTicks[(int)_frameStage] += Stopwatch.GetTimestamp() - _stageStarted;
            _measuringStage = false;
        }

        public static void EndFrame()
        {
            EndStage();
            long now = Stopwatch.GetTimestamp();
            long activeTicks = 0;

            for (int i = 0; i < _frameStageTicks.Length; i++)
                activeTicks += _frameStageTicks[i];

            if (IsShortStall(activeTicks))
            {
                _shortStallCount++;

                if (ShouldReplaceWorst(activeTicks, _worstFrameTicks))
                {
                    _worstFrameTicks = activeTicks;
                    _worstFrameKind = _frameKind;
                    Array.Copy(
                        _frameStageTicks,
                        _worstFrameStageTicks,
                        _frameStageTicks.Length
                    );
                }
            }

            if (!IsShortStallReportDue(now, _lastShortStallReport, _shortStallCount))
                return;

            _lastShortStallReport = now;
            Log.Warn(
                $"Short main-thread frame stalls: count={_shortStallCount}, worstFrame={_worstFrameKind}, "
                + $"worstActiveWork={_worstFrameTicks * 1000 / Stopwatch.Frequency}ms, "
                + $"networkWork={WorstStageMilliseconds(FrameStage.Network)}ms, "
                + $"RazorWork={WorstStageMilliseconds(FrameStage.Razor)}ms, "
                + $"UIWork={WorstStageMilliseconds(FrameStage.UI)}ms, "
                + $"sceneWork={WorstStageMilliseconds(FrameStage.Scene)}ms, "
                + $"renderWork={WorstStageMilliseconds(FrameStage.Render)}ms, "
                + $"otherWork={WorstStageMilliseconds(FrameStage.Other)}ms."
            );

            _shortStallCount = 0;
            _worstFrameTicks = 0;
        }

        internal static bool IsShortStall(long activeTicks)
        {
            return activeTicks * 1000 / Stopwatch.Frequency >= SHORT_STALL_THRESHOLD_MS;
        }

        internal static bool ShouldReplaceWorst(long activeTicks, long worstTicks)
        {
            return activeTicks > worstTicks;
        }

        internal static bool IsShortStallReportDue(long now, long lastReport, int stallCount)
        {
            return stallCount > 0
                && (now - lastReport) * 1000 / Stopwatch.Frequency >= REPORT_INTERVAL_MS;
        }

        private static long WorstStageMilliseconds(FrameStage stage)
        {
            return _worstFrameStageTicks[(int)stage] * 1000 / Stopwatch.Frequency;
        }

        public static void Stop()
        {
            Interlocked.Exchange(ref _timer, null)?.Dispose();
        }

        private static void CheckForStall(object state)
        {
            if (Interlocked.Exchange(ref _watching, 1) != 0)
                return;

            try
            {
                long now = Stopwatch.GetTimestamp();
                long lastProgress = Interlocked.Read(ref _lastProgress);
                long stalledMilliseconds = (now - lastProgress) * 1000 / Stopwatch.Frequency;

                if (stalledMilliseconds < STALL_THRESHOLD_MS)
                    return;

                long lastReport = Interlocked.Read(ref _lastReport);
                long sinceLastReport = (now - lastReport) * 1000 / Stopwatch.Frequency;

                if (lastReport != 0 && sinceLastReport < REPORT_INTERVAL_MS)
                    return;

                Interlocked.Exchange(ref _lastReport, now);

                AsyncNetClient socket = AsyncNetClient.Socket;
                Log.Warn(
                    $"Possible main-thread stall: stage={_stage}, elapsed={stalledMilliseconds}ms, "
                    + $"incomingChunks={socket.IncomingMessageCount}, incomingBytes={socket.IncomingBytes}, "
                    + $"oldestIncoming={socket.OldestIncomingMessageAgeMilliseconds}ms, "
                    + $"parserBytes={PacketHandlers.Handler.PendingBytes}."
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hang diagnostics failed: {ex}");
            }
            finally
            {
                Volatile.Write(ref _watching, 0);
            }
        }
    }
}

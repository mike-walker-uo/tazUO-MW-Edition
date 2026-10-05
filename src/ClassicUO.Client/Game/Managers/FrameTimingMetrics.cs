// TazUO MW Edition addition.
using System;
using System.Diagnostics;

namespace ClassicUO.Game.Managers
{
    internal static class FrameTimingMetrics
    {
        private const int CAPACITY = 240;
        private static readonly double[] _samples = new double[CAPACITY];
        private static readonly double[] _scratch = new double[CAPACITY];
        private static int _next, _count;
        private static long _lastDraw;

        // Draw-to-draw cadence includes frame limiting and the previous presentation wait.
        // It is not Update elapsed time or GPU execution time.
        internal static void RecordDraw(long timestamp)
        {
            if (_lastDraw != 0 && timestamp > _lastDraw)
                Record((timestamp - _lastDraw) * 1000.0 / Stopwatch.Frequency);
            _lastDraw = timestamp;
        }

        internal static void Reset()
        {
            _next = _count = 0;
            _lastDraw = 0;
        }

        public static void Record(double milliseconds)
        {
            if (milliseconds <= 0 || milliseconds > 10000) return;
            _samples[_next] = milliseconds;
            _next = (_next + 1) % CAPACITY;
            if (_count < CAPACITY) _count++;
        }

        public static void Snapshot(out double averageMilliseconds, out int onePercentLowFps)
        {
            if (_count == 0) { averageMilliseconds = 0; onePercentLowFps = 0; return; }
            double total = 0;
            for (int i = 0; i < _count; i++)
            {
                double value = _samples[i];
                _scratch[i] = value;
                total += value;
            }
            averageMilliseconds = total / _count;
            Array.Sort(_scratch, 0, _count);
            int slowCount = Math.Max(1, (int)Math.Ceiling(_count * 0.01));
            double slowTotal = 0;
            for (int i = _count - slowCount; i < _count; i++) slowTotal += _scratch[i];
            onePercentLowFps = (int)Math.Round(1000.0 * slowCount / slowTotal);
        }
    }
}

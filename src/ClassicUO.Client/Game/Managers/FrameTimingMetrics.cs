// TazUO MW Edition addition.
using System;

namespace ClassicUO.Game.Managers
{
    internal static class FrameTimingMetrics
    {
        private const int CAPACITY = 240;
        private static readonly double[] _samples = new double[CAPACITY];
        private static readonly double[] _scratch = new double[CAPACITY];
        private static int _next, _count;

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
            int slowIndex = Math.Max(0, (int)Math.Ceiling(_count * 0.99) - 1);
            onePercentLowFps = _scratch[slowIndex] > 0 ? (int)Math.Round(1000 / _scratch[slowIndex]) : 0;
        }
    }
}

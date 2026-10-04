using System;
using ClassicUO.Configuration;

namespace ClassicUO.Game.Managers
{
    internal static class VisualBudget
    {
        private static double _averageMs;
        private static int _frames;
        internal static float Factor { get; private set; } = 1f;
        internal static VisualEnhancementSettings Settings => ProfileManager.CurrentProfile?.VisualEnhancements;

        internal static void Frame(double elapsedMs, int targetFPS)
        {
            if (Settings?.AdaptiveParticles != true || !World.InGame || !Client.Game.IsActive) { Factor = 1f; _averageMs = 0; _frames = 0; return; }
            // Filter pauses and resize stalls. Adjust slowly; never alter authoritative game objects.
            if (elapsedMs <= 0 || elapsedMs > 250) return;
            _averageMs = _averageMs == 0 ? elapsedMs : _averageMs * 0.95 + elapsedMs * 0.05;
            if (++_frames < 45) return;
            _frames = 0;
            Factor = Adjust(Factor, _averageMs, 1000.0 / Math.Max(20, Math.Min(240, targetFPS)));
        }

        internal static float Adjust(float factor, double frameMs, double budgetMs)
        {
            if (frameMs > budgetMs * 1.10) factor -= 0.08f;
            else if (frameMs < budgetMs * 1.01) factor += 0.025f;
            return Math.Max(0.25f, Math.Min(1f, factor));
        }

        internal static float Percent(int value) => Math.Max(0, Math.Min(200, value)) / 100f;
        internal static float MapDensity => Settings?.MapParticleIntensity != null
            && Settings.MapParticleIntensity.TryGetValue(World.MapIndex, out int value) ? Percent(value) : 1f;
    }
}

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Renderer;

namespace ClassicUO.Game.Managers
{
    internal static class EffectPresentation
    {
        private static string _effect;
        private static EffectDetailSettings _detail;
        private static float _density = 1f;
        internal static float GroundY { get; private set; } = -1f;
        private static readonly string[] Names = Enum.GetNames(typeof(SpellAbilityEffectId));
        private static int _previewQuality = -1;
        private static readonly Dictionary<string, uint> Recent = new Dictionary<string, uint>();
        private static uint _window;
        private static int _distant;
        internal readonly struct Scope : IDisposable
        {
            private readonly string _previous;
            private readonly EffectDetailSettings _previousDetail;
            private readonly float _previousDensity;
            private readonly int _quality;
            private readonly float _opacity, _glow, _ground;
            internal Scope(string effect, int quality, float ground)
            {
                _previous = _effect; _quality = _previewQuality; _previousDetail = _detail; _previousDensity = _density;
                _ground = GroundY; GroundY = ground;
                _opacity = UltimaBatcher2D.OptionalEffectOpacity; _glow = UltimaBatcher2D.OptionalEffectGlow;
                _effect = effect; _previewQuality = quality;
                _detail = null;
                if (VisualBudget.Settings?.Effects != null) VisualBudget.Settings.Effects.TryGetValue(effect, out _detail);
                float qualityDensity = quality >= 0 ? (quality == 0 ? 0.45f : quality == 1 ? 0.72f : 1f) : SceneryInteractionManager.Density;
                _density = qualityDensity * VisualBudget.Percent(_detail?.Density ?? 100);
                UltimaBatcher2D.OptionalEffectOpacity = Intensity; UltimaBatcher2D.OptionalEffectGlow = Glow;
            }
            public void Dispose()
            {
                GroundY = _ground;
                _effect = _previous; _previewQuality = _quality; _detail = _previousDetail; _density = _previousDensity;
                UltimaBatcher2D.OptionalEffectOpacity = _opacity; UltimaBatcher2D.OptionalEffectGlow = _glow;
            }
        }
        internal static Scope For(SpellAbilityEffectId id, int quality = -1, float groundY = -1f) => new Scope(Names[(int)id], quality < 0 ? _previewQuality : quality, groundY);
        private static EffectDetailSettings Detail => _detail;
        internal static float Intensity => VisualBudget.Percent(Detail?.Intensity ?? 100);
        internal static bool IsPreview => _previewQuality >= 0;
        internal static float Glow => VisualBudget.Percent(Detail?.Glow ?? 100);
        internal static int Count(int count) => Math.Max(0, (int)Math.Round(count * _density));

        internal static bool Admit(string kind, uint source, ushort x, ushort y)
        {
            if (IsPreview || VisualBudget.Settings?.PrioritizeCombatEffects != true || World.Player == null) return true;
            if (source == World.Player.Serial || source != 0 && source == TargetManager.LastTargetInfo.Serial) return true;
            if (Near(x, y, World.Player.X, World.Player.Y)) return true;
            var target = World.Get(TargetManager.LastTargetInfo.Serial);
            if (target != null && Near(x, y, target.X, target.Y)) return true;
            uint now = Time.Ticks;
            if (now - _window >= 250) { _window = now; _distant = 0; }
            string key = kind + ":" + (x / 2) + ":" + (y / 2);
            if (Recent.TryGetValue(key, out uint last) && now - last < 650) return false;
            if (_distant >= Math.Max(8, (int)(32 * VisualBudget.Factor))) return false;
            if (Recent.Count > 256) Recent.Clear();
            Recent[key] = now; _distant++;
            return true;
        }
        private static bool Near(int x, int y, int tx, int ty) => Math.Max(Math.Abs(x - tx), Math.Abs(y - ty)) <= 8;
        internal static void Reset() { Recent.Clear(); _window = 0; _distant = 0; _effect = null; _previewQuality = -1; _detail = null; _density = 1f; GroundY = -1f;
            UltimaBatcher2D.OptionalEffectOpacity = UltimaBatcher2D.OptionalEffectGlow = 1f; }
    }
}

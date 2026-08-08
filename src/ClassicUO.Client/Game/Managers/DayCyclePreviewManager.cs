#region license
// TazUO addition. Session-only continuous day/dusk/night/dawn preview.
#endregion

using System;
using ClassicUO.Game.UI;

namespace ClassicUO.Game.Managers
{
    internal enum DayCyclePhase
    {
        Day,
        Dusk,
        Night,
        Dawn
    }

    internal static class DayCyclePreviewManager
    {
        public const int DEFAULT_SECONDS = 120;
        public const int MIN_SECONDS = 30;
        public const int MAX_SECONDS = 3600;

        private static uint _startedAt;
        private static uint _durationMs;
        private static int _currentLight = -1;
        private static DayCyclePhase? _currentPhase;
        private static bool _previousAmbienceEnabled;

        public static bool Enabled { get; private set; }
        public static bool IsDawn => Enabled && _currentPhase == DayCyclePhase.Dawn;

        public static void Start(int seconds)
        {
            if (Enabled)
            {
                return;
            }

            seconds = Math.Max(MIN_SECONDS, Math.Min(MAX_SECONDS, seconds));
            EnvironmentControlManager.ClearLight(false);
            Enabled = true;
            _durationMs = (uint)seconds * 1000u;
            _startedAt = Time.Ticks;
            _currentLight = -1;
            _currentPhase = null;
            _previousAmbienceEnabled = AmbienceOverlay.Enabled;
            AmbienceOverlay.Enabled = true;
            GameActions.Print($"Day cycle ON — {seconds}-second day/dusk/night/dawn loop.", 0x35);
            Update();
        }

        public static void Stop()
        {
            if (!Enabled)
            {
                return;
            }

            Enabled = false;
            EnvironmentalShadowManager.ClearPreviewLight();
            EnvironmentShowcaseManager.RestoreEffectiveLight();
            AmbienceOverlay.Enabled = _previousAmbienceEnabled;
            _currentLight = -1;
            _currentPhase = null;
            GameActions.Print("Day cycle OFF — server/profile light restored.", 0x35);
        }

        public static void Update()
        {
            if (!Enabled || !World.InGame || _durationMs == 0)
            {
                return;
            }

            uint elapsed = Time.Ticks - _startedAt;
            double progress = elapsed % _durationMs / (double)_durationMs;
            int light = CalculateLight(progress, out DayCyclePhase phase);

            if (light != _currentLight || World.Light.Overall != light)
            {
                _currentLight = light;
                World.Light.Overall = light;
                EnvironmentalShadowManager.SetPreviewLight(light);
            }
            if (World.Light.Personal != 0)
            {
                World.Light.Personal = 0;
            }

            if (_currentPhase != phase)
            {
                _currentPhase = phase;
                GameActions.Print($"Day cycle: {phase}.", 0x35);
            }
        }

        public static void ResetSession()
        {
            Enabled = false;
            _durationMs = 0;
            _currentLight = -1;
            _currentPhase = null;
        }

        internal static int CalculateLight(double progress, out DayCyclePhase phase)
        {
            progress -= Math.Floor(progress);
            const double sixth = 1.0 / 6.0;

            if (progress < sixth)
            {
                phase = DayCyclePhase.Day;
                return 0;
            }
            if (progress < 2.0 * sixth)
            {
                phase = DayCyclePhase.Dusk;
                return (int)Math.Round((progress - sixth) / sixth * 12.0);
            }
            if (progress < 4.0 * sixth)
            {
                phase = DayCyclePhase.Night;
                double nightProgress = (progress - 2.0 * sixth) / (2.0 * sixth);
                return 12 + (int)Math.Round(18.0 * Math.Sin(Math.PI * nightProgress));
            }
            if (progress < 5.0 * sixth)
            {
                phase = DayCyclePhase.Dawn;
                return (int)Math.Round((5.0 * sixth - progress) / sixth * 12.0);
            }

            phase = DayCyclePhase.Day;
            return 0;
        }
    }
}

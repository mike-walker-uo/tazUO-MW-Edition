#region license
// TazUO addition. Client-side ambient weather: shards rarely (or never) send
// weather packets, so this rolls a random condition every 8-25 minutes.
// Server weather always wins — rolls are skipped while real weather runs,
// and a server packet simply overwrites whatever this started.
// `-ambientweather on|off`.
#endregion

using ClassicUO.Configuration;
using ClassicUO.Utility;

namespace ClassicUO.Game.Managers
{
    public static class AmbientWeatherManager
    {
        public static bool Enabled { get; private set; } = true;

        private static long _nextRoll;
        private static long _fogOffAt;
        private static bool _ambientFog;

        // Multi-phase weather arc (e.g. brewing → storm → tempest → rain).
        private static string[] _seq;
        private static int _seqIdx = -1;
        private static long _seqNextAt;

        // First roll 2-5 min after login; then one roll every 8-25 min.
        private const int FIRST_MIN_S = 120, FIRST_MAX_S = 300;
        private const int ROLL_MIN_S = 480, ROLL_MAX_S = 1500;
        private const int FOG_MIN_S = 120, FOG_MAX_S = 300;
        // Per-phase duration inside an arc.
        private const int PHASE_MIN_S = 100, PHASE_MAX_S = 200;

        private static readonly string[] StormArc = { "brewing", "storm", "tempest", "rain" };
        private static readonly string[] SnowArc = { "snow", "heavysnow", "blizzard", "snow" };

        public static void Tick()
        {
            if (!Enabled || EnvironmentShowcaseManager.Enabled || !World.InGame || World.Player == null) return;
            var scene = Client.Game.GetScene<Scenes.GameScene>();
            if (scene == null) return;
            var w = scene.Weather;

            // A server packet or manual command replaced our current phase.
            // Cancel the arc instead of overwriting that external weather when
            // the next ambient phase becomes due.
            if (_seqIdx >= 0 && w.Source != WeatherSource.Ambient)
            {
                CancelSequence();
            }

            // End an ambient fog on its own timer (fog has no built-in one).
            if (_ambientFog && Time.Ticks >= _fogOffAt)
            {
                if (w.Fog && w.Source == WeatherSource.Ambient) w.Reset();
                _ambientFog = false;
            }

            // Advance a running arc.
            if (_seqIdx >= 0)
            {
                if (Time.Ticks < _seqNextAt) return;
                _seqIdx++;
                if (_seqIdx >= _seq.Length)
                {
                    // Arc finished — let the last phase run out naturally.
                    _seqIdx = -1;
                    _seq = null;
                    return;
                }
                ApplyKind(w, _seq[_seqIdx]);
                _seqNextAt = (long)Time.Ticks + RandomHelper.GetValue(PHASE_MIN_S, PHASE_MAX_S) * 1000L;
                return;
            }

            if (_nextRoll == 0)
            {
                _nextRoll = (long)Time.Ticks + RandomHelper.GetValue(FIRST_MIN_S, FIRST_MAX_S) * 1000L;
                return;
            }
            if (Time.Ticks < _nextRoll) return;
            _nextRoll = (long)Time.Ticks + RandomHelper.GetValue(ROLL_MIN_S, ROLL_MAX_S) * 1000L;

            // Never fight running weather (server-sent or a previous roll
            // still inside its 6-minute window) or active fog.
            if (w.IsActive || w.Fog) return;

            int roll = RandomHelper.GetValue(0, 99);
            if      (roll < 22) ApplyKind(w, "rain");
            else if (roll < 34) ApplyKind(w, "snow");
            else if (roll < 40) ApplyKind(w, "heavysnow");
            else if (roll < 46) StartArc(w, SnowArc);
            else if (roll < 62) StartArc(w, StormArc);
            else if (roll < 68) ApplyKind(w, "tempest");
            else if (roll < 76) ApplyKind(w, "brewing");
            else if (roll < 80) ApplyKind(w, "hail");
            else if (roll < 84) ApplyKind(w, "sleet");
            else if (roll < 93)
            {
                w.SetFog(WeatherSource.Ambient);
                _ambientFog = true;
                _fogOffAt = (long)Time.Ticks + RandomHelper.GetValue(FOG_MIN_S, FOG_MAX_S) * 1000L;
            }
            // else: clear skies this roll.
        }

        private static void StartArc(Weather w, string[] arc)
        {
            _seq = arc;
            _seqIdx = 0;
            ApplyKind(w, arc[0]);
            _seqNextAt = (long)Time.Ticks + RandomHelper.GetValue(PHASE_MIN_S, PHASE_MAX_S) * 1000L;
        }

        // Same kind vocabulary as the -weather command.
        private static void ApplyKind(Weather w, string kind)
        {
            switch (kind)
            {
                case "rain":      w.Generate(WeatherType.WT_RAIN, 70, 0, WeatherSource.Ambient); break;
                case "sleet":     w.Generate(WeatherType.WT_RAIN, 70, 0, WeatherSource.Ambient); w.Sleet = true; break;
                case "snow":      w.Generate(WeatherType.WT_SNOW, 70, 0, WeatherSource.Ambient); break;
                case "heavysnow": w.Generate(WeatherType.WT_SNOW, 200, 0, WeatherSource.Ambient); w.HeavySnow = true; break;
                case "blizzard":  w.Generate(WeatherType.WT_SNOW, 200, 0, WeatherSource.Ambient); w.Blizzard = true; break;
                case "hail":      w.Generate(WeatherType.WT_SNOW, 100, 0, WeatherSource.Ambient); w.Hail = true; break;
                case "storm":     w.Generate(WeatherType.WT_STORM_APPROACH, 70, 0, WeatherSource.Ambient); break;
                case "tempest":   w.Generate(WeatherType.WT_STORM_APPROACH, 200, 0, WeatherSource.Ambient); w.Tempest = true; break;
                case "brewing":   w.Generate(WeatherType.WT_STORM_BREWING, 70, 0, WeatherSource.Ambient); break;
            }
        }

        public static void Configure(bool enabled)
        {
            Enabled = enabled;
            ResetSchedule();
        }

        public static void CancelForExternalWeather()
        {
            CancelSequence();
            _ambientFog = false;
            _fogOffAt = 0;
        }

        public static void CancelSequence()
        {
            _seqIdx = -1;
            _seq = null;
            _seqNextAt = 0;
        }

        private static void ResetSchedule()
        {
            _nextRoll = 0;
            _ambientFog = false;
            _fogOffAt = 0;
            CancelSequence();
        }

        public static void SetEnabled(bool on)
        {
            if (!on)
            {
                var scene = Client.Game.GetScene<Scenes.GameScene>();
                if (scene?.Weather.Source == WeatherSource.Ambient)
                {
                    scene.Weather.Reset();
                }
            }

            Enabled = on;
            ResetSchedule();
            if (ProfileManager.CurrentProfile != null)
            {
                ProfileManager.CurrentProfile.AmbientWeatherEnabled = on;
            }
            GameActions.Print($"Ambient weather {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

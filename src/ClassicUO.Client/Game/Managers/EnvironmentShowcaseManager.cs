#region license
// TazUO addition. Session-only environmental showcase for quickly reviewing
// the complete light, ambience, shadow, season and weather range.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.UI;
using ClassicUO.Utility;

namespace ClassicUO.Game.Managers
{
    internal static class EnvironmentShowcaseManager
    {
        private const uint SCENE_DURATION_MS = 10000;
        private const uint BEAUTIFUL_SCENE_DURATION_MS = 10000;

        private enum ShowcaseWeather
        {
            Clear,
            Rain,
            Snow,
            HeavySnow,
            Blizzard,
            Storm,
            Tempest,
            Brewing,
            Hail,
            Sleet,
            Fog
        }

        private struct BeautifulScene
        {
            public BeautifulScene(string name, Season season, ShowcaseWeather weather, byte startLight, byte endLight)
            {
                Name = name;
                Season = season;
                Weather = weather;
                StartLight = startLight;
                EndLight = endLight;
            }

            public readonly string Name;
            public readonly Season Season;
            public readonly ShowcaseWeather Weather;
            public readonly byte StartLight;
            public readonly byte EndLight;
        }

        private static readonly Season[] _seasons =
        {
            Season.Spring,
            Season.Summer,
            Season.Fall,
            Season.Winter
        };
        private static readonly ShowcaseWeather[] _weather =
        {
            ShowcaseWeather.Clear,
            ShowcaseWeather.Rain,
            ShowcaseWeather.Snow,
            ShowcaseWeather.HeavySnow,
            ShowcaseWeather.Blizzard,
            ShowcaseWeather.Storm,
            ShowcaseWeather.Tempest,
            ShowcaseWeather.Brewing,
            ShowcaseWeather.Hail,
            ShowcaseWeather.Sleet,
            ShowcaseWeather.Fog
        };
        private static readonly BeautifulScene[] _beautifulScenes =
        {
            new BeautifulScene("Rain-Washed Sunrise", Season.Spring, ShowcaseWeather.Rain, 10, 3),
            new BeautifulScene("Spring Sunbreak", Season.Spring, ShowcaseWeather.Clear, 3, 0),
            new BeautifulScene("Summer High Sun", Season.Summer, ShowcaseWeather.Clear, 0, 2),
            new BeautifulScene("Summer Golden Hour", Season.Summer, ShowcaseWeather.Clear, 2, 8),
            new BeautifulScene("Autumn Ember Sunset", Season.Fall, ShowcaseWeather.Clear, 8, 10),
            new BeautifulScene("Autumn Rain Blue Hour", Season.Fall, ShowcaseWeather.Rain, 10, 12),
            new BeautifulScene("Summer Firefly Night", Season.Summer, ShowcaseWeather.Clear, 12, 14),
            new BeautifulScene("Winter Moonrise", Season.Winter, ShowcaseWeather.Snow, 14, 14),
            new BeautifulScene("Moonlit Heavy Snow", Season.Winter, ShowcaseWeather.HeavySnow, 14, 16),
            new BeautifulScene("Storm Gathering", Season.Summer, ShowcaseWeather.Brewing, 16, 18),
            new BeautifulScene("Thunder on the Horizon", Season.Summer, ShowcaseWeather.Storm, 18, 24),
            new BeautifulScene("Midnight Tempest", Season.Fall, ShowcaseWeather.Tempest, 24, 30),
            new BeautifulScene("Blizzard Before Dawn", Season.Winter, ShowcaseWeather.Blizzard, 30, 16),
            new BeautifulScene("Silver Fog Dawn", Season.Winter, ShowcaseWeather.Fog, 16, 10)
        };

        private static int _seasonIndex = _seasons.Length;
        private static int _weatherIndex = _weather.Length;
        private static int _beautifulIndex;
        private static BeautifulScene _beautifulScene;
        private static int _currentLight = -1;
        private static DayCyclePhase? _currentPhase;
        private static uint _sceneStartedAt;
        private static uint _nextChange;
        private static bool _previousAmbienceEnabled;

        public static bool Enabled { get; private set; }
        public static bool BeautifulEnabled { get; private set; }
        public static bool IsDawn => Enabled && _currentPhase == DayCyclePhase.Dawn;

        public static void Start(Weather weather)
        {
            if (Enabled || weather == null)
            {
                return;
            }

            EnvironmentControlManager.ClearLight(false);
            Enabled = true;
            BeautifulEnabled = false;
            _previousAmbienceEnabled = AmbienceOverlay.Enabled;
            AmbienceOverlay.Enabled = true;
            _seasonIndex = _seasons.Length;
            _weatherIndex = _weather.Length;
            _currentLight = -1;
            _currentPhase = null;
            _sceneStartedAt = 0;
            _nextChange = 0;
            AmbientWeatherManager.CancelForExternalWeather();
            GameActions.Print("Environment showcase ON — a new 10-second day/night scene.", 0x35);
            Update(weather);
        }

        public static void StartBeautiful(Weather weather)
        {
            if (Enabled || weather == null)
            {
                return;
            }

            EnvironmentControlManager.ClearLight(false);
            Enabled = true;
            BeautifulEnabled = true;
            _previousAmbienceEnabled = AmbienceOverlay.Enabled;
            AmbienceOverlay.Enabled = true;
            _beautifulIndex = 0;
            _currentLight = -1;
            _currentPhase = null;
            _sceneStartedAt = 0;
            _nextChange = 0;
            AmbientWeatherManager.CancelForExternalWeather();
            GameActions.Print(
                $"Beautiful environment showcase ON — {_beautifulScenes.Length} cinematic 10-second scenes.",
                0x35
            );
            Update(weather);
        }

        public static void Stop(Weather weather)
        {
            if (!Enabled)
            {
                return;
            }

            Enabled = false;
            BeautifulEnabled = false;
            if (weather?.Source == WeatherSource.Manual)
            {
                weather.Reset();
            }
            EnvironmentalShadowManager.ClearPreviewLight();
            World.SetSeasonOverride(null);
            RestoreEffectiveLight();
            AmbienceOverlay.Enabled = _previousAmbienceEnabled;
            _currentLight = -1;
            _currentPhase = null;
            _sceneStartedAt = 0;
            _nextChange = 0;
            GameActions.Print("Environment showcase OFF — server light and season restored.", 0x35);
        }

        public static void Update(Weather weather)
        {
            if (!Enabled || weather == null || !World.InGame)
            {
                return;
            }

            uint now = Time.Ticks;
            if (BeautifulEnabled)
            {
                UpdateBeautiful(weather, now);
                return;
            }

            if (_nextChange == 0 || now >= _nextChange)
            {
                _sceneStartedAt = now;
                _nextChange = now + SCENE_DURATION_MS;
                Season season = Next(_seasons, ref _seasonIndex);
                ShowcaseWeather weatherKind = Next(_weather, ref _weatherIndex);
                World.SetSeasonOverride(season);
                ApplyWeather(weather, weatherKind);
                GameActions.Print(
                    $"Showcase: {season}, {GetWeatherName(weatherKind)} — full 10s day/night cycle.",
                    0x35
                );
            }

            double progress = (now - _sceneStartedAt) / (double)SCENE_DURATION_MS;
            int light = DayCyclePreviewManager.CalculateLight(progress, out DayCyclePhase phase);

            ApplyLight(light, phase);
        }

        public static void ResetSession()
        {
            Enabled = false;
            BeautifulEnabled = false;
            _currentLight = -1;
            _currentPhase = null;
            _sceneStartedAt = 0;
            _nextChange = 0;
            _seasonIndex = _seasons.Length;
            _weatherIndex = _weather.Length;
            _beautifulIndex = 0;
        }

        private static void UpdateBeautiful(Weather weather, uint now)
        {
            if (_nextChange == 0 || now >= _nextChange)
            {
                _sceneStartedAt = now;
                _nextChange = now + BEAUTIFUL_SCENE_DURATION_MS;
                int sceneNumber = _beautifulIndex + 1;
                _beautifulScene = _beautifulScenes[_beautifulIndex++];
                if (_beautifulIndex >= _beautifulScenes.Length)
                {
                    _beautifulIndex = 0;
                }

                World.SetSeasonOverride(_beautifulScene.Season);
                ApplyWeather(weather, _beautifulScene.Weather);
                GameActions.Print(
                    $"Beautiful scene {sceneNumber}/{_beautifulScenes.Length}: {_beautifulScene.Name}.",
                    0x35
                );
            }

            double progress = (now - _sceneStartedAt) / (double)BEAUTIFUL_SCENE_DURATION_MS;
            progress = Math.Max(0.0, Math.Min(1.0, progress));
            double eased = progress * progress * (3.0 - 2.0 * progress);
            int light = (int)Math.Round(
                _beautifulScene.StartLight
                + (_beautifulScene.EndLight - _beautifulScene.StartLight) * eased
            );

            DayCyclePhase phase;
            if (light >= 12)
            {
                phase = DayCyclePhase.Night;
            }
            else if (_beautifulScene.StartLight <= 3 && _beautifulScene.EndLight <= 3)
            {
                phase = DayCyclePhase.Day;
            }
            else
            {
                phase = _beautifulScene.EndLight < _beautifulScene.StartLight
                    ? DayCyclePhase.Dawn
                    : DayCyclePhase.Dusk;
            }
            ApplyLight(light, phase);
        }

        private static void ApplyLight(int light, DayCyclePhase phase)
        {
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
            _currentPhase = phase;
        }

        private static T Next<T>(T[] values, ref int index)
        {
            if (index >= values.Length)
            {
                T previous = values[values.Length - 1];
                Shuffle(values);
                if (values.Length > 1 && object.Equals(values[0], previous))
                {
                    T value = values[0];
                    values[0] = values[1];
                    values[1] = value;
                }
                index = 0;
            }

            return values[index++];
        }

        private static void Shuffle<T>(T[] values)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int other = RandomHelper.GetValue(0, i);
                T value = values[i];
                values[i] = values[other];
                values[other] = value;
            }
        }

        private static void ApplyWeather(Weather weather, ShowcaseWeather kind)
        {
            AmbientWeatherManager.CancelForExternalWeather();
            switch (kind)
            {
                case ShowcaseWeather.Clear:
                    weather.Reset();
                    break;
                case ShowcaseWeather.Rain:
                    weather.Generate(WeatherType.WT_RAIN, 70, 0, WeatherSource.Manual);
                    break;
                case ShowcaseWeather.Snow:
                    weather.Generate(WeatherType.WT_SNOW, 70, 0, WeatherSource.Manual);
                    break;
                case ShowcaseWeather.HeavySnow:
                    weather.Generate(WeatherType.WT_SNOW, 200, 0, WeatherSource.Manual);
                    weather.HeavySnow = true;
                    break;
                case ShowcaseWeather.Blizzard:
                    weather.Generate(WeatherType.WT_SNOW, 200, 0, WeatherSource.Manual);
                    weather.Blizzard = true;
                    break;
                case ShowcaseWeather.Storm:
                    weather.Generate(WeatherType.WT_STORM_APPROACH, 100, 0, WeatherSource.Manual);
                    break;
                case ShowcaseWeather.Tempest:
                    weather.Generate(WeatherType.WT_STORM_APPROACH, 200, 0, WeatherSource.Manual);
                    weather.Tempest = true;
                    break;
                case ShowcaseWeather.Brewing:
                    weather.Generate(WeatherType.WT_STORM_BREWING, 70, 0, WeatherSource.Manual);
                    break;
                case ShowcaseWeather.Hail:
                    weather.Generate(WeatherType.WT_SNOW, 100, 0, WeatherSource.Manual);
                    weather.Hail = true;
                    break;
                case ShowcaseWeather.Sleet:
                    weather.Generate(WeatherType.WT_RAIN, 70, 0, WeatherSource.Manual);
                    weather.Sleet = true;
                    break;
                case ShowcaseWeather.Fog:
                    weather.SetFog(WeatherSource.Manual);
                    break;
            }
        }

        private static string GetWeatherName(ShowcaseWeather weather)
        {
            switch (weather)
            {
                case ShowcaseWeather.HeavySnow: return "heavy snow";
                default: return weather.ToString().ToLowerInvariant();
            }
        }

        internal static void RestoreEffectiveLight()
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile?.UseCustomLightLevel == true)
            {
                World.Light.Overall = profile.LightLevelType == 1
                    ? Math.Min(World.Light.RealOverall, profile.LightLevel)
                    : profile.LightLevel;
                World.Light.Personal = 0;
            }
            else
            {
                World.Light.Overall = World.Light.RealOverall;
                World.Light.Personal = World.Light.RealPersonal;
            }
        }
    }
}

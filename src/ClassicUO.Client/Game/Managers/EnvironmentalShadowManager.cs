#region license
// TazUO addition. Coordinates object shadows with the server day/night light
// level and active weather while keeping the renderer allocation-free.
#endregion

using System;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal static class EnvironmentalShadowManager
    {
        public const int MODE_CLASSIC = 0;
        public const int MODE_DYNAMIC = 1;

        private const float CLASSIC_OPACITY = 0.40f;
        private const uint INDOOR_CHECK_MS = 500;

        private static uint _lastTick;
        private static uint _nextIndoorCheck;
        private static bool _isIndoors;
        private static int _previewLight = -1;
        private static float _opacity = CLASSIC_OPACITY;
        private static float _projectionLength = 1f;
        private static float _projectionSkew = 1f;

        public static float Opacity => _opacity;
        public static float ProjectionLength => _projectionLength;
        public static float ProjectionSkew => _projectionSkew;
        public static float Daylight { get; private set; } = 1f;
        public static float Dusk { get; private set; }
        public static float Night { get; private set; }
        public static float Moonlight { get; private set; }
        public static float WeatherVisibility { get; private set; } = 1f;
        public static float AmbienceStrength { get; private set; } = 1f;
        public static bool IsIndoors => _isIndoors;
        private static Profile Profile => ProfileManager.CurrentProfile;
        private static bool Showcase =>
            EnvironmentShowcaseManager.Enabled || DayCyclePreviewManager.Enabled;
        private static bool CanDrawShadows =>
            Profile != null && (Profile.ShadowsEnabled || Showcase) && _opacity > 0.005f;

        public static void Reset()
        {
            _lastTick = 0;
            _nextIndoorCheck = 0;
            _isIndoors = false;
            _previewLight = -1;
            _opacity = CLASSIC_OPACITY;
            _projectionLength = 1f;
            _projectionSkew = 1f;
            Daylight = 1f;
            Dusk = Night = Moonlight = 0f;
            WeatherVisibility = 1f;
            AmbienceStrength = 1f;
        }

        public static void Update(Weather weather)
        {
            Profile profile = Profile;
            if (profile == null)
            {
                return;
            }

            UpdateIndoorState();
            int environmentalLight = _previewLight >= 0 ? _previewLight : World.Light.RealOverall;
            CalculateSolarState(environmentalLight, out float day, out float dusk, out float night, out float moon);

            float weatherVisibility = CalculateWeatherVisibility(
                weather?.Type,
                weather?.IsActive == true,
                weather?.Fog == true,
                weather?.HeavySnow == true,
                weather?.Blizzard == true,
                weather?.Tempest == true,
                weather?.Hail == true,
                weather?.Sleet == true
            );

            float targetOpacity;
            float targetLength;
            float targetSkew;

            if (!profile.ShadowsEnabled && !Showcase)
            {
                targetOpacity = 0f;
                targetLength = 1f;
                targetSkew = 1f;
            }
            else if (profile.ShadowMode == MODE_CLASSIC && !Showcase)
            {
                targetOpacity = CLASSIC_OPACITY;
                targetLength = 1f;
                targetSkew = 1f;
            }
            else
            {
                // The server's global light level is the clock. Bright day
                // uses a short fixed sun projection; dusk lengthens it. The
                // moon reuses the full-day projection exactly, changing only
                // intensity, so shadows never jump across their objects.
                float sunOpacity = day > 0.01f ? 0.12f + 0.34f * day : 0f;
                float moonOpacity = 0.13f * moon;
                bool useMoon = moonOpacity > sunOpacity;

                targetOpacity = useMoon ? moonOpacity : sunOpacity;
                targetLength = useMoon ? 0.80f : 1.55f - 0.75f * day;
                targetSkew = 1f;
                targetOpacity *= weatherVisibility;
                if (_isIndoors)
                {
                    targetOpacity *= 0.05f;
                }

                targetOpacity *= Showcase ? 1f : Clamp(profile.DynamicShadowIntensity, 0, 150) / 100f;
                targetLength *= Showcase ? 1f : Clamp(profile.DynamicShadowLength, 50, 180) / 100f;
            }

            // A nearby lightning flash briefly restores crisp directional
            // shadows. Direction remains identical to sun/moon projection so
            // objects never appear to jump across their own shadow.
            float lightning = weather?.LightningStrength ?? 0f;
            if (lightning > 0.01f && !_isIndoors && (profile.ShadowsEnabled || Showcase))
            {
                targetOpacity = Math.Max(targetOpacity, 0.42f * lightning);
                targetLength = 0.80f;
                targetSkew = 1f;
            }

            uint now = Time.Ticks;
            bool firstUpdate = _lastTick == 0;
            uint elapsed = firstUpdate ? 1000u : now - _lastTick;
            _lastTick = now;
            if (elapsed > 250)
            {
                elapsed = 250;
            }

            // Smooth environmental changes without retaining per-object state.
            float blend = firstUpdate || lightning > 0.01f
                ? 1f
                : 1f - (float)Math.Exp(-elapsed / 700f);
            _opacity += (targetOpacity - _opacity) * blend;
            _projectionLength += (targetLength - _projectionLength) * blend;
            _projectionSkew += (targetSkew - _projectionSkew) * blend;
            Daylight += (day - Daylight) * blend;
            Dusk += (dusk - Dusk) * blend;
            Night += (night - Night) * blend;
            Moonlight += (moon - Moonlight) * blend;
            WeatherVisibility += (weatherVisibility - WeatherVisibility) * blend;
            AmbienceStrength = Showcase ? 1f : Clamp(profile.DynamicAmbienceIntensity, 0, 150) / 100f;
        }

        public static bool ShouldCastMobile(Mobile mobile)
            => CanDrawShadows && mobile != null && !mobile.IsDead && !mobile.IsHidden;

        public static void SetPreviewLight(int light)
        {
            _previewLight = Clamp(light, 0, 30);
        }

        public static void ClearPreviewLight()
        {
            _previewLight = -1;
        }

        public static bool ShouldCastStatic(ushort graphic, in StaticTiles data)
        {
            if (!CanDrawShadows || (!Showcase && Profile?.ShadowsStatics != true))
            {
                return false;
            }

            bool classicCandidate = IsClassicCandidate(graphic, in data);
            return IsEligible(graphic, in data, classicCandidate,
                Showcase || Profile.ShadowMode == MODE_DYNAMIC && Profile.EnhancedObjectShadows);
        }

        public static bool ShouldCastItem(Item item)
        {
            if (!CanDrawShadows || (!Showcase && (Profile?.ShadowMode != MODE_DYNAMIC || Profile.EnhancedObjectShadows != true))
                || item == null || item.IsDestroyed || !item.OnGround || item.IsCorpse || item.IsCoin
                || item.IsMulti || item.IsHidden || item.AlphaHue < 64)
            {
                return false;
            }

            StaticTiles data = item.ItemData;
            ushort graphic = item.DisplayedGraphic;
            return IsEligible(graphic, in data, IsClassicCandidate(graphic, in data), true);
        }

        public static bool ShouldCastMulti(Multi multi)
        {
            if (!CanDrawShadows || (!Showcase && (Profile?.ShadowMode != MODE_DYNAMIC || Profile.EnhancedObjectShadows != true))
                || multi == null || multi.IsDestroyed || multi.IsHousePreview || multi.AlphaHue < 64)
            {
                return false;
            }

            StaticTiles data = multi.ItemData;
            return IsEligible(multi.Graphic, in data, IsClassicCandidate(multi.Graphic, in data), true);
        }

        internal static void CalculateSolarState(int overallLight, out float day, out float dusk, out float night, out float moon)
        {
            // ServUO's standard outdoor cycle is 0 (day) through 12
            // (night); 26 is its dungeon level. Calibrate the outdoor phases
            // to that real range while retaining 30 as an explicit moonless
            // preview/extreme-darkness state.
            float light = Clamp(overallLight, 0, 30);
            day = 1f - SmoothStep(2f, 10f, light);
            dusk = Clamp01(1f - Math.Abs(light - 7f) / 5f);
            night = SmoothStep(8f, 12f, light);
            // The darkest server level represents a moonless night. The band
            // immediately below it provides a subtle fixed-position moon.
            moon = night * (1f - SmoothStep(27f, 30f, light));
        }

        internal static float CalculateWeatherVisibility(
            WeatherType? type,
            bool active,
            bool fog,
            bool heavySnow,
            bool blizzard,
            bool tempest,
            bool hail,
            bool sleet)
        {
            if (fog)
            {
                return 0.12f;
            }
            if (!active || !type.HasValue)
            {
                return 1f;
            }

            switch (type.Value)
            {
                case WeatherType.WT_STORM_APPROACH: return tempest ? 0.08f : 0.24f;
                case WeatherType.WT_STORM_BREWING: return 0.42f;
                case WeatherType.WT_RAIN: return sleet ? 0.32f : 0.55f;
                case WeatherType.WT_SNOW:
                    if (blizzard) return 0.10f;
                    if (heavySnow) return 0.32f;
                    if (hail) return 0.40f;
                    return 0.65f;
                default: return 1f;
            }
        }

        private static bool IsClassicCandidate(ushort graphic, in StaticTiles data)
            => StaticFilters.IsTree(graphic, out _) || data.IsFoliage || StaticFilters.IsRock(graphic);

        private static bool IsEligible(ushort graphic, in StaticTiles data, bool classicCandidate, bool enhanced)
        {
            if ((data.Flags & TileFlag.NoShadow) != 0)
            {
                return false;
            }
            if (classicCandidate)
            {
                return true;
            }
            if (!enhanced)
            {
                return false;
            }
            if (data.IsInternal || data.IsWet || data.IsLight || data.IsBackground || data.IsSurface
                || data.IsWall || data.IsRoof || data.IsDoor || data.IsTransparent || data.IsTranslucent)
            {
                return false;
            }

            // Tall/solid artwork produces useful silhouettes. Small ground
            // clutter is skipped to limit overdraw and visual noise.
            return StaticFilters.IsVegetation(graphic)
                || data.IsContainer && data.Height >= 4
                || data.Height >= 6 && data.IsImpassable
                || data.Height >= 10;
        }

        private static void UpdateIndoorState()
        {
            if (World.Player == null || Time.Ticks < _nextIndoorCheck)
            {
                return;
            }

            _nextIndoorCheck = Time.Ticks + INDOOR_CHECK_MS;
            _isIndoors = World.MapIndex <= 1 && World.Player.X >= 5120;
            if (_isIndoors || World.Map == null)
            {
                return;
            }

            for (GameObject obj = World.Map.GetTile(World.Player.X, World.Player.Y, false); obj != null; obj = obj.TNext)
            {
                StaticTiles data;
                if (obj is Static staticObject)
                {
                    data = staticObject.ItemData;
                }
                else if (obj is Multi multi)
                {
                    data = multi.ItemData;
                }
                else
                {
                    continue;
                }

                if (obj.Z > World.Player.Z + 6 && (data.IsRoof || data.IsSurface))
                {
                    _isIndoors = true;
                    break;
                }
            }
        }

        private static float SmoothStep(float min, float max, float value)
        {
            float t = Clamp01((value - min) / (max - min));
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}

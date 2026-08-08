#region license
// TazUO addition. Exact wet-art stencil mask and restrained optical water pass.
#endregion

using System;
using System.IO;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal static class WaterEnhancementManager
    {
        internal const byte EDGE_NORTH = 1;
        internal const byte EDGE_EAST = 2;
        internal const byte EDGE_SOUTH = 4;
        internal const byte EDGE_WEST = 8;
        internal const byte MAX_VISUAL_DEPTH = 8;
        internal const int WATER_STYLE_NATURAL = 0;
        internal const int WATER_STYLE_WAVES = 1;
        internal const int WATER_STYLE_CHOPPY = 2;
        internal const int WATER_STYLE_SWELL = 3;
        internal const int WATER_STYLE_STORM = 4;
        internal const int WATER_STYLE_MOONLIT = 5;
        internal const int DEFAULT_INTENSITY_PERCENT = 200;

        private const int MIN_RADIUS = 8;
        private const int MAX_RADIUS = 96;
        private const int GRID_PADDING = MAX_VISUAL_DEPTH + 1;
        private const int GRID_SIZE = (MAX_RADIUS + GRID_PADDING) * 2 + 1;
        private const int MAX_TILES = (MAX_RADIUS * 2 + 1) * (MAX_RADIUS * 2 + 1);
        private const int MAX_MASK_SPRITES = 32768;
        private const int SCREEN_MARGIN = 120;
        private const int WATER_STENCIL = 1;
        private const int DEPTH_STENCIL_NEAR = 2;
        private const int DEPTH_STENCIL_MID = 4;
        private const int DEPTH_STENCIL_FAR = 8;

        private static readonly int[] _depthThresholds = { 2, 4, 6 };

        private struct WaterTile
        {
            public int X, Y;
            public sbyte Z;
            public byte Depth;
            public ushort Seed;
        }

        private struct MaskSprite
        {
            public Texture2D Texture;
            public Rectangle Source;
            public Vector2 Position;
            public float Depth;
            public bool Stretched;
            public UltimaBatcher2D.YOffsets YOffsets;
            public Vector3 NormalTop, NormalRight, NormalLeft, NormalBottom;
        }

        internal struct WaterEnvironment
        {
            public float ArtworkStrength;
            public float DepthStrength;
            public float MotionStrength;
            public float CloudStrength;
            public float ReflectionStrength;
            public float MistStrength;
            public float GlintStrength;
            public float WhitecapStrength;
            public int OverlayStyle;
            public float OverlayStrength;
        }

        private static readonly bool[,] _wet = new bool[GRID_SIZE, GRID_SIZE];
        private static readonly sbyte[,] _waterZ = new sbyte[GRID_SIZE, GRID_SIZE];
        private static readonly WaterTile[] _tiles = new WaterTile[MAX_TILES];
        private static readonly MaskSprite[] _maskSprites = new MaskSprite[MAX_MASK_SPRITES];

        private static readonly Lazy<BlendState> _maskWriteBlend = new Lazy<BlendState>(() =>
            new BlendState
            {
                ColorWriteChannels = ColorWriteChannels.None,
                ColorWriteChannels1 = ColorWriteChannels.None,
                ColorWriteChannels2 = ColorWriteChannels.None,
                ColorWriteChannels3 = ColorWriteChannels.None
            });

        private static readonly Lazy<DepthStencilState> _maskWriteStencil =
            new Lazy<DepthStencilState>(() => new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = false,
                DepthBufferFunction = CompareFunction.LessEqual,
                StencilEnable = true,
                StencilFunction = CompareFunction.Always,
                StencilPass = StencilOperation.Replace,
                ReferenceStencil = WATER_STENCIL,
                StencilMask = 0xFF,
                StencilWriteMask = 0xFF
            });

        private static readonly Lazy<DepthStencilState> _maskReadStencil =
            new Lazy<DepthStencilState>(() => new DepthStencilState
            {
                DepthBufferEnable = false,
                DepthBufferWriteEnable = false,
                StencilEnable = true,
                StencilFunction = CompareFunction.Equal,
                StencilPass = StencilOperation.Keep,
                ReferenceStencil = WATER_STENCIL,
                StencilMask = WATER_STENCIL,
                StencilWriteMask = 0
            });

        private static readonly Lazy<DepthStencilState>[] _depthWriteStencils =
        {
            new Lazy<DepthStencilState>(() => CreateDepthWriteStencil(DEPTH_STENCIL_NEAR)),
            new Lazy<DepthStencilState>(() => CreateDepthWriteStencil(DEPTH_STENCIL_MID)),
            new Lazy<DepthStencilState>(() => CreateDepthWriteStencil(DEPTH_STENCIL_FAR))
        };

        private static readonly Lazy<DepthStencilState>[] _depthReadStencils =
        {
            new Lazy<DepthStencilState>(() => CreateDepthReadStencil(DEPTH_STENCIL_NEAR)),
            new Lazy<DepthStencilState>(() => CreateDepthReadStencil(DEPTH_STENCIL_MID)),
            new Lazy<DepthStencilState>(() => CreateDepthReadStencil(DEPTH_STENCIL_FAR))
        };

        private static int _tileCount;
        private static int _originX, _originY, _radius;
        private static int _lastPlayerX = int.MinValue, _lastPlayerY;
        private static int _lastViewportWidth, _lastViewportHeight;
        private static float _lastCameraOffsetX, _lastCameraOffsetY;
        private static float _lastZoom;
        private static uint _nextScan;

        private static int _maskSpriteCount;
        private static bool _captureActive;
        private static Rectangle _captureBounds;

        private static readonly Texture2D[] _waterArtwork = new Texture2D[6];
        private static WaterEffect _waterEffect;
        private static bool _waterEffectLoadAttempted;
        private static Texture2D _depthMaskDiamond;
        private static Texture2D _frostDiamond;
        private static Texture2D _ripple;
        private static Texture2D _cloudReflection;
        private static Texture2D _duskCloudReflection;
        private static Texture2D _moonCloudReflection;
        private static Texture2D _cloudShadow;
        private static Texture2D _waterMist;
        private static Texture2D _waterGlint;
        private static bool _artworkEnabled = true;
        private static bool _atmosphereEnabled = true;
        private static int _artworkStyle = WATER_STYLE_STORM;
        private static int _intensityPercent = DEFAULT_INTENSITY_PERCENT;

        private static Profile Profile => ProfileManager.CurrentProfile;
        private static bool Enabled => AmbienceOverlay.Enabled && Profile != null;
        internal static bool ArtworkEnabled => _artworkEnabled;
        internal static bool AtmosphereEnabled => _atmosphereEnabled;
        internal static int IntensityPercent => _intensityPercent;
        internal static string ArtworkStyleName
        {
            get
            {
                switch (_artworkStyle)
                {
                    case WATER_STYLE_WAVES: return "Waves";
                    case WATER_STYLE_CHOPPY: return "Choppy";
                    case WATER_STYLE_SWELL: return "Gentle Swell";
                    case WATER_STYLE_STORM: return "Storm Swell";
                    case WATER_STYLE_MOONLIT: return "Moonlit Swell";
                    default: return "Natural";
                }
            }
        }

        public static void SetArtworkEnabled(bool enabled)
        {
            _artworkEnabled = enabled;
            if (Profile != null) Profile.EnhancedWaterEnabled = enabled;
        }

        public static void SetAtmosphereEnabled(bool enabled)
        {
            _atmosphereEnabled = enabled;
            if (Profile != null) Profile.WaterAtmosphereEnabled = enabled;
        }

        public static bool SetIntensity(int percent)
        {
            if (percent < 0 || percent > 200) return false;
            _intensityPercent = percent;
            if (Profile != null) Profile.WaterMaterialIntensity = percent;
            return true;
        }

        public static void ApplyProfile(Profile profile)
        {
            if (profile == null) return;
            _artworkEnabled = profile.EnhancedWaterEnabled;
            _atmosphereEnabled = profile.WaterAtmosphereEnabled;
            _artworkStyle = Math.Max(WATER_STYLE_NATURAL,
                Math.Min(WATER_STYLE_MOONLIT, profile.EnhancedWaterStyle));
            _intensityPercent = Math.Max(0, Math.Min(200, profile.WaterMaterialIntensity));
            profile.EnhancedWaterStyle = _artworkStyle;
            profile.WaterMaterialIntensity = _intensityPercent;
        }

        internal static int ParseArtworkStyle(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return -1;
            switch (value.Trim().ToLowerInvariant())
            {
                case "natural": return WATER_STYLE_NATURAL;
                case "waves": return WATER_STYLE_WAVES;
                case "choppy": return WATER_STYLE_CHOPPY;
                case "swell":
                case "gentle":
                case "gentle swell": return WATER_STYLE_SWELL;
                case "storm":
                case "storm swell": return WATER_STYLE_STORM;
                case "moonlit":
                case "moon":
                case "moonlit swell": return WATER_STYLE_MOONLIT;
                default: return -1;
            }
        }

        public static bool SetArtworkStyle(string value)
        {
            int style = ParseArtworkStyle(value);
            if (style < 0) return false;
            _artworkStyle = style;
            _artworkEnabled = true;
            if (Profile != null)
            {
                Profile.EnhancedWaterStyle = style;
                Profile.EnhancedWaterEnabled = true;
            }
            return true;
        }

        public static void CycleArtworkStyle()
        {
            _artworkStyle = (_artworkStyle + 1) % _waterArtwork.Length;
            _artworkEnabled = true;
            if (Profile != null)
            {
                Profile.EnhancedWaterStyle = _artworkStyle;
                Profile.EnhancedWaterEnabled = true;
            }
        }

        public static void ResetSession()
        {
            if (_maskSpriteCount > 0)
                Array.Clear(_maskSprites, 0, _maskSpriteCount);
            _maskSpriteCount = 0;
            _captureActive = false;
            _captureBounds = Rectangle.Empty;
            _tileCount = 0;
            _lastPlayerX = int.MinValue;
            _lastPlayerY = 0;
            _lastViewportWidth = _lastViewportHeight = 0;
            _lastCameraOffsetX = _lastCameraOffsetY = 0;
            _lastZoom = 0f;
            _nextScan = 0;
            _artworkEnabled = true;
            _atmosphereEnabled = true;
            _artworkStyle = WATER_STYLE_STORM;
            _intensityPercent = DEFAULT_INTENSITY_PERCENT;
        }

        public static bool BeginFrameCapture(bool stencilAvailable, Weather weather)
        {
            _maskSpriteCount = 0;
            _captureBounds = Rectangle.Empty;
            _captureActive = stencilAvailable && World.InGame && World.Player != null
                && (Enabled && (_artworkEnabled && _intensityPercent > 0
                    && SceneryInteractionManager.Quality > SceneryInteractionManager.QUALITY_LOW
                    || WantsSurfaceEvents(weather))
                    || _atmosphereEnabled
                    && Profile != null
                    && SceneryInteractionManager.Quality > SceneryInteractionManager.QUALITY_LOW);
            return _captureActive;
        }

        private static bool WantsSurfaceEvents(Weather weather)
        {
            if (weather == null) return false;
            bool storm = weather.IsActive && (weather.Type == WeatherType.WT_STORM_APPROACH
                || weather.Type == WeatherType.WT_STORM_BREWING || weather.Tempest);
            bool rain = weather.IsActive && (weather.Type == WeatherType.WT_RAIN || storm);
            bool frost = World.Season == Season.Winter && weather.Coldness > 0.35f;
            return frost || rain && SceneryInteractionManager.Quality > SceneryInteractionManager.QUALITY_LOW;
        }

        public static void CaptureSprite(
            Texture2D texture,
            Vector2 position,
            Rectangle source,
            float depth)
        {
            if (!_captureActive || texture == null || texture.IsDisposed
                || _maskSpriteCount >= MAX_MASK_SPRITES)
                return;

            ref MaskSprite sprite = ref _maskSprites[_maskSpriteCount++];
            sprite.Texture = texture;
            sprite.Source = source;
            sprite.Position = position;
            sprite.Depth = depth;
            sprite.Stretched = false;
            IncludeCaptureBounds((int)position.X, (int)position.Y, source.Width, source.Height);
        }

        public static void CaptureStretchedLand(
            Texture2D texture,
            Vector2 position,
            Rectangle source,
            ref UltimaBatcher2D.YOffsets yOffsets,
            ref Vector3 normalTop,
            ref Vector3 normalRight,
            ref Vector3 normalLeft,
            ref Vector3 normalBottom,
            float depth)
        {
            if (!_captureActive || texture == null || texture.IsDisposed
                || _maskSpriteCount >= MAX_MASK_SPRITES)
                return;

            ref MaskSprite sprite = ref _maskSprites[_maskSpriteCount++];
            sprite.Texture = texture;
            sprite.Source = source;
            sprite.Position = position;
            sprite.Depth = depth;
            sprite.Stretched = true;
            sprite.YOffsets = yOffsets;
            sprite.NormalTop = normalTop;
            sprite.NormalRight = normalRight;
            sprite.NormalLeft = normalLeft;
            sprite.NormalBottom = normalBottom;
            IncludeCaptureBounds((int)position.X, (int)position.Y - 32, 44, 108);
        }

        private static void IncludeCaptureBounds(int x, int y, int width, int height)
        {
            Rectangle value = new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
            _captureBounds = _captureBounds.IsEmpty ? value : Rectangle.Union(_captureBounds, value);
        }

        public static void Update()
        {
            if (!Enabled || !World.InGame || World.Player == null || World.Map == null)
            {
                _tileCount = 0;
                return;
            }

            GameScene scene = Client.Game.GetScene<GameScene>();
            if (scene == null) return;
            int viewportWidth = scene.Camera.Bounds.Width;
            int viewportHeight = scene.Camera.Bounds.Height;
            float zoom = scene.Camera.Zoom;
            float cameraOffsetX = scene.Camera.Offset.X;
            float cameraOffsetY = scene.Camera.Offset.Y;
            int scaledWidth = viewportWidth;
            int scaledHeight = viewportHeight;
            if (Profile.GlobalScaling && Profile.GameWindowFullSize && Profile.GlobalScale > 0.01f)
            {
                scaledWidth = (int)(scaledWidth / Profile.GlobalScale);
                scaledHeight = (int)(scaledHeight / Profile.GlobalScale);
            }
            int requestedRadius = CalculateCoverageRadius(
                scaledWidth, scaledHeight, zoom, cameraOffsetX, cameraOffsetY);

            uint now = Time.Ticks;
            bool moved = World.Player.X != _lastPlayerX || World.Player.Y != _lastPlayerY;
            bool viewportChanged = viewportWidth != _lastViewportWidth
                || viewportHeight != _lastViewportHeight || Math.Abs(zoom - _lastZoom) > 0.001f
                || Math.Abs(cameraOffsetX - _lastCameraOffsetX) > 44f
                || Math.Abs(cameraOffsetY - _lastCameraOffsetY) > 44f
                || requestedRadius != _radius;
            if (!moved && !viewportChanged && now < _nextScan) return;

            _lastPlayerX = World.Player.X;
            _lastPlayerY = World.Player.Y;
            _lastViewportWidth = viewportWidth;
            _lastViewportHeight = viewportHeight;
            _lastCameraOffsetX = cameraOffsetX;
            _lastCameraOffsetY = cameraOffsetY;
            _lastZoom = zoom;
            _nextScan = now + (uint)(moved
                ? SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_LOW ? 1100
                    : SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_MEDIUM ? 700 : 450
                : 2200);
            Scan(requestedRadius, scene.Camera.Bounds);
        }

        private static void Scan(int radius, Rectangle bounds)
        {
            _tileCount = 0;
            _radius = radius;
            int scanRadius = _radius + GRID_PADDING;
            _originX = World.Player.X - scanRadius;
            _originY = World.Player.Y - scanRadius;
            int size = scanRadius * 2 + 1;

            for (int gy = 0; gy < size; gy++)
            {
                for (int gx = 0; gx < size; gx++)
                {
                    ReadCell(_originX + gx, _originY + gy,
                        out _wet[gx, gy], out _waterZ[gx, gy]);
                }
            }

            for (int gy = GRID_PADDING; gy < size - GRID_PADDING && _tileCount < MAX_TILES; gy++)
            {
                for (int gx = GRID_PADDING; gx < size - GRID_PADDING && _tileCount < MAX_TILES; gx++)
                {
                    if (!_wet[gx, gy]) continue;

                    sbyte z = _waterZ[gx, gy];
                    int x = _originX + gx;
                    int y = _originY + gy;
                    Point p = PathPreview.TileToScreen(x, y, z);
                    if (p.X < bounds.X - SCREEN_MARGIN || p.X > bounds.Right + SCREEN_MARGIN
                        || p.Y < bounds.Y - SCREEN_MARGIN || p.Y > bounds.Bottom + SCREEN_MARGIN)
                        continue;

                    ref WaterTile tile = ref _tiles[_tileCount++];
                    tile.X = x;
                    tile.Y = y;
                    tile.Z = z;
                    tile.Depth = ComputeDepthAt(gx, gy, size);
                    tile.Seed = (ushort)Hash(x, y);
                }
            }
        }

        internal static int CalculateCoverageRadius(
            int viewportWidth, int viewportHeight, float zoom, float cameraOffsetX, float cameraOffsetY)
        {
            float safeZoom = Math.Max(0.25f, zoom);
            float viewportTiles = Math.Max(viewportWidth / 44f + 1f, viewportHeight / 44f + 1f)
                * safeZoom;
            float cameraTiles = (Math.Abs(cameraOffsetX) + Math.Abs(cameraOffsetY)) * safeZoom / 44f;
            return Math.Max(MIN_RADIUS,
                Math.Min(MAX_RADIUS, (int)Math.Ceiling(viewportTiles + cameraTiles) + 2));
        }

        private static void ReadCell(int x, int y, out bool wet, out sbyte waterZ)
        {
            wet = false;
            waterZ = sbyte.MinValue;
            int coverTop = short.MinValue;
            try
            {
                for (GameObject obj = World.Map.GetTile(x, y, false); obj != null; obj = obj.TNext)
                {
                    bool isWet;
                    bool coversWater;
                    int height = 0;
                    if (obj is Land land)
                    {
                        isWet = land.TileData.IsWet;
                        coversWater = !isWet;
                    }
                    else if (obj is Static staticObject)
                    {
                        isWet = staticObject.ItemData.IsWet;
                        height = staticObject.ItemData.Height;
                        coversWater = !isWet && (staticObject.ItemData.IsSurface
                            || staticObject.ItemData.IsBridge || staticObject.ItemData.IsRoof);
                    }
                    else if (obj is Item item)
                    {
                        isWet = item.ItemData.IsWet;
                        height = item.ItemData.Height;
                        coversWater = !isWet && (item.ItemData.IsSurface
                            || item.ItemData.IsBridge || item.ItemData.IsRoof);
                    }
                    else if (obj is Multi multi)
                    {
                        isWet = multi.ItemData.IsWet;
                        height = multi.ItemData.Height;
                        coversWater = !isWet && (multi.ItemData.IsSurface
                            || multi.ItemData.IsBridge || multi.ItemData.IsRoof);
                    }
                    else continue;

                    int top = obj.Z + height;
                    if (coversWater && top > coverTop) coverTop = top;
                    if (isWet && obj.Z >= waterZ)
                    {
                        wet = true;
                        waterZ = obj.Z;
                    }
                }
                if (wet && coverTop > waterZ + 2) wet = false;
            }
            catch
            {
                wet = false;
            }
        }

        private static bool IsWetRing(int gx, int gy, int radius, int size)
        {
            if (gx - radius < 0 || gy - radius < 0 || gx + radius >= size || gy + radius >= size)
                return false;
            for (int offset = -radius; offset <= radius; offset++)
            {
                if (!_wet[gx + offset, gy - radius] || !_wet[gx + offset, gy + radius]
                    || !_wet[gx - radius, gy + offset] || !_wet[gx + radius, gy + offset]) return false;
            }
            return true;
        }

        internal static byte ComputeEdgeMask(bool northWet, bool eastWet, bool southWet, bool westWet)
        {
            byte result = 0;
            if (!northWet) result |= EDGE_NORTH;
            if (!eastWet) result |= EDGE_EAST;
            if (!southWet) result |= EDGE_SOUTH;
            if (!westWet) result |= EDGE_WEST;
            return result;
        }

        private static byte ComputeDepthAt(int gx, int gy, int size)
        {
            for (int radius = 1; radius <= MAX_VISUAL_DEPTH; radius++)
            {
                if (!IsWetRing(gx, gy, radius, size))
                    return ComputeDepthFromFirstDryRadius(radius);
            }
            return MAX_VISUAL_DEPTH;
        }

        internal static byte ComputeDepthFromFirstDryRadius(int radius)
            => (byte)Math.Max(0, Math.Min(MAX_VISUAL_DEPTH, radius - 1));

        internal static float CalculateVisibility(float ambience)
            => 0.72f + Math.Max(0f, Math.Min(1f, ambience)) * 0.28f;

        internal static float CalculateShoreArtworkOpacity(int quality)
            => quality <= SceneryInteractionManager.QUALITY_LOW ? 0f
                : quality == SceneryInteractionManager.QUALITY_MEDIUM ? 0.18f : 0.24f;

        internal static float CalculateDepthLayerOpacity(int quality)
            => quality <= SceneryInteractionManager.QUALITY_LOW ? 0f
                : quality == SceneryInteractionManager.QUALITY_MEDIUM ? 0.034f : 0.041f;

        internal static float ApplyIntensity(float opacity, int percent)
            => Math.Min(0.95f, Math.Max(0f, opacity) * Math.Max(0, Math.Min(200, percent)) / 100f);

        internal static float CalculateMotionPixels(int quality)
            => quality <= SceneryInteractionManager.QUALITY_LOW ? 0f
                : quality == SceneryInteractionManager.QUALITY_MEDIUM ? 0.18f : 0.28f;

        internal static float CalculateMotionScale(float wind, float weatherIntensity, bool storm)
        {
            float windStrength = Math.Min(1f, Math.Abs(wind) / 4f);
            float weatherStrength = Math.Max(0f, Math.Min(1f, weatherIntensity));
            return Math.Min(1f, 0.25f + windStrength * 0.35f
                + weatherStrength * 0.25f + (storm ? 0.25f : 0f));
        }

        private static WaterEnvironment CalculateEnvironment(Weather weather)
            => CalculateEnvironment(
                weather?.Type,
                weather?.IsActive == true,
                weather?.Fog == true,
                weather?.Tempest == true,
                weather?.VisualIntensity ?? 0f,
                weather?.Wetness ?? 0f,
                weather?.Coldness ?? 0f,
                EnvironmentalShadowManager.Daylight,
                EnvironmentalShadowManager.Dusk,
                EnvironmentalShadowManager.Night,
                EnvironmentalShadowManager.Moonlight,
                SceneryInteractionManager.SharedWind);

        internal static WaterEnvironment CalculateEnvironment(
            WeatherType? weatherType,
            bool active,
            bool fog,
            bool tempest,
            float weatherIntensity,
            float wetness,
            float coldness,
            float daylight,
            float dusk,
            float night,
            float moonlight,
            float wind)
        {
            weatherIntensity = Clamp01(weatherIntensity);
            wetness = Clamp01(wetness);
            coldness = Clamp01(coldness);
            daylight = Clamp01(daylight);
            dusk = Clamp01(dusk);
            night = Clamp01(night);
            moonlight = Clamp01(moonlight);

            bool storm = active && (weatherType == WeatherType.WT_STORM_APPROACH
                || weatherType == WeatherType.WT_STORM_BREWING || tempest);
            bool rain = active && (weatherType == WeatherType.WT_RAIN || storm);
            bool snow = active && weatherType == WeatherType.WT_SNOW;
            float stormWeight = storm ? weatherIntensity : 0f;
            float rainWeight = rain ? weatherIntensity : wetness * 0.18f;
            float snowWeight = snow ? weatherIntensity : coldness * 0.12f;
            float fogWeight = fog ? weatherIntensity : 0f;
            float windStrength = Math.Min(1f, Math.Abs(wind) / 4f);
            float celestial = daylight * 0.78f + dusk * 0.68f + moonlight * 0.88f;

            WaterEnvironment result = new WaterEnvironment
            {
                ArtworkStrength = 0.94f + daylight * 0.08f + dusk * 0.05f
                    + night * 0.04f + stormWeight * 0.16f - fogWeight * 0.06f,
                DepthStrength = 0.94f + night * 0.13f + stormWeight * 0.14f
                    + rainWeight * 0.05f - fogWeight * 0.07f,
                MotionStrength = 0.52f + windStrength * 0.40f
                    + rainWeight * 0.18f + stormWeight * 0.34f,
                CloudStrength = 0.16f + rainWeight * 0.34f
                    + stormWeight * 0.42f + fogWeight * 0.25f,
                ReflectionStrength = celestial
                    * (1f - stormWeight * 0.58f - fogWeight * 0.38f),
                MistStrength = fogWeight * 0.90f + stormWeight * 0.26f
                    + rainWeight * 0.10f + snowWeight * 0.12f,
                GlintStrength = (daylight * 0.74f + dusk * 0.92f + moonlight * 1.08f)
                    * (1f - rainWeight * 0.34f - stormWeight * 0.72f - fogWeight * 0.48f),
                WhitecapStrength = windStrength * 0.45f
                    + rainWeight * 0.20f + stormWeight * 0.68f,
                OverlayStyle = WATER_STYLE_SWELL,
                OverlayStrength = 0.04f * Math.Max(daylight, dusk)
            };

            if (stormWeight > 0.05f)
            {
                result.OverlayStyle = WATER_STYLE_STORM;
                result.OverlayStrength = 0.10f + stormWeight * 0.18f;
            }
            else if (rainWeight > 0.20f || windStrength > 0.65f)
            {
                result.OverlayStyle = WATER_STYLE_CHOPPY;
                result.OverlayStrength = 0.06f + Math.Max(rainWeight, windStrength) * 0.10f;
            }
            else if (moonlight > daylight && night > 0.25f)
            {
                result.OverlayStyle = WATER_STYLE_MOONLIT;
                result.OverlayStrength = 0.06f + moonlight * 0.12f;
            }

            result.ArtworkStrength = Clamp(result.ArtworkStrength, 0.78f, 1.22f);
            result.DepthStrength = Clamp(result.DepthStrength, 0.78f, 1.24f);
            result.MotionStrength = Clamp(result.MotionStrength, 0.25f, 1.35f);
            result.CloudStrength = Clamp01(result.CloudStrength);
            result.ReflectionStrength = Clamp(result.ReflectionStrength, 0f, 1.15f);
            result.MistStrength = Clamp01(result.MistStrength);
            result.GlintStrength = Clamp(result.GlintStrength, 0f, 1.20f);
            result.WhitecapStrength = Clamp01(result.WhitecapStrength);
            result.OverlayStrength = Clamp(result.OverlayStrength, 0f, 0.30f);
            return result;
        }

        public static void DrawMaskedWorld(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            Matrix transform)
        {
            if (!_captureActive || _maskSpriteCount == 0) return;

            Vector3 maskHue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            try
            {
                batcher.SetBlendState(_maskWriteBlend.Value);
                batcher.SetStencil(_maskWriteStencil.Value);
                for (int i = 0; i < _maskSpriteCount; i++)
                {
                    ref MaskSprite sprite = ref _maskSprites[i];
                    if (sprite.Texture == null || sprite.Texture.IsDisposed) continue;
                    if (sprite.Stretched)
                    {
                        batcher.DrawStretchedLand(
                            sprite.Texture,
                            sprite.Position,
                            sprite.Source,
                            ref sprite.YOffsets,
                            ref sprite.NormalTop,
                            ref sprite.NormalRight,
                            ref sprite.NormalLeft,
                            ref sprite.NormalBottom,
                            maskHue,
                            sprite.Depth
                        );
                    }
                    else
                    {
                        batcher.Draw(
                            sprite.Texture,
                            sprite.Position,
                            sprite.Source,
                            maskHue,
                            0f,
                            Vector2.Zero,
                            Vector2.One,
                            SpriteEffects.None,
                            sprite.Depth
                        );
                    }
                }

                batcher.SetBlendState(null);
                batcher.SetStencil(_maskReadStencil.Value);
                if (Enabled) DrawReplacementArtwork(batcher, bounds, weather, transform);
                batcher.SetStencil(_maskReadStencil.Value);
                if (_atmosphereEnabled && Profile != null) DrawAtmospherePass(batcher, bounds, weather);
                batcher.SetStencil(_maskReadStencil.Value);
                if (Enabled) DrawWeatherPass(batcher, bounds, weather);
            }
            finally
            {
                batcher.SetBlendState(null);
                batcher.SetStencil(null);
                batcher.SetCustomEffect(null);
                batcher.SetSampler(null);
            }
        }

        private static void DrawReplacementArtwork(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            Matrix transform)
        {
            if (!_artworkEnabled || _intensityPercent <= 0) return;

            int quality = SceneryInteractionManager.Quality;
            if (quality <= SceneryInteractionManager.QUALITY_LOW) return;

            Texture2D artwork = GetWaterArtwork();
            if (artwork == null || artwork.IsDisposed) return;

            Rectangle visibleBounds = new Rectangle(
                bounds.X - artwork.Width,
                bounds.Y - artwork.Height,
                bounds.Width + artwork.Width * 2,
                bounds.Height + artwork.Height * 2
            );
            Rectangle area = Rectangle.Intersect(_captureBounds, visibleBounds);
            if (area.IsEmpty) return;

            WaterEnvironment environment = CalculateEnvironment(weather);
            WaterEffect effect = GetWaterEffect(batcher.GraphicsDevice);
            bool animate = Profile.AnimatedWaterEffect && effect != null;
            if (animate)
            {
                bool storm = weather != null && weather.IsActive
                    && (weather.Type == WeatherType.WT_STORM_APPROACH
                        || weather.Type == WeatherType.WT_STORM_BREWING || weather.Tempest);
                float motionScale = CalculateMotionScale(
                    SceneryInteractionManager.SharedWind,
                    weather?.VisualIntensity ?? 0f,
                    storm
                ) * environment.MotionStrength;
                effect.Configure(
                    transform,
                    Time.Ticks / 1000f,
                    CalculateMotionPixels(quality) * Math.Min(1.35f, motionScale)
                        / artwork.Width
                );
            }

            Point playerScreen = PathPreview.TileToScreen(
                World.Player.X, World.Player.Y, World.Player.Z);
            int worldX = (World.Player.X - World.Player.Y) * 22;
            int worldY = (World.Player.X + World.Player.Y) * 22 - World.Player.Z * 4;
            int anchorX = playerScreen.X - PositiveModulo(worldX, artwork.Width);
            int anchorY = playerScreen.Y - PositiveModulo(worldY, artwork.Height);
            DrawArtworkComposite(
                batcher,
                artwork,
                area,
                anchorX,
                anchorY,
                ApplyIntensity(CalculateShoreArtworkOpacity(quality), _intensityPercent)
                    * environment.ArtworkStrength,
                effect,
                animate,
                playerScreen,
                worldX,
                worldY
            );
            if (quality >= SceneryInteractionManager.QUALITY_HIGH)
                DrawEnvironmentArtwork(
                    batcher, area, environment, playerScreen, worldX, worldY);

            float depthAlpha = ApplyIntensity(
                CalculateDepthLayerOpacity(quality), _intensityPercent)
                * environment.DepthStrength;
            for (int i = 0; i < _depthThresholds.Length; i++)
            {
                WriteDepthStencil(batcher, bounds, _depthThresholds[i], _depthWriteStencils[i].Value);
                batcher.SetStencil(_depthReadStencils[i].Value);
                DrawArtworkComposite(
                    batcher, artwork, area, anchorX, anchorY, depthAlpha, effect, animate,
                    playerScreen, worldX, worldY);
            }
        }

        private static void DrawEnvironmentArtwork(
            UltimaBatcher2D batcher,
            Rectangle area,
            WaterEnvironment environment,
            Point playerScreen,
            int worldX,
            int worldY)
        {
            if (environment.OverlayStrength <= 0.005f
                || environment.OverlayStyle == _artworkStyle) return;
            Texture2D artwork = GetWaterArtwork(environment.OverlayStyle);
            if (artwork == null || artwork.IsDisposed) return;

            int anchorX = playerScreen.X
                - PositiveModulo(worldX + 149, artwork.Width);
            int anchorY = playerScreen.Y
                - PositiveModulo(worldY + 263, artwork.Height);
            float alpha = ApplyIntensity(
                CalculateShoreArtworkOpacity(SceneryInteractionManager.QUALITY_HIGH)
                    * environment.OverlayStrength,
                _intensityPercent);
            DrawArtworkLayer(
                batcher,
                artwork,
                area,
                anchorX,
                anchorY,
                alpha,
                null,
                false,
                artwork.Width,
                artwork.Height,
                true);
        }

        private static void DrawArtworkComposite(
            UltimaBatcher2D batcher,
            Texture2D artwork,
            Rectangle area,
            int anchorX,
            int anchorY,
            float alpha,
            WaterEffect effect,
            bool animate,
            Point playerScreen,
            int worldX,
            int worldY)
        {
            if (animate)
            {
                DrawArtworkLayer(batcher, artwork, area, anchorX, anchorY, alpha, effect,
                    true, artwork.Width, artwork.Height, true);
                return;
            }

            // Two non-aligning, world-anchored scales hide repetition without adding
            // line glints or screen-space tint. Their combined opacity stays near
            // the original single-layer strength.
            DrawArtworkLayer(batcher, artwork, area, anchorX, anchorY, alpha * 0.82f, null,
                false, artwork.Width, artwork.Height, false);
            int secondaryWidth = Math.Max(1, (int)(artwork.Width * 1.618f));
            int secondaryHeight = Math.Max(1, (int)(artwork.Height * 1.618f));
            int secondaryX = playerScreen.X
                - PositiveModulo(worldX + 317, secondaryWidth);
            int secondaryY = playerScreen.Y
                - PositiveModulo(worldY + 191, secondaryHeight);
            DrawArtworkLayer(batcher, artwork, area, secondaryX, secondaryY, alpha * 0.20f,
                null, false, secondaryWidth, secondaryHeight, true);
        }

        private static void DrawArtworkLayer(
            UltimaBatcher2D batcher,
            Texture2D artwork,
            Rectangle area,
            int anchorX,
            int anchorY,
            float alpha,
            WaterEffect effect,
            bool animate,
            int tileWidth,
            int tileHeight,
            bool linear)
        {
            if (alpha <= 0f) return;
            batcher.SetCustomEffect(animate ? effect : null);
            batcher.SetSampler(animate || linear ? SamplerState.LinearWrap : SamplerState.PointWrap);

            int startX = area.Left - PositiveModulo(area.Left - anchorX, tileWidth);
            int startY = area.Top - PositiveModulo(area.Top - anchorY, tileHeight);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            for (int y = startY; y < area.Bottom; y += tileHeight)
            {
                for (int x = startX; x < area.Right; x += tileWidth)
                {
                    int cellX = (x - anchorX) / tileWidth;
                    int cellY = (y - anchorY) / tileHeight;
                    uint variant = Hash(cellX, cellY);
                    SpriteEffects effects = SpriteEffects.None;
                    if ((variant & 1) != 0) effects |= SpriteEffects.FlipHorizontally;
                    if ((variant & 2) != 0) effects |= SpriteEffects.FlipVertically;
                    batcher.Draw(
                        artwork,
                        new Rectangle(x, y, tileWidth, tileHeight),
                        null,
                        hue,
                        0f,
                        Vector2.Zero,
                        effects,
                        0f
                    );
                }
            }

            batcher.SetCustomEffect(null);
            batcher.SetSampler(null);
        }

        private static void WriteDepthStencil(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            int minimumDepth,
            DepthStencilState stencil)
        {
            Texture2D diamond = GetDiamond(ref _depthMaskDiamond, Color.White);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            batcher.SetCustomEffect(null);
            batcher.SetSampler(null);
            batcher.SetBlendState(_maskWriteBlend.Value);
            batcher.SetStencil(stencil);

            for (int i = 0; i < _tileCount; i++)
            {
                ref WaterTile tile = ref _tiles[i];
                if (tile.Depth < minimumDepth) continue;
                Point p = PathPreview.TileToScreen(tile.X, tile.Y, tile.Z);
                if (p.X < bounds.X - 22 || p.X > bounds.Right + 22
                    || p.Y < bounds.Y - 11 || p.Y > bounds.Bottom + 11) continue;
                batcher.Draw(
                    diamond,
                    new Rectangle(p.X - 22, p.Y - 11, 44, 22),
                    hue
                );
            }

            batcher.SetBlendState(null);
        }

        private static DepthStencilState CreateDepthWriteStencil(int depthBit)
            => new DepthStencilState
            {
                DepthBufferEnable = false,
                DepthBufferWriteEnable = false,
                StencilEnable = true,
                StencilFunction = CompareFunction.Equal,
                StencilPass = StencilOperation.Replace,
                ReferenceStencil = WATER_STENCIL | depthBit,
                StencilMask = WATER_STENCIL,
                StencilWriteMask = depthBit
            };

        private static DepthStencilState CreateDepthReadStencil(int depthBit)
            => new DepthStencilState
            {
                DepthBufferEnable = false,
                DepthBufferWriteEnable = false,
                StencilEnable = true,
                StencilFunction = CompareFunction.Equal,
                StencilPass = StencilOperation.Keep,
                ReferenceStencil = WATER_STENCIL | depthBit,
                StencilMask = WATER_STENCIL | depthBit,
                StencilWriteMask = 0
            };

        private static Texture2D GetWaterArtwork()
            => GetWaterArtwork(_artworkStyle);

        private static Texture2D GetWaterArtwork(int style)
        {
            style = Math.Max(WATER_STYLE_NATURAL, Math.Min(WATER_STYLE_MOONLIT, style));
            Texture2D artwork = _waterArtwork[style];
            if (artwork != null && !artwork.IsDisposed) return artwork;
            string fileName;
            switch (style)
            {
                case WATER_STYLE_WAVES:
                    fileName = "water-surface-waves.png";
                    break;
                case WATER_STYLE_CHOPPY:
                    fileName = "water-surface-choppy.png";
                    break;
                case WATER_STYLE_SWELL:
                    fileName = "water-surface-swell.png";
                    break;
                case WATER_STYLE_STORM:
                    fileName = "water-surface-storm.png";
                    break;
                case WATER_STYLE_MOONLIT:
                    fileName = "water-surface-moonlit.png";
                    break;
                default:
                    fileName = "water-surface.png";
                    break;
            }
            string externalPath = Path.Combine(
                CUOEnviroment.ExecutablePath,
                "ExternalImages",
                "water",
                fileName
            );
            try
            {
                if (File.Exists(externalPath))
                    artwork = PNGLoader.Instance.GetImageTexture(externalPath);
            }
            catch
            {
                artwork = null;
            }

            if (artwork != null && !artwork.IsDisposed)
            {
                _waterArtwork[style] = artwork;
                return artwork;
            }
            try
            {
                byte[] data;
                switch (style)
                {
                    case WATER_STYLE_WAVES:
                        data = Loader.GetWaterSurfaceWaves().ToArray();
                        break;
                    case WATER_STYLE_CHOPPY:
                        data = Loader.GetWaterSurfaceChoppy().ToArray();
                        break;
                    case WATER_STYLE_SWELL:
                        data = Loader.GetWaterSurfaceSwell().ToArray();
                        break;
                    case WATER_STYLE_STORM:
                        data = Loader.GetWaterSurfaceStorm().ToArray();
                        break;
                    case WATER_STYLE_MOONLIT:
                        data = Loader.GetWaterSurfaceMoonlit().ToArray();
                        break;
                    default:
                        data = Loader.GetWaterSurface().ToArray();
                        break;
                }
                using (MemoryStream stream = new MemoryStream(data, false))
                    artwork = Texture2D.FromStream(Client.Game.GraphicsDevice, stream);
            }
            catch
            {
                artwork = null;
            }
            _waterArtwork[style] = artwork;
            return artwork;
        }

        private static WaterEffect GetWaterEffect(GraphicsDevice graphicsDevice)
        {
            if (_waterEffect != null && !_waterEffect.IsDisposed) return _waterEffect;
            if (_waterEffectLoadAttempted) return null;
            _waterEffectLoadAttempted = true;
            try
            {
                string path = Path.Combine(CUOEnviroment.ExecutablePath, "Water.fxc");
                if (File.Exists(path))
                    _waterEffect = new WaterEffect(graphicsDevice, File.ReadAllBytes(path));
            }
            catch
            {
                _waterEffect = null;
            }
            return _waterEffect;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static void DrawAtmospherePass(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather)
        {
            int quality = SceneryInteractionManager.Quality;
            if (quality <= SceneryInteractionManager.QUALITY_LOW || _tileCount == 0) return;

            Rectangle visible = new Rectangle(
                bounds.X - 160,
                bounds.Y - 100,
                bounds.Width + 320,
                bounds.Height + 200);
            Rectangle area = Rectangle.Intersect(_captureBounds, visible);
            if (area.IsEmpty) return;

            bool activeWeather = weather != null && weather.IsActive;
            bool storm = activeWeather && (weather.Type == WeatherType.WT_STORM_APPROACH
                || weather.Type == WeatherType.WT_STORM_BREWING || weather.Tempest);
            float wind = SceneryInteractionManager.SharedWind;
            float motion = Profile.ReduceWeatherMotion ? 0.35f : 1f;
            float seconds = Time.Ticks / 1000f;
            int direction = wind < -0.05f ? -1 : 1;
            int driftX = (int)(seconds * direction * (2.2f + Math.Abs(wind) * 1.4f) * motion);
            int driftY = (int)(seconds * (0.45f + Math.Abs(wind) * 0.18f) * motion);

            WaterEnvironment environment = CalculateEnvironment(weather);
            float cloudAmount = environment.CloudStrength;
            float daylight = EnvironmentalShadowManager.Daylight;
            float dusk = EnvironmentalShadowManager.Dusk;
            float moonlight = EnvironmentalShadowManager.Moonlight;
            float shadowAlpha = 0.012f + cloudAmount * (storm ? 0.050f : 0.026f);
            float reflectionAlpha = (0.010f + cloudAmount * 0.025f)
                * environment.ReflectionStrength;

            Texture2D shadow = GetAtmosphereTexture(
                ref _cloudShadow, 256, 128, new Color(0, 0, 0, 255), 0x46D2, false);
            Texture2D reflection;
            if (dusk > daylight && dusk > moonlight)
                reflection = GetAtmosphereTexture(ref _duskCloudReflection, 256, 128,
                    new Color(218, 177, 137, 255), 0x46D2, false);
            else if (daylight >= moonlight)
                reflection = GetAtmosphereTexture(ref _cloudReflection, 256, 128,
                    new Color(185, 205, 214, 255), 0x46D2, false);
            else
                reflection = GetAtmosphereTexture(ref _moonCloudReflection, 256, 128,
                    new Color(145, 174, 210, 255), 0x46D2, false);

            DrawWorldAnchoredAtmosphereLayer(
                batcher, shadow, area, 820, 410, 47, 83, driftX, driftY, shadowAlpha);
            DrawWorldAnchoredAtmosphereLayer(
                batcher, reflection, area, 1037, 519, 211, 137,
                driftX + 12, driftY + 7, reflectionAlpha);

            float mistAlpha = environment.MistStrength > 0.01f
                ? 0.020f + environment.MistStrength * 0.125f : 0f;
            if (mistAlpha > 0f)
            {
                Texture2D mist = GetAtmosphereTexture(
                    ref _waterMist, 320, 96, new Color(185, 204, 211, 255), 0xA193, true);
                DrawWorldAnchoredAtmosphereLayer(
                    batcher, mist, area, 760, 228, 389, 59,
                    driftX * 2, driftY, mistAlpha);
                if (quality == SceneryInteractionManager.QUALITY_HIGH)
                    DrawWorldAnchoredAtmosphereLayer(
                        batcher, mist, area, 1189, 357, 101, 241,
                        -driftX, driftY / 2, mistAlpha * 0.42f);
            }

            DrawCelestialGlints(batcher, bounds, environment.GlintStrength);
            DrawWeatherWhitecaps(batcher, bounds, environment.WhitecapStrength);
            batcher.SetSampler(null);
        }

        private static void DrawWorldAnchoredAtmosphereLayer(
            UltimaBatcher2D batcher,
            Texture2D texture,
            Rectangle area,
            int tileWidth,
            int tileHeight,
            int saltX,
            int saltY,
            int driftX,
            int driftY,
            float alpha)
        {
            if (texture == null || texture.IsDisposed || alpha <= 0f) return;

            Point playerScreen = PathPreview.TileToScreen(
                World.Player.X, World.Player.Y, World.Player.Z);
            int worldX = (World.Player.X - World.Player.Y) * 22;
            int worldY = (World.Player.X + World.Player.Y) * 22 - World.Player.Z * 4;
            int anchorX = playerScreen.X
                - PositiveModulo(worldX + saltX - driftX, tileWidth);
            int anchorY = playerScreen.Y
                - PositiveModulo(worldY + saltY - driftY, tileHeight);
            int startX = area.Left - PositiveModulo(area.Left - anchorX, tileWidth);
            int startY = area.Top - PositiveModulo(area.Top - anchorY, tileHeight);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);

            batcher.SetCustomEffect(null);
            batcher.SetSampler(SamplerState.LinearWrap);
            for (int y = startY; y < area.Bottom; y += tileHeight)
            {
                for (int x = startX; x < area.Right; x += tileWidth)
                    batcher.Draw(texture, new Rectangle(x, y, tileWidth, tileHeight), hue);
            }
        }

        private static void DrawCelestialGlints(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            float strength)
        {
            if (strength < 0.10f) return;

            Texture2D glint = GetWaterGlintTexture();
            int density = SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_HIGH
                ? 23 : 37;
            for (int i = 0; i < _tileCount; i++)
            {
                ref WaterTile tile = ref _tiles[i];
                if (tile.Depth < 3 || tile.Seed % density != 0) continue;
                uint cycle = (Time.Ticks + (uint)tile.Seed * 97u) % 3600u;
                if (cycle > 900u) continue;
                float phase = cycle / 900f;
                float pulse = 1f - Math.Abs(phase * 2f - 1f);
                Point p = PathPreview.TileToScreen(tile.X, tile.Y, tile.Z);
                if (p.X < bounds.X - 20 || p.X > bounds.Right + 20
                    || p.Y < bounds.Y - 12 || p.Y > bounds.Bottom + 12) continue;
                int width = 4 + (tile.Seed & 7);
                int height = 2 + ((tile.Seed >> 3) & 1);
                float alpha = pulse * strength * (0.055f + (tile.Seed & 3) * 0.009f);
                batcher.Draw(glint,
                    new Rectangle(p.X - width / 2, p.Y - height / 2, width, height),
                    ShaderHueTranslator.GetHueVector(0, false, alpha));
            }
        }

        private static void DrawWeatherWhitecaps(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            float strength)
        {
            if (strength < 0.42f) return;

            Texture2D ripple = GetRippleTexture();
            int density = SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_HIGH
                ? 11 : 17;
            for (int i = 0; i < _tileCount; i++)
            {
                ref WaterTile tile = ref _tiles[i];
                if (tile.Depth < 3 || tile.Seed % density != 0) continue;
                uint cycle = (Time.Ticks + (uint)tile.Seed * 61u) % 2100u;
                if (cycle > 720u) continue;
                float phase = cycle / 720f;
                Point p = PathPreview.TileToScreen(tile.X, tile.Y, tile.Z);
                if (p.X < bounds.X - 24 || p.X > bounds.Right + 24
                    || p.Y < bounds.Y - 14 || p.Y > bounds.Bottom + 14) continue;
                int width = 9 + (int)(phase * 10f) + (tile.Seed & 3);
                int height = Math.Max(4, width / 3);
                float alpha = (1f - phase) * strength * 0.15f;
                batcher.Draw(ripple,
                    new Rectangle(p.X - width / 2, p.Y - height / 2, width, height),
                    ShaderHueTranslator.GetHueVector(0, false, alpha));
            }
        }

        private static void DrawWeatherPass(UltimaBatcher2D batcher, Rectangle bounds, Weather weather)
        {
            int quality = SceneryInteractionManager.Quality;
            float ambience = EnvironmentalShadowManager.AmbienceStrength;
            float visibility = CalculateVisibility(ambience);
            bool storm = weather != null && weather.IsActive
                && (weather.Type == WeatherType.WT_STORM_APPROACH
                    || weather.Type == WeatherType.WT_STORM_BREWING || weather.Tempest);

            float frost = World.Season == Season.Winter && weather != null
                && weather.Coldness > 0.35f
                ? Math.Min(1f, (weather.Coldness - 0.35f) / 0.65f) : 0f;
            bool rain = weather != null && weather.IsActive
                && (weather.Type == WeatherType.WT_RAIN || storm);
            if (frost > 0.01f || rain && quality > SceneryInteractionManager.QUALITY_LOW)
                DrawSurfaceEvents(batcher, bounds, weather, frost, visibility, rain);
        }

        private static void DrawSurfaceEvents(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            float frost,
            float visibility,
            bool rain)
        {
            Texture2D frozen = frost > 0f
                ? GetDiamond(ref _frostDiamond, new Color(190, 222, 235, 255)) : null;
            Texture2D ripple = rain ? GetRippleTexture() : null;
            int density = SceneryInteractionManager.Quality == SceneryInteractionManager.QUALITY_HIGH ? 7 : 11;

            for (int i = 0; i < _tileCount; i++)
            {
                ref WaterTile tile = ref _tiles[i];
                Point p = PathPreview.TileToScreen(tile.X, tile.Y, tile.Z);
                if (p.X < bounds.X - 30 || p.X > bounds.Right + 30
                    || p.Y < bounds.Y - 20 || p.Y > bounds.Bottom + 20) continue;

                if (frozen != null && tile.Depth <= 1 && (tile.Seed & 3) == 0)
                {
                    batcher.Draw(frozen, new Rectangle(p.X - 22, p.Y - 11, 44, 22),
                        ShaderHueTranslator.GetHueVector(0, false, 0.075f * frost * visibility));
                }

                if (ripple == null || tile.Seed % density != 0) continue;
                uint cycle = (Time.Ticks + (uint)tile.Seed * 37u) % 1250u;
                if (cycle > 560u) continue;
                float t = cycle / 560f;
                int width = 7 + (int)(t * 17f);
                int height = Math.Max(4, width / 2);
                float alpha = (1f - t) * (0.24f + weather.VisualIntensity * 0.22f) * visibility;
                batcher.Draw(ripple,
                    new Rectangle(p.X - width / 2, p.Y - height / 2, width, height),
                    ShaderHueTranslator.GetHueVector(0, false, alpha));
            }
        }

        private static Texture2D GetDiamond(ref Texture2D texture, Color color)
        {
            if (texture != null && !texture.IsDisposed) return texture;
            const int width = 44, height = 22;
            Color[] data = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                int half = y <= height / 2 ? y * 2 : (height - 1 - y) * 2;
                int start = width / 2 - half;
                int end = width / 2 + half;
                for (int x = Math.Max(0, start); x <= Math.Min(width - 1, end); x++)
                    data[y * width + x] = color;
            }
            texture = new Texture2D(Client.Game.GraphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private static Texture2D GetRippleTexture()
        {
            if (_ripple != null && !_ripple.IsDisposed) return _ripple;
            const int width = 32, height = 16;
            Color[] data = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = (y + 0.5f - height * 0.5f) / (height * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                    float radius = (float)Math.Sqrt(nx * nx + ny * ny);
                    float ring = Math.Max(0f, 1f - Math.Abs(radius - 0.72f) / 0.13f);
                    ring *= ring;
                    float coverage = ring * 0.52f;
                    byte alpha = (byte)(coverage * 255f);
                    data[y * width + x] = new Color(
                        (byte)(190 * coverage),
                        (byte)(225 * coverage),
                        (byte)(235 * coverage),
                        alpha);
                }
            }
            _ripple = new Texture2D(Client.Game.GraphicsDevice, width, height);
            _ripple.SetData(data);
            return _ripple;
        }

        private static Texture2D GetWaterGlintTexture()
        {
            if (_waterGlint != null && !_waterGlint.IsDisposed) return _waterGlint;
            const int width = 16, height = 5;
            Color[] data = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = Math.Abs(y - (height - 1) * 0.5f) / (height * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float dx = Math.Abs(x - (width - 1) * 0.5f) / (width * 0.5f);
                    float coverage = Math.Max(0f, 1f - dx * dx) * Math.Max(0f, 1f - dy);
                    coverage *= coverage;
                    data[y * width + x] = Premultiplied(new Color(205, 231, 238, 255), coverage);
                }
            }
            _waterGlint = new Texture2D(Client.Game.GraphicsDevice, width, height);
            _waterGlint.SetData(data);
            return _waterGlint;
        }

        private static Texture2D GetAtmosphereTexture(
            ref Texture2D texture,
            int width,
            int height,
            Color color,
            int seed,
            bool horizontalBands)
        {
            if (texture != null && !texture.IsDisposed) return texture;

            float[] field = new float[width * height];
            int blobs = horizontalBands ? 13 : 18;
            for (int i = 0; i < blobs; i++)
            {
                uint hash = Hash(seed + i * 73, seed - i * 131);
                float cx = hash % (uint)width;
                float cy = (hash >> 9) % (uint)height;
                float rx = horizontalBands
                    ? 34f + ((hash >> 17) & 63)
                    : 20f + ((hash >> 17) & 31);
                float ry = horizontalBands
                    ? 6f + ((hash >> 23) & 15)
                    : 12f + ((hash >> 22) & 23);
                float weight = 0.45f + ((hash >> 28) & 7) * 0.08f;

                for (int y = 0; y < height; y++)
                {
                    float dy = Math.Abs(y - cy);
                    dy = Math.Min(dy, height - dy) / ry;
                    if (dy > 2.2f) continue;
                    for (int x = 0; x < width; x++)
                    {
                        float dx = Math.Abs(x - cx);
                        dx = Math.Min(dx, width - dx) / rx;
                        float distance = dx * dx + dy * dy;
                        if (distance > 4.8f) continue;
                        field[y * width + x] += (float)Math.Exp(-distance * 1.65f) * weight;
                    }
                }
            }

            Color[] data = new Color[width * height];
            for (int i = 0; i < data.Length; i++)
            {
                float coverage = Math.Max(0f, Math.Min(1f,
                    (field[i] - (horizontalBands ? 0.16f : 0.20f))
                    / (horizontalBands ? 0.82f : 0.95f)));
                coverage = coverage * coverage * (3f - 2f * coverage);
                data[i] = Premultiplied(color, coverage);
            }

            texture = new Texture2D(Client.Game.GraphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private static Color Premultiplied(Color color, float coverage)
        {
            coverage = Math.Max(0f, Math.Min(1f, coverage));
            return new Color(
                (byte)(color.R * coverage),
                (byte)(color.G * coverage),
                (byte)(color.B * coverage),
                (byte)(color.A * coverage));
        }

        private static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static float Clamp(float value, float min, float max)
            => Math.Max(min, Math.Min(max, value));

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint value = (uint)x * 73856093u ^ (uint)y * 19349663u;
                value ^= value >> 13;
                return value;
            }
        }
    }
}

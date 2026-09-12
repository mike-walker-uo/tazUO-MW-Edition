#region license
// TazUO addition. Exact-mask material variation and decorative detail pass.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal static class TerrainMaterialManager
    {
        private const int MAX_MASK_SPRITES = 32768;
        private const int MATERIAL_MARGIN = 256;
        private const int PATCH_WIDTH = 96;
        private const int PATCH_HEIGHT = 48;
        private const int PATCH_VARIANTS = 3;
        private const int DETAIL_TEXTURE_SIZE = 15;
        private const int TERRAIN_STENCIL = 16;

        private static readonly ScenerySurface[] _materials =
        {
            ScenerySurface.Sand,
            ScenerySurface.Grass,
            ScenerySurface.Mine,
            ScenerySurface.Dungeon,
            ScenerySurface.Dirt,
            ScenerySurface.Snow
        };

        private struct MaskSprite
        {
            public Texture2D Texture;
            public Rectangle Source;
            public Vector2 Position;
            public float Depth;
            public int WorldX, WorldY;
            public sbyte WorldZ;
            public bool HasWorldPosition;
            public ScenerySurface Surface;
            public bool Stretched;
            public UltimaBatcher2D.YOffsets YOffsets;
            public Vector3 NormalTop, NormalRight, NormalLeft, NormalBottom;
        }

        internal struct TerrainEnvironment
        {
            public float Wetness;
            public float PatchDensity;
            public float ReliefStrength;
            public float DetailDensity;
            public float DetailOpacity;
            public float ShadowStrength;
            public float SparkleStrength;
            public float WindParticles;
            public float AirborneDust;
        }

        private static readonly MaskSprite[] _maskSprites = new MaskSprite[MAX_MASK_SPRITES];
        private static readonly Texture2D[] _patchTextures = new Texture2D[9 * PATCH_VARIANTS];
        private static readonly Texture2D[] _detailTextures = new Texture2D[27];
        private static Texture2D _detailShadow;
        private static readonly bool[] _present = new bool[9];
        private static readonly int[] _intensityPercent =
            { 100, 100, 100, 100, 100, 100, 100, 100, 100 };

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
                ReferenceStencil = TERRAIN_STENCIL,
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
                ReferenceStencil = TERRAIN_STENCIL,
                StencilMask = 0xFF,
                StencilWriteMask = 0
            });

        private static int _maskSpriteCount;
        private static bool _captureActive;
        private static Rectangle _captureBounds;
        private static float _snowCover;
        private static bool _debugEnabled;

        private static Profile Profile => ProfileManager.CurrentProfile;
        internal static bool DebugEnabled => _debugEnabled;

        public static void SetDebugEnabled(bool enabled)
        {
            _debugEnabled = enabled;
        }

        internal static string IntensitySummary
        {
            get
            {
                int value = GetIntensityPercent(_materials[0]);
                for (int i = 1; i < _materials.Length; i++)
                {
                    if (GetIntensityPercent(_materials[i]) != value) return "Mixed";
                }
                return value + "%";
            }
        }

        internal static int CommonIntensityPercent
        {
            get
            {
                int value = GetIntensityPercent(_materials[0]);
                for (int i = 1; i < _materials.Length; i++)
                {
                    if (GetIntensityPercent(_materials[i]) != value) return -1;
                }
                return value;
            }
        }

        internal static int GetIntensityPercent(ScenerySurface surface)
        {
            int index = (int)surface;
            return index >= 0 && index < _intensityPercent.Length
                ? _intensityPercent[index] : 100;
        }

        internal static string IntensityReport
            => $"Sand {GetIntensityPercent(ScenerySurface.Sand)}%, "
                + $"grass {GetIntensityPercent(ScenerySurface.Grass)}%, "
                + $"mine {GetIntensityPercent(ScenerySurface.Mine)}%, "
                + $"dungeon {GetIntensityPercent(ScenerySurface.Dungeon)}%, "
                + $"dirt {GetIntensityPercent(ScenerySurface.Dirt)}%, "
                + $"snow {GetIntensityPercent(ScenerySurface.Snow)}%.";

        public static bool TryGetIntensity(string material, out int percent)
        {
            percent = 0;
            ScenerySurface surface = ParseSurface(material);
            if (surface == ScenerySurface.None) return false;
            percent = GetIntensityPercent(surface);
            return true;
        }

        public static bool SetIntensity(string material, int percent)
        {
            if (percent < 0 || percent > 200 || string.IsNullOrWhiteSpace(material)) return false;
            string name = material.Trim().ToLowerInvariant();
            if (name == "all")
            {
                for (int i = 0; i < _materials.Length; i++)
                {
                    _intensityPercent[(int)_materials[i]] = percent;
                    SetProfileIntensity(_materials[i], percent);
                }
                return true;
            }

            ScenerySurface surface = ParseSurface(name);
            if (surface == ScenerySurface.None) return false;
            _intensityPercent[(int)surface] = percent;
            SetProfileIntensity(surface, percent);
            return true;
        }

        private static ScenerySurface ParseSurface(string material)
        {
            switch (material?.Trim().ToLowerInvariant())
            {
                case "sand":
                case "desert": return ScenerySurface.Sand;
                case "grass": return ScenerySurface.Grass;
                case "mine":
                case "mines": return ScenerySurface.Mine;
                case "dungeon":
                case "dungeons": return ScenerySurface.Dungeon;
                case "dirt": return ScenerySurface.Dirt;
                case "snow": return ScenerySurface.Snow;
                default: return ScenerySurface.None;
            }
        }

        public static void ApplyProfile(Profile profile)
        {
            if (profile == null) return;
            ApplyProfileIntensity(profile, ScenerySurface.Sand, profile.SandMaterialIntensity);
            ApplyProfileIntensity(profile, ScenerySurface.Grass, profile.GrassMaterialIntensity);
            ApplyProfileIntensity(profile, ScenerySurface.Mine, profile.MineMaterialIntensity);
            ApplyProfileIntensity(profile, ScenerySurface.Dungeon, profile.DungeonMaterialIntensity);
            ApplyProfileIntensity(profile, ScenerySurface.Dirt, profile.DirtMaterialIntensity);
            ApplyProfileIntensity(profile, ScenerySurface.Snow, profile.SnowMaterialIntensity);
        }

        private static void ApplyProfileIntensity(
            Profile profile, ScenerySurface surface, int percent)
        {
            int value = Math.Max(0, Math.Min(200, percent));
            _intensityPercent[(int)surface] = value;
            SetProfileIntensity(surface, value, profile);
        }

        private static void SetProfileIntensity(
            ScenerySurface surface, int percent, Profile profile = null)
        {
            profile = profile ?? Profile;
            if (profile == null) return;
            switch (surface)
            {
                case ScenerySurface.Sand: profile.SandMaterialIntensity = percent; break;
                case ScenerySurface.Grass: profile.GrassMaterialIntensity = percent; break;
                case ScenerySurface.Mine: profile.MineMaterialIntensity = percent; break;
                case ScenerySurface.Dungeon: profile.DungeonMaterialIntensity = percent; break;
                case ScenerySurface.Dirt: profile.DirtMaterialIntensity = percent; break;
                case ScenerySurface.Snow: profile.SnowMaterialIntensity = percent; break;
            }
        }

        public static bool BeginFrameCapture(bool stencilAvailable, Weather weather)
        {
            if (_maskSpriteCount > 0)
                Array.Clear(_maskSprites, 0, _maskSpriteCount);
            Array.Clear(_present, 0, _present.Length);
            _maskSpriteCount = 0;
            _captureBounds = Rectangle.Empty;
            _snowCover = weather?.SnowCover ?? 0f;
            _captureActive = !CUOEnviroment.SafeGraphicsMode && stencilAvailable && AmbienceOverlay.Enabled && Profile != null
                && SceneryInteractionManager.Quality > SceneryInteractionManager.QUALITY_LOW
                && (HasVisibleIntensity() || _debugEnabled)
                && World.InGame && World.Player != null;
            return _captureActive;
        }

        private static bool HasVisibleIntensity()
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (GetIntensityPercent(_materials[i]) > 0) return true;
            }
            return false;
        }

        public static void ResetSession()
        {
            if (_maskSpriteCount > 0)
                Array.Clear(_maskSprites, 0, _maskSpriteCount);
            Array.Clear(_present, 0, _present.Length);
            _maskSpriteCount = 0;
            _captureActive = false;
            _captureBounds = Rectangle.Empty;
            _snowCover = 0f;
            _debugEnabled = false;
            for (int i = 0; i < _materials.Length; i++)
                _intensityPercent[(int)_materials[i]] = 100;
        }

        public static void CaptureLand(
            Land land,
            Texture2D texture,
            Vector2 position,
            Rectangle source,
            float depth)
        {
            if (!_captureActive || land == null || land.TileData.IsWet) return;
            ScenerySurface surface = SceneryInteractionManager.ClassifyLandMaterial(
                land.OriginalGraphic,
                land.TileData.Name,
                SceneryInteractionManager.IsDungeonArea,
                EnvironmentalShadowManager.IsIndoors ? 0f : _snowCover
            );
            CaptureSprite(texture, position, source, depth, surface,
                land.X, land.Y, land.Z, true);
        }

        public static void CaptureStretchedLand(
            Land land,
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
            if (!_captureActive || land == null || land.TileData.IsWet
                || texture == null || texture.IsDisposed || _maskSpriteCount >= MAX_MASK_SPRITES)
                return;
            ScenerySurface surface = SceneryInteractionManager.ClassifyLandMaterial(
                land.OriginalGraphic,
                land.TileData.Name,
                SceneryInteractionManager.IsDungeonArea,
                EnvironmentalShadowManager.IsIndoors ? 0f : _snowCover
            );
            if (!IsEnhanced(surface)) return;

            ref MaskSprite sprite = ref _maskSprites[_maskSpriteCount++];
            sprite.Texture = texture;
            sprite.Source = source;
            sprite.Position = position;
            sprite.Depth = depth;
            sprite.WorldX = land.X;
            sprite.WorldY = land.Y;
            sprite.WorldZ = land.Z;
            sprite.HasWorldPosition = true;
            sprite.Surface = surface;
            sprite.Stretched = true;
            sprite.YOffsets = yOffsets;
            sprite.NormalTop = normalTop;
            sprite.NormalRight = normalRight;
            sprite.NormalLeft = normalLeft;
            sprite.NormalBottom = normalBottom;
            _present[(int)surface] = true;
            IncludeBounds((int)position.X, (int)position.Y - 32, 44, 108);
        }

        public static void CaptureStatic(
            Texture2D texture,
            Vector2 position,
            Rectangle source,
            float depth,
            ScenerySurface surface)
            => CaptureSprite(texture, position, source, depth, surface);

        private static void CaptureSprite(
            Texture2D texture,
            Vector2 position,
            Rectangle source,
            float depth,
            ScenerySurface surface,
            int worldX = 0,
            int worldY = 0,
            sbyte worldZ = 0,
            bool hasWorldPosition = false)
        {
            if (!_captureActive || !IsEnhanced(surface) || texture == null || texture.IsDisposed
                || _maskSpriteCount >= MAX_MASK_SPRITES) return;

            ref MaskSprite sprite = ref _maskSprites[_maskSpriteCount++];
            sprite.Texture = texture;
            sprite.Source = source;
            sprite.Position = position;
            sprite.Depth = depth;
            sprite.WorldX = worldX;
            sprite.WorldY = worldY;
            sprite.WorldZ = worldZ;
            sprite.HasWorldPosition = hasWorldPosition;
            sprite.Surface = surface;
            sprite.Stretched = false;
            _present[(int)surface] = true;
            IncludeBounds((int)position.X, (int)position.Y, source.Width, source.Height);
        }

        private static bool IsEnhanced(ScenerySurface surface)
            => surface == ScenerySurface.Sand || surface == ScenerySurface.Grass
                || surface == ScenerySurface.Mine || surface == ScenerySurface.Dungeon
                || surface == ScenerySurface.Dirt || surface == ScenerySurface.Snow;

        private static void IncludeBounds(int x, int y, int width, int height)
        {
            Rectangle value = new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
            _captureBounds = _captureBounds.IsEmpty ? value : Rectangle.Union(_captureBounds, value);
        }

        public static void DrawMaskedWorld(UltimaBatcher2D batcher, Rectangle bounds, Weather weather)
        {
            if (!_captureActive || _maskSpriteCount == 0 || _captureBounds.IsEmpty) return;
            Rectangle area = Rectangle.Intersect(
                _captureBounds,
                new Rectangle(bounds.X - MATERIAL_MARGIN, bounds.Y - MATERIAL_MARGIN,
                    bounds.Width + MATERIAL_MARGIN * 2, bounds.Height + MATERIAL_MARGIN * 2)
            );
            if (area.IsEmpty) return;

            try
            {
                for (int materialIndex = 0; materialIndex < _materials.Length; materialIndex++)
                {
                    ScenerySurface surface = _materials[materialIndex];
                    int intensity = GetIntensityPercent(surface);
                    if (!_present[(int)surface]) continue;

                    batcher.SetCustomEffect(null);
                    batcher.SetSampler(null);
                    batcher.SetBlendState(null);
                    batcher.SetStencil(null);
                    batcher.GraphicsDevice.Clear(ClearOptions.Stencil, Color.Transparent, 1f, 0);
                    WriteMask(batcher, surface);
                    batcher.SetBlendState(null);
                    batcher.SetStencil(_maskReadStencil.Value);
                    if (intensity > 0)
                    {
                        DrawMaterial(batcher, area, surface, weather);

                        if (surface == ScenerySurface.Mine || surface == ScenerySurface.Dungeon)
                        {
                            float moisture = Math.Max(weather?.Wetness ?? 0f,
                                surface == ScenerySurface.Dungeon ? 0.34f : 0.20f);
                            SceneryInteractionManager.DrawTerrainLightResponse(
                                batcher,
                                bounds,
                                moisture,
                                (surface == ScenerySurface.Dungeon ? 1f : 0.65f)
                                    * intensity / 100f
                            );
                        }
                    }
                    if (_debugEnabled) DrawDebugOverlay(batcher, area, surface);
                }
            }
            finally
            {
                batcher.SetBlendState(null);
                batcher.SetStencil(null);
                batcher.SetCustomEffect(null);
                batcher.SetSampler(null);
            }
        }

        private static void DrawDebugOverlay(
            UltimaBatcher2D batcher, Rectangle area, ScenerySurface surface)
        {
            Color color;
            switch (surface)
            {
                case ScenerySurface.Sand: color = new Color(255, 190, 55, 255); break;
                case ScenerySurface.Grass: color = new Color(45, 220, 70, 255); break;
                case ScenerySurface.Mine: color = new Color(220, 125, 45, 255); break;
                case ScenerySurface.Dungeon: color = new Color(175, 75, 220, 255); break;
                case ScenerySurface.Dirt: color = new Color(145, 85, 45, 255); break;
                case ScenerySurface.Snow: color = new Color(90, 215, 255, 255); break;
                default: return;
            }
            Texture2D texture = SolidColorTextureCache.GetTexture(color);
            batcher.Draw(texture, area,
                ShaderHueTranslator.GetHueVector(0, false, 0.34f));
        }

        private static void WriteMask(UltimaBatcher2D batcher, ScenerySurface surface)
        {
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            batcher.SetBlendState(_maskWriteBlend.Value);
            batcher.SetStencil(_maskWriteStencil.Value);
            for (int i = 0; i < _maskSpriteCount; i++)
            {
                ref MaskSprite sprite = ref _maskSprites[i];
                if (sprite.Surface != surface || sprite.Texture == null || sprite.Texture.IsDisposed)
                    continue;
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
                        hue,
                        sprite.Depth
                    );
                }
                else
                {
                    batcher.Draw(
                        sprite.Texture,
                        sprite.Position,
                        sprite.Source,
                        hue,
                        0f,
                        Vector2.Zero,
                        Vector2.One,
                        SpriteEffects.None,
                        sprite.Depth
                    );
                }
            }
        }

        private static void DrawMaterial(
            UltimaBatcher2D batcher,
            Rectangle area,
            ScenerySurface surface,
            Weather weather)
        {
            TerrainEnvironment environment = CalculateEnvironment(surface, weather);
            float opacity = CalculateOpacity(surface, SceneryInteractionManager.Quality,
                weather?.Wetness ?? 0f, _snowCover);
            float intensity = GetIntensityPercent(surface) / 100f;
            opacity = ApplyIntensity(opacity, GetIntensityPercent(surface));
            opacity *= environment.ReliefStrength;
            if (opacity <= 0f) return;

            DrawMacroPatches(batcher, area, surface, opacity, intensity, environment);
            DrawWindGrains(batcher, area, surface, intensity, environment.WindParticles);
            DrawStoneMotes(batcher, area, surface, intensity, environment.AirborneDust);
            DrawSurfaceDetails(batcher, area, surface, intensity, environment);
        }

        private static void DrawMacroPatches(
            UltimaBatcher2D batcher,
            Rectangle area,
            ScenerySurface surface,
            float opacity,
            float intensity,
            TerrainEnvironment environment)
        {
            int clusterSize;
            switch (surface)
            {
                case ScenerySurface.Grass: clusterSize = 5; break;
                case ScenerySurface.Sand:
                case ScenerySurface.Snow: clusterSize = 6; break;
                default: clusterSize = 4; break;
            }

            int quality = SceneryInteractionManager.Quality;
            int chance = quality >= SceneryInteractionManager.QUALITY_HIGH ? 150 : 92;
            chance = Math.Min(235, (int)(chance * Math.Min(1.35f, intensity)
                * environment.PatchDensity));
            float wetness = environment.Wetness;
            batcher.SetSampler(SamplerState.PointClamp);

            for (int i = 0; i < _maskSpriteCount; i++)
            {
                ref MaskSprite sprite = ref _maskSprites[i];
                if (sprite.Surface != surface || !sprite.HasWorldPosition) continue;

                int cellX = FloorDivide(sprite.WorldX, clusterSize);
                int cellY = FloorDivide(sprite.WorldY, clusterSize);
                uint value = Hash(cellX, cellY, (uint)surface * 0xA24BAED5u);
                int anchorX = cellX * clusterSize + (int)((value >> 8) % (uint)clusterSize);
                int anchorY = cellY * clusterSize + (int)((value >> 16) % (uint)clusterSize);
                if (sprite.WorldX != anchorX || sprite.WorldY != anchorY) continue;

                bool boundary = IsMaterialBoundary(sprite, surface);
                if ((value & 0xFFu) >= (uint)chance
                    && !(boundary && (value & 0xFFu) < 210u))
                    continue;

                bool wetVariant = surface != ScenerySurface.Snow
                    && ((value >> 18) & 0xFFu) < (uint)(wetness * 220f);
                int variant = wetVariant ? 2 : (int)((value >> 25) & 1u);
                Texture2D texture = GetPatchTexture(surface, variant);
                if (texture == null || texture.IsDisposed) continue;

                Point p = PathPreview.TileToScreen(
                    sprite.WorldX, sprite.WorldY, sprite.WorldZ);
                GetPatchSize(surface, value, out int width, out int height);
                if (p.X + width / 2 < area.Left || p.X - width / 2 > area.Right
                    || p.Y + height / 2 < area.Top || p.Y - height / 2 > area.Bottom)
                    continue;

                float alpha = Math.Min(0.88f, opacity * (boundary ? 0.72f : 0.58f));
                if (variant == 2) alpha *= 0.65f + wetness * 0.35f;
                SpriteEffects effects = (value & 0x40000000u) != 0
                    ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                batcher.Draw(
                    texture,
                    new Rectangle(p.X - width / 2, p.Y - height / 2, width, height),
                    null,
                    ShaderHueTranslator.GetHueVector(0, false, alpha),
                    0f,
                    Vector2.Zero,
                    effects,
                    0f);
            }
            batcher.SetSampler(null);
        }

        private static bool IsMaterialBoundary(
            MaskSprite sprite,
            ScenerySurface surface)
            => SceneryInteractionManager.ClassifySurface(
                    sprite.WorldX + 1, sprite.WorldY, sprite.WorldZ) != surface
                || SceneryInteractionManager.ClassifySurface(
                    sprite.WorldX - 1, sprite.WorldY, sprite.WorldZ) != surface
                || SceneryInteractionManager.ClassifySurface(
                    sprite.WorldX, sprite.WorldY + 1, sprite.WorldZ) != surface
                || SceneryInteractionManager.ClassifySurface(
                    sprite.WorldX, sprite.WorldY - 1, sprite.WorldZ) != surface;

        private static void GetPatchSize(
            ScenerySurface surface,
            uint value,
            out int width,
            out int height)
        {
            int variation = (int)((value >> 19) & 31u);
            switch (surface)
            {
                case ScenerySurface.Sand:
                    width = 126 + variation * 2;
                    height = 48 + variation / 2;
                    break;
                case ScenerySurface.Grass:
                    width = 92 + variation * 2;
                    height = 40 + variation / 2;
                    break;
                case ScenerySurface.Snow:
                    width = 118 + variation * 2;
                    height = 46 + variation / 2;
                    break;
                default:
                    width = 78 + variation * 2;
                    height = 36 + variation / 2;
                    break;
            }
        }

        private static void DrawWindGrains(
            UltimaBatcher2D batcher,
            Rectangle area,
            ScenerySurface surface,
            float intensity,
            float weatherParticles)
        {
            if (surface != ScenerySurface.Sand && surface != ScenerySurface.Snow
                || Profile?.ReduceWeatherMotion == true || weatherParticles <= 0.05f) return;
            float wind = SceneryInteractionManager.SharedWind;
            if (Math.Abs(wind) < 0.45f) return;
            int count = SceneryInteractionManager.ScaleCount(
                surface == ScenerySurface.Sand ? 11 : 15);
            int span = Math.Max(1, area.Width + 24);
            int direction = wind < 0f ? -1 : 1;
            Color color = surface == ScenerySurface.Sand
                ? new Color(221, 194, 135, 255) : new Color(226, 239, 248, 255);
            Texture2D grain = SolidColorTextureCache.GetTexture(color);
            for (int i = 0; i < count; i++)
            {
                int travel = (int)(Time.Ticks * (0.018f + i % 4 * 0.005f)
                    * Math.Max(0.5f, Math.Abs(wind)));
                int x = (i * 389 + direction * travel) % span;
                if (x < 0) x += span;
                x += area.X - 12;
                int y = area.Y + (i * 233 + (int)(5f * Math.Sin(Time.Ticks / 360f + i)))
                    % Math.Max(1, area.Height);
                int length = surface == ScenerySurface.Sand ? 3 + i % 3 : 2 + i % 2;
                batcher.DrawLine(
                    grain,
                    new Vector2(x, y),
                    new Vector2(x + direction * length, y - 1),
                    ShaderHueTranslator.GetHueVector(0, false,
                        Math.Min(0.95f,
                            (surface == ScenerySurface.Sand ? 0.10f : 0.13f)
                            * intensity * weatherParticles)),
                    1
                );
            }
        }

        private static void DrawStoneMotes(
            UltimaBatcher2D batcher,
            Rectangle area,
            ScenerySurface surface,
            float intensity,
            float airborneDust)
        {
            if (surface != ScenerySurface.Mine && surface != ScenerySurface.Dungeon
                || airborneDust <= 0.05f) return;
            int count = SceneryInteractionManager.ScaleCount(
                surface == ScenerySurface.Mine ? 7 : 10);
            Texture2D mote = SolidColorTextureCache.GetTexture(
                surface == ScenerySurface.Mine
                    ? new Color(150, 139, 120, 255) : new Color(170, 176, 168, 255));
            int width = Math.Max(1, area.Width);
            int height = Math.Max(1, area.Height);
            for (int i = 0; i < count; i++)
            {
                int x = area.X + (i * 397 + (int)(7f * Math.Sin(Time.Ticks / 850f + i))) % width;
                int y = area.Y + (i * 229 + (int)(Time.Ticks * (0.003f + i % 3 * 0.001f))) % height;
                float pulse = 0.55f + 0.45f * (float)Math.Sin(Time.Ticks / 430f + i * 1.9f);
                batcher.Draw(
                    mote,
                    new Rectangle(x, y, 1, 1),
                    ShaderHueTranslator.GetHueVector(0, false,
                        Math.Min(0.95f, 0.11f * pulse * intensity * airborneDust))
                );
            }
        }

        private static void DrawSurfaceDetails(
            UltimaBatcher2D batcher,
            Rectangle area,
            ScenerySurface surface,
            float intensity,
            TerrainEnvironment environment)
        {
            int quality = SceneryInteractionManager.Quality;
            if (quality <= SceneryInteractionManager.QUALITY_LOW || intensity <= 0f) return;

            int cellSize = quality >= SceneryInteractionManager.QUALITY_HIGH ? 176 : 236;
            int chance = quality >= SceneryInteractionManager.QUALITY_HIGH ? 150 : 102;
            chance = Math.Min(235, (int)(chance * Math.Min(1.35f, intensity)
                * environment.DetailDensity));
            Point playerScreen = PathPreview.TileToScreen(
                World.Player.X, World.Player.Y, World.Player.Z);
            int worldX = (World.Player.X - World.Player.Y) * 22;
            int worldY = (World.Player.X + World.Player.Y) * 22 - World.Player.Z * 4;
            int anchorX = playerScreen.X - PositiveModulo(worldX, cellSize);
            int anchorY = playerScreen.Y - PositiveModulo(worldY, cellSize);
            int startX = area.Left - PositiveModulo(area.Left - anchorX, cellSize);
            int startY = area.Top - PositiveModulo(area.Top - anchorY, cellSize);
            float wetness = environment.Wetness;
            float celestial = Math.Max(EnvironmentalShadowManager.Daylight,
                EnvironmentalShadowManager.Moonlight * 0.55f);
            int motionX = CalculateMotionOffset(surface, SceneryInteractionManager.SharedWind,
                Profile?.ReduceWeatherMotion == true, Time.Ticks);
            Texture2D contactShadow = GetDetailShadowTexture();
            float weatherDetailStrength = wetness;
            if (surface == ScenerySurface.Snow)
                weatherDetailStrength = Math.Max(weatherDetailStrength,
                    Clamp01((environment.WindParticles - 0.70f) / 0.65f));

            for (int y = startY; y < area.Bottom; y += cellSize)
            {
                for (int x = startX; x < area.Right; x += cellSize)
                {
                    int cellX = FloorDivide(worldX + x - playerScreen.X, cellSize);
                    int cellY = FloorDivide(worldY + y - playerScreen.Y, cellSize);
                    uint value = Hash(cellX, cellY, (uint)surface * 0x9E3779B9u);
                    if ((value & 0xFFu) >= (uint)chance) continue;

                    const int margin = 34;
                    int centerX = x + margin
                        + (int)((value >> 8) % (uint)Math.Max(1, cellSize - margin * 2));
                    int centerY = y + margin / 2
                        + (int)((value >> 17) % (uint)Math.Max(1, cellSize - margin));
                    int count = quality >= SceneryInteractionManager.QUALITY_HIGH
                        ? 2 + (int)((value >> 27) & 3u) : 1 + (int)((value >> 29) & 1u);
                    if (intensity > 1.45f && quality >= SceneryInteractionManager.QUALITY_HIGH)
                        count++;

                    for (int detailIndex = 0; detailIndex < count; detailIndex++)
                    {
                        uint detailValue = Hash(
                            cellX * 7 + detailIndex,
                            cellY * 11 - detailIndex,
                            value ^ 0xC2B2AE35u);
                        int px = centerX + (int)((detailValue >> 8) % 73u) - 36 + motionX;
                        int py = centerY + (int)((detailValue >> 17) % 37u) - 18;
                        bool weatherDetail = ((detailValue >> 14) & 0xFFu)
                            < (uint)(weatherDetailStrength * 72f);
                        int detailVariant = weatherDetail ? 2
                            : ((detailValue >> 25) & 7u) == 0 ? 1 : 0;
                        bool accent = detailVariant == 1;
                        Texture2D detail = GetDetailTexture(surface, detailVariant);
                        if (detail == null || detail.IsDisposed) continue;

                        int size = GetDetailSize(surface, detailValue, accent);
                        float opacity = Math.Min(0.94f,
                            (quality >= SceneryInteractionManager.QUALITY_HIGH ? 0.84f : 0.66f)
                            * Math.Min(1.35f, intensity) * environment.DetailOpacity);
                        int shadowWidth = Math.Max(8, size - 2);
                        int shadowHeight = Math.Max(4, size / 3);
                        float shadowAlpha = 0.30f * EnvironmentalShadowManager.Opacity
                            * Math.Min(1.35f, intensity) * environment.ShadowStrength;
                        batcher.Draw(
                            contactShadow,
                            new Rectangle(px - shadowWidth / 2 + 2,
                                py + size / 4, shadowWidth, shadowHeight),
                            ShaderHueTranslator.GetHueVector(0, false, shadowAlpha));
                        batcher.Draw(
                            detail,
                            new Rectangle(px - size / 2, py - size / 2, size, size),
                            null,
                            ShaderHueTranslator.GetHueVector(0, false, opacity),
                            0f,
                            Vector2.Zero,
                            (detailValue & 0x10000000u) != 0
                                ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                            0f);

                        bool naturallyReflective = surface == ScenerySurface.Snow
                            || surface == ScenerySurface.Mine
                            || surface == ScenerySurface.Dungeon;
                        if ((wetness > 0.14f || naturallyReflective)
                            && ((detailValue >> 20) & 3u) == 0 && celestial > 0.08f)
                        {
                            float pulse = 0.35f + 0.65f * (float)Math.Sin(
                                Time.Ticks / 260f + (detailValue & 31u));
                            pulse = Math.Max(0f, pulse);
                            float reflective = naturallyReflective ? 0.32f : wetness * 0.26f;
                            Color color = surface == ScenerySurface.Mine
                                ? new Color(230, 205, 145, 255)
                                : new Color(215, 235, 248, 255);
                            Texture2D sparkle = SolidColorTextureCache.GetTexture(color);
                            float alpha = Math.Min(0.72f,
                                reflective * pulse * celestial * Math.Min(1.5f, intensity)
                                * environment.SparkleStrength);
                            batcher.Draw(sparkle,
                                new Rectangle(px + size / 4, py - size / 4, 2, 1),
                                ShaderHueTranslator.GetHueVector(0, false, alpha));
                            if (alpha > 0.24f)
                                batcher.Draw(sparkle,
                                    new Rectangle(px + size / 4 + 1,
                                        py - size / 4 - 1, 1, 3),
                                    ShaderHueTranslator.GetHueVector(0, false, alpha * 0.65f));
                        }
                    }
                }
            }
        }

        private static int GetDetailSize(ScenerySurface surface, uint value, bool accent)
        {
            int variation = (int)((value >> 5) & 3u);
            switch (surface)
            {
                case ScenerySurface.Grass: return (accent ? 16 : 20) + variation;
                case ScenerySurface.Sand: return 12 + variation;
                case ScenerySurface.Mine: return 12 + variation;
                case ScenerySurface.Dungeon: return 16 + variation;
                case ScenerySurface.Snow: return (accent ? 13 : 17) + variation;
                default: return 12 + variation;
            }
        }

        private static Texture2D GetDetailTexture(ScenerySurface surface, int variant)
        {
            variant = Math.Max(0, Math.Min(2, variant));
            int index = (int)surface * 3 + variant;
            Texture2D texture = _detailTextures[index];
            if (texture != null && !texture.IsDisposed) return texture;
            texture = BuildDetailTexture(surface, variant);
            _detailTextures[index] = texture;
            return texture;
        }

        private static Texture2D BuildDetailTexture(ScenerySurface surface, int variant)
        {
            bool accent = variant == 1;
            bool weathered = variant == 2;
            Color[] data = new Color[DETAIL_TEXTURE_SIZE * DETAIL_TEXTURE_SIZE];
            switch (surface)
            {
                case ScenerySurface.Grass:
                    DrawSoftPebble(data, new Color(17, 31, 12, 255), 7, 11, 5, 2, 0.34f);
                    for (int i = 0; i < 5; i++)
                    {
                        int rootX = 4 + i * 2;
                        int bend = i % 2 == 0 ? -1 : 1;
                        DrawPixelLine(data, rootX, 12, rootX + bend, 5 + i % 3,
                            weathered ? new Color(39, 83, 38, 255)
                                : i % 2 == 0 ? new Color(49, 104, 37, 255)
                                    : new Color(82, 132, 52, 255), 0.82f);
                    }
                    if (accent)
                    {
                        PutPixel(data, 7, 5, new Color(235, 198, 76, 255), 0.95f);
                        PutPixel(data, 6, 5, new Color(245, 226, 150, 255), 0.80f);
                        PutPixel(data, 8, 5, new Color(245, 226, 150, 255), 0.80f);
                    }
                    if (weathered)
                        DrawPixelLine(data, 3, 11, 11, 9,
                            new Color(52, 91, 43, 255), 0.65f);
                    break;
                case ScenerySurface.Sand:
                    DrawSoftPebble(data, new Color(104, 83, 57, 255), 8, 10, 5, 2, 0.34f);
                    DrawSoftPebble(data,
                        weathered ? new Color(116, 101, 79, 255)
                            : accent ? new Color(205, 190, 151, 255)
                                : new Color(154, 132, 94, 255),
                        7, 8, accent ? 3 : 4, 2, 0.78f);
                    PutPixel(data, 6, 7, new Color(232, 214, 168, 255), 0.55f);
                    if (weathered)
                        DrawPixelLine(data, 3, 11, 11, 10,
                            new Color(79, 70, 58, 255), 0.52f);
                    break;
                case ScenerySurface.Mine:
                    DrawSoftPebble(data, new Color(13, 15, 15, 255), 8, 11, 5, 2, 0.42f);
                    DrawSoftPebble(data, weathered ? new Color(47, 62, 64, 255)
                        : new Color(67, 69, 66, 255), 7, 8, 4, 3, 0.88f);
                    PutPixel(data, 6, 7,
                        accent ? new Color(202, 154, 72, 255) : new Color(151, 146, 131, 255),
                        0.95f);
                    PutPixel(data, 8, 8,
                        accent ? new Color(229, 199, 112, 255) : new Color(112, 119, 121, 255),
                        0.78f);
                    if (weathered)
                        PutPixel(data, 9, 6, new Color(164, 190, 188, 255), 0.58f);
                    break;
                case ScenerySurface.Dungeon:
                    DrawSoftPebble(data, new Color(11, 15, 13, 255), 8, 10, 6, 3, 0.36f);
                    DrawSoftPebble(data,
                        weathered ? new Color(37, 76, 53, 255)
                            : accent ? new Color(45, 82, 53, 255)
                                : new Color(35, 59, 41, 255),
                        7, 8, 5, 3, 0.72f);
                    PutPixel(data, 5, 7, new Color(72, 104, 67, 255), 0.55f);
                    PutPixel(data, 9, 9, new Color(88, 116, 76, 255), 0.50f);
                    if (weathered)
                        DrawPixelLine(data, 5, 6, 10, 11,
                            new Color(62, 103, 70, 255), 0.48f);
                    break;
                case ScenerySurface.Snow:
                    DrawSoftPebble(data, new Color(91, 121, 142, 255), 8, 11, 6, 2, 0.28f);
                    DrawSoftPebble(data, weathered ? new Color(190, 214, 228, 255)
                        : new Color(224, 234, 238, 255), 7, 9, 5, 3, 0.62f);
                    if (accent)
                    {
                        PutPixel(data, 7, 5, Color.White, 0.95f);
                        PutPixel(data, 6, 5, new Color(220, 240, 255, 255), 0.72f);
                        PutPixel(data, 8, 5, new Color(220, 240, 255, 255), 0.72f);
                        PutPixel(data, 7, 4, new Color(220, 240, 255, 255), 0.72f);
                    }
                    if (weathered)
                        DrawPixelLine(data, 2, 11, 12, 8,
                            new Color(218, 237, 246, 255), 0.70f);
                    break;
                default:
                    DrawSoftPebble(data, new Color(47, 31, 22, 255), 8, 10, 5, 2, 0.38f);
                    DrawSoftPebble(data,
                        weathered ? new Color(70, 58, 47, 255)
                            : accent ? new Color(133, 103, 67, 255)
                                : new Color(91, 72, 54, 255),
                        7, 8, 4, 2, 0.76f);
                    PutPixel(data, 6, 7, new Color(174, 142, 96, 255), 0.42f);
                    if (weathered)
                    {
                        DrawPixelLine(data, 4, 6, 10, 10,
                            new Color(54, 43, 35, 255), 0.62f);
                        DrawPixelLine(data, 5, 5, 8, 11,
                            new Color(83, 65, 48, 255), 0.52f);
                    }
                    break;
            }

            Texture2D texture = new Texture2D(
                Client.Game.GraphicsDevice, DETAIL_TEXTURE_SIZE, DETAIL_TEXTURE_SIZE);
            texture.SetData(data);
            return texture;
        }

        private static void DrawSoftPebble(
            Color[] data, Color color, int cx, int cy, int rx, int ry, float alpha)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (x - cx) / (float)Math.Max(1, rx);
                    float dy = (y - cy) / (float)Math.Max(1, ry);
                    float distance = dx * dx + dy * dy;
                    if (distance <= 1f)
                        PutPixel(data, x, y, color, alpha * (1f - distance * 0.45f));
                }
            }
        }

        private static void DrawPixelLine(
            Color[] data, int x0, int y0, int x1, int y1, Color color, float alpha)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                PutPixel(data, x0, y0, color, alpha);
                if (x0 == x1 && y0 == y1) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void PutPixel(
            Color[] data, int x, int y, Color color, float alpha)
        {
            if (x < 0 || y < 0 || x >= DETAIL_TEXTURE_SIZE || y >= DETAIL_TEXTURE_SIZE) return;
            Color value = Premultiply(color, alpha);
            int index = y * DETAIL_TEXTURE_SIZE + x;
            if (value.A >= data[index].A) data[index] = value;
        }

        private static TerrainEnvironment CalculateEnvironment(
            ScenerySurface surface,
            Weather weather)
            => CalculateEnvironment(
                surface,
                weather?.Type,
                weather?.IsActive == true,
                weather?.Fog == true,
                weather?.Tempest == true,
                weather?.Wetness ?? 0f,
                weather?.Coldness ?? 0f,
                weather?.SnowCover ?? 0f,
                weather?.VisualIntensity ?? 0f,
                EnvironmentalShadowManager.Daylight,
                EnvironmentalShadowManager.Dusk,
                EnvironmentalShadowManager.Night,
                EnvironmentalShadowManager.Moonlight);

        internal static TerrainEnvironment CalculateEnvironment(
            ScenerySurface surface,
            WeatherType? weatherType,
            bool active,
            bool fog,
            bool tempest,
            float wetness,
            float coldness,
            float snowCover,
            float weatherIntensity,
            float daylight,
            float dusk,
            float night,
            float moonlight)
        {
            wetness = Clamp01(wetness);
            coldness = Clamp01(coldness);
            snowCover = Clamp01(snowCover);
            weatherIntensity = Clamp01(weatherIntensity);
            daylight = Clamp01(daylight);
            dusk = Clamp01(dusk);
            night = Clamp01(night);
            moonlight = Clamp01(moonlight);

            bool storm = active && (weatherType == WeatherType.WT_STORM_APPROACH
                || weatherType == WeatherType.WT_STORM_BREWING || tempest);
            bool rain = active && (weatherType == WeatherType.WT_RAIN || storm);
            bool snowing = active && weatherType == WeatherType.WT_SNOW;
            float weather = active || fog ? weatherIntensity : 0f;
            float directLight = Math.Max(daylight, moonlight * 0.58f + dusk * 0.42f);

            TerrainEnvironment result = new TerrainEnvironment
            {
                Wetness = wetness,
                PatchDensity = 1f,
                ReliefStrength = 0.90f + directLight * 0.12f + dusk * 0.04f,
                DetailDensity = 1f,
                DetailOpacity = 0.84f + directLight * 0.16f,
                ShadowStrength = 0.30f + daylight * 0.70f + moonlight * 0.35f,
                SparkleStrength = 0.20f + daylight * 0.75f
                    + dusk * 0.35f + moonlight * 0.90f,
                WindParticles = 0.72f + weather * 0.38f,
                AirborneDust = 0.65f + (1f - wetness) * 0.35f
            };

            if (rain)
            {
                result.PatchDensity *= 1f + wetness * 0.20f;
                result.ReliefStrength *= 1f + wetness * 0.08f;
                result.SparkleStrength *= 1f + wetness * 0.35f;
                result.AirborneDust *= 0.30f;
            }
            if (storm)
            {
                result.PatchDensity *= 1f + weather * 0.12f;
                result.DetailDensity *= 1f - weather * 0.22f;
                result.DetailOpacity *= 1f - weather * 0.18f;
                result.ShadowStrength *= 1f - weather * 0.48f;
                result.WindParticles *= 1f + weather * 0.32f;
            }
            if (fog)
            {
                result.ReliefStrength *= 1f - weather * 0.10f;
                result.DetailOpacity *= 1f - weather * 0.22f;
                result.ShadowStrength *= 1f - weather * 0.58f;
                result.SparkleStrength *= 1f + weather * 0.18f;
                result.AirborneDust *= 1f - weather * 0.55f;
            }

            switch (surface)
            {
                case ScenerySurface.Sand:
                    result.PatchDensity *= wetness > 0.15f ? 0.88f : 1.12f;
                    result.WindParticles *= rain
                        ? 1.15f - weather * 0.87f : 1.15f;
                    break;
                case ScenerySurface.Grass:
                    result.PatchDensity *= 1f + wetness * 0.14f;
                    result.DetailDensity *= rain && !storm ? 1f + weather * 0.12f : 1f;
                    result.SparkleStrength *= fog ? 1f + weather * 0.20f : 1f;
                    break;
                case ScenerySurface.Mine:
                case ScenerySurface.Dungeon:
                    result.PatchDensity *= 1f + wetness * 0.22f;
                    result.SparkleStrength *= 1.12f + wetness * 0.25f;
                    result.AirborneDust *= 0.80f;
                    break;
                case ScenerySurface.Dirt:
                    result.PatchDensity *= 1f + wetness * 0.28f;
                    result.DetailDensity *= rain ? 1.08f - weather * 0.24f : 1.08f;
                    break;
                case ScenerySurface.Snow:
                    result.Wetness = Math.Max(result.Wetness, coldness * 0.35f);
                    result.PatchDensity *= 0.90f + snowCover * 0.35f;
                    result.DetailDensity *= snowing ? 1f + weather * 0.22f : 1f;
                    result.WindParticles *= snowing ? 0.72f + weather * 0.63f : 0.72f;
                    result.SparkleStrength *= 1.18f;
                    break;
            }

            result.PatchDensity = Clamp(result.PatchDensity, 0.45f, 1.45f);
            result.ReliefStrength = Clamp(result.ReliefStrength, 0.60f, 1.30f);
            result.DetailDensity = Clamp(result.DetailDensity, 0.45f, 1.35f);
            result.DetailOpacity = Clamp(result.DetailOpacity, 0.45f, 1.10f);
            result.ShadowStrength = Clamp(result.ShadowStrength, 0.15f, 1f);
            result.SparkleStrength = Clamp(result.SparkleStrength, 0.15f, 1.65f);
            result.WindParticles = Clamp(result.WindParticles, 0.15f, 1.60f);
            result.AirborneDust = Clamp(result.AirborneDust, 0.10f, 1f);
            return result;
        }

        internal static float CalculateOpacity(
            ScenerySurface surface,
            int quality,
            float wetness,
            float snowCover)
        {
            if (quality <= SceneryInteractionManager.QUALITY_LOW) return 0f;
            bool high = quality >= SceneryInteractionManager.QUALITY_HIGH;
            float wet = Math.Max(0f, Math.Min(1f, wetness));
            switch (surface)
            {
                case ScenerySurface.Sand: return high ? 0.62f : 0.42f;
                case ScenerySurface.Grass: return high ? 0.50f : 0.34f;
                case ScenerySurface.Mine: return (high ? 0.64f : 0.44f) + wet * 0.08f;
                case ScenerySurface.Dungeon: return (high ? 0.68f : 0.48f) + wet * 0.10f;
                case ScenerySurface.Dirt: return (high ? 0.56f : 0.38f) + wet * 0.12f;
                case ScenerySurface.Snow:
                    return (high ? 0.58f : 0.40f)
                        * (0.65f + Math.Max(0f, Math.Min(1f, snowCover)) * 0.35f);
                default: return 0f;
            }
        }

        internal static float ApplyIntensity(float opacity, int percent)
            => Math.Min(0.95f, Math.Max(0f, opacity) * Math.Max(0, Math.Min(200, percent)) / 100f);

        internal static int CalculateMotionOffset(
            ScenerySurface surface,
            float wind,
            bool reducedMotion,
            uint ticks)
        {
            if (reducedMotion || Math.Abs(wind) < 0.45f) return 0;
            if (surface == ScenerySurface.Sand)
                return (int)Math.Round(Math.Sin(ticks / 1100f) * Math.Min(2f, Math.Abs(wind) * 0.5f));
            if (surface == ScenerySurface.Grass)
                return (int)Math.Round(Math.Sin(ticks / 420f) * Math.Min(1f, Math.Abs(wind) * 0.35f));
            return 0;
        }

        private static Texture2D GetPatchTexture(ScenerySurface surface, int variant)
        {
            variant = Math.Max(0, Math.Min(PATCH_VARIANTS - 1, variant));
            int index = (int)surface * PATCH_VARIANTS + variant;
            Texture2D texture = _patchTextures[index];
            if (texture != null && !texture.IsDisposed) return texture;
            texture = BuildPatchTexture(surface, variant);
            _patchTextures[index] = texture;
            return texture;
        }

        private static Texture2D BuildPatchTexture(ScenerySurface surface, int variant)
        {
            Color[] data = new Color[PATCH_WIDTH * PATCH_HEIGHT];
            GetPatchColors(surface, variant, out Color shadow, out Color highlight);
            uint seed = (uint)surface * 0x9E3779B9u
                + (uint)variant * 0x85EBCA6Bu + 0xC2B2AE35u;
            float phase = (seed & 255u) / 255f * (float)Math.PI * 2f;

            for (int y = 0; y < PATCH_HEIGHT; y++)
            {
                float ny = (y - (PATCH_HEIGHT - 1) * 0.5f) / (PATCH_HEIGHT * 0.5f);
                for (int x = 0; x < PATCH_WIDTH; x++)
                {
                    float nx = (x - (PATCH_WIDTH - 1) * 0.5f) / (PATCH_WIDTH * 0.5f);
                    float broad = (float)Math.Sin(nx * 4.1f + ny * 2.3f + phase);
                    float cross = (float)Math.Sin(nx * -2.2f + ny * 5.4f - phase * 0.7f);
                    float fine = (float)Math.Sin(nx * 10.3f + ny * 8.7f + phase * 1.4f);
                    float irregular = broad * 0.10f + cross * 0.07f + fine * 0.025f;
                    float distance = nx * nx + ny * ny * 1.45f - irregular;
                    float coverage = Math.Max(0f, Math.Min(1f, (0.98f - distance) * 2.8f));
                    coverage = coverage * coverage * (3f - 2f * coverage);
                    if (coverage <= 0f) continue;

                    uint grain = Hash(x / 3, y / 3, seed);
                    if ((grain & 31u) < 3u) coverage *= 0.28f;

                    float relief;
                    float alpha;
                    if (surface == ScenerySurface.Sand || surface == ScenerySurface.Snow)
                    {
                        float ridge = (float)Math.Sin(
                            nx * 8.2f + ny * 3.1f + phase + broad * 0.55f);
                        relief = Math.Max(0f, Math.Min(1f,
                            0.50f + ridge * 0.32f - ny * 0.10f));
                        alpha = (0.16f + Math.Abs(ridge) * 0.20f) * coverage;
                    }
                    else
                    {
                        relief = Math.Max(0f, Math.Min(1f,
                            0.50f + broad * 0.22f - cross * 0.18f
                            - (nx + ny) * 0.07f));
                        alpha = (0.15f + Math.Abs(relief - 0.5f) * 0.34f) * coverage;
                    }

                    if (variant == 2) alpha *= 1.12f;
                    Color color = BlendColor(shadow, highlight, relief);
                    data[y * PATCH_WIDTH + x] = Premultiply(color, Math.Min(0.50f, alpha));
                }
            }

            Texture2D texture = new Texture2D(
                Client.Game.GraphicsDevice, PATCH_WIDTH, PATCH_HEIGHT);
            texture.SetData(data);
            return texture;
        }

        private static void GetPatchColors(
            ScenerySurface surface,
            int variant,
            out Color shadow,
            out Color highlight)
        {
            bool wet = variant == 2;
            switch (surface)
            {
                case ScenerySurface.Sand:
                    shadow = wet
                        ? new Color(101, 83, 62, 255) : new Color(137, 105, 67, 255);
                    highlight = wet
                        ? new Color(174, 151, 111, 255) : new Color(224, 198, 142, 255);
                    break;
                case ScenerySurface.Grass:
                    shadow = wet
                        ? new Color(22, 43, 30, 255) : new Color(31, 62, 30, 255);
                    highlight = wet
                        ? new Color(60, 92, 53, 255) : new Color(91, 124, 59, 255);
                    break;
                case ScenerySurface.Mine:
                    shadow = wet
                        ? new Color(19, 28, 30, 255) : new Color(35, 36, 36, 255);
                    highlight = wet
                        ? new Color(67, 76, 76, 255) : new Color(103, 98, 88, 255);
                    break;
                case ScenerySurface.Dungeon:
                    shadow = wet
                        ? new Color(15, 24, 24, 255) : new Color(27, 29, 29, 255);
                    highlight = wet
                        ? new Color(48, 69, 59, 255) : new Color(72, 75, 68, 255);
                    break;
                case ScenerySurface.Snow:
                    shadow = new Color(127, 158, 179, 255);
                    highlight = new Color(238, 245, 247, 255);
                    break;
                default:
                    shadow = wet
                        ? new Color(46, 36, 29, 255) : new Color(67, 47, 31, 255);
                    highlight = wet
                        ? new Color(96, 75, 57, 255) : new Color(137, 105, 68, 255);
                    break;
            }

            if (variant == 1 && !wet)
                highlight = BlendColor(highlight, Color.White, 0.12f);
        }

        private static Texture2D GetDetailShadowTexture()
        {
            if (_detailShadow != null && !_detailShadow.IsDisposed) return _detailShadow;
            const int width = 32;
            const int height = 12;
            Color[] data = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float dy = (y - (height - 1) * 0.5f) / (height * 0.5f);
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - (width - 1) * 0.5f) / (width * 0.5f);
                    float distance = dx * dx + dy * dy;
                    if (distance >= 1f) continue;
                    float alpha = 1f - distance;
                    data[y * width + x] = Premultiply(Color.Black, alpha * alpha * 0.58f);
                }
            }
            _detailShadow = new Texture2D(Client.Game.GraphicsDevice, width, height);
            _detailShadow.SetData(data);
            return _detailShadow;
        }

        private static Color BlendColor(Color a, Color b, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return new Color(
                (byte)(a.R + (b.R - a.R) * amount),
                (byte)(a.G + (b.G - a.G) * amount),
                (byte)(a.B + (b.B - a.B) * amount),
                255);
        }

        private static Color Premultiply(Color color, float alpha)
        {
            byte a = (byte)(Math.Max(0f, Math.Min(1f, alpha)) * 255f);
            return new Color(
                (byte)(color.R * a / 255),
                (byte)(color.G * a / 255),
                (byte)(color.B * a / 255),
                a
            );
        }

        private static uint Hash(int x, int y, uint seed)
        {
            uint value = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ seed;
            value ^= value >> 13;
            value *= 0x85EBCA6Bu;
            value ^= value >> 16;
            return value;
        }

        private static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static float Clamp(float value, float min, float max)
            => Math.Max(min, Math.Min(max, value));

        private static int FloorDivide(int value, int divisor)
        {
            int result = value / divisor;
            if (value % divisor < 0) result--;
            return result;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}

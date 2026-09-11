#region license
// TazUO addition. Material, shelter, light and depth-aware scenery interaction.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    public enum ScenerySurface : byte
    {
        None,
        Dirt,
        Grass,
        Stone,
        Sand,
        Mine,
        Dungeon,
        Snow,
        Water
    }

    internal static class SceneryInteractionManager
    {
        public const int QUALITY_LOW = 0;
        public const int QUALITY_MEDIUM = 1;
        public const int QUALITY_HIGH = 2;

        private const int MAX_PARTICLES = 160;
        private const int MAX_LIGHTS = 80;

        private enum ParticleKind : byte
        {
            Dust,
            Sand,
            MineralDust,
            Grass,
            Snow,
            Water,
            WetStone,
            Wake
        }

        private struct Particle
        {
            public int X, Y;
            public sbyte Z;
            public uint Born;
            public ushort Seed;
            public ParticleKind Kind;
            public bool Foreground;
        }

        private struct AtmosphericLight
        {
            public int X, Y;
            public Color Color;
            public float Radius;
            public float Strength;
            public bool Transient;
        }

        private static readonly Particle[] _particles = new Particle[MAX_PARTICLES];
        private static readonly AtmosphericLight[] _lights = new AtmosphericLight[MAX_LIGHTS];
        private static readonly Dictionary<uint, uint> _nextWake = new Dictionary<uint, uint>();
        private static int _particleIndex;
        private static int _lightCount;
        private static uint _nextShelterCheck;
        private static uint _nextWakeCheck;
        private static uint _lastUpdate;
        private static float _shelter;
        private static int _openSide = -1;
        private static Texture2D _softDark;
        private static Texture2D _softLight;
        private static Texture2D _softBlue;
        private static Texture2D _softGreen;
        private static Texture2D _softPale;

        private static Profile Profile => ProfileManager.CurrentProfile;
        public static int Quality
        {
            get
            {
                int quality = Profile?.SceneryQuality ?? QUALITY_HIGH;
                return quality < QUALITY_LOW ? QUALITY_LOW : quality > QUALITY_HIGH ? QUALITY_HIGH : quality;
            }
        }
        public static float Density => Quality == QUALITY_LOW ? 0.45f : Quality == QUALITY_MEDIUM ? 0.72f : 1f;
        public static float Shelter => _shelter;
        public static float WeatherSoundScale => 1f - _shelter * 0.62f;
        public static float AcousticEcho => Math.Min(0.65f,
            _shelter * 0.42f
            + (World.MapIndex <= 1 && World.Player != null && World.Player.X >= 5120 ? 0.20f
                : AmbienceOverlay.ForestLike ? 0.10f : AmbienceOverlay.TownLike ? 0.04f : 0f));
        public static float SharedWind
        {
            get
            {
                Weather weather = Client.Game.GetScene<GameScene>()?.Weather;
                if (weather?.IsActive == true) return weather.Wind;
                return 0.65f + 0.45f * (float)Math.Sin(Time.Ticks / 4200f);
            }
        }

        public static int ScaleCount(int count)
            => Math.Max(1, (int)Math.Round(count * Density));

        public static void ResetSession()
        {
            _particleIndex = 0;
            _lightCount = 0;
            _nextShelterCheck = _nextWakeCheck = _lastUpdate = 0;
            _shelter = 0f;
            _openSide = -1;
            _nextWake.Clear();
            for (int i = 0; i < _particles.Length; i++) _particles[i].Born = 0;
        }

        public static void Update()
        {
            if (!AmbienceOverlay.Enabled || !World.InGame || World.Player == null)
            {
                return;
            }

            uint now = Time.Ticks;
            uint elapsed = _lastUpdate == 0 ? 16u : now - _lastUpdate;
            _lastUpdate = now;
            if (elapsed > 250) elapsed = 250;

            if (now >= _nextShelterCheck)
            {
                _nextShelterCheck = now + (uint)(Quality == QUALITY_LOW ? 900 : 450);
                UpdateShelterTarget(out float target, out _openSide);
                float blend = 1f - (float)Math.Exp(-elapsed / 500f);
                _shelter += (target - _shelter) * Math.Max(0.20f, blend);
            }

            if (now >= _nextWakeCheck)
            {
                _nextWakeCheck = now + (uint)(Quality == QUALITY_LOW ? 450 : 250);
                int checkedMobiles = 0;
                foreach (Mobile mobile in MobileCache.All)
                {
                    if (mobile == null || mobile.IsDestroyed || mobile.IsDead || mobile.Distance > 12) continue;
                    if (checkedMobiles++ >= ScaleCount(28)) break;
                    if (ClassifySurface(mobile.X, mobile.Y, mobile.Z) != ScenerySurface.Water) continue;
                    if (_nextWake.TryGetValue(mobile.Serial, out uint next) && now < next) continue;
                    _nextWake[mobile.Serial] = now + (uint)(mobile.IsWalking ? 450 : 1800);
                    Spawn(mobile.X, mobile.Y, mobile.Z, ParticleKind.Wake, false, 1);
                }

                if (_nextWake.Count > 180) _nextWake.Clear();
            }
        }

        public static void OnPlayerMoved(int x, int y, sbyte z, bool running)
        {
            if (!AmbienceOverlay.Enabled || World.Player == null) return;

            ScenerySurface surface = ClassifySurface(x, y, z);
            Weather weather = Client.Game.GetScene<GameScene>()?.Weather;
            ParticleKind kind;
            switch (surface)
            {
                case ScenerySurface.Water: kind = ParticleKind.Water; break;
                case ScenerySurface.Snow: kind = ParticleKind.Snow; break;
                case ScenerySurface.Grass: kind = ParticleKind.Grass; break;
                case ScenerySurface.Sand: kind = ParticleKind.Sand; break;
                case ScenerySurface.Mine:
                case ScenerySurface.Dungeon:
                case ScenerySurface.Stone:
                    kind = weather?.Wetness > 0.12f ? ParticleKind.WetStone : ParticleKind.MineralDust;
                    break;
                default: kind = ParticleKind.Dust; break;
            }

            int count = ScaleCount(running ? 4 : 2);
            Spawn(x, y, z, kind, false, count);
        }

        private static void Spawn(int x, int y, sbyte z, ParticleKind kind, bool foreground, int count)
        {
            for (int i = 0; i < count; i++)
            {
                ref Particle particle = ref _particles[_particleIndex++ % MAX_PARTICLES];
                particle.X = x;
                particle.Y = y;
                particle.Z = z;
                particle.Born = Time.Ticks;
                particle.Kind = kind;
                particle.Foreground = foreground || i % 3 == 0;
                particle.Seed = (ushort)RandomHelper.GetValue(0, ushort.MaxValue);
            }
        }

        public static ScenerySurface ClassifySurface(int x, int y, sbyte z)
        {
            Weather weather = Client.Game.GetScene<GameScene>()?.Weather;
            if (weather?.SnowCover > 0.18f && !IsTileSheltered(x, y, z) && !IsNearLight(x, y, 2))
                return ScenerySurface.Snow;

            ScenerySurface landSurface = ScenerySurface.Dirt;
            try
            {
                for (GameObject obj = World.Map?.GetTile(x, y, false); obj != null; obj = obj.TNext)
                {
                    if (obj is Land land)
                    {
                        if (land.TileData.IsWet) return ScenerySurface.Water;
                        landSurface = ClassifyLandMaterial(
                            land.OriginalGraphic,
                            land.TileData.Name,
                            IsDungeonArea,
                            0f
                        );
                    }
                    else if (obj is Static staticObject && Math.Abs(staticObject.Z - z) <= 5)
                    {
                        if (staticObject.ItemData.IsWet) return ScenerySurface.Water;
                        if (staticObject.ItemData.IsSurface || staticObject.ItemData.IsBridge)
                            return ClassifyStaticMaterial(staticObject.ItemData.Name, IsDungeonArea);
                    }
                    else if (obj is Item item && Math.Abs(item.Z - z) <= 5)
                    {
                        if (item.ItemData.IsWet) return ScenerySurface.Water;
                        if (item.ItemData.IsSurface || item.ItemData.IsBridge)
                            return ClassifyStaticMaterial(item.ItemData.Name, IsDungeonArea);
                    }
                }
            }
            catch { }

            return landSurface;
        }

        internal static bool IsDungeonArea =>
            World.MapIndex <= 1 && World.Player != null && World.Player.X >= 5120;

        internal static ScenerySurface ClassifyLandMaterial(
            ushort graphic,
            string tileName,
            bool dungeon,
            float snowCover)
        {
            string name = tileName?.ToLowerInvariant() ?? string.Empty;
            if (snowCover > 0.18f || name.Contains("snow") || name.Contains("ice"))
                return ScenerySurface.Snow;
            if (IsSandGraphic(graphic) || name.Contains("sand") || name.Contains("desert")
                || name.Contains("dune"))
                return ScenerySurface.Sand;
            if (IsGrassGraphic(graphic) || name.Contains("grass") || name.Contains("meadow") || name.Contains("jungle")
                || name.Contains("forest") || name.Contains("moss"))
                return ScenerySurface.Grass;
            if (name.Contains("mine") || name.Contains("ore") || name.Contains("cave"))
                return ScenerySurface.Mine;
            if (dungeon) return ScenerySurface.Dungeon;
            if (name.Contains("rock") || name.Contains("stone") || name.Contains("brick")
                || name.Contains("marble") || name.Contains("cobble"))
                return ScenerySurface.Stone;
            return ScenerySurface.Dirt;
        }

        internal static ScenerySurface ClassifyStaticMaterial(string tileName, bool dungeon)
        {
            string name = tileName?.ToLowerInvariant() ?? string.Empty;
            if (name.Contains("mine") || name.Contains("ore") || name.Contains("cave"))
                return ScenerySurface.Mine;
            if (dungeon) return ScenerySurface.Dungeon;
            if (name.Contains("sand")) return ScenerySurface.Sand;
            if (name.Contains("grass") || name.Contains("moss")) return ScenerySurface.Grass;
            if (name.Contains("dirt") || name.Contains("soil") || name.Contains("mud")
                || name.Contains("earth")) return ScenerySurface.Dirt;
            return ScenerySurface.Stone;
        }

        internal static bool IsSandGraphic(ushort graphic)
            => graphic >= 0x0016 && graphic <= 0x0019
                || graphic >= 0x0022 && graphic <= 0x003A
                || graphic >= 0x011E && graphic <= 0x0126;

        internal static bool IsGrassGraphic(ushort graphic)
            => graphic == 0x0005;

        public static bool IsTileSheltered(int x, int y, sbyte z)
        {
            try
            {
                for (GameObject obj = World.Map?.GetTile(x, y, false); obj != null; obj = obj.TNext)
                {
                    if (obj.Z <= z + 6) continue;
                    if (obj is Static staticObject && (staticObject.ItemData.IsRoof || staticObject.ItemData.IsSurface)) return true;
                    if (obj is Multi multi && (multi.ItemData.IsRoof || multi.ItemData.IsSurface)) return true;
                    if (obj is Item item && (item.ItemData.IsRoof || item.ItemData.IsSurface)) return true;
                }
            }
            catch { }
            return World.MapIndex <= 1 && x >= 5120;
        }

        public static bool IsNearLight(int x, int y, int range)
        {
            try
            {
                for (int yy = y - range; yy <= y + range; yy++)
                {
                    for (int xx = x - range; xx <= x + range; xx++)
                    {
                        for (GameObject obj = World.Map?.GetTile(xx, yy, false); obj != null; obj = obj.TNext)
                        {
                            if (obj is Static s && s.ItemData.IsLight
                                || obj is Multi m && m.ItemData.IsLight
                                || obj is Item i && i.ItemData.IsLight) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static void UpdateShelterTarget(out float target, out int openSide)
        {
            int x = World.Player.X, y = World.Player.Y;
            sbyte z = World.Player.Z;
            bool center = IsTileSheltered(x, y, z);
            int open = 0;
            int bestDistance = int.MaxValue;
            openSide = -1;
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { -1, 0, 1, 0 };
            for (int side = 0; side < 4; side++)
            {
                for (int distance = 1; distance <= 6; distance++)
                {
                    if (!IsTileSheltered(x + dx[side] * distance, y + dy[side] * distance, z))
                    {
                        open++;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            openSide = side;
                        }
                        break;
                    }
                }
            }
            target = center ? Math.Max(0.55f, 1f - open * 0.10f) : 0f;
        }

        public static bool IsWeatherVisibleAt(float x, float y, int width, int height, uint seed)
        {
            if (_shelter < 0.05f) return true;
            uint hash = seed * 1103515245u + 12345u;
            if (hash % 1000 < (uint)((1f - _shelter) * 1000f)) return true;
            if (_openSide < 0) return false;

            float edge = 0.30f + 0.10f * (1f - _shelter);
            switch (_openSide)
            {
                case 0: return y < height * edge;
                case 1: return x > width * (1f - edge);
                case 2: return y > height * (1f - edge);
                default: return x < width * edge;
            }
        }

        public static int GetFoliageSway(int x, int y)
        {
            if (Quality == QUALITY_LOW || Profile?.ReduceWeatherMotion == true) return 0;
            float wind = SharedWind;
            if (Math.Abs(wind) < 0.5f) return 0;
            return (int)Math.Round(Math.Sin(Time.Ticks / 280f + x * 0.37f + y * 0.21f) * Math.Min(2f, Math.Abs(wind) * 0.45f));
        }

        public static void BeginLightFrame()
        {
            _lightCount = 0;
        }

        public static void RecordLight(int x, int y, ushort graphic, ushort hue)
        {
            if (_lightCount >= ScaleCount(MAX_LIGHTS)) return;
            ref AtmosphericLight light = ref _lights[_lightCount++];
            light.X = x;
            light.Y = y;
            light.Radius = graphic >= 0x36B0 && graphic <= 0x37AF ? 125f : 90f;
            light.Strength = 1f;
            light.Transient = false;
            if (hue > 0)
            {
                light.Color = new Color { PackedValue = HuesLoader.Instance.GetHueColorRgba8888(31, (ushort)(hue - 1)) };
            }
            else if (graphic >= 0x36B0 && graphic <= 0x37AF)
            {
                light.Color = new Color(105, 155, 255, 255);
            }
            else
            {
                light.Color = new Color(255, 158, 62, 255);
            }
        }

        public static void RecordTransientLight(
            int x,
            int y,
            Color color,
            float radius,
            float strength)
        {
            if (_lightCount >= ScaleCount(MAX_LIGHTS) || strength <= 0f) return;

            ref AtmosphericLight light = ref _lights[_lightCount++];
            light.X = x;
            light.Y = y;
            light.Color = color;
            light.Radius = Math.Max(16f, radius);
            light.Strength = Math.Min(1f, strength);
            light.Transient = true;
        }

        public static float GetLightInfluence(float x, float y, out Color color)
        {
            float best = 0f;
            color = Color.White;
            for (int i = 0; i < _lightCount; i++)
            {
                float dx = x - _lights[i].X;
                float dy = y - _lights[i].Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float influence = (1f - distance / _lights[i].Radius) * _lights[i].Strength;
                if (influence > best)
                {
                    best = influence;
                    color = _lights[i].Color;
                }
            }
            return Math.Max(0f, best);
        }

        public static bool DrawColoredLights(UltimaBatcher2D batcher, Rectangle bounds, float strength)
        {
            if (_lightCount == 0) return false;
            bool drew = false;
            for (int i = 0; i < _lightCount; i++)
            {
                float lightStrength = _lights[i].Transient
                    ? _lights[i].Strength
                    : strength * _lights[i].Strength;
                if (lightStrength < 0.02f) continue;

                Texture2D glow = GetColoredSoftLight(_lights[i].Color);
                int size = (int)(_lights[i].Radius * 1.15f);
                if (_lights[i].X < bounds.X - size || _lights[i].X > bounds.Right + size
                    || _lights[i].Y < bounds.Y - size || _lights[i].Y > bounds.Bottom + size) continue;
                drew = true;
                batcher.Draw(SolidColorTextureCache.GetTexture(_lights[i].Color),
                    new Rectangle(_lights[i].X - 2, _lights[i].Y - 2, 4, 4),
                    ShaderHueTranslator.GetHueVector(
                        0,
                        false,
                        0.20f * lightStrength));
                batcher.Draw(glow,
                    new Rectangle(_lights[i].X - size / 2, _lights[i].Y - size / 3, size, size * 2 / 3),
                    ShaderHueTranslator.GetHueVector(
                        0,
                        false,
                        0.18f * lightStrength));
            }
            return drew;
        }

        internal static void DrawTerrainLightResponse(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            float wetness,
            float materialScale)
        {
            float strength = Math.Max(0f, Math.Min(1f, wetness)) * materialScale;
            if (_lightCount == 0 || strength < 0.02f) return;
            for (int i = 0; i < _lightCount; i++)
            {
                int size = (int)(_lights[i].Radius * 0.82f);
                if (_lights[i].X < bounds.X - size || _lights[i].X > bounds.Right + size
                    || _lights[i].Y < bounds.Y - size || _lights[i].Y > bounds.Bottom + size)
                    continue;
                Texture2D glow = GetColoredSoftLight(_lights[i].Color);
                batcher.Draw(
                    glow,
                    new Rectangle(
                        _lights[i].X - size / 2,
                        _lights[i].Y - size / 4,
                        size,
                        size / 2),
                    ShaderHueTranslator.GetHueVector(
                        0,
                        false,
                        0.065f * strength * _lights[i].Strength)
                );
            }
        }

        public static void DrawBackground(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!AmbienceOverlay.Enabled || World.Player == null) return;
            DrawContactShadows(batcher, bounds);
            DrawParticles(batcher, bounds, false);
            DrawCanopySunbeams(batcher, bounds);
        }

        public static void DrawForeground(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!AmbienceOverlay.Enabled || World.Player == null) return;
            DrawParticles(batcher, bounds, true);
            DrawRareAtmosphere(batcher, bounds);
        }

        private static void DrawContactShadows(UltimaBatcher2D batcher, Rectangle bounds)
        {
            float alpha = 0.16f * EnvironmentalShadowManager.AmbienceStrength;
            Texture2D shadow = GetSoftDark();
            int limit = ScaleCount(45), count = 0;
            foreach (Mobile mobile in MobileCache.All)
            {
                if (mobile == null || mobile.IsDestroyed || mobile.IsDead || mobile.IsHidden || mobile.Distance > 18) continue;
                if (count++ >= limit) break;
                Point p = PathPreview.TileToScreen(mobile.X, mobile.Y, mobile.Z);
                if (!bounds.Contains(p)) continue;
                int width = mobile == World.Player ? 32 : 26;
                batcher.Draw(shadow, new Rectangle(p.X - width / 2, p.Y - 5, width, 10),
                    ShaderHueTranslator.GetHueVector(0, false, alpha * mobile.AlphaHue / 255f));
            }

            int itemLimit = ScaleCount(24), items = 0;
            foreach (Item item in MobileCache.GroundItems)
            {
                if (item == null || item.IsDestroyed || !item.OnGround || item.IsCorpse || item.Distance > 14
                    || item.ItemData.IsWet || item.ItemData.IsBackground) continue;
                if (items++ >= itemLimit) break;
                Point p = PathPreview.TileToScreen(item.X, item.Y, item.Z);
                if (!bounds.Contains(p)) continue;
                batcher.Draw(shadow, new Rectangle(p.X - 9, p.Y - 3, 18, 6),
                    ShaderHueTranslator.GetHueVector(0, false, alpha * 0.65f));
            }
        }

        private static void DrawParticles(UltimaBatcher2D batcher, Rectangle bounds, bool foreground)
        {
            uint now = Time.Ticks;
            for (int i = 0; i < _particles.Length; i++)
            {
                ref Particle particle = ref _particles[i];
                if (particle.Born == 0 || particle.Foreground != foreground) continue;
                uint life = particle.Kind == ParticleKind.Wake ? 1300u : 900u;
                uint age = now - particle.Born;
                if (age >= life) { particle.Born = 0; continue; }
                float t = age / (float)life;
                Point p = PathPreview.TileToScreen(particle.X, particle.Y, particle.Z);
                float alpha = (1f - t) * (foreground ? 0.55f : 0.32f);
                int spread = (int)(t * (6 + particle.Seed % 7));
                int drift = (int)(Math.Sin(particle.Seed + t * 4f) * 5f * t);
                if (particle.Kind == ParticleKind.Sand || particle.Kind == ParticleKind.Snow)
                    drift += (int)(SharedWind * t * 3f);

                Color color;
                switch (particle.Kind)
                {
                    case ParticleKind.Sand: color = new Color(205, 174, 112, 255); break;
                    case ParticleKind.MineralDust: color = new Color(125, 118, 108, 255); break;
                    case ParticleKind.Grass: color = new Color(92, 138, 62, 255); break;
                    case ParticleKind.Snow: color = new Color(230, 242, 255, 255); break;
                    case ParticleKind.Water:
                    case ParticleKind.Wake:
                    case ParticleKind.WetStone: color = new Color(145, 185, 220, 255); break;
                    default: color = new Color(155, 125, 88, 255); break;
                }
                Texture2D texture = SolidColorTextureCache.GetTexture(color);

                if (particle.Kind == ParticleKind.Water || particle.Kind == ParticleKind.Wake)
                {
                    int radius = 3 + spread;
                    Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.65f);
                    batcher.DrawLine(texture, new Vector2(p.X - radius, p.Y), new Vector2(p.X + radius, p.Y), hue, 1);
                    batcher.DrawLine(texture, new Vector2(p.X - radius / 2, p.Y + 2), new Vector2(p.X + radius / 2, p.Y + 2), hue, 1);
                }
                else if (particle.Kind == ParticleKind.WetStone)
                {
                    batcher.Draw(texture, new Rectangle(p.X - spread / 2, p.Y, 3 + spread, 1),
                        ShaderHueTranslator.GetHueVector(0, false, alpha * 0.55f));
                }
                else
                {
                    int py = p.Y - (int)(t * (particle.Kind == ParticleKind.Snow ? 12 : 8));
                    batcher.Draw(texture, new Rectangle(p.X + drift, py, particle.Kind == ParticleKind.Grass ? 2 : 3, 2),
                        ShaderHueTranslator.GetHueVector(0, false, alpha));
                }
            }
        }

        private static void DrawCanopySunbeams(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!AmbienceOverlay.ForestLike || EnvironmentalShadowManager.Daylight < 0.35f
                || EnvironmentalShadowManager.IsIndoors) return;
            Weather weather = Client.Game.GetScene<GameScene>()?.Weather;
            if (weather?.IsActive == true || weather?.Fog == true) return;

            Texture2D light = SolidColorTextureCache.GetTexture(new Color(255, 228, 166, 255));
            int beams = ScaleCount(4);
            for (int i = 0; i < beams; i++)
            {
                int x = bounds.X + bounds.Width * (18 + i * 23) / 100;
                int y = bounds.Y + bounds.Height / 10;
                float pulse = 0.55f + 0.45f * (float)Math.Sin(Time.Ticks / 2100f + i * 1.7f);
                batcher.DrawLine(light, new Vector2(x, y), new Vector2(x - 70, bounds.Y + bounds.Height * 3 / 4),
                    ShaderHueTranslator.GetHueVector(0, false,
                        0.035f * pulse * EnvironmentalShadowManager.Daylight), 18);
                for (int mote = 0; mote < 3; mote++)
                {
                    float t = (Time.Ticks * 0.00003f + mote * 0.31f + i * 0.17f) % 1f;
                    int mx = x - (int)(70 * t) + mote * 5;
                    int my = y + (int)(bounds.Height * 0.65f * t);
                    batcher.Draw(light, new Rectangle(mx, my, 1, 1),
                        ShaderHueTranslator.GetHueVector(0, false, 0.20f * pulse));
                }
            }
        }

        private static void DrawRareAtmosphere(UltimaBatcher2D batcher, Rectangle bounds)
        {
            Weather weather = Client.Game.GetScene<GameScene>()?.Weather;
            bool clear = weather == null || !weather.IsActive && !weather.Fog;
            float night = EnvironmentalShadowManager.Night;
            float day = EnvironmentalShadowManager.Daylight;
            if (clear && night > 0.55f)
            {
                float starCycle = Time.Ticks % 93000 / 1000f;
                if (starCycle < 1.2f)
                {
                    float t = starCycle / 1.2f;
                    int sx = bounds.X + bounds.Width / 5 + (int)(bounds.Width * 0.55f * t);
                    int sy = bounds.Y + 45 + (int)(bounds.Height * 0.18f * t);
                    Texture2D star = SolidColorTextureCache.GetTexture(new Color(225, 235, 255, 255));
                    batcher.DrawLine(star, new Vector2(sx - 30, sy - 14), new Vector2(sx, sy),
                        ShaderHueTranslator.GetHueVector(0, false, 0.65f * (1f - t)), 2);
                }

                float meteorCycle = Time.Ticks % 241000 / 1000f;
                if (Quality == QUALITY_HIGH && meteorCycle < 1.6f)
                {
                    float t = meteorCycle / 1.6f;
                    int mx = bounds.Right - 80 - (int)(bounds.Width * 0.65f * t);
                    int my = bounds.Y + 30 + (int)(bounds.Height * 0.32f * t);
                    Texture2D meteor = SolidColorTextureCache.GetTexture(new Color(255, 184, 96, 255));
                    batcher.DrawLine(meteor, new Vector2(mx + 55, my - 25), new Vector2(mx, my),
                        ShaderHueTranslator.GetHueVector(0, false, 0.75f * (1f - t)), 3);
                }

                if (World.Season == Season.Winter)
                {
                    float auroraCycle = Time.Ticks % 157000 / 1000f;
                    if (auroraCycle < 18f)
                    {
                        float fade = Math.Min(1f, Math.Min(auroraCycle, 18f - auroraCycle) / 3f);
                        Color[] colors = { new Color(80, 220, 170, 255), new Color(90, 145, 235, 255), new Color(175, 95, 225, 255) };
                        for (int band = 0; band < ScaleCount(3); band++)
                        {
                            Texture2D aurora = SolidColorTextureCache.GetTexture(colors[band]);
                            Vector2 previous = new Vector2(bounds.X, bounds.Y + 45 + band * 18);
                            for (int segment = 1; segment <= 12; segment++)
                            {
                                float xx = bounds.X + bounds.Width * segment / 12f;
                                float yy = bounds.Y + 45 + band * 18
                                    + 15 * (float)Math.Sin(Time.Ticks / 1600f + segment * 0.7f + band);
                                Vector2 next = new Vector2(xx, yy);
                                batcher.DrawLine(aurora, previous, next,
                                    ShaderHueTranslator.GetHueVector(0, false, 0.055f * fade * night), 9);
                                previous = next;
                            }
                        }
                    }
                }
            }

            bool dawn = DayCyclePreviewManager.IsDawn || EnvironmentShowcaseManager.IsDawn
                || EnvironmentControlManager.IsDawn;
            if (clear && dawn && day > 0.20f)
            {
                Texture2D dew = SolidColorTextureCache.GetTexture(new Color(225, 245, 255, 255));
                for (int i = 0; i < ScaleCount(18); i++)
                {
                    int x = bounds.X + (i * 431 + World.Player.X * 17) % Math.Max(1, bounds.Width);
                    int y = bounds.Y + bounds.Height / 2 + (i * 197) % Math.Max(1, bounds.Height / 2);
                    float sparkle = 0.5f + 0.5f * (float)Math.Sin(Time.Ticks / 150f + i * 2.4f);
                    batcher.Draw(dew, new Rectangle(x, y, 1, 1),
                        ShaderHueTranslator.GetHueVector(0, false, 0.42f * sparkle));
                }
            }
        }

        private static Texture2D GetSoftDark()
        {
            if (_softDark == null || _softDark.IsDisposed) _softDark = BuildBlob(new Color(8, 10, 14, 255));
            return _softDark;
        }

        private static Texture2D GetSoftLight()
        {
            if (_softLight == null || _softLight.IsDisposed) _softLight = BuildBlob(new Color(255, 176, 76, 255));
            return _softLight;
        }

        private static Texture2D GetColoredSoftLight(Color color)
        {
            if (color.G > color.R * 1.12f && color.G > color.B * 1.05f)
            {
                if (_softGreen == null || _softGreen.IsDisposed)
                    _softGreen = BuildBlob(new Color(105, 235, 135, 255));
                return _softGreen;
            }
            if (color.B > color.R * 1.08f)
            {
                if (_softBlue == null || _softBlue.IsDisposed)
                    _softBlue = BuildBlob(new Color(105, 155, 255, 255));
                return _softBlue;
            }
            if (Math.Abs(color.R - color.B) < 35 && Math.Abs(color.R - color.G) < 35)
            {
                if (_softPale == null || _softPale.IsDisposed)
                    _softPale = BuildBlob(new Color(240, 225, 190, 255));
                return _softPale;
            }
            return GetSoftLight();
        }

        private static Texture2D BuildBlob(Color color)
        {
            const int size = 64;
            var data = new Color[size * size];
            const float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = distance >= 1f ? 0f : (1f - distance) * (1f - distance);
                    byte value = (byte)(alpha * 255);
                    data[y * size + x] = new Color(
                        (byte)(color.R * value / 255),
                        (byte)(color.G * value / 255),
                        (byte)(color.B * value / 255),
                        value);
                }
            }
            var texture = new Texture2D(Client.Game.GraphicsDevice, size, size);
            texture.SetData(data);
            return texture;
        }
    }
}

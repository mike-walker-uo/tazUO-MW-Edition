#region license
// TazUO addition. Ambient world-depth effects: stars + moon, fireflies,
// falling leaves, footprints, blood pools, breath
// vapor, biome-aware particles, cloud shadows, shoreline mist, warm lights,
// water shimmer, sparse wildlife/sound cues, desert dust and dungeon motes.
// All stateless or ring-buffered; `-ambience on|off`.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    public static class AmbienceOverlay
    {
        public static bool Enabled = true;
        public static bool LightsEnabled { get; private set; } = true;

        public static void SetLightsEnabled(bool enabled)
        {
            LightsEnabled = enabled;
        }

        // --- footprints -------------------------------------------------
        private static int _lastTileX = int.MinValue, _lastTileY;

        // --- blood pools --------------------------------------------------
        private static readonly System.Collections.Generic.HashSet<uint> _knownDead
            = new System.Collections.Generic.HashSet<uint>();

        // --- breath vapor -------------------------------------------------
        private struct Puff { public float SX, SY; public uint Born; }
        private static readonly Puff[] _puffs = new Puff[10];
        private static int _puffIdx;
        private static uint _nextPuff;
        private const uint PUFF_LIFE_MS = 900;

        // --- desert detection (cached per tile) ---------------------------
        private static bool _onSand;

        private enum AmbientBiome : byte
        {
            Plains,
            Forest,
            Coast,
            Swamp,
            Desert,
            Town,
            Dungeon
        }

        private struct AmbientPoint
        {
            public int X, Y;
            public sbyte Z;
        }

        private static readonly AmbientPoint[] _waterPoints = new AmbientPoint[10];
        private static readonly AmbientPoint[] _lightPoints = new AmbientPoint[10];
        private static readonly AmbientPoint[] _windowPoints = new AmbientPoint[10];
        private static int _waterPointCount, _lightPointCount, _windowPointCount;
        private static AmbientBiome _biome;
        private static bool _nearWater;
        private static uint _nextEnvironmentScan;
        private static uint _nextAmbientSound;

        // Dark radial blob for the dungeon vignette (fog blob is white).
        private static Texture2D _darkBlob;
        private static Texture2D _warmBlob;

        private static GameScene Scene => Client.Game.GetScene<GameScene>();
        internal static bool ForestLike => _biome == AmbientBiome.Forest || _biome == AmbientBiome.Swamp;
        internal static bool TownLike => _biome == AmbientBiome.Town;

        public static void Configure(bool enabled)
        {
            Enabled = enabled;
            LightsEnabled = true;
            _lastTileX = int.MinValue;
            _lastTileY = 0;
            _knownDead.Clear();
            _puffIdx = 0;
            _nextPuff = 0;
            _onSand = false;
            _nearWater = false;
            _biome = AmbientBiome.Plains;
            _waterPointCount = _lightPointCount = _windowPointCount = 0;
            _nextEnvironmentScan = _nextAmbientSound = 0;
            for (int i = 0; i < _puffs.Length; i++)
            {
                _puffs[i].Born = 0;
            }
        }

        private static bool InDungeon =>
            World.MapIndex <= 1 && World.Player != null && World.Player.X >= 5120;

        public static void Tick()
        {
            if (!Enabled || !World.InGame || World.Player == null) return;
            var scene = Scene;
            if (scene == null) return;

            var w = scene.Weather;
            bool snowing = w.IsActive && w.Type == WeatherType.WT_SNOW;
            bool coldAir = snowing || w.Coldness > 0.25f
                || World.Season == Season.Winter && EnvironmentalShadowManager.Night > 0.35f;

            ScanEnvironment();
            UpdateAmbientSound(w);

            // Keep movement-sensitive biome state current. Footstep decals are
            // handled independently by SceneryInteractionManager.
            int px = World.Player.X, py = World.Player.Y;
            if (px != _lastTileX || py != _lastTileY)
            {
                _lastTileX = px;
                _lastTileY = py;
                _onSand = IsSandTile(px, py);
            }

            // Blood pools: decal under freshly dead mobiles.
            if (ProfileManager.CurrentProfile.BloodDecalsEnabled)
            {
                foreach (var m in MobileCache.All)
                {
                    if (m == null || m.IsDestroyed || !m.IsDead) continue;
                    if (m == World.Player) continue;
                    if (m.Distance > 18) continue;
                    if (!_knownDead.Add(m.Serial)) continue;
                    scene.GroundDecals.AddAtTile(
                        m.X,
                        m.Y,
                        m.Z,
                        (byte)RandomHelper.GetValue(3, 5),
                        GroundDecalKind.Blood
                    );
                }
            }
            if (_knownDead.Count > 300) _knownDead.Clear();

            // Breath vapor: cold weather only. Player + up to 3 nearby mobiles.
            if (coldAir && Time.Ticks >= _nextPuff)
            {
                _nextPuff = Time.Ticks + (uint)RandomHelper.GetValue(2500, 4000);
                SpawnPuff(World.Player);
                int extra = 0;
                foreach (var m in MobileCache.All)
                {
                    if (extra >= 3) break;
                    if (m == null || m.IsDestroyed || m.IsDead || m == World.Player) continue;
                    if (m.Distance > 10) continue;
                    SpawnPuff(m);
                    extra++;
                }
            }
        }

        private static void ScanEnvironment()
        {
            if (Time.Ticks < _nextEnvironmentScan || World.Map == null || World.Player == null)
            {
                return;
            }

            _nextEnvironmentScan = Time.Ticks + 1000;
            _waterPointCount = _lightPointCount = _windowPointCount = 0;
            int water = 0, vegetation = 0, built = 0, sand = 0;
            int px = World.Player.X, py = World.Player.Y;

            try
            {
                for (int ty = py - 6; ty <= py + 6; ty++)
                {
                    for (int tx = px - 6; tx <= px + 6; tx++)
                    {
                        for (GameObject obj = World.Map.GetTile(tx, ty, false); obj != null; obj = obj.TNext)
                        {
                            if (obj is Land land)
                            {
                                if (land.TileData.IsWet)
                                {
                                    water++;
                                    AddAmbientPoint(_waterPoints, ref _waterPointCount, tx, ty, land.Z);
                                }
                                if (IsSandGraphic(land.OriginalGraphic)) sand++;
                                continue;
                            }

                            if (obj is Static staticObject)
                            {
                                var data = staticObject.ItemData;
                                if (data.IsWet)
                                {
                                    water++;
                                    AddAmbientPoint(_waterPoints, ref _waterPointCount, tx, ty, staticObject.Z);
                                }
                                if (staticObject.IsVegetation) vegetation++;
                                if (data.IsWall || data.IsRoof || data.IsDoor) built++;
                                if (data.IsLight) AddAmbientPoint(_lightPoints, ref _lightPointCount, tx, ty, staticObject.Z);
                                else if (IsWindowName(data.Name)) AddAmbientPoint(_windowPoints, ref _windowPointCount, tx, ty, staticObject.Z);
                            }
                            else if (obj is Item item)
                            {
                                var data = item.ItemData;
                                if (data.IsWet)
                                {
                                    water++;
                                    AddAmbientPoint(_waterPoints, ref _waterPointCount, tx, ty, item.Z);
                                }
                                if (StaticFilters.IsVegetation(item.DisplayedGraphic)) vegetation++;
                                if (data.IsWall || data.IsRoof || data.IsDoor) built++;
                                if (data.IsLight) AddAmbientPoint(_lightPoints, ref _lightPointCount, tx, ty, item.Z);
                                else if (IsWindowName(data.Name)) AddAmbientPoint(_windowPoints, ref _windowPointCount, tx, ty, item.Z);
                            }
                            else if (obj is Multi multi)
                            {
                                var data = multi.ItemData;
                                if (data.IsWet)
                                {
                                    water++;
                                    AddAmbientPoint(_waterPoints, ref _waterPointCount, tx, ty, multi.Z);
                                }
                                if (StaticFilters.IsVegetation(multi.Graphic)) vegetation++;
                                if (data.IsWall || data.IsRoof || data.IsDoor) built++;
                                if (data.IsLight) AddAmbientPoint(_lightPoints, ref _lightPointCount, tx, ty, multi.Z);
                                else if (IsWindowName(data.Name)) AddAmbientPoint(_windowPoints, ref _windowPointCount, tx, ty, multi.Z);
                            }
                        }
                    }
                }
            }
            catch
            {
                _waterPointCount = _lightPointCount = _windowPointCount = 0;
            }

            _nearWater = water >= 4;
            _onSand = sand >= 8 || IsSandTile(px, py);
            if (InDungeon || EnvironmentalShadowManager.IsIndoors)
                _biome = AmbientBiome.Dungeon;
            else if (_onSand)
                _biome = AmbientBiome.Desert;
            else if (built >= 16 || _lightPointCount + _windowPointCount >= 3)
                _biome = AmbientBiome.Town;
            else if (water >= 5 && vegetation >= 8)
                _biome = AmbientBiome.Swamp;
            else if (water >= 5)
                _biome = AmbientBiome.Coast;
            else if (vegetation >= 12)
                _biome = AmbientBiome.Forest;
            else
                _biome = AmbientBiome.Plains;
        }

        private static void AddAmbientPoint(AmbientPoint[] points, ref int count, int x, int y, sbyte z)
        {
            if (count >= points.Length) return;
            points[count].X = x;
            points[count].Y = y;
            points[count].Z = z;
            count++;
        }

        private static bool IsWindowName(string name)
            => !string.IsNullOrEmpty(name)
                && name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsSandGraphic(ushort graphic)
            => graphic >= 0x0016 && graphic <= 0x0019
                || graphic >= 0x0022 && graphic <= 0x003A
                || graphic >= 0x011E && graphic <= 0x0126;

        private static void UpdateAmbientSound(Weather weather)
        {
            if (Time.Ticks < _nextAmbientSound || weather.IsActive || weather.Fog
                || _biome == AmbientBiome.Dungeon || EnvironmentalShadowManager.IsIndoors)
            {
                return;
            }

            _nextAmbientSound = Time.Ticks + (uint)RandomHelper.GetValue(18000, 36000);
            int sound = -1;
            float day = EnvironmentalShadowManager.Daylight;
            float night = EnvironmentalShadowManager.Night;
            if ((_biome == AmbientBiome.Forest || _biome == AmbientBiome.Plains) && day > 0.45f)
                sound = 0x01B; // existing bird call
            else if (_biome == AmbientBiome.Swamp && night > 0.35f)
                sound = 0x266; // existing frog call
            else if (_biome == AmbientBiome.Coast || World.Season == Season.Winter)
                sound = RandomHelper.RandomList(0x014, 0x015, 0x016); // existing wind

            if (sound < 0) return;
            int dx = RandomHelper.GetValue(8, 14) * (RandomHelper.RandomBool() ? 1 : -1);
            int dy = RandomHelper.GetValue(8, 14) * (RandomHelper.RandomBool() ? 1 : -1);
            try
            {
                Client.Game.Audio.PlaySoundWithDistance(
                    sound, World.Player.X + dx, World.Player.Y + dy,
                    SceneryInteractionManager.WeatherSoundScale);
            }
            catch { }
        }

        private static void SpawnPuff(Mobile m)
        {
            Point p = PathPreview.TileToScreen(m.X, m.Y, m.Z);
            ref Puff puff = ref _puffs[_puffIdx++ % _puffs.Length];
            puff.SX = p.X + RandomHelper.GetValue(-3, 3);
            puff.SY = p.Y - 38;
            puff.Born = Time.Ticks;
        }

        // Classic sand land-tile ranges (RunUO/POL convention).
        private static bool IsSandTile(int tx, int ty)
        {
            try
            {
                for (GameObject o = World.Map?.GetTile(tx, ty, false); o != null; o = o.TNext)
                {
                    if (o is Land land)
                    {
                        ushort g = land.Graphic;
                        return IsSandGraphic(g);
                    }
                }
            }
            catch { }
            return false;
        }

        private static Texture2D GetDarkBlob()
        {
            if (_darkBlob != null && !_darkBlob.IsDisposed) return _darkBlob;
            const int S = 128;
            var data = new Color[S * S];
            const float half = S / 2f;
            for (int yy = 0; yy < S; yy++)
            {
                for (int xx = 0; xx < S; xx++)
                {
                    float dx = (xx - half) / half;
                    float dy = (yy - half) / half;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float a = d >= 1f ? 0f : (1f - d) * (1f - d);
                    byte b = (byte)(a * 255);
                    data[yy * S + xx] = new Color((byte)(b * 8 / 255), (byte)(b * 8 / 255), (byte)(b * 14 / 255), b);
                }
            }
            _darkBlob = new Texture2D(Client.Game.GraphicsDevice, S, S);
            _darkBlob.SetData(data);
            return _darkBlob;
        }

        private static Texture2D GetWarmBlob()
        {
            if (_warmBlob != null && !_warmBlob.IsDisposed) return _warmBlob;
            const int size = 64;
            var data = new Color[size * size];
            const float half = size / 2f;
            for (int yy = 0; yy < size; yy++)
            {
                for (int xx = 0; xx < size; xx++)
                {
                    float dx = (xx - half) / half;
                    float dy = (yy - half) / half;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = distance >= 1f ? 0f : (1f - distance) * (1f - distance);
                    byte value = (byte)(alpha * 255);
                    data[yy * size + xx] = new Color(
                        value,
                        (byte)(value * 150 / 255),
                        (byte)(value * 55 / 255),
                        value
                    );
                }
            }
            _warmBlob = new Texture2D(Client.Game.GraphicsDevice, size, size);
            _warmBlob.SetData(data);
            return _warmBlob;
        }

        public static void DrawBackground(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled || !World.InGame || World.Player == null
                || EnvironmentalShadowManager.IsIndoors) return;
            var scene = Scene;
            if (scene == null) return;
            DrawSeasonalParticles(
                batcher, bounds, scene.Weather,
                EnvironmentalShadowManager.Daylight,
                EnvironmentalShadowManager.Dusk,
                EnvironmentalShadowManager.Night,
                EnvironmentalShadowManager.AmbienceStrength,
                false);
        }

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled || !World.InGame || World.Player == null) return;
            var scene = Scene;
            if (scene == null) return;

            int x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
            float day = EnvironmentalShadowManager.Daylight;
            float night = EnvironmentalShadowManager.Night;
            float dusk = EnvironmentalShadowManager.Dusk;
            float moonlight = EnvironmentalShadowManager.Moonlight;
            float ambience = EnvironmentalShadowManager.AmbienceStrength;
            bool forcePreview = DayCyclePreviewManager.Enabled || EnvironmentShowcaseManager.Enabled
                || EnvironmentControlManager.Enabled;
            bool dungeon = !forcePreview && InDungeon;
            bool outdoors = forcePreview || !EnvironmentalShadowManager.IsIndoors;
            var weather = scene.Weather;
            bool weatherActive = weather.IsActive || weather.Fog;

            if (outdoors)
            {
                DrawCloudShadows(batcher, bounds, weather, day, dusk, ambience);
                DrawWaterAndMist(batcher, bounds, weather, day, dusk, ambience);
            }

            // --- stars + moon (clear nights, outdoors) --------------------
            if (outdoors && night > 0.4f && !weatherActive)
            {
                Texture2D star = SolidColorTextureCache.GetTexture(Color.White);
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(45); i++)
                {
                    int sx = (i * 7919) % w;
                    int sy = (i * 4409) % (h / 3);
                    float tw = 0.35f + 0.3f * (float)Math.Sin(Time.Ticks / 350f + i * 2.3f);
                    batcher.Draw(star, new Rectangle(x + sx, y + sy, 1, 1),
                        ShaderHueTranslator.GetHueVector(0, false, tw * night * ambience));
                }

                // Fixed moon position: the server provides light level but no
                // astronomical coordinates. Its shadow direction is fixed by
                // EnvironmentalShadowManager as well.
                Texture2D blob = Weather.GetFogBlob();
                int mx = w * 3 / 4;
                int my = h / 10;
                batcher.Draw(blob, new Rectangle(x + mx - 30, y + my - 30, 60, 60),
                    ShaderHueTranslator.GetHueVector(0, false, 0.35f * moonlight * ambience));
                batcher.Draw(blob, new Rectangle(x + mx - 14, y + my - 14, 28, 28),
                    ShaderHueTranslator.GetHueVector(0, false, 0.85f * moonlight * ambience));
            }

            DrawWarmLights(batcher, bounds, dusk, night, ambience, outdoors);

            if (outdoors)
            {
                DrawSeasonalParticles(batcher, bounds, weather, day, dusk, night, ambience, true);
                DrawWildlife(batcher, bounds, weather, day, night, ambience);
            }

            // --- desert dust / heat shimmer --------------------------------
            if (outdoors && _onSand && night < 0.5f && !weatherActive)
            {
                Texture2D dust = SolidColorTextureCache.GetTexture(new Color(200, 175, 120, 255));
                float sandWind = SceneryInteractionManager.SharedWind;
                float windSpeed = Math.Max(0.35f, Math.Abs(sandWind));
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(14); i++)
                {
                    float speed = 0.05f + (i % 4) * 0.02f;
                    int span = w + 12;
                    int travel = (int)(Time.Ticks * speed * windSpeed);
                    int dx = (i * 449 + (sandWind < 0f ? -travel : travel)) % span;
                    if (dx < 0) dx += span;
                    dx -= 6;
                    int dy = (i * 193) % h + (int)(8 * Math.Sin(Time.Ticks / 300f + i));
                    if (dy < 0 || dy >= h) continue;
                    float da = 0.15f + 0.10f * (float)Math.Sin(Time.Ticks / 180f + i * 1.7f);
                    batcher.Draw(dust, new Rectangle(x + dx, y + dy, 2, 1),
                        ShaderHueTranslator.GetHueVector(0, false, da));
                }
            }

            // --- dungeon dust motes + vignette -----------------------------
            // (No fake light shafts: the client can't know where ceiling
            // openings are, and over cave blackness they read as artifacts.)
            if (dungeon)
            {
                Texture2D white = SolidColorTextureCache.GetTexture(new Color(235, 235, 220, 255));
                // Sparse dust motes drifting down near the player (screen
                // center), where there's actual visible ground.
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(8); i++)
                {
                    float t = (Time.Ticks * (0.006f + (i % 3) * 0.003f) + i * 311) % (h * 0.5f);
                    int mxp = (int)(w * 0.30f + (i * 173) % (int)(w * 0.4f)
                            + 10 * Math.Sin(Time.Ticks / 400f + i * 2.1f));
                    float ma = 0.07f + 0.05f * (float)Math.Sin(Time.Ticks / 260f + i);
                    batcher.Draw(white, new Rectangle(x + mxp, y + (int)(h * 0.25f + t), 1, 1),
                        ShaderHueTranslator.GetHueVector(0, false, ma));
                }

                // Vignette: dark soft blobs in the corners.
                Texture2D dark = GetDarkBlob();
                int vs = (int)(h * 1.1f);
                Vector3 vHue = ShaderHueTranslator.GetHueVector(0, false, 0.55f);
                batcher.Draw(dark, new Rectangle(x - vs / 2, y - vs / 2, vs, vs), vHue);
                batcher.Draw(dark, new Rectangle(x + w - vs / 2, y - vs / 2, vs, vs), vHue);
                batcher.Draw(dark, new Rectangle(x - vs / 2, y + h - vs / 2, vs, vs), vHue);
                batcher.Draw(dark, new Rectangle(x + w - vs / 2, y + h - vs / 2, vs, vs), vHue);
            }

            // --- breath vapor puffs ----------------------------------------
            {
                Texture2D blob = Weather.GetFogBlob();
                uint now = Time.Ticks;
                for (int i = 0; i < _puffs.Length; i++)
                {
                    ref Puff p = ref _puffs[i];
                    if (p.Born == 0) continue;
                    uint age = now - p.Born;
                    if (age >= PUFF_LIFE_MS) { p.Born = 0; continue; }
                    float t = age / (float)PUFF_LIFE_MS;
                    int size = 6 + (int)(10 * t);
                    float alpha = 0.35f * (1f - t);
                    batcher.Draw(blob,
                        new Rectangle((int)p.SX - size / 2, (int)(p.SY - 6 * t) - size / 2, size, size),
                        ShaderHueTranslator.GetHueVector(0, false, alpha));
                }
            }

            SceneryInteractionManager.DrawForeground(batcher, bounds);
        }

        private static void DrawCloudShadows(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            float day,
            float dusk,
            float ambience)
        {
            bool severe = weather.Fog || weather.IsActive
                && (weather.Type == WeatherType.WT_STORM_APPROACH
                    || weather.Blizzard || weather.HeavySnow);
            float solar = Math.Max(day, dusk * 0.55f);
            if (severe || solar < 0.08f) return;

            Texture2D cloud = GetDarkBlob();
            int worldOffset = World.Player == null ? 0 : (World.Player.X - World.Player.Y) * 22;
            float weatherBoost = weather.IsActive ? 1.35f : 1f;
            for (int i = 0; i < 4; i++)
            {
                int cw = 310 + i * 75;
                int ch = 95 + i * 17;
                int span = bounds.Width + cw;
                float speed = 0.010f + i * 0.003f;
                int cx = (int)((Time.Ticks * speed + i * 503 - worldOffset) % span);
                if (cx < 0) cx += span;
                cx -= cw;
                int cy = bounds.Height / 7 + (i * 149) % Math.Max(1, bounds.Height * 2 / 3);
                float breathe = 0.65f + 0.35f * (float)Math.Sin(Time.Ticks / 2400f + i * 1.8f);
                float alpha = (0.030f + i * 0.004f) * solar * ambience * weatherBoost * breathe;
                batcher.Draw(cloud,
                    new Rectangle(bounds.X + cx, bounds.Y + cy, cw, ch),
                    ShaderHueTranslator.GetHueVector(0, false, alpha));
            }
        }

        private static void DrawWaterAndMist(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            float day,
            float dusk,
            float ambience)
        {
            bool dawn = DayCyclePreviewManager.IsDawn || EnvironmentShowcaseManager.IsDawn
                || EnvironmentControlManager.IsDawn;
            bool forestDawn = _biome == AmbientBiome.Forest && (dawn || dusk > 0.45f && day > 0.15f);
            if (!_nearWater && weather.Wetness < 0.08f && !forestDawn) return;

            Texture2D mist = Weather.GetFogBlob();

            if (_waterPointCount == 0 && (forestDawn || weather.Wetness > 0.10f))
            {
                float groundMist = Math.Max(forestDawn ? 0.45f : 0f, weather.Wetness * 0.55f);
                for (int i = 0; i < 4; i++)
                {
                    int mw = 260 + i * 45;
                    int mh = 55 + i * 8;
                    int span = bounds.Width + mw;
                    int mx = (int)((Time.Ticks * (0.005f + i * 0.002f) + i * 347) % span) - mw;
                    int my = bounds.Y + bounds.Height * (58 + i * 9) / 100;
                    batcher.Draw(mist,
                        new Rectangle(bounds.X + mx, my, mw, mh),
                        ShaderHueTranslator.GetHueVector(0, false,
                            0.075f * groundMist * ambience));
                }
            }

            // Small reflections remain after rainfall even away from a sampled
            // shoreline, making wetness visible without a full-screen gloss.
            if (weather.Wetness > 0.12f)
            {
                Texture2D wet = SolidColorTextureCache.GetTexture(new Color(120, 155, 195, 255));
                for (int i = 0; i < 8; i++)
                {
                    int sx = bounds.X + (i * 317 + World.Player.X * 13) % Math.Max(1, bounds.Width);
                    int sy = bounds.Y + bounds.Height / 2
                        + (i * 173 + World.Player.Y * 7) % Math.Max(1, bounds.Height / 2);
                    float pulse = 0.5f + 0.5f * (float)Math.Sin(Time.Ticks / 420f + i * 2.1f);
                    batcher.Draw(wet, new Rectangle(sx, sy, 3 + i % 4, 1),
                        ShaderHueTranslator.GetHueVector(0, false,
                            weather.Wetness * pulse * 0.10f * ambience));
                }
            }
        }

        private static void DrawWarmLights(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            float dusk,
            float night,
            float ambience,
            bool outdoors)
        {
            if (!LightsEnabled) return;

            float strength = outdoors ? Math.Max(night, dusk * 0.75f) * ambience : 0f;
            bool coloredLights = SceneryInteractionManager.DrawColoredLights(batcher, bounds, strength);
            if (!outdoors || strength < 0.04f) return;
            int realCount = coloredLights ? 0 : _lightPointCount;
            int pointCount = realCount + _windowPointCount;
            if (pointCount == 0) return;

            Texture2D glow = GetWarmBlob();
            for (int i = 0; i < pointCount; i++)
            {
                AmbientPoint lightPoint = i < realCount
                    ? _lightPoints[i] : _windowPoints[i - realCount];
                Point p = PathPreview.TileToScreen(lightPoint.X, lightPoint.Y, lightPoint.Z);
                if (!bounds.Contains(p)) continue;
                float flicker = 0.82f + 0.18f * (float)Math.Sin(Time.Ticks / 120f + i * 2.7f);
                int width = 72 + i % 3 * 12;
                int height = width * 2 / 3;
                batcher.Draw(glow,
                    new Rectangle(p.X - width / 2, p.Y - height / 2 - 10, width, height),
                    ShaderHueTranslator.GetHueVector(0, false, 0.24f * strength * flicker));
            }
        }

        private static void DrawSeasonalParticles(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            float day,
            float dusk,
            float night,
            float ambience,
            bool foreground)
        {
            bool severe = weather.IsActive && (weather.Type == WeatherType.WT_STORM_APPROACH
                || weather.Blizzard || weather.HeavySnow);
            if (severe || weather.Fog
                || _biome == AmbientBiome.Town && !EnvironmentShowcaseManager.BeautifulEnabled) return;

            int count = SceneryInteractionManager.ScaleCount(
                EnvironmentShowcaseManager.BeautifulEnabled ? 12 : 8);
            float wind = SceneryInteractionManager.SharedWind;
            for (int i = 0; i < count; i++)
            {
                if ((i % 3 != 0) != foreground) continue;
                int tx = World.Player.X + (i * 7 % 19) - 9;
                int ty = World.Player.Y + (i * 11 % 19) - 9;
                Point anchor = PathPreview.TileToScreen(tx, ty, World.Player.Z);
                float speed = 0.018f + i % 3 * 0.007f;
                int fall = (int)((Time.Ticks * speed + i * 557) % 130);
                int sx = anchor.X + (int)(wind * fall * 0.22f)
                    + (int)(12 * Math.Sin(Time.Ticks / 600f + i));
                int sy = anchor.Y - 95 + fall;
                if (!bounds.Contains(sx, sy)) continue;

                Color color;
                int width = 2, height = 2;
                float alpha;
                if (World.Season == Season.Spring && night < 0.55f)
                {
                    color = i % 2 == 0 ? new Color(255, 190, 205, 255) : new Color(245, 235, 205, 255);
                    alpha = 0.42f * Math.Max(day, dusk) * ambience;
                }
                else if (World.Season == Season.Summer && night < 0.45f)
                {
                    color = new Color(245, 220, 135, 255);
                    width = height = 1;
                    alpha = 0.28f * Math.Max(day, dusk) * ambience;
                }
                else if (World.Season == Season.Fall && night < 0.60f)
                {
                    color = i % 3 == 0 ? new Color(150, 78, 35, 255)
                        : i % 3 == 1 ? new Color(190, 122, 38, 255)
                        : new Color(115, 120, 42, 255);
                    width = 3;
                    alpha = 0.52f * Math.Max(day, dusk) * ambience;
                }
                else if (World.Season == Season.Winter || weather.Coldness > 0.2f)
                {
                    color = new Color(220, 238, 255, 255);
                    width = height = 1;
                    float sparkle = 0.5f + 0.5f * (float)Math.Sin(Time.Ticks / 180f + i * 2.8f);
                    alpha = (0.10f + 0.28f * sparkle) * Math.Max(day, night * 0.6f) * ambience;
                }
                else
                {
                    continue;
                }

                batcher.Draw(SolidColorTextureCache.GetTexture(color),
                    new Rectangle(sx, sy, width, height),
                    ShaderHueTranslator.GetHueVector(0, false, alpha));
            }
        }

        private static void DrawWildlife(
            UltimaBatcher2D batcher,
            Rectangle bounds,
            Weather weather,
            float day,
            float night,
            float ambience)
        {
            bool calm = !weather.IsActive && !weather.Fog;
            bool natural = EnvironmentShowcaseManager.BeautifulEnabled
                || _biome == AmbientBiome.Forest || _biome == AmbientBiome.Plains
                || _biome == AmbientBiome.Swamp || _biome == AmbientBiome.Coast;
            if (!calm || !natural) return;

            bool warmSeason = World.Season == Season.Spring || World.Season == Season.Summer;
            if (warmSeason && night > 0.30f)
            {
                Texture2D fly = SolidColorTextureCache.GetTexture(new Color(255, 224, 112, 255));
                int count = SceneryInteractionManager.ScaleCount(
                    EnvironmentShowcaseManager.BeautifulEnabled ? 16 : 10);
                float t = Time.Ticks / 1000f;
                for (int i = 0; i < count; i++)
                {
                    int tx = World.Player.X + (i * 7 % 21) - 10;
                    int ty = World.Player.Y + (i * 13 % 21) - 10;
                    Point p = PathPreview.TileToScreen(tx, ty, World.Player.Z);
                    int fx = p.X + (int)(18 * Math.Sin(t * 0.31f + i * 1.9f));
                    int fy = p.Y - 20 + (int)(15 * Math.Sin(t * 0.47f + i));
                    if (!bounds.Contains(fx, fy)) continue;
                    float blink = (float)Math.Sin(t * 1.1f + i * 2.7f);
                    if (blink < 0.20f) continue;
                    float alpha = (blink - 0.20f) * night * ambience;
                    batcher.Draw(fly, new Rectangle(fx, fy, 2, 2),
                        ShaderHueTranslator.GetHueVector(0, false, alpha));
                    if (blink > 0.72f)
                    {
                        batcher.Draw(GetWarmBlob(), new Rectangle(fx - 4, fy - 4, 10, 10),
                            ShaderHueTranslator.GetHueVector(0, false, alpha * 0.22f));
                    }
                }
            }

            // Moths orbit real light-emitting objects rather than arbitrary
            // screen points.
            if (night > 0.35f && _lightPointCount > 0)
            {
                Texture2D moth = SolidColorTextureCache.GetTexture(new Color(238, 224, 180, 255));
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(_lightPointCount); i++)
                {
                    Point p = PathPreview.TileToScreen(_lightPoints[i].X, _lightPoints[i].Y, _lightPoints[i].Z);
                    float angle = Time.Ticks / 700f + i * 2.4f;
                    int mx = p.X + (int)(15 * Math.Cos(angle));
                    int my = p.Y - 10 + (int)(8 * Math.Sin(angle * 1.3f));
                    if (!bounds.Contains(mx, my)) continue;
                    float light = SceneryInteractionManager.GetLightInfluence(
                        mx, my, out Color lightColor);
                    Texture2D litMoth = light > 0.03f
                        ? SolidColorTextureCache.GetTexture(Color.Lerp(
                            new Color(238, 224, 180, 255), lightColor, Math.Min(0.45f, light)))
                        : moth;
                    batcher.Draw(litMoth, new Rectangle(mx, my, 2, 1),
                        ShaderHueTranslator.GetHueVector(0, false,
                            Math.Min(0.85f, 0.35f + light * 0.35f) * night * ambience));
                }
            }

            // A small bat silhouette crosses occasionally during deep night.
            float batCycle = Time.Ticks % 42000 / 1000f;
            if (night > 0.65f && batCycle < 8f && _biome != AmbientBiome.Plains)
            {
                Texture2D bat = SolidColorTextureCache.GetTexture(new Color(16, 18, 26, 255));
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(3); i++)
                {
                    int bx = bounds.X + (int)((batCycle / 8f) * (bounds.Width + 80)) - 40 - i * 55;
                    int by = bounds.Y + bounds.Height / 5 + i * 28
                        + (int)(12 * Math.Sin(Time.Ticks / 240f + i));
                    int wing = (int)(3 + 2 * Math.Sin(Time.Ticks / 90f + i));
                    Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 0.65f * night * ambience);
                    batcher.DrawLine(bat, new Vector2(bx - 5, by + wing), new Vector2(bx, by), hue, 2);
                    batcher.DrawLine(bat, new Vector2(bx, by), new Vector2(bx + 5, by + wing), hue, 2);
                }
            }

            // Dawn/day bird silhouettes: brief flock, then a long quiet gap.
            float birdCycle = Time.Ticks % 52000 / 1000f;
            if (day > 0.55f && birdCycle < 9f)
            {
                Texture2D bird = SolidColorTextureCache.GetTexture(new Color(40, 44, 48, 255));
                for (int i = 0; i < SceneryInteractionManager.ScaleCount(4); i++)
                {
                    int bx = bounds.Right - (int)(birdCycle / 9f * (bounds.Width + 100)) + i * 34;
                    int by = bounds.Y + 55 + i * 18;
                    Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 0.38f * day * ambience);
                    batcher.DrawLine(bird, new Vector2(bx - 3, by + 2), new Vector2(bx, by), hue, 1);
                    batcher.DrawLine(bird, new Vector2(bx, by), new Vector2(bx + 3, by + 2), hue, 1);
                }
            }
        }
    }
}

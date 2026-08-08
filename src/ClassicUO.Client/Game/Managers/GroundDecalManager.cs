#region license
// TazUO addition. Short-lived world-anchored atmospheric decals.
#endregion

using System;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal enum GroundDecalKind : byte
    {
        Snow,
        Puddle,
        Footprint,
        Blood
    }

    internal sealed class GroundDecalManager
    {
        private const int MAX_DECALS = 900;
        private const uint DEFAULT_LIFE_MS = 45_000;
        private const uint DEFAULT_FADE_MS = 12_000;
        private const uint WEATHER_DECAL_INTERVAL_MS = 50;

        private readonly GroundDecal[] _decals = new GroundDecal[MAX_DECALS];
        private int _decalIndex;
        private uint _nextWeatherDecal;
        private static Texture2D _puddleBlob;

        public void AddFromScreen(
            float screenX,
            float screenY,
            Point windowSize,
            byte size,
            GroundDecalKind kind
        )
        {
            Mobile player = World.Player;
            if (player == null || Time.Ticks < _nextWeatherDecal)
            {
                return;
            }

            _nextWeatherDecal = Time.Ticks + WEATHER_DECAL_INTERVAL_MS;

            float offsetX = (int)player.Offset.X;
            float offsetYZ = (int)(player.Offset.Y - player.Offset.Z);
            float a = (windowSize.X / 2f - offsetX - screenX) / 22f;
            float b = (windowSize.Y / 2f - offsetYZ + (player.Z << 2) - screenY) / 22f;
            float tileX = player.X - (a + b) / 2f;
            float tileY = player.Y - (b - a) / 2f;

            sbyte tileZ = player.Z;
            try
            {
                tileZ = World.Map?.GetTileZ((int)tileX, (int)tileY) ?? player.Z;
            }
            catch
            {
            }

            AddAtTile(tileX, tileY, tileZ, size, kind);
        }

        public void AddAtTile(float tileX, float tileY, sbyte tileZ, byte size, GroundDecalKind kind)
        {
            int x = (int)tileX;
            int y = (int)tileY;
            if ((kind == GroundDecalKind.Snow || kind == GroundDecalKind.Puddle)
                && SceneryInteractionManager.IsTileSheltered(x, y, tileZ)) return;
            if (kind == GroundDecalKind.Snow && SceneryInteractionManager.IsNearLight(x, y, 2)) return;

            ScenerySurface surface = SceneryInteractionManager.ClassifySurface(x, y, tileZ);
            if (kind == GroundDecalKind.Puddle && surface == ScenerySurface.Water) return;

            ref GroundDecal decal = ref _decals[_decalIndex++ % MAX_DECALS];
            decal.TileX = tileX;
            decal.TileY = tileY;
            decal.TileZ = tileZ;
            decal.Size = size;
            decal.Kind = kind;
            decal.Surface = surface;
            decal.Map = (byte)World.MapIndex;
            decal.Born = Time.Ticks;
            decal.NextHeatCheck = 0;
            decal.MeltStarted = 0;
            decal.NearHeat = false;
        }

        public void Draw(UltimaBatcher2D batcher, int x, int y)
        {
            Mobile player = World.Player;
            if (player == null || !World.InGame)
            {
                return;
            }

            uint now = Time.Ticks;
            Rectangle bounds = Client.Game.Scene.Camera.Bounds;
            float centerX = bounds.Width / 2f;
            float centerY = bounds.Height / 2f;
            float offsetX = (int)player.Offset.X;
            float offsetYZ = (int)(player.Offset.Y - player.Offset.Z);
            byte map = (byte)World.MapIndex;

            Texture2D snowTexture = SolidColorTextureCache.GetTexture(Color.White);
            Texture2D footprintTexture = SolidColorTextureCache.GetTexture(new Color(60, 55, 50, 255));
            Texture2D bloodTexture = SolidColorTextureCache.GetTexture(new Color(110, 12, 12, 255));
            Rectangle rectangle = new Rectangle(0, 0, 1, 1);

            for (int i = 0; i < MAX_DECALS; i++)
            {
                ref GroundDecal decal = ref _decals[i];
                if (decal.Born == 0)
                {
                    continue;
                }

                uint age = now - decal.Born;
                uint puddleLife = decal.Surface == ScenerySurface.Stone
                    || decal.Surface == ScenerySurface.Mine
                    || decal.Surface == ScenerySurface.Dungeon
                    ? 150_000u : decal.Surface == ScenerySurface.Dirt ? 70_000u
                        : decal.Surface == ScenerySurface.Sand ? 50_000u : 100_000u;
                uint life = decal.Kind == GroundDecalKind.Footprint
                    ? 20_000u
                    : decal.Kind == GroundDecalKind.Blood
                        ? 60_000u
                        : decal.Kind == GroundDecalKind.Puddle
                            ? puddleLife
                            : DEFAULT_LIFE_MS;
                uint fade = decal.Kind == GroundDecalKind.Footprint
                    ? 8_000u
                    : decal.Kind == GroundDecalKind.Blood
                        ? 20_000u
                        : decal.Kind == GroundDecalKind.Puddle
                            ? 45_000u
                            : DEFAULT_FADE_MS;

                if (age >= life || decal.Map != map)
                {
                    decal.Born = 0;
                    continue;
                }

                float gameOffsetX = player.X - decal.TileX;
                float gameOffsetY = player.Y - decal.TileY;
                float screenX = centerX - (gameOffsetX - gameOffsetY) * 22f - offsetX;
                float screenY = centerY - (gameOffsetX + gameOffsetY) * 22f - offsetYZ
                    + (player.Z << 2) - (decal.TileZ << 2);

                if (screenX < -8 || screenX > bounds.Width + 8 || screenY < -8 || screenY > bounds.Height + 8)
                {
                    continue;
                }

                float heatFade = 1f;
                if (decal.Kind == GroundDecalKind.Snow)
                {
                    if (now >= decal.NextHeatCheck)
                    {
                        decal.NextHeatCheck = now + 1000u + (uint)(i % 10 * 37);
                        decal.NearHeat = SceneryInteractionManager.IsNearLight(
                            (int)decal.TileX, (int)decal.TileY, 2);
                        if (decal.NearHeat && decal.MeltStarted == 0) decal.MeltStarted = now;
                        else if (!decal.NearHeat) decal.MeltStarted = 0;
                    }
                    if (decal.NearHeat && decal.MeltStarted != 0)
                    {
                        uint meltAge = now - decal.MeltStarted;
                        if (meltAge >= 5000u) { decal.Born = 0; continue; }
                        heatFade = 1f - meltAge / 5000f;
                    }
                }

                float alpha = decal.Kind == GroundDecalKind.Puddle
                    ? decal.Surface == ScenerySurface.Stone ? 0.56f
                        : decal.Surface == ScenerySurface.Mine ? 0.52f
                        : decal.Surface == ScenerySurface.Dungeon ? 0.50f
                        : decal.Surface == ScenerySurface.Dirt ? 0.34f
                        : decal.Surface == ScenerySurface.Sand ? 0.26f : 0.44f
                    : decal.Kind == GroundDecalKind.Footprint
                        ? 0.30f
                        : decal.Kind == GroundDecalKind.Blood
                            ? 0.45f
                            : 0.75f;
                uint fadeStart = life - fade;
                if (age > fadeStart)
                {
                    alpha *= 1f - (age - fadeStart) / (float)fade;
                }
                alpha *= heatFade;

                rectangle.X = x + (int)screenX;
                rectangle.Y = y + (int)screenY;
                Texture2D texture;

                switch (decal.Kind)
                {
                    case GroundDecalKind.Puddle:
                        float puddleGrowth = Math.Min(1f, age / 8000f) * 0.6f + 0.7f;
                        int puddleWidth = (int)(decal.Size * 8 * puddleGrowth);
                        int puddleHeight = puddleWidth / 2;
                        rectangle.X -= puddleWidth / 2;
                        rectangle.Y -= puddleHeight / 2;
                        rectangle.Width = puddleWidth;
                        rectangle.Height = puddleHeight;
                        alpha *= 1f + 0.15f * (float)Math.Sin(now / 400f + i * 1.3f);
                        texture = GetPuddleBlob();
                        break;
                    case GroundDecalKind.Footprint:
                        rectangle.Width = 3;
                        rectangle.Height = 2;
                        texture = footprintTexture;
                        break;
                    case GroundDecalKind.Blood:
                        float bloodGrowth = Math.Min(1f, age / 5000f) * 0.5f + 0.9f;
                        rectangle.Width = (int)(decal.Size * 2 * bloodGrowth);
                        rectangle.Height = (int)(decal.Size * bloodGrowth);
                        texture = bloodTexture;
                        break;
                    default:
                        rectangle.Width = decal.Size + 1;
                        rectangle.Height = decal.Size;
                        texture = snowTexture;
                        break;
                }

                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
                batcher.Draw(texture, rectangle, hue);
            }
        }

        private static Texture2D GetPuddleBlob()
        {
            if (_puddleBlob != null && !_puddleBlob.IsDisposed)
            {
                return _puddleBlob;
            }

            const int size = 64;
            var data = new Color[size * size];
            const float half = size / 2f;
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float dx = (px - half) / half;
                    float dy = (py - half) / half;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = distance >= 1f ? 0f : (1f - distance) * (1f - distance);
                    byte value = (byte)(alpha * 255);
                    data[py * size + px] = new Color(
                        (byte)(value * 45 / 255),
                        (byte)(value * 58 / 255),
                        (byte)(value * 82 / 255),
                        value
                    );
                }
            }

            _puddleBlob = new Texture2D(Client.Game.GraphicsDevice, size, size);
            _puddleBlob.SetData(data);
            return _puddleBlob;
        }

        private struct GroundDecal
        {
            public float TileX;
            public float TileY;
            public sbyte TileZ;
            public byte Size;
            public byte Map;
            public GroundDecalKind Kind;
            public ScenerySurface Surface;
            public uint Born;
            public uint NextHeatCheck;
            public uint MeltStarted;
            public bool NearHeat;
        }
    }
}

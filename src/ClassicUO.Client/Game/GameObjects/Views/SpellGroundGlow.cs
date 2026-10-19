using System;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal static class SpellGroundGlow
    {
        private const int GROUND_CELL = 128;
        private static Texture2D _groundTexture;

        internal static void Draw(UltimaBatcher2D batcher, Vector2 center,
            float diameter, Color color, float opacity, bool ring = false)
        {
            EnsureGroundTexture(batcher.GraphicsDevice);
            var source = new Rectangle(ring ? GROUND_CELL : 0, 0, GROUND_CELL, GROUND_CELL);
            color.A = (byte)(MathHelper.Clamp(opacity, 0, 1) * 255);
            batcher.Draw(_groundTexture, center - new Vector2(diameter * 0.5f, diameter * 0.25f),
                source, color, 0f, new Vector2(diameter / GROUND_CELL, diameter * 0.5f / GROUND_CELL), 0f);
        }

        private static void EnsureGroundTexture(GraphicsDevice device)
        {
            if (_groundTexture != null && !_groundTexture.IsDisposed) return;
            var pixels = new Color[GROUND_CELL * GROUND_CELL * 2];
            for (int y = 0; y < GROUND_CELL; y++)
                for (int x = 0; x < GROUND_CELL; x++)
                {
                    float dx = (x + 0.5f) / (GROUND_CELL * 0.5f) - 1f;
                    float dy = (y + 0.5f) / (GROUND_CELL * 0.5f) - 1f;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    float edge = MathHelper.Clamp((1f - r) * 5f, 0, 1);
                    byte glow = (byte)(Math.Exp(-r * r * 4.5f) * edge * 255);
                    byte halo = (byte)(Math.Exp(-Math.Pow((r - 0.74f) / 0.1f, 2)) * edge * 255);
                    pixels[y * GROUND_CELL * 2 + x] = new Color(glow, glow, glow, glow);
                    pixels[y * GROUND_CELL * 2 + x + GROUND_CELL] = new Color(halo, halo, halo, halo);
                }
            _groundTexture = new Texture2D(device, GROUND_CELL * 2, GROUND_CELL);
            _groundTexture.SetData(pixels);
        }

        internal static void Reset()
        {
            _groundTexture?.Dispose();
            _groundTexture = null;
        }
    }
}

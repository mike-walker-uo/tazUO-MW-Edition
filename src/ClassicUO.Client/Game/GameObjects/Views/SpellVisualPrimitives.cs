using System;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    // One white atlas, tinted at draw time. No per-particle textures or retained trails.
    internal static class SpellVisualPrimitives
    {
        private static Texture2D _atlas;
        private static readonly Rectangle Disc = new Rectangle(0, 0, 64, 64);
        private static readonly Rectangle Beam = new Rectangle(0, 64, 128, 32);
        private static readonly Rectangle Shard = new Rectangle(64, 0, 64, 32);
        private static readonly Rectangle Vapor = new Rectangle(0, 96, 64, 32);

        internal static float Envelope(float progress, float attack = 0.12f, float release = 0.45f)
        {
            if (progress <= 0 || progress >= 1) return 0;
            float a = MathHelper.Clamp(progress / attack, 0, 1);
            float r = MathHelper.Clamp((1f - progress) / release, 0, 1);
            return a * a * (3f - 2f * a) * r * r * (3f - 2f * r);
        }

        internal static float Coverage(float x, float y, bool beam)
        {
            float edge = beam ? MathHelper.Clamp((1f - Math.Abs(x)) * 32f, 0, 1)
                : MathHelper.Clamp((1f - x * x - y * y) * 3f, 0, 1);
            return (float)Math.Exp(-(beam ? y * y * 3.5f : (x * x + y * y) * 3f))
                * edge * MathHelper.Clamp((1f - Math.Abs(y)) * 6f, 0, 1);
        }

        internal static void Stroke(UltimaBatcher2D batcher, Vector2 start, Vector2 end,
            Color color, float alpha, float width, float depth)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length <= 0.01f || alpha <= 0 || width <= 0) return;
            float height = Math.Max(1f, width * 1.8f);
            Vector2 normal = new Vector2(-delta.Y, delta.X) / length;
            Patch(batcher, Beam, start - normal * height * 0.5f, length, height,
                (float)Math.Atan2(delta.Y, delta.X), color, alpha, depth);
        }

        internal static void Mote(UltimaBatcher2D batcher, Vector2 center, Color color, float alpha, float size, float depth)
        {
            if (alpha <= 0 || size <= 0) return;
            float diameter = Math.Max(1.5f, size * 1.8f);
            Patch(batcher, Disc, center - new Vector2(diameter * 0.5f), diameter, diameter, 0, color, alpha, depth);
        }

        internal static void Mist(UltimaBatcher2D batcher, Vector2 center, Color color, float alpha,
            float width, float height, float rotation, float depth)
        {
            Vector2 half = new Vector2(width * 0.5f, height * 0.5f);
            float c = (float)Math.Cos(rotation), s = (float)Math.Sin(rotation);
            Patch(batcher, Vapor, center - new Vector2(c * half.X - s * half.Y, s * half.X + c * half.Y),
                width, height, rotation, color, alpha, depth);
        }

        internal static void Fragment(UltimaBatcher2D batcher, Vector2 center, Color color, float alpha,
            float size, float rotation, float depth)
        {
            Vector2 half = new Vector2(size * 0.5f, size * 0.25f);
            float c = (float)Math.Cos(rotation), s = (float)Math.Sin(rotation);
            Patch(batcher, Shard, center - new Vector2(c * half.X - s * half.Y, s * half.X + c * half.Y),
                size, size * 0.5f, rotation, color, alpha, depth);
        }

        internal static Vector2 Curve(Vector2 start, Vector2 control, Vector2 end, float t) =>
            (1f - t) * (1f - t) * start + 2f * (1f - t) * t * control + t * t * end;

        internal static void Ribbon(UltimaBatcher2D batcher, Vector2 start, Vector2 control, Vector2 end,
            Color color, float alpha, float width, float depth)
        {
            if (alpha <= 0 || width <= 0) return;
            int steps = 6 + Math.Min(6, SceneryInteractionManager.Quality * 2);
            Vector2 previous = start;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 next = Curve(start, control, end, t);
                float taper = 0.18f + 0.82f * (float)Math.Sin((i - 0.5f) / steps * MathHelper.Pi);
                Stroke(batcher, previous, next, color, alpha, width * taper, depth);
                previous = next;
            }
        }

        private static void Patch(UltimaBatcher2D batcher, Rectangle source, Vector2 position,
            float width, float height, float rotation, Color color, float alpha, float depth)
        {
            if (alpha <= 0 || width <= 0 || height <= 0) return;
            EnsureTexture(batcher.GraphicsDevice);
            OptionalTextureCache.Touch(_atlas);
            color.A = (byte)(MathHelper.Clamp(alpha, 0, 1) * 255);
            batcher.Draw(_atlas, position, source, color, rotation,
                new Vector2(width / source.Width, height / source.Height), depth);
        }

        private static void EnsureTexture(GraphicsDevice device)
        {
            if (_atlas != null && !_atlas.IsDisposed) return;
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float a = 0;
                    if (y < 64 && x < 64) a = Coverage((x + 0.5f) / 32f - 1, (y + 0.5f) / 32f - 1, false);
                    else if (y < 32)
                    {
                        float dx = (x - 64 + 0.5f) / 32f - 1, dy = (y + 0.5f) / 16f - 1;
                        float diamond = MathHelper.Clamp((1 - Math.Abs(dx) - Math.Abs(dy)) * 7f, 0, 1);
                        a = diamond * (0.55f + 0.45f * MathHelper.Clamp(1f - dy, 0, 1));
                    }
                    else if (y >= 64 && y < 96) a = Coverage((x + 0.5f) / 64f - 1, (y - 64 + 0.5f) / 16f - 1, true);
                    else if (y >= 96 && x < 64)
                    {
                        float dx = (x + 0.5f) / 32f - 1, dy = (y - 96 + 0.5f) / 16f - 1;
                        a = Coverage(dx, dy, false) * (0.72f + 0.28f * (float)Math.Sin(dx * 9f + dy * 7f));
                    }
                    byte value = (byte)(MathHelper.Clamp(a, 0, 1) * 255);
                    pixels[y * 128 + x] = new Color(value, value, value, value);
                }
            _atlas = new Texture2D(device, 128, 128);
            _atlas.SetData(pixels);
            Texture2D atlas = _atlas;
            OptionalTextureCache.Register(atlas, () => { if (ReferenceEquals(_atlas, atlas)) _atlas = null; });
        }

        internal static void Reset() { _atlas?.Dispose(); _atlas = null; }
    }
}

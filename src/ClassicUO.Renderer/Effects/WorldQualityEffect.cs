using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public sealed class WorldQualityEffect : Effect
    {
        public WorldQualityEffect(GraphicsDevice device, byte[] bytecode) : base(device, bytecode) { }

        public void Configure(Rectangle source, Point textureSize, Point destinationSize, int strength, bool upscale)
        {
            var viewport = GraphicsDevice.Viewport;
            CurrentTechnique = Techniques[upscale ? "PixelArt" : "AntiAlias"];
            Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(
                0, viewport.Width, viewport.Height, 0, short.MinValue, short.MaxValue));
            Parameters["Viewport"].SetValue(new Vector2(viewport.Width, viewport.Height));
            Parameters["TextureSize"].SetValue(new Vector2(textureSize.X, textureSize.Y));
            Parameters["SampleBounds"].SetValue(new Vector4(
                (source.Left + 0.5f) / textureSize.X, (source.Top + 0.5f) / textureSize.Y,
                (source.Right - 0.5f) / textureSize.X, (source.Bottom - 0.5f) / textureSize.Y));
            Parameters["Footprint"].SetValue(new Vector2(
                (float)source.Width / destinationSize.X, (float)source.Height / destinationSize.Y));
            Parameters["Strength"].SetValue(Math.Max(0, Math.Min(100, strength)) / 100f);
        }
    }
}

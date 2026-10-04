using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public sealed class MagnifierEffect : Effect
    {
        public MagnifierEffect(GraphicsDevice device, byte[] bytecode) : base(device, bytecode) { }

        public void Configure(Vector4 sourceRegion)
        {
            var viewport = GraphicsDevice.Viewport;
            Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(
                0, viewport.Width, viewport.Height, 0, short.MinValue, short.MaxValue));
            Parameters["Viewport"].SetValue(new Vector2(viewport.Width, viewport.Height));
            Parameters["SourceCenter"].SetValue(new Vector2(sourceRegion.X, sourceRegion.Y));
            Parameters["SourceRadius"].SetValue(new Vector2(sourceRegion.Z, sourceRegion.W));
        }
    }
}

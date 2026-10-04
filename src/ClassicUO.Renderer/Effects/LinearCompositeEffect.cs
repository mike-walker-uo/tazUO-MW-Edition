using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public sealed class LinearCompositeEffect : Effect
    {
        public LinearCompositeEffect(GraphicsDevice device, byte[] bytecode) : base(device, bytecode) { }
        public void Configure(Texture2D lights, bool useLights, bool altLights)
        {
            var viewport = GraphicsDevice.Viewport;
            Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, short.MinValue, short.MaxValue));
            Parameters["Viewport"].SetValue(new Vector2(viewport.Width, viewport.Height));
            Parameters["LightTexture"].SetValue(lights);
            Parameters["UseLights"].SetValue(useLights ? 1f : 0f);
            Parameters["AltLights"].SetValue(altLights ? 1f : 0f);
        }
    }
}

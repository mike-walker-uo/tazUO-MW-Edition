using System.Collections.Generic;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal static class AtmosphereTextures
    {
        private static readonly Dictionary<uint, Texture2D> Colors = new Dictionary<uint, Texture2D>();
        internal static Texture2D GetSolid(Color color)
        {
            // Animated light tints must not create a permanent texture for every intermediate RGB value.
            color.R = (byte)((color.R >> 3) * 255 / 31);
            color.G = (byte)((color.G >> 3) * 255 / 31);
            color.B = (byte)((color.B >> 3) * 255 / 31);
            uint key = color.PackedValue;
            if (!Colors.TryGetValue(key, out Texture2D texture) || texture.IsDisposed)
            {
                texture = new Texture2D(Client.Game.GraphicsDevice, 1, 1);
                texture.SetData(new[] { color });
                Colors[key] = texture;
                OptionalTextureCache.Register(texture, () => Colors.Remove(key));
            }
            return texture;
        }
    }
}

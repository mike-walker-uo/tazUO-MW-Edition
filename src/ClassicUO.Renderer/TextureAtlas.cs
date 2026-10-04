using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StbRectPackSharp;
using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer
{
    public class TextureAtlas : IDisposable
    {
        private const int SPRITE_PADDING = 2;
        private readonly int _width,
            _height;
        private readonly SurfaceFormat _format;
        private readonly GraphicsDevice _device;
        private readonly List<Texture2D> _textureList;
        private Packer _packer;

        public TextureAtlas(GraphicsDevice device, int width, int height, SurfaceFormat format)
        {
            _device = device;
            _width = width;
            _height = height;
            _format = format;

            _textureList = new List<Texture2D>();
        }

        public int TexturesCount => _textureList.Count;

        public unsafe Texture2D AddSprite(
            ReadOnlySpan<uint> pixels,
            int width,
            int height,
            out Rectangle pr
        )
        {
            if (width <= 0 || width > _width - SPRITE_PADDING)
                throw new ArgumentOutOfRangeException(nameof(width), "Sprite width must fit the atlas.");
            if (height <= 0 || height > _height - SPRITE_PADDING)
                throw new ArgumentOutOfRangeException(nameof(height), "Sprite height must fit the atlas.");
            if ((long)width * height > pixels.Length)
                throw new ArgumentException("Sprite pixel data is incomplete.", nameof(pixels));

            var index = _textureList.Count - 1;

            if (index < 0)
            {
                index = 0;
                CreateNewTexture2D();
            }

            while (!_packer.PackRect(width, height, out pr, SPRITE_PADDING))
            {
                CreateNewTexture2D();
                index = _textureList.Count - 1;
            }

            Texture2D texture = _textureList[index];

            fixed (uint* src = pixels)
            {
                texture.SetDataPointerEXT(0, pr, (IntPtr)src, sizeof(uint) * width * height);
            }

            return texture;
        }

        private void CreateNewTexture2D()
        {
            Utility.Logging.Log.Trace($"creating texture: {_width}x{_height} {_format}");
            Texture2D texture = new Texture2D(_device, _width, _height, false, _format);
            _textureList.Add(texture);

            _packer?.Dispose();
            _packer = new Packer(_width, _height);
        }

        public void SaveImages(string name)
        {
            for (int i = 0, count = TexturesCount; i < count; ++i)
            {
                var texture = _textureList[i];

                using (var stream = System.IO.File.Create($"atlas/{name}_atlas_{i}.png"))
                {
                    texture.SaveAsPng(stream, texture.Width, texture.Height);
                }
            }
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _textureList)
            {
                if (!texture.IsDisposed)
                {
                    texture.Dispose();
                }
            }

            _packer?.Dispose();
            _packer = null;
            _textureList.Clear();
        }
    }
}

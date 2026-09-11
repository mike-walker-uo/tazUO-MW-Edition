using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Threading.Tasks;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.UI.Controls
{
    public class ExternalUrlImage : Control
    {
        private Texture2D _texture;
        private bool _loading;
        private int _loadVersion;
        private Vector3 _hue;

        public ExternalUrlImage(string url, int width = 100, int height = 100)
        {
            Width = width;
            Height = height;
            _hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            LoadImageFromUrl(url);
        }

        public void LoadImageFromUrl(string url)
        {
            _loading = true;
            int version = ++_loadVersion;
            _ = LoadImageAsync(url, version);
        }

        private async Task LoadImageAsync(string url, int version)
        {
            byte[] data;
            try
            {
                data = await ExternalImageLoader.DownloadAsync(url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load image from URL '{url}': {ex}");
                MainThreadQueue.EnqueueAction(() =>
                {
                    if (version == _loadVersion) _loading = false;
                });
                return;
            }

            MainThreadQueue.EnqueueAction(() =>
            {
                if (IsDisposed || version != _loadVersion) return;

                try
                {
                    using var ms = new MemoryStream(data, false);
                    Texture2D texture = Texture2D.FromStream(Client.Game.GraphicsDevice, ms);
                    _texture?.Dispose();
                    _texture = texture;
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to create texture for URL '{url}': {ex}");
                }
                finally
                {
                    _loading = false;
                }
            });
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);

            if (_texture != null && !_loading)
            {
                batcher.Draw(_texture, new Rectangle(x, y, Width, Height), _texture.Bounds, _hue);
            }

            return true;
        }

        public override void Dispose()
        {
            _loadVersion++;
            _texture?.Dispose();
            _texture = null;
            base.Dispose();
        }
    }
}

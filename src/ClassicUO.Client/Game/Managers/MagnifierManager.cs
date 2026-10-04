using System;
using System.Globalization;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal static class MagnifierManager
    {
        internal const int LensRadius = 90;
        private static RenderTarget2D _screen;
        private static MagnifierEffect _effect;
        private static Texture2D _art;
        private static bool _enabled;
        internal static bool Capturing { get; private set; }
        internal static bool Active => _enabled;
        private static int Zoom => ClampZoom(ProfileManager.CurrentProfile?.MagnifierZoom ?? 2);

        internal static int ClampZoom(int zoom) => Math.Max(1, Math.Min(4, zoom));

        internal static void Disable() => _enabled = false;

        // Center and half-size in screen texture UVs. Never move the center at screen edges.
        internal static Vector4 SourceRegion(Point center, int zoom, int width, int height) =>
            new Vector4((float)center.X / width, (float)center.Y / height,
                (float)LensRadius / ClampZoom(zoom) / width, (float)LensRadius / ClampZoom(zoom) / height);

        internal static void Command(string[] args)
        {
            string action = args != null && args.Length > 1 ? args[1].ToLowerInvariant() : "toggle";
            if (args?.Length > 2 || !(action == "toggle" || action == "on" || action == "off"
                || action == "status" || action == "next" || action == "prev"
                || action == "1" || action == "2" || action == "3" || action == "4"))
            {
                GameActions.Print("Usage: -magnifier [on|off|toggle|1|2|3|4|next|prev|status]", 0x35);
                return;
            }
            if (!World.InGame || ProfileManager.CurrentProfile == null)
            {
                GameActions.Print("Log in to use the magnifying glass.", 0x35);
                return;
            }
            if (action == "toggle") _enabled = !_enabled;
            else if (action == "on") _enabled = true;
            else if (action == "off") _enabled = false;
            else if (action != "status")
            {
                ProfileManager.CurrentProfile.MagnifierZoom = action == "next" ? (Zoom == 4 ? 1 : Zoom + 1)
                    : action == "prev" ? (Zoom == 1 ? 4 : Zoom - 1) : int.Parse(action, CultureInfo.InvariantCulture);
                _enabled = true;
                if (!string.IsNullOrEmpty(ProfileManager.ProfilePath))
                    ProfileManager.CurrentProfile.Save(ProfileManager.ProfilePath, false, true);
            }
            GameActions.Print($"Magnifying glass {(_enabled ? "ON" : "OFF")}: {Zoom}x.", 0x35);
        }

        internal static void BeginFrame(GraphicsDevice device)
        {
            Capturing = false;
            if (!World.InGame) _enabled = false;
            // AA also needs a preserved frame: FNA discards the backbuffer on target changes.
            bool worldAA = World.InGame && VisualBudget.Settings?.WorldAntiAliasing == true
                && VisualBudget.Settings.WorldAntiAliasingStrength > 0;
            if (!_enabled && !worldAA)
            {
                Dispose();
                return;
            }
            if (_enabled && (_effect == null || _effect.IsDisposed))
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Magnifier.fxc");
                if (!File.Exists(path))
                {
                    _enabled = false;
                    Dispose();
                    GameActions.Print("Magnifier.fxc is missing. Rebuild or install the complete Windows client.", 0x35);
                    return;
                }
                _effect = new MagnifierEffect(device, File.ReadAllBytes(path));
            }
            if (_enabled && (_art == null || _art.IsDisposed))
            {
                using var stream = typeof(MagnifierManager).Assembly.GetManifestResourceStream("ClassicUO.MagnifierFrame.png");
                _art = Texture2D.FromStream(device, stream);
            }
            var pp = device.PresentationParameters;
            if (_screen == null || _screen.IsDisposed || _screen.Width != pp.BackBufferWidth || _screen.Height != pp.BackBufferHeight)
            {
                _screen?.Dispose();
                // Scene/light passes temporarily bind other targets; keep the completed screen content.
                _screen = new RenderTarget2D(device, pp.BackBufferWidth, pp.BackBufferHeight, false,
                    pp.BackBufferFormat, pp.DepthStencilFormat, 0, RenderTargetUsage.PreserveContents);
            }
            Capturing = true;
            RestoreScreenTarget(device);
        }

        internal static void RestoreScreenTarget(GraphicsDevice device) => device.SetRenderTarget(Capturing ? _screen : null);

        internal static void Present(UltimaBatcher2D batcher)
        {
            if (!Capturing) return;
            Capturing = false;
            batcher.GraphicsDevice.SetRenderTarget(null);
            Vector3 hue = new Vector3(0, 0, 1);
            batcher.SetSampler(SamplerState.PointClamp);
            // The completed frame's alpha may be changed by lighting passes.
            batcher.SetBlendState(BlendState.Opaque);
            batcher.Begin();
            batcher.Draw(_screen, _screen.Bounds, hue);
            batcher.End();
            batcher.SetBlendState(null);
            if (!_enabled) return;

            Point center = Mouse.WorldPosition;
            _effect.Configure(SourceRegion(center, Zoom, _screen.Width, _screen.Height));
            batcher.SetBlendState(BlendState.Opaque);
            batcher.SetSampler(ProfileManager.CurrentProfile?.MagnifierSmooth == true
                ? SamplerState.LinearClamp : SamplerState.PointClamp);
            batcher.Begin(_effect);
            batcher.Draw(_screen, new Rectangle(center.X - LensRadius, center.Y - LensRadius,
                LensRadius * 2, LensRadius * 2), _screen.Bounds, hue);
            batcher.End();

            batcher.SetBlendState(null);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Begin();
            // Artwork opening: center (531.5, 474), radii (365.5, 354) in the embedded 1254px sprite.
            Vector2 scale = new Vector2(LensRadius / 365.5f, LensRadius / 354f);
            Vector2 position = new Vector2(center.X - 531.5f * scale.X, center.Y - 474f * scale.Y);
            batcher.Draw(_art, position, _art.Bounds, hue, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            Texture2D black = SolidColorTextureCache.GetTexture(Color.Black);
            Texture2D white = SolidColorTextureCache.GetTexture(Color.White);
            batcher.Draw(black, new Rectangle(center.X - 7, center.Y - 1, 15, 3), hue);
            batcher.Draw(black, new Rectangle(center.X - 1, center.Y - 7, 3, 15), hue);
            batcher.Draw(white, new Rectangle(center.X - 5, center.Y, 11, 1), hue);
            batcher.Draw(white, new Rectangle(center.X, center.Y - 5, 1, 11), hue);
            batcher.End();
            batcher.SetSampler(null);
        }

        internal static void Dispose()
        {
            Capturing = false;
            _enabled = false;
            _screen?.Dispose(); _screen = null;
            _effect?.Dispose(); _effect = null;
            _art?.Dispose(); _art = null;
        }
    }
}

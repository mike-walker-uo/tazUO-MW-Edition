#region license
// TazUO addition.
#endregion

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Slow horizontal-drifting pixel particles overlaid on the viewport,
    /// approximating wind. Pure cosmetic — does NOT consult tile region tags
    /// (yet), so it runs everywhere when enabled. `-wind on|off`.
    /// </summary>
    public static class WindParticlesOverlay
    {
        public static bool Enabled = false;
        private const int PARTICLE_COUNT = 36;

        private struct Particle { public float X, Y, Vx, A; }
        private static readonly Particle[] _p = new Particle[PARTICLE_COUNT];
        private static readonly System.Random _rng = new System.Random(0x57E1);
        private static bool _inited;
        private static int _lastVw, _lastVh;

        private static void Init(int vw, int vh)
        {
            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                _p[i].X = (float)(_rng.NextDouble() * vw);
                _p[i].Y = (float)(_rng.NextDouble() * vh);
                _p[i].Vx = 0.4f + (float)_rng.NextDouble() * 1.2f;
                _p[i].A = 0.2f + (float)_rng.NextDouble() * 0.5f;
            }
            _inited = true;
            _lastVw = vw; _lastVh = vh;
        }

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle viewport)
        {
            if (!Enabled) return;
            int vw = viewport.Width, vh = viewport.Height;
            if (!_inited || vw != _lastVw || vh != _lastVh) Init(vw, vh);

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 255, 255, 255));

            for (int i = 0; i < PARTICLE_COUNT; i++)
            {
                ref var p = ref _p[i];
                p.X += p.Vx;
                if (p.X > vw)
                {
                    p.X = -4;
                    p.Y = (float)(_rng.NextDouble() * vh);
                    p.A = 0.2f + (float)_rng.NextDouble() * 0.5f;
                }
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, p.A);
                batcher.Draw(tex, new Rectangle(viewport.X + (int)p.X, viewport.Y + (int)p.Y, 2, 1), hue);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Wind particles {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

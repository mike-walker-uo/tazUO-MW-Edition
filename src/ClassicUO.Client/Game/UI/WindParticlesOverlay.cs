#region license
// TazUO addition.
#endregion

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Utility;
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
        private static uint _lastTick;

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
            if (!Enabled) { _lastTick = 0; return; }
            int vw = viewport.Width, vh = viewport.Height;
            if (vw <= 0 || vh <= 0) return;
            uint now = Time.Ticks;
            float frameScale = Math.Min(100u, _lastTick == 0 ? 16u : now - _lastTick) * 0.06f;
            _lastTick = now;
            if (ProfileManager.CurrentProfile?.ReduceWeatherMotion == true) frameScale *= 0.35f;
            if (!_inited || vw != _lastVw || vh != _lastVh) Init(vw, vh);

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 255, 255, 255));

            int count = Math.Min(PARTICLE_COUNT, SceneryInteractionManager.ScaleCount(PARTICLE_COUNT));
            for (int i = 0; i < count; i++)
            {
                ref var p = ref _p[i];
                p.X += p.Vx * frameScale * SceneryInteractionManager.SharedWind;
                if (p.X < -4) p.X = vw;
                if (p.X > vw)
                {
                    p.X = -4;
                    p.Y = (float)(_rng.NextDouble() * vh);
                    p.A = 0.2f + (float)_rng.NextDouble() * 0.5f;
                }
                float flutter = (float)Math.Sin(now / 1300f + i * 1.7f);
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, p.A * (0.65f + flutter * 0.15f));
                int length = 2 + i % 3;
                batcher.Draw(tex, new Rectangle(viewport.X + (int)p.X, viewport.Y + (int)(p.Y + flutter * 3f), length, 1), hue);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            _lastTick = 0;
            GameActions.Print($"Wind particles {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

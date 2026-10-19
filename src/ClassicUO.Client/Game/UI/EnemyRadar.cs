using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    // Compact, ground-projected radar. Contacts come only from the shared mobile cache.
    public static class EnemyRadar
    {
        private const int SECTORS = 16;
        private const int CELL = 128;
        private const float FLATTEN = 0.5f;
        private static readonly Color Amber = new Color(245, 190, 90);
        private static readonly Color Coral = new Color(245, 110, 100);
        private static readonly List<Mobile> _contacts = new List<Mobile>(64);
        private static readonly int[] _counts = new int[SECTORS];
        private static readonly float[] _nearest = new float[SECTORS];
        private static readonly Vector2[] _directions = new Vector2[SECTORS];
        private static Texture2D _ring, _ringEdge, _dot, _sectors, _beam;
        private static long _nextRefresh;
        private static int _style, _size = 220, _opacity = 50, _range = Constants.MAX_VIEW_RANGE;

        public static bool Enabled = false;
        public static bool IncludeNeutral = true;
        public static int Style { get => _style; set => _style = Math.Max(0, Math.Min(2, value)); }
        public static int Size { get => _size; set => _size = Math.Max(100, Math.Min(400, value)); }
        public static int Opacity { get => _opacity; set => _opacity = Math.Max(5, Math.Min(100, value)); }
        public static int Range { get => _range; set => _range = Math.Max(1, Math.Min(Constants.MAX_VIEW_RANGE, value)); }

        internal static bool IsHostile(NotorietyFlag flag, bool includeNeutral) =>
            flag == NotorietyFlag.Enemy || flag == NotorietyFlag.Murderer ||
            flag == NotorietyFlag.Criminal || includeNeutral && flag == NotorietyFlag.Gray;

        internal static bool Eligible(Mobile m, int range) =>
            m != null && m != World.Player && !m.IsDestroyed && !m.IsDead && !m.IsHidden &&
            !m.InParty && !m.IsRenamable && m.Distance <= range && IsHostile(m.NotorietyFlag, IncludeNeutral);

        // Keep UO's square tile-range semantics, compressing distance into a round radar.
        // Z is intentionally excluded: a hill must not change an enemy's compass direction.
        internal static Vector2 Project(float dx, float dy, int range)
        {
            Vector2 direction = new Vector2(dx - dy, dx + dy);
            float length = direction.Length();
            if (length < 0.001f || range <= 0) return Vector2.Zero;
            float distance = Math.Min(1f, Math.Max(Math.Abs(dx), Math.Abs(dy)) / range);
            return direction * (distance / length);
        }

        internal static float BeamFade(float progress) =>
            progress < 0f || progress >= 1f ? 0f : (1f - progress) * (1f - progress);

        private static Vector2 Relative(Mobile m, int range)
        {
            Mobile player = World.Player;
            float sx = m.Offset.X - player.Offset.X;
            float sy = m.Offset.Y - player.Offset.Y;
            return Project(m.X - player.X + (sx + sy) / 44f,
                m.Y - player.Y + (sy - sx) / 44f, range);
        }

        public static void Draw(UltimaBatcher2D batcher)
        {
            if (!Enabled || !World.InGame || World.Player == null || World.Player.IsDead || ExplorationMode.Active) return;
            int range = Math.Min(Range, World.ClientViewRange);
            if (range < 1) return;

            if ((long)Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = (long)Time.Ticks + 50;
                _contacts.Clear();
                foreach (Mobile m in MobileCache.Hostiles)
                    if (Eligible(m, range)) _contacts.Add(m);
                _contacts.Sort(CompareDistance);
            }

            Array.Clear(_counts, 0, SECTORS);
            Array.Clear(_directions, 0, SECTORS);
            for (int i = 0; i < SECTORS; i++) _nearest[i] = 1f;
            int visible = 0;
            foreach (Mobile m in _contacts)
            {
                // A cached entity may disappear, die or change notoriety before the next snapshot.
                if (!Eligible(m, range)) continue;
                Vector2 p = Relative(m, range);
                float distance = p.Length();
                if (distance < 0.001f) continue;
                int sector = Sector(p);
                _counts[sector]++;
                _nearest[sector] = Math.Min(_nearest[sector], distance);
                _directions[sector] += p / distance;
                visible++;
            }
            if (visible == 0) return;

            EnsureTextures(batcher.GraphicsDevice);
            // Match Mobile.Draw's tile center, movement offset and three-pixel foot correction.
            Mobile player = World.Player;
            Vector2 center = new Vector2(player.RealScreenPosition.X + player.Offset.X + 22,
                player.RealScreenPosition.Y + player.Offset.Y - player.Offset.Z + 19);
            float radius = Size * 0.5f;
            float opacity = Opacity / 100f;
            const float depth = 0f;

            batcher.SetStencil(DepthStencilState.None);
            try
            {
                if (Style == 0)
                {
                    Vector2 ringSize = new Vector2(Size, Size * FLATTEN);
                    DrawCentered(batcher, _ringEdge, center, ringSize, Tint(Color.Black, opacity * 0.6f), depth);
                    DrawCentered(batcher, _ring, center, ringSize, Tint(new Color(241, 204, 142), opacity), depth);
                    int dots = 0;
                    foreach (Mobile m in _contacts)
                    {
                        if (!Eligible(m, range)) continue;
                        if (dots++ >= 64) break;
                        Vector2 p = Relative(m, range);
                        Vector2 position = center + new Vector2(p.X, p.Y * FLATTEN) * (radius * 0.88f);
                        bool target = m.Serial == TargetManager.LastAttack;
                        DrawCentered(batcher, _dot, position, new Vector2(target ? 11f : 8f),
                            Tint(Color.Black, opacity * 0.8f), depth);
                        if (target)
                            DrawCentered(batcher, _dot, position, new Vector2(9f),
                                Tint(new Color(231, 220, 191), opacity), depth);
                        DrawCentered(batcher, _dot, position, new Vector2(5.5f),
                            Tint(p.Length() < 0.35f ? Coral : Amber, opacity), depth);
                    }
                    return;
                }

                for (int i = 0; i < SECTORS; i++)
                {
                    if (_counts[i] == 0) continue;
                    Color color = _nearest[i] < 0.35f || _counts[i] >= 4 ? Coral : Amber;
                    float strength = Math.Min(1f, 0.6f + _counts[i] * 0.1f);
                    if (Style == 1)
                    {
                        var source = new Rectangle(i % 4 * CELL, i / 4 * CELL, CELL, CELL);
                        batcher.Draw(_sectors, center - new Vector2(radius, radius * FLATTEN), source,
                            Tint(color, opacity * strength * 0.9f), 0f,
                            new Vector2(Size / (float)CELL, Size * FLATTEN / CELL), depth);
                    }
                    else
                    {
                        // One fading thread per occupied direction keeps a crowd from filling the screen.
                        Vector2 direction = Vector2.Normalize(_directions[i]);
                        Vector2 end = center + new Vector2(direction.X, direction.Y * FLATTEN)
                            * (radius * (0.55f + 0.45f * _nearest[i]));
                        DrawBeam(batcher, center, end, 7f, Tint(Color.Black, opacity * strength * 0.6f));
                        DrawBeam(batcher, center, end, 4.5f, Tint(color, opacity * strength));
                    }
                }
            }
            finally
            {
                batcher.SetStencil(DepthStencilState.Default);
            }
        }

        private static Color Tint(Color color, float opacity) =>
            new Color(color.R, color.G, color.B, (byte)(MathHelper.Clamp(opacity, 0f, 1f) * 255));

        private static int CompareDistance(Mobile a, Mobile b) => a.Distance.CompareTo(b.Distance);

        private static int Sector(Vector2 p) =>
            ((int)Math.Round(Math.Atan2(p.Y, p.X) * SECTORS / MathHelper.TwoPi) + SECTORS) % SECTORS;

        private static void DrawCentered(UltimaBatcher2D batcher, Texture2D texture,
            Vector2 center, Vector2 size, Color color, float depth) =>
            batcher.Draw(texture, center - size * 0.5f, null, color, 0f,
                new Vector2(size.X / texture.Width, size.Y / texture.Height), depth);

        private static void DrawBeam(UltimaBatcher2D batcher, Vector2 center, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - center;
            float length = delta.Length();
            if (length < 1f) return;
            Vector2 start = center + new Vector2(delta.Y, -delta.X) * (width * 0.5f / length);
            batcher.Draw(_beam, start, null, color, (float)Math.Atan2(delta.Y, delta.X),
                new Vector2(length / _beam.Width, width / _beam.Height), 0f);
        }

        private static void EnsureTextures(GraphicsDevice device)
        {
            if (_ring != null && !_ring.IsDisposed) return;
            _ring = CreateTexture(device, 256, 256, (x, y) =>
            {
                float r = new Vector2(x - 127.5f, y - 127.5f).Length();
                return Math.Max(0f, 1f - Math.Abs(r - 126f) / 1.3f);
            });
            _ringEdge = CreateTexture(device, 256, 256, (x, y) =>
            {
                float r = new Vector2(x - 127.5f, y - 127.5f).Length();
                return Math.Max(0f, 1f - Math.Abs(r - 126f) / 2.5f);
            });
            _dot = CreateTexture(device, 16, 16, (x, y) =>
                MathHelper.Clamp(7f - new Vector2(x - 7.5f, y - 7.5f).Length(), 0f, 1f));
            _beam = CreateTexture(device, 128, 16, (x, y) =>
                BeamFade(x / 127f) * (float)Math.Exp(-Math.Pow((y - 7.5f) / 4f, 2)));
            _sectors = CreateTexture(device, CELL * 4, CELL * 4, (x, y) =>
            {
                int sector = x / CELL + y / CELL * 4;
                float px = (x % CELL + 0.5f) / (CELL * 0.5f) - 1f;
                float py = (y % CELL + 0.5f) / (CELL * 0.5f) - 1f;
                float r = (float)Math.Sqrt(px * px + py * py);
                if (r >= 1f) return 0f;
                float delta = MathHelper.WrapAngle((float)Math.Atan2(py, px) - sector * MathHelper.TwoPi / SECTORS);
                float angular = Math.Max(0f, 1f - Math.Abs(delta) / (MathHelper.TwoPi / SECTORS * 1.5f));
                float radial = 4f * r * (1f - r);
                return angular * angular * radial * radial;
            });
        }

        private static Texture2D CreateTexture(GraphicsDevice device, int width, int height, Func<int, int, float> coverage)
        {
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    byte a = (byte)(MathHelper.Clamp(coverage(x, y), 0f, 1f) * 255);
                    pixels[y * width + x] = new Color(a, a, a, a); // Premultiplied white mask.
                }
            var texture = new Texture2D(device, width, height);
            texture.SetData(pixels);
            return texture;
        }

        public static void ResetSession()
        {
            _contacts.Clear();
            _nextRefresh = 0;
            _ring?.Dispose(); _ringEdge?.Dispose(); _dot?.Dispose(); _sectors?.Dispose(); _beam?.Dispose();
            _ring = _ringEdge = _dot = _sectors = _beam = null;
        }
    }
}

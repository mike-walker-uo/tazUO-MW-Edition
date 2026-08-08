#region license
// TazUO addition.
#endregion

using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Shows an upright holy sword above the player when the server confirms
    /// Consecrate Weapon by adding its buff.
    /// </summary>
    public static class ConsecrateWeaponEffect
    {
        private const int TEXTURE_WIDTH = 72;
        private const int TEXTURE_HEIGHT = 136;
        private const long LIFETIME_MS = 1650;

        private static bool _hooked;
        private static long _triggeredAt;
        private static Texture2D _sword;
        private static Texture2D _swordGlow;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OnBuffAdded += OnBuffAdded;
            _hooked = true;
        }

        public static void ResetSession()
        {
            _triggeredAt = 0;
        }

        internal static bool IsConsecrateWeapon(BuffIconType type)
        {
            return type == BuffIconType.ConsecrateWeapon;
        }

        private static void OnBuffAdded(object sender, BuffEventArgs e)
        {
            if (e?.Buff == null || !IsConsecrateWeapon(e.Buff.Type)) return;
            if (
                !SpellAbilityEffectSettings.IsEnabled(
                    SpellAbilityEffectId.ConsecrateWeapon
                )
            )
            {
                return;
            }

            _triggeredAt = (long)Time.Ticks;
        }

        internal static void Preview()
        {
            _triggeredAt = (long)Time.Ticks;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled ||
                _triggeredAt == 0 || World.Player == null || !World.InGame) return;

            long age = (long)Time.Ticks - _triggeredAt;
            if (age < 0 || age >= LIFETIME_MS)
            {
                _triggeredAt = 0;
                return;
            }

            EnsureTextures();
            if (_sword == null || _swordGlow == null) return;

            float progress = age / (float)LIFETIME_MS;
            float appear = Math.Min(1f, progress / 0.18f);
            float fade = progress < 0.7f
                ? 1f
                : Math.Max(0f, (1f - progress) / 0.3f);
            float pulse = 0.94f + 0.08f *
                (float)Math.Sin(progress * MathHelper.TwoPi * 3.2f);
            float scale =
                (0.7f + 0.3f * EaseOut(appear))
                * pulse
                * OverheadEffectSizeSettings.Scale;
            float descent = 46f * (1f - EaseOut(appear));
            float rise = 7f * EaseOut(progress);

            Point ground = PathPreview.TileToScreen(
                World.Player.X,
                World.Player.Y,
                World.Player.Z
            );
            Vector2 center = new Vector2(
                ground.X,
                ground.Y
                    - 148f
                    - rise
                    - descent
                    - OverheadEffectSizeSettings.VerticalLift
            );
            Vector2 origin = new Vector2(
                TEXTURE_WIDTH * 0.5f,
                TEXTURE_HEIGHT * 0.5f
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawConsecrationAura(batcher, center, progress, fade, scale);
            batcher.Draw(
                _swordGlow,
                center,
                _swordGlow.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade * 0.82f),
                0f,
                origin,
                new Vector2(scale * 1.08f),
                SpriteEffects.None,
                0f
            );

            batcher.SetBlendState(BlendState.AlphaBlend);
            batcher.Draw(
                _sword,
                center,
                _sword.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade),
                0f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawMotes(batcher, center, progress, fade, scale);
            batcher.SetBlendState(BlendState.AlphaBlend);
        }

        private static void DrawConsecrationAura(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color blue = new Color(133, 178, 255);
            Color white = new Color(245, 249, 255);
            Color gold = new Color(255, 215, 105);
            float flash = Math.Max(0f, 1f - progress / 0.32f);
            float ringRadius = (27f + progress * 12f) * scale;

            for (int i = 0; i < 20; i++)
            {
                if (i % 5 == 3) continue;
                float a0 = i * MathHelper.TwoPi / 20f + progress * 1.3f;
                float a1 = (i + 0.72f) * MathHelper.TwoPi / 20f + progress * 1.3f;
                CombatVisualEffect.DrawLine(
                    batcher,
                    Ellipse(center + new Vector2(0f, 10f), ringRadius, ringRadius * 0.36f, a0),
                    Ellipse(center + new Vector2(0f, 10f), ringRadius, ringRadius * 0.36f, a1),
                    i % 4 == 0 ? gold : blue,
                    fade * (0.32f + flash * 0.35f),
                    i % 4 == 0 ? 2.2f : 1.3f,
                    0f
                );
            }

            if (flash <= 0.01f) return;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * MathHelper.TwoPi / 8f;
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    center + direction * 25f,
                    center + direction * (42f + progress * 42f),
                    i % 2 == 0 ? white : gold,
                    flash * fade * 0.72f,
                    i % 2 == 0 ? 2.2f : 1.2f,
                    0f
                );
            }
        }

        private static void DrawMotes(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color blue = new Color(122, 177, 255);
            Color gold = new Color(255, 218, 112);

            for (int i = 0; i < 12; i++)
            {
                float phase = progress * 5.2f + i * 1.73f;
                float radius = (24f + i % 4 * 7f) * scale;
                Vector2 mote = center + new Vector2(
                    (float)Math.Sin(phase) * radius,
                    45f - ((progress * 115f + i * 19f) % 104f)
                );
                float twinkle = 0.52f + 0.48f * (float)Math.Sin(phase * 2.4f);
                CombatVisualEffect.DrawPoint(
                    batcher,
                    mote,
                    i % 3 == 0 ? gold : blue,
                    fade * Math.Max(0.12f, twinkle),
                    i % 4 == 0 ? 4.2f : 2.5f,
                    0f
                );
            }
        }

        private static Vector2 Ellipse(
            Vector2 center,
            float radiusX,
            float radiusY,
            float angle)
        {
            return center + new Vector2(
                (float)Math.Cos(angle) * radiusX,
                (float)Math.Sin(angle) * radiusY
            );
        }

        private static void EnsureTextures()
        {
            if (_sword != null && !_sword.IsDisposed &&
                _swordGlow != null && !_swordGlow.IsDisposed)
            {
                return;
            }

            if (Client.Game?.GraphicsDevice == null) return;

            Color[] swordPixels = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            bool[] mask = new bool[swordPixels.Length];

            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    Color color = GetSwordColor(x, y);
                    int index = y * TEXTURE_WIDTH + x;
                    swordPixels[index] = color;
                    mask[index] = color.A != 0;
                }
            }

            Color[] glowPixels = new Color[swordPixels.Length];
            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    int nearest = 7;
                    for (int oy = -6; oy <= 6; oy++)
                    {
                        int sy = y + oy;
                        if (sy < 0 || sy >= TEXTURE_HEIGHT) continue;

                        for (int ox = -6; ox <= 6; ox++)
                        {
                            int sx = x + ox;
                            if (sx < 0 || sx >= TEXTURE_WIDTH) continue;
                            if (!mask[sy * TEXTURE_WIDTH + sx]) continue;
                            int distance = Math.Abs(ox) + Math.Abs(oy);
                            if (distance < nearest) nearest = distance;
                        }
                    }

                    if (nearest <= 6)
                    {
                        int strength = 18 + (6 - nearest) * 21;
                        glowPixels[y * TEXTURE_WIDTH + x] =
                            new Color(86, 143, 255, (byte)strength);
                    }
                }
            }

            _sword = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _sword.SetData(swordPixels);
            _swordGlow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _swordGlow.SetData(glowPixels);
        }

        private static Color GetSwordColor(int x, int y)
        {
            int center = TEXTURE_WIDTH / 2;
            int dx = Math.Abs(x - center);

            if (y >= 5 && y <= 86)
            {
                int halfWidth = y < 19 ? Math.Max(1, (y - 3) / 2) : 8;
                if (dx <= halfWidth)
                {
                    if (dx >= halfWidth - 1)
                        return new Color(76, 91, 148, 255);
                    if (x < center)
                        return new Color(132, 151, 214, 255);
                    if (x == center)
                        return new Color(235, 241, 255, 255);
                    return new Color(171, 188, 235, 255);
                }
            }

            if (y >= 84 && y <= 99)
            {
                int guardHalf = 30 - Math.Abs(y - 91) * 3;
                if (guardHalf >= 8 && dx <= guardHalf)
                {
                    if (y <= 87 && dx > 20) return Color.Transparent;
                    if (dx >= guardHalf - 2)
                        return new Color(134, 151, 204, 255);
                    return new Color(205, 216, 247, 255);
                }
            }

            if (y >= 96 && y <= 121 && dx <= 5)
            {
                if (dx >= 4) return new Color(45, 31, 30, 255);
                return x < center
                    ? new Color(78, 48, 42, 255)
                    : new Color(112, 72, 55, 255);
            }

            if (y >= 119 && y <= 132)
            {
                int pommelHalf = 8 - Math.Abs(y - 125);
                if (pommelHalf >= 1 && dx <= pommelHalf)
                {
                    return dx >= pommelHalf - 1
                        ? new Color(105, 121, 174, 255)
                        : new Color(202, 215, 247, 255);
                }
            }

            return Color.Transparent;
        }

        private static float EaseOut(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            float inverse = 1f - value;
            return 1f - inverse * inverse;
        }
    }
}

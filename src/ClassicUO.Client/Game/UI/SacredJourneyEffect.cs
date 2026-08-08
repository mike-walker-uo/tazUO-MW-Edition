#region license
// TazUO addition.
#endregion

using System;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Shows a rearing horse above the player when Sacred Journey is confirmed
    /// by its server-sent travel sound.
    /// </summary>
    public static class SacredJourneyEffect
    {
        internal const int SACRED_JOURNEY_SPELL_ID = 210;
        internal const ushort SACRED_JOURNEY_SOUND = 0x01FC;

        private const int TEXTURE_WIDTH = 120;
        private const int TEXTURE_HEIGHT = 136;
        private const long PENDING_MS = 60000;
        private const long LIFETIME_MS = 1750;

        private static bool _hooked;
        private static long _pendingUntil;
        private static long _triggeredAt;
        private static Texture2D _horse;
        private static Texture2D _horseGlow;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.SpellCastBegin += OnSpellCastBegin;
            _hooked = true;
        }

        public static void ResetSession()
        {
            _pendingUntil = 0;
            _triggeredAt = 0;
        }

        internal static bool IsSacredJourney(int spell)
        {
            return spell == SACRED_JOURNEY_SPELL_ID;
        }

        internal static bool IsTravelConfirmation(ushort sound)
        {
            return sound == SACRED_JOURNEY_SOUND;
        }

        private static void OnSpellCastBegin(object sender, int spell)
        {
            ObserveCastRequest(spell);
        }

        internal static void ObserveCastRequest(int spell)
        {
            _pendingUntil = IsSacredJourney(spell)
                ? (long)Time.Ticks + PENDING_MS
                : 0;
        }

        internal static void OnSound(ushort sound, ushort x, ushort y)
        {
            long now = (long)Time.Ticks;
            if (_pendingUntil == 0 || now > _pendingUntil)
            {
                _pendingUntil = 0;
                return;
            }

            if (!IsTravelConfirmation(sound) || World.Player == null) return;

            _pendingUntil = 0;
            if (
                !SpellAbilityEffectSettings.IsEnabled(
                    SpellAbilityEffectId.SacredJourney
                )
            )
            {
                return;
            }

            _triggeredAt = now;
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
            if (_horse == null || _horseGlow == null) return;

            float progress = age / (float)LIFETIME_MS;
            float appear = Math.Min(1f, progress / 0.14f);
            float fade = progress < 0.68f
                ? 1f
                : Math.Max(0f, (1f - progress) / 0.32f);
            float pulse = 0.96f + 0.055f *
                (float)Math.Sin(progress * MathHelper.TwoPi * 3.2f);
            float scale =
                (0.68f + 0.34f * EaseOut(appear))
                * pulse
                * OverheadEffectSizeSettings.Scale;
            float rise = 13f * EaseOut(progress);
            float sway = 0.025f *
                (float)Math.Sin(progress * MathHelper.TwoPi * 2.2f);

            Point ground = PathPreview.TileToScreen(
                World.Player.X,
                World.Player.Y,
                World.Player.Z
            );
            Vector2 center = new Vector2(
                ground.X,
                ground.Y - 151f - rise - OverheadEffectSizeSettings.VerticalLift
            );
            Vector2 origin = new Vector2(
                TEXTURE_WIDTH * 0.5f,
                TEXTURE_HEIGHT * 0.5f
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawJourneyHalo(batcher, center, progress, fade, scale);
            batcher.Draw(
                _horseGlow,
                center,
                _horseGlow.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade * 0.9f),
                sway,
                origin,
                new Vector2(scale * 1.12f),
                SpriteEffects.None,
                0f
            );

            batcher.SetBlendState(BlendState.AlphaBlend);
            batcher.Draw(
                _horse,
                center,
                _horse.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade),
                sway,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawHoofSparks(batcher, center, progress, fade, scale);
            batcher.SetBlendState(BlendState.AlphaBlend);
        }

        private static void DrawJourneyHalo(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color white = new Color(255, 249, 221);
            Color gold = new Color(255, 193, 72);
            Color blue = new Color(117, 177, 255);
            float burst = Math.Max(0f, 1f - progress / 0.34f) * fade;

            for (int i = 0; i < 12; i++)
            {
                float angle = i * MathHelper.TwoPi / 12f + progress * 1.5f;
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                float inner = 47f * scale;
                float outer = (60f + progress * 45f) * scale;
                CombatVisualEffect.DrawLine(
                    batcher,
                    center + direction * inner,
                    center + direction * outer,
                    i % 3 == 0 ? white : i % 2 == 0 ? blue : gold,
                    burst * (i % 3 == 0 ? 0.8f : 0.52f),
                    i % 3 == 0 ? 2.4f : 1.4f,
                    0f
                );
            }
        }

        private static void DrawHoofSparks(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color white = new Color(255, 252, 226);
            Color gold = new Color(255, 196, 78);
            Color blue = new Color(105, 169, 255);

            for (int i = 0; i < 18; i++)
            {
                float phase = progress * 13f + i * 1.71f;
                float travel = (progress * 1.8f + i * 0.131f) % 1f;
                Vector2 spark = center + new Vector2(
                    (float)Math.Sin(phase) * (24f + travel * 44f) * scale,
                    54f - travel * 115f
                );
                Color color = i % 5 == 0 ? white : i % 2 == 0 ? gold : blue;
                CombatVisualEffect.DrawPoint(
                    batcher,
                    spark,
                    color,
                    fade * (0.3f + 0.7f * (1f - travel)),
                    i % 5 == 0 ? 4f : 2.4f,
                    0f
                );
            }
        }

        private static void EnsureTextures()
        {
            if (_horse != null && !_horse.IsDisposed &&
                _horseGlow != null && !_horseGlow.IsDisposed)
            {
                return;
            }

            if (Client.Game?.GraphicsDevice == null) return;

            Color[] pixels = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            bool[] mask = new bool[pixels.Length];

            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    int index = y * TEXTURE_WIDTH + x;
                    mask[index] = InsideHorse(x, y);
                }
            }

            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    int index = y * TEXTURE_WIDTH + x;
                    if (!mask[index]) continue;

                    bool edge =
                        !IsMasked(mask, x - 2, y) ||
                        !IsMasked(mask, x + 2, y) ||
                        !IsMasked(mask, x, y - 2) ||
                        !IsMasked(mask, x, y + 2);
                    pixels[index] = edge
                        ? new Color(55, 39, 21, 255)
                        : x < 61
                            ? new Color(184, 126, 43, 255)
                            : new Color(244, 201, 100, 255);
                }
            }

            Color[] glowPixels = new Color[pixels.Length];
            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    int nearest = 8;
                    for (int oy = -7; oy <= 7; oy++)
                    {
                        for (int ox = -7; ox <= 7; ox++)
                        {
                            if (!IsMasked(mask, x + ox, y + oy)) continue;
                            nearest = Math.Min(nearest, Math.Abs(ox) + Math.Abs(oy));
                        }
                    }

                    if (nearest <= 7)
                    {
                        int strength = 13 + (7 - nearest) * 18;
                        glowPixels[y * TEXTURE_WIDTH + x] =
                            new Color(255, 183, 62, (byte)strength);
                    }
                }
            }

            _horse = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _horse.SetData(pixels);
            _horseGlow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _horseGlow.SetData(glowPixels);
        }

        private static bool InsideHorse(int x, int y)
        {
            Vector2 p = new Vector2(x, y);

            bool body = InRotatedEllipse(p, new Vector2(54f, 78f), 34f, 20f, -0.34f);
            bool chest = InRotatedEllipse(p, new Vector2(70f, 68f), 20f, 23f, -0.2f);
            bool neck =
                NearSegment(p, new Vector2(68f, 70f), new Vector2(77f, 38f), 13f) ||
                NearSegment(p, new Vector2(76f, 48f), new Vector2(86f, 28f), 11f);
            bool head = InRotatedEllipse(p, new Vector2(90f, 27f), 18f, 10f, 0.24f);
            bool muzzle = InRotatedEllipse(p, new Vector2(103f, 33f), 11f, 7f, 0.16f);
            bool ears =
                InTriangle(p, new Vector2(79f, 20f), new Vector2(80f, 6f), new Vector2(87f, 20f)) ||
                InTriangle(p, new Vector2(86f, 19f), new Vector2(91f, 8f), new Vector2(94f, 23f));
            bool hindLegs =
                NearSegment(p, new Vector2(42f, 88f), new Vector2(34f, 119f), 7f) ||
                NearSegment(p, new Vector2(56f, 91f), new Vector2(55f, 125f), 7f) ||
                InRotatedEllipse(p, new Vector2(37f, 124f), 10f, 5f, 0.1f) ||
                InRotatedEllipse(p, new Vector2(59f, 129f), 10f, 5f, 0.1f);
            bool raisedLegs =
                NearSegment(p, new Vector2(72f, 69f), new Vector2(91f, 61f), 6f) ||
                NearSegment(p, new Vector2(91f, 61f), new Vector2(106f, 72f), 5f) ||
                NearSegment(p, new Vector2(73f, 77f), new Vector2(89f, 82f), 6f) ||
                NearSegment(p, new Vector2(89f, 82f), new Vector2(99f, 99f), 5f);
            bool tail =
                NearSegment(p, new Vector2(27f, 77f), new Vector2(14f, 91f), 5f) ||
                NearSegment(p, new Vector2(15f, 90f), new Vector2(9f, 111f), 4f) ||
                NearSegment(p, new Vector2(18f, 92f), new Vector2(18f, 117f), 3f) ||
                NearSegment(p, new Vector2(20f, 92f), new Vector2(28f, 115f), 3f);
            bool mane =
                InTriangle(p, new Vector2(69f, 54f), new Vector2(56f, 49f), new Vector2(70f, 61f)) ||
                InTriangle(p, new Vector2(72f, 46f), new Vector2(59f, 39f), new Vector2(74f, 53f)) ||
                InTriangle(p, new Vector2(77f, 37f), new Vector2(64f, 29f), new Vector2(81f, 44f));

            return body || chest || neck || head || muzzle || ears ||
                hindLegs || raisedLegs || tail || mane;
        }

        private static bool InRotatedEllipse(
            Vector2 point,
            Vector2 center,
            float radiusX,
            float radiusY,
            float rotation)
        {
            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            float rx = dx * cos + dy * sin;
            float ry = -dx * sin + dy * cos;
            return rx * rx / (radiusX * radiusX) +
                ry * ry / (radiusY * radiusY) <= 1f;
        }

        private static bool NearSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end,
            float radius)
        {
            Vector2 line = end - start;
            float lengthSquared = line.LengthSquared();
            if (lengthSquared <= 0.001f) return Vector2.Distance(point, start) <= radius;
            float amount = Vector2.Dot(point - start, line) / lengthSquared;
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Vector2.Distance(point, start + line * amount) <= radius;
        }

        private static bool InTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(point, a, b);
            float d2 = Cross(point, b, c);
            float d3 = Cross(point, c, a);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }

        private static float Cross(Vector2 point, Vector2 a, Vector2 b)
        {
            return (point.X - b.X) * (a.Y - b.Y) -
                (a.X - b.X) * (point.Y - b.Y);
        }

        private static bool IsMasked(bool[] mask, int x, int y)
        {
            return x >= 0 && y >= 0 && x < TEXTURE_WIDTH && y < TEXTURE_HEIGHT &&
                mask[y * TEXTURE_WIDTH + x];
        }

        private static float EaseOut(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            float inverse = 1f - value;
            return 1f - inverse * inverse;
        }
    }
}

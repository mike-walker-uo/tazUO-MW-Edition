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
    /// Shows a gold ankh wrapped by a rotating holy tornado when the server
    /// confirms Divine Fury by adding its buff.
    /// </summary>
    public static partial class DivineFuryEffect
    {
        private const int TEXTURE_WIDTH = 76;
        private const int TEXTURE_HEIGHT = 116;
        private const long LIFETIME_MS = 1850;

        private static bool _hooked;
        private static long _triggeredAt;
        private static Texture2D _ankh;
        private static Texture2D _ankhGlow;

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

        internal static bool IsDivineFury(BuffIconType type)
        {
            return type == BuffIconType.DivineFury;
        }

        private static void OnBuffAdded(object sender, BuffEventArgs e)
        {
            if (e?.Buff == null || !IsDivineFury(e.Buff.Type)) return;
            if (
                !SpellAbilityEffectSettings.IsEnabled(
                    SpellAbilityEffectId.DivineFury
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
            using var presentation = EffectPresentation.For(SpellAbilityEffectId.DivineFury);
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled ||
                _triggeredAt == 0 || World.Player == null || !World.InGame) return;

            long age = (long)Time.Ticks - _triggeredAt;
            if (age < 0 || age >= LIFETIME_MS)
            {
                _triggeredAt = 0;
                return;
            }

            EnsureTextures();
            if (_ankh == null || _ankhGlow == null) return;

            float progress = age / (float)LIFETIME_MS;
            float appear = Math.Min(1f, progress / 0.15f);
            float fade = SpellVisualPrimitives.Envelope(progress, 0.12f, 0.38f);
            float pulse = 0.95f + 0.025f *
                (float)Math.Sin(progress * MathHelper.TwoPi * 3.4f);
            float scale =
                (0.72f + 0.3f * EaseOut(appear))
                * pulse
                * OverheadEffectSizeSettings.Scale;
            float rise = 10f * EaseOut(progress);

            Point ground = PathPreview.TileToViewport(
                World.Player.X,
                World.Player.Y,
                World.Player.Z
            );
            Vector2 center = new Vector2(
                ground.X,
                ground.Y - 145f - rise - OverheadEffectSizeSettings.VerticalLift
            );
            Vector2 origin = new Vector2(
                TEXTURE_WIDTH * 0.5f,
                TEXTURE_HEIGHT * 0.5f
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawTornadoHalf(batcher, center, progress, fade, scale, false);
            batcher.Draw(
                _ankhGlow,
                center,
                _ankhGlow.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade * 0.85f),
                0f,
                origin,
                new Vector2(scale * 1.1f),
                SpriteEffects.None,
                0f
            );
            DrawActivationFlash(batcher, center, progress, fade);

            batcher.SetBlendState(BlendState.AlphaBlend);
            batcher.Draw(
                _ankh,
                center,
                _ankh.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade),
                0f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawTornadoHalf(batcher, center, progress, fade, scale, true);
            DrawStormMotes(batcher, center, progress, fade, scale);
            batcher.SetBlendState(BlendState.AlphaBlend);
        }

        private static void DrawTornadoHalf(UltimaBatcher2D batcher, Vector2 center,
            float progress, float fade, float scale, bool front)
        {
            Color pearl = new Color(218, 235, 250), gold = new Color(235, 193, 112);
            float phase = progress * MathHelper.TwoPi * 1.4f;
            // Three continuous ribbons, split by depth around the ankh.
            for (int strand = 0; strand < 3; strand++)
            {
                Vector2 previous = default;
                for (int step = 0; step <= 20; step++)
                {
                    float t = step / 20f;
                    float angle = phase + strand * MathHelper.TwoPi / 3f + t * MathHelper.TwoPi * 1.1f;
                    float radius = (18f + 15f * (float)Math.Sin(t * MathHelper.Pi)) * scale;
                    Vector2 point = center + new Vector2((float)Math.Cos(angle) * radius,
                        (48f - t * 105f) * scale + (float)Math.Sin(angle) * 5f * scale);
                    if (step > 0 && ((float)Math.Sin(angle) > 0) == front)
                    {
                        float taper = (float)Math.Sin((t * 0.9f + 0.05f) * MathHelper.Pi);
                        Color color = strand == 1 ? gold : pearl;
                        SpellVisualPrimitives.Stroke(batcher, previous, point, color,
                            fade * (front ? 0.42f : 0.19f), (2f + taper * 2f) * scale, 0);
                        SpellVisualPrimitives.Stroke(batcher, previous + new Vector2(-0.4f, -0.5f), point,
                            new Color(255, 245, 210), fade * (front ? 0.60f : 0.25f), 0.9f * scale, 0);
                    }
                    previous = point;
                }
            }
        }

        private static void DrawActivationFlash(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade)
        {
            float flash = Math.Max(0f, 1f - progress / 0.3f) * fade;
            if (flash <= 0.01f) return;

            Color white = new Color(255, 250, 220);
            Color gold = new Color(255, 195, 70);

            for (int i = 0; i < EffectPresentation.Count(6); i++)
            {
                float angle = i * MathHelper.TwoPi / 10f + 0.16f;
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    center + direction * 24f,
                    center + direction * (48f + progress * 38f),
                    i % 2 == 0 ? white : gold,
                    flash * (i % 2 == 0 ? 0.8f : 0.55f),
                    i % 2 == 0 ? 2.4f : 1.3f,
                    0f
                );
            }
        }

        private static void DrawStormMotes(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color white = new Color(246, 249, 255);
            Color blue = new Color(101, 167, 255);
            Color gold = new Color(255, 215, 102);

            for (int i = 0; i < EffectPresentation.Count(8); i++)
            {
                float phase = progress * 14f + i * 1.49f;
                float heightT = ((progress * 1.7f + i * 0.137f) % 1f);
                float radius = (14f + heightT * 33f) * scale;
                Vector2 mote = center + new Vector2(
                    (float)Math.Sin(phase) * radius,
                    51f - heightT * 108f
                );
                float twinkle = 0.5f + 0.5f * (float)Math.Sin(phase * 2.2f);
                Color color = i % 5 == 0 ? gold : i % 2 == 0 ? white : blue;
                CombatVisualEffect.DrawPoint(
                    batcher,
                    mote,
                    color,
                    fade * Math.Max(0.15f, twinkle),
                    i % 5 == 0 ? 4.2f : 2.4f,
                    0f
                );
            }
        }


        private static void EnsureTextures()
        {
            if (_ankh != null && !_ankh.IsDisposed &&
                _ankhGlow != null && !_ankhGlow.IsDisposed)
            {
                return;
            }

            if (Client.Game?.GraphicsDevice == null) return;

            Color[] ankhPixels = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            bool[] mask = new bool[ankhPixels.Length];

            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    Color color = GetAnkhColor(x, y);
                    int index = y * TEXTURE_WIDTH + x;
                    ankhPixels[index] = color;
                    mask[index] = color.A != 0;
                }
            }

            Color[] glowPixels = new Color[ankhPixels.Length];
            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    int nearest = 8;
                    for (int oy = -7; oy <= 7; oy++)
                    {
                        int sy = y + oy;
                        if (sy < 0 || sy >= TEXTURE_HEIGHT) continue;

                        for (int ox = -7; ox <= 7; ox++)
                        {
                            int sx = x + ox;
                            if (sx < 0 || sx >= TEXTURE_WIDTH) continue;
                            if (!mask[sy * TEXTURE_WIDTH + sx]) continue;
                            int distance = Math.Abs(ox) + Math.Abs(oy);
                            if (distance < nearest) nearest = distance;
                        }
                    }

                    if (nearest <= 7)
                    {
                        int strength = 14 + (7 - nearest) * 18;
                        glowPixels[y * TEXTURE_WIDTH + x] =
                            new Color(255, 192, 68, (byte)strength);
                    }
                }
            }

            _ankh = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _ankh.SetData(ankhPixels);
            _ankhGlow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _ankhGlow.SetData(glowPixels);
        }

        private static Color GetAnkhColor(int x, int y)
        {
            int center = TEXTURE_WIDTH / 2;
            int dx = Math.Abs(x - center);
            float loopX = (x - center) / 17f;
            float loopY = (y - 27f) / 22f;
            float loopRadius = loopX * loopX + loopY * loopY;
            float holeX = (x - center) / 8.5f;
            float holeY = (y - 27f) / 11f;
            float holeRadius = holeX * holeX + holeY * holeY;
            bool loop = loopRadius <= 1f && holeRadius >= 1f;
            bool stem = y >= 43 && y <= 108 && dx <= 7;
            bool bar = y >= 53 && y <= 66 &&
                dx <= 31 - Math.Abs(y - 59) / 2;

            if (!loop && !stem && !bar) return Color.Transparent;

            bool edge =
                !InsideAnkh(x - 2, y) ||
                !InsideAnkh(x + 2, y) ||
                !InsideAnkh(x, y - 2) ||
                !InsideAnkh(x, y + 2);
            if (edge) return new Color(75, 48, 19, 255);

            int shine = 182 + (int)(47f * (1f - x / (float)TEXTURE_WIDTH));
            return x < center
                ? new Color(shine, (byte)(shine * 0.72f), 48, 255)
                : new Color(247, 205, 96, 255);
        }

        private static bool InsideAnkh(int x, int y)
        {
            int center = TEXTURE_WIDTH / 2;
            int dx = Math.Abs(x - center);
            float loopX = (x - center) / 17f;
            float loopY = (y - 27f) / 22f;
            float loopRadius = loopX * loopX + loopY * loopY;
            float holeX = (x - center) / 8.5f;
            float holeY = (y - 27f) / 11f;
            float holeRadius = holeX * holeX + holeY * holeY;
            bool loop = loopRadius <= 1f && holeRadius >= 1f;
            bool stem = y >= 43 && y <= 108 && dx <= 7;
            bool bar = y >= 53 && y <= 66 &&
                dx <= 31 - Math.Abs(y - 59) / 2;
            return loop || stem || bar;
        }

        private static float EaseOut(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            float inverse = 1f - value;
            return 1f - inverse * inverse;
        }
    }
}

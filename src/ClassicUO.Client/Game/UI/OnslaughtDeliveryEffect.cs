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
    /// Displays a breaking shield above the player only when the server confirms
    /// that Onslaught was delivered. The separate "ready" message does not fire it.
    /// </summary>
    public static class OnslaughtDeliveryEffect
    {
        internal const uint READY_CLILOC = 1156007;
        internal const uint DELIVERY_CLILOC = 1156008;

        private const int TEXTURE_WIDTH = 124;
        private const int TEXTURE_HEIGHT = 148;
        private const long LIFETIME_MS = 1750;

        private static bool _hooked;
        private static long _triggeredAt;
        private static Texture2D _leftShield;
        private static Texture2D _rightShield;
        private static Texture2D _leftGlow;
        private static Texture2D _rightGlow;
        private static Texture2D _leftShadow;
        private static Texture2D _rightShadow;
        private static readonly Point[] _rivets =
        {
            new Point(28, 31),
            new Point(96, 31),
            new Point(21, 63),
            new Point(103, 63),
            new Point(34, 99),
            new Point(90, 99)
        };

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.ClilocMessageReceived += OnClilocMessage;
            _hooked = true;
        }

        public static void ResetSession()
        {
            _triggeredAt = 0;
        }

        internal static bool IsConfirmedDelivery(uint cliloc)
        {
            return cliloc == DELIVERY_CLILOC;
        }

        private static void OnClilocMessage(object sender, MessageEventArgs e)
        {
            if (!IsConfirmedDelivery(e?.Cliloc ?? 0) || World.Player == null) return;
            if (e.Parent == null || e.Parent.Serial != World.Player.Serial) return;
            if (
                !SpellAbilityEffectSettings.IsEnabled(
                    SpellAbilityEffectId.Onslaught
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
            if (_leftShield == null || _rightShield == null) return;

            float progress = age / (float)LIFETIME_MS;
            float appear = Math.Min(1f, progress / 0.12f);
            float fade = progress < 0.68f
                ? 1f
                : Math.Max(0f, (1f - progress) / 0.32f);
            float pulse = 1f + 0.055f * (float)Math.Sin(progress * MathHelper.TwoPi * 3f);
            float sizeScale = OverheadEffectSizeSettings.Scale;
            float scale =
                (0.62f + 0.38f * EaseOut(appear)) * pulse * sizeScale;
            float breakProgress = Math.Max(0f, (progress - 0.22f) / 0.78f);
            float separation =
                (2f + 25f * EaseOut(breakProgress)) * sizeScale;
            float rise = 12f * EaseOut(progress);

            Point ground = PathPreview.TileToScreen(
                World.Player.X,
                World.Player.Y,
                World.Player.Z
            );
            Vector2 center = new Vector2(
                ground.X,
                ground.Y - 143f - rise - OverheadEffectSizeSettings.VerticalLift
            );
            Vector2 origin = new Vector2(TEXTURE_WIDTH * 0.5f, TEXTURE_HEIGHT * 0.5f);
            Vector3 shieldHue = ShaderHueTranslator.GetHueVector(0, false, fade);

            batcher.SetBlendState(BlendState.Additive);
            DrawImpactFlash(batcher, center, progress, fade, sizeScale);

            batcher.SetBlendState(BlendState.AlphaBlend);
            Vector3 shadowHue = ShaderHueTranslator.GetHueVector(
                0,
                false,
                fade * 0.72f
            );
            Vector2 shadowOffset = new Vector2(4f, 7f) * scale;
            batcher.Draw(
                _leftShadow,
                center
                    + new Vector2(-separation, breakProgress * 8f)
                    + shadowOffset,
                _leftShadow.Bounds,
                shadowHue,
                -0.025f - breakProgress * 0.15f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );
            batcher.Draw(
                _rightShadow,
                center
                    + new Vector2(separation, -breakProgress * 5f)
                    + shadowOffset,
                _rightShadow.Bounds,
                shadowHue,
                0.025f + breakProgress * 0.15f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );
            batcher.Draw(
                _leftShield,
                center + new Vector2(-separation, breakProgress * 8f),
                _leftShield.Bounds,
                shieldHue,
                -0.025f - breakProgress * 0.15f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );
            batcher.Draw(
                _rightShield,
                center + new Vector2(separation, -breakProgress * 5f),
                _rightShield.Bounds,
                shieldHue,
                0.025f + breakProgress * 0.15f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );

            batcher.SetBlendState(BlendState.Additive);
            float glow =
                fade
                * (0.55f + 0.25f * (float)Math.Sin(progress * MathHelper.TwoPi * 4f));
            Vector3 glowHue = ShaderHueTranslator.GetHueVector(0, false, glow);
            batcher.Draw(
                _leftGlow,
                center + new Vector2(-separation, breakProgress * 8f),
                _leftGlow.Bounds,
                glowHue,
                -0.025f - breakProgress * 0.15f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );
            batcher.Draw(
                _rightGlow,
                center + new Vector2(separation, -breakProgress * 5f),
                _rightGlow.Bounds,
                glowHue,
                0.025f + breakProgress * 0.15f,
                origin,
                new Vector2(scale),
                SpriteEffects.None,
                0f
            );
            DrawFragments(batcher, center, progress, fade, scale);
            batcher.SetBlendState(BlendState.AlphaBlend);
        }

        private static void DrawImpactFlash(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            float flash = Math.Max(0f, 1f - progress / 0.28f) * fade;
            if (flash <= 0.01f) return;

            Color gold = new Color(255, 194, 74);
            Color white = new Color(255, 244, 205);
            float radius = (58f + progress * 72f) * scale;

            DrawImpactRings(batcher, center, progress, flash, scale);

            for (int i = 0; i < 18; i++)
            {
                float angle = i * MathHelper.TwoPi / 18f + progress * 1.4f;
                Vector2 direction = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    center + direction * (41f * scale),
                    center + direction * radius,
                    i % 4 == 0 ? white : gold,
                    flash * (i % 4 == 0 ? 0.9f : 0.48f),
                    i % 4 == 0 ? 3f : 1.35f,
                    0f
                );
            }

            CombatVisualEffect.RecordLight(
                center,
                new Color(255, 190, 58),
                185f * scale,
                flash * 0.8f
            );
        }

        private static void DrawImpactRings(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float alpha,
            float scale)
        {
            Color gold = new Color(255, 176, 38);
            Color pale = new Color(255, 245, 196);

            for (int ring = 0; ring < 2; ring++)
            {
                float radius = (48f + progress * (80f + ring * 28f)) * scale;
                float ringAlpha = alpha * (ring == 0 ? 0.62f : 0.34f);

                for (int segment = 0; segment < 24; segment++)
                {
                    if ((segment + ring) % 6 == 2) continue;

                    float a0 = segment * MathHelper.TwoPi / 24f;
                    float a1 = (segment + 0.72f) * MathHelper.TwoPi / 24f;
                    Vector2 start = center + new Vector2(
                        (float)Math.Cos(a0) * radius,
                        (float)Math.Sin(a0) * radius * 0.48f
                    );
                    Vector2 end = center + new Vector2(
                        (float)Math.Cos(a1) * radius,
                        (float)Math.Sin(a1) * radius * 0.48f
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        start,
                        end,
                        segment % 4 == 0 ? pale : gold,
                        ringAlpha,
                        ring == 0 ? 2.2f : 1.3f,
                        0f
                    );
                }
            }
        }

        private static void DrawFragments(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            if (progress < 0.18f) return;

            float travel = EaseOut((progress - 0.18f) / 0.82f);
            Color gold = new Color(244, 170, 48);
            Color steel = new Color(185, 211, 229);
            Color pale = new Color(255, 244, 202);

            for (int i = 0; i < 14; i++)
            {
                float side = (i & 1) == 0 ? -1f : 1f;
                float x = side * (21f + i * 3.5f) * travel;
                float y = (i - 6) * 5f - travel * (9f + (i % 4) * 7f);
                Vector2 shard = center + new Vector2(x, y) * scale;
                float shardSize = (3f + i % 4) * (1f - travel * 0.42f);
                Vector2 direction = new Vector2(
                    side * (0.65f + (i % 3) * 0.14f),
                    -0.75f + (i % 5) * 0.3f
                );
                Color color = i % 5 == 0 ? pale : i % 3 == 0 ? gold : steel;
                CombatVisualEffect.DrawLine(
                    batcher,
                    shard - direction * shardSize,
                    shard + direction * shardSize,
                    color,
                    fade * 0.88f,
                    i % 5 == 0 ? 2.5f : 1.6f,
                    0f
                );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    shard,
                    color,
                    fade * 0.62f,
                    i % 5 == 0 ? 3.4f : 2.2f,
                    0f
                );
            }
        }

        private static void EnsureTextures()
        {
            if (_leftShield != null && !_leftShield.IsDisposed &&
                _rightShield != null && !_rightShield.IsDisposed &&
                _leftGlow != null && !_leftGlow.IsDisposed &&
                _rightGlow != null && !_rightGlow.IsDisposed &&
                _leftShadow != null && !_leftShadow.IsDisposed &&
                _rightShadow != null && !_rightShadow.IsDisposed)
            {
                return;
            }

            if (Client.Game?.GraphicsDevice == null) return;

            Color[] left = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            Color[] right = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            Color[] leftGlow = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            Color[] rightGlow = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            Color[] leftShadow = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];
            Color[] rightShadow = new Color[TEXTURE_WIDTH * TEXTURE_HEIGHT];

            for (int y = 0; y < TEXTURE_HEIGHT; y++)
            {
                int crackX = GetSplitX(y);

                for (int x = 0; x < TEXTURE_WIDTH; x++)
                {
                    if (!InsideShield(x, y)) continue;

                    Color color = GetShieldColor(x, y);
                    Color glow = GetShieldGlow(x, y);
                    int index = y * TEXTURE_WIDTH + x;
                    int crackDistance = Math.Abs(x - crackX);

                    if (x <= crackX - 1)
                    {
                        left[index] = GetFractureColor(
                            color,
                            crackDistance,
                            true
                        );
                        if (crackDistance > 4) leftGlow[index] = glow;
                        leftShadow[index] = new Color(5, 8, 16, 174);
                    }
                    else if (x >= crackX + 1)
                    {
                        right[index] = GetFractureColor(
                            color,
                            crackDistance,
                            false
                        );
                        if (crackDistance > 4) rightGlow[index] = glow;
                        rightShadow[index] = new Color(5, 8, 16, 174);
                    }
                }
            }

            _leftShield = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _leftShield.SetData(left);
            _rightShield = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _rightShield.SetData(right);
            _leftGlow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _leftGlow.SetData(leftGlow);
            _rightGlow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _rightGlow.SetData(rightGlow);
            _leftShadow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _leftShadow.SetData(leftShadow);
            _rightShadow = new Texture2D(
                Client.Game.GraphicsDevice,
                TEXTURE_WIDTH,
                TEXTURE_HEIGHT
            );
            _rightShadow.SetData(rightShadow);
        }

        private static bool InsideShield(int x, int y)
        {
            const float top = 5f;
            const float bottom = 142f;
            if (y < top || y > bottom) return false;

            float t = (y - top) / (bottom - top);
            float center = TEXTURE_WIDTH * 0.5f;
            float dx = Math.Abs(x - center);
            float halfWidth;

            if (t < 0.24f)
            {
                halfWidth = 55f - t * 17f;
            }
            else
            {
                float lower = (t - 0.24f) / 0.76f;
                halfWidth =
                    51f * (1f - (float)Math.Pow(lower, 1.42f)) + 3f;
            }

            float curvedTop = top + dx * 0.105f;
            return y >= curvedTop && dx <= halfWidth;
        }

        private static Color GetShieldColor(int x, int y)
        {
            bool outerRim = IsShieldEdge(x, y, 6);
            bool innerBevel = !outerRim && IsShieldEdge(x, y, 10);
            bool goldCross =
                Math.Abs(x - TEXTURE_WIDTH * 0.5f) <= 5
                || Math.Abs(y - 58) <= 5;

            if (outerRim || goldCross)
            {
                float highlight =
                    Math.Max(
                        0f,
                        1f - Distance(x, y, 35f, 25f) / 105f
                    );
                int gold = 142 + (int)(70f * highlight) + (x + y) % 13;
                return new Color(
                    ClampByte(gold),
                    ClampByte((int)(gold * 0.67f)),
                    ClampByte(28 + (int)(highlight * 31f)),
                    255
                );
            }

            if (innerBevel)
            {
                return new Color(52, 64, 75, 255);
            }

            if (IsBranchCrack(x, y))
            {
                return new Color(27, 23, 29, 255);
            }

            float nx =
                (x - TEXTURE_WIDTH * 0.5f) / (TEXTURE_WIDTH * 0.5f);
            float ny = y / (float)(TEXTURE_HEIGHT - 1);
            float convex = Math.Max(0f, 1f - nx * nx);
            float diagonalLight = Math.Max(
                0f,
                1f - Distance(x, y, 37f, 31f) / 115f
            );
            int steel =
                67
                + (int)(67f * convex)
                + (int)(54f * diagonalLight)
                - (int)(ny * 28f);
            int grain = ((x * 17 + y * 11 + x * y) & 11) - 5;
            int engraving =
                ((x + y * 2) % 31 <= 1 && y > 28 && y < 122) ? -13 : 0;

            if (IsRivet(x, y))
            {
                return diagonalLight > 0.45f
                    ? new Color(245, 220, 136, 255)
                    : new Color(176, 120, 38, 255);
            }

            return new Color(
                ClampByte(steel + grain + engraving),
                ClampByte(steel + 12 + grain + engraving),
                ClampByte(steel + 22 + grain + engraving),
                255
            );
        }

        private static Color GetShieldGlow(int x, int y)
        {
            bool highlightRim =
                IsShieldEdge(x, y, 3)
                && (x < TEXTURE_WIDTH * 0.56f || y < 35);
            bool crossHighlight =
                Math.Abs(x - TEXTURE_WIDTH * 0.5f) <= 1
                || Math.Abs(y - 58) <= 1;

            if (highlightRim || crossHighlight)
            {
                return new Color(255, 225, 126, 178);
            }

            if (IsRivet(x, y))
            {
                return new Color(255, 240, 187, 150);
            }

            float glint = Distance(x, y, 38f, 29f);
            if (glint < 18f && ((x + y) & 3) == 0)
            {
                return new Color(188, 227, 255, 78);
            }

            return Color.Transparent;
        }

        private static Color GetFractureColor(
            Color baseColor,
            int distance,
            bool leftSide)
        {
            if (distance <= 2)
            {
                return new Color(18, 13, 18, 255);
            }

            if (distance <= 4)
            {
                return leftSide
                    ? new Color(79, 49, 31, 255)
                    : new Color(205, 167, 86, 255);
            }

            return baseColor;
        }

        private static bool IsShieldEdge(int x, int y, int inset)
        {
            return
                !InsideShield(x - inset, y)
                || !InsideShield(x + inset, y)
                || !InsideShield(x, y - inset)
                || !InsideShield(x, y + inset);
        }

        private static bool IsBranchCrack(int x, int y)
        {
            int split = GetSplitX(y);
            int distance = x - split;

            if (distance < -4 && y >= 47 && y <= 67)
            {
                int branchY = 57 - (-distance - 4) / 2;
                return Math.Abs(y - branchY) <= 1;
            }

            if (distance > 4 && y >= 75 && y <= 99)
            {
                int branchY = 84 + (distance - 4) / 3;
                return Math.Abs(y - branchY) <= 1;
            }

            return false;
        }

        private static bool IsRivet(int x, int y)
        {
            for (int i = 0; i < _rivets.Length; i++)
            {
                int dx = x - _rivets[i].X;
                int dy = y - _rivets[i].Y;
                if (dx * dx + dy * dy <= 5) return true;
            }

            return false;
        }

        private static int GetSplitX(int y)
        {
            int x = TEXTURE_WIDTH / 2;

            if (y < 31)
            {
                x += 7 - y / 5;
            }
            else if (y < 58)
            {
                x -= (y - 31) / 2;
            }
            else if (y < 88)
            {
                x -= 13;
                x += (y - 58) / 3;
            }
            else if (y < 116)
            {
                x -= 3;
                x += (y - 88) / 5;
            }
            else
            {
                x += 2 - (y - 116) / 7;
            }

            return x + (int)(Math.Sin(y * 0.31f) * 2f);
        }

        private static float Distance(
            float x,
            float y,
            float targetX,
            float targetY)
        {
            float dx = x - targetX;
            float dy = y - targetY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static byte ClampByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private static float EaseOut(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            float inverse = 1f - value;
            return 1f - inverse * inverse;
        }
    }
}

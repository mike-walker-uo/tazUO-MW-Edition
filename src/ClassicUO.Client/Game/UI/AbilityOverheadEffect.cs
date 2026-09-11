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
    public enum AbilityOverheadKind : byte
    {
        None,
        EnemyOfOne,
        CleanseByFire,
        CloseWounds,
        RemoveCurse,
        Confidence,
        Evasion,
        CounterAttack,
        LightningStrikeHit,
        MomentumStrikeHit
    }

    /// <summary>
    /// Confirmed-use overhead symbols for Chivalry and Bushido abilities.
    /// Prepared Lightning/Momentum buffs are deliberately not treated as hits.
    /// </summary>
    public static class AbilityOverheadEffect
    {
        internal const uint LIGHTNING_STRIKE_HIT_CLILOC = 1063168;
        internal const uint MOMENTUM_STRIKE_HIT_CLILOC = 1063171;

        private const long PENDING_MS = 60000;
        private const long LIFETIME_MS = 1750;

        private static bool _hooked;
        private static int _pendingSpell;
        private static long _pendingUntil;
        private static long _triggeredAt;
        private static AbilityOverheadKind _kind;
        private static bool _enemyOfOneActive;

        public static void EnsureHooked()
        {
            if (_hooked) return;
            EventSink.OnBuffAdded += OnBuffAdded;
            EventSink.OnBuffRemoved += OnBuffRemoved;
            EventSink.SpellCastBegin += OnSpellCastBegin;
            EventSink.ClilocMessageReceived += OnClilocMessage;
            _hooked = true;
        }

        public static void ResetSession()
        {
            _pendingSpell = 0;
            _pendingUntil = 0;
            _triggeredAt = 0;
            _kind = AbilityOverheadKind.None;
            _enemyOfOneActive = false;
        }

        internal static AbilityOverheadKind ClassifyBuff(BuffIconType type)
        {
            switch (type)
            {
                case BuffIconType.EnemyOfOne:
                    return AbilityOverheadKind.EnemyOfOne;
                case BuffIconType.Confidence:
                    return AbilityOverheadKind.Confidence;
                case BuffIconType.Evasion:
                    return AbilityOverheadKind.Evasion;
                case BuffIconType.CounterAttack:
                    return AbilityOverheadKind.CounterAttack;
                default:
                    return AbilityOverheadKind.None;
            }
        }

        internal static AbilityOverheadKind ClassifyConfirmedSound(
            int spell,
            ushort sound)
        {
            if (spell == 201 && sound == 0x0208)
            {
                return AbilityOverheadKind.CleanseByFire;
            }

            if (spell == 202 && sound == 0x0202)
            {
                return AbilityOverheadKind.CloseWounds;
            }

            if (spell == 209 && (sound == 0x00F6 || sound == 0x01F7))
            {
                return AbilityOverheadKind.RemoveCurse;
            }

            return AbilityOverheadKind.None;
        }

        internal static AbilityOverheadKind ClassifyHitMessage(uint cliloc)
        {
            switch (cliloc)
            {
                case LIGHTNING_STRIKE_HIT_CLILOC:
                    return AbilityOverheadKind.LightningStrikeHit;
                case MOMENTUM_STRIKE_HIT_CLILOC:
                    return AbilityOverheadKind.MomentumStrikeHit;
                default:
                    return AbilityOverheadKind.None;
            }
        }

        private static void OnBuffAdded(object sender, BuffEventArgs e)
        {
            AbilityOverheadKind kind = e?.Buff == null
                ? AbilityOverheadKind.None
                : ClassifyBuff(e.Buff.Type);

            if (kind == AbilityOverheadKind.EnemyOfOne)
            {
                if (_enemyOfOneActive) return;
                _enemyOfOneActive = true;
            }

            Trigger(kind);
        }

        private static void OnBuffRemoved(object sender, BuffEventArgs e)
        {
            if (e?.Buff?.Type == BuffIconType.EnemyOfOne)
            {
                _enemyOfOneActive = false;
            }
        }

        private static void OnSpellCastBegin(object sender, int spell)
        {
            ObserveCastRequest(spell);
        }

        internal static void ObserveCastRequest(int spell)
        {
            if (spell == 201 || spell == 202 || spell == 209)
            {
                _pendingSpell = spell;
                _pendingUntil = (long)Time.Ticks + PENDING_MS;
            }
            else
            {
                _pendingSpell = 0;
                _pendingUntil = 0;
            }
        }

        internal static void OnSound(ushort sound, ushort x, ushort y)
        {
            long now = (long)Time.Ticks;
            if (_pendingSpell == 0 || _pendingUntil == 0 || now > _pendingUntil)
            {
                _pendingSpell = 0;
                _pendingUntil = 0;
                return;
            }

            AbilityOverheadKind kind = ClassifyConfirmedSound(_pendingSpell, sound);
            if (kind == AbilityOverheadKind.None || World.Player == null) return;

            if (
                kind != AbilityOverheadKind.RemoveCurse
                && (
                    Math.Abs(World.Player.X - x) > 2
                    || Math.Abs(World.Player.Y - y) > 2
                )
            )
            {
                return;
            }

            _pendingSpell = 0;
            _pendingUntil = 0;
            Trigger(kind);
        }

        private static void OnClilocMessage(object sender, MessageEventArgs e)
        {
            if (World.Player == null || e?.Parent == null ||
                e.Parent.Serial != World.Player.Serial)
            {
                return;
            }

            Trigger(ClassifyHitMessage(e.Cliloc));
        }

        private static void Trigger(AbilityOverheadKind kind)
        {
            if (
                kind == AbilityOverheadKind.None
                || !SpellAbilityEffectSettings.IsEnabled(kind)
            )
            {
                return;
            }

            _kind = kind;
            _triggeredAt = (long)Time.Ticks;
        }

        internal static void Preview(AbilityOverheadKind kind)
        {
            if (kind == AbilityOverheadKind.None)
            {
                return;
            }

            _kind = kind;
            _triggeredAt = (long)Time.Ticks;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled ||
                _kind == AbilityOverheadKind.None || _triggeredAt == 0 ||
                World.Player == null || !World.InGame)
            {
                return;
            }

            long age = (long)Time.Ticks - _triggeredAt;
            if (age < 0 || age >= LIFETIME_MS)
            {
                _kind = AbilityOverheadKind.None;
                _triggeredAt = 0;
                return;
            }

            float progress = age / (float)LIFETIME_MS;
            float appear = Math.Min(1f, progress / 0.13f);
            float fade = progress < 0.7f
                ? 1f
                : Math.Max(0f, (1f - progress) / 0.3f);
            float pulse = 0.94f + 0.08f *
                (float)Math.Sin(progress * MathHelper.TwoPi * 3.1f);
            float scale =
                (0.7f + 0.3f * EaseOut(appear))
                * pulse
                * OverheadEffectSizeSettings.Scale;

            Point ground = PathPreview.TileToScreen(
                World.Player.X,
                World.Player.Y,
                World.Player.Z
            );
            Vector2 center = new Vector2(
                ground.X,
                ground.Y
                    - 143f
                    - 10f * EaseOut(progress)
                    - OverheadEffectSizeSettings.VerticalLift
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawSymbol(batcher, center, progress, fade, scale, _kind);
            batcher.SetBlendState(BlendState.AlphaBlend);
        }

        private static void DrawSymbol(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale,
            AbilityOverheadKind kind)
        {
            switch (kind)
            {
                case AbilityOverheadKind.EnemyOfOne:
                    DrawEnemyOfOne(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.CleanseByFire:
                    DrawCleanseByFire(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.CloseWounds:
                    DrawCloseWounds(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.RemoveCurse:
                    DrawRemoveCurse(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.Confidence:
                    DrawConfidence(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.Evasion:
                    DrawEvasion(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.CounterAttack:
                    DrawCounterAttack(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.LightningStrikeHit:
                    DrawLightningStrike(batcher, center, progress, fade, scale);
                    break;
                case AbilityOverheadKind.MomentumStrikeHit:
                    DrawMomentumStrike(batcher, center, progress, fade, scale);
                    break;
            }
        }

        private static void DrawEnemyOfOne(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color crimson = new Color(225, 32, 44);
            Color ruby = new Color(255, 82, 70);
            Color gold = new Color(255, 202, 67);
            Color ivory = new Color(255, 248, 218);
            float irisPulse =
                0.88f
                + 0.16f
                    * Math.Abs(
                        (float)Math.Sin(progress * MathHelper.TwoPi * 3f)
                    );
            float turn = progress * MathHelper.TwoPi * 0.72f;

            DrawLayeredArc(
                batcher,
                center,
                55f * scale,
                34f * scale,
                MathHelper.Pi,
                MathHelper.TwoPi,
                crimson,
                ivory,
                fade,
                scale
            );
            DrawLayeredArc(
                batcher,
                center,
                55f * scale,
                34f * scale,
                0f,
                MathHelper.Pi,
                crimson,
                gold,
                fade,
                scale
            );
            DrawLayeredArc(
                batcher,
                center,
                19f * scale * irisPulse,
                23f * scale * irisPulse,
                0f,
                MathHelper.TwoPi,
                ruby,
                gold,
                fade,
                scale
            );
            DrawLayeredLine(
                batcher,
                center + S(0, -20, scale * irisPulse),
                center + S(0, 20, scale * irisPulse),
                crimson,
                ivory,
                fade,
                scale
            );
            DrawLayeredArc(
                batcher,
                center,
                66f * scale,
                49f * scale,
                turn,
                turn + 4.75f,
                crimson,
                gold,
                fade * 0.72f,
                scale * 0.72f
            );

            for (int corner = 0; corner < 4; corner++)
            {
                float angle = corner * MathHelper.PiOver2 + turn * 0.22f;
                Vector2 d = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                Vector2 side = new Vector2(-d.Y, d.X);
                Vector2 point = center + d * 65f * scale;
                DrawLayeredLine(
                    batcher,
                    point - side * 8f * scale,
                    point + side * 8f * scale,
                    crimson,
                    gold,
                    fade * 0.78f,
                    scale * 0.65f
                );
            }

            DrawOrbitMotes(
                batcher,
                center,
                progress,
                fade,
                scale,
                crimson,
                gold
            );
        }

        private static void DrawCleanseByFire(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color ember = new Color(184, 31, 20);
            Color orange = new Color(255, 111, 22);
            Color gold = new Color(255, 207, 70);
            Color white = new Color(255, 250, 210);
            float sway =
                (float)Math.Sin(progress * MathHelper.TwoPi * 2.6f)
                * 5f
                * scale;
            Vector2[] outerFlame =
            {
                S(0, 53, scale), S(-30, 40, scale), S(-43, 12, scale),
                S(-29, -14, scale), S(-18, -49, scale),
                S(0, -31, scale) + new Vector2(sway, 0),
                S(17, -66, scale) + new Vector2(sway, 0),
                S(34, -24, scale), S(44, 8, scale), S(31, 39, scale),
                S(0, 53, scale)
            };

            DrawLayeredPath(
                batcher,
                center,
                outerFlame,
                ember,
                orange,
                fade,
                scale
            );

            Vector2[] innerFlame =
            {
                S(0, 39, scale), S(-18, 24, scale), S(-13, 1, scale),
                S(2, -27, scale), S(10, -4, scale),
                S(23, 18, scale), S(15, 37, scale), S(0, 39, scale)
            };
            DrawLayeredPath(
                batcher,
                center,
                innerFlame,
                orange,
                white,
                fade,
                scale * 0.72f
            );
            DrawLayeredLine(
                batcher,
                center + S(0, -12, scale),
                center + S(0, 24, scale),
                orange,
                white,
                fade,
                scale
            );
            DrawLayeredLine(
                batcher,
                center + S(-14, 5, scale),
                center + S(14, 5, scale),
                orange,
                gold,
                fade,
                scale
            );

            for (int emberIndex = 0; emberIndex < 9; emberIndex++)
            {
                float rise =
                    (progress * 1.8f + emberIndex * 0.117f) % 1f;
                float phase =
                    progress * 10f + emberIndex * 1.71f;
                Dot(
                    batcher,
                    center
                        + new Vector2(
                            (float)Math.Sin(phase) * 35f * scale,
                            40f * scale - rise * 116f * scale
                        ),
                    emberIndex % 3 == 0 ? white : gold,
                    fade * (1f - rise),
                    emberIndex % 3 == 0 ? 3.2f : 2f
                );
            }

            DrawBurst(batcher, center, progress, fade, scale, orange, white);
        }

        private static void DrawCloseWounds(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color white = new Color(255, 252, 230);
            Color gold = new Color(255, 201, 76);
            Color rose = new Color(255, 91, 130);
            Color crimson = new Color(126, 13, 44);
            float beat =
                0.96f
                + 0.08f
                    * (float)Math.Sin(progress * MathHelper.TwoPi * 4f);
            scale *= beat;

            DrawLayeredLine(
                batcher,
                center + S(0, -50, scale),
                center + S(0, 46, scale),
                crimson,
                gold,
                fade * 0.64f,
                scale * 0.8f
            );
            DrawLayeredLine(
                batcher,
                center + S(-43, -3, scale),
                center + S(43, -3, scale),
                crimson,
                white,
                fade * 0.64f,
                scale * 0.8f
            );

            Vector2[] heart =
            {
                S(0, 48, scale),
                S(-16, 34, scale),
                S(-36, 14, scale),
                S(-48, -8, scale),
                S(-44, -29, scale),
                S(-29, -43, scale),
                S(-11, -43, scale),
                S(0, -29, scale),
                S(11, -43, scale),
                S(29, -43, scale),
                S(44, -29, scale),
                S(48, -8, scale),
                S(36, 14, scale),
                S(16, 34, scale),
                S(0, 48, scale)
            };

            DrawLayeredPath(
                batcher,
                center,
                heart,
                crimson,
                rose,
                fade,
                scale
            );
            DrawPath(
                batcher,
                center,
                heart,
                white,
                fade,
                Math.Max(1f, 1.8f * scale)
            );

            Vector2[] closingSeam =
            {
                S(0, -25, scale),
                S(-6, -14, scale),
                S(5, -4, scale),
                S(-5, 8, scale),
                S(5, 19, scale),
                S(0, 34, scale)
            };
            DrawLayeredPath(
                batcher,
                center,
                closingSeam,
                crimson,
                rose,
                fade,
                scale * 0.72f
            );

            for (int stitch = 0; stitch < 5; stitch++)
            {
                float y = -17f + stitch * 11f;
                float close = 1f - progress * 0.55f;
                DrawLayeredLine(
                    batcher,
                    center + S(-12f * close, y - 4f, scale),
                    center + S(12f * close, y + 4f, scale),
                    crimson,
                    stitch % 2 == 0 ? white : gold,
                    fade,
                    scale * 0.58f
                );
            }

            DrawLayeredArc(
                batcher,
                center,
                58f * scale,
                48f * scale,
                progress * 1.5f,
                progress * 1.5f + 4.8f,
                rose,
                gold,
                fade * 0.46f,
                scale * 0.55f
            );
            DrawBurst(batcher, center, progress, fade, scale, rose, white);
        }

        private static void DrawRemoveCurse(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color dark = new Color(48, 12, 75);
            Color violet = new Color(178, 75, 255);
            Color blue = new Color(74, 183, 255);
            Color gold = new Color(255, 213, 91);
            Color white = new Color(255, 252, 235);
            float open = 8f + 27f * EaseOut(progress);
            float twist = progress * 0.9f;

            DrawLink(
                batcher,
                center + S(-31 - open, -8, scale),
                -0.45f - twist,
                scale,
                fade,
                dark,
                violet
            );
            DrawLink(
                batcher,
                center + S(31 + open, 8, scale),
                0.45f + twist,
                scale,
                fade,
                dark,
                blue
            );
            DrawLink(
                batcher,
                center + S(-8 - open * 0.45f, 5, scale),
                0.62f - twist,
                scale * 0.78f,
                fade * 0.82f,
                dark,
                violet
            );
            DrawLink(
                batcher,
                center + S(8 + open * 0.45f, -5, scale),
                -0.62f + twist,
                scale * 0.78f,
                fade * 0.82f,
                dark,
                blue
            );
            DrawLayeredLine(
                batcher,
                center + S(0, -37, scale),
                center + S(0, 34, scale),
                violet,
                white,
                fade,
                scale
            );
            DrawLayeredLine(
                batcher,
                center + S(-28, -2, scale),
                center + S(28, -2, scale),
                blue,
                gold,
                fade,
                scale
            );

            for (int shard = 0; shard < 10; shard++)
            {
                float angle =
                    shard * MathHelper.TwoPi / 10f + progress * 0.7f;
                Vector2 d = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                float distance =
                    (35f + 43f * EaseOut(progress)) * scale;
                DrawLayeredLine(
                    batcher,
                    center + d * distance,
                    center + d * (distance + 10f * scale),
                    shard % 2 == 0 ? violet : blue,
                    white,
                    fade * (1f - progress * 0.35f),
                    scale * 0.45f
                );
            }

            DrawBurst(batcher, center, progress, fade, scale, violet, white);
        }

        private static void DrawConfidence(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color dark = new Color(63, 22, 18);
            Color crimson = new Color(207, 52, 42);
            Color gold = new Color(255, 190, 48);
            Color white = new Color(255, 247, 205);
            float rise = 11f * (1f - EaseOut(progress));
            Vector2 crestCenter = center + S(0, rise, scale);

            DrawLayeredArc(
                batcher,
                crestCenter,
                43f * scale,
                43f * scale,
                0.28f,
                MathHelper.TwoPi + 0.05f,
                dark,
                gold,
                fade,
                scale
            );
            DrawLayeredArc(
                batcher,
                crestCenter,
                31f * scale,
                31f * scale,
                0f,
                MathHelper.Pi,
                crimson,
                white,
                fade,
                scale * 0.72f
            );

            for (int i = 0; i < 11; i++)
            {
                float angle =
                    MathHelper.Pi
                    + i * MathHelper.Pi / 10f
                    + progress * 0.12f;
                Vector2 d = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                );
                DrawLayeredLine(
                    batcher,
                    crestCenter + d * 48f * scale,
                    crestCenter + d * (60f + (i % 2) * 5f) * scale,
                    crimson,
                    i % 3 == 0 ? white : gold,
                    fade * 0.78f,
                    scale * 0.55f
                );
            }

            Vector2[] mountain =
            {
                S(-38, 28, scale),
                S(-18, 4, scale),
                S(-5, 17, scale),
                S(10, -16, scale),
                S(39, 28, scale)
            };
            DrawLayeredPath(
                batcher,
                crestCenter,
                mountain,
                dark,
                gold,
                fade,
                scale
            );
            DrawLayeredLine(
                batcher,
                crestCenter + S(-36, 29, scale),
                crestCenter + S(36, 29, scale),
                dark,
                white,
                fade,
                scale * 0.75f
            );
            DrawOrbitMotes(batcher, center, progress, fade, scale, gold, white);
        }

        private static void DrawEvasion(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color dark = new Color(20, 41, 83);
            Color cyan = new Color(74, 224, 255);
            Color blue = new Color(91, 122, 255);
            Color white = new Color(246, 252, 255);
            float sweep = (float)Math.Sin(progress * MathHelper.TwoPi) * 0.12f;

            for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
            {
                Color color = sideIndex < 0 ? cyan : blue;
                for (int feather = 0; feather < 4; feather++)
                {
                    float y = -31f + feather * 19f;
                    Vector2 root =
                        center + S(sideIndex * 5f, y * 0.36f, scale);
                    Vector2 elbow =
                        center
                        + S(
                            sideIndex * (30f + feather * 5f),
                            y - 7f,
                            scale
                        );
                    Vector2 tip =
                        center
                        + S(
                            sideIndex * (61f - feather * 3f),
                            y + feather * 5f,
                            scale
                        );
                    Vector2[] featherPath =
                    {
                        root,
                        elbow,
                        tip
                    };
                    DrawLayeredPath(
                        batcher,
                        Vector2.Zero,
                        featherPath,
                        dark,
                        color,
                        fade * (0.96f - feather * 0.08f),
                        scale * (0.82f - feather * 0.07f)
                    );
                    DrawLayeredLine(
                        batcher,
                        tip,
                        tip + S(sideIndex * 10f, -7f + sweep, scale),
                        dark,
                        white,
                        fade * 0.75f,
                        scale * 0.45f
                    );
                }
            }

            DrawLayeredArc(
                batcher,
                center,
                25f * scale,
                41f * scale,
                progress * 1.3f,
                progress * 1.3f + 5.15f,
                dark,
                white,
                fade,
                scale * 0.72f
            );
            DrawLayeredLine(
                batcher,
                center + S(0, -43, scale),
                center + S(0, 43, scale),
                dark,
                cyan,
                fade * 0.9f,
                scale * 0.62f
            );
            DrawOrbitMotes(batcher, center, progress, fade, scale, cyan, white);
        }

        private static void DrawCounterAttack(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color steel = new Color(169, 216, 255);
            Color white = new Color(250, 253, 255);
            Color gold = new Color(255, 192, 68);
            Color red = new Color(232, 51, 52);

            DrawSword(batcher, center, -0.72f, scale * 1.06f, fade, steel, gold);
            DrawSword(batcher, center, 0.72f, scale * 1.06f, fade, steel, gold);
            float turn = progress * MathHelper.TwoPi * 1.6f;
            DrawLayeredArc(
                batcher,
                center,
                58f * scale,
                44f * scale,
                turn,
                turn + 4.75f,
                red,
                gold,
                fade * 0.88f,
                scale * 0.72f
            );
            Vector2 tip = center + new Vector2(
                (float)Math.Cos(turn + 4.75f) * 58f * scale,
                (float)Math.Sin(turn + 4.75f) * 44f * scale
            );
            DrawLayeredLine(
                batcher,
                tip,
                tip + S(-14, -4, scale),
                red,
                white,
                fade,
                scale * 0.7f
            );
            DrawLayeredLine(
                batcher,
                tip,
                tip + S(-4, -14, scale),
                red,
                gold,
                fade,
                scale * 0.7f
            );
            DrawBurst(
                batcher,
                center,
                progress,
                fade,
                scale * 0.75f,
                red,
                white
            );
            DrawOrbitMotes(batcher, center, progress, fade, scale, red, gold);
        }

        private static void DrawLightningStrike(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color dark = new Color(36, 19, 91);
            Color violet = new Color(161, 79, 255);
            Color cyan = new Color(68, 218, 255);
            Color white = new Color(255, 255, 248);
            float flash =
                0.55f
                + 0.45f
                    * (float)Math.Pow(
                        Math.Abs(
                            Math.Sin(
                                progress * MathHelper.TwoPi * 7f
                            )
                        ),
                        3
                    );
            Vector2[] bolt =
            {
                S(-14, -103, scale), S(14, -66, scale),
                S(-7, -39, scale), S(20, -17, scale),
                S(1, 7, scale), S(17, 26, scale),
                S(-15, 83, scale)
            };
            DrawLayeredPath(
                batcher,
                center,
                bolt,
                dark,
                violet,
                fade * flash,
                scale * 1.35f
            );
            DrawPath(
                batcher,
                center,
                bolt,
                white,
                fade * flash,
                Math.Max(1.4f, 3.2f * scale)
            );
            DrawLayeredLine(
                batcher,
                center + S(-4, -31, scale),
                center + S(48, -54, scale),
                violet,
                cyan,
                fade * 0.88f * flash,
                scale * 0.72f
            );
            DrawLayeredLine(
                batcher,
                center + S(4, 11, scale),
                center + S(-52, 33, scale),
                dark,
                violet,
                fade * 0.86f * flash,
                scale * 0.72f
            );
            DrawLayeredArc(
                batcher,
                center + S(-15, 83, scale),
                32f * scale,
                15f * scale,
                progress * 4f,
                progress * 4f + 5.3f,
                violet,
                cyan,
                fade * flash,
                scale * 0.72f
            );
            DrawBurst(
                batcher,
                center + S(-15, 83, scale),
                progress,
                fade,
                scale * 1.2f,
                cyan,
                white
            );
        }

        private static void DrawMomentumStrike(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale)
        {
            Color dark = new Color(36, 34, 49);
            Color gold = new Color(255, 190, 45);
            Color teal = new Color(55, 226, 203);
            Color white = new Color(255, 250, 225);
            float sweep = progress * MathHelper.TwoPi * 1.15f;

            DrawLayeredArc(
                batcher,
                center + S(-19, 0, scale),
                44f * scale,
                30f * scale,
                sweep,
                sweep + 5.15f,
                dark,
                gold,
                fade,
                scale
            );
            DrawLayeredArc(
                batcher,
                center + S(19, 0, scale),
                44f * scale,
                30f * scale,
                -sweep + 2.7f,
                -sweep + 7.85f,
                dark,
                teal,
                fade,
                scale
            );

            Vector2[] goldSlash =
            {
                S(-58, -38, scale),
                S(-15, -8, scale),
                S(54, 34, scale)
            };
            Vector2[] tealSlash =
            {
                S(-58, 38, scale),
                S(15, 8, scale),
                S(54, -34, scale)
            };
            DrawLayeredPath(
                batcher,
                center,
                goldSlash,
                dark,
                gold,
                fade,
                scale * 0.72f
            );
            DrawLayeredPath(
                batcher,
                center,
                tealSlash,
                dark,
                teal,
                fade,
                scale * 0.72f
            );
            DrawLayeredLine(
                batcher,
                center + S(-10, 0, scale),
                center + S(10, 0, scale),
                dark,
                white,
                fade,
                scale
            );
            DrawBurst(
                batcher,
                center,
                progress,
                fade,
                scale,
                gold,
                white
            );
            DrawOrbitMotes(batcher, center, progress, fade, scale, gold, teal);
        }

        private static void DrawSword(
            UltimaBatcher2D batcher,
            Vector2 center,
            float angle,
            float scale,
            float fade,
            Color blade,
            Color hilt)
        {
            Vector2 d = new Vector2((float)Math.Sin(angle), -(float)Math.Cos(angle));
            Vector2 side = new Vector2(-d.Y, d.X);
            Vector2 tip = center + d * 61f * scale;
            Vector2 bladeBase = center - d * 10f * scale;
            Vector2 guard = center - d * 13f * scale;
            Vector2 gripEnd = center - d * 43f * scale;
            Vector2 pommel = center - d * 49f * scale;
            float silhouette = Math.Max(4f, 14f * scale);
            float bladeWidth = Math.Max(2.4f, 8f * scale);

            batcher.SetBlendState(BlendState.AlphaBlend);
            Line(
                batcher,
                bladeBase,
                tip,
                new Color(25, 31, 43),
                fade * 0.92f,
                silhouette
            );
            Line(
                batcher,
                guard - side * 21f * scale,
                guard + side * 21f * scale,
                new Color(66, 38, 18),
                fade,
                Math.Max(4f, 10f * scale)
            );
            Line(
                batcher,
                guard,
                gripEnd,
                new Color(45, 24, 18),
                fade,
                Math.Max(3f, 9f * scale)
            );

            batcher.SetBlendState(BlendState.Additive);
            Line(batcher, bladeBase, tip, blade, fade * 0.88f, bladeWidth);
            Line(
                batcher,
                bladeBase + side * 2f * scale,
                tip,
                Color.White,
                fade,
                Math.Max(1.1f, 2.2f * scale)
            );
            Line(
                batcher,
                guard - side * 19f * scale,
                guard + side * 19f * scale,
                hilt,
                fade,
                Math.Max(2.2f, 6f * scale)
            );
            Line(
                batcher,
                guard,
                gripEnd,
                new Color(210, 82, 38),
                fade * 0.86f,
                Math.Max(1.8f, 4f * scale)
            );
            Dot(
                batcher,
                pommel,
                hilt,
                fade,
                Math.Max(3f, 8f * scale)
            );
        }

        private static void DrawLayeredLine(
            UltimaBatcher2D batcher,
            Vector2 start,
            Vector2 end,
            Color body,
            Color highlight,
            float alpha,
            float scale)
        {
            batcher.SetBlendState(BlendState.AlphaBlend);
            Line(
                batcher,
                start,
                end,
                body,
                alpha * 0.9f,
                Math.Max(3f, 11f * scale)
            );

            batcher.SetBlendState(BlendState.Additive);
            Line(
                batcher,
                start,
                end,
                highlight,
                alpha * 0.88f,
                Math.Max(1.7f, 5.2f * scale)
            );
            Line(
                batcher,
                start,
                end,
                Color.Lerp(highlight, Color.White, 0.72f),
                alpha * 0.72f,
                Math.Max(0.9f, 1.8f * scale)
            );
        }

        private static void DrawLayeredArc(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radiusX,
            float radiusY,
            float start,
            float end,
            Color body,
            Color highlight,
            float alpha,
            float scale)
        {
            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawArc(
                batcher,
                center,
                radiusX,
                radiusY,
                start,
                end,
                body,
                alpha * 0.9f,
                Math.Max(3f, 11f * scale)
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawArc(
                batcher,
                center,
                radiusX,
                radiusY,
                start,
                end,
                highlight,
                alpha * 0.88f,
                Math.Max(1.7f, 5.2f * scale)
            );
            DrawArc(
                batcher,
                center,
                radiusX,
                radiusY,
                start,
                end,
                Color.Lerp(highlight, Color.White, 0.72f),
                alpha * 0.7f,
                Math.Max(0.9f, 1.8f * scale)
            );
        }

        private static void DrawLayeredPath(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2[] points,
            Color body,
            Color highlight,
            float alpha,
            float scale)
        {
            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawPath(
                batcher,
                center,
                points,
                body,
                alpha * 0.9f,
                Math.Max(3f, 11f * scale)
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawPath(
                batcher,
                center,
                points,
                highlight,
                alpha * 0.88f,
                Math.Max(1.7f, 5.2f * scale)
            );
            DrawPath(
                batcher,
                center,
                points,
                Color.Lerp(highlight, Color.White, 0.72f),
                alpha * 0.7f,
                Math.Max(0.9f, 1.8f * scale)
            );
        }

        private static void DrawLink(
            UltimaBatcher2D batcher,
            Vector2 center,
            float rotation,
            float scale,
            float alpha,
            Color body,
            Color highlight)
        {
            const int SEGMENTS = 18;
            Vector2[] points = new Vector2[SEGMENTS + 1];
            float sin = (float)Math.Sin(rotation);
            float cos = (float)Math.Cos(rotation);

            for (int i = 0; i <= SEGMENTS; i++)
            {
                float angle = i * MathHelper.TwoPi / SEGMENTS;
                float x = (float)Math.Cos(angle) * 27f * scale;
                float y = (float)Math.Sin(angle) * 14f * scale;
                points[i] =
                    center
                    + new Vector2(
                        x * cos - y * sin,
                        x * sin + y * cos
                    );
            }

            DrawLayeredPath(
                batcher,
                Vector2.Zero,
                points,
                body,
                highlight,
                alpha,
                scale * 0.72f
            );
        }

        private static void DrawBurst(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale,
            Color outerColor,
            Color coreColor)
        {
            float burst = Math.Max(0f, 1f - progress / 0.36f) * fade;
            for (int i = 0; i < 10; i++)
            {
                float angle = i * MathHelper.TwoPi / 10f + progress;
                Vector2 d = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Line(
                    batcher,
                    center + d * 25f * scale,
                    center + d * (43f + progress * 37f) * scale,
                    i % 3 == 0 ? coreColor : outerColor,
                    burst * (i % 3 == 0 ? 0.9f : 0.58f),
                    i % 3 == 0 ? 2.8f : 1.5f
                );
            }
        }

        private static void DrawOrbitMotes(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float scale,
            Color first,
            Color second)
        {
            for (int i = 0; i < 14; i++)
            {
                float phase = progress * 10f + i * 1.83f;
                float travel = (progress * 1.6f + i * 0.143f) % 1f;
                Vector2 position = center + new Vector2(
                    (float)Math.Sin(phase) * (29f + travel * 22f) * scale,
                    (34f - travel * 76f) * scale
                );
                Dot(
                    batcher,
                    position,
                    i % 3 == 0 ? second : first,
                    fade * (0.35f + 0.65f * (1f - travel)),
                    i % 4 == 0 ? 3.8f : 2.2f
                );
            }
        }

        private static void DrawArc(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radiusX,
            float radiusY,
            float start,
            float end,
            Color color,
            float alpha,
            float width)
        {
            const int SEGMENTS = 20;
            Vector2 previous = Ellipse(center, radiusX, radiusY, start);
            for (int i = 1; i <= SEGMENTS; i++)
            {
                float angle = start + (end - start) * i / SEGMENTS;
                Vector2 current = Ellipse(center, radiusX, radiusY, angle);
                Line(batcher, previous, current, color, alpha, width);
                previous = current;
            }
        }

        private static void DrawPath(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2[] points,
            Color color,
            float alpha,
            float width)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Line(batcher, center + points[i - 1], center + points[i], color, alpha, width);
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

        private static Vector2 S(float x, float y, float scale)
        {
            return new Vector2(x * scale, y * scale);
        }

        private static void Line(
            UltimaBatcher2D batcher,
            Vector2 start,
            Vector2 end,
            Color color,
            float alpha,
            float width)
        {
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                end,
                color,
                Math.Max(0f, alpha),
                width,
                0f
            );
        }

        private static void Dot(
            UltimaBatcher2D batcher,
            Vector2 position,
            Color color,
            float alpha,
            float size)
        {
            CombatVisualEffect.DrawPoint(
                batcher,
                position,
                color,
                Math.Max(0f, alpha),
                size,
                0f
            );
        }

        private static float EaseOut(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            float inverse = 1f - value;
            return 1f - inverse * inverse;
        }
    }
}

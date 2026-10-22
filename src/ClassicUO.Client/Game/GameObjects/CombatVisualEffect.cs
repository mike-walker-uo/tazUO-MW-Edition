#region license
// TazUO addition. Focused combat-property effects plus reserved reusable aura art.
#endregion

using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    public enum CombatVisualKind : byte
    {
        None,
        MagicArrow,
        Fireball,
        Harm,
        LowerAttack,
        LowerDefense,
        ManaDrain,
        StaminaDrain,
        BattleLust,
        ReservedVoidAura
    }

    internal sealed partial class CombatVisualEffect : GameEffect
    {
        private readonly uint _born;
        private readonly int _visualDuration;
        private readonly CombatVisualKind _kind;
        private readonly int _variant;
        private static Texture2D _fireballSmoke;
        private static Texture2D _fireballRed;
        private static Texture2D _fireballOrange;
        private static Texture2D _fireballGold;
        private static Texture2D _fireballWhite;

        public CombatVisualEffect(
            EffectManager manager,
            uint sourceSerial,
            ushort sourceX,
            ushort sourceY,
            sbyte sourceZ,
            CombatVisualKind kind,
            int variant = -1)
            : base(manager, 0, 0, GetDuration(kind), 0)
        {
            Entity source = World.Get(sourceSerial);

            if (source != null && SerialHelper.IsValid(sourceSerial))
            {
                SetSource(source);
            }
            else
            {
                SetSource(sourceX, sourceY, sourceZ);
            }

            AllowedToDraw = true;
            AnimationGraphic = 0;
            _born = Time.Ticks;
            _kind = kind;
            _visualDuration = GetDuration(kind);
            _variant =
                variant >= 0
                    ? variant % 3
                    : SelectReservedAuraVariant(sourceSerial, sourceX, sourceY, _born);
        }

        internal static CombatVisualKind ClassifyMoving(ushort graphic, ushort rawHue)
        {
            switch (graphic)
            {
                case 0x36E4:
                    return CombatVisualKind.MagicArrow;
                case 0x36D4:
                    return rawHue == 9501 || rawHue == 0x49A
                        ? CombatVisualKind.None
                        : CombatVisualKind.Fireball;
                default:
                    return CombatVisualKind.None;
            }
        }

        internal static CombatVisualKind ClassifyFixed(ushort graphic, ushort rawHue)
        {
            if (graphic == 0x374A)
            {
                if (rawHue == 1153)
                {
                    return CombatVisualKind.Harm;
                }

            }

            if (graphic == 0x37BE)
            {
                if (rawHue == 0x0A)
                {
                    return CombatVisualKind.LowerAttack;
                }

                if (rawHue == 0x23)
                {
                    return CombatVisualKind.LowerDefense;
                }
            }

            if (graphic == 0x3789 && rawHue == 0)
            {
                return CombatVisualKind.ManaDrain;
            }

            return CombatVisualKind.None;
        }

        internal static bool ReplacesOriginal(CombatVisualKind kind)
        {
            switch (kind)
            {
                case CombatVisualKind.MagicArrow:
                case CombatVisualKind.LowerAttack:
                case CombatVisualKind.LowerDefense:
                case CombatVisualKind.ManaDrain:
                case CombatVisualKind.Fireball:
                    return true;
                default:
                    return false;
            }
        }

        internal static void DrawFireballProjectile(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2 trailDirection,
            float depth)
        {
            Vector2 normal = new Vector2(-trailDirection.Y, trailDirection.X);
            float phase = Time.Ticks * 0.018f;
            float angle =
                (float)Math.Atan2(trailDirection.Y, trailDirection.X);
            float pulse = 0.88f + 0.12f * (float)Math.Sin(phase * 1.7f);

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int cloud = 5; cloud >= 0; cloud--)
            {
                float t = cloud / 5f;
                float curl =
                    (float)Math.Sin(phase * 0.72f + cloud * 1.83f)
                    * (3f + t * 6f);
                Vector2 cloudCenter =
                    center
                    + trailDirection * (12f + cloud * 13f)
                    + normal * curl;
                DrawFlameCloud(
                    batcher,
                    cloud >= 4
                        ? GetFlameCloud(
                            ref _fireballSmoke,
                            new Color(70, 20, 12),
                            7,
                            22f
                        )
                        : GetFlameCloud(
                            ref _fireballRed,
                            new Color(232, 43, 8),
                            8,
                            25f
                        ),
                    cloudCenter,
                    34f - t * 19f,
                    27f - t * 14f,
                    angle + curl * 0.025f,
                    cloud >= 4 ? 0.36f : 0.58f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);

            for (int flame = 0; flame < EffectPresentation.Count(5); flame++)
            {
                float t = flame / 4f;
                float curl =
                    (float)Math.Sin(phase + flame * 1.67f)
                    * (2f + t * 5f);
                Vector2 flameCenter =
                    center
                    + trailDirection * (4f + flame * 13f)
                    + normal * curl;
                float width = (31f - t * 17f) * pulse;
                float height = (26f - t * 13f) * pulse;

                DrawFlameCloud(
                    batcher,
                    GetFlameCloud(
                        ref _fireballOrange,
                        new Color(255, 92, 12),
                        8,
                        27f
                    ),
                    flameCenter,
                    width,
                    height,
                    angle - curl * 0.018f,
                    0.78f - t * 0.22f,
                    depth
                );
                DrawFlameCloud(
                    batcher,
                    GetFlameCloud(
                        ref _fireballGold,
                        new Color(255, 207, 45),
                        7,
                        24f
                    ),
                    flameCenter - trailDirection * 2f,
                    width * 0.58f,
                    height * 0.58f,
                    angle + curl * 0.02f,
                    0.88f - t * 0.3f,
                    depth
                );
            }

            for (int ribbon = 0; ribbon < 3; ribbon++)
            {
                DrawFlameRibbon(
                    batcher,
                    center,
                    trailDirection,
                    normal,
                    phase,
                    ribbon * MathHelper.TwoPi / 3f,
                    ribbon == 0
                        ? new Color(255, 242, 135)
                        : new Color(255, 142, 24),
                    depth
                );
            }

            for (int ember = 0; ember < EffectPresentation.Count(11); ember++)
            {
                float distance = 15f + ember * 6.4f;
                float side =
                    (float)Math.Sin(phase * 0.6f + ember * 2.23f);
                Vector2 emberPosition =
                    center
                    + trailDirection * distance
                    + normal * side * (10f + ember % 3 * 3f);
                DrawPoint(
                    batcher,
                    emberPosition,
                    ember % 3 == 0
                        ? new Color(255, 221, 75)
                        : new Color(255, 82, 14),
                    0.5f + ember % 3 * 0.12f,
                    1.2f + ember % 3,
                    depth
                );
            }

            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballRed,
                    new Color(232, 43, 8),
                    8,
                    25f
                ),
                center,
                43f * pulse,
                38f * pulse,
                -angle,
                0.84f,
                depth
            );
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballOrange,
                    new Color(255, 92, 12),
                    8,
                    27f
                ),
                center,
                34f * pulse,
                31f * pulse,
                angle,
                0.96f,
                depth
            );
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballGold,
                    new Color(255, 207, 45),
                    7,
                    24f
                ),
                center - trailDirection * 2f,
                24f * pulse,
                22f * pulse,
                -angle,
                1f,
                depth
            );
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballWhite,
                    new Color(255, 250, 218),
                    6,
                    19f
                ),
                center - trailDirection * 4f,
                13f * pulse,
                12f * pulse,
                angle,
                1f,
                depth
            );
            RecordLight(
                center,
                new Color(255, 104, 18),
                178f,
                0.92f
            );
        }

        private static void DrawFlameRibbon(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2 direction,
            Vector2 normal,
            float phase,
            float phaseOffset,
            Color color,
            float depth)
        {
            const int segments = 11;
            Vector2 previous = center + direction * 8f;

            for (int segment = 1; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float side =
                    (float)Math.Sin(
                        phase * 0.82f
                        + phaseOffset
                        + t * MathHelper.TwoPi * 1.55f
                    )
                    * 10f
                    * envelope;
                Vector2 current =
                    center
                    + direction * (8f + t * 76f)
                    + normal * side;
                DrawLine(
                    batcher,
                    previous,
                    current,
                    new Color(255, 67, 8),
                    (1f - t * 0.58f) * 0.45f,
                    5f - t * 3f,
                    depth
                );
                DrawLine(
                    batcher,
                    previous,
                    current,
                    color,
                    (1f - t * 0.65f) * 0.84f,
                    1.35f - t * 0.5f,
                    depth
                );
                previous = current;
            }
        }

        private static void DrawFlameCloud(
            UltimaBatcher2D batcher,
            Texture2D texture,
            Vector2 center,
            float width,
            float height,
            float rotation,
            float alpha,
            float depth)
        {
            if (texture == _fireballSmoke)
            {
                center.X += SceneryInteractionManager.SharedWind * height * 0.12f;
                alpha *= SceneryInteractionManager.SoftIntersection(center.X, center.Y + height * 0.25f, EffectPresentation.GroundY);
            }
            batcher.Draw(
                texture,
                center,
                texture.Bounds,
                ShaderHueTranslator.GetHueVector(
                    0,
                    false,
                    Math.Min(1f, alpha)
                ),
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                new Vector2(width / texture.Width, height / texture.Height),
                SpriteEffects.None,
                depth
            );
        }

        private static Texture2D GetFlameCloud(
            ref Texture2D texture,
            Color color,
            int lobes,
            float twist)
        {
            if (texture != null && !texture.IsDisposed)
            {
                return texture;
            }

            const int size = 64;
            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float radius = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (radius >= 1f)
                    {
                        continue;
                    }

                    float cloudAngle = (float)Math.Atan2(dy, dx);
                    float turbulence =
                        0.7f
                        + 0.3f
                        * (
                            0.5f
                            + 0.5f
                            * (float)Math.Sin(
                                cloudAngle * lobes + radius * twist
                            )
                        );
                    float edge = 1f - radius;
                    float coverage =
                        Math.Min(1f, edge * edge * 2.1f * turbulence);
                    pixels[y * size + x] = new Color(
                        (byte)(color.R * coverage),
                        (byte)(color.G * coverage),
                        (byte)(color.B * coverage),
                        (byte)(255f * coverage)
                    );
                }
            }

            texture = new Texture2D(
                Client.Game.GraphicsDevice,
                size,
                size
            );
            texture.SetData(pixels);
            OptionalTextureCache.Register(texture);
            return texture;
        }

        internal static int SelectReservedAuraVariant(
            uint sourceSerial,
            ushort x,
            ushort y,
            uint ticks)
        {
            unchecked
            {
                uint seed =
                    sourceSerial
                    ^ ((uint)x * 0x9E3779B9u)
                    ^ ((uint)y * 0x85EBCA6Bu)
                    ^ ((ticks / 250u) * 0xC2B2AE35u);

                return (int)(Mix(seed) % 3u);
            }
        }

        public override void Update()
        {
            base.Update();

            if (!IsDestroyed)
            {
                (ushort x, ushort y, sbyte z) = GetSource();

                if (Source != null)
                {
                    Offset = Source.Offset;
                }

                if (X != x || Y != y || Z != z)
                {
                    SetInWorldTile(x, y, z);
                }
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            SpellAbilityEffectSettings.TryMap(_kind, out SpellAbilityEffectId detailId);
            if (IsDestroyed || _kind == CombatVisualKind.None)
            {
                return false;
            }

            float progress = Math.Min(1f, (Time.Ticks - _born) / (float)_visualDuration);
            float fade = SpellVisualPrimitives.Envelope(progress, 0.08f, 0.55f);

            if (fade <= 0f)
            {
                return false;
            }

            GetCenters(posX, posY, out Vector2 body, out Vector2 ground);
            using var presentation = EffectPresentation.For(detailId, groundY: ground.Y);
            depth = Source != null ? Source.CalculateDepthZ() + 1.02f : depth + 0.02f;

            switch (_kind)
            {
                case CombatVisualKind.MagicArrow:
                    DrawMagicArrowImpact(batcher, body, progress, fade, depth);
                    break;
                case CombatVisualKind.Fireball:
                    DrawFireballImpact(batcher, body, ground, progress, fade, depth);
                    break;
                case CombatVisualKind.Harm:
                    DrawHarm(batcher, body, progress, fade, depth);
                    break;
                case CombatVisualKind.LowerAttack:
                    DrawLower(batcher, body, progress, fade, true, depth);
                    break;
                case CombatVisualKind.LowerDefense:
                    DrawLower(batcher, body, progress, fade, false, depth);
                    break;
                case CombatVisualKind.ManaDrain:
                    DrawManaDrain(batcher, body, progress, fade, depth);
                    break;
                case CombatVisualKind.StaminaDrain:
                    DrawStaminaDrain(batcher, body, ground, progress, fade, depth);
                    break;
                case CombatVisualKind.BattleLust:
                    DrawBattleLust(batcher, body, ground, progress, fade, depth);
                    break;
                case CombatVisualKind.ReservedVoidAura:
                    DrawReservedVoidAura(batcher, body, ground, progress, fade, depth);
                    break;
            }

            batcher.SetBlendState(null);
            return true;
        }

        private static void DrawMagicArrowImpact(UltimaBatcher2D batcher, Vector2 center,
            float progress, float fade, float depth)
        {
            Color amber = new Color(236, 170, 75), ivory = new Color(255, 242, 200);
            float burst = SpellVisualPrimitives.Envelope(progress, 0.06f, 0.75f);
            batcher.SetBlendState(BlendState.Additive);
            SpellVisualPrimitives.Mote(batcher, center, amber, burst * 0.48f, 13f * (1f - progress), depth);
            for (int i = 0; i < EffectPresentation.Count(5); i++)
            {
                float angle = i * 2.399f + 0.3f;
                Vector2 d = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle) * 0.65f);
                Vector2 tip = center + d * (8f + progress * (27f + i * 3f));
                SpellVisualPrimitives.Ribbon(batcher, tip - d * 10f, tip + new Vector2(0, -4), tip,
                    i % 2 == 0 ? ivory : amber, burst * 0.7f, 1.5f, depth);
            }
            RecordLight(center, amber, 85f, burst * 0.35f);
        }

        private static void DrawFireballImpact(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            Color red = new Color(255, 55, 20);
            Color orange = new Color(255, 112, 14);
            Color gold = new Color(255, 202, 56);
            float expansion = 8f + progress * 52f;
            float burst =
                (float)Math.Sin(
                    Math.Min(1f, progress * 1.35f) * MathHelper.Pi
                );

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballSmoke,
                    new Color(70, 20, 12),
                    7,
                    22f
                ),
                body - new Vector2(0f, 12f + progress * 23f),
                62f + progress * 35f,
                49f + progress * 24f,
                progress * 0.8f,
                fade * 0.42f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            DrawRing(
                batcher,
                ground,
                expansion,
                16,
                red,
                fade * 0.62f,
                7f,
                depth
            );
            DrawRing(
                batcher,
                ground,
                expansion * 0.72f,
                14,
                gold,
                fade * 0.48f,
                2.4f,
                depth
            );

            for (int lobe = 0; lobe < EffectPresentation.Count(12); lobe++)
            {
                uint h =
                    Mix(
                        (uint)(lobe * 113)
                        ^ (uint)ground.X
                        ^ ((uint)ground.Y << 16)
                    );
                float angle =
                    lobe * MathHelper.TwoPi / 12f
                    + progress * (lobe % 2 == 0 ? 0.7f : -0.55f);
                float radial =
                    progress * (23f + ((h >> 7) & 23u));
                Vector2 lobeCenter =
                    body
                    + new Vector2(
                        (float)Math.Cos(angle) * radial,
                        (float)Math.Sin(angle) * radial * 0.7f
                            - progress * (h & 15u) * 0.6f
                    );
                float size =
                    (24f + ((h >> 13) & 15u))
                    * (0.76f + burst * 0.42f);

                DrawFlameCloud(
                    batcher,
                    GetFlameCloud(
                        ref _fireballRed,
                        new Color(232, 43, 8),
                        8,
                        25f
                    ),
                    lobeCenter,
                    size * 1.2f,
                    size,
                    angle,
                    fade * 0.7f,
                    depth
                );
                DrawFlameCloud(
                    batcher,
                    GetFlameCloud(
                        ref _fireballOrange,
                        orange,
                        8,
                        27f
                    ),
                    lobeCenter,
                    size * 0.72f,
                    size * 0.64f,
                    -angle,
                    fade * 0.82f,
                    depth
                );

                Vector2 sparkEnd =
                    lobeCenter
                    + new Vector2(
                        (float)Math.Cos(angle) * (13f + (h & 7u)),
                        (float)Math.Sin(angle) * (10f + (h & 7u))
                            - 8f
                    );
                DrawLine(
                    batcher,
                    lobeCenter,
                    sparkEnd,
                    lobe % 3 == 0 ? gold : orange,
                    fade * 0.78f,
                    1.2f + (h & 1u),
                    depth
                );
            }

            float coreScale = 1f - progress * 0.45f;
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballRed,
                    red,
                    8,
                    25f
                ),
                body,
                59f * coreScale,
                52f * coreScale,
                progress,
                fade * 0.84f,
                depth
            );
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballGold,
                    gold,
                    7,
                    24f
                ),
                body,
                36f * coreScale,
                32f * coreScale,
                -progress,
                fade,
                depth
            );
            DrawFlameCloud(
                batcher,
                GetFlameCloud(
                    ref _fireballWhite,
                    new Color(255, 250, 218),
                    6,
                    19f
                ),
                body,
                17f * coreScale,
                15f * coreScale,
                progress,
                fade,
                depth
            );
            RecordLight(
                body,
                progress < 0.32f ? gold : red,
                195f + burst * 45f,
                fade * (0.76f + burst * 0.24f)
            );
        }

        private void DrawHarm(UltimaBatcher2D batcher, Vector2 center, float progress, float fade, float depth)
        {
            Color blue = new Color(95, 155, 220), silver = new Color(195, 228, 245);
            float envelope = SpellVisualPrimitives.Envelope(progress, 0.10f, 0.6f);
            float radius = 34f * (1f - progress) + 5f;
            batcher.SetBlendState(BlendState.Additive);
            for (int i = 0; i < EffectPresentation.Count(5); i++)
            {
                float angle = i * 2.399f + progress * 1.5f;
                Vector2 outer = Ellipse(center, radius, radius * 0.75f, angle);
                Vector2 curl = Ellipse(center, radius * 0.8f, radius * 0.5f, angle + 1.1f);
                SpellVisualPrimitives.Ribbon(batcher, outer, curl, center, blue, envelope * 0.5f, 3.2f, depth);
                SpellVisualPrimitives.Ribbon(batcher, outer, curl, center, silver, envelope * 0.55f, 0.9f, depth);
            }
            SpellVisualPrimitives.Mist(batcher, center, blue, envelope * 0.28f, radius * 2f, radius, -progress, depth);
            SpellVisualPrimitives.Mote(batcher, center, silver, envelope * 0.55f, 3f, depth);
            RecordLight(center, blue, 90f, envelope * 0.4f);
        }

        private void DrawLower(UltimaBatcher2D batcher, Vector2 center, float progress,
            float fade, bool attack, float depth)
        {
            Color bronze = attack ? new Color(191, 112, 66) : new Color(85, 145, 180);
            Color silver = new Color(220, 225, 205);
            float envelope = SpellVisualPrimitives.Envelope(progress, 0.14f, 0.55f);
            float split = Math.Max(0, progress - 0.28f) * 13f;
            center += new Vector2(0, -6f + progress * 9f);
            batcher.SetBlendState(BlendState.Additive);
            if (attack)
            {
                Vector2 lower = center + new Vector2(-6, 8), upper = center + new Vector2(6, -12);
                SpellVisualPrimitives.Stroke(batcher, lower, center, bronze, envelope * 0.8f, 3.3f, depth);
                SpellVisualPrimitives.Stroke(batcher, center + new Vector2(split, -split - 2),
                    upper + new Vector2(split, -split), silver, envelope * 0.8f, 2f, depth);
                SpellVisualPrimitives.Stroke(batcher, lower + new Vector2(-5, -3), lower + new Vector2(5, 3),
                    bronze, envelope * 0.85f, 2f, depth);
                SpellVisualPrimitives.Stroke(batcher, lower, lower + new Vector2(-4, 6), silver, envelope * 0.6f, 1.6f, depth);
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 shift = new Vector2(side * split, split * 0.45f);
                    Vector2 top = center + shift + new Vector2(0, -12);
                    Vector2 rim = center + shift + new Vector2(side * 10, -8);
                    Vector2 edge = center + shift + new Vector2(side * 8, 5);
                    Vector2 tip = center + shift + new Vector2(0, 13);
                    SpellVisualPrimitives.Stroke(batcher, top, rim, silver, envelope * 0.65f, 2f, depth);
                    SpellVisualPrimitives.Ribbon(batcher, rim, edge, tip, bronze, envelope * 0.85f, 3f, depth);
                    SpellVisualPrimitives.Stroke(batcher, top + new Vector2(0, 3), tip, silver, envelope * 0.4f, 0.8f, depth);
                }
            }
            for (int i = 0; i < EffectPresentation.Count(4); i++)
            {
                float t = MathHelper.Clamp((progress - 0.25f) / 0.75f, 0, 1);
                Vector2 chip = center + new Vector2((i - 1.5f) * (4f + t * 9f), t * t * 22f - i * 3f);
                SpellVisualPrimitives.Fragment(batcher, chip, i % 2 == 0 ? silver : bronze,
                    envelope * t * 0.6f, 3f, i + t * 2f, depth);
            }
            RecordLight(center, bronze, 72f, envelope * 0.3f);
        }

        private void DrawManaDrain(UltimaBatcher2D batcher, Vector2 center, float progress, float fade, float depth)
        {
            Color blue = new Color(80, 135, 220), violet = new Color(155, 115, 220);
            batcher.SetBlendState(BlendState.Additive);
            for (int i = 0; i < EffectPresentation.Count(7); i++)
            {
                float t = MathHelper.Clamp((progress - i * 0.035f) / 0.72f, 0, 1);
                float envelope = SpellVisualPrimitives.Envelope(t, 0.15f, 0.45f);
                float angle = i * 2.399f + t * 1.3f;
                Vector2 start = Ellipse(center, 34f + i % 3 * 4f, 30f, angle);
                Vector2 end = center + new Vector2(0, -4);
                Vector2 control = Ellipse(center, 23f, 38f, angle + 1f);
                Vector2 head = SpellVisualPrimitives.Curve(start, control, end, t);
                Vector2 tail = SpellVisualPrimitives.Curve(start, control, end, Math.Max(0, t - 0.22f));
                SpellVisualPrimitives.Ribbon(batcher, tail, (tail + head) * 0.5f + new Vector2(4, -3), head,
                    i % 2 == 0 ? blue : violet, envelope * 0.6f, 2.4f, depth);
                SpellVisualPrimitives.Mote(batcher, head, new Color(185, 210, 250), envelope * 0.65f, 2f, depth);
            }
            RecordLight(center, blue, 90f, fade * 0.3f);
        }

        private void DrawStaminaDrain(UltimaBatcher2D batcher, Vector2 body, Vector2 ground,
            float progress, float fade, float depth)
        {
            Color sage = new Color(95, 185, 140), gold = new Color(205, 190, 110);
            batcher.SetBlendState(BlendState.Additive);
            for (int i = 0; i < EffectPresentation.Count(6); i++)
            {
                float t = MathHelper.Clamp((progress - i * 0.03f) / 0.8f, 0, 1);
                float envelope = SpellVisualPrimitives.Envelope(t);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector2 head = body + new Vector2(side * (10f + t * 22f), t * t * 35f - i * 3f);
                SpellVisualPrimitives.Ribbon(batcher, head - new Vector2(side * 7f, 9f),
                    head + new Vector2(-side * 5f, -7f), head, i % 3 == 0 ? gold : sage, envelope * 0.55f, 2.5f, depth);
                SpellVisualPrimitives.Mote(batcher, head, gold, envelope * 0.4f, 1.8f, depth);
            }
            RecordLight(body, sage, 85f, fade * 0.3f);
        }

        private void DrawBattleLust(UltimaBatcher2D batcher, Vector2 body, Vector2 ground,
            float progress, float fade, float depth)
        {
            Color crimson = new Color(190, 45, 60), copper = new Color(230, 150, 100);
            float envelope = SpellVisualPrimitives.Envelope(progress, 0.16f, 0.5f);
            float pulse = 0.78f + 0.22f * (float)Math.Sin(progress * MathHelper.TwoPi * 2f);
            batcher.SetBlendState(BlendState.Additive);
            SpellVisualPrimitives.Mist(batcher, body, crimson, envelope * pulse * 0.28f, 45, 65, progress * 0.2f, depth);
            for (int i = 0; i < EffectPresentation.Count(5); i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 start = body + new Vector2(side * (12 + i * 2), 20);
                Vector2 end = start + new Vector2(side * 4, -45 - progress * 16);
                SpellVisualPrimitives.Ribbon(batcher, start, body + new Vector2(-side * 24, -4), end,
                    i == 0 ? copper : crimson, envelope * pulse * 0.42f, 3f, depth);
            }
            RecordLight(body, crimson, 100f, envelope * pulse * 0.35f);
        }

        private void DrawReservedVoidAura(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            switch (_variant)
            {
                case 0:
                    DrawVoidRift(batcher, body, ground, progress, fade, depth);
                    break;
                case 1:
                    DrawSoulPrism(batcher, body, progress, fade, depth);
                    break;
                default:
                    DrawAbyssBloom(batcher, body, ground, progress, fade, depth);
                    break;
            }
        }

        private static void DrawVoidRift(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            Color violet = new Color(126, 45, 238);
            Color cyan = new Color(57, 219, 255);
            float radius = 13f + progress * 41f;

            DrawRing(batcher, ground, radius, 15, violet, fade * 0.62f, 7f, depth);
            batcher.SetBlendState(BlendState.Additive);

            for (int i = 0; i < EffectPresentation.Count(7); i++)
            {
                float angle = i * MathHelper.TwoPi / 7f - progress * 2f;
                Vector2 outer = Ellipse(ground, radius, radius * 0.45f, angle);
                Vector2 inner = Ellipse(body, 5f, 4f, angle + progress);
                DrawLine(batcher, outer, inner, violet, fade * 0.72f, 3f, depth);
                DrawPoint(batcher, outer, cyan, fade * 0.9f, 3f, depth);
            }

            RecordLight(body, violet, 150f, fade * 0.78f);
        }

        private static void DrawSoulPrism(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float depth)
        {
            Color teal = new Color(45, 238, 211);
            Color violet = new Color(205, 77, 255);
            float radius = 19f + progress * 29f;
            batcher.SetBlendState(BlendState.Additive);

            Vector2 top = center + new Vector2(0f, -radius);
            Vector2 left = center + new Vector2(-radius * 0.75f, radius * 0.55f);
            Vector2 right = center + new Vector2(radius * 0.75f, radius * 0.55f);

            DrawLine(batcher, top, left, teal, fade * 0.76f, 3.6f, depth);
            DrawLine(batcher, left, right, violet, fade * 0.76f, 3.6f, depth);
            DrawLine(batcher, right, top, teal, fade * 0.76f, 3.6f, depth);
            DrawLine(batcher, top, center, violet, fade * 0.7f, 2f, depth);
            DrawLine(batcher, left, center, teal, fade * 0.7f, 2f, depth);
            DrawLine(batcher, right, center, violet, fade * 0.7f, 2f, depth);

            for (int i = 0; i < EffectPresentation.Count(6); i++)
            {
                float angle = i * MathHelper.TwoPi / 6f + progress * 3f;
                DrawPoint(
                    batcher,
                    Ellipse(center, radius * 1.12f, radius * 0.72f, angle),
                    i % 2 == 0 ? teal : violet,
                    fade,
                    3.5f,
                    depth
                );
            }

            RecordLight(center, teal, 145f, fade * 0.76f);
        }

        private static void DrawAbyssBloom(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            Color magenta = new Color(245, 42, 181);
            Color blue = new Color(58, 115, 255);
            float radius = 10f + progress * 38f;
            batcher.SetBlendState(BlendState.Additive);

            for (int i = 0; i < EffectPresentation.Count(9); i++)
            {
                float angle = i * MathHelper.TwoPi / 9f + progress * 1.2f;
                Vector2 inner = Ellipse(body, 5f, 4f, angle);
                Vector2 middle = Ellipse(body, radius * 0.55f, radius * 0.3f, angle + 0.23f);
                Vector2 outer = Ellipse(ground, radius, radius * 0.48f, angle);
                DrawLine(batcher, inner, middle, magenta, fade * 0.66f, 4f, depth);
                DrawLine(batcher, middle, outer, blue, fade * 0.72f, 3f, depth);
            }

            DrawRing(batcher, ground, radius * 0.82f, 18, magenta, fade * 0.46f, 3f, depth);
            RecordLight(body, magenta, 152f, fade * 0.78f);
        }

        private void GetCenters(int posX, int posY, out Vector2 body, out Vector2 ground)
        {
            int tileX = posX + (int)Offset.X + 22;
            int tileY = posY + (int)(Offset.Z + Offset.Y) + 22;
            body = new Vector2(tileX, tileY - 29f);
            ground = new Vector2(tileX, tileY);

            Mobile mobile = Source as Mobile;
            if (mobile == null)
            {
                return;
            }

            Rectangle bounds = mobile.GetOnScreenRectangle();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            body = new Vector2(bounds.Center.X, bounds.Y + bounds.Height * 0.52f);
            ground = new Vector2(bounds.Center.X, bounds.Bottom - 3f);
        }

        private static int GetDuration(CombatVisualKind kind)
        {
            switch (kind)
            {
                case CombatVisualKind.BattleLust:
                    return 1150;
                case CombatVisualKind.ReservedVoidAura:
                    return 950;
                default:
                    return 760;
            }
        }

        internal static void RecordLight(Vector2 position, Color color, float radius, float strength)
        {
            SceneryInteractionManager.RecordTransientLight(
                (int)position.X,
                (int)position.Y,
                color,
                radius,
                strength
            );
            Client.Game
                .GetScene<GameScene>()
                ?.AddTransientLight((int)position.X, (int)position.Y, 2, strength * 0.68f);
        }

        private static void DrawRing(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radius,
            int segments,
            Color color,
            float alpha,
            float width,
            float depth)
        {
            batcher.SetBlendState(BlendState.Additive);

            for (int i = 0; i < segments; i++)
            {
                if (i % 5 == 1)
                {
                    continue;
                }

                float a0 = i * MathHelper.TwoPi / segments;
                float a1 = (i + 0.72f) * MathHelper.TwoPi / segments;
                DrawArc(
                    batcher,
                    center, radius, radius * 0.46f, a0, a1,
                    color,
                    alpha,
                    width,
                    depth
                );
            }
        }

        internal static void DrawArc(UltimaBatcher2D batcher, Vector2 center,
            float radiusX, float radiusY, float startAngle, float endAngle,
            Color color, float alpha, float width, float depth)
        {
            if (alpha <= 0f) return;
            int steps = Math.Max(2, Math.Min(16, (int)Math.Ceiling(Math.Abs(endAngle - startAngle) * Math.Max(radiusX, radiusY) / 5f)));
            Vector2 previous = Ellipse(center, radiusX, radiusY, startAngle);
            for (int i = 1; i <= steps; i++)
            {
                float angle = MathHelper.Lerp(startAngle, endAngle, i / (float)steps);
                Vector2 point = Ellipse(center, radiusX, radiusY, angle);
                DrawLine(batcher, previous, point, color, alpha, width, depth);
                previous = point;
            }
        }

        private static Vector2 Ellipse(
            Vector2 center,
            float radiusX,
            float radiusY,
            float angle)
        {
            return center
                + new Vector2(
                    (float)Math.Cos(angle) * radiusX,
                    (float)Math.Sin(angle) * radiusY
                );
        }

        internal static void DrawLine(
            UltimaBatcher2D batcher,
            Vector2 start,
            Vector2 end,
            Color color,
            float alpha,
            float width,
            float depth)
        {
            if (EffectPresentation.IsActive)
            {
                SpellVisualPrimitives.Stroke(batcher, start, end, color, alpha, width, depth);
                return;
            }
            Vector2 delta = end - start;
            float length = delta.Length();

            if (length <= 0.01f || alpha <= 0f)
            {
                return;
            }

            Texture2D texture = AtmosphereTextures.GetSolid(color);
            batcher.Draw(
                texture,
                start,
                texture.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, Math.Min(1f, alpha)),
                (float)Math.Atan2(delta.Y, delta.X),
                Vector2.Zero,
                new Vector2(length, Math.Max(0.5f, width)),
                SpriteEffects.None,
                depth
            );
        }

        internal static void DrawPoint(
            UltimaBatcher2D batcher,
            Vector2 position,
            Color color,
            float alpha,
            float size,
            float depth)
        {
            if (EffectPresentation.IsActive)
            {
                SpellVisualPrimitives.Mote(batcher, position, color, alpha, size, depth);
                return;
            }
            Texture2D texture = AtmosphereTextures.GetSolid(color);
            batcher.Draw(
                texture,
                position - new Vector2(size * 0.5f),
                texture.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, Math.Min(1f, alpha)),
                0f,
                Vector2.Zero,
                new Vector2(size, size),
                SpriteEffects.None,
                depth
            );
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    internal static class CombatVisualTrigger
    {
        internal static void OnCliloc(uint cliloc, Entity entity)
        {
            if (cliloc == 1113748)
            {
                World.SpawnCombatVisual(entity ?? World.Player, CombatVisualKind.BattleLust);
            }
        }

        internal static void OnSound(ushort sound, ushort x, ushort y)
        {
            if (
                sound != 0x44D
                || World.Player == null
                || Math.Abs(World.Player.X - x) > 1
                || Math.Abs(World.Player.Y - y) > 1
                || (
                    !HasEquippedProperty("Hit Stamina Leech")
                    && !HasEquippedProperty("Hit Stamina Drain")
                )
            )
            {
                return;
            }

            World.SpawnCombatVisual(World.Player, CombatVisualKind.StaminaDrain);
        }

        private static bool HasEquippedProperty(string property)
        {
            Item weapon =
                World.Player.FindItemByLayer(Layer.OneHanded)
                ?? World.Player.FindItemByLayer(Layer.TwoHanded);

            return weapon != null
                && World.OPL.TryGetNameAndData(weapon.Serial, out _, out string data)
                && !string.IsNullOrEmpty(data)
                && data.IndexOf(property, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

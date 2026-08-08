#region license
// TazUO addition. Selectable field visuals for Nether Blast.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal static class NetherBlastVortexManager
    {
        internal const ushort FieldGraphic = 0x37CC;
        internal const ushort CreationGraphic = 0x376A;
        internal const ushort CreationParticle = 5048;

        private static readonly HashSet<uint> _active = new HashSet<uint>();
        private static readonly List<PendingField> _pending = new List<PendingField>();
        private static uint _previewUntil;

        private sealed class PendingField
        {
            internal EffectManager Manager;
            internal GameEffect CreationEffect;
            internal ushort X;
            internal ushort Y;
            internal sbyte Z;
            internal uint Expires;
        }

        internal static int Style
        {
            get
            {
                if (!SpellAbilityEffectSettings.CustomEffectsEnabled)
                {
                    return 0;
                }

                int style = ProfileManager.CurrentProfile?.NetherBlastVortexStyle ?? 1;
                bool previewing =
                    _previewUntil != 0 && Time.Ticks <= _previewUntil;
                if (
                    !previewing
                    && !SpellAbilityEffectSettings.IsEnabled(
                        SpellAbilityEffectId.NetherBlast
                    )
                )
                {
                    return 0;
                }

                if (previewing && style == 0)
                {
                    style = 1;
                }

                return Math.Max(0, Math.Min(6, style));
            }
        }

        internal static void Preview(ushort x, ushort y, sbyte z)
        {
            _previewUntil = Time.Ticks + 2600;
            World.SpawnNetherBlastPreview(x, y, z);
        }

        internal static bool IsFieldParticle(ushort graphic, ushort particleEffect)
        {
            return graphic == CreationGraphic && particleEffect == CreationParticle;
        }

        internal static bool IsFieldItem(ushort graphic)
        {
            return graphic == FieldGraphic;
        }

        internal static bool TryParseStyle(string value, out int style)
        {
            style = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "0":
                case "off":
                    return true;
                case "1":
                case "maelstrom":
                case "void":
                    style = 1;
                    return true;
                case "2":
                case "cyclone":
                case "arcane":
                    style = 2;
                    return true;
                case "3":
                case "storm":
                case "abyssal":
                    style = 3;
                    return true;
                case "4":
                case "rainbow":
                case "prismatic":
                case "spectrum":
                    style = 4;
                    return true;
                case "5":
                case "pulse":
                case "wave":
                case "netherwave":
                    style = 5;
                    return true;
                case "6":
                case "cosmic":
                case "portal":
                case "singularity":
                    style = 6;
                    return true;
                default:
                    return false;
            }
        }

        internal static string GetStyleName(int style)
        {
            switch (style)
            {
                case 1:
                    return "Void Maelstrom";
                case 2:
                    return "Arcane Cyclone";
                case 3:
                    return "Abyssal Storm";
                case 4:
                    return "Prismatic Tempest";
                case 5:
                    return "Nether Pulse Wave";
                case 6:
                    return "Cosmic Singularity";
                default:
                    return "Original";
            }
        }

        internal static void SetStyle(int style)
        {
            style = Math.Max(0, Math.Min(6, style));

            if (ProfileManager.CurrentProfile != null)
            {
                ProfileManager.CurrentProfile.NetherBlastVortexStyle = style;
            }

            if (style != 0)
            {
                return;
            }

            _pending.Clear();

            foreach (uint serial in _active)
            {
                Item item = World.Items.Get(serial);

                if (item != null && !item.IsDestroyed)
                {
                    item.AllowedToDraw = true;
                }
            }
        }

        internal static void RecordCreation(
            EffectManager manager,
            GameEffect creationEffect,
            ushort graphic,
            ushort particleEffect,
            ushort x,
            ushort y,
            sbyte z)
        {
            if (Style == 0 || !IsFieldParticle(graphic, particleEffect))
            {
                return;
            }

            if (_pending.Count >= 32)
            {
                _pending.RemoveAt(0);
            }

            _pending.Add(
                new PendingField
                {
                    Manager = manager,
                    CreationEffect = creationEffect,
                    X = x,
                    Y = y,
                    Z = z,
                    Expires = Time.Ticks + 600
                }
            );
            ResolvePending();
        }

        internal static void ObserveFieldItem(EffectManager manager, Item field)
        {
            if (
                Style == 0
                || manager == null
                || field == null
                || field.IsDestroyed
                || !IsFieldItem(field.OriginalGraphic)
            )
            {
                return;
            }

            field.AllowedToDraw = false;

            if (_active.Add(field.Serial))
            {
                manager.PushToBack(
                    new NetherBlastVortexEffect(manager, field, null)
                );
            }
        }

        internal static void ResolvePending()
        {
            for (int pendingIndex = _pending.Count - 1; pendingIndex >= 0; pendingIndex--)
            {
                PendingField pending = _pending[pendingIndex];

                if (
                    Style == 0
                    || pending.CreationEffect == null
                    || pending.CreationEffect.IsDestroyed
                    || Time.Ticks > pending.Expires
                )
                {
                    _pending.RemoveAt(pendingIndex);
                    continue;
                }

                Item field = FindField(pending.X, pending.Y, pending.Z);

                if (field == null)
                {
                    continue;
                }

                pending.CreationEffect.AllowedToDraw = false;

                if (_active.Add(field.Serial))
                {
                    field.AllowedToDraw = false;
                    pending.Manager.PushToBack(
                        new NetherBlastVortexEffect(
                            pending.Manager,
                            field,
                            pending.CreationEffect
                        )
                    );
                }

                _pending.RemoveAt(pendingIndex);
            }
        }

        private static Item FindField(ushort x, ushort y, sbyte z)
        {
            foreach (Item item in World.Items.Values)
            {
                if (
                    !item.IsDestroyed
                    && item.OriginalGraphic == FieldGraphic
                    && item.X == x
                    && item.Y == y
                    && Math.Abs(item.Z - z) <= 2
                )
                {
                    return item;
                }
            }

            return null;
        }

        internal static void Release(uint serial)
        {
            _active.Remove(serial);
        }
    }

    internal sealed class NetherBlastVortexEffect : GameEffect
    {
        private readonly uint _born;
        private readonly uint _fieldSerial;
        private readonly GameEffect _creationEffect;
        private static Texture2D _deepBlueCloud;
        private static Texture2D _azureCloud;
        private static Texture2D _violetCloud;
        private static Texture2D _rainbowRedCloud;
        private static Texture2D _rainbowGoldCloud;
        private static Texture2D _rainbowGreenCloud;
        private static Texture2D _rainbowCyanCloud;
        private static Texture2D _rainbowBlueCloud;
        private static Texture2D _rainbowVioletCloud;
        private static Texture2D _netherCloud;
        private static Texture2D _cosmicVoidCloud;
        private static Texture2D _cosmicVioletCloud;
        private static Texture2D _cosmicMagentaCloud;
        private static Texture2D _pulseCore;

        internal NetherBlastVortexEffect(
            EffectManager manager,
            Item field,
            GameEffect creationEffect)
            : base(manager, 0, 0, 8000, 0)
        {
            _born = Time.Ticks;
            _fieldSerial = field.Serial;
            _creationEffect = creationEffect;
            AllowedToDraw = true;
            AnimationGraphic = 0;
            SetSource(field);
        }

        internal NetherBlastVortexEffect(
            EffectManager manager,
            Entity source)
            : base(manager, 0, 0, 2400, 0)
        {
            _born = Time.Ticks;
            _fieldSerial = 0;
            AllowedToDraw = true;
            AnimationGraphic = 0;
            SetSource(source);
        }

        internal NetherBlastVortexEffect(
            EffectManager manager,
            ushort x,
            ushort y,
            sbyte z)
            : base(manager, 0, 0, 2400, 0)
        {
            _born = Time.Ticks;
            _fieldSerial = 0;
            AllowedToDraw = true;
            AnimationGraphic = 0;
            SetSource(x, y, z);
        }

        public override void Update()
        {
            if (NetherBlastVortexManager.Style == 0)
            {
                Destroy();
                return;
            }

            base.Update();

            if (!IsDestroyed && Source != null)
            {
                Offset = Source.Offset;
                SetInWorldTile(Source.X, Source.Y, Source.Z);
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            if (IsDestroyed || NetherBlastVortexManager.Style == 0)
            {
                return false;
            }

            float seconds = (Time.Ticks - _born) * 0.001f;
            float pulse = 0.82f + 0.18f * (float)Math.Sin(seconds * 7.5f);
            Vector2 ground = new Vector2(
                posX + Offset.X + 22f,
                posY + Offset.Z + Offset.Y + 19f
            );
            depth =
                Source != null
                    ? Source.CalculateDepthZ() + 1.02f
                    : depth + 0.02f;

            switch (NetherBlastVortexManager.Style)
            {
                case 1:
                    DrawVoidMaelstrom(batcher, ground, seconds, pulse, depth);
                    break;
                case 2:
                    DrawArcaneCyclone(batcher, ground, seconds, pulse, depth);
                    break;
                case 3:
                    DrawAbyssalStorm(batcher, ground, seconds, pulse, depth);
                    break;
                case 4:
                    DrawPrismaticTempest(batcher, ground, seconds, pulse, depth);
                    break;
                case 5:
                    float fieldPhase = (X + Y) % 5 * 0.14f;
                    DrawNetherPulseWave(
                        batcher,
                        ground,
                        seconds + fieldPhase,
                        pulse,
                        depth
                    );
                    break;
                case 6:
                    DrawCosmicSingularity(batcher, ground, seconds, pulse, depth);
                    break;
            }

            batcher.SetBlendState(null);
            return true;
        }

        public override void Destroy()
        {
            if (Source != null && !Source.IsDestroyed)
            {
                Source.AllowedToDraw = true;
            }

            if (_creationEffect != null && !_creationEffect.IsDestroyed)
            {
                _creationEffect.AllowedToDraw = true;
            }

            NetherBlastVortexManager.Release(_fieldSerial);
            base.Destroy();
        }

        private static void DrawVoidMaelstrom(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float seconds,
            float pulse,
            float depth)
        {
            Color deepBlue = new Color(28, 68, 220);
            Color blue = new Color(48, 128, 255);
            Color cyan = new Color(112, 220, 255);
            Vector2 center = ground - new Vector2(0f, 7f);

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _deepBlueCloud, deepBlue, 5, 15f),
                center,
                72f + pulse * 4f,
                35f + pulse * 2f,
                -seconds * 0.75f,
                0.76f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(ref _azureCloud, blue, 6, 18f),
                center - new Vector2(0f, 3f),
                57f,
                28f,
                seconds * 1.15f,
                0.62f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(ref _violetCloud, new Color(92, 66, 220), 7, 21f),
                center - new Vector2(0f, 6f),
                43f,
                22f,
                -seconds * 1.55f,
                0.46f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            for (int arm = 0; arm < 3; arm++)
            {
                DrawSpiral(
                    batcher,
                    center,
                    seconds * 2.65f + arm * MathHelper.TwoPi / 3f,
                    1.18f,
                    5f,
                    37f + pulse * 2f,
                    7f,
                    blue,
                    cyan,
                    7.2f,
                    depth
                );
            }

            DrawBrokenRing(
                batcher,
                center,
                35f + pulse * 2f,
                16f,
                seconds * 2.3f,
                cyan,
                0.58f,
                2.2f,
                depth
            );
            DrawVortexLightning(
                batcher,
                center,
                31f,
                13f,
                seconds,
                1,
                cyan,
                depth
            );
            CombatVisualEffect.DrawPoint(batcher, center, new Color(225, 242, 255), 0.92f, 5f, depth);
            CombatVisualEffect.RecordLight(center, blue, 142f, 0.72f * pulse);
        }

        private static void DrawArcaneCyclone(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float seconds,
            float pulse,
            float depth)
        {
            Color deepBlue = new Color(25, 70, 215);
            Color blue = new Color(55, 145, 255);
            Color cyan = new Color(115, 235, 255);
            Vector2 baseCenter = ground - new Vector2(0f, 6f);

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _deepBlueCloud, deepBlue, 5, 15f),
                baseCenter,
                43f,
                24f,
                -seconds * 1.2f,
                0.72f,
                depth
            );

            for (int layer = 0; layer < 5; layer++)
            {
                float t = layer / 4f;
                Vector2 center = baseCenter - new Vector2(0f, 9f + t * 43f);
                DrawCloud(
                    batcher,
                    layer % 2 == 0
                        ? GetCloud(ref _azureCloud, blue, 6, 18f)
                        : GetCloud(ref _deepBlueCloud, deepBlue, 5, 15f),
                    center,
                    24f + t * 34f + pulse * 2f,
                    17f + t * 8f,
                    seconds * (1.5f + t) + layer,
                    0.42f + t * 0.08f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);
            DrawSpiral(
                batcher,
                baseCenter,
                seconds * 3.35f,
                1.82f,
                6f,
                29f + pulse * 2f,
                57f,
                blue,
                cyan,
                7.5f,
                depth
            );
            DrawSpiral(
                batcher,
                baseCenter,
                seconds * 3.35f + MathHelper.Pi,
                1.82f,
                6f,
                29f + pulse * 2f,
                57f,
                deepBlue,
                new Color(225, 245, 255),
                6.2f,
                depth
            );
            DrawBrokenRing(
                batcher,
                baseCenter - new Vector2(0f, 57f),
                29f,
                12f,
                -seconds * 2.8f,
                cyan,
                0.66f,
                2.4f,
                depth
            );
            DrawVortexLightning(
                batcher,
                baseCenter - new Vector2(0f, 29f),
                24f,
                31f,
                seconds,
                2,
                cyan,
                depth
            );
            CombatVisualEffect.RecordLight(baseCenter - new Vector2(0f, 24f), cyan, 148f, 0.76f * pulse);
        }

        private static void DrawAbyssalStorm(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float seconds,
            float pulse,
            float depth)
        {
            Color deepBlue = new Color(22, 55, 205);
            Color azure = new Color(48, 135, 255);
            Color cyan = new Color(115, 225, 255);
            Color violet = new Color(122, 65, 240);
            Vector2 center = ground - new Vector2(0f, 12f);

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int cloud = 0; cloud < 6; cloud++)
            {
                float angle = seconds * 2.15f + cloud * MathHelper.TwoPi / 6f;
                float vertical = cloud % 3 * 9f;
                Vector2 cloudCenter =
                    center
                    + new Vector2(
                        (float)Math.Cos(angle) * (9f + vertical * 0.2f),
                        (float)Math.Sin(angle) * 4f - vertical
                    );
                DrawCloud(
                    batcher,
                    cloud % 3 == 0
                        ? GetCloud(ref _violetCloud, violet, 7, 21f)
                        : cloud % 2 == 0
                            ? GetCloud(ref _azureCloud, azure, 6, 18f)
                            : GetCloud(ref _deepBlueCloud, deepBlue, 5, 15f),
                    cloudCenter,
                    42f + (cloud % 2) * 9f + pulse * 2f,
                    25f + (cloud % 3) * 3f,
                    -angle * 0.7f,
                    0.46f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);

            for (int arm = 0; arm < 3; arm++)
            {
                DrawSpiral(
                    batcher,
                    center,
                    -seconds * 3.6f + arm * MathHelper.TwoPi / 3f,
                    1.4f,
                    7f,
                    36f + pulse * 2f,
                    25f,
                    arm == 1 ? violet : azure,
                    arm == 1 ? cyan : new Color(215, 238, 255),
                    8f,
                    depth
                );
            }

            DrawBrokenRing(
                batcher,
                center,
                38f + pulse * 2f,
                17f,
                seconds * 3.5f,
                cyan,
                0.64f,
                2.5f,
                depth
            );

            for (int spark = 0; spark < 7; spark++)
            {
                float angle = seconds * 4.1f + spark * MathHelper.TwoPi / 7f;
                Vector2 position =
                    Ellipse(center - new Vector2(0f, spark % 3 * 7f), 30f, 14f, angle);
                CombatVisualEffect.DrawPoint(
                    batcher,
                    position,
                    spark % 2 == 0 ? cyan : new Color(225, 238, 255),
                    0.78f,
                    2.4f + (spark & 1),
                    depth
                );
            }

            DrawVortexLightning(
                batcher,
                center - new Vector2(0f, 7f),
                29f,
                22f,
                seconds,
                3,
                violet,
                depth
            );
            CombatVisualEffect.RecordLight(center, azure, 154f, 0.78f * pulse);
        }

        private static void DrawPrismaticTempest(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float seconds,
            float pulse,
            float depth)
        {
            Vector2 center = ground - new Vector2(0f, 19f);

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int cloud = 0; cloud < 6; cloud++)
            {
                float angle = -seconds * 1.8f + cloud * MathHelper.TwoPi / 6f;
                Vector2 cloudCenter =
                    center
                    + new Vector2(
                        (float)Math.Cos(angle) * 12f,
                        (float)Math.Sin(angle) * 6f - cloud % 2 * 7f
                    );
                DrawCloud(
                    batcher,
                    GetRainbowCloud(cloud),
                    cloudCenter,
                    48f + (cloud % 3) * 7f + pulse * 2f,
                    27f + (cloud & 1) * 5f,
                    angle * 0.8f,
                    0.5f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);

            for (int arm = 0; arm < 4; arm++)
            {
                DrawSpiral(
                    batcher,
                    center,
                    seconds * 3.15f + arm * MathHelper.PiOver2,
                    1.55f,
                    6f,
                    41f + pulse * 2f,
                    31f,
                    RainbowColor(arm + (int)(seconds * 2f)),
                    new Color(225, 246, 255),
                    7.5f,
                    depth
                );
            }

            DrawBrokenRing(
                batcher,
                center - new Vector2(0f, 3f),
                41f + pulse * 2f,
                19f,
                seconds * 3.2f,
                RainbowColor((int)(seconds * 3f)),
                0.72f,
                2.7f,
                depth
            );
            DrawBrokenRing(
                batcher,
                center - new Vector2(0f, 23f),
                28f,
                12f,
                -seconds * 3.8f,
                RainbowColor(3 + (int)(seconds * 3f)),
                0.62f,
                2.2f,
                depth
            );

            for (int spark = 0; spark < 10; spark++)
            {
                float angle = seconds * 4.4f + spark * MathHelper.TwoPi / 10f;
                Vector2 position = Ellipse(
                    center - new Vector2(0f, spark % 4 * 6f),
                    34f,
                    16f,
                    angle
                );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    position,
                    RainbowColor(spark + (int)(seconds * 4f)),
                    0.82f,
                    2.4f + (spark & 1),
                    depth
                );
            }

            DrawVortexLightning(
                batcher,
                center - new Vector2(0f, 7f),
                34f,
                25f,
                seconds,
                4,
                RainbowColor((int)(seconds * 5f)),
                depth
            );
            CombatVisualEffect.DrawPoint(
                batcher,
                center,
                new Color(238, 248, 255),
                0.96f,
                5.5f,
                depth
            );
            CombatVisualEffect.RecordLight(
                center,
                new Color(170, 155, 255),
                164f,
                0.82f * pulse
            );
        }

        private static void DrawVortexLightning(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radiusX,
            float radiusY,
            float seconds,
            int style,
            Color color,
            float depth)
        {
            float frameTime = seconds * 12f;
            int frame = (int)frameTime;
            float envelope = 1f - (frameTime - frame);

            if ((frame + style) % 3 == 0)
            {
                return;
            }

            for (int arc = 0; arc < 2; arc++)
            {
                float startAngle = frame * 1.37f + arc * 2.61f + style * 0.73f;
                float span = 0.72f + arc * 0.18f;
                Vector2 previous = Ellipse(
                    center,
                    radiusX * 0.42f,
                    radiusY * 0.42f,
                    startAngle
                );

                for (int segment = 1; segment <= 5; segment++)
                {
                    float t = segment / 5f;
                    float jitter =
                        0.39f
                        + 0.15f
                        * (float)Math.Sin(
                            frame * 2.17f + arc * 4.31f + segment * 5.73f
                        );
                    Vector2 current = Ellipse(
                        center,
                        radiusX * jitter,
                        radiusY * jitter,
                        startAngle + span * t
                    );

                    Color arcColor =
                        style == 4
                            ? RainbowColor(frame + arc + segment)
                            : color;
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        arcColor,
                        0.46f * envelope,
                        3f,
                        depth
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        new Color(235, 248, 255),
                        0.88f * envelope,
                        0.9f,
                        depth
                    );
                    previous = current;
                }
            }
        }

        private static void DrawNetherPulseWave(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float seconds,
            float pulse,
            float depth)
        {
            Color deepNether = new Color(49, 32, 165);
            Color violet = new Color(145, 60, 255);
            Color blue = new Color(45, 126, 255);
            Color cyan = new Color(118, 232, 255);
            Vector2 center = ground - new Vector2(0f, 9f);
            float cycle = seconds * 1.3f;
            float primary = cycle - (float)Math.Floor(cycle);
            float impact =
                0.5f + 0.5f * (float)Math.Cos(primary * MathHelper.TwoPi);
            impact *= impact;

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _netherCloud, deepNether, 7, 23f),
                center,
                76f + pulse * 6f,
                31f + impact * 8f,
                -seconds * 0.55f,
                0.66f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(ref _violetCloud, violet, 7, 21f),
                center - new Vector2(0f, 4f),
                55f + impact * 10f,
                24f + impact * 7f,
                seconds * 0.9f,
                0.46f + impact * 0.18f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            for (int wave = 0; wave < 3; wave++)
            {
                float waveTime = cycle - wave * 0.29f;
                float progress = waveTime - (float)Math.Floor(waveTime);
                float fade = 1f - progress;
                DrawBrokenRing(
                    batcher,
                    center,
                    9f + progress * 43f,
                    5f + progress * 17f,
                    seconds * (wave % 2 == 0 ? 0.55f : -0.55f) + wave,
                    wave == 1 ? violet : blue,
                    fade * 0.82f,
                    3.2f - progress * 1.2f,
                    depth
                );
                DrawBrokenRing(
                    batcher,
                    center,
                    7f + progress * 38f,
                    4f + progress * 15f,
                    -seconds * 0.7f + wave,
                    cyan,
                    fade * 0.58f,
                    1.1f,
                    depth
                );
            }

            for (int mote = 0; mote < 8; mote++)
            {
                float angle =
                    mote * MathHelper.TwoPi / 8f
                    + seconds * (mote % 2 == 0 ? 1.1f : -1.1f);
                Vector2 position = Ellipse(
                    center - new Vector2(0f, mote % 3 * 3f),
                    11f + primary * 34f,
                    6f + primary * 13f,
                    angle
                );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    position,
                    mote % 3 == 0 ? violet : cyan,
                    (1f - primary) * 0.82f,
                    2.2f + (mote & 1),
                    depth
                );
            }

            DrawVortexLightning(
                batcher,
                center,
                32f,
                14f,
                seconds,
                5,
                violet,
                depth
            );
            DrawPulseCore(
                batcher,
                center - new Vector2(0f, impact * 5f),
                13f + impact * 35f,
                0.34f + impact * 0.64f,
                depth
            );
            DrawPulseCore(
                batcher,
                center - new Vector2(0f, impact * 5f),
                6f + impact * 16f,
                0.62f + impact * 0.36f,
                depth
            );
            CombatVisualEffect.RecordLight(
                center,
                impact > 0.25f ? violet : blue,
                150f + impact * 24f,
                (0.58f + impact * 0.34f) * pulse
            );
        }

        private static void DrawPulseCore(
            UltimaBatcher2D batcher,
            Vector2 center,
            float size,
            float alpha,
            float depth)
        {
            Texture2D texture = GetPulseCore();
            batcher.Draw(
                texture,
                center,
                texture.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, Math.Min(1f, alpha)),
                0f,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                new Vector2(size / texture.Width),
                SpriteEffects.None,
                depth
            );
        }

        private static void DrawCosmicSingularity(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float seconds,
            float pulse,
            float depth)
        {
            Color voidViolet = new Color(28, 8, 68);
            Color deepViolet = new Color(82, 28, 205);
            Color violet = new Color(164, 62, 255);
            Color magenta = new Color(255, 72, 238);
            Color blue = new Color(66, 104, 255);
            Color cyan = new Color(125, 225, 255);
            Vector2 center = ground - new Vector2(0f, 18f);
            float rotation = seconds * 2.15f;
            float breathe = 0.94f + pulse * 0.08f;

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _cosmicVoidCloud, voidViolet, 9, 28f),
                center,
                108f * breathe,
                55f * breathe,
                -rotation * 0.18f,
                0.82f,
                depth
            );

            for (int ribbon = 0; ribbon < 8; ribbon++)
            {
                float angle =
                    rotation * (ribbon % 2 == 0 ? 0.42f : -0.34f)
                    + ribbon * MathHelper.TwoPi / 8f;
                Vector2 ribbonCenter = Ellipse(center, 35f, 16f, angle);
                DrawCloud(
                    batcher,
                    ribbon % 2 == 0
                        ? GetCloud(ref _cosmicMagentaCloud, magenta, 8, 27f)
                        : GetCloud(ref _cosmicVioletCloud, deepViolet, 7, 24f),
                    ribbonCenter,
                    37f + (ribbon & 1) * 7f,
                    14f + (ribbon % 3) * 2f,
                    angle + rotation * 0.2f,
                    0.34f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);

            for (int ring = 0; ring < 6; ring++)
            {
                float t = ring / 5f;
                float radiusX = (49f - ring * 5.4f) * breathe;
                float radiusY = (23f - ring * 2.25f) * breathe;
                Vector2 ringCenter = center - new Vector2(0f, ring * 0.65f);
                Color ringColor = ring % 3 == 0
                    ? magenta
                    : ring % 3 == 1
                        ? violet
                        : blue;

                DrawPortalArc(
                    batcher,
                    ringCenter,
                    radiusX,
                    radiusY,
                    MathHelper.Pi,
                    MathHelper.Pi,
                    rotation * (ring % 2 == 0 ? 1f : -0.82f) + ring,
                    ringColor,
                    cyan,
                    0.34f + t * 0.08f,
                    3.8f - t,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _cosmicVoidCloud, voidViolet, 9, 28f),
                center - new Vector2(0f, 4f),
                39f + pulse * 3f,
                20f + pulse * 2f,
                rotation * 0.12f,
                0.98f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            for (int ring = 5; ring >= 0; ring--)
            {
                float t = ring / 5f;
                float radiusX = (49f - ring * 5.4f) * breathe;
                float radiusY = (23f - ring * 2.25f) * breathe;
                Vector2 ringCenter = center - new Vector2(0f, ring * 0.65f);
                Color ringColor = ring % 3 == 0
                    ? magenta
                    : ring % 3 == 1
                        ? violet
                        : blue;

                DrawPortalArc(
                    batcher,
                    ringCenter,
                    radiusX,
                    radiusY,
                    0f,
                    MathHelper.Pi,
                    rotation * (ring % 2 == 0 ? 1f : -0.82f) + ring,
                    ringColor,
                    ring % 2 == 0 ? new Color(245, 220, 255) : cyan,
                    0.62f + t * 0.12f,
                    4.4f - t,
                    depth
                );
            }

            for (int star = 0; star < 14; star++)
            {
                float angle =
                    rotation * (star % 2 == 0 ? 0.72f : -0.54f)
                    + star * MathHelper.TwoPi / 14f;
                float radius = 31f + star % 4 * 6f;
                Vector2 position = Ellipse(center, radius, radius * 0.47f, angle);
                Color starColor = star % 3 == 0
                    ? magenta
                    : star % 3 == 1
                        ? cyan
                        : new Color(235, 218, 255);
                float starPulse =
                    0.45f
                    + 0.45f
                    * (0.5f + 0.5f * (float)Math.Sin(seconds * 8f + star * 1.71f));
                DrawStar(batcher, position, starColor, starPulse, 2.5f + star % 3, depth);
            }

            DrawVortexLightning(
                batcher,
                center,
                39f,
                17f,
                seconds,
                6,
                magenta,
                depth
            );
            CombatVisualEffect.DrawPoint(
                batcher,
                center - new Vector2(0f, 4f),
                new Color(104, 58, 255),
                0.62f + pulse * 0.22f,
                3.5f,
                depth
            );
            CombatVisualEffect.RecordLight(
                center,
                new Color(152, 80, 255),
                176f,
                0.7f + pulse * 0.18f
            );
        }

        private static void DrawPortalArc(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radiusX,
            float radiusY,
            float start,
            float span,
            float phase,
            Color body,
            Color core,
            float alpha,
            float width,
            float depth)
        {
            const int segments = 9;
            float firstAngle = start;
            float firstWobble = 1f + 0.045f * (float)Math.Sin(phase + firstAngle * 5f);
            Vector2 previous = Ellipse(
                center,
                radiusX * firstWobble,
                radiusY * firstWobble,
                firstAngle
            );

            for (int segment = 1; segment <= segments; segment++)
            {
                float angle = start + span * segment / segments;
                float wobble =
                    1f
                    + 0.045f
                    * (float)Math.Sin(phase + angle * 5f + segment * 0.37f);
                Vector2 current = Ellipse(
                    center,
                    radiusX * wobble,
                    radiusY * wobble,
                    angle
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    body,
                    alpha,
                    width,
                    depth
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    core,
                    Math.Min(1f, alpha + 0.16f),
                    Math.Max(0.9f, width * 0.24f),
                    depth
                );
                previous = current;
            }
        }

        private static void DrawStar(
            UltimaBatcher2D batcher,
            Vector2 center,
            Color color,
            float alpha,
            float size,
            float depth)
        {
            CombatVisualEffect.DrawLine(
                batcher,
                center - new Vector2(size, 0f),
                center + new Vector2(size, 0f),
                color,
                alpha,
                0.8f,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                center - new Vector2(0f, size),
                center + new Vector2(0f, size),
                color,
                alpha,
                0.8f,
                depth
            );
        }

        private static Texture2D GetPulseCore()
        {
            if (_pulseCore != null && !_pulseCore.IsDisposed)
            {
                return _pulseCore;
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

                    float coverage = 1f - radius;
                    coverage *= coverage;
                    byte value = (byte)(coverage * 255f);
                    pixels[y * size + x] = new Color(value, value, value, value);
                }
            }

            _pulseCore = new Texture2D(Client.Game.GraphicsDevice, size, size);
            _pulseCore.SetData(pixels);
            return _pulseCore;
        }

        private static Texture2D GetRainbowCloud(int index)
        {
            switch ((index % 6 + 6) % 6)
            {
                case 0:
                    return GetCloud(ref _rainbowRedCloud, new Color(255, 58, 105), 5, 17f);
                case 1:
                    return GetCloud(ref _rainbowGoldCloud, new Color(255, 174, 52), 6, 19f);
                case 2:
                    return GetCloud(ref _rainbowGreenCloud, new Color(54, 235, 132), 7, 21f);
                case 3:
                    return GetCloud(ref _rainbowCyanCloud, new Color(62, 220, 255), 5, 18f);
                case 4:
                    return GetCloud(ref _rainbowBlueCloud, new Color(60, 108, 255), 6, 20f);
                default:
                    return GetCloud(ref _rainbowVioletCloud, new Color(190, 76, 255), 7, 22f);
            }
        }

        private static Color RainbowColor(int index)
        {
            switch ((index % 6 + 6) % 6)
            {
                case 0:
                    return new Color(255, 72, 120);
                case 1:
                    return new Color(255, 186, 58);
                case 2:
                    return new Color(66, 238, 142);
                case 3:
                    return new Color(72, 225, 255);
                case 4:
                    return new Color(78, 122, 255);
                default:
                    return new Color(202, 86, 255);
            }
        }

        private static void DrawSpiral(
            UltimaBatcher2D batcher,
            Vector2 center,
            float phase,
            float turns,
            float innerRadius,
            float outerRadius,
            float rise,
            Color body,
            Color core,
            float width,
            float depth)
        {
            const int segments = 14;
            Vector2 previous = center + new Vector2(
                (float)Math.Cos(phase) * innerRadius,
                (float)Math.Sin(phase) * innerRadius * 0.46f
            );

            for (int segment = 1; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float radius = innerRadius + (outerRadius - innerRadius) * t;
                float angle = phase + t * MathHelper.TwoPi * turns;
                Vector2 current =
                    center
                    + new Vector2(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius * 0.46f - rise * t
                    );

                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    body,
                    0.46f,
                    width,
                    depth
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    core,
                    0.74f,
                    Math.Max(1.1f, width * 0.24f),
                    depth
                );
                previous = current;
            }
        }

        private static void DrawCloud(
            UltimaBatcher2D batcher,
            Texture2D texture,
            Vector2 center,
            float width,
            float height,
            float rotation,
            float alpha,
            float depth)
        {
            batcher.Draw(
                texture,
                center,
                texture.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, Math.Min(1f, alpha)),
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                new Vector2(width / texture.Width, height / texture.Height),
                SpriteEffects.None,
                depth
            );
        }

        private static Texture2D GetCloud(
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

                    float angle = (float)Math.Atan2(dy, dx);
                    float swirl =
                        0.76f
                        + 0.24f
                        * (0.5f + 0.5f * (float)Math.Sin(angle * lobes + radius * twist));
                    float edge = 1f - radius;
                    float coverage = Math.Min(1f, edge * edge * 1.75f * swirl);
                    byte alpha = (byte)(coverage * 255f);
                    pixels[y * size + x] = new Color(
                        (byte)(color.R * coverage),
                        (byte)(color.G * coverage),
                        (byte)(color.B * coverage),
                        alpha
                    );
                }
            }

            texture = new Texture2D(Client.Game.GraphicsDevice, size, size);
            texture.SetData(pixels);
            return texture;
        }

        private static void DrawBrokenRing(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radiusX,
            float radiusY,
            float rotation,
            Color color,
            float alpha,
            float width,
            float depth)
        {
            const int segments = 14;

            for (int i = 0; i < segments; i++)
            {
                if (i % 5 == 1)
                {
                    continue;
                }

                float a0 = rotation + i * MathHelper.TwoPi / segments;
                float a1 = rotation + (i + 0.72f) * MathHelper.TwoPi / segments;
                CombatVisualEffect.DrawLine(
                    batcher,
                    Ellipse(center, radiusX, radiusY, a0),
                    Ellipse(center, radiusX, radiusY, a1),
                    color,
                    alpha,
                    width,
                    depth
                );
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
    }
}

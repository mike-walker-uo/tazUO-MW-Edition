#region license
// TazUO addition. Element-specific presentation for weapon hit-area procs.
#endregion

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal enum HitAreaElement : byte
    {
        None,
        Fire,
        Cold,
        Poison,
        Energy
    }

    internal sealed partial class HitAreaEffect : GameEffect
    {
        private readonly uint _born;
        private readonly HitAreaElement _element;
        private readonly bool _enhancedAdmitted;
        private readonly int _visualDuration;

        public HitAreaEffect(
            EffectManager manager,
            uint sourceSerial,
            ushort sourceX,
            ushort sourceY,
            sbyte sourceZ,
            ushort graphic,
            ushort hue,
            int duration,
            byte speed,
            HitAreaElement element)
            : base(manager, graphic, hue, duration, speed)
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

            _born = Time.Ticks;
            _element = element;
            _enhancedAdmitted = EffectPresentation.Admit("HitArea:" + element, sourceSerial, sourceX, sourceY);
            _visualDuration = Math.Max(350, Math.Min(900, duration));
        }

        internal static HitAreaElement Classify(ushort graphic, ushort rawHue)
        {
            if (graphic != 0x3779)
            {
                return HitAreaElement.None;
            }

            switch (rawHue)
            {
                case 1160:
                    return HitAreaElement.Fire;
                case 2100:
                    return HitAreaElement.Cold;
                case 1166:
                    return HitAreaElement.Poison;
                case 120:
                    return HitAreaElement.Energy;
                default:
                    return HitAreaElement.None;
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
            bool drewOriginal = base.Draw(batcher, posX, posY, depth);
            SpellAbilityEffectSettings.TryMap(_element, out SpellAbilityEffectId detailId);
            using var presentation = EffectPresentation.For(detailId);

            if (IsDestroyed || !_enhancedAdmitted || _element == HitAreaElement.None)
            {
                return drewOriginal;
            }

            float progress = Math.Min(1f, (Time.Ticks - _born) / (float)_visualDuration);
            float fade = SpellVisualPrimitives.Envelope(progress, 0.08f, 0.55f);

            if (fade <= 0f)
            {
                return drewOriginal;
            }

            int centerX = posX + (int)Offset.X + 22;
            int centerY = posY + (int)(Offset.Z + Offset.Y) + 22;
            depth = Source != null ? Source.CalculateDepthZ() + 1.01f : depth + 0.01f;

            GetPalette(_element, out Color glowColor, out Color coreColor);

            float pulse = 0.84f + 0.16f * (float)Math.Sin(progress * Math.PI * 5f);
            float lightStrength = fade * pulse;
            SceneryInteractionManager.RecordTransientLight(
                centerX,
                centerY - 12,
                glowColor,
                150f,
                lightStrength
            );
            Client.Game
                .GetScene<GameScene>()
                ?.AddTransientLight(centerX, centerY - 12, 2, lightStrength * 0.72f);

            float radius = 18f + 58f * (1f - (1f - progress) * (1f - progress));
            uint seed = Mix(
                (Source is Entity entity ? entity.Serial : 0u) ^ ((uint)X << 16) ^ Y
            );

            DrawBrokenRing(
                batcher,
                centerX,
                centerY,
                radius,
                seed,
                glowColor,
                coreColor,
                fade,
                depth
            );
            DrawElementAccents(
                batcher,
                centerX,
                centerY,
                radius,
                progress,
                fade,
                seed,
                glowColor,
                coreColor,
                depth
            );

            batcher.SetBlendState(null);
            return true;
        }

        private void DrawElementAccents(UltimaBatcher2D batcher, int centerX, int centerY,
            float radius, float progress, float fade, uint seed, Color glowColor, Color coreColor, float depth)
        {
            batcher.SetBlendState(BlendState.Additive);
            Vector2 center = new Vector2(centerX, centerY - 6f);
            for (int i = 0; i < Math.Min(9, EffectPresentation.Count(6)); i++)
            {
                uint h = Mix(seed + (uint)(i * 97));
                float t = MathHelper.Clamp((progress - i * 0.018f) / 0.82f, 0, 1);
                float envelope = SpellVisualPrimitives.Envelope(t, 0.10f, 0.55f);
                float angle = i * 2.399f + (h & 31u) * 0.025f;
                float variation = ((h >> 8) & 31u) / 31f;
                switch (_element)
                {
                    case HitAreaElement.Fire:
                    {
                        Vector2 root = center + new Vector2((i - 3f) * 6f, 5f);
                        Vector2 tip = root + new Vector2((float)Math.Sin(i + t * 5f) * 8f, -t * (25f + variation * 24f));
                        Vector2 curl = (root + tip) * 0.5f + new Vector2((i % 2 == 0 ? 1 : -1) * 10f, -8);
                        SpellVisualPrimitives.Ribbon(batcher, root, curl, tip, glowColor, envelope * 0.5f, 5f - t * 2f, depth);
                        SpellVisualPrimitives.Ribbon(batcher, root, curl, tip, coreColor, envelope * 0.55f, 1.2f, depth);
                        SpellVisualPrimitives.Mote(batcher, tip, coreColor, envelope * 0.5f, 1.5f, depth);
                        break;
                    }
                    case HitAreaElement.Cold:
                    {
                        Vector2 d = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle) * 0.5f);
                        Vector2 point = center + d * radius * (0.35f + t * 0.55f) - new Vector2(0, (float)Math.Sin(t * MathHelper.Pi) * 15f);
                        SpellVisualPrimitives.Fragment(batcher, point, coreColor, envelope * 0.8f,
                            (4f + variation * 6f) * (1f - t * 0.4f), angle + t * 0.4f, depth);
                        SpellVisualPrimitives.Stroke(batcher, point - d * 8f, point, glowColor, envelope * 0.3f, 1.2f, depth);
                        break;
                    }
                    case HitAreaElement.Poison:
                    {
                        Vector2 point = center + new Vector2((i - 3f) * 8f + (float)Math.Sin(t * 4f + i) * 6f,
                            -t * (18f + variation * 23f));
                        SpellVisualPrimitives.Mist(batcher, point, glowColor, envelope * 0.19f,
                            22f + t * 15f, 12f + t * 11f, i * 0.7f + t * 0.5f, depth);
                        SpellVisualPrimitives.Mote(batcher, point + new Vector2(3, -5), coreColor, envelope * 0.35f, 1.5f, depth);
                        break;
                    }
                    case HitAreaElement.Energy:
                    {
                        Vector2 tip = center + new Vector2((float)Math.Cos(angle) * radius * 0.85f,
                            (float)Math.Sin(angle) * radius * 0.4f - 7f);
                        Vector2 joint = Vector2.Lerp(center, tip, 0.5f) + new Vector2((i % 2 == 0 ? 1 : -1) * 7f, -6);
                        float flash = envelope * (0.65f + 0.35f * (float)Math.Sin(t * 16f + i));
                        SpellVisualPrimitives.Stroke(batcher, center, joint, glowColor, flash * 0.45f, 3.5f, depth);
                        SpellVisualPrimitives.Stroke(batcher, joint, tip, glowColor, flash * 0.45f, 2.4f, depth);
                        SpellVisualPrimitives.Stroke(batcher, center, joint, coreColor, flash * 0.8f, 0.8f, depth);
                        SpellVisualPrimitives.Stroke(batcher, joint, tip, coreColor, flash * 0.65f, 0.6f, depth);
                        break;
                    }
                }
            }
        }

        private static void DrawBrokenRing(UltimaBatcher2D batcher, int centerX, int centerY, float radius,
            uint seed, Color glowColor, Color coreColor, float alpha, float depth)
        {
            batcher.SetBlendState(BlendState.Additive);
            var center = new Vector2(centerX, centerY);
            // Fine, smoothly sampled arcs leave space between elemental accents.
            for (int i = 0; i < 5; i++)
            {
                float angle = i * MathHelper.TwoPi / 5f + (seed & 31u) * 0.025f;
                CombatVisualEffect.DrawArc(batcher, center, radius, radius * 0.46f,
                    angle, angle + 0.8f, glowColor, alpha * 0.20f, 2.5f, depth);
                CombatVisualEffect.DrawArc(batcher, center, radius, radius * 0.46f,
                    angle, angle + 0.8f, coreColor, alpha * 0.25f, 0.8f, depth);
            }
        }


        private static void GetPalette(
            HitAreaElement element,
            out Color glow,
            out Color core)
        {
            switch (element)
            {
                case HitAreaElement.Fire:
                    glow = new Color(255, 70, 18, 255);
                    core = new Color(255, 224, 82, 255);
                    break;
                case HitAreaElement.Cold:
                    glow = new Color(72, 154, 255, 255);
                    core = new Color(224, 250, 255, 255);
                    break;
                case HitAreaElement.Poison:
                    glow = new Color(73, 218, 67, 255);
                    core = new Color(200, 92, 255, 255);
                    break;
                default:
                    glow = new Color(136, 55, 255, 255);
                    core = new Color(92, 238, 255, 255);
                    break;
            }
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }
    }
}

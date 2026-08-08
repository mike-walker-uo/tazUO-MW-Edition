#region license
// TazUO addition. Element-specific presentation for weapon hit-area procs.
#endregion

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Renderer;
using ClassicUO.Utility;
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

    internal sealed class HitAreaEffect : GameEffect
    {
        private readonly uint _born;
        private readonly HitAreaElement _element;
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

            if (IsDestroyed || _element == HitAreaElement.None)
            {
                return drewOriginal;
            }

            float progress = Math.Min(1f, (Time.Ticks - _born) / (float)_visualDuration);
            float fade = 1f - progress;

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

        private void DrawElementAccents(
            UltimaBatcher2D batcher,
            int centerX,
            int centerY,
            float radius,
            float progress,
            float fade,
            uint seed,
            Color glowColor,
            Color coreColor,
            float depth)
        {
            Texture2D glow = SolidColorTextureCache.GetTexture(glowColor);
            Texture2D core = SolidColorTextureCache.GetTexture(coreColor);
            Vector3 glowHue = ShaderHueTranslator.GetHueVector(0, false, fade * 0.42f);
            Vector3 coreHue = ShaderHueTranslator.GetHueVector(0, false, fade * 0.9f);

            batcher.SetBlendState(BlendState.Additive);

            switch (_element)
            {
                case HitAreaElement.Fire:
                    for (int i = 0; i < 7; i++)
                    {
                        uint h = Mix(seed + (uint)(i * 71));
                        float x = centerX + ((h & 31u) - 15f);
                        float rise = 9f + ((h >> 5) & 23u);
                        float y = centerY - 4f - progress * rise;
                        DrawLine(
                            batcher,
                            glow,
                            new Vector2(x, y + 7f),
                            new Vector2(x + ((h >> 10) % 5u) - 2f, y),
                            glowHue,
                            3f,
                            depth
                        );
                        DrawLine(
                            batcher,
                            core,
                            new Vector2(x, y + 5f),
                            new Vector2(x, y + 1f),
                            coreHue,
                            1f,
                            depth
                        );
                    }
                    break;

                case HitAreaElement.Cold:
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * (float)(Math.PI * 2.0) / 8f + 0.16f;
                        Vector2 inner = EllipsePoint(centerX, centerY, radius * 0.62f, angle);
                        Vector2 outer = EllipsePoint(centerX, centerY, radius * 0.94f, angle);
                        DrawLine(batcher, glow, inner, outer, glowHue, 3.5f, depth);
                        DrawLine(batcher, core, inner, outer, coreHue, 1.2f, depth);
                    }
                    break;

                case HitAreaElement.Poison:
                    for (int i = 0; i < 6; i++)
                    {
                        uint h = Mix(seed + (uint)(i * 97));
                        float x = centerX + ((h & 47u) - 23f);
                        float y = centerY - progress * (12f + ((h >> 6) & 23u));
                        int size = 2 + (int)((h >> 12) & 3u);
                        batcher.Draw(
                            core,
                            new Vector2(x, y),
                            core.Bounds,
                            ShaderHueTranslator.GetHueVector(0, false, fade * 0.72f),
                            0f,
                            Vector2.Zero,
                            new Vector2(size, size),
                            SpriteEffects.None,
                            depth
                        );
                    }
                    break;

                case HitAreaElement.Energy:
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * (float)(Math.PI * 2.0) / 6f + progress * 0.7f;
                        Vector2 start = EllipsePoint(centerX, centerY, radius * 0.45f, angle);
                        Vector2 kink = EllipsePoint(
                            centerX,
                            centerY,
                            radius * 0.69f,
                            angle + (i % 2 == 0 ? 0.14f : -0.14f)
                        );
                        Vector2 end = EllipsePoint(centerX, centerY, radius, angle);
                        DrawLine(batcher, glow, start, kink, glowHue, 4f, depth);
                        DrawLine(batcher, glow, kink, end, glowHue, 4f, depth);
                        DrawLine(batcher, core, start, kink, coreHue, 1.2f, depth);
                        DrawLine(batcher, core, kink, end, coreHue, 1.2f, depth);
                    }
                    break;
            }
        }

        private static void DrawBrokenRing(
            UltimaBatcher2D batcher,
            int centerX,
            int centerY,
            float radius,
            uint seed,
            Color glowColor,
            Color coreColor,
            float alpha,
            float depth)
        {
            const int segments = 16;
            Texture2D glow = SolidColorTextureCache.GetTexture(glowColor);
            Texture2D core = SolidColorTextureCache.GetTexture(coreColor);
            Vector3 glowHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.34f);
            Vector3 coreHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.82f);

            batcher.SetBlendState(BlendState.AlphaBlend);
            for (int i = 0; i < segments; i++)
            {
                if ((seed + (uint)i) % 5u == 0u) continue;
                float a0 = i * (float)(Math.PI * 2.0) / segments;
                float a1 = (i + 0.78f) * (float)(Math.PI * 2.0) / segments;
                DrawLine(
                    batcher,
                    glow,
                    EllipsePoint(centerX, centerY, radius, a0),
                    EllipsePoint(centerX, centerY, radius, a1),
                    glowHue,
                    6f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);
            for (int i = 0; i < segments; i++)
            {
                if ((seed + (uint)i) % 5u == 0u) continue;
                float a0 = i * (float)(Math.PI * 2.0) / segments;
                float a1 = (i + 0.78f) * (float)(Math.PI * 2.0) / segments;
                DrawLine(
                    batcher,
                    core,
                    EllipsePoint(centerX, centerY, radius, a0),
                    EllipsePoint(centerX, centerY, radius, a1),
                    coreHue,
                    1.5f,
                    depth
                );
            }
        }

        private static Vector2 EllipsePoint(int x, int y, float radius, float angle)
        {
            return new Vector2(
                x + (float)Math.Cos(angle) * radius,
                y + (float)Math.Sin(angle) * radius * 0.46f
            );
        }

        private static void DrawLine(
            UltimaBatcher2D batcher,
            Texture2D texture,
            Vector2 start,
            Vector2 end,
            Vector3 hue,
            float width,
            float depth)
        {
            float rotation = Utility.MathHelper.AngleBetweenVectors(start, end);
            float length = Vector2.Distance(start, end);
            batcher.Draw(
                texture,
                start,
                texture.Bounds,
                hue,
                rotation,
                Vector2.Zero,
                new Vector2(length, width),
                SpriteEffects.None,
                depth
            );
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

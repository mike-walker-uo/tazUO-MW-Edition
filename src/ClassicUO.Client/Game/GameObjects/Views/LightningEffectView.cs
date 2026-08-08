#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.IO;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal sealed partial class LightningEffect
    {
        private uint _skyBoltSeed;
        private bool _skyBoltSeedInitialized;

        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            if (
                !_forceEnhanced
                && !SpellAbilityEffectSettings.IsEnabled(
                    SpellAbilityEffectId.Lightning
                )
            )
            {
                return DrawClassic(batcher, posX, posY, depth);
            }

            int visibleHeight = 0;
            int impactLightX = posX + 22;
            int impactLightY = posY + 22;
            if (Source is Mobile mobile)
            {
                depth = Source.CalculateDepthZ() + 1f;
                Rectangle visibleBounds = mobile.GetOnScreenRectangle();
                Point impact = CalculateMobileImpactPoint(visibleBounds, posX, posY);
                impactLightX = visibleBounds.Width > 0 ? impact.X : impactLightX;
                impactLightY = visibleBounds.Height > 0
                    ? visibleBounds.Bottom - 4
                    : impactLightY;
                posX = impact.X;
                posY = impact.Y;
                visibleHeight = visibleBounds.Height;
            }

            batcher.SetBlendState(BlendState.Additive);

            float boltAlpha = CalculateBoltAlpha(AnimIndex);
            if (boltAlpha > 0f)
            {
                float boltSize = CalculateBoltSize(AnimIndex);
                float impactLightStrength = System.Math.Min(
                    1f,
                    boltAlpha * (0.48f + boltSize * 0.28f)
                );
                SceneryInteractionManager.RecordTransientLight(
                    impactLightX,
                    impactLightY,
                    new Color(142, 72, 255, 255),
                    170f + boltSize * 84f,
                    impactLightStrength * 0.64f
                );
                SceneryInteractionManager.RecordTransientLight(
                    impactLightX,
                    impactLightY,
                    new Color(244, 246, 255, 255),
                    62f + boltSize * 27f,
                    impactLightStrength
                );
                Client.Game
                    .GetScene<GameScene>()
                    ?.AddTransientLight(
                        impactLightX,
                        impactLightY,
                        2,
                        impactLightStrength
                    );

                int primaryLength = CalculateBoltLength(visibleHeight);
                uint seed = CalculateBoltSeed();

                DrawSkyBolt(
                    batcher,
                    posX,
                    posY,
                    impactLightX,
                    impactLightY,
                    primaryLength,
                    seed,
                    boltAlpha,
                    boltSize,
                    depth,
                    -82f
                );
            }

            batcher.SetBlendState(null);

            return true;
        }

        private bool DrawClassic(
            UltimaBatcher2D batcher,
            int posX,
            int posY,
            float depth)
        {
            ushort hue = Hue;
            Profile profile = ProfileManager.CurrentProfile;

            if (
                profile != null
                && profile.NoColorObjectsOutOfRange
                && Distance > World.ClientViewRange
            )
            {
                hue = Constants.OUT_RANGE_COLOR;
            }
            else if (
                World.Player.IsDead
                && profile != null
                && profile.EnableBlackWhiteEffect
            )
            {
                hue = Constants.DEAD_RANGE_COLOR;
            }

            Vector3 hueVector =
                ShaderHueTranslator.GetHueVector(hue, false, 1);
            hueVector.Y = hueVector.X > 1f
                ? ShaderHueTranslator.SHADER_LIGHTS
                : ShaderHueTranslator.SHADER_NONE;

            ref UOFileIndex index =
                ref GumpsLoader.Instance.GetValidRefEntry(AnimationGraphic);
            posX -= index.Width >> 1;
            posY -= index.Height;

            batcher.SetBlendState(BlendState.Additive);
            DrawGump(
                batcher,
                AnimationGraphic,
                posX,
                posY,
                hueVector,
                depth
            );
            batcher.SetBlendState(null);
            return true;
        }

        internal static Point CalculateMobileImpactPoint(
            Rectangle visibleBounds,
            int tileScreenX,
            int tileScreenY)
        {
            if (visibleBounds.Width > 0 && visibleBounds.Height > 0)
            {
                return new Point(
                    visibleBounds.X + (visibleBounds.Width >> 1),
                    visibleBounds.Y + (visibleBounds.Height >> 1)
                );
            }

            return new Point(tileScreenX + 22, tileScreenY - 32);
        }

        internal static int CalculateBoltLength(int visibleHeight)
        {
            const int minimumLength = 520;
            const int maximumLength = 700;

            return System.Math.Max(
                minimumLength,
                System.Math.Min(
                    maximumLength,
                    460 + System.Math.Max(0, visibleHeight) * 2
                )
            );
        }

        internal static float CalculateBoltAlpha(int animationIndex)
        {
            switch (animationIndex)
            {
                case 0:
                case 1:
                    return 1f;
                case 2:
                    return 0.68f;
                case 3:
                    return 0.12f;
                case 4:
                    return 0.82f;
                case 5:
                    return 0.48f;
                case 6:
                    return 0.22f;
                default:
                    return 0f;
            }
        }

        internal static float CalculateBoltSize(int animationIndex)
        {
            switch (animationIndex)
            {
                case 0:
                    return 0.58f;
                case 1:
                    return 4.2f;
                case 2:
                    return 1.16f;
                case 3:
                    return 0.34f;
                case 4:
                    return 3.1f;
                case 5:
                    return 1.22f;
                case 6:
                    return 0.44f;
                default:
                    return 0f;
            }
        }

        private uint CalculateBoltSeed()
        {
            if (!_skyBoltSeedInitialized)
            {
                unchecked
                {
                    uint seed = Source is Entity entity ? entity.Serial : 0u;
                    seed ^= (uint)X * 0x9E3779B9u;
                    seed ^= (uint)Y * 0x85EBCA6Bu;
                    _skyBoltSeed = Mix(seed);
                }

                _skyBoltSeedInitialized = true;
            }

            return _skyBoltSeed;
        }

        private static void DrawSkyBolt(
            UltimaBatcher2D batcher,
            int impactX,
            int impactY,
            int groundX,
            int groundY,
            int length,
            uint seed,
            float alpha,
            float sizeScale,
            float depth,
            float skyOffsetX)
        {
            const int pointCount = 19;

            Texture2D glowTexture =
                SolidColorTextureCache.GetTexture(new Color(118, 52, 238, 255));
            Texture2D auraTexture =
                SolidColorTextureCache.GetTexture(new Color(70, 165, 255, 255));
            Texture2D bodyTexture =
                SolidColorTextureCache.GetTexture(new Color(168, 220, 255, 255));
            Texture2D coreTexture =
                SolidColorTextureCache.GetTexture(new Color(255, 252, 255, 255));

            Vector3 glowHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.28f);
            Vector3 auraHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.30f);
            Vector3 bodyHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.72f);
            Vector3 coreHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.96f);
            float auraScale = 0.62f + sizeScale * 0.58f;
            float bodyScale = 0.82f + sizeScale * 0.24f;
            float coreScale = 0.92f + sizeScale * 0.10f;

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawBoltPass(
                batcher, glowTexture, glowHue, 12f * sizeScale,
                impactX, impactY, length, seed, pointCount, depth, skyOffsetX
            );
            DrawBoltPass(
                batcher, auraTexture, auraHue, 6f * auraScale,
                impactX, impactY, length, seed, pointCount, depth, skyOffsetX
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawBoltPass(
                batcher, bodyTexture, bodyHue, 3.2f * bodyScale,
                impactX, impactY, length, seed, pointCount, depth, skyOffsetX
            );
            DrawBoltPass(
                batcher, coreTexture, coreHue, 1.5f * coreScale,
                impactX, impactY, length, seed, pointCount, depth, skyOffsetX
            );

            int forkIndex = 10 + (int)(seed & 1u);
            Vector2 forkStart = CalculateBoltPoint(
                impactX, impactY, length, seed, forkIndex, pointCount, skyOffsetX
            );
            float direction = (seed & 2u) == 0u ? -1f : 1f;
            Vector2 forkMiddle = new Vector2(
                forkStart.X + direction * (12 + (int)(seed % 6u)),
                forkStart.Y + 10
            );
            Vector2 forkKink = new Vector2(
                forkMiddle.X + direction * (8 + (int)((seed >> 4) % 5u)),
                forkMiddle.Y + 9
            );
            Vector2 forkEnd = new Vector2(
                forkKink.X + direction * (10 + (int)((seed >> 8) % 6u)),
                forkKink.Y + 13
            );

            DrawBranch(
                batcher, forkStart, forkMiddle,
                glowTexture, auraTexture, coreTexture, alpha, depth, 0.48f
            );
            DrawBranch(
                batcher, forkMiddle, forkKink,
                glowTexture, auraTexture, coreTexture, alpha, depth, 0.34f
            );
            DrawBranch(
                batcher, forkKink, forkEnd,
                glowTexture, auraTexture, coreTexture, alpha, depth, 0.20f
            );
            DrawImpactBurst(
                batcher,
                impactX,
                impactY,
                groundX,
                groundY,
                seed,
                alpha,
                sizeScale,
                glowTexture,
                auraTexture,
                coreTexture,
                depth
            );
        }

        private static void DrawBranch(
            UltimaBatcher2D batcher,
            Vector2 start,
            Vector2 end,
            Texture2D glowTexture,
            Texture2D auraTexture,
            Texture2D coreTexture,
            float alpha,
            float depth,
            float strength)
        {
            Vector3 glowHue = ShaderHueTranslator.GetHueVector(
                0, false, alpha * 0.24f * strength
            );
            Vector3 auraHue = ShaderHueTranslator.GetHueVector(
                0, false, alpha * 0.4f * strength
            );
            Vector3 coreHue = ShaderHueTranslator.GetHueVector(
                0, false, alpha * 0.9f * strength
            );

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawLine(batcher, glowTexture, start, end, glowHue, 5f * strength, depth);
            DrawLine(batcher, auraTexture, start, end, auraHue, 2.5f * strength, depth);

            batcher.SetBlendState(BlendState.Additive);
            DrawLine(batcher, coreTexture, start, end, coreHue, 0.9f, depth);
        }

        private static void DrawBoltPass(
            UltimaBatcher2D batcher,
            Texture2D texture,
            Vector3 hue,
            float width,
            int impactX,
            int impactY,
            int length,
            uint seed,
            int pointCount,
            float depth,
            float skyOffsetX)
        {
            Vector2 previous = CalculateBoltPoint(
                impactX, impactY, length, seed, 0, pointCount, skyOffsetX
            );

            for (int i = 1; i < pointCount; i++)
            {
                Vector2 next = CalculateBoltPoint(
                    impactX, impactY, length, seed, i, pointCount, skyOffsetX
                );
                float progress = i / (float)(pointCount - 1);
                float taperedWidth = width * (0.38f + progress * 0.62f);
                DrawLine(batcher, texture, previous, next, hue, taperedWidth, depth);
                previous = next;
            }
        }

        private static void DrawImpactBurst(
            UltimaBatcher2D batcher,
            int impactX,
            int impactY,
            int groundX,
            int groundY,
            uint seed,
            float alpha,
            float sizeScale,
            Texture2D glowTexture,
            Texture2D auraTexture,
            Texture2D coreTexture,
            float depth)
        {
            Texture2D bloom = ClassicUO.Game.Weather.GetFogBlob();
            float bloomDiameter = 56f * (0.62f + sizeScale * 0.56f);
            float bloomScale = bloomDiameter / bloom.Width;

            batcher.SetBlendState(BlendState.Additive);
            batcher.Draw(
                bloom,
                new Vector2(
                    impactX - bloomDiameter * 0.5f,
                    impactY - bloomDiameter * 0.5f
                ),
                bloom.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, alpha * 0.52f),
                0f,
                Vector2.Zero,
                new Vector2(bloomScale, bloomScale),
                SpriteEffects.None,
                depth
            );

            Vector3 groundGlow =
                ShaderHueTranslator.GetHueVector(0, false, alpha * 0.16f);
            Vector3 groundAura =
                ShaderHueTranslator.GetHueVector(0, false, alpha * 0.34f);
            Vector3 groundCore =
                ShaderHueTranslator.GetHueVector(0, false, alpha * 0.74f);

            for (int i = 0; i < 3; i++)
            {
                uint mixed = Mix(seed ^ (uint)(0x51ED + i * 0x9E37));
                float angle =
                    ((mixed & 1023u) / 1023f) * (float)(System.Math.PI * 2.0);
                float length =
                    (18f + ((mixed >> 10) & 15u))
                    * (0.75f + sizeScale * 0.20f);
                Vector2 start = new Vector2(groundX, groundY - 2);
                Vector2 kink = new Vector2(
                    start.X + (float)System.Math.Cos(angle) * length * 0.52f,
                    start.Y + (float)System.Math.Sin(angle) * length * 0.22f
                );
                Vector2 end = new Vector2(
                    start.X + (float)System.Math.Cos(angle) * length,
                    start.Y + (float)System.Math.Sin(angle) * length * 0.42f
                );
                kink.X += (int)((mixed >> 15) & 5u) - 2;
                kink.Y += (int)((mixed >> 18) & 3u) - 1;

                batcher.SetBlendState(BlendState.AlphaBlend);
                DrawLine(batcher, glowTexture, start, kink, groundGlow, 4f, depth);
                DrawLine(batcher, glowTexture, kink, end, groundGlow, 3f, depth);
                DrawLine(batcher, auraTexture, start, kink, groundAura, 2f, depth);
                DrawLine(batcher, auraTexture, kink, end, groundAura, 1.6f, depth);

                batcher.SetBlendState(BlendState.Additive);
                DrawLine(batcher, coreTexture, start, kink, groundCore, 0.9f, depth);
                DrawLine(batcher, coreTexture, kink, end, groundCore, 0.75f, depth);
            }
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

        private static Vector2 CalculateBoltPoint(
            int impactX,
            int impactY,
            int length,
            uint seed,
            int index,
            int pointCount,
            float skyOffsetX)
        {
            float progress = index / (float)(pointCount - 1);
            float y = impactY - length + length * progress;

            if (index == pointCount - 1)
            {
                return new Vector2(impactX, impactY);
            }

            uint mixed = Mix(seed + (uint)(index * 0x9E37));
            int amplitude = index == 0 ? 8 : 15;
            float jitter = (int)(mixed % (uint)(amplitude * 2 + 1)) - amplitude;
            float startDrift = ((int)((seed >> 8) % 17u) - 8) * (1f - progress);
            float skyDrift = skyOffsetX * (1f - progress);

            return new Vector2(impactX + skyDrift + startDrift + jitter, y);
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }
    }
}

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

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal sealed class MovingEffect : GameEffect
    {        
        public MovingEffect
        (
            EffectManager manager,
            uint src,
            uint trg,
            ushort xSource,
            ushort ySource,
            sbyte zSource,
            ushort xTarget,
            ushort yTarget,
            sbyte zTarget,
            ushort graphic,
            ushort hue,
            bool fixedDir,
            int duration,
            byte speed
        ) : base(manager, graphic, hue, 0, speed)
        {
            FixedDir = fixedDir;

            // we override interval time with speed
            var d = Constants.ITEM_EFFECT_ANIMATION_DELAY * 2;

            IntervalInMs = (uint)(d + (speed * d));

            // moving effects want a +22 to the X
            Offset.X += 22;

            Entity source = World.Get(src);

            if (SerialHelper.IsValid(src) && source != null)
            {
                SetSource(source);
            }
            else
            {
                SetSource(xSource, ySource, zSource);
            }


            Entity target = World.Get(trg);

            if (SerialHelper.IsValid(trg) && target != null)
            {
                SetTarget(target);
            }
            else
            {
                SetTarget(xTarget, yTarget, zTarget);
            }
        }

        public readonly bool FixedDir;
        public CombatVisualKind CombatVisual;
        public EnhancedSpellVisualKind EnhancedSpellVisual;


        public override void Update()
        {
            base.Update();
            UpdateOffset();
        }


        private void UpdateOffset()
        {
            if (Target != null && Target.IsDestroyed)
            {
                TargetX = Target.X;
                TargetY = Target.Y;
                TargetZ = Target.Z;
            }

            int playerX = World.Player.X;
            int playerY = World.Player.Y;
            int playerZ = World.Player.Z;

            (int sX, int sY, int sZ) = GetSource();
            int offsetSourceX = sX - playerX;
            int offsetSourceY = sY - playerY;
            int offsetSourceZ = sZ - playerZ;

            (int tX, int tY, int tZ) = GetTarget();
            int offsetTargetX = tX - playerX;
            int offsetTargetY = tY - playerY;
            int offsetTargetZ = tZ - playerZ;

            Vector2 source = new Vector2((offsetSourceX - offsetSourceY) * 22, (offsetSourceX + offsetSourceY) * 22 - offsetSourceZ * 4);

            source.X += Offset.X;
            source.Y += Offset.Y;

            Vector2 target = new Vector2((offsetTargetX - offsetTargetY) * 22, (offsetTargetX + offsetTargetY) * 22 - offsetTargetZ * 4);

            var offset = target - source;
            var distance = offset.Length();
            var frameIndependentSpeed = IntervalInMs * Time.Delta;
            Vector2 s0;

            if (distance > frameIndependentSpeed)
            {
                offset.Normalize();
                s0 = offset * frameIndependentSpeed;
            }
            else
            {
                s0 = target;
            }


            if (distance <= 22)
            {
                RemoveMe();

                return;
            }

            int newOffsetX = (int) (source.X / 22f);
            int newOffsetY = (int) (source.Y / 22f);

            TileOffsetOnMonitorToXY(ref newOffsetX, ref newOffsetY, out int newCoordX, out int newCoordY);

            int newX = playerX + newCoordX;
            int newY = playerY + newCoordY;

            if (newX == tX && newY == tY)
            {
                RemoveMe();

                return;
            }


            IsPositionChanged = true;
            AngleToTarget = (float) Math.Atan2(-offset.Y, -offset.X);

            if (newX != sX || newY != sY)
            {
                // TODO: Z is wrong. We have to calculate an average
                SetSource((ushort) newX, (ushort) newY, (sbyte)sZ);

                Vector2 nextSource = new Vector2((newCoordX - newCoordY) * 22, (newCoordX + newCoordY) * 22 - offsetSourceZ * 4);

                Offset.X = source.X - nextSource.X;
                Offset.Y = source.Y - nextSource.Y;
            }

            Offset.X += s0.X;
            Offset.Y += s0.Y;
        }


        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            bool drewOriginal = base.Draw(batcher, posX, posY, depth);

            bool hasCombatTrail =
                CombatVisual == CombatVisualKind.MagicArrow
                || CombatVisual == CombatVisualKind.Fireball;
            bool hasSpellTrail =
                EnhancedSpellVisual != EnhancedSpellVisualKind.None
                && (
                    !EnhancedSpellVisualPolicy.ReplacesMovingProjectile(
                        EnhancedSpellVisual
                    )
                    || EnhancedSpellVisual == EnhancedSpellVisualKind.WordOfDeath
                );

            if (IsDestroyed || !hasCombatTrail && !hasSpellTrail)
            {
                return drewOriginal;
            }

            Vector2 center = new Vector2(
                posX + Offset.X + 22f,
                posY + Offset.Z + Offset.Y + 22f
            );
            Vector2 trailDirection = new Vector2(
                (float)Math.Cos(AngleToTarget),
                (float)Math.Sin(AngleToTarget)
            );
            depth += 0.02f;

            if (hasCombatTrail)
            {
                bool fire = CombatVisual == CombatVisualKind.Fireball;

                if (fire)
                {
                    CombatVisualEffect.DrawFireballProjectile(
                        batcher,
                        center,
                        trailDirection,
                        depth
                    );
                }
                else
                {
                    Color glow = new Color(157, 73, 255);
                    Color core = new Color(224, 205, 255);
                    batcher.SetBlendState(BlendState.Additive);

                    for (int i = 1; i <= 6; i++)
                    {
                        float distance = 5f + i * 5.5f;
                        float alpha = (7f - i) / 7f;
                        Vector2 point =
                            center
                            + trailDirection * distance
                            + new Vector2(0f, i % 2 == 0 ? 3f : -3f);
                        CombatVisualEffect.DrawPoint(
                            batcher,
                            point,
                            i < 3 ? core : glow,
                            alpha * 0.68f,
                            4.2f - i * 0.25f,
                            depth
                        );
                    }

                    float pulse =
                        7f + (float)Math.Sin(Time.Ticks * 0.025f) * 2f;
                    CombatVisualEffect.DrawPoint(
                        batcher,
                        center,
                        glow,
                        0.48f,
                        pulse,
                        depth
                    );
                    CombatVisualEffect.DrawPoint(
                        batcher,
                        center,
                        core,
                        0.92f,
                        3.2f,
                        depth
                    );
                    CombatVisualEffect.RecordLight(
                        center,
                        glow,
                        96f,
                        0.55f
                    );
                }
            }

            if (hasSpellTrail)
            {
                EnhancedSpellVisualEffect.DrawProjectile(
                    batcher,
                    EnhancedSpellVisual,
                    center,
                    trailDirection,
                    depth + 0.002f
                );
            }

            batcher.SetBlendState(null);
            return true;
        }

        private void RemoveMe()
        {
            CreateExplosionEffect();
            CreateTargetCombatVisualEffect(CombatVisual);
            CreateTargetEnhancedSpellVisualEffect(EnhancedSpellVisual);

            Destroy();
        }

        private static void TileOffsetOnMonitorToXY(ref int ofsX, ref int ofsY, out int x, out int y)
        {
            y = 0;

            if (ofsX == 0)
            {
                x = y = ofsY >> 1;
            }
            else if (ofsY == 0)
            {
                x = ofsX >> 1;
                y = -x;
            }
            else
            {
                int absX = Math.Abs(ofsX);
                int absY = Math.Abs(ofsY);
                x = ofsX;

                if (ofsY > ofsX)
                {
                    if (ofsX < 0 && ofsY < 0)
                    {
                        y = absX - absY;
                    }
                    else if (ofsX > 0 && ofsY > 0)
                    {
                        y = absY - absX;
                    }
                }
                else if (ofsX > ofsY)
                {
                    if (ofsX < 0 && ofsY < 0)
                    {
                        y = -(absY - absX);
                    }
                    else if (ofsX > 0 && ofsY > 0)
                    {
                        y = -(absX - absY);
                    }
                }

                if (y == 0 && ofsY != ofsX)
                {
                    if (ofsY < 0)
                    {
                        y = -(absX + absY);
                    }
                    else
                    {
                        y = absX + absY;
                    }
                }

                y /= 2;
                x += y;
            }
        }
    }
}

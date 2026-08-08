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
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    internal class EffectManager : LinkedObject
    {
        private uint _lastMeteorSwarmVisual;
        private ushort _lastMeteorSwarmX;
        private ushort _lastMeteorSwarmY;
        private uint _lastWordOfDeathVisual;
        private ushort _lastWordOfDeathX;
        private ushort _lastWordOfDeathY;

        public void Update()
        {
            NetherBlastVortexManager.ResolvePending();

            for (GameEffect f = (GameEffect) Items; f != null;)
            {
                GameEffect next = (GameEffect) f.Next;

                f.Update();

                if (!f.IsDestroyed && f.Distance > World.ClientViewRange)
                {
                    f.Destroy();
                }

                f = next;
            }
        }


        public void CreateEffect
        (
            GraphicEffectType type,
            uint source,
            uint target,
            ushort graphic,
            ushort hue,
            ushort srcX,
            ushort srcY,
            sbyte srcZ,
            ushort targetX,
            ushort targetY,
            sbyte targetZ,
            byte speed,
            int duration,
            bool fixedDir,
            bool doesExplode,
            bool hasparticles,
            GraphicEffectBlendMode blendmode,
            ushort particleEffect
        )
        {
            if (!SpellAbilityEffectSettings.OriginalEffectsEnabled)
            {
                return;
            }

            if (hasparticles)
            {
                Log.Warn("Unhandled particles in an effects packet.");
            }

            GameEffect effect;
            HitAreaElement hitAreaElement =
                type == GraphicEffectType.FixedFrom
                    ? HitAreaEffect.Classify(graphic, hue)
                    : HitAreaElement.None;
            CombatVisualKind movingVisualKind =
                type == GraphicEffectType.Moving
                    ? CombatVisualEffect.ClassifyMoving(graphic, hue)
                    : CombatVisualKind.None;
            CombatVisualKind fixedVisualKind =
                type == GraphicEffectType.FixedFrom
                    ? CombatVisualEffect.ClassifyFixed(graphic, hue)
                    : CombatVisualKind.None;
            EnhancedSpellVisualKind spellVisualKind =
                EnhancedSpellVisualPolicy.Classify(
                    type,
                    graphic,
                    hue,
                    particleEffect
                );
            hitAreaElement = SpellAbilityEffectSettings.Filter(hitAreaElement);
            movingVisualKind =
                SpellAbilityEffectSettings.Filter(movingVisualKind);
            fixedVisualKind =
                SpellAbilityEffectSettings.Filter(fixedVisualKind);
            spellVisualKind =
                SpellAbilityEffectSettings.Filter(spellVisualKind);
            EnhancedSpellVisualTrigger.OnEffectPacket(
                type,
                graphic,
                hue,
                srcX,
                srcY,
                srcZ
            );
            if (
                SpellAbilityEffectSettings.IsEnabled(
                    SpellAbilityEffectId.DeathRay
                )
            )
            {
                DeathRayVisualManager.ObservePacket(
                    this,
                    type,
                    source,
                    graphic,
                    hue,
                    particleEffect,
                    srcX,
                    srcY,
                    srcZ
                );
            }

            if (hue != 0)
            {
                hue++;
            }

            duration *= Constants.ITEM_EFFECT_ANIMATION_DELAY;

            switch (type)
            {
                case GraphicEffectType.Moving:
                    if (graphic <= 0)
                    {
                        return;
                    }

                    // TODO: speed == 0 means run at standard frameInterval got from anim.mul?
                    if (speed == 0)
                    {
                        speed++;
                    }

                    effect = new MovingEffect
                    (
                        this,
                        source,
                        target,
                        srcX,
                        srcY,
                        srcZ,
                        targetX,
                        targetY,
                        targetZ,
                        graphic,
                        hue,
                        fixedDir,
                        duration,
                        speed
                    )
                    {
                        Blend = blendmode,
                        CanCreateExplosionEffect = doesExplode,
                        CombatVisual = movingVisualKind,
                        EnhancedSpellVisual = spellVisualKind
                    };

                    break;

                case GraphicEffectType.DragEffect:

                    if (graphic <= 0)
                    {
                        return;
                    }

                    if (speed == 0)
                    {
                        speed++;
                    }

                    effect = new DragEffect
                    (
                        this,
                        source,
                        target,
                        srcX,
                        srcY,
                        srcZ,
                        targetX,
                        targetY,
                        targetZ,
                        graphic,
                        hue,
                        duration,
                        speed
                    )
                    {
                        Blend = blendmode,
                        CanCreateExplosionEffect = doesExplode
                    };

                    break;

                case GraphicEffectType.Lightning:
                    effect = new LightningEffect
                    (
                        this,
                        source,
                        srcX,
                        srcY,
                        srcZ,
                        hue
                    );

                    break;

                case GraphicEffectType.FixedXYZ:

                    if (graphic <= 0)
                    {
                        return;
                    }

                    effect = new FixedEffect
                    (
                        this,
                        srcX,
                        srcY,
                        srcZ,
                        graphic,
                        hue,
                        duration,
                        0 //speed [use 50ms]
                    )
                    {
                        Blend = blendmode
                    };

                    break;

                case GraphicEffectType.FixedFrom:

                    if (graphic <= 0)
                    {
                        return;
                    }

                    effect =
                        hitAreaElement != HitAreaElement.None
                            ? new HitAreaEffect
                            (
                                this,
                                source,
                                srcX,
                                srcY,
                                srcZ,
                                graphic,
                                hue,
                                duration,
                                0, //speed [use 50ms]
                                hitAreaElement
                            )
                            {
                                Blend = blendmode
                            }
                            : new FixedEffect
                            (
                                this,
                                source,
                                srcX,
                                srcY,
                                srcZ,
                                graphic,
                                hue,
                                duration,
                                0 //speed [use 50ms]
                            )
                            {
                                Blend = blendmode
                            };

                    break;

                case GraphicEffectType.ScreenFade:
                    Log.Warn("Unhandled 'Screen Fade' effect.");

                    return;

                default:
                    Log.Warn("Unhandled effect.");

                    return;
            }


            if (
                CombatVisualEffect.ReplacesOriginal(fixedVisualKind)
                || CombatVisualEffect.ReplacesOriginal(movingVisualKind)
                || EnhancedSpellVisualPolicy.ReplacesMovingProjectile(
                    spellVisualKind
                )
            )
            {
                effect.AllowedToDraw = false;
            }

            PushToBack(effect);

            NetherBlastVortexManager.RecordCreation(
                this,
                effect,
                graphic,
                particleEffect,
                srcX,
                srcY,
                srcZ
            );

            if (fixedVisualKind != CombatVisualKind.None)
            {
                CreateCombatVisual(source, srcX, srcY, srcZ, fixedVisualKind);
            }

            if (
                type != GraphicEffectType.Moving
                && spellVisualKind != EnhancedSpellVisualKind.None
                && spellVisualKind != EnhancedSpellVisualKind.Thunderstorm
                && spellVisualKind != EnhancedSpellVisualKind.DeathRay
            )
            {
                CreateEnhancedSpellVisual(
                    source,
                    srcX,
                    srcY,
                    srcZ,
                    spellVisualKind
                );
            }
        }

        internal void CreateCombatVisual(
            uint source,
            ushort sourceX,
            ushort sourceY,
            sbyte sourceZ,
            CombatVisualKind kind)
        {
            if (kind == CombatVisualKind.None)
            {
                return;
            }

            PushToBack(
                new CombatVisualEffect(
                    this,
                    source,
                    sourceX,
                    sourceY,
                    sourceZ,
                    kind
                )
            );
        }

        internal void CreateHitAreaVisual(
            ushort sourceX,
            ushort sourceY,
            sbyte sourceZ,
            HitAreaElement element)
        {
            ushort hue;

            switch (element)
            {
                case HitAreaElement.Fire:
                    hue = 1160;
                    break;
                case HitAreaElement.Cold:
                    hue = 2100;
                    break;
                case HitAreaElement.Poison:
                    hue = 1166;
                    break;
                case HitAreaElement.Energy:
                    hue = 120;
                    break;
                default:
                    return;
            }

            PushToBack(
                new HitAreaEffect(
                    this,
                    0,
                    sourceX,
                    sourceY,
                    sourceZ,
                    0x3779,
                    hue,
                    800,
                    0,
                    element
                )
            );
        }

        internal void CreateCombatVisualPreview(
            CombatVisualKind kind,
            ushort targetX,
            ushort targetY,
            sbyte targetZ)
        {
            if (World.Player == null)
            {
                return;
            }

            if (
                kind != CombatVisualKind.MagicArrow
                && kind != CombatVisualKind.Fireball
            )
            {
                CreateCombatVisual(0, targetX, targetY, targetZ, kind);
                return;
            }

            PushToBack(
                new MovingEffect(
                    this,
                    World.Player.Serial,
                    0,
                    World.Player.X,
                    World.Player.Y,
                    World.Player.Z,
                    targetX,
                    targetY,
                    targetZ,
                    0x36D4,
                    0,
                    false,
                    0,
                    2
                )
                {
                    AllowedToDraw = false,
                    CombatVisual = kind
                }
            );
        }

        internal void CreateEnhancedSpellVisualPreview(
            EnhancedSpellVisualKind kind,
            ushort targetX,
            ushort targetY,
            sbyte targetZ)
        {
            if (World.Player == null)
            {
                return;
            }

            if (
                kind != EnhancedSpellVisualKind.EnergyBolt
                && kind != EnhancedSpellVisualKind.NetherBolt
                && kind != EnhancedSpellVisualKind.EagleStrike
                && kind != EnhancedSpellVisualKind.Bombard
                && kind != EnhancedSpellVisualKind.WordOfDeath
            )
            {
                CreateEnhancedSpellVisual(0, targetX, targetY, targetZ, kind);
                return;
            }

            PushToBack(
                new MovingEffect(
                    this,
                    World.Player.Serial,
                    0,
                    World.Player.X,
                    World.Player.Y,
                    World.Player.Z,
                    targetX,
                    targetY,
                    targetZ,
                    0x36D4,
                    0,
                    false,
                    0,
                    2
                )
                {
                    AllowedToDraw = false,
                    EnhancedSpellVisual = kind
                }
            );
        }

        internal void CreateLightningPreview(
            ushort targetX,
            ushort targetY,
            sbyte targetZ)
        {
            if (World.Player == null)
            {
                return;
            }

            PushToBack(
                new LightningEffect(
                    this,
                    0,
                    targetX,
                    targetY,
                    targetZ,
                    0,
                    true
                )
            );
        }

        internal void CreateNetherBlastPreview(
            ushort targetX,
            ushort targetY,
            sbyte targetZ)
        {
            if (World.Player == null)
            {
                return;
            }

            PushToBack(
                new NetherBlastVortexEffect(
                    this,
                    targetX,
                    targetY,
                    targetZ
                )
            );
        }

        internal void CreateDeathRayPreview(
            ushort targetX,
            ushort targetY,
            sbyte targetZ)
        {
            if (World.Player == null)
            {
                return;
            }

            PushToBack(
                new DeathRayLinkEffect(
                    this,
                    World.Player.Serial,
                    World.Player.X,
                    World.Player.Y,
                    World.Player.Z,
                    0,
                    targetX,
                    targetY,
                    targetZ
                )
            );
        }

        internal void CreateEnhancedSpellVisual(
            uint source,
            ushort sourceX,
            ushort sourceY,
            sbyte sourceZ,
            EnhancedSpellVisualKind kind)
        {
            if (kind == EnhancedSpellVisualKind.None)
            {
                return;
            }

            if (kind == EnhancedSpellVisualKind.WordOfDeath)
            {
                uint now = Time.Ticks;

                if (
                    _lastWordOfDeathVisual != 0
                    && now - _lastWordOfDeathVisual < 1200
                    && Math.Abs(sourceX - _lastWordOfDeathX) <= 1
                    && Math.Abs(sourceY - _lastWordOfDeathY) <= 1
                )
                {
                    return;
                }

                _lastWordOfDeathVisual = now;
                _lastWordOfDeathX = sourceX;
                _lastWordOfDeathY = sourceY;
            }
            else if (kind == EnhancedSpellVisualKind.MeteorSwarm)
            {
                uint now = Time.Ticks;

                if (
                    _lastMeteorSwarmVisual != 0
                    && now - _lastMeteorSwarmVisual < 2600
                    && Math.Abs(sourceX - _lastMeteorSwarmX) <= 5
                    && Math.Abs(sourceY - _lastMeteorSwarmY) <= 5
                )
                {
                    return;
                }

                _lastMeteorSwarmVisual = now;
                _lastMeteorSwarmX = sourceX;
                _lastMeteorSwarmY = sourceY;
            }

            PushToBack(
                new EnhancedSpellVisualEffect(
                    this,
                    source,
                    sourceX,
                    sourceY,
                    sourceZ,
                    kind
                )
            );
        }

        public new void Clear()
        {
            GameEffect first = (GameEffect) Items;

            while (first != null)
            {
                LinkedObject n = first.Next;

                first.Destroy();

                first = (GameEffect) n;
            }

            Items = null;
            _lastMeteorSwarmVisual = 0;
            _lastWordOfDeathVisual = 0;
        }
    }
}

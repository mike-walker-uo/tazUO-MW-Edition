#region license
// TazUO addition. Exact-signature presentation for high-impact spells.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI;
using ClassicUO.Resources;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    public enum EnhancedSpellVisualKind : byte
    {
        None,
        EnergyBolt,
        Thunderstorm,
        Wildfire,
        NetherCyclone,
        Bombard,
        DeathRay,
        WordOfDeath,
        Explosion,
        MeteorSwarm,
        NetherBolt,
        EagleStrike,
        HailStorm
    }

    internal static class EnhancedSpellVisualPolicy
    {
        internal static EnhancedSpellVisualKind Classify(
            GraphicEffectType type,
            ushort graphic,
            ushort rawHue,
            ushort particleEffect)
        {
            if (type == GraphicEffectType.Moving)
            {
                if (
                    graphic == 0x36D4
                    && rawHue == 0x49A
                    && particleEffect == 0
                )
                {
                    return EnhancedSpellVisualKind.NetherBolt;
                }

                if (
                    graphic == 0x407A
                    && rawHue == 0
                    && particleEffect == 0
                )
                {
                    return EnhancedSpellVisualKind.EagleStrike;
                }

                if (
                    (graphic == 0x36D4 || graphic == 0xA1ED)
                    && rawHue == 9501
                )
                {
                    return EnhancedSpellVisualKind.MeteorSwarm;
                }

                if (
                    graphic == 0x379F
                    && rawHue == 0
                    && (particleEffect == 3043 || particleEffect == 0)
                )
                {
                    return EnhancedSpellVisualKind.EnergyBolt;
                }

                if (
                    graphic == 0x1363
                    && rawHue == 0
                    && particleEffect == 0
                )
                {
                    return EnhancedSpellVisualKind.Bombard;
                }

                if (
                    graphic == 0x0F5F
                    && rawHue == 0x21
                    && particleEffect == 0x251D
                )
                {
                    return EnhancedSpellVisualKind.WordOfDeath;
                }
            }

            if (
                (type == GraphicEffectType.FixedFrom
                    || type == GraphicEffectType.FixedXYZ)
                && graphic == 0x36BD
                && rawHue == 0
                && particleEffect == 5044
            )
            {
                return EnhancedSpellVisualKind.Explosion;
            }

            if (type != GraphicEffectType.FixedFrom)
            {
                return EnhancedSpellVisualKind.None;
            }

            if (
                graphic == 0x1B6C
                && rawHue == 0x480
                && particleEffect == 0
            )
            {
                return EnhancedSpellVisualKind.Thunderstorm;
            }

            if (
                graphic == 0x374A
                && rawHue == 0x7A2
                && particleEffect == 5054
            )
            {
                return EnhancedSpellVisualKind.DeathRay;
            }

            if (
                graphic == 0x3779
                && rawHue == 0x03
                && particleEffect == 0x26EC
            )
            {
                return EnhancedSpellVisualKind.WordOfDeath;
            }

            return EnhancedSpellVisualKind.None;
        }

        internal static bool CreatesImpact(EnhancedSpellVisualKind kind)
        {
            return kind == EnhancedSpellVisualKind.EnergyBolt
                || kind == EnhancedSpellVisualKind.Bombard
                || kind == EnhancedSpellVisualKind.MeteorSwarm
                || kind == EnhancedSpellVisualKind.NetherBolt
                || kind == EnhancedSpellVisualKind.EagleStrike
                || kind == EnhancedSpellVisualKind.WordOfDeath;
        }

        internal static bool ReplacesMovingProjectile(
            EnhancedSpellVisualKind kind)
        {
            return kind == EnhancedSpellVisualKind.MeteorSwarm
                || kind == EnhancedSpellVisualKind.WordOfDeath;
        }

        internal static EnhancedSpellVisualKind ClassifySound(ushort sound)
        {
            switch (sound)
            {
                case 0x5CF:
                    return EnhancedSpellVisualKind.Wildfire;
                case 0x64F:
                    return EnhancedSpellVisualKind.NetherCyclone;
                default:
                    return EnhancedSpellVisualKind.None;
            }
        }

        internal static bool IsEnhancedLightning(GraphicEffectType type)
        {
            // ServUO uses this packet for Lightning, Chain Lightning and
            // weapon Hit Lightning. One packet creates one Zeus strike;
            // Chain Lightning sends one packet per affected target.
            return type == GraphicEffectType.Lightning;
        }

        internal static bool IsDeathRayCasterMarker(
            GraphicEffectType type,
            ushort graphic,
            ushort particleEffect)
        {
            return type == GraphicEffectType.FixedFrom
                && graphic == 0
                && particleEffect == 2054;
        }

        internal static bool IsNetherCycloneAreaParticle(
            GraphicEffectType type,
            ushort graphic,
            ushort rawHue)
        {
            return type == GraphicEffectType.FixedXYZ
                && graphic == 0x375A
                && rawHue == 0x49A;
        }

        internal static bool IsHailStormAreaParticle(
            GraphicEffectType type,
            ushort graphic,
            ushort rawHue)
        {
            return type == GraphicEffectType.FixedXYZ
                && graphic == 0x3779
                && rawHue == 0x63;
        }

        internal static bool IsWildfireFieldGraphic(ushort graphic)
        {
            return graphic == 0x398C || graphic == 0x3996;
        }
    }

    internal static class EnhancedSpellVisualTrigger
    {
        internal const int METEOR_SWARM_SPELL_ID = 55;
        internal const ushort METEOR_SWARM_CENTER_SOUND = 0x160;
        internal const int THUNDERSTORM_SPELL_ID = 605;
        internal const int WORD_OF_DEATH_SPELL_ID = 614;
        internal const ushort WORD_OF_DEATH_IMPACT_SOUND = 0x211;

        private sealed class RecentFireItem
        {
            internal ushort X;
            internal ushort Y;
            internal uint Seen;
        }

        private static readonly List<RecentFireItem> _recentFireItems =
            new List<RecentFireItem>(16);
        private static uint _lastWildfire;
        private static ushort _lastWildfireX;
        private static ushort _lastWildfireY;
        private static uint _pendingCycloneExpires;
        private static ushort _pendingCycloneX;
        private static ushort _pendingCycloneY;
        private static sbyte _pendingCycloneZ;
        private static uint _pendingHailStormExpires;
        private static ushort _pendingHailStormX;
        private static ushort _pendingHailStormY;
        private static sbyte _pendingHailStormZ;
        private static bool _hooked;
        private static long _pendingMeteorSwarmCastExpires;
        private static long _pendingThunderstormCastExpires;
        private static long _pendingWordOfDeathCastExpires;
        private static uint _lastThunderstormVisual;

        internal static void EnsureHooked()
        {
            if (_hooked)
            {
                return;
            }

            EventSink.SpellCastBegin += OnSpellCastBegin;
            _hooked = true;
        }

        internal static void ResetSession()
        {
            _pendingMeteorSwarmCastExpires = 0;
            _pendingThunderstormCastExpires = 0;
            _pendingWordOfDeathCastExpires = 0;
            _lastThunderstormVisual = 0;
        }

        internal static bool IsMeteorSwarm(int spell)
        {
            return spell == METEOR_SWARM_SPELL_ID;
        }

        internal static bool IsMeteorSwarmCenterSound(ushort sound)
        {
            return sound == METEOR_SWARM_CENTER_SOUND;
        }

        internal static bool IsThunderstorm(int spell)
        {
            return spell == THUNDERSTORM_SPELL_ID;
        }

        internal static bool IsWordOfDeath(int spell)
        {
            return spell == WORD_OF_DEATH_SPELL_ID;
        }

        internal static bool IsWordOfDeathImpactSound(ushort sound)
        {
            return sound == WORD_OF_DEATH_IMPACT_SOUND;
        }

        internal static bool IsWordOfDeathMarker(
            GraphicEffectType type,
            ushort graphic)
        {
            return (
                    type == GraphicEffectType.FixedFrom
                    || type == GraphicEffectType.FixedXYZ
                )
                && graphic == 0x3779;
        }

        internal static void ObserveCastRequest(int spell)
        {
            _pendingMeteorSwarmCastExpires =
                IsMeteorSwarm(spell) ? (long)Time.Ticks + 15000 : 0;
            _pendingThunderstormCastExpires =
                IsThunderstorm(spell) ? (long)Time.Ticks + 60000 : 0;
            _pendingWordOfDeathCastExpires =
                IsWordOfDeath(spell) ? (long)Time.Ticks + 15000 : 0;
        }

        private static void OnSpellCastBegin(object sender, int spell)
        {
            ObserveCastRequest(spell);
        }

        internal static void OnSound(ushort sound, ushort x, ushort y, sbyte z)
        {
            if (
                IsMeteorSwarmCenterSound(sound)
                && _pendingMeteorSwarmCastExpires != 0
                && (long)Time.Ticks <= _pendingMeteorSwarmCastExpires
            )
            {
                // ServUO emits this sound at the selected area before any
                // per-victim projectiles. It therefore supplies the intended
                // barrage center even when no target projectile is sent.
                World.SpawnEnhancedSpellVisual(
                    x,
                    y,
                    z,
                    EnhancedSpellVisualKind.MeteorSwarm
                );
                _pendingMeteorSwarmCastExpires = 0;
            }

            if (
                IsWordOfDeathImpactSound(sound)
                && _pendingWordOfDeathCastExpires != 0
                && (long)Time.Ticks <= _pendingWordOfDeathCastExpires
            )
            {
                // Some shards alter or omit Word of Death's documented
                // particle metadata. Its impact sound is emitted at the
                // victim, so a locally armed cast provides a reliable,
                // correctly positioned fallback without matching unrelated
                // uses of the shared sound.
                World.SpawnEnhancedSpellVisual(
                    x,
                    y,
                    z,
                    EnhancedSpellVisualKind.WordOfDeath
                );
                _pendingWordOfDeathCastExpires = 0;
            }

            if (sound == 0x5CE)
            {
                // Holy Fist shares this sound. A local Thunderstorm cast arms
                // it, allowing the area field to appear even with no victims.
                if (
                    _pendingThunderstormCastExpires != 0
                    && (long)Time.Ticks <= _pendingThunderstormCastExpires
                    && World.Player != null
                )
                {
                    World.SpawnEnhancedSpellVisual(
                        World.Player.X,
                        World.Player.Y,
                        World.Player.Z,
                        EnhancedSpellVisualKind.Thunderstorm
                    );
                    _lastThunderstormVisual = Time.Ticks;
                    _pendingThunderstormCastExpires = 0;
                }
            }

            EnhancedSpellVisualKind kind =
                EnhancedSpellVisualPolicy.ClassifySound(sound);

            if (kind == EnhancedSpellVisualKind.Wildfire)
            {
                if (!HasRecentFireItem(x, y))
                {
                    return;
                }

                // Wildfire repeats its sound on damaged mobiles. Keep one
                // range-wide field centered on the original cast.
                if (
                    Time.Ticks - _lastWildfire < 1400
                    && Math.Abs(x - _lastWildfireX) <= 12
                    && Math.Abs(y - _lastWildfireY) <= 12
                )
                {
                    _lastWildfire = Time.Ticks;
                    return;
                }

                _lastWildfire = Time.Ticks;
                _lastWildfireX = x;
                _lastWildfireY = y;
                World.SpawnEnhancedSpellVisual(
                    x,
                    y,
                    z,
                    EnhancedSpellVisualKind.Wildfire
                );
            }
            else if (kind == EnhancedSpellVisualKind.NetherCyclone)
            {
                // Hail Storm shares this sound. Wait for either spell's
                // distinct area particle before creating a visual.
                _pendingCycloneX = x;
                _pendingCycloneY = y;
                _pendingCycloneZ = z;
                _pendingCycloneExpires = Time.Ticks + 650;
                _pendingHailStormX = x;
                _pendingHailStormY = y;
                _pendingHailStormZ = z;
                _pendingHailStormExpires = Time.Ticks + 850;
            }
        }

        internal static void OnEffectPacket(
            GraphicEffectType type,
            ushort graphic,
            ushort rawHue,
            ushort x,
            ushort y,
            sbyte z)
        {
            if (
                IsWordOfDeathMarker(type, graphic)
                && _pendingWordOfDeathCastExpires != 0
                && (long)Time.Ticks <= _pendingWordOfDeathCastExpires
            )
            {
                // Keep the local cast gate, but tolerate shards that change
                // the marker hue or particle id.
                World.SpawnEnhancedSpellVisual(
                    x,
                    y,
                    z,
                    EnhancedSpellVisualKind.WordOfDeath
                );
                _pendingWordOfDeathCastExpires = 0;
            }

            if (
                EnhancedSpellVisualPolicy.Classify(
                    type,
                    graphic,
                    rawHue,
                    0
                ) == EnhancedSpellVisualKind.Thunderstorm
            )
            {
                uint now = Time.Ticks;

                if (now - _lastThunderstormVisual >= 600)
                {
                    World.SpawnEnhancedSpellVisual(
                        x,
                        y,
                        z,
                        EnhancedSpellVisualKind.Thunderstorm
                    );
                    _lastThunderstormVisual = now;
                }

                return;
            }

            if (
                EnhancedSpellVisualPolicy.IsHailStormAreaParticle(
                    type,
                    graphic,
                    rawHue
                )
                && Time.Ticks <= _pendingHailStormExpires
                && Math.Abs(x - _pendingHailStormX) <= 4
                && Math.Abs(y - _pendingHailStormY) <= 4
            )
            {
                World.SpawnEnhancedSpellVisual(
                    _pendingHailStormX,
                    _pendingHailStormY,
                    _pendingHailStormZ,
                    EnhancedSpellVisualKind.HailStorm
                );
                _pendingHailStormExpires = 0;
                _pendingCycloneExpires = 0;
                return;
            }

            if (
                !EnhancedSpellVisualPolicy.IsNetherCycloneAreaParticle(
                    type,
                    graphic,
                    rawHue
                )
                || Time.Ticks > _pendingCycloneExpires
                || Math.Abs(x - _pendingCycloneX) > 4
                || Math.Abs(y - _pendingCycloneY) > 4
            )
            {
                return;
            }

            World.SpawnEnhancedSpellVisual(
                _pendingCycloneX,
                _pendingCycloneY,
                _pendingCycloneZ,
                EnhancedSpellVisualKind.NetherCyclone
            );
            _pendingCycloneExpires = 0;
            _pendingHailStormExpires = 0;
        }

        internal static void ObserveWorldItem(Item item)
        {
            if (
                item == null
                || item.IsDestroyed
                || !EnhancedSpellVisualPolicy.IsWildfireFieldGraphic(
                    item.OriginalGraphic
                )
            )
            {
                return;
            }

            PruneFireItems();

            if (_recentFireItems.Count >= 48)
            {
                _recentFireItems.RemoveAt(0);
            }

            _recentFireItems.Add(
                new RecentFireItem
                {
                    X = item.X,
                    Y = item.Y,
                    Seen = Time.Ticks
                }
            );
        }

        private static bool HasRecentFireItem(ushort x, ushort y)
        {
            PruneFireItems();

            for (int i = _recentFireItems.Count - 1; i >= 0; i--)
            {
                if (
                    Math.Abs(x - _recentFireItems[i].X) <= 12
                    && Math.Abs(y - _recentFireItems[i].Y) <= 12
                )
                {
                    return true;
                }
            }

            return false;
        }

        internal static int GetWildfireRange(ushort x, ushort y)
        {
            PruneFireItems();
            int range = 5;

            for (int i = _recentFireItems.Count - 1; i >= 0; i--)
            {
                int dx = Math.Abs(x - _recentFireItems[i].X);
                int dy = Math.Abs(y - _recentFireItems[i].Y);

                if (dx <= 12 && dy <= 12)
                {
                    range = Math.Max(range, Math.Max(dx, dy));
                }
            }

            return Math.Min(12, range);
        }

        private static void PruneFireItems()
        {
            for (int i = _recentFireItems.Count - 1; i >= 0; i--)
            {
                if (Time.Ticks - _recentFireItems[i].Seen > 700)
                {
                    _recentFireItems.RemoveAt(i);
                }
            }
        }
    }

    internal static class DeathRayVisualManager
    {
        private sealed class PendingTarget
        {
            internal EffectManager Manager;
            internal uint Serial;
            internal ushort X;
            internal ushort Y;
            internal sbyte Z;
            internal uint Expires;
        }

        private static readonly List<PendingTarget> _pending =
            new List<PendingTarget>(4);

        internal static void ObservePacket(
            EffectManager manager,
            GraphicEffectType type,
            uint source,
            ushort graphic,
            ushort rawHue,
            ushort particleEffect,
            ushort x,
            ushort y,
            sbyte z)
        {
            EnhancedSpellVisualKind kind =
                EnhancedSpellVisualPolicy.Classify(
                    type,
                    graphic,
                    rawHue,
                    particleEffect
                );

            if (kind == EnhancedSpellVisualKind.DeathRay)
            {
                Prune();

                if (_pending.Count >= 8)
                {
                    _pending.RemoveAt(0);
                }

                _pending.Add(
                    new PendingTarget
                    {
                        Manager = manager,
                        Serial = source,
                        X = x,
                        Y = y,
                        Z = z,
                        Expires = Time.Ticks + 350
                    }
                );
                return;
            }

            if (
                !EnhancedSpellVisualPolicy.IsDeathRayCasterMarker(
                    type,
                    graphic,
                    particleEffect
                )
            )
            {
                return;
            }

            Prune();

            if (_pending.Count == 0)
            {
                return;
            }

            PendingTarget target = _pending[_pending.Count - 1];
            _pending.RemoveAt(_pending.Count - 1);
            target.Manager.PushToBack(
                new DeathRayLinkEffect(
                    target.Manager,
                    source,
                    x,
                    y,
                    z,
                    target.Serial,
                    target.X,
                    target.Y,
                    target.Z
                )
            );
        }

        private static void Prune()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (Time.Ticks > _pending[i].Expires)
                {
                    _pending.RemoveAt(i);
                }
            }
        }
    }

    internal sealed class DeathRayLinkEffect : GameEffect
    {
        private const int VisualDuration = 2800;
        private readonly uint _born;
        private readonly uint _casterSerial;
        private readonly uint _targetSerial;
        private readonly ushort _casterX;
        private readonly ushort _casterY;
        private readonly sbyte _casterZ;
        private readonly ushort _targetX;
        private readonly ushort _targetY;
        private readonly sbyte _targetZ;
        private readonly uint _seed;
        private static Texture2D _rayVioletGlow;
        private static Texture2D _rayBlueGlow;

        internal DeathRayLinkEffect(
            EffectManager manager,
            uint casterSerial,
            ushort casterX,
            ushort casterY,
            sbyte casterZ,
            uint targetSerial,
            ushort targetX,
            ushort targetY,
            sbyte targetZ)
            : base(manager, 0, 0, VisualDuration, 0)
        {
            _born = Time.Ticks;
            _casterSerial = casterSerial;
            _targetSerial = targetSerial;
            _casterX = casterX;
            _casterY = casterY;
            _casterZ = casterZ;
            _targetX = targetX;
            _targetY = targetY;
            _targetZ = targetZ;
            _seed = Mix(casterSerial ^ targetSerial ^ _born);

            Entity caster = World.Get(casterSerial);

            if (caster != null && SerialHelper.IsValid(casterSerial))
            {
                SetSource(caster);
            }
            else
            {
                SetSource(casterX, casterY, casterZ);
            }

            AllowedToDraw = true;
            AnimationGraphic = 0;
        }

        public override void Update()
        {
            base.Update();

            if (IsDestroyed)
            {
                return;
            }

            Entity target = World.Get(_targetSerial);

            if (
                SerialHelper.IsValid(_targetSerial)
                && (target == null || target.IsDestroyed)
            )
            {
                Destroy();
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            if (IsDestroyed)
            {
                return false;
            }

            float progress = Math.Min(1f, (Time.Ticks - _born) / (float)VisualDuration);
            float fade =
                progress < 0.08f
                    ? progress / 0.08f
                    : Math.Min(1f, (1f - progress) / 0.18f);
            float phase = Time.Ticks * 0.021f + (_seed & 255u) * 0.01f;
            float pulse =
                0.88f + 0.12f * (float)Math.Sin(Time.Ticks * 0.031f);
            float surge =
                0.74f
                + 0.26f
                    * (float)Math.Pow(
                        Math.Max(0f, Math.Sin(Time.Ticks * 0.009f)),
                        4
                    );
            Vector2 caster = GetBodyPoint(
                _casterSerial,
                _casterX,
                _casterY,
                _casterZ
            );
            Vector2 target = GetBodyPoint(
                _targetSerial,
                _targetX,
                _targetY,
                _targetZ
            );
            Vector2 delta = target - caster;

            if (delta.LengthSquared() < 4f)
            {
                return false;
            }

            Vector2 normal = new Vector2(-delta.Y, delta.X);
            normal.Normalize();
            depth = Source != null ? Source.CalculateDepthZ() + 1.06f : depth + 0.06f;
            Texture2D violetGlow = GetRayGlow(
                ref _rayVioletGlow,
                new Color(92, 35, 218)
            );
            Texture2D blueGlow = GetRayGlow(
                ref _rayBlueGlow,
                new Color(74, 165, 255)
            );

            batcher.SetBlendState(BlendState.AlphaBlend);
            CombatVisualEffect.DrawLine(
                batcher,
                caster,
                target,
                new Color(34, 3, 76),
                fade * 0.42f,
                30f * pulse * surge,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                caster,
                target,
                new Color(42, 16, 132),
                fade * 0.54f,
                20f * pulse,
                depth
            );
            DrawRayGlow(
                batcher,
                violetGlow,
                caster,
                52f * pulse,
                fade * 0.62f,
                depth
            );
            DrawRayGlow(
                batcher,
                violetGlow,
                target,
                76f * pulse * surge,
                fade * 0.78f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);
            CombatVisualEffect.DrawLine(
                batcher,
                caster,
                target,
                new Color(109, 42, 255),
                fade * 0.82f,
                14f * pulse * surge,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                caster,
                target,
                new Color(42, 151, 255),
                fade * 0.9f,
                8f * pulse,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                caster,
                target,
                new Color(120, 225, 255),
                fade,
                4.5f * surge,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                caster,
                target,
                Color.White,
                fade,
                1.8f * pulse,
                depth
            );

            for (int strand = 0; strand < 3; strand++)
            {
                Vector2 previous = caster;

                for (int i = 1; i <= 28; i++)
                {
                    float t = i / 28f;
                    Vector2 center = Vector2.Lerp(caster, target, t);
                    float envelope = (float)Math.Sin(t * MathHelper.Pi);
                    float wave =
                        (float)Math.Sin(
                            phase * (strand % 2 == 0 ? 1f : -1f)
                            + t * MathHelper.TwoPi * (3.2f + strand * 0.34f)
                            + strand * MathHelper.PiOver2
                        );
                    Vector2 current =
                        center
                        + normal
                            * wave
                            * envelope
                            * (6f + strand * 1.4f)
                            * pulse;
                    Color color =
                        strand % 2 == 0
                            ? new Color(57, 188, 255)
                            : new Color(195, 75, 255);

                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        color,
                        fade * (strand < 2 ? 0.76f : 0.52f),
                        strand < 2 ? 2.4f : 1.5f,
                        depth
                    );
                    previous = current;
                }
            }

            float travelPhase = (Time.Ticks * 0.0013f) % 1f;

            for (int i = 0; i < 5; i++)
            {
                float travel = (travelPhase + i * 0.2f) % 1f;
                Vector2 node = Vector2.Lerp(caster, target, travel);
                float nodePulse =
                    0.72f
                    + 0.28f
                        * (float)Math.Sin(
                            travel * MathHelper.Pi
                            + Time.Ticks * 0.024f
                        );
                DrawRayGlow(
                    batcher,
                    i % 2 == 0 ? blueGlow : violetGlow,
                    node,
                    22f + nodePulse * 14f,
                    fade * 0.74f,
                    depth
                );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    node,
                    Color.White,
                    fade,
                    2f + nodePulse,
                    depth
                );
            }

            float ring = 24f + pulse * 18f;
            DrawEndpointRing(batcher, target, ring, fade, phase, depth);
            DrawEndpointRing(
                batcher,
                target,
                ring * 0.64f,
                fade * 0.9f,
                -phase * 1.4f,
                depth
            );

            for (int i = 0; i < 10; i++)
            {
                uint h = Mix(_seed + (uint)(i * 401));
                float angle =
                    ((h & 1023u) / 1023f) * MathHelper.TwoPi + phase;
                float radius = 28f + ((h >> 10) & 23u);
                Vector2 start =
                    target
                    + new Vector2(
                        (float)Math.Cos(angle) * radius * 0.35f,
                        (float)Math.Sin(angle) * radius * 0.2f
                    );
                Vector2 end =
                    target
                    + new Vector2(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius * 0.56f
                    );
                CombatVisualEffect.DrawLine(
                    batcher,
                    start,
                    end,
                    i % 3 == 0
                        ? Color.White
                        : new Color(100, 128, 255),
                    fade * (i % 3 == 0 ? 0.9f : 0.68f),
                    i % 3 == 0 ? 1.4f : 3.4f,
                    depth
                );
            }

            for (int i = 0; i <= 4; i++)
            {
                Vector2 lightPoint = Vector2.Lerp(caster, target, i / 4f);
                CombatVisualEffect.RecordLight(
                    lightPoint,
                    new Color(87, 76, 255),
                    i == 4 ? 220f : 100f,
                    fade * pulse * (i == 4 ? 0.88f : 0.46f)
                );
            }

            batcher.SetBlendState(null);
            return true;
        }

        private static void DrawRayGlow(
            UltimaBatcher2D batcher,
            Texture2D texture,
            Vector2 center,
            float size,
            float alpha,
            float depth)
        {
            batcher.Draw(
                texture,
                center,
                texture.Bounds,
                ShaderHueTranslator.GetHueVector(
                    0,
                    false,
                    Math.Min(1f, alpha)
                ),
                0f,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                new Vector2(size / texture.Width),
                SpriteEffects.None,
                depth
            );
        }

        private static Texture2D GetRayGlow(
            ref Texture2D texture,
            Color color)
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
                    float radius =
                        (float)Math.Sqrt(dx * dx + dy * dy);

                    if (radius >= 1f)
                    {
                        continue;
                    }

                    float coverage = (1f - radius) * (1f - radius);
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
            return texture;
        }

        private static Vector2 GetBodyPoint(
            uint serial,
            ushort x,
            ushort y,
            sbyte z)
        {
            if (World.Get(serial) is Mobile mobile)
            {
                Rectangle bounds = mobile.GetOnScreenRectangle();

                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    return new Vector2(
                        bounds.Center.X,
                        bounds.Y + bounds.Height * 0.48f
                    );
                }
            }

            Point tile = PathPreview.TileToScreen(x, y, z);
            return new Vector2(tile.X + 22f, tile.Y - 28f);
        }

        private static void DrawEndpointRing(
            UltimaBatcher2D batcher,
            Vector2 center,
            float radius,
            float alpha,
            float rotation,
            float depth)
        {
            Vector2 previous = center + new Vector2(
                (float)Math.Cos(rotation) * radius,
                (float)Math.Sin(rotation) * radius * 0.56f
            );

            for (int i = 1; i <= 18; i++)
            {
                float angle = rotation + i * MathHelper.TwoPi / 18f;
                Vector2 next = center + new Vector2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius * 0.56f
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    next,
                    i % 2 == 0
                        ? new Color(83, 188, 255)
                        : new Color(178, 72, 255),
                    alpha * 0.72f,
                    3f,
                    depth
                );
                previous = next;
            }
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

    internal sealed class EnhancedSpellVisualEffect : GameEffect
    {
        private readonly uint _born;
        private readonly int _visualDuration;
        private readonly EnhancedSpellVisualKind _kind;
        private readonly uint _seed;
        private readonly int _wildfireRange;

        private static Texture2D _blueCloud;
        private static Texture2D _violetCloud;
        private static Texture2D _cyanCloud;
        private static Texture2D _orangeCloud;
        private static Texture2D _redCloud;
        private static Texture2D _smokeCloud;
        private static Texture2D _paleCloud;
        private static Texture2D _blackCloud;
        private static Texture2D _energyBlueCloud;
        private static Texture2D _energyVioletCloud;
        private static Texture2D _energyWhiteCloud;
        private static Texture2D _meteorRedCloud;
        private static Texture2D _meteorOrangeCloud;
        private static Texture2D _meteorGoldCloud;
        private static Texture2D _meteorWhiteCloud;
        private static Texture2D _meteorSmokeCloud;
        private static Texture2D _netherDarkCloud;
        private static Texture2D _netherBlueCloud;
        private static Texture2D _netherVioletCloud;
        private static Texture2D _eagleGoldCloud;
        private static Texture2D _hailCloud;
        private static Texture2D _iceCloud;
        private static Texture2D _deathSkull;

        internal EnhancedSpellVisualEffect(
            EffectManager manager,
            uint sourceSerial,
            ushort sourceX,
            ushort sourceY,
            sbyte sourceZ,
            EnhancedSpellVisualKind kind)
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

            _kind = kind;
            _wildfireRange =
                kind == EnhancedSpellVisualKind.Wildfire
                    ? EnhancedSpellVisualTrigger.GetWildfireRange(
                        sourceX,
                        sourceY
                    )
                    : 0;
            _born = Time.Ticks;
            _visualDuration = GetDuration(kind);
            _seed = Mix(
                sourceSerial
                ^ ((uint)sourceX * 0x9E3779B9u)
                ^ ((uint)sourceY * 0x85EBCA6Bu)
                ^ _born
            );
            AllowedToDraw = true;
            AnimationGraphic = 0;
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
            if (IsDestroyed || _kind == EnhancedSpellVisualKind.None)
            {
                return false;
            }

            float progress = Math.Min(1f, (Time.Ticks - _born) / (float)_visualDuration);
            float fade = SmoothFade(progress);
            GetCenters(posX, posY, out Vector2 body, out Vector2 ground);
            depth = Source != null ? Source.CalculateDepthZ() + 1.04f : depth + 0.04f;

            switch (_kind)
            {
                case EnhancedSpellVisualKind.EnergyBolt:
                    DrawEnergyImpact(batcher, body, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.Thunderstorm:
                    DrawThunderstorm(batcher, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.Wildfire:
                    DrawWildfire(batcher, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.NetherCyclone:
                    DrawNetherCyclone(batcher, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.Bombard:
                    DrawBombard(batcher, body, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.WordOfDeath:
                    DrawWordOfDeath(batcher, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.Explosion:
                    DrawExplosion(batcher, body, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.MeteorSwarm:
                    DrawMeteorSwarm(batcher, ground, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.NetherBolt:
                    DrawNetherBoltImpact(batcher, body, progress, fade, depth);
                    break;
                case EnhancedSpellVisualKind.EagleStrike:
                    DrawEagleStrikeImpact(
                        batcher,
                        body,
                        ground,
                        progress,
                        fade,
                        depth
                    );
                    break;
                case EnhancedSpellVisualKind.HailStorm:
                    DrawHailStorm(batcher, ground, progress, fade, depth);
                    break;
            }

            batcher.SetBlendState(null);
            return true;
        }

        internal static void DrawProjectile(
            UltimaBatcher2D batcher,
            EnhancedSpellVisualKind kind,
            Vector2 center,
            Vector2 trailDirection,
            float depth)
        {
            if (
                kind != EnhancedSpellVisualKind.EnergyBolt
                && kind != EnhancedSpellVisualKind.Bombard
                && kind != EnhancedSpellVisualKind.WordOfDeath
                && kind != EnhancedSpellVisualKind.NetherBolt
                && kind != EnhancedSpellVisualKind.EagleStrike
            )
            {
                return;
            }

            batcher.SetBlendState(BlendState.Additive);
            float phase = Time.Ticks * 0.022f;

            if (kind == EnhancedSpellVisualKind.EnergyBolt)
            {
                Vector2 normal = new Vector2(-trailDirection.Y, trailDirection.X);
                float plasmaAngle =
                    (float)Math.Atan2(trailDirection.Y, trailDirection.X);
                Vector2 plasmaCenter = center + trailDirection * 51f;

                DrawCloud(
                    batcher,
                    GetCloud(ref _energyBlueCloud, new Color(28, 76, 238)),
                    plasmaCenter,
                    126f,
                    37f,
                    plasmaAngle,
                    0.48f,
                    depth
                );
                DrawCloud(
                    batcher,
                    GetCloud(ref _energyVioletCloud, new Color(105, 48, 230)),
                    plasmaCenter + normal * 2f,
                    112f,
                    24f,
                    plasmaAngle,
                    0.32f,
                    depth
                );

                DrawEnergyFilament(
                    batcher,
                    center,
                    trailDirection,
                    normal,
                    phase,
                    0f,
                    4.6f,
                    11f,
                    4.2f,
                    new Color(75, 190, 255),
                    1f,
                    depth
                );
                DrawEnergyFilament(
                    batcher,
                    center,
                    trailDirection,
                    normal,
                    phase,
                    MathHelper.TwoPi / 3f,
                    11.5f,
                    6.2f,
                    2.5f,
                    new Color(112, 218, 255),
                    0.86f,
                    depth
                );
                DrawEnergyFilament(
                    batcher,
                    center,
                    trailDirection,
                    normal,
                    phase,
                    MathHelper.TwoPi * 2f / 3f,
                    10f,
                    5.4f,
                    2.2f,
                    new Color(165, 105, 255),
                    0.78f,
                    depth
                );

                for (int branch = 0; branch < 5; branch++)
                {
                    float distance = 22f + branch * 17f;
                    float side = branch % 2 == 0 ? 1f : -1f;
                    float offset =
                        (float)Math.Sin(
                            phase * 0.68f + branch * 2.41f
                        ) * 4f;
                    Vector2 origin =
                        center
                        + trailDirection * distance
                        + normal * offset;
                    Vector2 joint =
                        origin
                        + trailDirection * (6f + branch)
                        + normal * side * (7f + branch * 1.8f);
                    Vector2 end =
                        joint
                        + trailDirection * (7f + branch)
                        + normal * side * (4f + branch);
                    DrawEnergyBranch(batcher, origin, joint, end, depth);
                }

                for (int spark = 0; spark < 14; spark++)
                {
                    float distance = 7f + spark * 7.2f;
                    float side =
                        (float)Math.Sin(phase * 0.43f + spark * 2.17f);
                    Vector2 sparkPosition =
                        center
                        + trailDirection * distance
                        + normal * side * (10f + spark % 4 * 2.5f);
                    CombatVisualEffect.DrawPoint(
                        batcher,
                        sparkPosition,
                        spark % 4 == 0
                            ? new Color(185, 112, 255)
                            : new Color(145, 226, 255),
                        0.45f + (spark % 3) * 0.14f,
                        1.2f + spark % 3,
                        depth
                    );
                }

                float headPulse =
                    11f + (float)Math.Sin(phase * 0.85f) * 2.5f;
                DrawCloud(
                    batcher,
                    GetCloud(ref _energyBlueCloud, new Color(28, 76, 238)),
                    center,
                    headPulse * 2.7f,
                    headPulse * 2.25f,
                    -plasmaAngle,
                    0.62f,
                    depth
                );
                DrawCloud(
                    batcher,
                    GetCloud(
                        ref _energyWhiteCloud,
                        new Color(232, 249, 255)
                    ),
                    center,
                    headPulse * 1.55f,
                    headPulse * 1.35f,
                    plasmaAngle,
                    0.96f,
                    depth
                );
                CombatVisualEffect.RecordLight(
                    center,
                    new Color(68, 131, 255),
                    178f,
                    0.94f
                );
            }
            else if (kind == EnhancedSpellVisualKind.NetherBolt)
            {
                DrawNetherBoltProjectile(
                    batcher,
                    center,
                    trailDirection,
                    phase,
                    depth
                );
            }
            else if (kind == EnhancedSpellVisualKind.EagleStrike)
            {
                DrawEagleStrikeProjectile(
                    batcher,
                    center,
                    trailDirection,
                    phase,
                    depth
                );
            }
            else if (kind == EnhancedSpellVisualKind.Bombard)
            {
                for (int i = 1; i <= 8; i++)
                {
                    Vector2 point =
                        center
                        + trailDirection * (i * 6.5f)
                        + new Vector2(
                            (float)Math.Sin(phase + i * 1.7f) * 4f,
                            i * 1.5f
                        );
                    CombatVisualEffect.DrawPoint(
                        batcher,
                        point,
                        i < 3 ? new Color(255, 184, 72) : new Color(92, 73, 66),
                        (9f - i) / 12f,
                        8f - i * 0.42f,
                        depth
                    );
                }
            }
            else
            {
                Vector2 normal = new Vector2(-trailDirection.Y, trailDirection.X);

                for (int i = 1; i <= 10; i++)
                {
                    float side = (float)Math.Sin(phase - i * 0.75f) * 7f;
                    Vector2 point = center + trailDirection * (i * 5.5f) + normal * side;
                    CombatVisualEffect.DrawPoint(
                        batcher,
                        point,
                        i % 2 == 0 ? new Color(94, 68, 196) : new Color(35, 164, 255),
                        (11f - i) / 15f,
                        7f - i * 0.25f,
                        depth
                    );
                }
            }

            batcher.SetBlendState(null);
        }

        private static void DrawMeteorStreak(
            UltimaBatcher2D batcher,
            Vector2 head,
            Vector2 trailDirection,
            Vector2 normal,
            float length,
            float scale,
            int variant,
            float depth)
        {
            float bend =
                (variant % 2 == 0 ? 1f : -1f)
                * (2.4f + variant % 3);
            Vector2 joint =
                head
                + trailDirection * (length * 0.43f)
                + normal * bend;
            Vector2 tail =
                head
                + trailDirection * length
                - normal * bend * 0.45f;

            batcher.SetBlendState(BlendState.Additive);
            DrawMeteorSegment(
                batcher,
                head,
                joint,
                scale,
                depth
            );
            DrawMeteorSegment(
                batcher,
                joint,
                tail,
                scale * 0.68f,
                depth
            );

            float rotation =
                (float)Math.Atan2(trailDirection.Y, trailDirection.X);
            DrawCloud(
                batcher,
                GetCloud(ref _meteorRedCloud, new Color(210, 28, 3)),
                head,
                38f * scale,
                31f * scale,
                rotation,
                0.72f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(
                    ref _meteorOrangeCloud,
                    new Color(255, 91, 4)
                ),
                head,
                29f * scale,
                24f * scale,
                -rotation,
                0.88f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(ref _meteorGoldCloud, new Color(255, 190, 42)),
                head,
                20f * scale,
                17f * scale,
                rotation,
                0.96f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(
                    ref _meteorWhiteCloud,
                    new Color(255, 247, 211)
                ),
                head,
                10f * scale,
                9f * scale,
                -rotation,
                1f,
                depth
            );

            for (int ember = 1; ember <= 4; ember++)
            {
                float distance = length * ember / 5f;
                Vector2 point =
                    head
                    + trailDirection * distance
                    + normal
                        * (float)Math.Sin(
                            variant * 2.3f + ember * 1.7f
                        )
                        * (3f + ember);
                CombatVisualEffect.DrawPoint(
                    batcher,
                    point,
                    ember < 3
                        ? new Color(255, 169, 35)
                        : new Color(207, 52, 7),
                    (5f - ember) * 0.14f,
                    Math.Max(1.2f, (4.6f - ember * 0.7f) * scale),
                    depth
                );
            }
        }

        private static void DrawMeteorSegment(
            UltimaBatcher2D batcher,
            Vector2 start,
            Vector2 end,
            float scale,
            float depth)
        {
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                end,
                new Color(174, 22, 2),
                0.36f,
                16f * scale,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                end,
                new Color(255, 72, 3),
                0.64f,
                9f * scale,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                end,
                new Color(255, 190, 42),
                0.9f,
                4.4f * scale,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                end,
                new Color(255, 249, 221),
                1f,
                Math.Max(1f, 1.45f * scale),
                depth
            );
        }

        private static void DrawNetherBoltProjectile(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2 trailDirection,
            float phase,
            float depth)
        {
            Vector2 normal = new Vector2(
                -trailDirection.Y,
                trailDirection.X
            );
            float rotation =
                (float)Math.Atan2(trailDirection.Y, trailDirection.X);
            float pulse = 1f + (float)Math.Sin(phase * 0.83f) * 0.13f;

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _netherDarkCloud, new Color(8, 3, 23)),
                center,
                48f * pulse,
                42f * pulse,
                rotation,
                0.9f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            for (int strand = 0; strand < 3; strand++)
            {
                Vector2 previous = center;

                for (int segment = 1; segment <= 12; segment++)
                {
                    float t = segment / 12f;
                    float envelope = (float)Math.Sin(t * MathHelper.Pi);
                    float wave =
                        (float)Math.Sin(
                            phase * (strand % 2 == 0 ? 0.72f : -0.78f)
                            + strand * MathHelper.TwoPi / 3f
                            + t * MathHelper.TwoPi * 1.8f
                        )
                        * envelope
                        * (9f + strand * 2f);
                    Vector2 current =
                        center
                        + trailDirection * (t * 94f)
                        + normal * wave;
                    Color color =
                        strand == 0
                            ? new Color(42, 112, 255)
                            : strand == 1
                                ? new Color(128, 44, 238)
                                : new Color(42, 220, 255);
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        color,
                        0.72f - t * 0.34f,
                        7f - t * 3.5f,
                        depth
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        new Color(203, 226, 255),
                        0.76f - t * 0.48f,
                        1.15f,
                        depth
                    );
                    previous = current;
                }
            }

            DrawCloud(
                batcher,
                GetCloud(ref _netherVioletCloud, new Color(111, 35, 235)),
                center,
                42f * pulse,
                36f * pulse,
                -rotation,
                0.84f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(ref _netherBlueCloud, new Color(34, 112, 255)),
                center,
                29f * pulse,
                25f * pulse,
                rotation,
                0.95f,
                depth
            );
            CombatVisualEffect.DrawPoint(
                batcher,
                center,
                new Color(222, 243, 255),
                1f,
                7f * pulse,
                depth
            );

            for (int mote = 0; mote < 9; mote++)
            {
                float t = (mote + 1f) / 10f;
                float side =
                    (float)Math.Sin(phase * 0.51f + mote * 2.21f);
                Vector2 point =
                    center
                    + trailDirection * (t * 83f)
                    + normal * side * (10f + mote % 3 * 3f);
                CombatVisualEffect.DrawPoint(
                    batcher,
                    point,
                    mote % 3 == 0
                        ? new Color(69, 212, 255)
                        : new Color(143, 65, 255),
                    0.68f - t * 0.32f,
                    2f + mote % 3,
                    depth
                );
            }

            CombatVisualEffect.RecordLight(
                center,
                new Color(57, 80, 230),
                172f,
                0.86f
            );
        }

        private static void DrawEagleStrikeProjectile(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2 trailDirection,
            float phase,
            float depth)
        {
            Vector2 normal = new Vector2(
                -trailDirection.Y,
                trailDirection.X
            );
            batcher.SetBlendState(BlendState.Additive);

            for (int wing = -1; wing <= 1; wing += 2)
            {
                Vector2 previous = center;

                for (int segment = 1; segment <= 9; segment++)
                {
                    float t = segment / 9f;
                    float spread =
                        (float)Math.Sin(t * MathHelper.Pi)
                        * (31f + (float)Math.Sin(phase * 0.38f) * 4f);
                    Vector2 current =
                        center
                        + trailDirection * (t * 104f)
                        + normal * wing * spread;
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        new Color(82, 184, 218),
                        0.5f - t * 0.22f,
                        8f - t * 4f,
                        depth
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        wing < 0
                            ? new Color(255, 215, 115)
                            : new Color(213, 244, 255),
                        0.86f - t * 0.46f,
                        1.6f,
                        depth
                    );
                    previous = current;
                }
            }

            for (int feather = 0; feather < 12; feather++)
            {
                float distance = 20f + feather * 6.5f;
                float side = feather % 2 == 0 ? 1f : -1f;
                Vector2 root =
                    center
                    + trailDirection * distance
                    + normal
                        * side
                        * (8f + feather % 4 * 5f);
                Vector2 tip =
                    root
                    + trailDirection * (8f + feather % 3 * 3f)
                    + normal * side * 4f;
                CombatVisualEffect.DrawLine(
                    batcher,
                    root,
                    tip,
                    feather % 3 == 0
                        ? new Color(255, 224, 138)
                        : new Color(189, 235, 255),
                    0.68f - feather * 0.026f,
                    1.4f,
                    depth
                );
            }

            DrawCloud(
                batcher,
                GetCloud(ref _eagleGoldCloud, new Color(255, 183, 55)),
                center,
                37f,
                25f,
                phase * 0.08f,
                0.58f,
                depth
            );
            CombatVisualEffect.RecordLight(
                center,
                new Color(255, 203, 103),
                136f,
                0.62f
            );
        }

        private static void DrawEnergyFilament(
            UltimaBatcher2D batcher,
            Vector2 center,
            Vector2 direction,
            Vector2 normal,
            float phase,
            float phaseOffset,
            float amplitude,
            float coronaWidth,
            float bodyWidth,
            Color bodyColor,
            float alpha,
            float depth)
        {
            const int segments = 18;
            const float length = 104f;
            Vector2 previous = center;

            for (int segment = 1; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float wave =
                    (float)Math.Sin(
                        phase * 0.74f
                        + phaseOffset
                        + t * MathHelper.TwoPi * 2.35f
                    )
                    * amplitude
                    * envelope;
                wave +=
                    (float)Math.Sin(
                        phase * 1.17f
                        - phaseOffset
                        + t * MathHelper.TwoPi * 4.8f
                    )
                    * amplitude
                    * 0.24f
                    * envelope;
                float taper = 1f - t * 0.42f;
                Vector2 current =
                    center + direction * (length * t) + normal * wave;

                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    new Color(45, 50, 235),
                    alpha * 0.38f,
                    coronaWidth * taper,
                    depth
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    bodyColor,
                    alpha * 0.8f,
                    bodyWidth * taper,
                    depth
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    previous,
                    current,
                    new Color(232, 249, 255),
                    alpha * 0.96f,
                    Math.Max(0.72f, bodyWidth * 0.27f * taper),
                    depth
                );
                previous = current;
            }
        }

        private static void DrawEnergyBranch(
            UltimaBatcher2D batcher,
            Vector2 start,
            Vector2 joint,
            Vector2 end,
            float depth)
        {
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                joint,
                new Color(77, 123, 255),
                0.5f,
                3.2f,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                joint,
                end,
                new Color(77, 123, 255),
                0.34f,
                2.4f,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                start,
                joint,
                new Color(220, 246, 255),
                0.82f,
                0.82f,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                joint,
                end,
                new Color(220, 246, 255),
                0.68f,
                0.72f,
                depth
            );
        }

        private void DrawEnergyImpact(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float depth)
        {
            float radius = 12f + EaseOut(progress) * 78f;
            DrawBrokenRing(
                batcher,
                center,
                radius,
                radius * 0.72f,
                progress * 2.4f,
                new Color(42, 109, 255),
                fade * 0.75f,
                7f,
                depth
            );
            DrawBrokenRing(
                batcher,
                center,
                radius * 0.72f,
                radius * 0.5f,
                -progress * 3.2f,
                new Color(180, 236, 255),
                fade,
                2.3f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            for (int i = 0; i < 13; i++)
            {
                uint h = Mix(_seed + (uint)(i * 193));
                float angle = ((h & 1023u) / 1023f) * MathHelper.TwoPi;
                Vector2 end = Ellipse(center, radius * (0.75f + ((h >> 10) & 15u) / 40f), radius * 0.58f, angle);
                CombatVisualEffect.DrawLine(
                    batcher,
                    center,
                    end,
                    i % 3 == 0 ? Color.White : new Color(77, 139, 255),
                    fade * (i % 3 == 0 ? 0.9f : 0.56f),
                    i % 3 == 0 ? 1.2f : 3.4f,
                    depth
                );
            }

            DrawCloud(
                batcher,
                GetCloud(ref _blueCloud, new Color(38, 86, 245)),
                center,
                58f + radius * 0.45f,
                48f + radius * 0.34f,
                progress * 1.6f,
                fade * 0.62f,
                depth
            );
            CombatVisualEffect.RecordLight(center, new Color(75, 143, 255), 220f, fade);
        }

        private void DrawMeteorSwarm(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            Vector2 fallDirection = new Vector2(0.3f, 0.954f);
            fallDirection.Normalize();
            Vector2 trailDirection = -fallDirection;
            Vector2 normal = new Vector2(
                -trailDirection.Y,
                trailDirection.X
            );
            float strongestImpact = 0f;

            for (int meteor = 0; meteor < 18; meteor++)
            {
                uint h = Mix(_seed + (uint)(meteor * 1049));
                float delay =
                    (meteor % 6) * 0.035f
                    + (meteor / 6) * 0.061f;
                float local = progress - delay;

                if (local < 0f)
                {
                    continue;
                }

                Vector2 impact =
                    ground
                    + new Vector2(
                        ((h & 255u) / 255f - 0.5f) * 190f,
                        (((h >> 8) & 255u) / 255f - 0.5f) * 112f
                    );
                float scale =
                    0.68f + ((h >> 16) & 255u) / 255f * 0.58f;

                if (local < 0.31f)
                {
                    float descent = EaseOut(local / 0.31f);
                    Vector2 sky =
                        impact
                        - fallDirection * (360f + scale * 88f)
                        + normal
                            * ((((h >> 24) & 255u) / 255f - 0.5f) * 58f);
                    Vector2 head = Vector2.Lerp(sky, impact, descent);
                    CombatVisualEffect.DrawFireballProjectile(
                        batcher,
                        head,
                        trailDirection,
                        depth
                    );
                }

                float impactAge = (local - 0.27f) / 0.38f;

                if (impactAge >= 0f && impactAge < 1f)
                {
                    DrawMeteorImpact(
                        batcher,
                        impact,
                        impactAge,
                        scale,
                        meteor,
                        fade,
                        depth
                    );
                    strongestImpact = Math.Max(
                        strongestImpact,
                        (1f - impactAge) * scale
                    );
                }
            }

            if (strongestImpact > 0f)
            {
                CombatVisualEffect.RecordLight(
                    ground,
                    new Color(255, 82, 12),
                    300f + strongestImpact * 90f,
                    Math.Min(1f, strongestImpact)
                );
            }
        }

        private static void DrawMeteorImpact(
            UltimaBatcher2D batcher,
            Vector2 center,
            float age,
            float scale,
            int variant,
            float overallFade,
            float depth)
        {
            float inverse = 1f - age;
            float flash =
                Math.Min(1f, age / 0.1f)
                * Math.Min(1f, inverse / 0.32f)
                * overallFade;
            float radius = (12f + EaseOut(age) * 48f) * scale;

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(
                    ref _meteorSmokeCloud,
                    new Color(47, 35, 32)
                ),
                center + new Vector2(0f, -age * 26f),
                (34f + age * 54f) * scale,
                (24f + age * 42f) * scale,
                age * (variant % 2 == 0 ? 1.4f : -1.3f),
                inverse * 0.58f * overallFade,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawCloud(
                batcher,
                GetCloud(ref _meteorRedCloud, new Color(210, 28, 3)),
                center,
                (42f + radius) * scale,
                (28f + radius * 0.55f) * scale,
                age * 2.1f + variant,
                flash * 0.72f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(
                    ref _meteorOrangeCloud,
                    new Color(255, 91, 4)
                ),
                center,
                (29f + radius * 0.56f) * scale,
                (22f + radius * 0.4f) * scale,
                -age * 2.7f,
                flash * 0.88f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(
                    ref _meteorWhiteCloud,
                    new Color(255, 247, 211)
                ),
                center,
                Math.Max(5f, (28f - age * 22f) * scale),
                Math.Max(4f, (22f - age * 17f) * scale),
                age,
                flash,
                depth
            );
            DrawBrokenRing(
                batcher,
                center,
                radius,
                radius * 0.46f,
                variant * 0.37f,
                new Color(255, 132, 18),
                flash * 0.7f,
                Math.Max(1.5f, 5f * scale * inverse),
                depth
            );

            for (int spark = 0; spark < 7; spark++)
            {
                float angle =
                    variant * 1.73f
                    + spark * MathHelper.TwoPi / 7f;
                float distance =
                    radius * (0.62f + (spark % 3) * 0.16f);
                Vector2 end = Ellipse(
                    center,
                    distance,
                    distance * 0.48f,
                    angle
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    center,
                    end,
                    spark % 3 == 0
                        ? new Color(255, 240, 188)
                        : new Color(255, 102, 7),
                    flash * (spark % 3 == 0 ? 0.9f : 0.58f),
                    spark % 3 == 0 ? 1.1f : 2.5f,
                    depth
                );
            }
        }

        private void DrawNetherBoltImpact(
            UltimaBatcher2D batcher,
            Vector2 center,
            float progress,
            float fade,
            float depth)
        {
            float collapse =
                Math.Max(0f, 1f - progress / 0.68f);
            float burst =
                progress < 0.42f
                    ? 0f
                    : Math.Min(1f, (progress - 0.42f) / 0.16f)
                        * Math.Min(1f, (1f - progress) / 0.3f);
            float phase = progress * 9f;
            float radius = 18f + collapse * 62f;

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _netherDarkCloud, new Color(8, 3, 23)),
                center,
                radius * 1.34f,
                radius * 1.12f,
                phase,
                fade * 0.94f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);
            DrawCloud(
                batcher,
                GetCloud(ref _netherVioletCloud, new Color(111, 35, 235)),
                center,
                radius,
                radius * 0.84f,
                -phase * 1.3f,
                fade * (0.48f + burst * 0.42f),
                depth
            );
            DrawBrokenRing(
                batcher,
                center,
                radius * 1.08f + burst * 42f,
                radius * 0.62f + burst * 20f,
                phase,
                new Color(61, 147, 255),
                fade * (0.78f + burst * 0.2f),
                5.5f,
                depth
            );
            DrawBrokenRing(
                batcher,
                center,
                radius * 0.74f + burst * 64f,
                radius * 0.42f + burst * 29f,
                -phase * 1.5f,
                new Color(177, 70, 255),
                fade * 0.72f,
                2.3f,
                depth
            );

            for (int ray = 0; ray < 12; ray++)
            {
                float angle =
                    phase * (ray % 2 == 0 ? 1f : -1f)
                    + ray * MathHelper.TwoPi / 12f;
                Vector2 outer = Ellipse(
                    center,
                    radius * (1.1f + ray % 3 * 0.12f),
                    radius * 0.68f,
                    angle
                );
                Vector2 inner = Vector2.Lerp(
                    outer,
                    center,
                    0.72f + progress * 0.23f
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    outer,
                    inner,
                    ray % 3 == 0
                        ? new Color(132, 226, 255)
                        : new Color(115, 49, 244),
                    fade * (0.54f + burst * 0.34f),
                    ray % 3 == 0 ? 1.3f : 3.1f,
                    depth
                );
            }

            CombatVisualEffect.DrawPoint(
                batcher,
                center,
                new Color(226, 247, 255),
                fade * (0.72f + burst * 0.28f),
                5f + burst * 14f,
                depth
            );
            CombatVisualEffect.RecordLight(
                center,
                new Color(69, 62, 228),
                178f + burst * 92f,
                fade * (0.72f + burst * 0.28f)
            );
        }

        private void DrawEagleStrikeImpact(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float strikeProgress = Math.Min(1f, progress / 0.58f);
            float strike =
                (float)Math.Sin(strikeProgress * MathHelper.Pi);
            Vector2 descending =
                Vector2.Lerp(
                    body + new Vector2(-58f, -158f),
                    body,
                    EaseOut(strikeProgress)
                );

            batcher.SetBlendState(BlendState.Additive);
            CombatVisualEffect.DrawLine(
                batcher,
                descending,
                body,
                new Color(76, 181, 223),
                fade * strike * 0.58f,
                16f,
                depth
            );
            CombatVisualEffect.DrawLine(
                batcher,
                descending,
                body,
                new Color(255, 232, 162),
                fade * strike,
                3f,
                depth
            );

            for (int wing = -1; wing <= 1; wing += 2)
            {
                Vector2 previous = body;

                for (int segment = 1; segment <= 8; segment++)
                {
                    float t = segment / 8f;
                    Vector2 current =
                        body
                        + new Vector2(
                            wing * (t * 91f),
                            -((float)Math.Sin(t * MathHelper.Pi) * 39f)
                                + t * 12f
                        )
                            * (0.72f + strike * 0.28f);
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        wing < 0
                            ? new Color(255, 202, 78)
                            : new Color(181, 235, 255),
                        fade * (0.82f - t * 0.34f),
                        5.4f - t * 2.8f,
                        depth
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        new Color(255, 247, 215),
                        fade * (0.88f - t * 0.48f),
                        1.15f,
                        depth
                    );
                    previous = current;
                }
            }

            float ringRadius = 15f + EaseOut(progress) * 94f;
            DrawBrokenRing(
                batcher,
                ground,
                ringRadius,
                ringRadius * 0.43f,
                progress * 2f,
                new Color(104, 202, 232),
                fade * 0.66f,
                4.5f,
                depth
            );

            for (int feather = 0; feather < 15; feather++)
            {
                uint h = Mix(_seed + (uint)(feather * 337));
                float angle =
                    ((h & 1023u) / 1023f) * MathHelper.TwoPi;
                float distance =
                    EaseOut(progress)
                    * (34f + ((h >> 10) & 63u));
                Vector2 root = Ellipse(
                    body,
                    distance,
                    distance * 0.58f,
                    angle
                );
                Vector2 tip =
                    root
                    + new Vector2(
                        (float)Math.Cos(angle) * 8f,
                        8f + progress * 22f
                    );
                CombatVisualEffect.DrawLine(
                    batcher,
                    root,
                    tip,
                    feather % 3 == 0
                        ? new Color(255, 216, 112)
                        : new Color(203, 241, 255),
                    fade * 0.74f,
                    1.5f,
                    depth
                );
            }

            DrawCloud(
                batcher,
                GetCloud(ref _eagleGoldCloud, new Color(255, 183, 55)),
                body,
                54f + strike * 42f,
                39f + strike * 26f,
                progress * 2.4f,
                fade * 0.62f,
                depth
            );
            CombatVisualEffect.RecordLight(
                body,
                new Color(255, 210, 118),
                205f,
                fade * (0.62f + strike * 0.28f)
            );
        }

        private void DrawHailStorm(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                GetCloud(ref _hailCloud, new Color(42, 58, 84)),
                ground + new Vector2(-31f, -174f),
                330f,
                92f,
                progress * 0.28f,
                fade * 0.58f,
                depth
            );
            DrawCloud(
                batcher,
                GetCloud(ref _iceCloud, new Color(182, 226, 245)),
                ground + new Vector2(0f, -6f),
                286f,
                82f,
                -progress * 0.17f,
                fade * 0.2f,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);

            for (int hail = 0; hail < 32; hail++)
            {
                uint h = Mix(_seed + (uint)(hail * 719));
                float offset = (h & 1023u) / 1023f;
                float cycle = (progress * 3.15f + offset) % 1f;
                Vector2 impact =
                    ground
                    + new Vector2(
                        (((h >> 10) & 255u) / 255f - 0.5f) * 286f,
                        (((h >> 18) & 255u) / 255f - 0.5f) * 142f
                    );
                Vector2 head =
                    impact
                    + new Vector2(-38f, -198f) * (1f - cycle);
                float hailScale =
                    0.62f + ((h >> 26) & 31u) / 31f * 0.62f;
                Vector2 tail = head + new Vector2(-8f, -31f * hailScale);

                CombatVisualEffect.DrawLine(
                    batcher,
                    head,
                    tail,
                    new Color(67, 157, 225),
                    fade * 0.58f,
                    5.2f * hailScale,
                    depth
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    head,
                    tail,
                    new Color(225, 248, 255),
                    fade * 0.94f,
                    1.2f * hailScale,
                    depth
                );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    head,
                    Color.White,
                    fade,
                    2.2f * hailScale,
                    depth
                );

                if (cycle > 0.84f)
                {
                    float splash = (cycle - 0.84f) / 0.16f;
                    float splashFade = (1f - splash) * fade;

                    if (hail % 3 == 0)
                    {
                        DrawBrokenRing(
                            batcher,
                            impact,
                            4f + splash * 15f,
                            2f + splash * 6f,
                            hail,
                            new Color(180, 231, 255),
                            splashFade * 0.68f,
                            1.5f,
                            depth
                        );
                    }

                    for (int shard = -1; shard <= 1; shard++)
                    {
                        Vector2 shardEnd =
                            impact
                            + new Vector2(
                                shard * (5f + splash * 10f),
                                -3f - splash * (8f + Math.Abs(shard) * 4f)
                            );
                        CombatVisualEffect.DrawLine(
                            batcher,
                            impact,
                            shardEnd,
                            new Color(211, 244, 255),
                            splashFade * 0.78f,
                            1.1f,
                            depth
                        );
                    }
                }
            }

            for (int patch = 0; patch < 8; patch++)
            {
                uint h = Mix(_seed + (uint)(patch * 1237 + 91));
                Vector2 point =
                    ground
                    + new Vector2(
                        ((h & 255u) / 255f - 0.5f) * 248f,
                        (((h >> 8) & 255u) / 255f - 0.5f) * 116f
                    );
                DrawCloud(
                    batcher,
                    GetCloud(ref _iceCloud, new Color(182, 226, 245)),
                    point,
                    25f + patch % 3 * 8f,
                    10f + patch % 2 * 5f,
                    patch,
                    fade * Math.Min(0.34f, progress * 0.62f),
                    depth
                );
            }

            CombatVisualEffect.RecordLight(
                ground,
                new Color(105, 181, 232),
                285f,
                fade * 0.46f
            );
        }

        private void DrawThunderstorm(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float phase = Time.Ticks * 0.008f;
            Texture2D smoke = GetCloud(ref _smokeCloud, new Color(32, 42, 70));
            Texture2D violet = GetCloud(ref _violetCloud, new Color(94, 54, 218));
            Texture2D blue = GetCloud(ref _blueCloud, new Color(39, 81, 192));

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int i = 0; i < 14; i++)
            {
                uint h = Mix(_seed + (uint)(i * 313));
                float x = ((h & 1023u) / 1023f - 0.5f) * 530f;
                float y = (((h >> 10) & 1023u) / 1023f - 0.5f) * 260f;
                Vector2 cloud =
                    ground + new Vector2(x, y - 126f);
                DrawCloud(
                    batcher,
                    smoke,
                    cloud,
                    126f + (h & 31u),
                    54f + ((h >> 15) & 15u),
                    phase * 0.018f + i,
                    fade * 0.42f,
                    depth
                );
                DrawCloud(
                    batcher,
                    i % 2 == 0 ? violet : blue,
                    cloud,
                    84f + ((h >> 20) & 15u),
                    38f,
                    -phase * 0.024f - i,
                    fade * 0.22f,
                    depth
                );
            }

            batcher.SetBlendState(BlendState.Additive);

            for (int bolt = 0; bolt < 42; bolt++)
            {
                uint h = Mix(_seed ^ (uint)(bolt * 977));
                float angle =
                    ((h & 2047u) / 2047f) * MathHelper.TwoPi;
                float radial =
                    (float)Math.Sqrt(((h >> 11) & 1023u) / 1023f);
                Vector2 impact =
                    ground
                    + new Vector2(
                        (float)Math.Cos(angle) * 264f * radial,
                        (float)Math.Sin(angle) * 132f * radial
                    );
                float strikeTime =
                    progress * (5.4f + (bolt % 4) * 0.23f)
                    + ((h >> 21) & 255u) / 255f;
                float local = strikeTime - (float)Math.Floor(strikeTime);

                if (local > 0.36f)
                {
                    continue;
                }

                float strikeEnvelope =
                    (float)Math.Sin(
                        local / 0.36f * MathHelper.Pi
                    );
                float flash = strikeEnvelope * fade;
                float snap =
                    (
                        local < 0.11f
                            ? 1f
                            : Math.Max(0.42f, strikeEnvelope)
                    )
                    * fade;
                float height = 52f + ((h >> 16) & 47u);
                Vector2 previous = impact - new Vector2(0f, height);
                Vector2 forkOrigin = previous;

                for (int segment = 1; segment <= 6; segment++)
                {
                    float t = segment / 6f;
                    Vector2 current =
                        Vector2.Lerp(
                            impact - new Vector2(0f, height),
                            impact,
                            t
                        );

                    if (segment < 6)
                    {
                        current.X +=
                            (float)Math.Sin(
                                bolt * 2.31f
                                + segment * 4.17f
                                + phase * 0.7f
                            )
                            * (7f + ((h >> segment) & 7u));
                    }

                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        new Color(103, 55, 235),
                        flash * 0.62f,
                        8f,
                        depth
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        new Color(122, 224, 255),
                        flash * 0.9f,
                        3.2f,
                        depth
                    );
                    CombatVisualEffect.DrawLine(
                        batcher,
                        previous,
                        current,
                        Color.White,
                        snap,
                        1.25f,
                        depth
                    );

                    if (segment == 3)
                    {
                        forkOrigin = current;
                    }

                    previous = current;
                }

                float forkSide = (h & 1u) == 0 ? -1f : 1f;
                Vector2 forkJoint =
                    forkOrigin
                    + new Vector2(
                        forkSide * (11f + ((h >> 9) & 15u)),
                        12f + ((h >> 13) & 7u)
                    );
                Vector2 forkEnd =
                    forkJoint
                    + new Vector2(
                        forkSide * (8f + ((h >> 6) & 11u)),
                        11f + ((h >> 18) & 7u)
                    );
                CombatVisualEffect.DrawLine(
                    batcher,
                    forkOrigin,
                    forkJoint,
                    new Color(116, 185, 255),
                    flash * 0.65f,
                    1.7f,
                    depth
                );
                CombatVisualEffect.DrawLine(
                    batcher,
                    forkJoint,
                    forkEnd,
                    Color.White,
                    flash * 0.72f,
                    0.8f,
                    depth
                );

                CombatVisualEffect.DrawLine(
                    batcher,
                    impact - new Vector2(12f, 0f),
                    impact + new Vector2(12f, 0f),
                    new Color(151, 222, 255),
                    flash * 0.82f,
                    2.4f,
                    depth
                );
                DrawBrokenRing(
                    batcher,
                    impact,
                    5f + local * 54f,
                    2f + local * 20f,
                    angle,
                    new Color(102, 151, 255),
                    flash * 0.58f,
                    1.8f,
                    depth
                );
                CombatVisualEffect.RecordLight(
                    impact,
                    new Color(92, 126, 255),
                    132f,
                    flash * 0.82f
                );
            }

            for (int spark = 0; spark < 48; spark++)
            {
                uint h = Mix(_seed + (uint)(spark * 1597 + 71));
                float angle =
                    ((h & 2047u) / 2047f) * MathHelper.TwoPi;
                float radial =
                    (float)Math.Sqrt(
                        ((h >> 11) & 1023u) / 1023f
                    );
                float flicker =
                    0.35f
                    + 0.65f
                        * Math.Abs(
                            (float)Math.Sin(
                                phase * (0.7f + spark % 5 * 0.08f)
                                + spark
                            )
                        );
                Vector2 sparkPosition =
                    ground
                    + new Vector2(
                        (float)Math.Cos(angle) * 258f * radial,
                        (float)Math.Sin(angle) * 128f * radial
                    );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    sparkPosition,
                    spark % 5 == 0
                        ? Color.White
                        : new Color(100, 165, 255),
                    fade * flicker * 0.68f,
                    1.4f + spark % 3,
                    depth
                );
            }

            DrawBrokenRing(
                batcher,
                ground,
                264f,
                132f,
                phase * 0.02f,
                new Color(82, 132, 255),
                fade * 0.24f,
                2f,
                depth
            );
            CombatVisualEffect.RecordLight(
                ground,
                new Color(61, 84, 185),
                335f,
                fade * 0.22f
            );
        }

        private void DrawWildfire(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float grow = Math.Min(1f, progress * 3.8f);
            float spread = 0.18f + grow * 0.82f;
            int range = Math.Max(5, _wildfireRange);
            int wallCount = 28 + range * 2;
            int flameCount = 44 + range * 4;
            Texture2D red = GetCloud(ref _redCloud, new Color(205, 28, 8));
            Texture2D orange = GetCloud(ref _orangeCloud, new Color(255, 112, 12));
            Texture2D smoke = GetCloud(ref _smokeCloud, new Color(42, 37, 43));

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int i = 0; i < wallCount; i++)
            {
                uint h = Mix(_seed + (uint)(i * 0x51ED + 0x398C));
                float tileX =
                    ((h & 2047u) / 2047f * 2f - 1f)
                    * range
                    * spread;
                float tileY =
                    (((h >> 11) & 2047u) / 2047f * 2f - 1f)
                    * range
                    * spread;
                Vector2 basePoint =
                    ground
                    + new Vector2(
                        (tileX - tileY) * 22f,
                        (tileX + tileY) * 22f
                    );
                float scale =
                    0.72f + ((h >> 22) & 255u) / 255f * 0.34f;

                DrawWildfireWall(
                    batcher,
                    basePoint,
                    i,
                    scale,
                    fade * 0.86f,
                    depth
                );
            }

            for (int i = 0; i < flameCount; i++)
            {
                uint h = Mix(_seed + (uint)(i * 0x9E37));
                float tileX =
                    ((h & 2047u) / 2047f * 2f - 1f)
                    * range
                    * spread;
                float tileY =
                    (((h >> 11) & 2047u) / 2047f * 2f - 1f)
                    * range
                    * spread;
                Vector2 basePoint =
                    ground
                    + new Vector2(
                        (tileX - tileY) * 22f,
                        (tileX + tileY) * 22f
                    );
                float flicker =
                    0.65f
                    + 0.35f
                        * (float)Math.Sin(
                            Time.Ticks * 0.018f + i * 1.91f
                        );
                float rise = (14f + ((h >> 17) & 31u)) * flicker;

                DrawCloud(
                    batcher,
                    i % 5 == 0 ? smoke : red,
                    basePoint - new Vector2(0f, rise * 0.55f),
                    28f + ((h >> 22) & 15u),
                    45f + rise,
                    (tileX - tileY) * 0.025f,
                    fade * (i % 6 == 0 ? 0.3f : 0.66f),
                    depth
                );
                DrawCloud(
                    batcher,
                    orange,
                    basePoint - new Vector2(0f, rise * 0.42f),
                    14f + ((h >> 24) & 7u),
                    30f + rise * 0.62f,
                    (tileY - tileX) * 0.018f,
                    fade * 0.72f,
                    depth
                );

                if ((h & 3u) == 0u)
                {
                    CombatVisualEffect.DrawPoint(
                        batcher,
                        basePoint - new Vector2(0f, 28f + progress * (35f + (h & 31u))),
                        new Color(255, 218, 82),
                        fade * 0.88f,
                        2.2f + (h & 3u),
                        depth
                    );
                }
            }

            DrawBrokenRing(
                batcher,
                ground,
                range * 37f * spread,
                range * 20f * spread,
                progress * 0.6f,
                new Color(255, 69, 10),
                fade * 0.16f,
                5f,
                depth
            );

            for (int light = 0; light < 7; light++)
            {
                Vector2 lightPoint =
                    light == 0
                        ? ground
                        : Ellipse(
                            ground,
                            range * 31f,
                            range * 18f,
                            light * MathHelper.TwoPi / 6f
                        );
                CombatVisualEffect.RecordLight(
                    lightPoint - new Vector2(0f, 22f),
                    new Color(255, 91, 18),
                    220f + range * 7f,
                    fade * (light == 0 ? 0.72f : 0.46f)
                );
            }
        }

        private static void DrawWildfireWall(
            UltimaBatcher2D batcher,
            Vector2 ground,
            int variant,
            float scale,
            float alpha,
            float depth)
        {
            uint baseGraphic = variant % 2 == 0 ? 0x398Cu : 0x3996u;
            ushort graphic =
                (ushort)(
                    baseGraphic
                    + (Time.Ticks / 80u + (uint)(variant * 3)) % 10u
                );
            ref readonly var art = ref Client.Game.Arts.GetArt(graphic);

            if (art.Texture == null)
            {
                return;
            }

            batcher.Draw(
                art.Texture,
                ground,
                art.UV,
                ShaderHueTranslator.GetHueVector(
                    0,
                    false,
                    Math.Min(1f, alpha)
                ),
                0f,
                new Vector2(art.UV.Width * 0.5f, art.UV.Height - 2f),
                new Vector2(scale),
                SpriteEffects.None,
                depth
            );
        }

        private void DrawNetherCyclone(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float grow = Math.Min(1f, progress * 4f);
            float rotation = Time.Ticks * 0.0045f;
            Texture2D blue = GetCloud(ref _blueCloud, new Color(28, 78, 230));
            Texture2D violet = GetCloud(ref _violetCloud, new Color(105, 42, 214));
            Texture2D cyan = GetCloud(ref _cyanCloud, new Color(44, 198, 255));

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int level = 0; level < 12; level++)
            {
                float t = level / 11f;
                float y = ground.Y - 8f - t * 190f * grow;
                float radius = 18f + t * 72f;
                float angle = rotation * (1.6f - t * 0.35f) + level * 1.37f;
                Vector2 c = new Vector2(
                    ground.X + (float)Math.Cos(angle) * radius * 0.34f,
                    y
                );
                DrawCloud(
                    batcher,
                    level % 3 == 0 ? violet : blue,
                    c,
                    radius * 1.7f,
                    24f + radius * 0.34f,
                    angle,
                    fade * (0.42f + t * 0.22f),
                    depth
                );
                DrawCloud(
                    batcher,
                    cyan,
                    c,
                    radius * 0.72f,
                    13f + radius * 0.12f,
                    -angle,
                    fade * 0.42f,
                    depth
                );
            }

            for (int ring = 0; ring < 4; ring++)
            {
                float r = 34f + ring * 22f + (rotation * 22f % 18f);
                DrawBrokenRing(
                    batcher,
                    ground - new Vector2(0f, ring * 3f),
                    r,
                    r * 0.38f,
                    rotation + ring,
                    ring % 2 == 0 ? new Color(63, 122, 255) : new Color(155, 74, 255),
                    fade * (0.58f - ring * 0.08f),
                    5f,
                    depth
                );
            }

            CombatVisualEffect.RecordLight(ground - new Vector2(0f, 76f), new Color(72, 92, 255), 270f, fade * 0.82f);
        }

        private void DrawBombard(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float radius = 18f + EaseOut(progress) * 92f;
            Texture2D smoke = GetCloud(ref _smokeCloud, new Color(61, 54, 52));
            Texture2D orange = GetCloud(ref _orangeCloud, new Color(222, 120, 35));

            batcher.SetBlendState(BlendState.AlphaBlend);

            for (int i = 0; i < 10; i++)
            {
                uint h = Mix(_seed + (uint)(i * 107));
                float angle = ((h & 1023u) / 1023f) * MathHelper.TwoPi;
                Vector2 c = Ellipse(ground, radius * (0.35f + ((h >> 10) & 31u) / 60f), radius * 0.26f, angle);
                DrawCloud(
                    batcher,
                    smoke,
                    c - new Vector2(0f, progress * (18f + ((h >> 15) & 31u))),
                    30f + (h & 15u),
                    22f + ((h >> 20) & 15u),
                    angle,
                    fade * 0.46f,
                    depth
                );
            }

            DrawCloud(batcher, orange, ground, 76f + radius, 34f + radius * 0.34f, progress, fade * 0.3f, depth);
            DrawBrokenRing(batcher, ground, radius, radius * 0.38f, progress, new Color(255, 174, 66), fade * 0.8f, 7f, depth);
            DrawBrokenRing(batcher, ground, radius * 0.76f, radius * 0.28f, -progress, new Color(80, 66, 62), fade * 0.72f, 4f, depth);

            batcher.SetBlendState(BlendState.Additive);

            for (int i = 0; i < 14; i++)
            {
                uint h = Mix(_seed ^ (uint)(i * 251));
                float angle = ((h & 1023u) / 1023f) * MathHelper.TwoPi;
                Vector2 end = Ellipse(ground, radius, radius * 0.42f, angle) - new Vector2(0f, (float)Math.Sin(progress * Math.PI) * (30f + (h & 31u)));
                CombatVisualEffect.DrawLine(batcher, ground, end, new Color(133, 108, 82), fade * 0.72f, 2f + (h & 3u), depth);
            }

            CombatVisualEffect.RecordLight(ground, new Color(255, 148, 48), 175f, fade * 0.64f);
        }

        private void DrawExplosion(
            UltimaBatcher2D batcher,
            Vector2 body,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float expansion = EaseOut(Math.Min(1f, progress * 2.35f));
            float ignition = Math.Min(1f, progress / 0.08f);
            float flash =
                progress < 0.38f
                    ? (float)Math.Sin(progress / 0.38f * MathHelper.Pi)
                    : 0f;
            float smokePhase = Math.Max(0f, (progress - 0.16f) / 0.84f);
            Texture2D red = GetCloud(ref _redCloud, new Color(225, 31, 4));
            Texture2D orange = GetCloud(ref _orangeCloud, new Color(255, 116, 7));
            Texture2D pale = GetCloud(ref _paleCloud, new Color(255, 232, 154));
            Texture2D smoke = GetCloud(ref _smokeCloud, new Color(52, 39, 38));
            Vector2 center = body + new Vector2(0f, 5f);

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                red,
                center,
                44f + expansion * 120f,
                38f + expansion * 104f,
                progress * 0.7f,
                fade * (0.54f + flash * 0.25f),
                depth
            );
            DrawCloud(
                batcher,
                orange,
                center - new Vector2(0f, 4f),
                30f + expansion * 82f,
                28f + expansion * 74f,
                -progress,
                fade * (0.68f + flash * 0.24f),
                depth
            );

            for (int i = 0; i < 12; i++)
            {
                uint h = Mix(_seed + (uint)(i * 269));
                float angle =
                    ((h & 2047u) / 2047f) * MathHelper.TwoPi;
                float distance =
                    expansion * (24f + ((h >> 11) & 63u));
                Vector2 lobe =
                    center
                    + new Vector2(
                        (float)Math.Cos(angle) * distance,
                        (float)Math.Sin(angle) * distance * 0.72f
                    )
                    - new Vector2(0f, smokePhase * (18f + ((h >> 18) & 31u)));
                float size = 25f + ((h >> 23) & 23u);

                DrawCloud(
                    batcher,
                    smokePhase > 0.34f && i % 3 == 0 ? smoke : red,
                    lobe,
                    size + expansion * 24f,
                    size * 0.82f + expansion * 19f,
                    angle + progress,
                    fade * (i % 3 == 0 ? 0.48f : 0.66f),
                    depth
                );
                DrawCloud(
                    batcher,
                    orange,
                    Vector2.Lerp(center, lobe, 0.82f),
                    size * 0.58f,
                    size * 0.46f,
                    -angle,
                    fade * 0.72f,
                    depth
                );
            }

            if (smokePhase > 0f)
            {
                for (int i = 0; i < 5; i++)
                {
                    uint h = Mix(_seed ^ (uint)(i * 619));
                    float drift =
                        (((h >> 7) & 31u) - 15f) * smokePhase;
                    Vector2 cloud =
                        center
                        + new Vector2(
                            drift,
                            -28f - smokePhase * (42f + (h & 31u))
                        );
                    DrawCloud(
                        batcher,
                        smoke,
                        cloud,
                        54f + smokePhase * 48f,
                        42f + smokePhase * 38f,
                        progress + i,
                        fade * smokePhase * 0.5f,
                        depth
                    );
                }
            }

            batcher.SetBlendState(BlendState.Additive);
            DrawCloud(
                batcher,
                pale,
                center,
                20f + expansion * 60f,
                18f + expansion * 50f,
                0f,
                ignition * fade * (0.72f + flash * 0.28f),
                depth
            );

            float pressure = EaseOut(Math.Min(1f, progress * 1.7f));
            DrawBrokenRing(
                batcher,
                ground,
                22f + pressure * 126f,
                9f + pressure * 46f,
                progress * 1.2f,
                new Color(255, 111, 18),
                fade * 0.76f,
                8f,
                depth
            );
            DrawBrokenRing(
                batcher,
                ground,
                13f + pressure * 94f,
                6f + pressure * 34f,
                -progress * 1.8f,
                new Color(255, 227, 130),
                fade * 0.7f,
                2.5f,
                depth
            );

            for (int i = 0; i < 18; i++)
            {
                uint h = Mix(_seed ^ (uint)(i * 431));
                float angle =
                    ((h & 2047u) / 2047f) * MathHelper.TwoPi;
                float distance =
                    expansion * (52f + ((h >> 11) & 79u));
                Vector2 end =
                    center
                    + new Vector2(
                        (float)Math.Cos(angle) * distance,
                        (float)Math.Sin(angle) * distance * 0.62f
                    )
                    - new Vector2(
                        0f,
                        (float)Math.Sin(progress * MathHelper.Pi)
                            * (20f + ((h >> 18) & 31u))
                    );
                Color color =
                    i % 4 == 0
                        ? new Color(255, 245, 184)
                        : new Color(255, 121, 20);

                CombatVisualEffect.DrawLine(
                    batcher,
                    Vector2.Lerp(center, end, 0.68f),
                    end,
                    color,
                    fade * (i % 4 == 0 ? 0.94f : 0.66f),
                    i % 4 == 0 ? 1.3f : 2.4f,
                    depth
                );
            }

            CombatVisualEffect.RecordLight(
                center,
                new Color(255, 105, 18),
                170f + flash * 150f,
                fade * (0.64f + flash * 0.36f)
            );
        }

        private void DrawWordOfDeath(
            UltimaBatcher2D batcher,
            Vector2 ground,
            float progress,
            float fade,
            float depth)
        {
            float reveal = EaseOut(Math.Min(1f, progress * 5.2f));
            float pulse =
                0.96f + 0.06f * (float)Math.Sin(Time.Ticks * 0.022f);
            float heartbeat =
                0.72f
                + 0.28f
                    * (float)Math.Pow(
                        Math.Max(0f, Math.Sin(Time.Ticks * 0.009f)),
                        5
                    );
            Vector2 skullCenter =
                ground - new Vector2(0f, 142f + 18f * reveal);
            Texture2D red = GetCloud(ref _redCloud, new Color(205, 12, 22));
            Texture2D violet = GetCloud(ref _violetCloud, new Color(104, 31, 172));
            Texture2D black = GetCloud(ref _blackCloud, new Color(12, 2, 18));
            Texture2D skull = GetDeathSkull();
            float iconScale =
                reveal * pulse * (0.68f + 0.045f * heartbeat);
            Vector2 skullOrigin = new Vector2(
                skull.Width * 0.5f,
                skull.Height * 0.5f
            );

            batcher.SetBlendState(BlendState.AlphaBlend);
            DrawCloud(
                batcher,
                black,
                skullCenter,
                205f * pulse,
                176f * pulse,
                0f,
                fade * 0.82f,
                depth
            );
            DrawCloud(
                batcher,
                violet,
                skullCenter,
                172f * pulse,
                150f * pulse,
                progress * 0.35f,
                fade * 0.56f,
                depth
            );
            batcher.Draw(
                skull,
                skullCenter,
                skull.Bounds,
                ShaderHueTranslator.GetHueVector(0, false, fade),
                0f,
                skullOrigin,
                new Vector2(iconScale),
                SpriteEffects.None,
                depth
            );

            batcher.SetBlendState(BlendState.Additive);
            batcher.Draw(
                skull,
                skullCenter,
                skull.Bounds,
                ShaderHueTranslator.GetHueVector(
                    0,
                    false,
                    fade * (0.08f + 0.08f * heartbeat)
                ),
                0f,
                skullOrigin,
                new Vector2(iconScale * 1.025f),
                SpriteEffects.None,
                depth
            );
            DrawCloud(
                batcher,
                red,
                skullCenter,
                54f * heartbeat,
                46f * heartbeat,
                0f,
                fade * 0.48f,
                depth
            );

            for (int i = 0; i < 14; i++)
            {
                uint h = Mix(_seed + (uint)(i * 283));
                float angle =
                    ((h & 1023u) / 1023f) * MathHelper.TwoPi
                    + progress * (i % 2 == 0 ? 2.6f : -2.1f);
                Vector2 mote =
                    Ellipse(
                        skullCenter,
                        92f + (h & 31u),
                        76f + ((h >> 8) & 31u),
                        angle
                    );
                CombatVisualEffect.DrawPoint(
                    batcher,
                    mote,
                    i % 3 == 0
                        ? new Color(255, 151, 185)
                        : new Color(130, 43, 209),
                    fade * 0.8f,
                    2f + ((h >> 16) & 3u),
                    depth
                );
            }

            DrawBrokenRing(
                batcher,
                ground,
                48f + progress * 118f,
                18f + progress * 43f,
                progress * 2.4f,
                new Color(178, 20, 67),
                fade * 0.72f,
                8f,
                depth
            );
            CombatVisualEffect.RecordLight(
                skullCenter,
                new Color(176, 24, 80),
                275f,
                fade * heartbeat
            );
        }

        private void GetCenters(int posX, int posY, out Vector2 body, out Vector2 ground)
        {
            int tileX = posX + (int)Offset.X + 22;
            int tileY = posY + (int)(Offset.Z + Offset.Y) + 22;
            body = new Vector2(tileX, tileY - 29f);
            ground = new Vector2(tileX, tileY);

            if (!(Source is Mobile mobile))
            {
                return;
            }

            Rectangle bounds = mobile.GetOnScreenRectangle();

            if (bounds.Width > 0 && bounds.Height > 0)
            {
                body = new Vector2(bounds.Center.X, bounds.Y + bounds.Height * 0.52f);
                ground = new Vector2(bounds.Center.X, bounds.Bottom - 3f);
            }
        }

        private static int GetDuration(EnhancedSpellVisualKind kind)
        {
            switch (kind)
            {
                case EnhancedSpellVisualKind.Wildfire:
                    return 3600;
                case EnhancedSpellVisualKind.NetherCyclone:
                    return 1800;
                case EnhancedSpellVisualKind.WordOfDeath:
                    return 2800;
                case EnhancedSpellVisualKind.Thunderstorm:
                    return 3200;
                case EnhancedSpellVisualKind.Bombard:
                    return 950;
                case EnhancedSpellVisualKind.Explosion:
                    return 1300;
                case EnhancedSpellVisualKind.MeteorSwarm:
                    return 2100;
                case EnhancedSpellVisualKind.NetherBolt:
                    return 950;
                case EnhancedSpellVisualKind.EagleStrike:
                    return 1100;
                case EnhancedSpellVisualKind.HailStorm:
                    return 1900;
                default:
                    return 800;
            }
        }

        private static float SmoothFade(float progress)
        {
            if (progress < 0.12f)
            {
                return progress / 0.12f;
            }

            float remaining = (1f - progress) / 0.42f;
            return Math.Max(0f, Math.Min(1f, remaining));
        }

        private static float EaseOut(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse;
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
            const int segments = 18;
            batcher.SetBlendState(BlendState.Additive);

            for (int i = 0; i < segments; i++)
            {
                if (i % 6 == 2)
                {
                    continue;
                }

                float a0 = rotation + i * MathHelper.TwoPi / segments;
                float a1 = rotation + (i + 0.76f) * MathHelper.TwoPi / segments;
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

        private static Texture2D GetDeathSkull()
        {
            if (_deathSkull != null && !_deathSkull.IsDisposed)
            {
                return _deathSkull;
            }

            byte[] data = Loader.GetWordOfDeathSkull().ToArray();

            using (var stream = new MemoryStream(data, false))
            {
                _deathSkull = Texture2D.FromStream(
                    Client.Game.GraphicsDevice,
                    stream
                );
            }

            return _deathSkull;
        }

        private static Texture2D GetCloud(ref Texture2D texture, Color color)
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
                    float lobe = 0.78f + 0.22f * (float)Math.Sin(angle * 7f + radius * 15f);
                    float coverage = Math.Min(1f, (1f - radius) * (1f - radius) * 2f * lobe);
                    pixels[y * size + x] = new Color(
                        (byte)(color.R * coverage),
                        (byte)(color.G * coverage),
                        (byte)(color.B * coverage),
                        (byte)(255f * coverage)
                    );
                }
            }

            texture = new Texture2D(Client.Game.GraphicsDevice, size, size);
            texture.SetData(pixels);
            return texture;
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

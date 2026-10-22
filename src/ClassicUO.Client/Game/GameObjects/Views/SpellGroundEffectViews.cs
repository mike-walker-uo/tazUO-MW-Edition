using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.GameObjects
{
    internal sealed partial class CombatVisualEffect
    {
        internal bool DrawGround(UltimaBatcher2D batcher) =>
            SpellAbilityEffectSettings.TryMap(_kind, out SpellAbilityEffectId id)
            && SpellGroundGlow.DrawStationary(batcher, id, this, Time.Ticks - _born, (uint)_visualDuration);
    }

    internal sealed partial class EnhancedSpellVisualEffect
    {
        internal bool DrawGround(UltimaBatcher2D batcher) =>
            SpellAbilityEffectSettings.TryMap(_kind, out SpellAbilityEffectId id)
            && SpellGroundGlow.DrawStationary(batcher, id, this, Time.Ticks - _born, (uint)_visualDuration);
    }

    internal sealed partial class HitAreaEffect
    {
        internal bool DrawGround(UltimaBatcher2D batcher) => _enhancedAdmitted
            && SpellAbilityEffectSettings.TryMap(_element, out SpellAbilityEffectId id)
            && SpellGroundGlow.DrawStationary(batcher, id, this, Time.Ticks - _born, (uint)_visualDuration);
    }

    internal sealed partial class NetherBlastVortexEffect
    {
        internal bool DrawGround(UltimaBatcher2D batcher) => NetherBlastVortexManager.Style != 0
            && SpellGroundGlow.DrawStationary(batcher, SpellAbilityEffectId.NetherBlast, this,
                Time.Ticks - _born, _fieldSerial == 0 ? 2400u : 8000u);
    }

    internal sealed partial class DeathRayLinkEffect
    {
        internal bool DrawGround(UltimaBatcher2D batcher)
        {
            uint elapsed = Time.Ticks - _born;
            if (IsDestroyed || SpellGroundGlow.Fade(elapsed, VisualDuration) <= 0
                || !SpellGroundGlow.TrySettings(SpellAbilityEffectId.DeathRay, out _, out _)) return false;
            if (!TryGroundPoint(_casterSerial, _casterX, _casterY, _casterZ, out Vector2 caster)
                || !TryGroundPoint(_targetSerial, _targetX, _targetY, _targetZ, out Vector2 target)) return false;
            // A faint pool beneath the beam, bounded to six patches regardless of beam length.
            for (int i = 0; i < 6; i++)
                SpellGroundGlow.DrawEffect(batcher, SpellAbilityEffectId.DeathRay,
                    Vector2.Lerp(caster, target, i / 5f), elapsed, VisualDuration, i == 0 || i == 5 ? 0.7f : 0.4f);
            return true;
        }

        private bool TryGroundPoint(uint serial, ushort x, ushort y, sbyte z, out Vector2 center)
        {
            Entity entity = World.Get(serial);
            if (entity != null && !entity.IsDestroyed)
            {
                center = entity.GetScreenPosition() + new Vector2(22, 19);
                return true;
            }
            center = default;
            if (SerialHelper.IsValid(serial) || !SpellGroundGlow.TrySurfaceZ(new Point(x, y), z, out int groundZ)) return false;
            center = new Vector2(RealScreenPosition.X + 22 + ((x - X) - (y - Y)) * 22,
                RealScreenPosition.Y + 22 + ((x - X) + (y - Y)) * 22 - (groundZ - Z) * 4);
            return true;
        }
    }
}

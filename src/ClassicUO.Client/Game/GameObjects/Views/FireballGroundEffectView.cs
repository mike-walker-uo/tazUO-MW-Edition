using System;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.GameObjects
{
    internal sealed partial class MovingEffect
    {
        internal static float GroundFade(uint elapsed, float targetDistance) =>
            Math.Min(1f, elapsed / 60f) * MathHelper.Clamp((targetDistance - 22f) / 66f, 0f, 1f);

        internal static float GroundStrength(float height) => 1f / (1f + Math.Max(0f, height) * 0.05f);

        internal static Point GroundTile(ushort x, ushort y, Vector3 offset) => new Point(
            (int)Math.Floor(x + (offset.X + offset.Y) / 44f + 0.5f),
            (int)Math.Floor(y + (offset.Y - offset.X) / 44f + 0.5f));

        internal static Vector2 GroundCenter(Point screen, Vector3 offset, int z, int groundZ) =>
            new Vector2(screen.X + offset.X + 22f, screen.Y + offset.Y + 22f + (z - groundZ) * 4f);

        // Follow the projectile's projected XY; discard its vertical offset for the ground pool.
        internal bool DrawGround(UltimaBatcher2D batcher)
        {
            if (IsDestroyed || !SpellAbilityEffectSettings.TryMap(CombatVisual, out SpellAbilityEffectId id)
                && !SpellAbilityEffectSettings.TryMap(EnhancedSpellVisual, out id)) return false;
            if (!SpellGroundGlow.TrySettings(id, out float size, out float opacity)
                || !SpellGroundGlow.TryStyle(id, out var style)) return false;

            var target = GetTarget();
            float dx = ((target.x - X) - (target.y - Y)) * 22f - Offset.X;
            float dy = ((target.x - X) + (target.y - Y)) * 22f - (target.z - Z) * 4f - Offset.Y;
            float fade = GroundFade(Time.Ticks - _groundStarted, (float)Math.Sqrt(dx * dx + dy * dy));
            Point tile = GroundTile(X, Y, Offset);
            if (fade <= 0f || !SpellGroundGlow.TrySurfaceZ(tile, Z, out int groundZ)) return false;

            using var presentation = EffectPresentation.For(id);
            Vector2 center = GroundCenter(RealScreenPosition, Offset, Z, groundZ);
            float strength = GroundStrength(Z - groundZ - Offset.Z / 4f);
            float diameter = style.Diameter * size * (1f + (1f - strength) * 0.6f);
            float pulse = 0.94f + (float)Math.Sin((Time.Ticks - _groundStarted) * 0.035f) * 0.06f;
            float alpha = opacity * fade * strength * pulse;
            SpellGroundGlow.Draw(batcher, center, diameter, style.Glow, alpha * style.Strength);
            SpellGroundGlow.Draw(batcher, center, diameter * 0.42f, style.Core, alpha * (id == SpellAbilityEffectId.Fireball ? 0.58f : style.Strength * 0.6f));
            return true;
        }
    }
}

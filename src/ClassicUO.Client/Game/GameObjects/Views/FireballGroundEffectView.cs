using System;
using ClassicUO.Assets;
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
            var settings = VisualBudget.Settings;
            if (IsDestroyed || CombatVisual != CombatVisualKind.Fireball
                || settings?.FireballGroundEnabled != true || settings.FireballGroundOpacity <= 0
                || !SpellAbilityEffectSettings.CustomEffectsEnabled
                || !SpellAbilityEffectSettings.IsEnabled(SpellAbilityEffectId.Fireball)) return false;

            var target = GetTarget();
            float dx = ((target.x - X) - (target.y - Y)) * 22f - Offset.X;
            float dy = ((target.x - X) + (target.y - Y)) * 22f - (target.z - Z) * 4f - Offset.Y;
            float fade = GroundFade(Time.Ticks - _groundStarted, (float)Math.Sqrt(dx * dx + dy * dy));
            Point tile = GroundTile(X, Y, Offset);
            if (fade <= 0f || !TryGroundZ(tile, out int groundZ)) return false;

            using var presentation = EffectPresentation.For(SpellAbilityEffectId.Fireball);
            Vector2 center = GroundCenter(RealScreenPosition, Offset, Z, groundZ);
            float strength = GroundStrength(Z - groundZ - Offset.Z / 4f);
            float size = MathHelper.Clamp(settings.FireballGroundSize, 50, 200) / 100f;
            float diameter = 110f * size * (1f + (1f - strength) * 0.6f);
            float pulse = 0.94f + (float)Math.Sin((Time.Ticks - _groundStarted) * 0.035f) * 0.06f;
            float alpha = MathHelper.Clamp(settings.FireballGroundOpacity, 0, 100) / 100f * fade * strength * pulse;
            SpellGroundGlow.Draw(batcher, center, diameter, new Color(255, 80, 15), alpha * 0.72f);
            SpellGroundGlow.Draw(batcher, center, diameter * 0.42f, new Color(255, 190, 65), alpha * 0.58f);
            return true;
        }

        private bool TryGroundZ(Point tile, out int groundZ)
        {
            groundZ = int.MinValue;
            // Loaded surfaces only: no chunk loading, server requests or changes to effect movement.
            for (GameObject obj = World.Map?.GetTile(tile.X, tile.Y, false); obj != null; obj = obj.TNext)
            {
                if (obj.IsDestroyed) continue;
                int top;
                if (obj is Land land) top = land.AverageZ;
                else
                {
                    StaticTiles data;
                    if (obj is Static scenery) data = scenery.ItemData;
                    else if (obj is Multi multi) data = multi.ItemData;
                    else if (obj is Item item && !item.IsMulti) data = item.ItemData;
                    else continue;
                    if (!(data.IsSurface || data.IsBridge) || data.Height == 0xFF) continue;
                    top = obj.Z + (data.IsBridge ? data.Height / 2 : data.Height);
                }
                // Select the highest surface beneath this projectile, never an overhead floor.
                if (top <= Z && top > groundZ) groundZ = top;
            }
            return groundZ != int.MinValue;
        }
    }
}

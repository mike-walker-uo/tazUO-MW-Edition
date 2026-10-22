using System;
using ClassicUO.Assets;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal static partial class SpellGroundGlow
    {
        private const int GROUND_CELL = 128;
        private static Texture2D _groundTexture;

        private static readonly string[] EffectNames = Enum.GetNames(typeof(SpellAbilityEffectId));

        internal static bool TrySettings(SpellAbilityEffectId id, out float size, out float opacity)
        {
            size = opacity = 0f;
            var settings = VisualBudget.Settings;
            if (settings?.SpellGroundEffectsEnabled != true || !SpellAbilityEffectSettings.CustomEffectsEnabled) return false;
            bool enabled;
            int percentSize, percentOpacity;
            if (id == SpellAbilityEffectId.Fireball)
            {
                enabled = settings.FireballGroundEnabled;
                percentSize = settings.FireballGroundSize;
                percentOpacity = settings.FireballGroundOpacity;
            }
            else if (id == SpellAbilityEffectId.Lightning)
            {
                enabled = settings.LightningGroundEnabled;
                percentSize = settings.LightningGroundSize;
                percentOpacity = settings.LightningGroundOpacity;
            }
            else
            {
                settings.Effects.TryGetValue(EffectNames[(int)id], out var detail);
                enabled = detail?.GroundEnabled ?? true;
                percentSize = detail?.GroundSize ?? 100;
                percentOpacity = detail?.GroundOpacity ?? 55;
            }
            size = MathHelper.Clamp(percentSize, 50, 200) / 100f;
            opacity = MathHelper.Clamp(percentOpacity, 0, 100) / 100f;
            return enabled && opacity > 0 && SpellAbilityEffectSettings.IsEnabled(id);
        }

        internal static bool TryCenter(GameEffect effect, out Vector2 center)
        {
            if (effect.Source is Mobile mobile)
            {
                center = mobile.GetScreenPosition() + new Vector2(22, 19);
                return !mobile.IsDestroyed;
            }
            center = default;
            if (!TrySurfaceZ(new Point(effect.X, effect.Y), effect.Z, out int groundZ)) return false;
            center = MovingEffect.GroundCenter(effect.RealScreenPosition, effect.Offset, effect.Z, groundZ);
            return true;
        }

        internal static bool TrySurfaceZ(Point tile, int ceiling, out int groundZ)
        {
            groundZ = int.MinValue;
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
                if (top <= ceiling && top > groundZ) groundZ = top;
            }
            return groundZ != int.MinValue;
        }

        internal static bool DrawStationary(UltimaBatcher2D batcher, SpellAbilityEffectId id,
            GameEffect effect, uint elapsed, uint duration)
        {
            return !effect.IsDestroyed && Fade(elapsed, duration) > 0
                && TrySettings(id, out _, out _) && TryCenter(effect, out Vector2 center)
                && DrawEffect(batcher, id, center, elapsed, duration);
        }

        internal static bool DrawPlayer(UltimaBatcher2D batcher, SpellAbilityEffectId id, long started, long duration)
        {
            long age = (long)Time.Ticks - started;
            if (started == 0 || age < 0 || age >= duration || World.Player == null || !World.InGame) return false;
            return DrawEffect(batcher, id, World.Player.GetScreenPosition() + new Vector2(22, 19), (uint)age, (uint)duration);
        }

        internal static void Draw(UltimaBatcher2D batcher, Vector2 center,
            float diameter, Color color, float opacity, bool ring = false)
        {
            EnsureGroundTexture(batcher.GraphicsDevice);
            var source = new Rectangle(ring ? GROUND_CELL : 0, 0, GROUND_CELL, GROUND_CELL);
            color.A = (byte)(MathHelper.Clamp(opacity, 0, 1) * 255);
            batcher.Draw(_groundTexture, center - new Vector2(diameter * 0.5f, diameter * 0.25f),
                source, color, 0f, new Vector2(diameter / GROUND_CELL, diameter * 0.5f / GROUND_CELL), 0f);
        }

        private static void EnsureGroundTexture(GraphicsDevice device)
        {
            if (_groundTexture != null && !_groundTexture.IsDisposed) return;
            var pixels = new Color[GROUND_CELL * GROUND_CELL * 2];
            for (int y = 0; y < GROUND_CELL; y++)
                for (int x = 0; x < GROUND_CELL; x++)
                {
                    float dx = (x + 0.5f) / (GROUND_CELL * 0.5f) - 1f;
                    float dy = (y + 0.5f) / (GROUND_CELL * 0.5f) - 1f;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    float edge = MathHelper.Clamp((1f - r) * 5f, 0, 1);
                    byte glow = (byte)(Math.Exp(-r * r * 4.5f) * edge * 255);
                    byte halo = (byte)(Math.Exp(-Math.Pow((r - 0.74f) / 0.1f, 2)) * edge * 255);
                    pixels[y * GROUND_CELL * 2 + x] = new Color(glow, glow, glow, glow);
                    pixels[y * GROUND_CELL * 2 + x + GROUND_CELL] = new Color(halo, halo, halo, halo);
                }
            _groundTexture = new Texture2D(device, GROUND_CELL * 2, GROUND_CELL);
            _groundTexture.SetData(pixels);
        }

        internal static void Reset()
        {
            _groundTexture?.Dispose();
            _groundTexture = null;
        }
    }
}

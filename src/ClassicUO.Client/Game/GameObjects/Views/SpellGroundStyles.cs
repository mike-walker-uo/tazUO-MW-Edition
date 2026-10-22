using System;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.GameObjects
{
    internal enum SpellGroundMotion : byte { Pulse, Ripple, Inward, Swirl, Flicker, Scattered }

    internal readonly struct SpellGroundStyle
    {
        internal readonly Color Glow, Core;
        internal readonly float Diameter, Strength;
        internal readonly SpellGroundMotion Motion;
        internal SpellGroundStyle(Color glow, Color core, float diameter, float strength, SpellGroundMotion motion)
        { Glow = glow; Core = core; Diameter = diameter; Strength = strength; Motion = motion; }
    }

    internal static partial class SpellGroundGlow
    {
        // Decorative footprints, not damage-radius indicators. Frequent hit procs stay faint.
        internal static bool TryStyle(SpellAbilityEffectId id, out SpellGroundStyle style)
        {
            switch (id)
            {
                case SpellAbilityEffectId.MagicArrow: style = new SpellGroundStyle(new Color(255, 163, 50), new Color(255, 236, 160), 70, 0.20f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.Fireball: style = new SpellGroundStyle(new Color(255, 80, 15), new Color(255, 190, 65), 110, 0.72f, SpellGroundMotion.Flicker); break;
                case SpellAbilityEffectId.EnergyBolt: style = new SpellGroundStyle(new Color(85, 100, 255), new Color(180, 228, 255), 120, 0.48f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.NetherBolt: style = new SpellGroundStyle(new Color(125, 45, 230), new Color(235, 100, 255), 115, 0.40f, SpellGroundMotion.Inward); break;
                case SpellAbilityEffectId.EagleStrike: style = new SpellGroundStyle(new Color(75, 178, 210), new Color(255, 218, 115), 140, 0.32f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.Bombard: style = new SpellGroundStyle(new Color(190, 117, 55), new Color(255, 218, 155), 175, 0.28f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.DeathRay: style = new SpellGroundStyle(new Color(92, 35, 218), new Color(74, 165, 255), 95, 0.34f, SpellGroundMotion.Flicker); break;
                case SpellAbilityEffectId.WordOfDeath: style = new SpellGroundStyle(new Color(95, 30, 170), new Color(80, 235, 195), 180, 0.36f, SpellGroundMotion.Inward); break;
                case SpellAbilityEffectId.Explosion: style = new SpellGroundStyle(new Color(255, 100, 12), new Color(255, 236, 170), 210, 0.66f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.MeteorSwarm: style = new SpellGroundStyle(new Color(255, 80, 12), new Color(255, 205, 90), 260, 0.30f, SpellGroundMotion.Scattered); break;
                case SpellAbilityEffectId.Thunderstorm: style = new SpellGroundStyle(new Color(75, 105, 235), new Color(180, 230, 255), 240, 0.32f, SpellGroundMotion.Flicker); break;
                case SpellAbilityEffectId.Wildfire: style = new SpellGroundStyle(new Color(255, 87, 15), new Color(255, 184, 55), 250, 0.32f, SpellGroundMotion.Flicker); break;
                case SpellAbilityEffectId.NetherCyclone: style = new SpellGroundStyle(new Color(115, 55, 230), new Color(95, 205, 255), 200, 0.32f, SpellGroundMotion.Swirl); break;
                case SpellAbilityEffectId.HailStorm: style = new SpellGroundStyle(new Color(90, 175, 235), new Color(215, 248, 255), 240, 0.26f, SpellGroundMotion.Scattered); break;
                case SpellAbilityEffectId.NetherBlast: style = new SpellGroundStyle(new Color(60, 100, 235), new Color(125, 215, 255), 150, 0.30f, SpellGroundMotion.Swirl); break;
                case SpellAbilityEffectId.Lightning: style = new SpellGroundStyle(new Color(70, 145, 255), new Color(210, 239, 255), 180, 0.65f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.Harm: style = new SpellGroundStyle(new Color(85, 170, 235), new Color(195, 240, 255), 90, 0.22f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.LowerAttack: style = new SpellGroundStyle(new Color(215, 140, 40), new Color(255, 211, 110), 85, 0.18f, SpellGroundMotion.Inward); break;
                case SpellAbilityEffectId.LowerDefense: style = new SpellGroundStyle(new Color(220, 50, 50), new Color(255, 155, 100), 85, 0.18f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.ManaDrain: style = new SpellGroundStyle(new Color(70, 85, 230), new Color(145, 180, 255), 95, 0.22f, SpellGroundMotion.Inward); break;
                case SpellAbilityEffectId.StaminaDrain: style = new SpellGroundStyle(new Color(65, 180, 105), new Color(180, 235, 100), 95, 0.20f, SpellGroundMotion.Inward); break;
                case SpellAbilityEffectId.BattleLust: style = new SpellGroundStyle(new Color(210, 35, 65), new Color(255, 135, 65), 115, 0.22f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.HitFireArea: style = new SpellGroundStyle(new Color(255, 95, 20), new Color(255, 205, 85), 165, 0.27f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.HitColdArea: style = new SpellGroundStyle(new Color(90, 175, 245), new Color(215, 248, 255), 165, 0.25f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.HitPoisonArea: style = new SpellGroundStyle(new Color(55, 190, 65), new Color(175, 240, 85), 165, 0.22f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.HitEnergyArea: style = new SpellGroundStyle(new Color(125, 65, 240), new Color(110, 205, 255), 165, 0.25f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.Onslaught: style = new SpellGroundStyle(new Color(235, 140, 40), new Color(255, 240, 185), 130, 0.24f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.ConsecrateWeapon: style = new SpellGroundStyle(new Color(105, 150, 245), new Color(255, 210, 100), 120, 0.25f, SpellGroundMotion.Swirl); break;
                case SpellAbilityEffectId.DivineFury: style = new SpellGroundStyle(new Color(100, 170, 255), new Color(255, 220, 125), 135, 0.28f, SpellGroundMotion.Flicker); break;
                case SpellAbilityEffectId.SacredJourney: style = new SpellGroundStyle(new Color(230, 165, 65), new Color(205, 230, 255), 145, 0.28f, SpellGroundMotion.Inward); break;
                case SpellAbilityEffectId.EnemyOfOne: style = new SpellGroundStyle(new Color(215, 40, 55), new Color(255, 195, 70), 120, 0.24f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.CleanseByFire: style = new SpellGroundStyle(new Color(255, 125, 30), new Color(255, 240, 170), 125, 0.28f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.CloseWounds: style = new SpellGroundStyle(new Color(240, 180, 95), new Color(255, 240, 215), 115, 0.26f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.RemoveCurse: style = new SpellGroundStyle(new Color(135, 85, 235), new Color(205, 235, 255), 130, 0.25f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.Confidence: style = new SpellGroundStyle(new Color(220, 160, 60), new Color(255, 235, 180), 110, 0.22f, SpellGroundMotion.Pulse); break;
                case SpellAbilityEffectId.Evasion: style = new SpellGroundStyle(new Color(70, 185, 235), new Color(195, 240, 255), 120, 0.22f, SpellGroundMotion.Swirl); break;
                case SpellAbilityEffectId.CounterAttack: style = new SpellGroundStyle(new Color(200, 100, 65), new Color(220, 235, 255), 110, 0.20f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.LightningStrike: style = new SpellGroundStyle(new Color(120, 75, 235), new Color(105, 225, 255), 105, 0.23f, SpellGroundMotion.Ripple); break;
                case SpellAbilityEffectId.MomentumStrike: style = new SpellGroundStyle(new Color(45, 190, 170), new Color(255, 210, 100), 115, 0.22f, SpellGroundMotion.Ripple); break;
                default: style = default; return false;
            }
            return true;
        }

        internal static float Fade(uint elapsed, uint duration) => duration == 0 || elapsed >= duration ? 0f :
            Math.Min(1f, elapsed / 60f) * Math.Min(1f, (duration - elapsed) / Math.Min(300f, duration * 0.4f));

        internal static bool DrawEffect(UltimaBatcher2D batcher, SpellAbilityEffectId id,
            Vector2 center, uint elapsed, uint duration, float sizeMultiplier = 1f)
        {
            float fade = Fade(elapsed, duration);
            if (fade <= 0 || !TrySettings(id, out float size, out float opacity) || !TryStyle(id, out var style)) return false;
            using var presentation = EffectPresentation.For(id);
            float progress = elapsed / (float)duration;
            float phase = elapsed * 0.008f;
            float pulse = style.Motion == SpellGroundMotion.Flicker
                ? 0.80f + 0.12f * (float)Math.Sin(phase * 2.7f) + 0.08f * (float)Math.Sin(phase * 6.1f)
                : 0.94f + 0.06f * (float)Math.Sin(phase);
            float diameter = style.Diameter * size * sizeMultiplier;
            float alpha = opacity * style.Strength * fade * pulse;
            Draw(batcher, center, diameter, style.Glow, alpha);
            Draw(batcher, center, diameter * 0.4f, style.Core, alpha * 0.6f);
            if (style.Motion == SpellGroundMotion.Ripple || style.Motion == SpellGroundMotion.Inward)
            {
                float radius = style.Motion == SpellGroundMotion.Inward ? 1.2f - progress * 0.8f : 0.4f + progress * 0.9f;
                Draw(batcher, center, diameter * radius, style.Core, alpha * 0.55f, true);
            }
            else if (style.Motion == SpellGroundMotion.Swirl || style.Motion == SpellGroundMotion.Scattered)
            {
                for (int i = 0; i < Math.Min(4, EffectPresentation.Count(3)); i++)
                {
                    float angle = style.Motion == SpellGroundMotion.Swirl ? phase + i * MathHelper.TwoPi / 3f : i * 2.399f;
                    float radius = diameter * (style.Motion == SpellGroundMotion.Swirl ? 0.28f : 0.35f);
                    Vector2 point = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle) * 0.5f) * radius;
                    float local = style.Motion == SpellGroundMotion.Scattered ? 0.5f + 0.5f * (float)Math.Sin(phase * 3f + i * 2f) : 1f;
                    Draw(batcher, point, diameter * 0.30f, style.Core, alpha * local * 0.65f);
                }
            }
            return true;
        }
    }
}

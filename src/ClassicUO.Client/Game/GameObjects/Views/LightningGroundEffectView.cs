using System;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    internal sealed partial class LightningEffect
    {
        internal static float GroundFade(uint elapsed) => elapsed >= 400 ? 0f :
            (1f - elapsed / 400f) * (1f - elapsed / 400f);

        // Called in the ground pass, before mobile sprites, using the same camera transform.
        internal bool DrawGround(UltimaBatcher2D batcher)
        {
            var settings = VisualBudget.Settings;
            uint elapsed = Time.Ticks - _groundStarted;
            float fade = GroundFade(elapsed);
            if (settings?.LightningGroundEnabled != true || settings.LightningGroundOpacity <= 0
                || fade <= 0 || !_enhancedAdmitted || !SpellAbilityEffectSettings.CustomEffectsEnabled
                || !_forceEnhanced && !SpellAbilityEffectSettings.IsEnabled(SpellAbilityEffectId.Lightning)) return false;

            using var presentation = EffectPresentation.For(SpellAbilityEffectId.Lightning);
            Vector2 center = Source is Mobile mobile
                ? mobile.GetScreenPosition() + new Vector2(22, 19)
                : GetScreenPosition() + new Vector2(22, 22);
            // The existing preview raises its bolt target above the player's ground Z.
            if (_forceEnhanced && Source == null && World.Player != null)
                center.Y += (Z - World.Player.Z) * 4;

            float scale = MathHelper.Clamp(settings.LightningGroundSize, 50, 200) / 100f;
            float opacity = MathHelper.Clamp(settings.LightningGroundOpacity, 0, 100) / 100f;
            float flash = CalculateBoltAlpha(AnimIndex);
            float alpha = opacity * (fade * 0.22f + flash * 0.65f);
            float progress = elapsed / 400f;
            SpellGroundGlow.Draw(batcher, center, 180f * scale, new Color(70, 145, 255), alpha, false);
            SpellGroundGlow.Draw(batcher, center, (44f + flash * 24f) * scale,
                new Color(210, 239, 255), alpha * 0.8f, false);
            SpellGroundGlow.Draw(batcher, center, (70f + progress * 130f) * scale,
                new Color(125, 195, 255), opacity * fade * 0.48f, true);

            uint seed = CalculateBoltSeed();
            Texture2D aura = SolidColorTextureCache.GetTexture(new Color(65, 150, 255));
            Texture2D core = SolidColorTextureCache.GetTexture(new Color(195, 233, 255));
            Vector3 auraHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.22f);
            Vector3 coreHue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.65f);
            for (int i = 0; i < Math.Min(6, EffectPresentation.Count(5)); i++)
            {
                uint mixed = Mix(seed ^ (uint)(0x7319 + i * 0x9E37));
                float angle = (mixed & 1023u) / 1023f * MathHelper.TwoPi;
                float length = (42f + ((mixed >> 10) & 31u)) * scale;
                Vector2 previous = center;
                for (int j = 1; j <= 3; j++)
                {
                    float radius = length * j / 3f;
                    float bend = angle + (j == 2 ? 0.26f : -0.12f);
                    Vector2 next = center + new Vector2((float)Math.Cos(bend), (float)Math.Sin(bend) * 0.5f) * radius;
                    DrawLine(batcher, aura, previous, next, auraHue, 3f, 0f);
                    DrawLine(batcher, core, previous, next, coreHue, 1.1f - j * 0.2f, 0f);
                    previous = next;
                }
            }
            return true;
        }

    }
}

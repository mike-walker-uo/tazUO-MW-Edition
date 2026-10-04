using ClassicUO.Game.UI;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    internal static class SceneLightPalette
    {
        internal static Color Grade(Color color)
        {
            if (VisualBudget.Settings?.UnifiedLightPalette != true) return color;
            Color tint = Color.Lerp(new Color(255, 238, 211), new Color(180, 205, 255), EnvironmentalShadowManager.Night);
            float strength = 0.12f + EnvironmentalShadowManager.Dusk * 0.12f;
            return Color.Lerp(color, new Color(color.R * tint.R / 255, color.G * tint.G / 255, color.B * tint.B / 255, color.A), strength);
        }
    }
}

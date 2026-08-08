#region license
// TazUO addition.
#endregion

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Full-viewport translucent red tint scaling with player HP. Begins at
    /// 50% HP (very faint) and ramps to maximum tint at 0%. Replaces the
    /// edge-only LowHpVignette. `-hptint on|off`.
    /// </summary>
    public static class LowHpScreenTint
    {
        public static bool Enabled = true;
        public static int StartPct = 50; // begin tinting at this HP%
        public static int MaxAlpha = 110; // 0..255 — peak at 0% HP

        public static void DrawWorld(UltimaBatcher2D batcher, Rectangle bounds)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;
            int max = World.Player.HitsMax;
            if (max <= 0) return;
            int pct = (int)(World.Player.Hits * 100f / max);
            if (pct >= StartPct) return;

            // Linear ramp: at StartPct → alpha 0; at 0% → MaxAlpha.
            float t = 1f - pct / (float)StartPct;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            int a = (int)(MaxAlpha * t);
            if (a <= 2) return;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(120, 0, 0, 255));
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, a / 255f);
            batcher.Draw(tex, bounds, hue);
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"HP screen tint {(on ? "ON" : "OFF")}.",
                (ushort)(on ? 0x35 : 0x21));
        }
    }
}

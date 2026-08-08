#region license
// TazUO addition. See LastTargetHighlight.cs for upstream header.
#endregion

using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Faint pulsing red ring at the feet of every hostile currently
    /// registered as an aggressor against the player. Different from
    /// `-hostilebox` (which highlights ALL hostiles) — this only marks
    /// "targeting YOU right now". `-targetingyou on|off`.
    /// </summary>
    public static class TargetingYouAura
    {
        public static bool Enabled = false;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;

            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(Time.Ticks * 0.006f);
            int alpha = 60 + (int)(80 * pulse);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha / 255f);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 50, 50, 255));

            foreach (var m in MobileCache.Hostiles)
            {
                if (m == null || m.IsDestroyed || m.IsDead) continue;
                if (m == World.Player) continue;
                if (!AggroIndicatorManager.IsAggressor(m.Serial)) continue;

                Point p = PathPreview.TileToScreen(m.X, m.Y, m.Z);
                // Ellipse-ish ring: 12 segments around feet.
                const int RX = 18, RY = 8;
                for (int i = 0; i < 18; i++)
                {
                    float ang = i * (float)(System.Math.PI * 2 / 18);
                    int x = p.X + (int)(System.Math.Cos(ang) * RX);
                    int y = p.Y + (int)(System.Math.Sin(ang) * RY);
                    batcher.Draw(tex, new Rectangle(x - 1, y - 1, 3, 3), hue);
                }
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Targeting-you aura {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

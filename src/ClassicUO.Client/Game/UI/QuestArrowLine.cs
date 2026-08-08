#region license
// TazUO addition. Mirrors NearestHostileLine but targets active QuestArrowGump
// destinations (tracking arrows). Green tint.
#endregion

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Thin green line from the player to each active tracking / quest arrow.
    /// Same Bresenham-stepped 2px segments as NearestHostileLine, cubic alpha
    /// falloff toward the target. `-arrowline on|off`.
    /// </summary>
    public static class QuestArrowLine
    {
        public static bool Enabled = true;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (World.Player == null || !World.InGame) return;

            Point a = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);

            for (var node = UIManager.Gumps.First; node != null; node = node.Next)
            {
                var arrow = node.Value as QuestArrowGump;
                if (arrow == null || arrow.IsDisposed) continue;
                // Target Z unknown — use player Z so endpoint projects to ground at (mx,my).
                Point b = PathPreview.TileToScreen(arrow.TargetX, arrow.TargetY, World.Player.Z);
                DrawLine(batcher, a, b);
            }
        }

        private static void DrawLine(UltimaBatcher2D batcher, Point a, Point b)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            int steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
            if (steps <= 0) return;
            // 4px quads stay contiguous with a 3px step — a third the draw calls.
            int segments = System.Math.Min(steps / 3, 120);
            if (segments < 1) segments = 1;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(20, 140, 50, 255));
            const float PEAK = 0.85f;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float falloff = 1f - t;
                float alpha = PEAK * falloff * falloff;
                if (alpha <= 0.02f) continue;
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
                int px = a.X + (int)(dx * t);
                int py = a.Y + (int)(dy * t);
                batcher.Draw(tex, new Rectangle(px - 2, py - 2, 4, 4), hue);
            }
        }
    }
}

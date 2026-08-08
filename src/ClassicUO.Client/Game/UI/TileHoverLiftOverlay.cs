#region license
// TazUO addition.
#endregion

using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using ClassicUO.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Subtle 2px raised outline drawn above the tile currently under the
    /// mouse cursor — gives a faint "3D lift" feel. Only renders when the
    /// hovered object is Land (walkable terrain). `-tilelift on|off`.
    /// </summary>
    public static class TileHoverLiftOverlay
    {
        public static bool Enabled = false;

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            var sel = SelectedObject.Object;
            Land land = sel as Land;
            if (land == null) return;

            // Tile center in screen coords, lifted by 2px.
            Point p = PathPreview.TileToScreen(land.X, land.Y, land.Z);
            p.Y -= 2;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 255, 220, 255));
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 0.35f);

            // Diamond outline (44 wide, 22 tall).
            const int HW = 22, HH = 11;
            // top→right
            DrawLine(batcher, tex, hue, p.X, p.Y - HH, p.X + HW, p.Y);
            // right→bottom
            DrawLine(batcher, tex, hue, p.X + HW, p.Y, p.X, p.Y + HH);
            // bottom→left
            DrawLine(batcher, tex, hue, p.X, p.Y + HH, p.X - HW, p.Y);
            // left→top
            DrawLine(batcher, tex, hue, p.X - HW, p.Y, p.X, p.Y - HH);
        }

        private static void DrawLine(UltimaBatcher2D batcher, Texture2D tex, Vector3 hue, int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1, dy = y2 - y1;
            int steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
            if (steps <= 0) return;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int px = x1 + (int)(dx * t);
                int py = y1 + (int)(dy * t);
                batcher.Draw(tex, new Rectangle(px, py, 1, 1), hue);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Tile-hover lift {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

#region license
// TazUO addition.
#endregion

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using ClassicUO.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Small icon drawn next to the mouse cursor indicating the kind of
    /// thing currently under it: red square = hostile, green square = ally,
    /// yellow square = container/item. Lightweight: a colored dot, not a
    /// full cursor-art swap (which the UO cursor pipeline doesn't support
    /// cleanly). `-cursorhint on|off`.
    /// </summary>
    public static class CursorHintOverlay
    {
        public static bool Enabled = false;

        public static void Draw(UltimaBatcher2D batcher)
        {
            if (!Enabled) return;
            if (Client.Game == null) return;

            var selected = SelectedObject.Object;
            if (selected == null) return;

            Color color;
            if (selected is Mobile m)
            {
                if (m == World.Player) return;
                var n = m.NotorietyFlag;
                if (n == NotorietyFlag.Innocent) color = new Color(120, 200, 255); // blue
                else if (n == NotorietyFlag.Ally) color = new Color(80, 220, 80);  // green
                else if (n == NotorietyFlag.Invulnerable) color = new Color(255, 220, 80); // yellow
                else color = new Color(255, 60, 60); // hostile red
            }
            else if (selected is Item it)
            {
                color = it.ItemData.IsContainer
                    ? new Color(255, 200, 80)   // container — gold
                    : new Color(200, 200, 200); // generic item — grey
            }
            else return;

            int mx = Mouse.Position.X + 14;
            int my = Mouse.Position.Y + 14;
            Texture2D tex = SolidColorTextureCache.GetTexture(color);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 0.85f);
            batcher.Draw(tex, new Rectangle(mx, my, 7, 7), hue);
            // 1px dark border
            Texture2D bd = SolidColorTextureCache.GetTexture(new Color(0, 0, 0, 255));
            Vector3 bhue = ShaderHueTranslator.GetHueVector(0, false, 0.9f);
            batcher.Draw(bd, new Rectangle(mx - 1, my - 1, 9, 1), bhue);
            batcher.Draw(bd, new Rectangle(mx - 1, my + 7, 9, 1), bhue);
            batcher.Draw(bd, new Rectangle(mx - 1, my,     1, 7), bhue);
            batcher.Draw(bd, new Rectangle(mx + 7, my,     1, 7), bhue);
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Cursor hint {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

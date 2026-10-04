using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls
{
    internal sealed class ResizeGrip : HitBox
    {
        internal ResizeGrip(int x, int y, int w, int h, string tooltip = null, float alpha = 0.5f)
            : base(x, y, w, h, "Drag corner to resize. Alt+click corner to lock/unlock.", alpha)
        {
            MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtonType.Left || !Keyboard.Alt || !(RootParent is Gump gump)) return;
                if (gump is ResizableGump resizable) resizable.ToggleResizeLock();
                else gump.IsLocked = !gump.IsLocked;
            };
        }
        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            bool locked = (RootParent as Gump)?.IsLocked == true;
            Color color = locked ? new Color(190, 158, 92) : MouseIsOver ? Color.White : new Color(115, 177, 210);
            var texture = SolidColorTextureCache.GetTexture(color);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, MouseIsOver ? 1f : 0.75f);
            if (locked)
            {
                batcher.Draw(texture, new Rectangle(x + Width - 10, y + Height - 7, 7, 6), hue);
                batcher.Draw(texture, new Rectangle(x + Width - 9, y + Height - 11, 5, 1), hue);
                batcher.Draw(texture, new Rectangle(x + Width - 9, y + Height - 10, 1, 4), hue);
                batcher.Draw(texture, new Rectangle(x + Width - 5, y + Height - 10, 1, 4), hue);
            }
            else for (int i = 0; i < 3; i++)
                batcher.DrawLine(texture, new Vector2(x + Width - 3 - i * 4, y + Height - 2),
                    new Vector2(x + Width - 2, y + Height - 3 - i * 4), hue, 1f);
            return true;
        }
    }
}

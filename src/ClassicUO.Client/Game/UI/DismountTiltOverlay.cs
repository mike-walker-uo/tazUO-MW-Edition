#region license
// TazUO addition.
#endregion

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Detects mounted→unmounted transition on the player and draws a brief
    /// forward-tilt motion-blur streak under the player for ~250ms. Pure
    /// cosmetic. `-dismounttilt on|off`.
    /// </summary>
    public static class DismountTiltOverlay
    {
        public static bool Enabled = true;
        private const long LIFETIME_MS = 280;

        private static bool _wasMounted;
        private static long _emitAt;

        public static void Tick()
        {
            if (!Enabled || World.Player == null || !World.InGame) return;
            bool isMounted = World.Player.IsMounted;
            if (_wasMounted && !isMounted) _emitAt = (long)Time.Ticks;
            _wasMounted = isMounted;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled || _emitAt == 0) return;
            long age = (long)Time.Ticks - _emitAt;
            if (age >= LIFETIME_MS) { _emitAt = 0; return; }
            if (World.Player == null) return;

            float t = age / (float)LIFETIME_MS;       // 0 → 1
            float alpha = (1f - t) * 0.6f;
            int yOffset = (int)(8f * (1f - (1f - t) * (1f - t))); // ease-out forward
            Point p = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 240, 200, 255));
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);
            // 3-band streak fading rearward.
            for (int i = 0; i < 3; i++)
            {
                int w = 30 - i * 8;
                int h = 2;
                Vector3 hI = ShaderHueTranslator.GetHueVector(0, false, alpha * (1f - i * 0.3f));
                batcher.Draw(tex, new Rectangle(p.X - w/2, p.Y + yOffset + 6 + i*3, w, h), hI);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Dismount tilt {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

#region license
// TazUO addition. See LastTargetHighlight.cs for upstream header.
#endregion

using System.Collections.Generic;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// On each incoming damage hit, briefly draws a thin red line from the
    /// attacker's tile to the player. Identifies who hit you in a swarm.
    /// `-dmgsourceline on|off`. Lines fade over LIFETIME_MS.
    /// </summary>
    public static class DamageSourceLineOverlay
    {
        public static bool Enabled = false;
        private const long LIFETIME_MS = 600;

        private struct Line { public uint Src; public long EmitAt; }
        private static readonly List<Line> _lines = new List<Line>(16);

        /// <summary>Call when an attacker damages the player.</summary>
        public static void RegisterHit(uint sourceSerial)
        {
            if (!Enabled) return;
            if (sourceSerial == 0 || sourceSerial == (World.Player?.Serial ?? 0)) return;
            _lines.Add(new Line { Src = sourceSerial, EmitAt = (long)Time.Ticks });
            if (_lines.Count > 32) _lines.RemoveAt(0);
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!Enabled || _lines.Count == 0) return;
            if (World.Player == null || !World.InGame) return;

            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(255, 60, 60, 255));
            long now = (long)Time.Ticks;
            Point b = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);

            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                long age = now - _lines[i].EmitAt;
                if (age >= LIFETIME_MS) { _lines.RemoveAt(i); continue; }
                var src = World.Mobiles.Get(_lines[i].Src);
                if (src == null || src.IsDestroyed) continue;

                float alpha = 1f - age / (float)LIFETIME_MS;
                Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha * 0.75f);
                Point a = PathPreview.TileToScreen(src.X, src.Y, src.Z);

                int dx = b.X - a.X, dy = b.Y - a.Y;
                int steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));
                if (steps <= 0) continue;
                // 2px quads stay contiguous with a 2px step — half the draw calls.
                int segments = System.Math.Min(steps / 2, 120);
                if (segments < 1) segments = 1;
                for (int j = 0; j <= segments; j++)
                {
                    float t = j / (float)segments;
                    int px = a.X + (int)(dx * t);
                    int py = a.Y + (int)(dy * t);
                    batcher.Draw(tex, new Rectangle(px - 1, py - 1, 2, 2), hue);
                }
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on) _lines.Clear();
            GameActions.Print($"Damage-source line {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }

        public static void ResetSession() => _lines.Clear();
    }
}

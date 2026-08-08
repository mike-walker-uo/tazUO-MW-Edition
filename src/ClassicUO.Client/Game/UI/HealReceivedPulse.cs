#region license
// TazUO addition.
#endregion

using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI
{
    /// <summary>
    /// Brief green glow around the player when HP increases (heal received).
    /// Hooks EventSink.OnPlayerStatChange to detect a positive Hits delta.
    /// `-healpulse on|off`. Default OFF.
    /// </summary>
    public static class HealReceivedPulse
    {
        public static bool Enabled = false;
        private const long LIFETIME_MS = 800;

        private static long _emitAt;
        private static int _emitMagnitude; // damage healed, scales intensity
        private static bool _hooked;
        internal static int HookRegistrationCount { get; private set; }

        public static void Hook()
        {
            if (_hooked) return;
            EventSink.OnPlayerStatChange += OnStatChanged;
            _hooked = true;
            HookRegistrationCount++;
        }

        public static void ResetSession()
        {
            _emitAt = 0;
            _emitMagnitude = 0;
        }

        private static void OnStatChanged(object sender, PlayerStatChangedArgs e)
        {
            if (!Enabled || !SpellAbilityEffectSettings.CustomEffectsEnabled) return;
            if (e.Stat != PlayerStatChangedArgs.PlayerStat.Hits) return;
            int delta = e.NewValue - e.OldValue;
            if (delta <= 0) return; // damage already handled by other overlays
            _emitAt = (long)Time.Ticks;
            _emitMagnitude = delta;
        }

        public static void DrawWorld(UltimaBatcher2D batcher)
        {
            if (!SpellAbilityEffectSettings.CustomEffectsEnabled ||
                !Enabled || _emitAt == 0) return;
            long age = (long)Time.Ticks - _emitAt;
            if (age >= LIFETIME_MS) { _emitAt = 0; return; }
            if (World.Player == null) return;

            float t = age / (float)LIFETIME_MS;
            float alpha = (1f - t) * 0.55f;
            // Magnitude bumps both ring size and brightness.
            float magScale = 1f + System.Math.Min(_emitMagnitude / 50f, 2f);
            int baseR = (int)(22 + 22 * t);
            int rx = (int)(baseR * magScale);
            int ry = (int)((baseR * 0.55f) * magScale);

            Point p = PathPreview.TileToScreen(World.Player.X, World.Player.Y, World.Player.Z);
            Texture2D tex = SolidColorTextureCache.GetTexture(new Color(70, 255, 90, 255));
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, alpha);

            // Expanding ring under player's feet.
            const int SEG = 22;
            for (int i = 0; i < SEG; i++)
            {
                float ang = i * (float)(System.Math.PI * 2 / SEG);
                int x = p.X + (int)(System.Math.Cos(ang) * rx);
                int y = p.Y + (int)(System.Math.Sin(ang) * ry);
                batcher.Draw(tex, new Rectangle(x - 1, y - 1, 3, 3), hue);
            }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Heal pulse {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
        }
    }
}

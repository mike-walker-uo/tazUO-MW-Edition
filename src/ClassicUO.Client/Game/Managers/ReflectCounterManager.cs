#region license
// TazUO addition.
#endregion

using System;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Sniffs journal lines for "reflect" keywords and counts how many
    /// spells your Magic Reflection has bounced this session. Toast on
    /// each event. `-reflectcount on|off|reset`.
    /// </summary>
    public static class ReflectCounterManager
    {
        public static bool Enabled = false;
        public static int SessionCount;
        private static bool _hooked;
        internal static int HookRegistrationCount { get; private set; }

        public static void Hook()
        {
            if (_hooked) return;
            EventSink.RawMessageReceived += OnMsg;
            _hooked = true;
            HookRegistrationCount++;
        }

        private static void OnMsg(object sender, MessageEventArgs e)
        {
            if (!Enabled) return;
            if (string.IsNullOrEmpty(e.Text)) return;
            string s = e.Text;
            if (s.IndexOf("reflect", StringComparison.OrdinalIgnoreCase) < 0) return;
            // Look for "spell" or "bounce" to reduce false positives.
            if (s.IndexOf("spell", StringComparison.OrdinalIgnoreCase) < 0
             && s.IndexOf("bounce", StringComparison.OrdinalIgnoreCase) < 0
             && s.IndexOf("absorb", StringComparison.OrdinalIgnoreCase) < 0)
                return;
            SessionCount++;
            try { UI.Gumps.ToastManager.Show($"Reflect #{SessionCount}", 0x44, 1500); } catch { }
        }

        public static void SetEnabled(bool on)
        {
            Enabled = on;
            GameActions.Print($"Reflect counter {(on ? "ON" : "OFF")} (total: {SessionCount}).",
                (ushort)(on ? 0x35 : 0x21));
        }

        public static void Reset()
        {
            SessionCount = 0;
            GameActions.Print("Reflect counter reset.", 0x35);
        }

        public static void ResetSessionQuiet() => SessionCount = 0;
    }
}

#region license
// TazUO addition.
#endregion

using System;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Wraps a manager's Tick (or any action) so repeated throws don't
    /// take down the whole game loop. After 3 throws within 60s the
    /// callsite is auto-disabled and a journal warning is printed.
    /// Counters self-reset after 60s of clean runs.
    /// </summary>
    public static class SafeTick
    {
        /// <summary>Run <paramref name="action"/>; on exception, log + count.</summary>
        public static void Run(string name, Action action)
        {
            FeatureDiagnostics.Guard(name, action);
        }

        public static bool IsDisabled(string name) => FeatureDiagnostics.IsDisabled(name);

        public static void Reenable(string name)
        {
            FeatureDiagnostics.Reenable(name);
            GameActions.Print($"[TazUO] {name} re-enabled.", 0x35);
        }
    }
}

#region license
// TazUO addition.
#endregion

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Controls how aggressor / war-mode / dangerous-notoriety mobs are
    /// re-hued by MobileView. `-aggrotint red|gray|off`.
    /// </summary>
    public static class AggroTintManager
    {
        public enum TintMode { Off, Gray, Red }
        public static TintMode Mode = TintMode.Off;

        public static void Set(string arg)
        {
            switch ((arg ?? "").Trim().ToLowerInvariant())
            {
                case "red":  Mode = TintMode.Red;  break;
                case "gray":
                case "grey": Mode = TintMode.Gray; break;
                case "off":
                case "none": Mode = TintMode.Off;  break;
                default:
                    GameActions.Print("Usage: -aggrotint red|gray|off", 0x21);
                    return;
            }
            GameActions.Print($"Aggro tint = {Mode}.", 0x35);
        }
    }
}

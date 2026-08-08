#region license
// TazUO addition. Session-only fixed environment light used by the clickable
// environment control gump. Server light is retained for clean restoration.
#endregion

namespace ClassicUO.Game.Managers
{
    internal static class EnvironmentControlManager
    {
        private static int _light = -1;

        public static bool Enabled => _light >= 0;
        public static bool IsDawn { get; private set; }
        public static int Light => _light;

        public static void ApplyLight(int light, bool dawn)
        {
            if (light < 0) light = 0;
            if (light > 30) light = 30;
            _light = light;
            IsDawn = dawn;
            Update();
            GameActions.Print($"Environment light {light}{(dawn ? " (dawn)" : string.Empty)} — client preview.", 0x35);
        }

        public static void ClearLight(bool announce = true)
        {
            bool wasEnabled = Enabled;
            _light = -1;
            IsDawn = false;
            EnvironmentalShadowManager.ClearPreviewLight();
            EnvironmentShowcaseManager.RestoreEffectiveLight();
            if (announce && wasEnabled)
            {
                GameActions.Print("Environment light AUTO — server/profile light restored.", 0x35);
            }
        }

        public static void Update()
        {
            if (!Enabled || !World.InGame) return;
            if (World.Light.Overall != _light) World.Light.Overall = _light;
            if (World.Light.Personal != 0) World.Light.Personal = 0;
            EnvironmentalShadowManager.SetPreviewLight(_light);
        }

        public static void ResetSession()
        {
            _light = -1;
            IsDawn = false;
        }
    }
}

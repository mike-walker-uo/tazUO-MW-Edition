using ClassicUO.Configuration;

namespace ClassicUO.Game.UI
{
    internal enum OverheadEffectSizePreset
    {
        Small,
        Normal,
        Large,
        ExtraLarge
    }

    internal static class OverheadEffectSizeSettings
    {
        private static OverheadEffectSizePreset _sessionPreset =
            OverheadEffectSizePreset.Normal;

        private static OverheadEffectSizePreset Preset
        {
            get
            {
                int value = ProfileManager.CurrentProfile
                    ?.OverheadEffectSizePreset
                    ?? (int)_sessionPreset;
                if (value < (int)OverheadEffectSizePreset.Small)
                {
                    value = (int)OverheadEffectSizePreset.Small;
                }
                else if (value > (int)OverheadEffectSizePreset.ExtraLarge)
                {
                    value = (int)OverheadEffectSizePreset.ExtraLarge;
                }

                return (OverheadEffectSizePreset)value;
            }
        }

        internal static float Scale => GetScale(Preset);

        // Keep sizes above the 50% normal preset from growing into the mobile.
        internal static float VerticalLift =>
            Scale > 0.5f ? (Scale - 0.5f) * 40f : 0f;

        internal static string Name => GetName(Preset);

        internal static bool TrySet(string value)
        {
            if (!TryParse(value, out OverheadEffectSizePreset preset))
            {
                return false;
            }

            _sessionPreset = preset;

            if (ProfileManager.CurrentProfile != null)
            {
                ProfileManager.CurrentProfile.OverheadEffectSizePreset =
                    (int)preset;
            }

            return true;
        }

        internal static bool TryParse(
            string value,
            out OverheadEffectSizePreset preset)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "small":
                case "s":
                    preset = OverheadEffectSizePreset.Small;
                    return true;
                case "normal":
                case "medium":
                case "default":
                case "n":
                case "m":
                    preset = OverheadEffectSizePreset.Normal;
                    return true;
                case "large":
                case "big":
                case "l":
                    preset = OverheadEffectSizePreset.Large;
                    return true;
                case "extralarge":
                case "extra-large":
                case "xlarge":
                case "extra":
                case "xl":
                    preset = OverheadEffectSizePreset.ExtraLarge;
                    return true;
                default:
                    preset = OverheadEffectSizePreset.Normal;
                    return false;
            }
        }

        internal static float GetScale(OverheadEffectSizePreset preset)
        {
            switch (preset)
            {
                case OverheadEffectSizePreset.Small:
                    return 0.25f;
                case OverheadEffectSizePreset.Large:
                    return 0.75f;
                case OverheadEffectSizePreset.ExtraLarge:
                    return 1f;
                default:
                    return 0.5f;
            }
        }

        internal static string GetName(OverheadEffectSizePreset preset)
        {
            switch (preset)
            {
                case OverheadEffectSizePreset.Small:
                    return "small";
                case OverheadEffectSizePreset.Large:
                    return "large";
                case OverheadEffectSizePreset.ExtraLarge:
                    return "extra large";
                default:
                    return "normal";
            }
        }
    }
}

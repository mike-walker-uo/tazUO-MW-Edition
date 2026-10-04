using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    internal static class HealthBarDragSelect
    {
        internal static readonly string[] FilterNames =
        {
            "All Mobiles", "Players only", "Friendly Players only", "Guild only",
            "Hostile Mobiles only", "Grey and Hostile Mobiles only", "Neutral Mobiles only"
        };

        internal static readonly string[] ModifierNames =
        {
            "None", "Ctrl", "Shift", "Alt", "Ctrl + Shift", "Ctrl + Alt", "Shift + Alt", "Ctrl + Shift + Alt"
        };

        internal static void NormalizeSettings(Profile profile)
        {
            if ((uint)profile.DragSelectFilter >= FilterNames.Length)
                profile.DragSelectFilter = HealthBarDragFilter.AllMobiles;

            int[] modifiers = profile.DragSelectFilterModifiers;
            if (modifiers == null || modifiers.Length != FilterNames.Length)
            {
                var resized = new int[FilterNames.Length];
                if (modifiers != null) Array.Copy(modifiers, resized, Math.Min(modifiers.Length, resized.Length));
                profile.DragSelectFilterModifiers = modifiers = resized;
            }

            int used = 0;
            for (int i = 0; i < modifiers.Length; i++)
            {
                int modifier = modifiers[i];
                if ((uint)modifier >= ModifierNames.Length || (modifier != 0 && (used & (1 << modifier)) != 0))
                    modifiers[i] = 0;
                else if (modifier != 0) used |= 1 << modifier;
            }
        }

        internal static void AssignModifier(Profile profile, int filter, int modifier)
        {
            NormalizeSettings(profile);
            for (int i = 0; i < profile.DragSelectFilterModifiers.Length; i++)
                if (modifier != 0 && profile.DragSelectFilterModifiers[i] == modifier)
                    profile.DragSelectFilterModifiers[i] = 0;
            profile.DragSelectFilterModifiers[filter] = modifier;
        }

        internal static bool TryGetOverride(Profile profile, bool ctrl, bool shift, bool alt, out HealthBarDragFilter filter)
        {
            int modifier = (ctrl ? 1 : 0) | (shift ? 2 : 0) | (alt ? 4 : 0);
            if (modifier == 3) modifier = 4;
            else if (modifier == 4) modifier = 3;
            int[] modifiers = profile.DragSelectFilterModifiers;
            if (modifier != 0 && modifiers != null)
            {
                for (int i = 0; i < Math.Min(modifiers.Length, FilterNames.Length); i++)
                {
                    if (modifiers[i] == modifier)
                    {
                        filter = (HealthBarDragFilter)i;
                        return true;
                    }
                }
            }

            filter = (uint)profile.DragSelectFilter < FilterNames.Length ? profile.DragSelectFilter : HealthBarDragFilter.AllMobiles;
            return false;
        }

        internal static bool Matches(HealthBarDragFilter filter, NotorietyFlag notoriety)
        {
            switch (filter)
            {
                case HealthBarDragFilter.Players:
                    return notoriety == NotorietyFlag.Innocent || notoriety == NotorietyFlag.Ally || notoriety == NotorietyFlag.Murderer;
                case HealthBarDragFilter.FriendlyPlayers:
                    return notoriety == NotorietyFlag.Innocent || notoriety == NotorietyFlag.Ally;
                case HealthBarDragFilter.Guild:
                    return notoriety == NotorietyFlag.Ally;
                case HealthBarDragFilter.HostileMobiles:
                    return notoriety == NotorietyFlag.Criminal || notoriety == NotorietyFlag.Enemy || notoriety == NotorietyFlag.Murderer;
                case HealthBarDragFilter.GreyAndHostileMobiles:
                    return notoriety == NotorietyFlag.Gray || notoriety == NotorietyFlag.Criminal || notoriety == NotorietyFlag.Enemy || notoriety == NotorietyFlag.Murderer;
                case HealthBarDragFilter.NeutralMobiles:
                    return notoriety == NotorietyFlag.Gray;
                default:
                    return true;
            }
        }
    }
}

using System;
using System.Text.RegularExpressions;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal static class HealerAppearanceManager
    {
        private static readonly bool Enabled = false;
        internal const string DisplayName = "Dad the Wandering Priest";
        internal const ushort GoldHue = 0x0035;
        private static long _nextSparkle;
        private static readonly string[] Titles =
        {
            "healer", "wandering healer", "the healer", "the wandering healer",
            "the evil healer", "the evil wandering healer", "a gargish wandering healer",
            "the priest of mondain"
        };

        internal static bool HasHealerTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.IndexOf('<') >= 0) text = Regex.Replace(text, "<[^>]*>", " ");
            text = text.Trim();
            foreach (string title in Titles)
            {
                if (text.Equals(title, StringComparison.OrdinalIgnoreCase)
                    || ((title.StartsWith("the ", StringComparison.Ordinal) || title.StartsWith("a ", StringComparison.Ordinal))
                        && text.Length > title.Length && text[text.Length - title.Length - 1] == ' '
                        && text.EndsWith(title, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        internal static bool IsHealer(Mobile mobile)
        {
            if (!Enabled
                || mobile == null || mobile.IsDestroyed || mobile.IsPlayer || mobile.IsRenamable
                || (!mobile.IsHuman && !mobile.IsGargoyle)) return false;
            World.OPL.TryGetNameAndData(mobile.Serial, out string name, out _);
            return HasHealerTitle(mobile.Name) || HasHealerTitle(mobile.Title) || HasHealerTitle(name);
        }

        internal static string GetDisplayName(Entity entity) =>
            entity is Mobile mobile && IsHealer(mobile) ? DisplayName : entity?.Name;

        internal static void ResetSession() => _nextSparkle = 0;

        internal static void Tick()
        {
            if (!Enabled
                || !World.InGame || World.Player == null || !SpellAbilityEffectSettings.CustomEffectsEnabled
                || Time.Ticks < _nextSparkle) return;
            _nextSparkle = (long)Time.Ticks + 1200;
            foreach (Mobile mobile in World.Mobiles.Values)
            {
                if (mobile.IsDead || mobile.IsHidden || mobile.Distance > World.ClientViewRange || !IsHealer(mobile)) continue;
                World.SpawnEffect(GraphicEffectType.FixedFrom, mobile.Serial, mobile.Serial,
                    0x376A, GoldHue, mobile.X, mobile.Y, mobile.Z, mobile.X, mobile.Y, mobile.Z,
                    5, 10, true, false, false, GraphicEffectBlendMode.Screen);
            }
        }
    }
}

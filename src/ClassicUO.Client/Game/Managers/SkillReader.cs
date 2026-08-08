#region license
// TazUO addition.
#endregion

using System;
using System.Collections.Generic;
using ClassicUO.Assets;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Helper for reading the player's current skill values by skill name.
    /// Caches the (case-insensitive) name → index lookup on first hit.
    /// </summary>
    public static class SkillReader
    {
        private static readonly Dictionary<string, int> _idx =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static float Get(string skillName)
        {
            if (World.Player == null || World.Player.Skills == null) return 0f;
            if (!_idx.TryGetValue(skillName, out int i))
            {
                i = LookupIndex(skillName);
                _idx[skillName] = i;
            }
            if (i < 0 || i >= World.Player.Skills.Length) return 0f;
            return World.Player.Skills[i]?.Value ?? 0f;
        }

        private static int LookupIndex(string name)
        {
            if (SkillsLoader.Instance == null || SkillsLoader.Instance.Skills == null) return -1;
            var skills = SkillsLoader.Instance.Skills;
            for (int j = 0; j < skills.Count; j++)
            {
                if (string.Equals(skills[j].Name, name, StringComparison.OrdinalIgnoreCase))
                    return j;
            }
            return -1;
        }
    }
}

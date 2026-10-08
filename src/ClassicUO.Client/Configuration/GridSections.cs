using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Game.Data;

namespace ClassicUO.Configuration
{
    public enum GridSectionMatch { Weapons, Armor, Reagents, EquipmentLayer, Graphic, NameContains }

    public sealed class GridSectionRule
    {
        public string Name { get; set; } = "Section";
        public GridSectionMatch Match { get; set; }
        public int Value { get; set; }
        public string Text { get; set; } = string.Empty;
        public ushort Hue { get; set; } = 0x0481;

        internal bool Matches(StaticTiles data, ushort graphic, string name)
        {
            switch (Match)
            {
                case GridSectionMatch.Weapons: return data.IsWeapon;
                case GridSectionMatch.Armor: return data.IsWearable && !data.IsWeapon;
                case GridSectionMatch.Reagents:
                    return graphic == 0x0F7A || graphic == 0x0F7B || graphic == 0x0F84 || graphic == 0x0F85 ||
                        graphic == 0x0F86 || graphic == 0x0F88 || graphic == 0x0F8C || graphic == 0x0F8D ||
                        graphic == 0x0F78 || graphic == 0x0F8F || graphic == 0x0F7D || graphic == 0x0F8E || graphic == 0x0F8A;
                case GridSectionMatch.EquipmentLayer: return data.Layer == Value && (data.IsWearable || data.IsWeapon);
                case GridSectionMatch.Graphic: return graphic == Value;
                case GridSectionMatch.NameContains: return !string.IsNullOrWhiteSpace(Text) && (name ?? string.Empty).IndexOf(Text, StringComparison.OrdinalIgnoreCase) >= 0;
                default: return false;
            }
        }
    }

    public sealed class GridSectionsConfig
    {
        public bool Enabled { get; set; }
        public List<GridSectionRule> Rules { get; set; } = new List<GridSectionRule>
        {
            new GridSectionRule { Name = "Weapons", Match = GridSectionMatch.Weapons, Hue = 0x0021 },
            new GridSectionRule { Name = "Armor", Match = GridSectionMatch.Armor, Hue = 0x0481 },
            new GridSectionRule { Name = "Reagents", Match = GridSectionMatch.Reagents, Hue = 0x0058 }
        };

        internal int Group(StaticTiles data, ushort graphic, string name)
        {
            for (int i = 0; i < Rules.Count; i++)
                if (Rules[i].Matches(data, graphic, name)) return i;
            return Rules.Count;
        }
    }
}

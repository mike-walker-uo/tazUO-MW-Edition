using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PremiumGumpTheme
    {
        internal readonly CustomGumpTheme Theme;
        internal readonly string Name;
        internal readonly string Description;
        internal readonly Color Surface;
        internal readonly Color Selection;
        internal readonly Color Accent;
        internal readonly int SheetSplit;
        internal readonly int PanelEdge;

        internal PremiumGumpTheme(CustomGumpTheme theme, string name, string description,
            Color surface, Color selection, Color accent, int sheetSplit = 768, int panelEdge = 160)
        {
            Theme = theme;
            Name = name;
            Description = description;
            Surface = surface;
            Selection = selection;
            Accent = accent;
            SheetSplit = sheetSplit;
            PanelEdge = panelEdge;
        }
    }

    internal static class PremiumGumpThemes
    {
        internal static readonly PremiumGumpTheme[] Entries =
        {
            new PremiumGumpTheme(CustomGumpTheme.SovereignGold, "Sovereign Gold", "Black onyx, champagne gold and royal sunburst engraving",
                new Color(19, 20, 24), new Color(66, 52, 34), new Color(209, 174, 103), 704),
            new PremiumGumpTheme(CustomGumpTheme.LunarSilver, "Lunar Silver", "Midnight slate, moonstone and fine silver crescents",
                new Color(17, 24, 36), new Color(39, 60, 79), new Color(161, 193, 216)),
            new PremiumGumpTheme(CustomGumpTheme.DragonEmber, "Dragon Ember", "Charred iron, copper scales and restrained ember inlay",
                new Color(30, 18, 18), new Color(83, 38, 28), new Color(218, 121, 62)),
            new PremiumGumpTheme(CustomGumpTheme.GlacialCrown, "Glacial Crown", "Deep ice-blue crystal, platinum and cut frost tracery",
                new Color(17, 31, 43), new Color(35, 67, 88), new Color(147, 221, 237)),
            new PremiumGumpTheme(CustomGumpTheme.VerdantCathedral, "Verdant Cathedral", "Dark emerald lacquer, bronze vines and leaf tracery",
                new Color(16, 29, 23), new Color(37, 68, 47), new Color(147, 191, 112)),
            new PremiumGumpTheme(CustomGumpTheme.AstralObservatory, "Astral Observatory", "Indigo enamel, astronomical brass and star-map engraving",
                new Color(20, 21, 40), new Color(48, 44, 80), new Color(189, 164, 228)),
            new PremiumGumpTheme(CustomGumpTheme.CrimsonVelvet, "Crimson Velvet", "Oxblood velvet, antique gold and baroque cornerwork",
                new Color(32, 16, 23), new Color(75, 32, 45), new Color(220, 169, 112), 736),
            new PremiumGumpTheme(CustomGumpTheme.PearlSanctum, "Pearl Sanctum", "Smoked pearl, silver shell inlay and pale aqua edging",
                new Color(24, 33, 37), new Color(47, 72, 77), new Color(176, 218, 218), 744),
            new PremiumGumpTheme(CustomGumpTheme.SunkenTreasury, "Sunken Treasury", "Deep ocean glass, oxidized copper and nautical knots",
                new Color(12, 30, 34), new Color(27, 69, 72), new Color(87, 191, 179), 744),
            new PremiumGumpTheme(CustomGumpTheme.RunicObsidian, "Runic Obsidian", "Volcanic black glass, violet crystal and engraved runes",
                new Color(23, 18, 32), new Color(58, 39, 77), new Color(180, 135, 230), 784),
            new PremiumGumpTheme(CustomGumpTheme.AmberAlchemist, "Amber Alchemist", "Burnished bronze, dark amber resin and alchemical geometry",
                new Color(32, 24, 16), new Color(75, 53, 29), new Color(228, 177, 80), 736),
            new PremiumGumpTheme(CustomGumpTheme.IvoryCitadel, "Ivory Citadel", "Graphite inset, ivory stone and gilded architectural trim",
                new Color(25, 25, 27), new Color(64, 60, 49), new Color(211, 204, 178)),
            new PremiumGumpTheme(CustomGumpTheme.JadeDynasty, "Jade Dynasty", "Black jade, polished gold and restrained cloud scrolls",
                new Color(13, 28, 24), new Color(32, 69, 49), new Color(150, 203, 159)),
            new PremiumGumpTheme(CustomGumpTheme.Stormforged, "Stormforged", "Blue steel, platinum edges and electric storm etching",
                new Color(18, 26, 35), new Color(41, 65, 81), new Color(124, 188, 230), 752),
            new PremiumGumpTheme(CustomGumpTheme.RoseQuartzCourt, "Rose Quartz Court", "Plum satin, rose quartz and delicate rose-gold filigree",
                new Color(31, 20, 31), new Color(70, 43, 65), new Color(221, 159, 189)),
            new PremiumGumpTheme(CustomGumpTheme.SapphireReliquary, "Sapphire Reliquary", "Sapphire enamel, silver reliquary work and jeweled corners",
                new Color(13, 24, 40), new Color(29, 52, 85), new Color(122, 170, 236), 784),
            new PremiumGumpTheme(CustomGumpTheme.AncientSandstone, "Ancient Sandstone", "Dark umber inset, carved sandstone and turquoise inlay",
                new Color(34, 27, 19), new Color(73, 59, 37), new Color(206, 180, 115), 784),
            new PremiumGumpTheme(CustomGumpTheme.Nocturne, "Nocturne", "Matte charcoal, cool nickel and precise understated bevels",
                new Color(19, 21, 25), new Color(45, 51, 61), new Color(172, 186, 207), 784),
            new PremiumGumpTheme(CustomGumpTheme.PhoenixImperial, "Phoenix Imperial", "Dark aubergine, imperial gold and phoenix feather motifs",
                new Color(30, 18, 26), new Color(70, 39, 47), new Color(235, 180, 87), 784),
            new PremiumGumpTheme(CustomGumpTheme.PrismaticVault, "Prismatic Vault", "Midnight glass, restrained iridescent facets and platinum",
                new Color(17, 22, 34), new Color(39, 54, 77), new Color(144, 199, 223), 784),
            new PremiumGumpTheme(CustomGumpTheme.EternalEclipse, "Eternal Eclipse", "Sculpted dragons, black opal, star sapphire and platinum-gold filigree",
                new Color(14, 19, 28), new Color(40, 51, 67), new Color(216, 194, 143), 808, 240),
        };

        internal static PremiumGumpTheme Get(CustomGumpTheme theme)
        {
            int index = (int)theme - (int)CustomGumpTheme.SovereignGold;
            return (uint)index < Entries.Length ? Entries[index] : null;
        }

        internal static bool TryParse(string value, out CustomGumpTheme theme)
        {
            string name = (value ?? string.Empty).Replace(" ", string.Empty)
                .Replace("-", string.Empty).Replace("_", string.Empty);
            foreach (PremiumGumpTheme entry in Entries)
            {
                if (string.Equals(name, entry.Theme.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    theme = entry.Theme;
                    return true;
                }
            }
            theme = CustomGumpTheme.Minimal;
            return false;
        }
    }
}

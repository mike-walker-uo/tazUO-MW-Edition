#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Hardcoded mob-name → max-HP table compiled from the shard's mob list.
    /// Used by BodyScaleManager when wire-level HitsMax is scaled (non-player
    /// mobs ship 0..25-ish) and OPL data isn't yet available. Keys are
    /// lowercase with leading "a "/"an "/"the " stripped.
    /// </summary>
    public static class MobHpTable
    {
        private static readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // ── bosses ──────────────────────────────────────────────
            { "adrian", 60000 }, { "medusa", 60000 }, { "navrey night-eyes", 35000 },
            { "silvani", 600 }, { "soulbound pirate raider", 250 }, { "soulbound swashbuckler", 125 },
            // ── named ──────────────────────────────────────────────
            { "abscess", 7540 }, { "drelgor the impaler", 136 }, { "flurry", 477 },
            { "grimmoch drummel", 207 }, { "lysander gathenwale", 207 }, { "mistral", 609 },
            { "morg bergen", 207 }, { "night terror", 50000 }, { "shadow knight", 5000 },
            { "tavara sewel", 207 }, { "tempest", 602 }, { "thrasher", 984 },
            { "tyball's shadow", 3000 }, { "virulent", 740 },
            // ── creatures ───────────────────────────────────────────
            { "abyssmal horror", 6000 }, { "abyssal abomination", 750 }, { "acid elemental", 213 },
            { "acid slug", 370 }, { "agapite elemental", 153 }, { "air elemental", 150 },
            { "alligator", 60 }, { "allosaurus", 18000 }, { "anchisaur", 3718 },
            { "ancient lich", 595 }, { "ancient wyrm", 711 }, { "ant lion", 162 },
            { "arcane daemon", 115 }, { "arch deamon", 711 }, { "archaeosaurus", 2500 },
            { "arctic ogre lord", 552 }, { "bake kitsune", 350 }, { "balron", 711 },
            { "battle chicken lizard", 177 }, { "betrayer", 300 }, { "crow", 55 },
            { "black bear", 60 }, { "black solen infiltrator", 162 }, { "black solen queen", 162 },
            { "black solen warrior", 107 }, { "black solen worker", 72 },
            { "blood elemental", 369 }, { "blood fox", 200 }, { "bloodworm", 422 },
            { "boar", 15 }, { "bog thing", 540 }, { "bogle", 60 }, { "bogling", 72 },
            { "bone demon", 3600 }, { "bone knight", 150 }, { "bone mage", 60 },
            { "bronze elemental", 153 }, { "brown bear", 60 }, { "bulbous putrification", 1231 },
            { "bull", 64 }, { "bull frog", 42 }, { "cat", 6 }, { "centaur", 172 },
            { "changeling", 211 }, { "chaos daemon", 110 }, { "chaos dragoon", 225 },
            { "chaos dragoon elite", 350 }, { "chicken", 3 }, { "chicken lizard", 177 },
            { "clan chitter assistant", 145 }, { "clan scratch tinkerer", 2068 },
            { "clan ribbon courtier", 2100 }, { "clan ribbon supplicant", 127 },
            { "clan ribbon plague rat", 92 }, { "clan scratch henchrat", 2065 },
            { "clan scratch scrounger", 135 }, { "clan scratch savage wolf", 65 },
            { "clockwork scorpion", 210 }, { "cold drake", 500 }, { "copper elemental", 153 },
            { "coral snake", 200 }, { "corporeal brume", 1250 }, { "corpser", 108 },
            { "corrosive slime", 19 }, { "corrupted soul", 69 }, { "cougar", 48 },
            { "cow", 18 }, { "crane", 35 }, { "crystal daemon", 220 },
            { "crystal elemental", 150 }, { "crystal hydra", 1500 },
            { "crystal lattice seeker", 550 }, { "crystal vortex", 400 },
            { "cursed metallic knight", 150 }, { "cursed metallic mage", 66 },
            { "cursed soul", 20 }, { "cyclopean warrior", 231 }, { "daemon", 1174 },
            { "dark guardian", 180 }, { "dark wisp", 135 }, { "darknight creeper", 4000 },
            { "deathwatch beetle", 145 }, { "deathwatch beetle hatchling", 60 },
            { "deep sea serpent", 255 }, { "demon knight", 30000 },
            { "desert scorpion", 400 }, { "devourer of souls", 650 },
            { "dimetrosaur", 5400 }, { "dire wolf", 72 }, { "dog", 22 },
            { "dolphin", 27 }, { "doppleganger", 120 }, { "dragon", 495 },
            { "dragon turtle hatchling", 850 }, { "dragon wolf", 860 }, { "drake", 258 },
            { "dread spider", 132 }, { "dream wraith", 650 },
            { "dull copper elemental", 153 }, { "eagle", 27 },
            { "earth elemental", 550 }, { "effete putrid gargoyle", 111 },
            { "effete undead gargoyle", 70 }, { "efreet", 213 }, { "elder gazer", 195 },
            { "enraged earth elemental", 550 }, { "enslaved gargoyle", 212 },
            { "enslaved goblin keeper", 174 }, { "enslaved goblin mage", 174 },
            { "enslaved goblin scout", 182 }, { "enslaved gray goblin", 179 },
            { "enslaved green goblin", 184 }, { "green goblin alchemist", 197 },
            { "ethereal warrior", 471 }, { "ettin", 99 }, { "evil mage", 63 },
            { "evil mage lord", 63 }, { "exodus minion", 570 }, { "exodus overseer", 390 },
            { "fairy dragon", 403 }, { "fan dancer", 430 }, { "feral treefellow", 1320 },
            { "ferret", 50 }, { "fetid essence", 650 }, { "fire ant", 299 },
            { "fire daemon", 1174 }, { "fire elemental", 93 }, { "fire gargoyle", 240 },
            { "flesh golem", 120 }, { "fleshrenderer", 4500 }, { "frost dragon", 2250 },
            { "frost mite", 1000 }, { "frost ooze", 17 }, { "frost spider", 60 },
            { "frost troll", 156 }, { "gallusaurus", 900 }, { "gaman", 160 },
            { "gargoyle male", 1200 }, { "gargoyle", 105 }, { "gargoyle destroyer", 485 },
            { "gargoyle enforcer", 485 }, { "abyss guardian", 485 },
            { "gargoyle shade", 64 }, { "gazer", 75 }, { "gazer larva", 47 },
            { "ghoul", 60 }, { "giant black widow", 60 }, { "giant ice worm", 147 },
            { "giant rat", 39 }, { "giant serpent", 129 }, { "giant spider", 60 },
            { "giant toad", 60 }, { "giant turkey", 25000 }, { "gibberling", 99 },
            { "goat", 12 }, { "golden elemental", 153 }, { "gore fiend", 111 },
            { "gorilla", 51 }, { "gray goblin", 194 }, { "gray goblin keeper", 186 },
            { "gray goblin mage", 151 }, { "great hart", 41 }, { "greater dragon", 2000 },
            { "greater mongbat", 48 }, { "greater phoenix", 240 },
            { "greater poison elemental", 702 }, { "green goblin", 208 },
            { "green goblin scout", 198 }, { "gremlin", 70 }, { "grey wolf", 48 },
            { "grizzly bear", 93 }, { "grubber", 200 }, { "harpy", 72 },
            { "headless one", 30 }, { "hell cat", 67 }, { "hell hound", 300 },
            { "high plains boura", 618 }, { "hind", 29 }, { "horde minion", 24 },
            { "hydra", 1500 }, { "ice elemental", 111 }, { "ice fiend", 243 },
            { "ice hound", 125 }, { "giant ice serpent", 147 }, { "ice snake", 77 },
            { "imp", 70 }, { "impaler", 5000 }, { "infernus", 243 },
            { "interred grizzle", 1500 }, { "iron beetle", 830 }, { "jack rabbit", 9 },
            { "blackthorn juggernaut", 240 }, { "juka lord", 300 }, { "juka mage", 180 },
            { "juka warrior", 210 }, { "kappa", 180 }, { "kaze kemono", 330 },
            { "kepetch", 400 }, { "kepetch ambusher", 544 }, { "zealot of khaldun", 480 },
            { "kraken", 468 }, { "lady of the snow", 625 }, { "lava elemental", 290 },
            { "lava lizard", 90 }, { "lava serpent", 249 }, { "lava snake", 32 },
            { "leather wolf", 329 }, { "lich", 120 }, { "lich lord", 303 },
            { "lifestealer", 4650 }, { "lion", 370 }, { "lizardman", 72 },
            { "llama", 27 }, { "lowland boura", 553 }, { "dryad", 321 },
            { "maddening horror", 660 }, { "mantra effervescence", 250 },
            { "meer captain", 66 }, { "meer eternal", 303 }, { "meer mage", 120 },
            { "meer warrior", 60 }, { "mimic", 543 }, { "minion of scelestus", 30000 },
            { "minotaur", 340 }, { "minotaur captain", 440 }, { "minotaur scout", 383 },
            { "moloch", 200 }, { "mongbat", 6 }, { "mound of maggots", 85 },
            { "mountain goat", 33 }, { "mummy", 222 }, { "myrmidex drone", 597 },
            { "myrmidex larvae", 588 }, { "myrmidex warrior", 3000 },
            { "najasaurus", 854 }, { "ogre", 117 }, { "ogre lord", 552 }, { "oni", 530 },
            { "ophidian archmage", 183 }, { "ophidian knight", 342 },
            { "ophidian mage", 123 }, { "ophidian matriarch", 303 },
            { "ophidian warrior", 155 }, { "orc", 87 }, { "orc bomber", 123 },
            { "orc brute", 552 }, { "orc chopper", 139 }, { "orc scout", 72 },
            { "orcish lord", 123 }, { "orcish mage", 90 }, { "ortanord", 100 },
            { "ossein ram", 550 }, { "pack horse", 80 }, { "pack llama", 50 },
            { "panther", 51 }, { "patchwork skeleton", 72 }, { "pestilent bandage", 445 },
            { "phoenix", 383 }, { "pig", 12 }, { "pit fiend", 243 }, { "pixie", 18 },
            { "plague beast", 404 }, { "plague beast lord", 1800 },
            { "poison elemental", 309 }, { "polar bear", 84 },
            { "predator hellcat", 131 }, { "protector", 450 },
            { "putrid undead gargoyle", 665 }, { "putrid undead guardian", 553 },
            { "quagmire", 105 }, { "rabbit", 6 }, { "raging grizzly bear", 930 },
            { "rai-ju", 280 }, { "rat", 6 }, { "ratman", 108 }, { "ravager", 175 },
            { "reaper", 129 }, { "red solen infiltrator", 162 },
            { "red solen queen", 162 }, { "red solen warrior", 107 },
            { "red solen worker", 72 }, { "restless soul", 24 },
            { "revenant lion", 280 }, { "rotting corpse", 1200 }, { "rotworm", 250 },
            { "ruddy boura", 509 }, { "rune beetle", 360 }, { "sabre-toothed tiger", 423 },
            { "sand vortex", 62 }, { "satyr", 400 }, { "saurosaurus", 1468 },
            { "savage", 107 }, { "savage rider", 135 }, { "savage shaman", 122 },
            { "scorpion", 63 }, { "sea serpent", 127 }, { "sentinel spider", 265 },
            { "serpentine dragon", 480 }, { "sewer rat", 6 }, { "shade", 60 },
            { "shadow dweller", 120 }, { "shadow iron elemental", 153 },
            { "shadow wyrm", 599 }, { "sheep", 12 }, { "silver serpent", 216 },
            { "silverback gorilla", 588 }, { "skeletal dragon", 599 },
            { "skeletal drake", 400 }, { "skeletal knight", 150 },
            { "skeletal lich", 1200 }, { "skeletal mage", 60 }, { "skeleton", 48 },
            { "skittering hopper", 45 }, { "skree", 300 }, { "slime", 19 },
            { "slith", 85 }, { "snake", 19 }, { "snow elemental", 213 },
            { "snow leopard", 48 }, { "spectral armour", 201 }, { "spectre", 60 },
            { "spectral spellbinder", 50 }, { "stone gargoyle", 165 },
            { "stone harpy", 192 }, { "stone monster", 155 }, { "stone slith", 166 },
            { "stygian drake", 510 }, { "succubus", 353 }, { "swamp tentacle", 72 },
            { "tangling root", 246 }, { "terathan avenger", 372 },
            { "terathan drone", 39 }, { "terathan matriarch", 243 },
            { "terathan warrior", 129 }, { "timber wolf", 48 }, { "titan", 351 },
            { "tormented minotaur", 4200 }, { "toxic slith", 215 },
            { "trapdoor spider", 144 }, { "treefellow", 132 },
            { "treefellow guardian", 900 }, { "triceratops", 1200 },
            { "troglodyte", 340 }, { "troll", 123 }, { "tsuki wolf", 450 },
            { "undead gargoyle", 300 }, { "undead guardian", 138 },
            { "unfrozen mummy", 1500 }, { "valorite elemental", 153 },
            { "vampire bat", 66 }, { "verite elemental", 153 }, { "viscera", 230 },
            { "vorpal bunny", 2000 }, { "wailing banshee", 90 }, { "walrus", 17 },
            { "wanderer of the void", 400 }, { "water elemental", 165 },
            { "whipping vine", 200 }, { "white wolf", 48 }, { "white wyrm", 456 },
            { "wight", 250 }, { "wisp", 135 }, { "wolf spider", 160 },
            { "wraith", 60 }, { "wyvern", 141 }, { "yamandon", 1800 },
            { "yomotsu elder", 900 }, { "yomotsu priest", 530 },
            { "yomotsu warrior", 530 }, { "zombie", 42 },
            // ── champion / event bosses ────────────────────────────
            { "barracoon the piper", 12000 }, { "mephitis", 12000 }, { "rikktor", 15000 },
            { "semidar", 10000 }, { "neira the necromancer", 4800 }, { "lord oaks", 12000 },
            { "serado the awakened", 9000 }, { "harrower", 550 }, { "true harrower", 550 },
            { "dread horn", 50000 }, { "lady melisande", 100000 },
            { "chief paroxysmus", 50000 }, { "monstrous interred grizzle", 50000 },
            { "travesty", 35000 }, { "slasher of veils", 65000 },
            { "stygian dragon", 30000 }, { "crimson dragon", 25000 },
            { "abyssal infernal", 30000 }, { "primeval lich", 30000 },
            { "corgul the soulbinder", 65000 },
        };

        /// <summary>Looks up max HP by raw mob name; returns 0 if unknown.</summary>
        public static int Get(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return 0;
            string n = Normalize(rawName);
            if (n == null) return 0;
            return _byName.TryGetValue(n, out int hp) ? hp : 0;
        }

        /// <summary>Strips "(Paragon)" suffix and leading "a "/"an "/"the ".</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int paren = name.IndexOf("(Paragon", StringComparison.OrdinalIgnoreCase);
            if (paren > 0) name = name.Substring(0, paren).TrimEnd();
            name = name.Trim();
            if (name.Length == 0) return null;
            // Strip articles.
            if (name.StartsWith("a ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(2);
            else if (name.StartsWith("an ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(3);
            else if (name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) name = name.Substring(4);
            return name.ToLowerInvariant();
        }
    }
}

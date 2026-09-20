#region license
// TazUO addition. Central description/category/usage seed for chat commands
// surfaced by CommandPaletteGump (`-commands`). Adding metadata here doesn't
// touch the Register(...) call sites — unknown commands still appear in the
// gump under "Other".
#endregion

using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    public static class CommandMetadata
    {
        public sealed class Entry
        {
            public string Category;
            public string Description;
            public string Usage;
        }

        // Display order for the gump.
        public static readonly string[] CategoryOrder =
        {
            "Bandage", "Heal/Buff", "Combat", "Pets", "Loot",
            "UI", "Visual", "Movement", "Map/Markers",
            "Skills/Stats", "Alerts", "Debug/Tools", "Misc", "Other",
        };

        private static readonly Dictionary<string, Entry> _meta =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Bandage ---
            { "autobandage",  new Entry { Category="Bandage", Description="Auto-bandage player on low HP.", Usage="on|off|<pct>" } },
            { "bandage",      new Entry { Category="Bandage", Description="Use a bandage on yourself immediately.", Usage="" } },
            { "petbandage",   new Entry { Category="Bandage", Description="Auto-bandage pets within 2 tiles.", Usage="on|off|<pct>" } },
            { "extbandage",   new Entry { Category="Bandage", Description="Auto-bandage an external target (party/ally).", Usage="pick|clear|on|off" } },
            { "bandageopts",  new Entry { Category="Bandage", Description="Open the bandage options gump.", Usage="" } },
            { "bandagetimer", new Entry { Category="Bandage", Description="Toggle the on-screen bandage cooldown timer or set its duration.", Usage="on|off|<seconds>" } },

            // --- Heal/Buff ---
            { "autobuff",      new Entry { Category="Heal/Buff", Description="Auto-recast buffs when they expire.", Usage="on|off" } },
            { "healbutton",    new Entry { Category="Heal/Buff", Description="Toggle a quick heal button on the player floater.", Usage="on|off" } },
            { "emergencyheal", new Entry { Category="Heal/Buff", Description="Trigger the emergency heal macro.", Usage="" } },
            { "poisoncure",    new Entry { Category="Heal/Buff", Description="Auto-cure poison.", Usage="on|off" } },
            { "drink",         new Entry { Category="Heal/Buff", Description="Drink a potion by name.", Usage="<heal|cure|refresh|...>" } },
            { "autopot",       new Entry { Category="Heal/Buff", Description="Auto-drink potions on threshold.", Usage="on|off" } },
            { "finishlow",     new Entry { Category="Heal/Buff", Description="Finish low-HP enemies automatically.", Usage="on|off" } },
            { "fullhptoast",   new Entry { Category="Heal/Buff", Description="Toast when reaching full HP after combat.", Usage="on|off" } },
            { "lowhp",         new Entry { Category="Heal/Buff", Description="Low-HP screen tint toggle.", Usage="on|off|<pct>" } },
            { "healpulse",     new Entry { Category="Heal/Buff", Description="Green pulse ring on heal received.", Usage="on|off" } },

            // --- Combat ---
            { "attackenemy",   new Entry { Category="Combat", Description="Attack nearest hostile.", Usage="" } },
            { "autohit",       new Entry { Category="Combat", Description="Auto-attack current target.", Usage="on|off" } },
            { "lastenemy",     new Entry { Category="Combat", Description="Re-target last attacked enemy.", Usage="" } },
            { "targetenemy",   new Entry { Category="Combat", Description="Pick a new enemy target.", Usage="" } },
            { "smartcast",     new Entry { Category="Combat", Description="Smart-cast: auto-target last enemy after cast.", Usage="on|off" } },
            { "cast",          new Entry { Category="Combat", Description="Cast a spell by name.", Usage="<spell name>" } },
            { "hostileline",   new Entry { Category="Combat", Description="Draw a line to the nearest hostile.", Usage="on|off" } },
            { "arrowline",     new Entry { Category="Combat", Description="Draw a green line to tracking arrows.", Usage="on|off" } },
            { "targetingyou",  new Entry { Category="Combat", Description="Show an aura over mobs targeting you.", Usage="on|off" } },
            { "dmgsourceline", new Entry { Category="Combat", Description="Draw a line from incoming damage source.", Usage="on|off" } },
            { "dmgnum",        new Entry { Category="Combat", Description="Toggle floating damage numbers.", Usage="on|off" } },
            { "hptint",        new Entry { Category="Combat", Description="Tint mobs based on remaining HP%.", Usage="on|off" } },
            { "hitflash",      new Entry { Category="Combat", Description="Brief red flash on incoming hit.", Usage="on|off" } },
            { "lastdamage",    new Entry { Category="Combat", Description="Show last damage taken.", Usage="" } },
            { "reflectcount",  new Entry { Category="Combat", Description="Track magic reflection charges.", Usage="on|off" } },
            { "combatstate",   new Entry { Category="Combat", Description="Show in/out of combat indicator.", Usage="on|off" } },
            { "warborder",     new Entry { Category="Combat", Description="Red border around viewport in war mode.", Usage="on|off" } },

            // --- Pets ---
            { "petbar",     new Entry { Category="Pets", Description="Open pet command bar (Kill/Guard/Follow/...).", Usage="" } },
            { "petpanel",   new Entry { Category="Pets", Description="Open the pet status panel.", Usage="" } },
            { "petkill",    new Entry { Category="Pets", Description="Pet kill command.", Usage="" } },
            { "petguard",   new Entry { Category="Pets", Description="Pet guard command.", Usage="" } },
            { "petfollow",  new Entry { Category="Pets", Description="Pet follow command.", Usage="" } },
            { "petcome",    new Entry { Category="Pets", Description="Pet come command.", Usage="" } },
            { "petstay",    new Entry { Category="Pets", Description="Pet stay command.", Usage="" } },
            { "petstop",    new Entry { Category="Pets", Description="Pet stop command.", Usage="" } },
            { "pethp",      new Entry { Category="Pets", Description="Show pet HP bars overhead.", Usage="on|off" } },
            { "petguardtint", new Entry { Category="Pets", Description="Tint pets gray when they are not guarding you.", Usage="on|off" } },
            { "petloyalty", new Entry { Category="Pets", Description="Remind when nearby pet loyalty is below 90%.", Usage="on|off|status" } },
            { "petwatch",   new Entry { Category="Pets", Description="Watch a pet's HP and alert on low.", Usage="on|off" } },
            { "claim",      new Entry { Category="Pets", Description="Claim stabled pets.", Usage="" } },
            { "stable",     new Entry { Category="Pets", Description="Open nearest stable master.", Usage="" } },

            // --- Loot ---
            { "quickloot",       new Entry { Category="Loot", Description="Open nearest corpse + Take All.", Usage="" } },
            { "autoclosecorpse", new Entry { Category="Loot", Description="Auto-close empty corpses.", Usage="on|off" } },
            { "corpsefade",      new Entry { Category="Loot", Description="Fade looted corpses out.", Usage="on|off" } },
            { "loothistory",     new Entry { Category="Loot", Description="Show recent loot history.", Usage="" } },
            { "trash",           new Entry { Category="Loot", Description="Open/configure trash bag.", Usage="" } },
            { "hidetrash",       new Entry { Category="Loot", Description="Hide items matching trash filter.", Usage="on|off" } },

            // --- UI ---
            { "globalchat",     new Entry { Category="UI", Description="Open global chat.", Usage="" } },
            { "guildchat",      new Entry { Category="UI", Description="Open guild chat.", Usage="" } },
            { "speechhistory",  new Entry { Category="UI", Description="Open nearby speech history. Use Clear inside the gump to reset it.", Usage="" } },
            { "damagetracker",  new Entry { Category="UI", Description="Open damage tracker gump.", Usage="" } },
            { "options",        new Entry { Category="UI", Description="Open modern options gump.", Usage="" } },
            { "optlink",        new Entry { Category="UI", Description="Jump to a specific options page.", Usage="<name>" } },
            { "paperdoll",      new Entry { Category="UI", Description="Open paperdoll.", Usage="" } },
            { "worldexplorer",  new Entry { Category="Map/Markers", Description="Scan rune books and pin quick travel destinations.", Usage="" } },
            { "openjournal",    new Entry { Category="UI", Description="Open journal.", Usage="" } },
            { "toast",          new Entry { Category="UI", Description="Show a test toast.", Usage="<text>" } },
            { "toastanchor",    new Entry { Category="UI", Description="Move or resize the top-center toast stack; right-click to save.", Usage="" } },
            { "friendsfloater", new Entry { Category="UI", Description="Open friends list floater.", Usage="" } },
            { "partyhud",       new Entry { Category="UI", Description="Show or hide the compact party HUD.", Usage="on|off" } },
            { "compass",        new Entry { Category="UI", Description="Toggle compass overlay.", Usage="on|off" } },
            { "cdhud",          new Entry { Category="UI", Description="Cooldown HUD toggle.", Usage="on|off" } },
            { "perfhud",        new Entry { Category="UI", Description="Performance HUD (FPS/ping/GC).", Usage="on|off" } },
            { "pinghud",        new Entry { Category="UI", Description="Ping display HUD.", Usage="on|off" } },
            { "playerinfo",     new Entry { Category="UI", Description="Open player info floater.", Usage="" } },
            { "summary",        new Entry { Category="UI", Description="Show session summary.", Usage="" } },
            { "timerhud",       new Entry { Category="UI", Description="Generic countdown HUD.", Usage="on|off" } },
            { "compactbars",    new Entry { Category="UI", Description="Compact party/pet healthbars.", Usage="on|off" } },
            { "aggrobars",      new Entry { Category="UI", Description="Aggressor healthbars.", Usage="on|off" } },
            { "castbar",        new Entry { Category="UI", Description="Spell cast progress bar.", Usage="on|off" } },
            { "sb",             new Entry { Category="UI", Description="Open the Public Script Browser for shared Legion and Python scripts.", Usage="" } },
            { "spellbook",      new Entry { Category="UI", Description="Open spellbook.", Usage="" } },
            { "musicmode",      new Entry { Category="UI", Description="Choose original/new/mixed music, or scan Music/Digital for newly added MP3s.", Usage="original|new|mixed|rescan" } },
            { "musicplayer",    new Entry { Category="UI", Description="Open or close the auto-started music/radio player with playlist favorites; login starts in mini layout.", Usage="" } },
            { "gumptheme",      new Entry { Category="UI", Description="Theme supported utility, HUD, tracker, grid-container, journal and modern chat gumps.", Usage="[minimal|classic|runestone|oakandiron|dark|royal|forest|dungeon|water|snow|heartwoodsanctuary|termur|kotl|tazuo|britannia|trinsic|minoc|blackthorn|obsidian|doom|midnight|necro|ornate|chronicle|arcane|relic|mariner|gildedgrove|aetherglass|celestial|exodus|blood|hildebrandt|next]" } },
            { "gumpopacity",    new Entry { Category="UI", Description="Set a gump opacity option. All: 0-100 (utility/chat min 20; grid borders and hover unchanged).", Usage="<all|custom|durability|container|corpse|gridborder|journal|buff|slayer|hovermin> <percent> | <altscroll|hoverboost> [on|off|toggle]" } },
            { "gumpopacityall",        new Entry { Category="UI", Description="Set supported gump opacity values; utility/chat min 20, grid borders and hover unchanged.", Usage="<0-100>" } },
            { "gumpopacitycustom",     new Entry { Category="UI", Description="Gump opacity: themed utility and chat gumps.", Usage="<20-100>" } },
            { "gumpopacitydurability", new Entry { Category="UI", Description="Gump opacity: durability display.", Usage="<0-100>" } },
            { "gumpopacitycontainer",  new Entry { Category="UI", Description="Gump opacity: containers.", Usage="<0-100>" } },
            { "gumpopacitycorpse",     new Entry { Category="UI", Description="Gump opacity: corpse containers.", Usage="<0-100>" } },
            { "gumpopacitygridborder", new Entry { Category="UI", Description="Gump opacity: grid item borders.", Usage="<0-100>" } },
            { "gumpopacityjournal",    new Entry { Category="UI", Description="Gump opacity: journal.", Usage="<0-100>" } },
            { "gumpopacitybuff",       new Entry { Category="UI", Description="Gump opacity: buff bar.", Usage="<0-100>" } },
            { "gumpopacityslayer",     new Entry { Category="UI", Description="Gump opacity: paperdoll slayer bar.", Usage="<0-100>" } },
            { "gumpopacityhovermin",   new Entry { Category="UI", Description="Gump opacity: minimum while hovered.", Usage="<0-100>" } },
            { "gumpopacityaltscroll",  new Entry { Category="UI", Description="Gump opacity: adjust with Alt + scroll wheel.", Usage="on|off" } },
            { "gumpopacityhoverboost", new Entry { Category="UI", Description="Gump opacity: boost low-opacity gumps on hover.", Usage="on|off" } },
            { "colorpicker",    new Entry { Category="UI", Description="Open the client hue color picker.", Usage="" } },
            { "dressagent",     new Entry { Category="UI", Description="Dress or undress using a named Dress Agent configuration.", Usage="<dress|undress> \"<config name>\"" } },

            // --- Visual ---
            { "bodyhue",        new Entry { Category="Visual", Description="Recolor mob bodies by class.", Usage="on|off" } },
            { "bodyscale",      new Entry { Category="Visual", Description="Toggle HP/type-based mob scaling or manage a body-specific scale override.", Usage="on|off|status|<body-hex> <scale>|del <body-hex>" } },
            { "paragonglow",    new Entry { Category="Visual", Description="Hue paragon mobs red.", Usage="on|off" } },
            { "ghostfade",      new Entry { Category="Visual", Description="Fade ghost players to translucent.", Usage="on|off" } },
            { "mobblood",       new Entry { Category="Visual", Description="Blood splatter on mob hit.", Usage="on|off" } },
            { "mobhp",          new Entry { Category="Visual", Description="Show mob HP bars within a tile range; 0 disables them.", Usage="<range; 0=off>" } },
            { "fx",             new Entry { Category="Visual", Description="Toggle particle FX.", Usage="on|off" } },
            { "overheadsize",   new Entry { Category="Visual", Description="Set the shared size of confirmed spell and ability overhead effects.", Usage="small|normal|large|extralarge|status" } },
            { "rain",           new Entry { Category="Visual", Description="Rain overlay.", Usage="on|off" } },
            { "weather",        new Entry { Category="Visual", Description="Force local weather (client-side preview).", Usage="rain|snow|heavysnow|blizzard|storm|tempest|brewing|hail|sleet|fog|off [count]" } },
            { "shadowtest",     new Entry { Category="Visual", Description="Preview environmental shadow/day-night states locally.", Usage="day|dusk|moon|night|auto|<0..30>" } },
            { "seasontest",     new Entry { Category="Visual", Description="Preview seasonal terrain and foliage locally.", Usage="spring|summer|fall|winter|desolation|auto" } },
            { "envshowcase",    new Entry { Category="Visual", Description="Cycle shuffled 10-second season/weather scenes, each with a full day/night rotation.", Usage="on|off" } },
            { "envshowcasebeautiful", new Entry { Category="Visual", Description="Rotate through curated cinematic season, weather, light, ambience, and shadow scenes.", Usage="on|off" } },
            { "daycycle",       new Entry { Category="Visual", Description="Continuously preview day, dusk, night, dawn, ambience, and shadows.", Usage="on|off [30-3600 seconds]" } },
            { "ambientweather", new Entry { Category="Visual", Description="Random client-side weather every 8-25 min when the server sends none.", Usage="on|off" } },
            { "weathermotion",  new Entry { Category="Visual", Description="Toggle full weather camera motion or reduced motion.", Usage="on|off" } },
            { "ambience",       new Entry { Category="Visual", Description="Localized atmosphere, particles, wildlife, footprints, breath, mist, clouds, and lights without a full-screen tint.", Usage="on|off" } },
            { "ambientlights",  new Entry { Category="Visual", Description="Toggle client-added warm and colored ambient glows only.", Usage="on|off" } },
            { "waterenhancement", new Entry { Category="Visual", Description="Toggle enhanced water material without changing ambience.", Usage="on|off" } },
            { "wateratmosphere", new Entry { Category="Visual", Description="Toggle water-only cloud reflections, fog, glints, and weather whitecaps.", Usage="on|off" } },
            { "waterstyle",      new Entry { Category="Visual", Description="Select or cycle the enhanced water material.", Usage="natural|waves|choppy|swell|storm|moonlit" } },
            { "waterintensity",  new Entry { Category="Visual", Description="Query, reset, or set enhanced water material strength.", Usage="[0-200|reset]" } },
            { "materialintensity", new Entry { Category="Visual", Description="Query, reset, or set one/all enhanced terrain material strengths.", Usage="[all|sand|grass|mine|dungeon|dirt|snow 0-200]|reset" } },
            { "materialdebug", new Entry { Category="Visual", Description="Color-code enhanced terrain classification through the exact material mask.", Usage="on|off" } },
            { "environment",    new Entry { Category="Visual", Description="Open clickable weather, season, ambience, light and scenery controls.", Usage="" } },
            { "spelleffects",   new Entry { Category="Visual", Description="Open per-effect spell/ability toggles and local previews.", Usage="" } },
            { "gumpthemes",     new Entry { Category="Visual", Description="Open the visual selector for all shared custom gump themes.", Usage="" } },
            { "visualsilence",  new Entry { Category="Visual", Description="Hide original and custom spell/combat effect graphics without muting audio.", Usage="on|off|status" } },
            { "classiceffects",  new Entry { Category="Visual", Description="Hide TazUO combat enhancements while retaining original UO effects.", Usage="on|off|status" } },
            { "netherblaststyle", new Entry { Category="Visual", Description="Select the original Nether Blast art or one of six enhanced vortex styles.", Usage="off|1|2|3|4|5|6|status" } },
            { "sceneryquality", new Entry { Category="Visual", Description="Set scenery interaction and atmospheric particle density.", Usage="low|medium|high" } },
            { "wind",           new Entry { Category="Visual", Description="Wind particles.", Usage="on|off" } },
            { "tilegrid",       new Entry { Category="Visual", Description="Show a tile grid within a range; 0 disables it.", Usage="<range; 0=off>" } },
            { "radius",         new Entry { Category="Visual", Description="Show a player-centered radius with optional hue.", Usage="<distance> [hue]" } },
            { "range",          new Entry { Category="Visual", Description="Show a range marker; 0 disables it.", Usage="<0..60>" } },
            { "cursorhint",     new Entry { Category="Visual", Description="Cursor hint text.", Usage="on|off" } },
            { "cursordist",     new Entry { Category="Visual", Description="Show distance under cursor.", Usage="on|off" } },
            { "hostilebox",     new Entry { Category="Visual", Description="Box around hostile mobs.", Usage="on|off" } },
            { "targethighlight",new Entry { Category="Visual", Description="Highlight current target.", Usage="on|off" } },
            { "tgtcross",       new Entry { Category="Visual", Description="Crosshair on target cursor.", Usage="on|off" } },
            { "dmgtype",        new Entry { Category="Visual", Description="Color damage by type.", Usage="on|off" } },
            { "durfloater",     new Entry { Category="Visual", Description="Durability floater.", Usage="on|off" } },
            { "speechfade",     new Entry { Category="Visual", Description="Fade overhead speech faster.", Usage="on|off" } },
            { "notdot",         new Entry { Category="Visual", Description="Notoriety dot over mobs.", Usage="on|off" } },
            { "aggrotint",      new Entry { Category="Visual", Description="Tint mobs that have aggro on you.", Usage="off|gray|red" } },
            { "arrowglow",      new Entry { Category="Visual", Description="Glow around arrows / projectiles.", Usage="on|off" } },
            { "containerbadge", new Entry { Category="Visual", Description="Badge on container icons.", Usage="on|off" } },
            { "guildhue",       new Entry { Category="Visual", Description="Custom hue for guild members.", Usage="<hue>" } },

            // --- Movement ---
            { "mount",         new Entry { Category="Movement", Description="Mount nearest pet/mount.", Usage="" } },
            { "automount",     new Entry { Category="Movement", Description="Auto-remount after stun.", Usage="on|off" } },
            { "follow",        new Entry { Category="Movement", Description="Follow a target.", Usage="<name>" } },
            { "walkto",        new Entry { Category="Movement", Description="Pathfind to a tile.", Usage="<x> <y>" } },
            { "pathpreview",   new Entry { Category="Movement", Description="Preview pathfinder route.", Usage="on|off" } },
            { "pathcolor",     new Entry { Category="Movement", Description="Set path preview hue.", Usage="<hue>" } },
            { "dismounttilt",  new Entry { Category="Movement", Description="Tilt-pulse on dismount.", Usage="on|off" } },
            { "trail",         new Entry { Category="Movement", Description="Show recent move trail.", Usage="on|off" } },
            { "trailfx",       new Entry { Category="Movement", Description="Open surface-track, particle, and fantasy movement-trail controls.", Usage="" } },
            { "jump",          new Entry { Category="Movement", Description="Jump to coordinates (gm).", Usage="<x> <y>" } },
            { "recall",        new Entry { Category="Movement", Description="Recall to a marked rune.", Usage="<rune>" } },
            { "mark",          new Entry { Category="Movement", Description="Mark a rune.", Usage="" } },

            // --- Map/Markers ---
            { "addmarker",   new Entry { Category="Map/Markers", Description="Add a world map marker at cursor.", Usage="<name>" } },
            { "pastemarker", new Entry { Category="Map/Markers", Description="Paste marker from clipboard coords.", Usage="" } },
            { "marks",       new Entry { Category="Map/Markers", Description="List/manage world map markers.", Usage="" } },
            { "marktile",    new Entry { Category="Map/Markers", Description="Highlight a tile in-world.", Usage="" } },
            { "deathmarker", new Entry { Category="Map/Markers", Description="Drop a death marker on the map.", Usage="on|off" } },
            { "pin",         new Entry { Category="Map/Markers", Description="Pin current location.", Usage="" } },
            { "pins",        new Entry { Category="Map/Markers", Description="Show all pins.", Usage="" } },
            { "waypoint",    new Entry { Category="Map/Markers", Description="Set a waypoint to walk to.", Usage="" } },

            // --- Skills/Stats ---
            { "skill",       new Entry { Category="Skills/Stats", Description="Use a skill by name.", Usage="<skill>" } },
            { "skillcap",    new Entry { Category="Skills/Stats", Description="Show skill cap progress.", Usage="" } },
            { "skillgains",  new Entry { Category="Skills/Stats", Description="Open skill gain tracker.", Usage="" } },
            { "statalert",   new Entry { Category="Skills/Stats", Description="Alert on stat thresholds.", Usage="on|off" } },

            // --- Alerts ---
            { "alertsound",   new Entry { Category="Alerts", Description="Sound played on alerts.", Usage="<sfx#>" } },
            { "hungeralert",  new Entry { Category="Alerts", Description="Alert on hunger threshold.", Usage="on|off" } },
            { "weightalert",  new Entry { Category="Alerts", Description="Alert at a weight percentage; 0 disables it.", Usage="<percent; 0=off>" } },
            { "stamalert",    new Entry { Category="Alerts", Description="Alert below a stamina percentage; 0 disables it.", Usage="<percent; 0=off>" } },
            { "manaalert",    new Entry { Category="Alerts", Description="Alert below a mana percentage; 0 disables it.", Usage="<percent; 0=off>" } },
            { "partyalert",   new Entry { Category="Alerts", Description="Alert on party member low HP.", Usage="on|off" } },
            { "idlewarn",     new Entry { Category="Alerts", Description="Warn after the player remains idle.", Usage="<seconds; 0=off>" } },
            { "loiterwarn",   new Entry { Category="Alerts", Description="Warn when a hostile remains nearby.", Usage="off|<range> <seconds>" } },
            { "poisonalert",  new Entry { Category="Alerts", Description="Alert when poisoned.", Usage="on|off" } },
            { "pingwarn",     new Entry { Category="Alerts", Description="Warn above a ping threshold; 0 disables it.", Usage="<milliseconds; 0=off>" } },
            { "rangewarn",    new Entry { Category="Alerts", Description="Warn when the target exceeds a tile distance.", Usage="on|off|<distance>" } },
            { "reagentwatch", new Entry { Category="Alerts", Description="Low reagent toast.", Usage="on|off" } },
            { "buffexpire",   new Entry { Category="Alerts", Description="Toast on buff expiring.", Usage="on|off" } },
            { "hiddenwatch",  new Entry { Category="Alerts", Description="Alert when hidden status changes.", Usage="on|off" } },

            // --- Debug/Tools ---
            { "eventlog",    new Entry { Category="Debug/Tools", Description="Open event log.", Usage="" } },
            { "combatlog",   new Entry { Category="Debug/Tools", Description="Open combat log.", Usage="" } },
            { "deathlog",    new Entry { Category="Debug/Tools", Description="Open death log.", Usage="" } },
            { "deathrecap",  new Entry { Category="Debug/Tools", Description="Show last-death damage recap.", Usage="" } },
            { "history",     new Entry { Category="Debug/Tools", Description="Show command history.", Usage="" } },
            { "updateapi",   new Entry { Category="Debug/Tools", Description="Check for TazUO updates.", Usage="" } },
            { "updatedebug", new Entry { Category="Debug/Tools", Description="Toggle update-checker debug log.", Usage="on|off" } },
            { "version",     new Entry { Category="Debug/Tools", Description="Print client version.", Usage="" } },
            { "spawntimer",  new Entry { Category="Debug/Tools", Description="Track mob spawn timers.", Usage="on|off" } },
            { "animbrowser", new Entry { Category="Debug/Tools", Description="Open animation browser.", Usage="" } },
            { "artbrowser",  new Entry { Category="Debug/Tools", Description="Open art browser.", Usage="" } },
            { "findground",  new Entry { Category="Debug/Tools", Description="Find ground item by graphic.", Usage="<gfx>" } },
            { "help",        new Entry { Category="Debug/Tools", Description="List commands in chat.", Usage="[filter]" } },
            { "commands",    new Entry { Category="Debug/Tools", Description="Open the searchable command palette with purpose tooltips and persistent quick-command pins.", Usage="" } },
            { "info",        new Entry { Category="Debug/Tools", Description="Target a world object to inspect its client-side information.", Usage="" } },
            { "datetime",    new Entry { Category="Debug/Tools", Description="Print the current local date and time.", Usage="" } },
            { "hue",         new Entry { Category="Debug/Tools", Description="Target a world object to inspect its hue.", Usage="" } },
            { "debug",       new Entry { Category="Debug/Tools", Description="Toggle the client debug mode.", Usage="" } },
            { "playlscript", new Entry { Category="Debug/Tools", Description="Start a loaded Legion Script by filename.", Usage="<filename>" } },
            { "stoplscript", new Entry { Category="Debug/Tools", Description="Stop a running Legion Script by filename.", Usage="<filename>" } },
            { "togglelscript", new Entry { Category="Debug/Tools", Description="Start or stop a Legion Script by filename.", Usage="<filename>" } },

            // --- Misc ---
            { "paste",         new Entry { Category="Misc", Description="Paste clipboard to chat.", Usage="" } },
            { "snippet",       new Entry { Category="Misc", Description="Insert a saved chat snippet.", Usage="<name>" } },
            { "note",          new Entry { Category="Misc", Description="Add a personal note.", Usage="<text>" } },
            { "keyword",       new Entry { Category="Misc", Description="Watch journal for keyword.", Usage="<word>" } },
            { "mute",          new Entry { Category="Misc", Description="Mute a player.", Usage="<name>" } },
            { "focusmute",     new Entry { Category="Misc", Description="Mute when window unfocused.", Usage="on|off" } },
            { "autothanks",    new Entry { Category="Misc", Description="Auto-thank on heals received.", Usage="on|off" } },
            { "afk",           new Entry { Category="Misc", Description="Toggle AFK state.", Usage="on|off" } },
            { "autorespawn",   new Entry { Category="Misc", Description="Auto-resurrect at nearest healer.", Usage="on|off" } },
            { "autostealth",   new Entry { Category="Misc", Description="Auto-use stealth when hidden.", Usage="on|off" } },
            { "autostop",      new Entry { Category="Misc", Description="Stop auto-actions.", Usage="" } },
            { "autopack",      new Entry { Category="Misc", Description="Auto-organize backpack.", Usage="on|off" } },
            { "autopaper",     new Entry { Category="Misc", Description="Auto-open paperdoll on login.", Usage="on|off" } },
            { "autorearm",     new Entry { Category="Misc", Description="Auto-rearm after disarm.", Usage="on|off" } },
            { "count",         new Entry { Category="Misc", Description="Count items in pack.", Usage="<gfx>" } },
            { "countall",      new Entry { Category="Misc", Description="Count all items in pack.", Usage="" } },
            { "countdown",     new Entry { Category="Misc", Description="Start countdown timer.", Usage="<sec> [name]" } },
            { "countdowns",    new Entry { Category="Misc", Description="List active countdowns.", Usage="" } },
            { "alias",         new Entry { Category="Misc", Description="Define a command alias.", Usage="<name> <cmd>" } },
            { "friend",        new Entry { Category="Misc", Description="Add a friend.", Usage="<name>" } },
            { "notorietywatch",new Entry { Category="Misc", Description="Watch notoriety changes.", Usage="on|off" } },
            { "onlineplayers", new Entry { Category="Misc", Description="List online players (shard).", Usage="" } },
            { "profilecopy",   new Entry { Category="Misc", Description="Copy settings from another character.", Usage="<char>" } },
            { "profileio",     new Entry { Category="Misc", Description="Export/import profile.", Usage="" } },
            { "loadout",       new Entry { Category="Misc", Description="Save/load equipment loadouts.", Usage="" } },
            { "organize",      new Entry { Category="Misc", Description="Run all organizers, or one organizer by index or name.", Usage="[index|name]" } },
            { "organizer",     new Entry { Category="Misc", Description="Run all organizers, or one organizer by index or name.", Usage="[index|name]" } },
            { "organizerlist", new Entry { Category="Misc", Description="List the configured Organizer Agent entries.", Usage="" } },
            { "buyvendor",     new Entry { Category="Misc", Description="Auto-buy from vendor shopping list.", Usage="" } },
            { "sellvendor",    new Entry { Category="Misc", Description="Auto-sell vendor list.", Usage="" } },
            { "vendorclose",   new Entry { Category="Misc", Description="Close vendor gump.", Usage="" } },
            { "itemtoclip",    new Entry { Category="Misc", Description="Copy item info to clipboard.", Usage="" } },
            { "itemdropsound", new Entry { Category="Misc", Description="Play sound on item drop.", Usage="on|off" } },
            { "pintooltip",    new Entry { Category="Misc", Description="Pin a tooltip on screen.", Usage="" } },
            { "ruler",         new Entry { Category="Misc", Description="Measure distance between tiles.", Usage="" } },
            { "tilelift",      new Entry { Category="Misc", Description="Lift one tile above for view.", Usage="" } },
            { "setinscreen",   new Entry { Category="Misc", Description="Recenter off-screen gumps.", Usage="" } },
            { "use",           new Entry { Category="Misc", Description="Use/double-click item by alias.", Usage="<alias>" } },
            { "rec",           new Entry { Category="Misc", Description="Recast last spell.", Usage="[lasttarget]" } },
            { "genspelldef",   new Entry { Category="Misc", Description="Generate spell definitions file.", Usage="" } },
            { "lastmsg",       new Entry { Category="Misc", Description="Replay last journal message.", Usage="" } },
            { "offscreenarrow",new Entry { Category="Misc", Description="Arrow indicator for off-screen mobs.", Usage="on|off" } },
            { "wparrow",       new Entry { Category="Misc", Description="Waypoint arrow toggle.", Usage="on|off" } },
            { "partycycle",    new Entry { Category="Misc", Description="Cycle to the next party member.", Usage="" } },
            { "automation",    new Entry { Category="Misc", Description="Enable or pause all automatic gameplay actions.", Usage="on|off" } },
            { "nativechat",    new Entry { Category="Misc", Description="Replace recognized server global-chat gumps.", Usage="on|off" } },
            { "repairauto",    new Entry { Category="Misc", Description="Enable recognized repair-bench auto-selection.", Usage="on|off" } },
            { "diagnostics",   new Entry { Category="Debug/Tools", Description="Show feature failures or re-enable a failed feature.", Usage="[reenable <name>]" } },
            { "profilerecovery", new Entry { Category="Debug/Tools", Description="Restore a recent profile and gump-layout snapshot after restart.", Usage="" } },
        };

        private static readonly HashSet<string> _builtInNames =
            new HashSet<string>(_meta.Keys, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _hiddenFromPalette =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "autobuff", "autopot", "drink", "finishlow", "attackenemy",
                "autohit", "cast", "lastenemy", "smartcast", "targetenemy",
                "hidetrash", "dismounttilt", "jump", "mark", "recall",
                "debug", "diagnostics", "eventlog", "findground",
                "gumpopacity"
            };

        private static readonly Dictionary<string, bool> _runtimeStates =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static int StateVersion { get; private set; }

        public static bool IsVisibleInPalette(string name)
        {
            return !_hiddenFromPalette.Contains(name ?? string.Empty);
        }

        public static bool? GetRuntimeState(string name)
        {
            return _runtimeStates.TryGetValue(name ?? string.Empty, out bool value)
                ? value
                : (bool?)null;
        }

        public static void RecordExecution(string name, string[] args)
        {
            Entry entry = Get(name);
            string usage = entry?.Usage ?? string.Empty;

            if (usage.IndexOf("on", StringComparison.OrdinalIgnoreCase) < 0 ||
                usage.IndexOf("off", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            bool? state = null;
            if (args != null && args.Length > 1)
            {
                if (string.Equals(args[1], "on", StringComparison.OrdinalIgnoreCase)) state = true;
                else if (string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase)) state = false;
            }
            else if (_runtimeStates.TryGetValue(name, out bool previous))
            {
                state = !previous;
            }

            if (state.HasValue &&
                (!_runtimeStates.TryGetValue(name, out bool current) || current != state.Value))
            {
                _runtimeStates[name] = state.Value;
                StateVersion++;
            }
        }

        public static Entry Get(string name)
        {
            return _meta.TryGetValue(name, out var e) ? e : null;
        }

        public static void Synchronize(IEnumerable<string> commandNames)
        {
            var live = new HashSet<string>(commandNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            // Runtime aliases and assistant-provided commands may disappear between
            // profiles. Never remove the prepared built-in descriptions when the
            // registry is briefly empty during scene/profile initialization.
            var stale = new List<string>();
            foreach (string name in _meta.Keys)
            {
                if (!_builtInNames.Contains(name) && !live.Contains(name)) stale.Add(name);
            }
            foreach (string name in stale) _meta.Remove(name);

            foreach (string name in live)
            {
                if (CommandAliasManager.All.TryGetValue(name, out string target) &&
                    _meta.TryGetValue(target, out Entry targetEntry))
                {
                    _meta[name] = new Entry
                    {
                        Category = targetEntry.Category,
                        Description = $"Alias for -{target}: {targetEntry.Description}",
                        Usage = targetEntry.Usage
                    };
                    continue;
                }

                if (!_meta.ContainsKey(name))
                {
                    _meta[name] = new Entry
                    {
                        Category = "Other",
                        Description = $"Custom command registered as -{name}.",
                        Usage = string.Empty
                    };
                }
            }
        }
    }
}

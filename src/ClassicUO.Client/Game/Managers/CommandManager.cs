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

using ClassicUO.Configuration;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Resources;
using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ClassicUO.Game.UI.Gumps.SpellBar;
using ClassicUO.LegionScripting;

namespace ClassicUO.Game.Managers
{
    public static class CommandManager
    {
        private static readonly Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();

        public static Dictionary<string, Action<string[]>> Commands { get { return _commands; } }

        public static void Initialize()
        {
            // Start collecting chat histories so they're there even if the gumps
            // haven't been opened yet.
            UI.Gumps.GlobalChatHistory.EnsureHooked();
            UI.Gumps.GuildChatHistory.EnsureHooked();
            UI.Gumps.NearbySpeechHistory.EnsureHooked();
            SkillCapTracker.EnsureHooked();
            DamageTypeTagManager.Hook();
            ReflectCounterManager.Hook();
            UI.HealReceivedPulse.Hook();
            UI.OnslaughtDeliveryEffect.EnsureHooked();
            UI.ConsecrateWeaponEffect.EnsureHooked();
            UI.DivineFuryEffect.EnsureHooked();
            UI.SacredJourneyEffect.EnsureHooked();
            UI.AbilityOverheadEffect.EnsureHooked();
            UI.LegendaryCreatureAlert.EnsureHooked();
            EnhancedSpellVisualTrigger.EnsureHooked();
            AfkReplyManager.EnsureHooked();
            LastSpeechManager.EnsureHooked();
            JournalKeywordToastManager.EnsureHooked();
            StartupBannerManager.EnsureHooked();
            // Default-on overlays need their event subscriptions wired now.
            UI.MoveTrailOverlay.EnsureHooked();
            UI.EffectsBundle.EnsureHooked();
            PartyInviteAlertManager.EnsureHooked();
            BodyScaleManager.EnsureOplHooked();
            AutoSayThanksManager.EnsureHooked();

#if ENABLE_LEGION_SCRIPTING
            Register("sb", (s)=>UIManager.Add(new ScriptBrowser()));
#endif

            Register("damagetracker", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.DamageTrackerGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new UI.Gumps.DamageTrackerGump(100, 100));
            });

            Register("globalchat", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.GlobalChatGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.CloseByUser();
                    return;
                }
                UI.Gumps.GlobalChatGump.OpenByUser(200, 200);
            });

            Register("guildchat", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.GuildChatGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.CloseByUser();
                    return;
                }
                UI.Gumps.GuildChatGump.OpenByUser(220, 220);
            });

            Register("nearbychat", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.NearbySpeechGump>();

                if (s != null && s.Length >= 2 && s[1].Trim().Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    if (existing != null && !existing.IsDisposed)
                        existing.ClearHistory();
                    else
                        UI.Gumps.NearbySpeechHistory.Instance.Clear();

                    GameActions.Print("Nearby speech history cleared.", 0x35);
                    return;
                }

                if (existing != null && !existing.IsDisposed)
                {
                    existing.CloseByUser();
                    return;
                }

                UI.Gumps.NearbySpeechGump.OpenByUser(240, 240);
            });
            Register("speechhistory", _commands["nearbychat"]);

            Register("targetenemy", (s) => TargetOrAttackNearestEnemy(false));
            Register("attackenemy", (s) => TargetOrAttackNearestEnemy(true));
            Register("quickloot", (s) => { QuickLootManager.EnsureHooked(); QuickLootManager.Trigger(); });

            Register("musicmode", (s) =>
            {
                Profile profile = ProfileManager.CurrentProfile;

                if (profile == null)
                {
                    GameActions.Print("Not in game.", 0x21);
                    return;
                }

                if (s == null || s.Length < 2)
                {
                    string current = profile.MusicSelectionMode == 0
                        ? "original"
                        : profile.MusicSelectionMode == 2 ? "mixed" : "new";
                    GameActions.Print($"Music mode: {current}.", 0x35);
                    GameActions.Print($"Custom MP3s found: {SoundsLoader.Instance.CustomMusicIndices.Count}.", 0x35);
                    GameActions.Print("Usage: -musicmode original|new|mixed|rescan", 0x35);
                    return;
                }

                string requested = s[1].Trim().ToLowerInvariant();

                if (requested == "rescan" || requested == "scan")
                {
                    int added = SoundsLoader.Instance.RescanCustomMusic();
                    GameActions.Print($"Music scan: {SoundsLoader.Instance.LastMusicScanFileCount} MP3s found, {added} added, {SoundsLoader.Instance.CustomMusicIndices.Count} custom tracks total.", 0x35);
                    GameActions.Print($"Scanned: {SoundsLoader.Instance.MusicDirectories}", 0x35);

                    if (added > 0)
                    {
                        Client.Game.Audio.RefreshMusicSelection();
                    }

                    return;
                }

                byte mode;

                if (requested == "original" || requested == "old")
                {
                    mode = 0;
                }
                else if (requested == "new" || requested == "newonly")
                {
                    mode = 1;
                }
                else if (requested == "mixed" || requested == "mix")
                {
                    mode = 2;
                }
                else
                {
                    GameActions.Print("Usage: -musicmode original|new|mixed|rescan", 0x21);
                    return;
                }

                profile.MusicSelectionMode = mode;
                profile.Save(ProfileManager.ProfilePath, false);
                Client.Game.Audio.RefreshMusicSelection();

                string selected = mode == 0 ? "ORIGINAL" : mode == 1 ? "NEW ONLY" : "MIXED";
                GameActions.Print($"Music mode: {selected}.", 0x35);
            });

            Register("musicplayer", (s) =>
            {
                MusicPlayerGump existing = UIManager.GetGump<MusicPlayerGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }

                UIManager.Add(new MusicPlayerGump());
            });

            Register("worldexplorer", (s) =>
            {
                WorldExplorerGump existing = UIManager.GetGump<WorldExplorerGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new WorldExplorerGump());
            });

            Register("itemfinder", (s) =>
            {
                string query = s != null && s.Length > 1
                    ? string.Join(" ", s.Skip(1))
                    : string.Empty;
                ItemFinderGump existing = UIManager.GetGump<ItemFinderGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        existing.FocusSearch();
                    }
                    else
                    {
                        existing.SetQuery(query);
                    }
                    return;
                }

                UIManager.Add(new ItemFinderGump(query));
            });

            Register("equipmentguru", (s) =>
            {
                EquipmentGuruGump existing = UIManager.GetGump<EquipmentGuruGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    existing.BringOnTop();
                    return;
                }

                UIManager.Add(new EquipmentGuruGump());
            });

            Register("restock", (s) =>
            {
                if (s != null && s.Length > 1
                    && s[1].Equals("run", StringComparison.OrdinalIgnoreCase))
                {
                    RestockAgentManager.Run();
                    return;
                }

                RestockAgentGump existing = UIManager.GetGump<RestockAgentGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }

                UIManager.Add(new RestockAgentGump());
            });

            Register("readycheck", (s) =>
            {
                RestockAgentGump existing = UIManager.GetGump<RestockAgentGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    existing.CheckReadiness(true);
                    return;
                }

                UIManager.Add(new RestockAgentGump(true));
            });

            Register("alertcenter", (s) =>
            {
                AlertCenterGump existing = UIManager.GetGump<AlertCenterGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }

                UIManager.Add(new AlertCenterGump());
            });
            Register("alerts", _commands["alertcenter"]);

            Register("gumptheme", (s) =>
            {
                Profile profile = ProfileManager.CurrentProfile;

                if (profile == null)
                {
                    GameActions.Print("Not in game.", 0x21);
                    return;
                }

                if (s == null || s.Length < 2)
                {
                    GameActions.Print($"Gump theme: {GumpThemeSelectorGump.DisplayName(CustomGumpThemeManager.Current)}.", 0x35);
                    GameActions.Print("Usage: -gumptheme minimal|classic|runestone|oakandiron|dark|royal|forest|dungeon|water|snow|heartwoodsanctuary|termur|kotl|tazuo|britannia|trinsic|minoc|blackthorn|obsidian|doom|midnight|necro|ornate|chronicle|arcane|relic|mariner|gildedgrove|aetherglass|celestial|exodus|blood|hildebrandt|next", 0x35);
                    return;
                }

                CustomGumpTheme theme;
                string requested = s[1].Trim().ToLowerInvariant();

                if (requested == "next" || requested == "cycle")
                {
                    theme = (CustomGumpTheme)(((int)CustomGumpThemeManager.Current + 1) % CustomGumpThemeManager.ThemeCount);
                }
                else if (!CustomGumpThemeManager.TryParse(requested, out theme))
                {
                    GameActions.Print("Usage: -gumptheme minimal|classic|runestone|oakandiron|dark|royal|forest|dungeon|water|snow|heartwoodsanctuary|termur|kotl|tazuo|britannia|trinsic|minoc|blackthorn|obsidian|doom|midnight|necro|ornate|chronicle|arcane|relic|mariner|gildedgrove|aetherglass|celestial|exodus|blood|hildebrandt|next", 0x21);
                    return;
                }

                CustomGumpThemeManager.SetTheme(theme);
                GameActions.Print($"Gump theme: {GumpThemeSelectorGump.DisplayName(theme)}.", 0x35);
            });

            Register("gumpthemes", (s) =>
            {
                GumpThemeSelectorGump existing =
                    UIManager.GetGump<GumpThemeSelectorGump>();

                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }

                UIManager.Add(new GumpThemeSelectorGump());
            });

            Register("gumpopacity", SetGumpOpacity);
            Register("gumpopacityall", s => SetGumpOpacityOption("all", s, 1));
            Register("gumpopacitycustom", s => SetGumpOpacityOption("custom", s, 1));
            Register("gumpopacitypaperdoll", s => SetGumpOpacityOption("paperdoll", s, 1));
            Register("gumpopacitydurability", s => SetGumpOpacityOption("durability", s, 1));
            Register("gumpopacitycontainer", s => SetGumpOpacityOption("container", s, 1));
            Register("gumpopacitycorpse", s => SetGumpOpacityOption("corpse", s, 1));
            Register("gumpopacitygridborder", s => SetGumpOpacityOption("gridborder", s, 1));
            Register("gumpopacityjournal", s => SetGumpOpacityOption("journal", s, 1));
            Register("gumpopacitybuff", s => SetGumpOpacityOption("buff", s, 1));
            Register("gumpopacityslayer", s => SetGumpOpacityOption("slayer", s, 1));
            Register("gumpopacityhovermin", s => SetGumpOpacityOption("hovermin", s, 1));
            Register("gumpopacityaltscroll", s => SetGumpOpacityOption("altscroll", s, 1));
            Register("gumpopacityhoverboost", s => SetGumpOpacityOption("hoverboost", s, 1));

            Register("profilecopy", (s) =>
            {
                if (string.IsNullOrEmpty(ProfileManager.ProfilePath))
                {
                    GameActions.Print("Not in game.", 0x21);
                    return;
                }

                // Profile layout: {root}/{user}/{server}/{character}/profile.json
                // Walk up one level to enumerate sibling characters.
                var charDir = new System.IO.DirectoryInfo(ProfileManager.ProfilePath);
                var serverDir = charDir.Parent;
                if (serverDir == null || !serverDir.Exists)
                {
                    GameActions.Print("Cannot locate server folder.", 0x21);
                    return;
                }

                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -profilecopy <other-character>", 0x21);
                    GameActions.Print("Available siblings:", 0x35);
                    foreach (var d in serverDir.GetDirectories())
                        if (!string.Equals(d.Name, charDir.Name, System.StringComparison.OrdinalIgnoreCase))
                            GameActions.Print(" - " + d.Name, 0x35);
                    return;
                }

                string sourceName = s[1];
                var sourceDir = new System.IO.DirectoryInfo(System.IO.Path.Combine(serverDir.FullName, sourceName));
                if (!sourceDir.Exists)
                {
                    GameActions.Print($"No profile for character '{sourceName}'.", 0x21);
                    return;
                }
                string sourceProfile = System.IO.Path.Combine(sourceDir.FullName, "profile.json");
                if (!System.IO.File.Exists(sourceProfile))
                {
                    GameActions.Print($"Character '{sourceName}' has no profile.json.", 0x21);
                    return;
                }

                // Backup current first (best-effort).
                string current = System.IO.Path.Combine(ProfileManager.ProfilePath, "profile.json");
                if (System.IO.File.Exists(current))
                {
                    try { System.IO.File.Copy(current, current + ".presync.bak", overwrite: true); } catch { }
                }
                try
                {
                    if (!ProfileDataStore.WriteAllText("profile.json", System.IO.File.ReadAllText(sourceProfile)))
                        throw new System.IO.IOException("Atomic profile replacement failed.");
                    GameActions.Print($"Profile copied from '{sourceName}'. Restart client to activate.", 0x35);
                }
                catch (System.Exception ex)
                {
                    GameActions.Print("Copy failed: " + ex.Message, 0x21);
                }
            });

            Register("itemtoclip", async (s) =>
            {
                GameActions.Print("Target an item to copy its OPL...", 0x35);
                await TargetHelper.TargetObject((ent) =>
                {
                    if (!(ent is Game.GameObjects.Item item))
                    {
                        GameActions.Print("Not an item.", 0x21);
                        return;
                    }
                    string name, data;
                    if (!World.OPL.TryGetNameAndData(item.Serial, out name, out data))
                    {
                        GameActions.Print("No OPL data available.", 0x21);
                        return;
                    }
                    string text = (string.IsNullOrEmpty(name) ? "" : name + "\n") + (data ?? "");
                    SDL3.SDL.SDL_SetClipboardText(text);
                    GameActions.Print("OPL copied to clipboard.", 0x35);
                });
            });

            Register("addmarker", (s) =>
            {
                if (s == null || s.Length < 3)
                {
                    GameActions.Print("Usage: -addmarker <x> <y> [name]", 0x21);
                    return;
                }
                if (!int.TryParse(s[1], out int mx)) { GameActions.Print("Bad X.", 0x21); return; }
                if (!int.TryParse(s[2], out int my)) { GameActions.Print("Bad Y.", 0x21); return; }
                string name = s.Length >= 4 ? string.Join(" ", s, 3, s.Length - 3) : $"Marker {mx},{my}";
                var map = World.MapIndex;
                var gump = UIManager.GetGump<UI.Gumps.WorldMapGump>();
                if (gump == null)
                {
                    gump = new UI.Gumps.WorldMapGump();
                    UIManager.Add(gump);
                }
                gump.AddUserMarker(name, mx, my, map);
                GameActions.Print($"Marker '{name}' added at ({mx},{my}) on map {map}.", 0x35);
            });

            Register("pastemarker", (s) =>
            {
                string clip = Utility.StringHelper.GetClipboardText(false);
                if (string.IsNullOrWhiteSpace(clip))
                {
                    GameActions.Print("Clipboard empty.", 0x21);
                    return;
                }
                // Match patterns: "x,y", "x:1234 y:1234", "(1234, 1234)", "1234 1234"
                var m = System.Text.RegularExpressions.Regex.Match(clip,
                    @"(?:x[:=]?\s*)?(-?\d+)[,\s]+(?:y[:=]?\s*)?(-?\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success)
                {
                    GameActions.Print("No coords found in clipboard.", 0x21);
                    return;
                }
                int mx = int.Parse(m.Groups[1].Value);
                int my = int.Parse(m.Groups[2].Value);
                var map = World.MapIndex;
                var gump = UIManager.GetGump<UI.Gumps.WorldMapGump>();
                if (gump == null)
                {
                    gump = new UI.Gumps.WorldMapGump();
                    UIManager.Add(gump);
                }
                gump.AddUserMarker($"Pasted {mx},{my}", mx, my, map);
                GameActions.Print($"Marker added at ({mx},{my}) on map {map}.", 0x35);
            });

            Register("toast", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -toast <text>", 0x21); return; }
                string text = string.Join(" ", s, 1, s.Length - 1);
                UI.Gumps.ToastManager.Show(text, 0x0481, 4000, "manual-toast",
                    AlertCategory.General, AlertSeverity.Info);
            });

            Register("petwatch", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    PetWatcherManager.SetEnabled(!PetWatcherManager.Enabled);
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") PetWatcherManager.SetEnabled(true);
                else if (a == "off") PetWatcherManager.SetEnabled(false);
                else GameActions.Print("Usage: -petwatch [on|off]", 0x21);
            });

            Register("autorespawn", (s) =>
            {
                bool alsoAttack = s != null && s.Length >= 2 &&
                                  string.Equals(s[1], "attack", System.StringComparison.OrdinalIgnoreCase);
                AutoRespawnTargetManager.SetEnabled(!AutoRespawnTargetManager.Enabled, alsoAttack);
            });

            Register("pintooltip", (s) => UI.Gumps.StickyTooltipGump.PinTargeted());

            Register("loothistory", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.LootHistoryGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new UI.Gumps.LootHistoryGump());
            });

            Register("friend", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -friend add|del|list <name>", 0x21); return; }
                string verb = s[1].ToLowerInvariant();
                if (verb == "list")
                {
                    var all = FriendsListManager.Instance.GetFriends();
                    if (all.Count == 0) { GameActions.Print("No friends.", 0x35); return; }
                    foreach (var f in all) GameActions.Print(" - " + f.Name, 0x35);
                    return;
                }
                if (s.Length < 3) { GameActions.Print("Need a name.", 0x21); return; }
                string name = string.Join(" ", s, 2, s.Length - 2);
                if (verb == "add")
                {
                    if (FriendsListManager.Instance.AddFriend(0, name))
                        GameActions.Print($"Added friend '{name}'.", 0x35);
                    else GameActions.Print("Could not add (already exists or invalid name).", 0x21);
                }
                else if (verb == "del" || verb == "delete")
                {
                    if (FriendsListManager.Instance.RemoveFriend(name)) GameActions.Print($"Removed '{name}'.", 0x35);
                    else GameActions.Print("Not found.", 0x21);
                }
                else GameActions.Print("Unknown verb.", 0x21);
            });

            Register("friendsfloater", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.FriendsFloaterGump>();
                if (ex != null && !ex.IsDisposed) { ex.Dispose(); return; }
                UIManager.Add(new UI.Gumps.FriendsFloaterGump());
            });

            Register("durfloater", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.DurabilityFloaterGump>();
                if (ex != null && !ex.IsDisposed) { ex.Dispose(); return; }
                UIManager.Add(new UI.Gumps.DurabilityFloaterGump());
            });

            Register("lastdamage", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.LastDamageFloaterGump>();
                if (ex != null && !ex.IsDisposed) { ex.Dispose(); return; }
                UIManager.Add(new UI.Gumps.LastDamageFloaterGump());
            });

            Register("spawntimer", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -spawntimer add <name> <seconds> | -spawntimer del <name> | -spawntimer list | -spawntimer clear", 0x21); return; }
                string verb = s[1].ToLowerInvariant();
                if (verb == "list")
                {
                    if (SpawnTimerManager.All.Count == 0) { GameActions.Print("No active timers.", 0x35); return; }
                    foreach (var t in SpawnTimerManager.All)
                    {
                        double rem = (t.ExpireAt - System.DateTime.Now).TotalSeconds;
                        GameActions.Print($"  {t.Name}: {rem:0.0}s {(t.Fired ? "[done]" : "")}", 0x35);
                    }
                    return;
                }
                if (verb == "clear") { SpawnTimerManager.Clear(); GameActions.Print("Timers cleared.", 0x35); return; }
                if (s.Length < 3) { GameActions.Print("Need a name.", 0x21); return; }
                string name = s[2];
                if (verb == "add")
                {
                    if (s.Length < 4 || !int.TryParse(s[3], out int sec)) { GameActions.Print("Usage: -spawntimer add <name> <seconds>", 0x21); return; }
                    SpawnTimerManager.Add(name, sec);
                    GameActions.Print($"Timer '{name}' set for {sec}s.", 0x35);
                }
                else if (verb == "del" || verb == "delete")
                {
                    if (SpawnTimerManager.Remove(name)) GameActions.Print($"Removed '{name}'.", 0x35);
                    else GameActions.Print("Not found.", 0x21);
                }
                else GameActions.Print("Unknown verb.", 0x21);
            });

            Register("note", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -note add <text> | -note del <#> | -note list", 0x21); return; }
                string verb = s[1].ToLowerInvariant();
                if (verb == "list")
                {
                    NotesManager.EnsureLoaded();
                    if (NotesManager.All.Count == 0) { GameActions.Print("No notes.", 0x35); return; }
                    for (int i = 0; i < NotesManager.All.Count; i++)
                        GameActions.Print($"  [{i}] {NotesManager.All[i]}", 0x35);
                    return;
                }
                if (verb == "add")
                {
                    if (s.Length < 3) { GameActions.Print("Need text.", 0x21); return; }
                    string text = string.Join(" ", s, 2, s.Length - 2);
                    NotesManager.Add(text);
                    GameActions.Print("Note added.", 0x35);
                }
                else if (verb == "del" || verb == "delete")
                {
                    if (s.Length < 3 || !int.TryParse(s[2], out int idx)) { GameActions.Print("Usage: -note del <#>", 0x21); return; }
                    GameActions.Print(NotesManager.DeleteAt(idx) ? $"Deleted note #{idx}." : "Index out of range.",
                        (ushort)(NotesManager.DeleteAt(idx) ? 0x35 : 0x21));
                }
                else GameActions.Print("Unknown verb.", 0x21);
            });

            Register("targethighlight", (s) =>
            {
                UI.LastTargetHighlight.Enabled = !UI.LastTargetHighlight.Enabled;
                GameActions.Print($"Target highlight {(UI.LastTargetHighlight.Enabled ? "ON" : "OFF")}.",
                    (ushort)(UI.LastTargetHighlight.Enabled ? 0x35 : 0x21));
            });

            Register("drink", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -drink <heal|cure|refresh|agility|strength|mana|nightsight|explosion>", 0x21);
                    return;
                }
                QuickDrinkManager.Drink(s[1].Trim());
            });

            Register("autostealth", (s) =>
            {
                if (s == null || s.Length < 2) { AutoStealthManager.SetEnabled(!AutoStealthManager.Enabled); return; }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") AutoStealthManager.SetEnabled(true);
                else if (a == "off") AutoStealthManager.SetEnabled(false);
                else GameActions.Print("Usage: -autostealth [on|off]", 0x21);
            });

            Register("count", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -count <graphic-hex|dec> [hue]", 0x21);
                    return;
                }
                if (!TryParseUshort(s[1], out ushort graphic)) { GameActions.Print("Bad graphic.", 0x21); return; }
                ushort hue = 0;
                if (s.Length >= 3 && !TryParseUshort(s[2], out hue)) hue = 0;
                int total = CountInWornBags(graphic, hue);
                GameActions.Print($"Count 0x{graphic:X4}" + (hue != 0 ? $" hue {hue}" : "") + $": {total}", 0x35);
            });

            Register("rangewarn", (s) =>
            {
                if (s == null || s.Length < 2) { TargetRangeWarnManager.SetEnabled(!TargetRangeWarnManager.Enabled); return; }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") TargetRangeWarnManager.SetEnabled(true);
                else if (a == "off") TargetRangeWarnManager.SetEnabled(false);
                else if (int.TryParse(a, out int d)) { TargetRangeWarnManager.SetDistance(d); TargetRangeWarnManager.SetEnabled(true); }
                else GameActions.Print("Usage: -rangewarn [on|off|<distance>]", 0x21);
            });

            Register("findground", (s) =>
            {
                if (s == null || s.Length < 2) { UI.GroundLootFinder.Range = 0; GameActions.Print("Ground loot finder OFF.", 0x21); return; }
                if (!int.TryParse(s[1], out int r) || r < 0 || r > 30) { GameActions.Print("Usage: -findground <0..30>", 0x21); return; }
                UI.GroundLootFinder.Range = r;
                GameActions.Print(r == 0 ? "Ground loot finder OFF." : $"Ground loot finder = {r} tiles.", (ushort)(r == 0 ? 0x21 : 0x35));
            });

            Register("snippet", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -snippet save <name> <text>  |  -snippet send <name>  |  -snippet del <name>  |  -snippet list", 0x21);
                    return;
                }
                string verb = s[1].ToLowerInvariant();
                if (verb == "list")
                {
                    SnippetManager.EnsureLoaded();
                    if (SnippetManager.Entries.Count == 0) { GameActions.Print("No snippets.", 0x35); return; }
                    foreach (var kv in SnippetManager.Entries) GameActions.Print($"  {kv.Key}: {kv.Value}", 0x35);
                    return;
                }
                if (s.Length < 3) { GameActions.Print("Usage: -snippet <verb> <name> [...]", 0x21); return; }
                string name = s[2];
                if (verb == "save")
                {
                    if (s.Length < 4) { GameActions.Print("Need text.", 0x21); return; }
                    string text = string.Join(" ", s, 3, s.Length - 3);
                    SnippetManager.Set(name, text);
                    GameActions.Print($"Snippet '{name}' saved.", 0x35);
                }
                else if (verb == "send")
                {
                    if (SnippetManager.TryGet(name, out string t))
                        GameActions.Say(t, ProfileManager.CurrentProfile?.SpeechHue ?? (ushort)0x35);
                    else GameActions.Print("No such snippet.", 0x21);
                }
                else if (verb == "del" || verb == "delete")
                {
                    GameActions.Print(SnippetManager.Delete(name) ? $"Deleted '{name}'." : "No such snippet.",
                        (ushort)(SnippetManager.Delete(name) ? 0x35 : 0x21));
                }
                else GameActions.Print("Unknown verb.", 0x21);
            });

            Register("combatlog", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.CombatLogGump>();
                if (ex != null && !ex.IsDisposed) { ex.Dispose(); return; }
                UIManager.Add(new UI.Gumps.CombatLogGump());
            });

            Register("onlineplayers", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.OnlinePlayersGump>();
                if (ex != null && !ex.IsDisposed) { ex.Dispose(); return; }
                UIManager.Add(new UI.Gumps.OnlinePlayersGump());
            });

            Register("deathmarker", (s) =>
            {
                if (s == null || s.Length < 2) { DeathMarkerManager.SetEnabled(!DeathMarkerManager.Enabled); return; }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") DeathMarkerManager.SetEnabled(true);
                else if (a == "off") DeathMarkerManager.SetEnabled(false);
                else GameActions.Print("Usage: -deathmarker [on|off]", 0x21);
            });

            Register("cursordist", (s) =>
            {
                UI.CursorDistanceOverlay.Enabled = !UI.CursorDistanceOverlay.Enabled;
                GameActions.Print($"Cursor distance {(UI.CursorDistanceOverlay.Enabled ? "ON" : "OFF")}.",
                    (ushort)(UI.CursorDistanceOverlay.Enabled ? 0x35 : 0x21));
            });

            Register("playerinfo", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.PlayerInfoFloater>();
                if (ex != null && !ex.IsDisposed) { ex.Dispose(); return; }
                UIManager.Add(new UI.Gumps.PlayerInfoFloater(320, 60));
            });

            Register("healbutton", (s) =>
            {
                var ex = UIManager.GetGump<UI.Gumps.HealSelfButtonGump>();
                bool on = s == null || s.Length < 2
                    ? ex == null || ex.IsDisposed
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                if (!on)
                {
                    if (ex != null && !ex.IsDisposed) ex.Dispose();
                    return;
                }
                if (ex == null || ex.IsDisposed) UIManager.Add(new UI.Gumps.HealSelfButtonGump(360, 60));
            });

            Register("alertsound", (s) =>
            {
                AggroIndicatorManager.PlayAlertSound = !AggroIndicatorManager.PlayAlertSound;
                GameActions.Print($"Aggressor alert sound {(AggroIndicatorManager.PlayAlertSound ? "ON" : "OFF")}.",
                    (ushort)(AggroIndicatorManager.PlayAlertSound ? 0x35 : 0x21));
            });

            Register("range", (s) =>
            {
                if (s == null || s.Length < 2) { UI.RangeIndicator.Range = 0; GameActions.Print("Range overlay OFF.", 0x21); return; }
                if (!int.TryParse(s[1], out int r) || r < 0 || r > 60) { GameActions.Print("Usage: -range <0..60>", 0x21); return; }
                UI.RangeIndicator.Range = r;
                GameActions.Print(r == 0 ? "Range overlay OFF." : $"Range overlay = {r} tiles.", (ushort)(r == 0 ? 0x21 : 0x35));
            });

            Register("afk", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    AfkReplyManager.SetEnabled(!AfkReplyManager.Enabled);
                    return;
                }
                string a = s[1].Trim();
                if (string.Equals(a, "on", System.StringComparison.OrdinalIgnoreCase))
                {
                    string msg = s.Length >= 3 ? string.Join(" ", s, 2, s.Length - 2) : null;
                    AfkReplyManager.SetEnabled(true, msg);
                }
                else if (string.Equals(a, "off", System.StringComparison.OrdinalIgnoreCase))
                    AfkReplyManager.SetEnabled(false);
                else
                {
                    string msg = string.Join(" ", s, 1, s.Length - 1);
                    AfkReplyManager.SetEnabled(true, msg);
                }
            });

            Register("skillcap", (s) =>
            {
                if (s == null || s.Length < 2) { SkillCapTracker.SetEnabled(!SkillCapTracker.Enabled); return; }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") SkillCapTracker.SetEnabled(true);
                else if (a == "off") SkillCapTracker.SetEnabled(false);
                else GameActions.Print("Usage: -skillcap [on|off]", 0x21);
            });

            Register("waypoint", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -waypoint <rec|stop|play|cancel>", 0x21);
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "rec" || a == "record") WaypointRecorder.StartRecording();
                else if (a == "stop") WaypointRecorder.StopRecording();
                else if (a == "play") WaypointRecorder.Play();
                else if (a == "cancel") WaypointRecorder.Stop();
                else GameActions.Print("Unknown subcommand.", 0x21);
            });

            Register("smartcast", (s) =>
            {
                var p = ProfileManager.CurrentProfile;
                if (p == null) return;
                p.SmartCastAutoTarget = !p.SmartCastAutoTarget;
                GameActions.Print($"SmartCast auto-target {(p.SmartCastAutoTarget ? "ON" : "OFF")}.",
                    (ushort)(p.SmartCastAutoTarget ? 0x35 : 0x21));
            });

            Register("ruler", async (s) =>
            {
                GameActions.Print("Ruler: target first object...", 0x35);
                int ax = 0, ay = 0; bool gotA = false;
                await TargetHelper.TargetObject((ent) =>
                {
                    if (ent != null) { ax = ent.X; ay = ent.Y; gotA = true; }
                });
                if (!gotA) return;
                GameActions.Print("Ruler: target second object...", 0x35);
                await TargetHelper.TargetObject((ent) =>
                {
                    if (ent == null) return;
                    int dx = System.Math.Abs(ent.X - ax);
                    int dy = System.Math.Abs(ent.Y - ay);
                    int cheb = System.Math.Max(dx, dy);          // UO movement distance
                    double euclid = System.Math.Sqrt(dx * dx + dy * dy);
                    GameActions.Print($"Ruler: tile-dist={cheb}, Δx={dx}, Δy={dy}, line={euclid:0.0}", 0x35);
                });
            });

            Register("guildhue", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -guildhue <substring> <hue>  |  -guildhue list  |  -guildhue clear <substring>", 0x21);
                    return;
                }
                if (string.Equals(s[1], "list", System.StringComparison.OrdinalIgnoreCase))
                {
                    GuildHueMap.EnsureLoaded();
                    if (GuildHueMap.Entries.Count == 0)
                    {
                        GameActions.Print("No guild hue overrides.", 0x35);
                        return;
                    }
                    foreach (var kv in GuildHueMap.Entries)
                        GameActions.Print($"  {kv.Key} -> 0x{kv.Value:X4}", 0x35);
                    return;
                }
                if (s.Length >= 3 && string.Equals(s[1], "clear", System.StringComparison.OrdinalIgnoreCase))
                {
                    GuildHueMap.Remove(s[2]);
                    GameActions.Print($"Cleared override for '{s[2]}'.", 0x35);
                    return;
                }
                if (s.Length < 3)
                {
                    GameActions.Print("Usage: -guildhue <substring> <hue>", 0x21);
                    return;
                }
                if (!ushort.TryParse(s[2], out ushort hueVal) &&
                    !(s[2].StartsWith("0x", System.StringComparison.OrdinalIgnoreCase) &&
                      ushort.TryParse(s[2].Substring(2), System.Globalization.NumberStyles.HexNumber,
                                       System.Globalization.CultureInfo.InvariantCulture, out hueVal)))
                {
                    GameActions.Print("Bad hue value.", 0x21);
                    return;
                }
                GuildHueMap.Set(s[1], hueVal);
                GameActions.Print($"Guild substring '{s[1]}' will tint to 0x{hueVal:X4}.", 0x35);
            });

            Register("dmgnum", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -dmgnum <hue> [size]  (hue 0 = default)", 0x21);
                    return;
                }
                if (!ushort.TryParse(s[1], out ushort h)) { GameActions.Print("Bad hue.", 0x21); return; }
                int size = 0;
                if (s.Length >= 3) int.TryParse(s[2], out size);
                if (size < 0) size = 0;
                if (size > 48) size = 48;
                var p = ProfileManager.CurrentProfile;
                if (p != null)
                {
                    p.DamageNumberHue = h;
                    p.DamageNumberSize = size;
                    GameActions.Print($"Damage number hue=0x{h:X4} size={size}.", 0x35);
                }
            });

            Register("pathcolor", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -pathcolor <hue-hex-or-dec>", 0x21);
                    return;
                }
                string arg = s[1].Trim();
                ushort hue;
                if (arg.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!ushort.TryParse(arg.Substring(2),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out hue))
                    {
                        GameActions.Print("Bad hex hue.", 0x21);
                        return;
                    }
                }
                else if (!ushort.TryParse(arg, out hue))
                {
                    GameActions.Print("Bad hue.", 0x21);
                    return;
                }
                UI.PathPreview.Hue = hue;
                GameActions.Print($"PathPreview hue = 0x{hue:X4} ({hue}).", 0x35);
            });

            Register("reagentwatch", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    ReagentWatcherManager.SetEnabled(!ReagentWatcherManager.Enabled);
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") ReagentWatcherManager.SetEnabled(true);
                else if (a == "off") ReagentWatcherManager.SetEnabled(false);
                else if (int.TryParse(a, out int t))
                {
                    ReagentWatcherManager.SetThreshold(t);
                    ReagentWatcherManager.SetEnabled(true);
                }
                else GameActions.Print("Usage: -reagentwatch [on|off|<threshold>]", 0x21);
            });

            Register("autobandage", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    AutoBandageManager.SetEnabled(!AutoBandageManager.Enabled);
                    return;
                }
                string arg = s[1].Trim().ToLowerInvariant();
                if (arg == "on") AutoBandageManager.SetEnabled(true);
                else if (arg == "off") AutoBandageManager.SetEnabled(false);
                else if (int.TryParse(arg, out int pct)) { AutoBandageManager.SetThreshold(pct); AutoBandageManager.SetEnabled(true); }
                else GameActions.Print("Usage: -autobandage [on|off|<percent>]", 0x21);
            });

            Register("extbandage", (s) =>
            {
                string arg = (s == null || s.Length < 2) ? "" : s[1].Trim().ToLowerInvariant();
                switch (arg)
                {
                    case "pick": ExternalBandageManager.PickTarget(); break;
                    case "clear": ExternalBandageManager.Clear(); break;
                    case "on": ExternalBandageManager.SetEnabled(true); break;
                    case "off": ExternalBandageManager.SetEnabled(false); break;
                    default:
                        if (int.TryParse(arg, out int pct)) { ExternalBandageManager.SetThreshold(pct); ExternalBandageManager.SetEnabled(true); }
                        else GameActions.Print("Usage: -extbandage pick|clear|on|off|<pct>", 0x21);
                        break;
                }
            });

            Register("toastanchor", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.ToastAnchorGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    GameActions.Print("Toast anchor position saved.", 0x35);
                    return;
                }
                UIManager.Add(new UI.Gumps.ToastAnchorGump());
                GameActions.Print("Drag the bar to move toasts or its right edge to resize. Right-click or re-run -toastanchor to save.", 0x35);
            });

            Register("bandageopts", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.BandageOptionsGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
                UIManager.Add(new UI.Gumps.BandageOptionsGump());
            });

            Register("petbandage", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    PetBandageManager.SetEnabled(!PetBandageManager.Enabled);
                    return;
                }
                string arg = s[1].Trim().ToLowerInvariant();
                if (arg == "on") PetBandageManager.SetEnabled(true);
                else if (arg == "off") PetBandageManager.SetEnabled(false);
                else if (int.TryParse(arg, out int pct)) { PetBandageManager.SetThreshold(pct); PetBandageManager.SetEnabled(true); }
                else GameActions.Print("Usage: -petbandage [on|off|<percent>]", 0x21);
            });

            Register("options", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.ModernOptionsGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
                UIManager.Add(new UI.Gumps.ModernOptionsGump());
            });

            Register("perfhud", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.PerfHudGump>();
                bool on = s == null || s.Length < 2
                    ? existing == null || existing.IsDisposed
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                if (!on)
                {
                    if (existing != null && !existing.IsDisposed) existing.Dispose();
                    return;
                }
                if (existing == null || existing.IsDisposed) UIManager.Add(new UI.Gumps.PerfHudGump(120, 80));
            });

            Register("profilerecovery", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.ProfileRecoveryGump>();
                if (existing != null && !existing.IsDisposed) { existing.BringOnTop(); return; }
                UIManager.Add(new UI.Gumps.ProfileRecoveryGump());
            });

            Register("skillgains", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.SkillGainTrackerGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
                UIManager.Add(new UI.Gumps.SkillGainTrackerGump(200, 200));
            });

            Register("petpanel", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.PetStatusPanelGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new UI.Gumps.PetStatusPanelGump(260, 260));
            });

Register("pathpreview", (s) =>
            {
                if (s == null || s.Length < 2 || string.IsNullOrWhiteSpace(s[1]))
                {
                    UI.PathPreview.Clear();
                    GameActions.Print("Path preview cleared.", 0x35);
                    return;
                }
                // Format: -pathpreview x1,y1,z1;x2,y2,z2;...
                string raw = string.Join(" ", s, 1, s.Length - 1);
                var tiles = new System.Collections.Generic.List<(int x, int y, int z)>();
                foreach (var part in raw.Split(';'))
                {
                    var bits = part.Split(',');
                    if (bits.Length < 2) continue;
                    if (!int.TryParse(bits[0].Trim(), out int x)) continue;
                    if (!int.TryParse(bits[1].Trim(), out int y)) continue;
                    int z = 0;
                    if (bits.Length >= 3) int.TryParse(bits[2].Trim(), out z);
                    tiles.Add((x, y, z));
                }
                if (tiles.Count == 0)
                {
                    GameActions.Print("Usage: -pathpreview x1,y1,z1;x2,y2,z2;...", 0x21);
                    return;
                }
                UI.PathPreview.Show(tiles);
                GameActions.Print($"Path preview: {tiles.Count} tile(s).", 0x35);
            });

            Register("profileio", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.ProfileExportImportGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new UI.Gumps.ProfileExportImportGump());
            });

#if ENABLE_LEGION_SCRIPTING
            Register("updateapi", (s) =>
            {
                LegionScripting.LegionScripting.DownloadAPIPy();
            });
#endif

            Register
            (
                "info",
                s =>
                {
                    if (TargetManager.IsTargeting)
                    {
                        TargetManager.CancelTarget();
                    }

                    TargetManager.SetTargeting(CursorTarget.SetTargetClientSide, CursorType.Target, TargetType.Neutral);
                }
            );

            Register
            (
                "datetime",
                s =>
                {
                    if (World.Player != null)
                    {
                        GameActions.Print(string.Format(ResGeneral.CurrentDateTimeNowIs0, DateTime.Now));
                    }
                }
            );

            Register
            (
                "hue",
                s =>
                {
                    if (TargetManager.IsTargeting)
                    {
                        TargetManager.CancelTarget();
                    }

                    TargetManager.SetTargeting(CursorTarget.HueCommandTarget, CursorType.Target, TargetType.Neutral);
                }
            );


            Register
            (
                "debug",
                s =>
                {
                    CUOEnviroment.Debug = !CUOEnviroment.Debug;

                }
            );

            Register
            (
                "colorpicker",
                s =>
                {
                    UIManager.Add(new UI.Gumps.ModernColorPicker(null, 8787));

                }
            );

            List<Skill> sortSkills = new List<Skill>(World.Player.Skills);

            Register("skill", s =>
            {
                string skill = "";
                for (int i = 1; i < s.Length; i++)
                {
                    skill += s[i] + " ";
                }
                skill = skill.Trim().ToLower();

                if (skill.Length > 0)
                {
                    for (int i = 0; i < World.Player.Skills.Length; i++)
                    {
                        if (World.Player.Skills[i].Name.ToLower().Contains(skill))
                        {
                            GameActions.UseSkill(World.Player.Skills[i].Index);
                            break;
                        }
                    }
                }
            });

            Register("version", s => { UIManager.Add(new VersionHistory()); });
            Register("rain", s =>
            {
                AmbientWeatherManager.CancelForExternalWeather();
                Client.Game.GetScene<ClassicUO.Game.Scenes.GameScene>()?.Weather.Generate(
                    WeatherType.WT_RAIN,
                    30,
                    75,
                    WeatherSource.Manual
                );
            });

            Register("marktile", s =>
            {
                if (s.Length > 1 && s[1] == "-r")
                {
                    if (s.Length == 2)
                    {
                        TileMarkerManager.Instance.RemoveTile(World.Player.X, World.Player.Y, World.Map.Index);
                    }
                    else if (s.Length == 4)
                    {
                        if (int.TryParse(s[2], out var x))
                            if (int.TryParse(s[3], out var y))
                                TileMarkerManager.Instance.RemoveTile(x, y, World.Map.Index);
                    }
                    else if (s.Length == 5)
                    {
                        if (int.TryParse(s[2], out var x))
                            if (int.TryParse(s[3], out var y))
                                if (int.TryParse(s[4], out var m))
                                    TileMarkerManager.Instance.RemoveTile(x, y, m);
                    }
                }
                else
                {
                    if (s.Length == 1)
                    {
                        TileMarkerManager.Instance.AddTile(World.Player.X, World.Player.Y, World.Map.Index, 32);
                    }
                    else if (s.Length == 2)
                    {
                        if (ushort.TryParse(s[1], out ushort h))
                            TileMarkerManager.Instance.AddTile(World.Player.X, World.Player.Y, World.Map.Index, h);
                    }
                    else if (s.Length == 4)
                    {
                        if (int.TryParse(s[1], out var x))
                            if (int.TryParse(s[2], out var y))
                                if (ushort.TryParse(s[3], out var h))
                                    TileMarkerManager.Instance.AddTile(x, y, World.Map.Index, h);
                    }
                    else if (s.Length == 5)
                    {
                        if (int.TryParse(s[1], out var x))
                            if (int.TryParse(s[2], out var y))
                                if (int.TryParse(s[3], out var m))
                                    if (ushort.TryParse(s[4], out var h))
                                        TileMarkerManager.Instance.AddTile(x, y, m, h);
                    }
                }
            });

            Register("radius", s =>
            {
                ///-radius distance hue
                if (s.Length == 1)
                    ProfileManager.CurrentProfile.DisplayRadius ^= true;
                if (s.Length > 1)
                {
                    if (int.TryParse(s[1], out var dist))
                        ProfileManager.CurrentProfile.DisplayRadiusDistance = dist;
                    ProfileManager.CurrentProfile.DisplayRadius = true;
                }
                if (s.Length > 2)
                    if (ushort.TryParse(s[2], out var h))
                        ProfileManager.CurrentProfile.DisplayRadiusHue = h;
            });

            Register("paperdoll", (s) =>
            {
                if (ProfileManager.CurrentProfile.UseModernPaperdoll)
                {
                    UIManager.Add(new PaperDollGump(World.Player, true));
                }
                else
                {
                    UIManager.Add(new ModernPaperdoll(World.Player));
                }

            });

            Register("optlink", (s) =>
            {
                ModernOptionsGump g = UIManager.GetGump<ModernOptionsGump>();
                if (s.Length > 1)
                {
                    if (g != null)
                    {
                        g.GoToPage(s[1]);
                    }
                    else
                    {
                        UIManager.Add(g = new ModernOptionsGump());
                        g.GoToPage(s[1]);
                    }
                }
                else
                {
                    if (g != null)
                    {
                        GameActions.Print(g.GetPageString());
                    }
                }
            });

            Register("genspelldef", (s) =>
            {
                Task.Run(SpellDefinition.SaveAllSpellsToJson);
            });

            Register("setinscreen", (s) =>
            {
                for (LinkedListNode<Gump> last = UIManager.Gumps.Last; last != null; last = last.Previous)
                {
                    Gump c = last.Value;

                    if (!c.IsDisposed)
                    {
                        c.SetInScreen();
                    }
                }
            });

            Register("updatedebug", (s) =>
            {
                UIManager.Add(new UI.Gumps.UpdateTimerViewer());
            });

            Register("artbrowser", (s) => { UIManager.Add(new ArtBrowserGump()); });

            Register("animbrowser", (s) => { UIManager.Add(new AnimBrowser()); });

            Register("autohit", (s) =>
            {
                AutoHitListManager.EnsureLoaded();
                if (s == null || s.Length < 2)
                {
                    AutoHitListManager.Enabled = !AutoHitListManager.Enabled;
                    GameActions.Print($"AutoHit {(AutoHitListManager.Enabled ? "ON" : "OFF")} ({AutoHitListManager.Patterns.Count} patterns, attack={AutoHitListManager.AlsoAttack}).",
                        (ushort)(AutoHitListManager.Enabled ? 0x35 : 0x21));
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") { AutoHitListManager.Enabled = true; GameActions.Print("AutoHit ON.", 0x35); }
                else if (a == "off") { AutoHitListManager.Enabled = false; GameActions.Print("AutoHit OFF.", 0x21); }
                else if (a == "attackmode" && s.Length >= 3)
                {
                    AutoHitListManager.AlsoAttack = s[2].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                    GameActions.Print($"AutoHit auto-attack {(AutoHitListManager.AlsoAttack ? "ON" : "OFF")}.", 0x35);
                }
                else if (a == "add" && s.Length >= 3)
                {
                    string p = string.Join(" ", s, 2, s.Length - 2);
                    AutoHitListManager.AddPattern(p);
                    GameActions.Print($"AutoHit + '{p}'.", 0x35);
                }
                else if (a == "del" && s.Length >= 3)
                {
                    string p = string.Join(" ", s, 2, s.Length - 2);
                    AutoHitListManager.RemovePattern(p);
                    GameActions.Print($"AutoHit - '{p}'.", 0x21);
                }
                else if (a == "list")
                {
                    foreach (var p in AutoHitListManager.Patterns)
                        GameActions.Print(" - " + p, 0x44);
                }
                else GameActions.Print("Usage: -autohit on|off|add <pat>|del <pat>|list|attackmode on|off", 0x21);
            });

            Register("itemdropsound", (s) =>
            {
                if (s != null && s.Length >= 3 && s[1].Trim().Equals("range", System.StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(s[2], out int r))
                {
                    ItemDropSoundManager.Range = System.Math.Max(1, r);
                    GameActions.Print($"Item drop sound range = {ItemDropSoundManager.Range}.", 0x35);
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !ItemDropSoundManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                ItemDropSoundManager.SetEnabled(on);
            });

            Register("hidetrash", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    UI.HideTrashOverlay.Range = UI.HideTrashOverlay.Range > 0 ? 0 : 8;
                    GameActions.Print($"Hide-trash overlay {(UI.HideTrashOverlay.Range > 0 ? $"ON (range {UI.HideTrashOverlay.Range})" : "OFF")}.",
                        (ushort)(UI.HideTrashOverlay.Range > 0 ? 0x35 : 0x21));
                    return;
                }
                if (int.TryParse(s[1], out int r))
                {
                    UI.HideTrashOverlay.Range = System.Math.Max(0, r);
                    GameActions.Print($"Hide-trash overlay {(UI.HideTrashOverlay.Range > 0 ? $"ON (range {UI.HideTrashOverlay.Range})" : "OFF")}.",
                        (ushort)(UI.HideTrashOverlay.Range > 0 ? 0x35 : 0x21));
                }
                else GameActions.Print("Usage: -hidetrash <range>  (0=off)", 0x21);
            });

            Register("petguardtint", (s) =>
            {
                if (s != null && s.Length >= 2)
                {
                    string action = s[1].Trim();
                    if (action.Equals("on", System.StringComparison.OrdinalIgnoreCase))
                        PetGuardTintManager.SetEnabled(true);
                    else if (action.Equals("off", System.StringComparison.OrdinalIgnoreCase))
                        PetGuardTintManager.SetEnabled(false);
                    else
                        GameActions.Print("Usage: -petguardtint on|off", 0x21);
                    return;
                }
                PetGuardTintManager.SetEnabled(!PetGuardTintManager.Enabled);
            });

            Register("petloyalty", (s) =>
            {
                if (s != null && s.Length >= 2)
                {
                    string action = s[1].Trim();
                    if (action.Equals("status", System.StringComparison.OrdinalIgnoreCase))
                        PetLoyaltyAlertManager.PrintStatus();
                    else if (action.Equals("on", System.StringComparison.OrdinalIgnoreCase))
                        PetLoyaltyAlertManager.SetEnabled(true);
                    else if (action.Equals("off", System.StringComparison.OrdinalIgnoreCase))
                        PetLoyaltyAlertManager.SetEnabled(false);
                    else
                        GameActions.Print("Usage: -petloyalty on|off|status", 0x21);
                    return;
                }
                PetLoyaltyAlertManager.SetEnabled(!PetLoyaltyAlertManager.Enabled);
            });

            Register("mount", (s) => { MountToggleManager.Toggle(); });

            Register("arrowglow", (s) =>
            {
                if (s != null && s.Length >= 2)
                {
                    string a = s[1].Trim().ToLowerInvariant();
                    if (a == "hue" && s.Length >= 3 && ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort h))
                    { ArrowGlowManager.GlowHue = h; GameActions.Print($"Arrow glow hue = 0x{h:X}.", 0x35); return; }
                    if (a == "add" && s.Length >= 3 && ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort g1))
                    { ArrowGlowManager.ArrowGraphics.Add(g1); GameActions.Print($"Added arrow graphic 0x{g1:X4}.", 0x35); return; }
                    if (a == "del" && s.Length >= 3 && ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort g2))
                    { ArrowGlowManager.ArrowGraphics.Remove(g2); GameActions.Print($"Removed arrow graphic 0x{g2:X4}.", 0x21); return; }
                    if (a == "list")
                    {
                        foreach (var g in ArrowGlowManager.ArrowGraphics)
                            GameActions.Print($"  0x{g:X4}", 0x44);
                        return;
                    }
                    if (a == "debug")
                    {
                        ArrowGlowManager.Debug = !ArrowGlowManager.Debug;
                        GameActions.Print($"Arrow-glow debug {(ArrowGlowManager.Debug ? "ON" : "OFF")} — fire a shot and watch journal for the actual graphic id.",
                            (ushort)(ArrowGlowManager.Debug ? 0x35 : 0x21));
                        return;
                    }
                }
                bool on = s == null || s.Length < 2
                    ? !ArrowGlowManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                ArrowGlowManager.SetEnabled(on);
            });

            Register("partyhud", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.CompactPartyHud.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CompactPartyHud.Enabled = on;
                GameActions.Print($"Party HUD {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("autostop", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AutoStopOnDeathManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoStopOnDeathManager.SetEnabled(on);
            });

            Register("history", (s) =>
            {
                int n = 10;
                if (s != null && s.Length >= 2 && int.TryParse(s[1], out int parsed))
                {
                    // Either re-execute by index OR print last N. Re-exec when >= 1 and <= CAPACITY.
                    string line = CommandHistoryManager.GetByIndex(parsed);
                    if (line != null)
                    {
                        var bits = line.TrimStart('-').Split(' ');
                        Execute(bits[0], bits);
                        return;
                    }
                    n = parsed;
                }
                CommandHistoryManager.Print(System.Math.Max(1, n));
            });

            Register("stable", (s) => { StableMacroManager.Stable(); });
            Register("claim", (s) => { StableMacroManager.Claim(); });

            Register("autothanks", (s) =>
            {
                if (s != null && s.Length >= 3 && s[1].Trim().Equals("phrase", System.StringComparison.OrdinalIgnoreCase))
                {
                    AutoSayThanksManager.Phrase = string.Join(" ", s, 2, s.Length - 2);
                    GameActions.Print($"Auto-thanks phrase = '{AutoSayThanksManager.Phrase}'.", 0x35);
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !AutoSayThanksManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoSayThanksManager.SetEnabled(on);
            });

            Register("aggrobars", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AggroIndicatorManager.DrawEnabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AggroIndicatorManager.DrawEnabled = on;
                GameActions.Print($"Aggressor markers {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("wparrow", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -wparrow set <x> <y> | clear", 0x21); return; }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "clear") { UI.WaypointArrowOverlay.Clear(); return; }
                if (a == "set" && s.Length >= 4 && int.TryParse(s[2], out int xx) && int.TryParse(s[3], out int yy))
                { UI.WaypointArrowOverlay.Set(xx, yy); return; }
                GameActions.Print("Usage: -wparrow set <x> <y> | clear", 0x21);
            });

            Register("bodyhue", (s) =>
            {
                BodyHueManager.EnsureLoaded();
                if (s == null || s.Length < 2)
                {
                    foreach (var kv in BodyHueManager.All)
                        GameActions.Print($"  0x{kv.Key:X} = 0x{kv.Value:X}", 0x44);
                    GameActions.Print("Usage: -bodyhue <body-hex> <hue-hex> | del <body-hex> | paragon <hue-hex>", 0x35);
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "del")
                {
                    if (s.Length < 3 || !ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort b)) { GameActions.Print("Bad body id.", 0x21); return; }
                    BodyHueManager.Remove(b);
                    return;
                }
                if (a == "paragon")
                {
                    if (s.Length < 3 || !ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort h)) { GameActions.Print("Bad hue.", 0x21); return; }
                    BodyHueManager.ParagonAutoHue = h;
                    GameActions.Print($"Paragon auto-hue = 0x{h:X} (0 clears).", 0x35);
                    return;
                }
                if (a == "debug")
                {
                    BodyHueManager.Debug = !BodyHueManager.Debug;
                    GameActions.Print($"BodyHue debug {(BodyHueManager.Debug ? "ON" : "OFF")} — toast fires once per paragon name when the override applies.",
                        (ushort)(BodyHueManager.Debug ? 0x35 : 0x21));
                    return;
                }
                if (s.Length < 3 || !ushort.TryParse(s[1], System.Globalization.NumberStyles.HexNumber, null, out ushort body))
                { GameActions.Print("Bad body id.", 0x21); return; }
                if (!ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort hue))
                { GameActions.Print("Bad hue.", 0x21); return; }
                BodyHueManager.Set(body, hue);
            });

            Register("mobblood", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.MobBloodOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.MobBloodOverlay.SetEnabled(on);
            });

            Register("ghostfade", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.GhostFadeOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.GhostFadeOverlay.SetEnabled(on);
            });

            Register("targetingyou", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.TargetingYouAura.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.TargetingYouAura.SetEnabled(on);
            });

            Register("dmgsourceline", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.DamageSourceLineOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.DamageSourceLineOverlay.SetEnabled(on);
            });

            Register("dmgtype", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !DamageTypeTagManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                DamageTypeTagManager.SetEnabled(on);
            });

            Register("dismounttilt", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.DismountTiltOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.DismountTiltOverlay.SetEnabled(on);
            });

            Register("reflectcount", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("reset", System.StringComparison.OrdinalIgnoreCase))
                { ReflectCounterManager.Reset(); return; }
                bool on = s == null || s.Length < 2
                    ? !ReflectCounterManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                ReflectCounterManager.SetEnabled(on);
            });

            Register("tilelift", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.TileHoverLiftOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.TileHoverLiftOverlay.SetEnabled(on);
            });

            Register("speechfade", (s) =>
            {
                if (s == null || s.Length < 2 || !int.TryParse(s[1].Trim(), out int ms))
                {
                    GameActions.Print($"Speech fade window = {TextRenderer.FadeWindowMs} ms. Usage: -speechfade <ms>", 0x35);
                    return;
                }
                if (ms < 200) ms = 200;
                if (ms > 20000) ms = 20000;
                TextRenderer.FadeWindowMs = ms;
                GameActions.Print($"Speech fade window = {ms} ms.", 0x35);
            });

            Register("hptint", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.LowHpScreenTint.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.LowHpScreenTint.SetEnabled(on);
            });

            Register("aggrotint", (s) =>
            {
                AggroTintManager.Set(s != null && s.Length >= 2 ? s[1] : "");
            });

            Register("warborder", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.WarModeBorder.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.WarModeBorder.SetEnabled(on);
            });

            Register("wind", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.WindParticlesOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.WindParticlesOverlay.SetEnabled(on);
            });

            Register("cursorhint", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.CursorHintOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CursorHintOverlay.SetEnabled(on);
            });

            // journaldock removed.

            Register("paragonglow", (s) =>
            {
                if (s != null && s.Length >= 3)
                {
                    string sub = s[1].Trim().ToLowerInvariant();
                    if (sub == "hue" && ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort h))
                    { ParagonGlowManager.GlowHue = h; GameActions.Print($"Paragon glow hue = 0x{h:X}.", 0x35); return; }
                    if (sub == "graphic" && ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort g))
                    { ParagonGlowManager.GlowGraphic = g; GameActions.Print($"Paragon glow graphic = 0x{g:X}.", 0x35); return; }
                }
                bool on = s == null || s.Length < 2
                    ? !ParagonGlowManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                ParagonGlowManager.SetEnabled(on);
            });

            Register("autopot", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -autopot heal on|off|<pct> | -autopot cure on|off", 0x21); return; }
                string kind = s[1].Trim().ToLowerInvariant();
                if (kind == "heal")
                {
                    if (s.Length >= 3 && int.TryParse(s[2], out int pct))
                    {
                        AutoHealPotionManager.ThresholdPct = System.Math.Max(1, System.Math.Min(95, pct));
                        AutoHealPotionManager.SetEnabled(true);
                        return;
                    }
                    bool on = s.Length < 3
                        ? !AutoHealPotionManager.Enabled
                        : s[2].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                    AutoHealPotionManager.SetEnabled(on);
                }
                else if (kind == "cure")
                {
                    bool on = s.Length < 3
                        ? !AutoCurePotionManager.Enabled
                        : s[2].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                    AutoCurePotionManager.SetEnabled(on);
                }
                else if (kind == "refresh")
                {
                    if (s.Length >= 3 && int.TryParse(s[2], out int rpct))
                    {
                        AutoRefreshPotionManager.SetThreshold(rpct);
                        AutoRefreshPotionManager.SetEnabled(true);
                        return;
                    }
                    bool on = s.Length < 3
                        ? !AutoRefreshPotionManager.Enabled
                        : s[2].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                    AutoRefreshPotionManager.SetEnabled(on);
                }
                else GameActions.Print("Usage: -autopot heal|cure|refresh on|off|<pct>", 0x21);
            });

            Register("healpulse", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.HealReceivedPulse.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.HealReceivedPulse.SetEnabled(on);
            });

            Register("petbar", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.PetCommandBarGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new UI.Gumps.PetCommandBarGump());
            });

            Register("hostilebox", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.HostileEdgeHighlight.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.HostileEdgeHighlight.Enabled = on;
                GameActions.Print($"Hostile box {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("partyalert", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !PartyInviteAlertManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                PartyInviteAlertManager.SetEnabled(on);
            });

            Register("fullhptoast", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !FullHpToastManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                FullHpToastManager.SetEnabled(on);
            });


            Register("pinghud", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.PingHudOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.PingHudOverlay.Enabled = on;
                GameActions.Print($"Ping HUD {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("compactbars", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.CompactBarsOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CompactBarsOverlay.Enabled = on;
                GameActions.Print($"Compact bars {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("mute", (s) =>
            {
                SystemMessageMuteManager.EnsureLoaded();
                if (s == null || s.Length < 2)
                {
                    SystemMessageMuteManager.SetEnabled(!SystemMessageMuteManager.Enabled);
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") SystemMessageMuteManager.SetEnabled(true);
                else if (a == "off") SystemMessageMuteManager.SetEnabled(false);
                else if (a == "add" && s.Length >= 3) { SystemMessageMuteManager.Add(string.Join(" ", s, 2, s.Length - 2)); GameActions.Print("Added.", 0x35); }
                else if (a == "del" && s.Length >= 3) { SystemMessageMuteManager.Del(string.Join(" ", s, 2, s.Length - 2)); GameActions.Print("Removed.", 0x21); }
                else if (a == "list") { foreach (var p in SystemMessageMuteManager.Patterns) GameActions.Print(" - " + p, 0x44); }
                else GameActions.Print("Usage: -mute on|off|add <p>|del <p>|list", 0x21);
            });

            Register("buffexpire", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !BuffExpiryToastManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                BuffExpiryToastManager.SetEnabled(on);
            });

            Register("bodyscale", (s) =>
            {
                BodyScaleManager.EnsureLoaded();
                if (s == null || s.Length < 2)
                {
                    GameActions.Print($"Body scaling: {(BodyScaleManager.Enabled ? "ON" : "OFF")}", 0x35);
                    foreach (var kv in BodyScaleManager.All)
                        GameActions.Print($"  0x{kv.Key:X} = {kv.Value}", 0x44);
                    GameActions.Print("Usage: -bodyscale on|off|status|<body-hex> <scale>|del <body-hex>", 0x35);
                    return;
                }
                string action = s[1].Trim();
                if (action.Equals("on", System.StringComparison.OrdinalIgnoreCase))
                {
                    BodyScaleManager.SetEnabled(true);
                    return;
                }
                if (action.Equals("off", System.StringComparison.OrdinalIgnoreCase))
                {
                    BodyScaleManager.SetEnabled(false);
                    return;
                }
                if (action.Equals("status", System.StringComparison.OrdinalIgnoreCase))
                {
                    GameActions.Print($"Body scaling: {(BodyScaleManager.Enabled ? "ON" : "OFF")}", 0x35);
                    return;
                }
                if (action.Equals("del", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (s.Length < 3 || !ushort.TryParse(s[2], System.Globalization.NumberStyles.HexNumber, null, out ushort b)) { GameActions.Print("Bad body id.", 0x21); return; }
                    BodyScaleManager.Remove(b);
                    return;
                }
                if (s.Length < 3 || !ushort.TryParse(s[1], System.Globalization.NumberStyles.HexNumber, null, out ushort body))
                { GameActions.Print("Bad body id.", 0x21); return; }
                if (!float.TryParse(s[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale))
                { GameActions.Print("Bad scale.", 0x21); return; }
                BodyScaleManager.Set(body, scale);
            });

            Register("cast", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -cast <spell> [lasttarget|self]", 0x21); return; }
                // Spell name can be multiple words; last token may be tgt mode.
                string last = s[s.Length - 1].Trim();
                bool hasMode = last.Equals("lasttarget", System.StringComparison.OrdinalIgnoreCase)
                            || last.Equals("self", System.StringComparison.OrdinalIgnoreCase);
                int endIdx = hasMode ? s.Length - 1 : s.Length;
                string name = string.Join(" ", s, 1, endIdx - 1);
                CastByNameManager.Cast(name, hasMode ? last : null);
            });

            Register("timerhud", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.TimerStackHud.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.TimerStackHud.Enabled = on;
                GameActions.Print($"Timer HUD {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("notdot", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.NotorietyDotOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.NotorietyDotOverlay.Enabled = on;
                GameActions.Print($"Notoriety dot {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("loiterwarn", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("off", System.StringComparison.OrdinalIgnoreCase))
                { HostileLoiteringWarner.Disable(); return; }
                int range = 6, sec = 8;
                if (s != null && s.Length >= 2) int.TryParse(s[1], out range);
                if (s != null && s.Length >= 3) int.TryParse(s[2], out sec);
                HostileLoiteringWarner.Configure(range, sec);
            });

            Register("jump", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -jump <name>", 0x21); return; }
                JumpToPlayerManager.JumpTo(string.Join(" ", s, 1, s.Length - 1));
            });

            Register("fx", (s) =>
            {
                UI.EffectsBundle.EnsureHooked();
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -fx <name> [on|off]   names: crit heal death castaura lockring stealth statusaura walkdust speedlines knockback loot darkaura all", 0x35);
                    return;
                }
                string name = s[1].Trim().ToLowerInvariant();
                bool? want = null;
                if (s.Length >= 3)
                    want = s[2].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                bool Flip(ref bool flag) { flag = want ?? !flag; return flag; }
                bool state;
                switch (name)
                {
                    case "crit":       state = Flip(ref UI.EffectsBundle.Crit); break;
                    case "heal":       state = Flip(ref UI.EffectsBundle.Heal); break;
                    case "death":      state = Flip(ref UI.EffectsBundle.Death); break;
                    case "castaura":   state = Flip(ref UI.EffectsBundle.CastAura); break;
                    case "lockring":   state = Flip(ref UI.EffectsBundle.LockRing); break;
                    case "stealth":    state = Flip(ref UI.EffectsBundle.Stealth); break;
                    case "statusaura": state = Flip(ref UI.EffectsBundle.StatusAura); break;
                    case "walkdust":   state = Flip(ref UI.EffectsBundle.WalkDust); break;
                    case "speedlines": state = Flip(ref UI.EffectsBundle.SpeedLines); break;
                    case "knockback":  state = Flip(ref UI.EffectsBundle.Knockback); break;
                    case "loot":       state = Flip(ref UI.EffectsBundle.LootPillar); break;
                    case "darkaura":   state = Flip(ref UI.EffectsBundle.DarkAura); break;
                    case "all":
                        bool v = want ?? !UI.EffectsBundle.Crit;
                        UI.EffectsBundle.Crit = UI.EffectsBundle.Heal = UI.EffectsBundle.Death =
                            UI.EffectsBundle.CastAura = UI.EffectsBundle.LockRing = UI.EffectsBundle.Stealth =
                            UI.EffectsBundle.StatusAura = UI.EffectsBundle.WalkDust = UI.EffectsBundle.SpeedLines =
                            UI.EffectsBundle.Knockback = UI.EffectsBundle.LootPillar = UI.EffectsBundle.DarkAura = v;
                        GameActions.Print($"All FX {(v ? "ON" : "OFF")}.", (ushort)(v ? 0x35 : 0x21));
                        return;
                    default:
                        GameActions.Print($"Unknown fx '{name}'.", 0x21);
                        return;
                }
                GameActions.Print($"fx {name} {(state ? "ON" : "OFF")}.", (ushort)(state ? 0x35 : 0x21));
            });

            Register("overheadsize", (s) =>
            {
                if (
                    s == null
                    || s.Length < 2
                    || s[1].Trim().Equals("status", StringComparison.OrdinalIgnoreCase)
                )
                {
                    GameActions.Print(
                        $"Overhead effect size: {UI.OverheadEffectSizeSettings.Name}. "
                        + "Use -overheadsize small|normal|large|extralarge.",
                        0x35
                    );
                    return;
                }

                if (!UI.OverheadEffectSizeSettings.TrySet(s[1]))
                {
                    GameActions.Print(
                        "Usage: -overheadsize small|normal|large|extralarge",
                        0x21
                    );
                    return;
                }

                GameActions.Print(
                    $"Overhead effect size: {UI.OverheadEffectSizeSettings.Name}.",
                    0x35
                );
            });

            Register("netherblaststyle", (s) =>
            {
                if (
                    s == null
                    || s.Length < 2
                    || s[1].Trim().Equals("status", StringComparison.OrdinalIgnoreCase)
                )
                {
                    int current = NetherBlastVortexManager.Style;
                    GameActions.Print(
                        $"Nether Blast style: {current} ({NetherBlastVortexManager.GetStyleName(current)}). "
                        + "Use -netherblaststyle off|1|2|3|4|5|6.",
                        0x35
                    );
                    return;
                }

                if (!NetherBlastVortexManager.TryParseStyle(s[1], out int style))
                {
                    GameActions.Print(
                        "Usage: -netherblaststyle off|1|2|3|4|5|6 "
                        + "(1 Void Maelstrom, 2 Arcane Cyclone, 3 Abyssal Storm, "
                        + "4 Prismatic Tempest, 5 Nether Pulse Wave, 6 Cosmic Singularity)",
                        0x21
                    );
                    return;
                }

                NetherBlastVortexManager.SetStyle(style);
                GameActions.Print(
                    $"Nether Blast style: {style} ({NetherBlastVortexManager.GetStyleName(style)}).",
                    (ushort)(style == 0 ? 0x21 : 0x35)
                );
            });

            Register("poisoncure", (s) =>
            {
                if (s != null && s.Length >= 3 && s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase))
                {
                    PoisonCureManager.SetEnabled(true, string.Join(" ", s, 2, s.Length - 2));
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !PoisonCureManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                PoisonCureManager.SetEnabled(on);
            });

            Register("openjournal", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("ondeath", System.StringComparison.OrdinalIgnoreCase))
                {
                    bool on = s.Length < 3 || s[2].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                    JournalOpenerManager.SetAutoOnDeath(on);
                    return;
                }
                JournalOpenerManager.Open();
            });

            Register("buyvendor", (s) => { BuyVendorMacro.Buy(); });
            Register("sellvendor", (s) => { BuyVendorMacro.Sell(); });

            Register("pin", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -pin <name>", 0x21); return; }
                PinnedSerialManager.Pin(string.Join(" ", s, 1, s.Length - 1));
            });
            Register("use", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -use <name>", 0x21); return; }
                PinnedSerialManager.Use(string.Join(" ", s, 1, s.Length - 1));
            });
            Register("pins", (s) =>
            {
                if (s != null && s.Length >= 3 && s[1].Trim().Equals("del", System.StringComparison.OrdinalIgnoreCase))
                {
                    PinnedSerialManager.Remove(string.Join(" ", s, 2, s.Length - 2));
                    return;
                }
                foreach (var kv in PinnedSerialManager.All)
                    GameActions.Print($"  {kv.Key} → 0x{kv.Value:X8}", 0x44);
            });

            Register("containerbadge", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.OpenContainerBadge.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.OpenContainerBadge.Enabled = on;
                GameActions.Print($"Container badge {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("deathlog", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("show", System.StringComparison.OrdinalIgnoreCase))
                {
                    int n = 5;
                    if (s.Length >= 3) int.TryParse(s[2], out n);
                    DeathLogManager.Show(n);
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !DeathLogManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                DeathLogManager.Enabled = on;
                GameActions.Print($"Death log {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("pingwarn", (s) =>
            {
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int ms))
                {
                    GameActions.Print("Usage: -pingwarn <ms>  (0=off)", 0x21);
                    return;
                }
                PingSpikeWarner.SpikeMs = System.Math.Max(0, ms);
                GameActions.Print($"Ping warn at >{PingSpikeWarner.SpikeMs}ms.", 0x35);
            });

            Register("countall", (s) =>
            {
                int top = 20;
                if (s != null && s.Length >= 2) int.TryParse(s[1], out top);
                CountAllManager.Print(top);
            });

            Register("eventlog", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !EventSinkLogger.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                EventSinkLogger.SetEnabled(on);
            });

            Register("hiddenwatch", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !HiddenStateWatcher.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                HiddenStateWatcher.SetEnabled(on);
            });

            Register("focusmute", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !WindowFocusMuteManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                WindowFocusMuteManager.SetEnabled(on);
            });

            Register("alias", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    foreach (var kv in CommandAliasManager.All)
                        GameActions.Print($"  -{kv.Key} → -{kv.Value}", 0x44);
                    return;
                }
                string sub = s[1].Trim().ToLowerInvariant();
                if (sub == "del" && s.Length >= 3) { CommandAliasManager.Remove(s[2]); return; }
                if (s.Length < 3) { GameActions.Print("Usage: -alias <new> <existing>  |  -alias del <new>  |  -alias", 0x21); return; }
                CommandAliasManager.Bind(s[1], s[2]);
            });

            Register("corpsefade", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.CorpseFadeOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CorpseFadeOverlay.Enabled = on;
                GameActions.Print($"Corpse fade {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("rec", (s) =>
            {
                bool lt = s != null && s.Length >= 2 && s[1].Trim().Equals("lasttarget", System.StringComparison.OrdinalIgnoreCase);
                LastSpellRecastManager.Recast(lt);
            });

            Register("hostileline", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.NearestHostileLine.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.NearestHostileLine.Enabled = on;
                GameActions.Print($"Nearest-hostile line {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("arrowline", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.QuestArrowLine.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.QuestArrowLine.Enabled = on;
                GameActions.Print($"Quest-arrow line {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("countdown", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("clear", System.StringComparison.OrdinalIgnoreCase))
                { CountdownTimerManager.Clear(); return; }
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int sec))
                {
                    GameActions.Print("Usage: -countdown <seconds> [label] | -countdown clear", 0x21);
                    return;
                }
                string label = s.Length >= 3 ? string.Join(" ", s, 2, s.Length - 2) : "(timer)";
                CountdownTimerManager.Schedule(sec, label);
            });

            Register("countdowns", (s) => { CountdownTimerManager.List(); });

            Register("pethp", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.PetHpBarsOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.PetHpBarsOverlay.Enabled = on;
                GameActions.Print($"Pet HP bars {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("notorietywatch", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !NotorietyChangeWatcher.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                NotorietyChangeWatcher.SetEnabled(on);
            });

            Register("petkill", (s) => { PetCommandManager.Kill(); });
            Register("petfollow", (s) => { PetCommandManager.Speak("follow me"); });
            Register("petstay",   (s) => { PetCommandManager.Speak("stay"); });
            Register("petguard",  (s) => { PetCommandManager.Speak("guard"); });
            Register("petcome",   (s) => { PetCommandManager.Speak("come"); });
            Register("petstop",   (s) => { PetCommandManager.Speak("stop"); });

            Register("keyword", (s) =>
            {
                JournalKeywordToastManager.EnsureLoaded();
                if (s == null || s.Length < 2)
                {
                    JournalKeywordToastManager.SetEnabled(!JournalKeywordToastManager.Enabled);
                    return;
                }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "on") JournalKeywordToastManager.SetEnabled(true);
                else if (a == "off") JournalKeywordToastManager.SetEnabled(false);
                else if (a == "add" && s.Length >= 3) JournalKeywordToastManager.Add(string.Join(" ", s, 2, s.Length - 2));
                else if (a == "del" && s.Length >= 3) JournalKeywordToastManager.Del(string.Join(" ", s, 2, s.Length - 2));
                else if (a == "list") { foreach (var p in JournalKeywordToastManager.Patterns) GameActions.Print(" - " + p, 0x44); }
                else GameActions.Print("Usage: -keyword on|off|add <p>|del <p>|list", 0x21);
            });

            Register("bandage", (s) =>
            {
                if (s != null && s.Length >= 2 && uint.TryParse(s[1], System.Globalization.NumberStyles.HexNumber, null, out uint hex))
                {
                    BandageQuickUseManager.UseOn(hex);
                    return;
                }
                BandageQuickUseManager.UseSelf();
            });

            Register("summary", (s) => { ToggleSummaryManager.Print(); });

            Register("combatstate", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !CombatStateManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                CombatStateManager.SetEnabled(on);
            });

            Register("lastmsg", (s) => { LastSpeechManager.EnsureHooked(); LastSpeechManager.Repeat(); });

            Register("autopaper", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AutoOpenPaperdollManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoOpenPaperdollManager.SetEnabled(on);
            });

            Register("tgtcross", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.TargetCrosshair.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.TargetCrosshair.Enabled = on;
                GameActions.Print($"Target crosshair {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("paste", (s) => { ClipboardSayManager.Speak(); });

            Register("tilegrid", (s) =>
            {
                if (s != null && s.Length >= 2 && int.TryParse(s[1], out int r))
                {
                    UI.TileGridOverlay.Range = System.Math.Max(0, r);
                    GameActions.Print($"Tile grid {(UI.TileGridOverlay.Range > 0 ? $"ON r={UI.TileGridOverlay.Range}" : "OFF")}.",
                        (ushort)(UI.TileGridOverlay.Range > 0 ? 0x35 : 0x21));
                    return;
                }
                UI.TileGridOverlay.Range = UI.TileGridOverlay.Range > 0 ? 0 : 5;
                GameActions.Print($"Tile grid {(UI.TileGridOverlay.Range > 0 ? $"ON r={UI.TileGridOverlay.Range}" : "OFF")}.",
                    (ushort)(UI.TileGridOverlay.Range > 0 ? 0x35 : 0x21));
            });

            Register("spellbook", (s) => { SpellbookOpenerManager.Open(); });

            Register("idlewarn", (s) =>
            {
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int sec))
                {
                    GameActions.Print("Usage: -idlewarn <seconds>  (0=off)", 0x21);
                    return;
                }
                IdleMonitorManager.IdleSeconds = System.Math.Max(0, sec);
                GameActions.Print($"Idle warn at {IdleMonitorManager.IdleSeconds}s.", 0x35);
            });

            Register("vendorclose", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AutoVendorCloseManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoVendorCloseManager.SetEnabled(on);
            });

            Register("compass", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.CompassOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CompassOverlay.Enabled = on;
                GameActions.Print($"Compass {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("hitflash", (s) =>
            {
                UI.ScreenflashOnHit.EnsureHooked();
                bool on = s == null || s.Length < 2
                    ? !UI.ScreenflashOnHit.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.ScreenflashOnHit.Enabled = on;
                GameActions.Print($"Hit flash {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("help", (s) =>
            {
                string pat = (s != null && s.Length >= 2) ? string.Join(" ", s, 1, s.Length - 1) : null;
                CommandHelpManager.Print(pat);
            });

            Register("weather", (s) =>
            {
                var scene = Client.Game.GetScene<Scenes.GameScene>();
                if (scene == null) return;
                string kind = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : "";
                bool heavyKind = kind == "blizzard" || kind == "heavysnow" || kind == "tempest";
                byte count = heavyKind ? (byte)200 : (byte)70;
                if (s != null && s.Length >= 3 && byte.TryParse(s[2], out byte c)) count = c;

                bool validKind = kind == "rain" || kind == "snow" || kind == "blizzard"
                    || kind == "heavysnow" || kind == "storm" || kind == "tempest"
                    || kind == "brewing" || kind == "hail" || kind == "sleet"
                    || kind == "fog" || kind == "off";
                if (!validKind)
                {
                    GameActions.Print("Usage: -weather rain|snow|heavysnow|blizzard|storm|tempest|brewing|hail|sleet|fog|off [count]", 0x35);
                    return;
                }

                AmbientWeatherManager.CancelForExternalWeather();
                switch (kind)
                {
                    case "rain":    scene.Weather.Generate(WeatherType.WT_RAIN, count, 0, WeatherSource.Manual); break;
                    case "snow":    scene.Weather.Generate(WeatherType.WT_SNOW, count, 0, WeatherSource.Manual); break;
                    case "blizzard":
                        scene.Weather.Generate(WeatherType.WT_SNOW, count, 0, WeatherSource.Manual);
                        scene.Weather.Blizzard = true;
                        break;
                    case "heavysnow":
                        scene.Weather.Generate(WeatherType.WT_SNOW, count, 0, WeatherSource.Manual);
                        scene.Weather.HeavySnow = true;
                        break;
                    case "storm":   scene.Weather.Generate(WeatherType.WT_STORM_APPROACH, count, 0, WeatherSource.Manual); break;
                    case "tempest":
                        scene.Weather.Generate(WeatherType.WT_STORM_APPROACH, count, 0, WeatherSource.Manual);
                        scene.Weather.Tempest = true;
                        break;
                    case "brewing": scene.Weather.Generate(WeatherType.WT_STORM_BREWING, count, 0, WeatherSource.Manual); break;
                    case "hail":
                        scene.Weather.Generate(WeatherType.WT_SNOW, count, 0, WeatherSource.Manual);
                        scene.Weather.Hail = true;
                        break;
                    case "sleet":
                        scene.Weather.Generate(WeatherType.WT_RAIN, count, 0, WeatherSource.Manual);
                        scene.Weather.Sleet = true;
                        break;
                    case "fog":
                        scene.Weather.SetFog(WeatherSource.Manual);
                        break;
                    case "off":     scene.Weather.Reset(); GameActions.Print("Weather off.", 0x21); return;
                }
                GameActions.Print($"Weather: {kind} (count {count}). Local only — server weather packets override it.", 0x35);
            });

            Register("shadowtest", (s) =>
            {
                string mode = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : "";
                if (mode == "auto")
                {
                    EnvironmentalShadowManager.ClearPreviewLight();
                    GameActions.Print("Shadow preview AUTO — following server global light.", 0x35);
                    return;
                }

                int light;
                string label;
                switch (mode)
                {
                    case "day":   light = 0;  label = "DAY"; break;
                    case "dusk":  light = 7;  label = "DUSK"; break;
                    case "moon":  light = 12; label = "MOONLIT NIGHT"; break;
                    case "night": light = 30; label = "MOONLESS NIGHT"; break;
                    default:
                        if (!int.TryParse(mode, out light) || light < 0 || light > 30)
                        {
                            GameActions.Print("Usage: -shadowtest day|dusk|moon|night|auto|<0..30>", 0x35);
                            return;
                        }
                        label = $"LIGHT {light}";
                        break;
                }

                EnvironmentalShadowManager.SetPreviewLight(light);
                GameActions.Print($"Shadow preview {label} — client only; world brightness unchanged.", 0x35);
            });

            Register("seasontest", (s) =>
            {
                string mode = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : "";
                if (mode == "auto")
                {
                    World.SetSeasonOverride(null);
                    GameActions.Print("Season preview AUTO — following the server season.", 0x35);
                    return;
                }

                Season season;
                switch (mode)
                {
                    case "spring":      season = Season.Spring; break;
                    case "summer":      season = Season.Summer; break;
                    case "fall":
                    case "autumn":      season = Season.Fall; break;
                    case "winter":      season = Season.Winter; break;
                    case "desolation":  season = Season.Desolation; break;
                    default:
                        GameActions.Print("Usage: -seasontest spring|summer|fall|winter|desolation|auto", 0x35);
                        return;
                }

                World.SetSeasonOverride(season);
                GameActions.Print($"Season preview {season.ToString().ToUpperInvariant()} — client only.", 0x35);
            });

            Register("envshowcase", (s) =>
            {
                var scene = Client.Game.GetScene<Scenes.GameScene>();
                if (scene == null) return;

                string mode = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : "";
                bool enable = mode == "on" || (mode.Length == 0 && !EnvironmentShowcaseManager.Enabled);
                bool disable = mode == "off" || (mode.Length == 0 && EnvironmentShowcaseManager.Enabled);
                if (!enable && !disable)
                {
                    GameActions.Print("Usage: -envshowcase on|off", 0x35);
                    return;
                }

                if (enable)
                {
                    if (DayCyclePreviewManager.Enabled)
                    {
                        DayCyclePreviewManager.Stop();
                    }
                    if (EnvironmentShowcaseManager.BeautifulEnabled)
                    {
                        EnvironmentShowcaseManager.Stop(scene.Weather);
                    }
                    EnvironmentShowcaseManager.Start(scene.Weather);
                }
                else
                {
                    EnvironmentShowcaseManager.Stop(scene.Weather);
                }
            });

            Register("envshowcasebeautiful", (s) =>
            {
                var scene = Client.Game.GetScene<Scenes.GameScene>();
                if (scene == null) return;

                string mode = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : "";
                bool enable = mode == "on" || (mode.Length == 0 && !EnvironmentShowcaseManager.BeautifulEnabled);
                bool disable = mode == "off" || (mode.Length == 0 && EnvironmentShowcaseManager.BeautifulEnabled);
                if (!enable && !disable)
                {
                    GameActions.Print("Usage: -envshowcasebeautiful on|off", 0x35);
                    return;
                }

                if (enable)
                {
                    if (DayCyclePreviewManager.Enabled)
                    {
                        DayCyclePreviewManager.Stop();
                    }
                    if (EnvironmentShowcaseManager.Enabled)
                    {
                        EnvironmentShowcaseManager.Stop(scene.Weather);
                    }
                    EnvironmentShowcaseManager.StartBeautiful(scene.Weather);
                }
                else if (EnvironmentShowcaseManager.BeautifulEnabled)
                {
                    EnvironmentShowcaseManager.Stop(scene.Weather);
                }
            });

            Register("daycycle", (s) =>
            {
                var scene = Client.Game.GetScene<Scenes.GameScene>();
                if (scene == null) return;

                string mode = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : "";
                bool enable = mode == "on" || (mode.Length == 0 && !DayCyclePreviewManager.Enabled);
                bool disable = mode == "off" || (mode.Length == 0 && DayCyclePreviewManager.Enabled);
                int seconds = DayCyclePreviewManager.DEFAULT_SECONDS;
                if (s != null && s.Length >= 3
                    && (!int.TryParse(s[2], out seconds)
                        || seconds < DayCyclePreviewManager.MIN_SECONDS
                        || seconds > DayCyclePreviewManager.MAX_SECONDS))
                {
                    GameActions.Print("Usage: -daycycle on|off [30-3600 seconds]", 0x35);
                    return;
                }
                if (!enable && !disable)
                {
                    GameActions.Print("Usage: -daycycle on|off [30-3600 seconds]", 0x35);
                    return;
                }

                if (enable)
                {
                    if (EnvironmentShowcaseManager.Enabled)
                    {
                        EnvironmentShowcaseManager.Stop(scene.Weather);
                    }
                    DayCyclePreviewManager.Start(seconds);
                }
                else
                {
                    DayCyclePreviewManager.Stop();
                }
            });

            Register("ambience", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.AmbienceOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.AmbienceOverlay.Enabled = on;
                if (ProfileManager.CurrentProfile != null)
                {
                    ProfileManager.CurrentProfile.AmbienceOverlayEnabled = on;
                }
                GameActions.Print($"Ambience effects {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("ambientlights", (s) =>
            {
                string mode = s != null && s.Length >= 2
                    ? s[1].Trim().ToLowerInvariant() : string.Empty;
                bool enabled;
                if (mode.Length == 0)
                {
                    enabled = !UI.AmbienceOverlay.LightsEnabled;
                }
                else if (mode == "on")
                {
                    enabled = true;
                }
                else if (mode == "off")
                {
                    enabled = false;
                }
                else
                {
                    GameActions.Print("Usage: -ambientlights on|off", 0x35);
                    return;
                }

                UI.AmbienceOverlay.SetLightsEnabled(enabled);
                GameActions.Print(
                    $"Ambient lights {(enabled ? "ON" : "OFF")}.",
                    (ushort)(enabled ? 0x35 : 0x21)
                );
            });

            Register("waterenhancement", (s) =>
            {
                string mode = s != null && s.Length >= 2
                    ? s[1].Trim().ToLowerInvariant() : string.Empty;
                bool enabled;
                if (mode.Length == 0)
                {
                    enabled = !WaterEnhancementManager.ArtworkEnabled;
                }
                else if (mode == "on")
                {
                    enabled = true;
                }
                else if (mode == "off")
                {
                    enabled = false;
                }
                else
                {
                    GameActions.Print("Usage: -waterenhancement on|off", 0x35);
                    return;
                }

                WaterEnhancementManager.SetArtworkEnabled(enabled);
                GameActions.Print(
                    $"Water enhancement {(enabled ? "ON" : "OFF")}.",
                    (ushort)(enabled ? 0x35 : 0x21)
                );
            });

            Register("wateratmosphere", (s) =>
            {
                string mode = s != null && s.Length >= 2
                    ? s[1].Trim().ToLowerInvariant() : string.Empty;
                bool enabled;
                if (mode.Length == 0)
                    enabled = !WaterEnhancementManager.AtmosphereEnabled;
                else if (mode == "on")
                    enabled = true;
                else if (mode == "off")
                    enabled = false;
                else
                {
                    GameActions.Print("Usage: -wateratmosphere on|off", 0x35);
                    return;
                }

                WaterEnhancementManager.SetAtmosphereEnabled(enabled);
                GameActions.Print(
                    $"Water atmosphere {(enabled ? "ON" : "OFF")}.",
                    (ushort)(enabled ? 0x35 : 0x21));
            });

            Register("waterstyle", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    WaterEnhancementManager.CycleArtworkStyle();
                }
                else if (!WaterEnhancementManager.SetArtworkStyle(s[1]))
                {
                    GameActions.Print(
                        "Usage: -waterstyle natural|waves|choppy|swell|storm|moonlit",
                        0x35
                    );
                    return;
                }

                GameActions.Print(
                    $"Water style: {WaterEnhancementManager.ArtworkStyleName}.",
                    0x35
                );
            });

            Register("waterintensity", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print(
                        $"Water intensity: {WaterEnhancementManager.IntensityPercent}%.", 0x35);
                    return;
                }

                int percent;
                if (string.Equals(s[1], "reset", StringComparison.OrdinalIgnoreCase))
                {
                    percent = WaterEnhancementManager.DEFAULT_INTENSITY_PERCENT;
                }
                else if (!int.TryParse(s[1], out percent))
                {
                    GameActions.Print("Usage: -waterintensity 0-200|reset", 0x35);
                    return;
                }
                if (!WaterEnhancementManager.SetIntensity(percent))
                {
                    GameActions.Print("Usage: -waterintensity 0-200|reset", 0x35);
                    return;
                }
                GameActions.Print($"Water intensity: {percent}%.", 0x35);
            });

            Register("materialintensity", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print(TerrainMaterialManager.IntensityReport, 0x35);
                    return;
                }
                if (s.Length == 2)
                {
                    if (string.Equals(s[1], "reset", StringComparison.OrdinalIgnoreCase))
                    {
                        TerrainMaterialManager.SetIntensity("all", 100);
                        GameActions.Print("All terrain material intensities reset to 100%.", 0x35);
                        return;
                    }
                    if (TerrainMaterialManager.TryGetIntensity(s[1], out int current))
                    {
                        GameActions.Print($"{s[1]} material intensity: {current}%.", 0x35);
                        return;
                    }
                }

                if (s.Length < 3 || !int.TryParse(s[2], out int percent)
                    || !TerrainMaterialManager.SetIntensity(s[1], percent))
                {
                    GameActions.Print(
                        "Usage: -materialintensity [all|sand|grass|mine|dungeon|dirt|snow 0-200]|reset",
                        0x35);
                    return;
                }
                GameActions.Print($"{s[1]} material intensity: {percent}%.", 0x35);
            });

            Register("materialdebug", (s) =>
            {
                string mode = s != null && s.Length >= 2
                    ? s[1].Trim().ToLowerInvariant() : string.Empty;
                bool enabled;
                if (mode.Length == 0)
                    enabled = !TerrainMaterialManager.DebugEnabled;
                else if (mode == "on")
                    enabled = true;
                else if (mode == "off")
                    enabled = false;
                else
                {
                    GameActions.Print("Usage: -materialdebug on|off", 0x35);
                    return;
                }

                TerrainMaterialManager.SetDebugEnabled(enabled);
                GameActions.Print(enabled
                    ? "Material debug ON: sand=gold, grass=green, mine=orange, dungeon=purple, dirt=brown, snow=cyan."
                    : "Material debug OFF.", 0x35);
            });

            Register("environment", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.EnvironmentControlGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }
                UIManager.Add(new UI.Gumps.EnvironmentControlGump());
            });

            Register("spelleffects", (s) =>
            {
                var existing =
                    UIManager.GetGump<UI.Gumps.SpellAbilityEffectsGump>();
                if (existing != null && !existing.IsDisposed)
                {
                    existing.Dispose();
                    return;
                }

                UIManager.Add(new UI.Gumps.SpellAbilityEffectsGump());
            });

            Register("visualsilence", (s) =>
            {
                string value = s != null && s.Length >= 2
                    ? s[1].Trim().ToLowerInvariant()
                    : string.Empty;

                if (value == "status")
                {
                    GameActions.Print(
                        $"Visual silence {(SpellAbilityEffectSettings.VisualSilenceEnabled ? "ON" : "OFF")}.",
                        0x35
                    );
                    return;
                }

                bool enabled;
                if (value.Length == 0)
                {
                    enabled = !SpellAbilityEffectSettings.VisualSilenceEnabled;
                }
                else if (value == "on")
                {
                    enabled = true;
                }
                else if (value == "off")
                {
                    enabled = false;
                }
                else
                {
                    GameActions.Print("Usage: -visualsilence on|off|status", 0x35);
                    return;
                }

                SpellAbilityEffectSettings.SetVisualSilence(enabled);
                GameActions.Print(
                    $"Visual silence {(enabled ? "ON" : "OFF")}: spell and combat effect graphics {(enabled ? "hidden" : "restored")}.",
                    enabled ? (ushort)0x21 : (ushort)0x35
                );
            });

            Register("classiceffects", (s) =>
            {
                string value = s != null && s.Length >= 2
                    ? s[1].Trim().ToLowerInvariant()
                    : string.Empty;

                if (value == "status")
                {
                    GameActions.Print(
                        $"Classic effects {(SpellAbilityEffectSettings.ClassicEffectsOnlyEnabled ? "ON" : "OFF")}.",
                        0x35
                    );
                    return;
                }

                bool enabled;
                if (value.Length == 0)
                {
                    enabled = !SpellAbilityEffectSettings.ClassicEffectsOnlyEnabled;
                }
                else if (value == "on")
                {
                    enabled = true;
                }
                else if (value == "off")
                {
                    enabled = false;
                }
                else
                {
                    GameActions.Print("Usage: -classiceffects on|off|status", 0x35);
                    return;
                }

                SpellAbilityEffectSettings.SetClassicEffectsOnly(enabled);
                GameActions.Print(
                    $"Classic effects {(enabled ? "ON" : "OFF")}: custom combat effects {(enabled ? "hidden" : "restored")}.",
                    0x35
                );
            });

            Register("sceneryquality", (s) =>
            {
                Profile profile = ProfileManager.CurrentProfile;
                if (profile == null) return;
                string value = s != null && s.Length >= 2 ? s[1].Trim().ToLowerInvariant() : string.Empty;
                switch (value)
                {
                    case "low": profile.SceneryQuality = SceneryInteractionManager.QUALITY_LOW; break;
                    case "medium":
                    case "med": profile.SceneryQuality = SceneryInteractionManager.QUALITY_MEDIUM; break;
                    case "high": profile.SceneryQuality = SceneryInteractionManager.QUALITY_HIGH; break;
                    default:
                        GameActions.Print("Usage: -sceneryquality low|medium|high", 0x35);
                        return;
                }
                GameActions.Print($"Scenery quality {value.ToUpperInvariant()}.", 0x35);
            });

            Register("ambientweather", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AmbientWeatherManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AmbientWeatherManager.SetEnabled(on);
            });

            Register("weathermotion", (s) =>
            {
                if (ProfileManager.CurrentProfile == null) return;
                bool fullMotion = s == null || s.Length < 2
                    ? ProfileManager.CurrentProfile.ReduceWeatherMotion
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                ProfileManager.CurrentProfile.ReduceWeatherMotion = !fullMotion;
                GameActions.Print(
                    $"Weather motion {(fullMotion ? "ON" : "REDUCED")}.",
                    (ushort)(fullMotion ? 0x35 : 0x21)
                );
            });

            Register("commands", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.CommandPaletteGump>();
                if (existing != null) { existing.Dispose(); return; }
                UIManager.Add(new UI.Gumps.CommandPaletteGump());
            });

            Register("autopack", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AutoOpenBackpackManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoOpenBackpackManager.SetEnabled(on);
            });

            Register("lastenemy", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("list", System.StringComparison.OrdinalIgnoreCase))
                {
                    TargetingHistoryManager.List();
                    return;
                }
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int idx))
                {
                    GameActions.Print("Usage: -lastenemy <0..4> | list", 0x21);
                    return;
                }
                TargetingHistoryManager.Recall(idx);
            });

            Register("deathrecap", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !DeathRecapManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                DeathRecapManager.SetEnabled(on);
            });

            Register("mark", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -mark <name>", 0x21); return; }
                CheckpointManager.Mark(string.Join(" ", s, 1, s.Length - 1));
            });

            Register("recall", (s) =>
            {
                if (s == null || s.Length < 2) { GameActions.Print("Usage: -recall <name>", 0x21); return; }
                CheckpointManager.Recall(string.Join(" ", s, 1, s.Length - 1));
            });

            Register("marks", (s) =>
            {
                if (s != null && s.Length >= 3 && s[1].Trim().Equals("del", System.StringComparison.OrdinalIgnoreCase))
                {
                    CheckpointManager.Delete(string.Join(" ", s, 2, s.Length - 2));
                    return;
                }
                foreach (var n in CheckpointManager.Names) GameActions.Print(" - " + n, 0x44);
            });

            Register("autoclosecorpse", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !AutoCloseEmptyCorpse.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoCloseEmptyCorpse.SetEnabled(on);
            });

            Register("trail", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.MoveTrailOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.MoveTrailOverlay.SetEnabled(on);
            });

            Register("trailfx", (s) =>
            {
                var existing = UIManager.GetGump<UI.Gumps.TrailEffectsGump>();
                if (existing != null && !existing.IsDisposed) existing.Dispose();
                UIManager.Add(new UI.Gumps.TrailEffectsGump());
            });

            Register("weightalert", (s) =>
            {
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int pct))
                {
                    GameActions.Print("Usage: -weightalert <pct>  (0=off)", 0x21);
                    return;
                }
                InventoryFullWarner.ThresholdPct = System.Math.Max(0, System.Math.Min(99, pct));
                GameActions.Print($"Weight alert at {InventoryFullWarner.ThresholdPct}%.", 0x35);
            });

            Register("finishlow", (s) =>
            {
                bool atk = s != null && s.Length >= 2 && s[1].Trim().Equals("attack", System.StringComparison.OrdinalIgnoreCase);
                FinishLowHpManager.Trigger(atk);
            });

            Register("cdhud", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.CooldownHud.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CooldownHud.Enabled = on;
                GameActions.Print($"Cooldown HUD {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("statalert", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !StatChangeAlertManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                StatChangeAlertManager.SetEnabled(on);
            });

            Register("trash", (s) =>
            {
                TrashContainerManager.EnsureLoaded();
                if (s == null || s.Length < 2) { TrashContainerManager.Open(); return; }
                string a = s[1].Trim().ToLowerInvariant();
                if (a == "pick") TrashContainerManager.Pick();
                else if (a == "drop") TrashContainerManager.Drop();
                else if (a == "open") TrashContainerManager.Open();
                else GameActions.Print("Usage: -trash | -trash pick | -trash drop | -trash open", 0x21);
            });

            Register("offscreenarrow", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !UI.OffscreenEnemyArrow.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.OffscreenEnemyArrow.Enabled = on;
                GameActions.Print($"Offscreen enemy arrow {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("follow", async (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("off", System.StringComparison.OrdinalIgnoreCase))
                {
                    AutoFollowManager.Set(0);
                    return;
                }
                if (s != null && s.Length >= 2 && uint.TryParse(s[1], System.Globalization.NumberStyles.HexNumber, null, out uint hex))
                {
                    AutoFollowManager.Set(hex);
                    return;
                }
                GameActions.Print("Follow: click a target...", 0x35);
                await TargetHelper.TargetObject((ent) =>
                {
                    if (ent != null && ent.Serial != 0) AutoFollowManager.Set(ent.Serial);
                });
            });

            Register("partycycle", (s) => { PartyCycleManager.Next(); });

            Register("emergencyheal", (s) =>
            {
                if (s != null && s.Length >= 2 && int.TryParse(s[1], out int pct))
                {
                    EmergencyHealManager.ThresholdPct = System.Math.Max(1, System.Math.Min(95, pct));
                    if (s.Length >= 3) EmergencyHealManager.SpellName = string.Join(" ", s, 2, s.Length - 2);
                    EmergencyHealManager.SetEnabled(true);
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !EmergencyHealManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                EmergencyHealManager.SetEnabled(on);
            });

            Register("walkto", (s) => { WalkToTargetManager.Prompt(); });

            Register("bandagetimer", (s) =>
            {
                if (s != null && s.Length >= 2 && int.TryParse(s[1], out int sec))
                {
                    BandageTimerManager.Duration = System.Math.Max(1, sec);
                    BandageTimerManager.SetEnabled(true);
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !BandageTimerManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                BandageTimerManager.SetEnabled(on);
            });

            Register("manaalert", (s) =>
            {
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int pct))
                {
                    GameActions.Print("Usage: -manaalert <pct>  (0=off)", 0x21);
                    return;
                }
                ManaStamAlertManager.ManaPct = System.Math.Max(0, System.Math.Min(99, pct));
                GameActions.Print($"Mana alert at <{ManaStamAlertManager.ManaPct}%.", 0x35);
            });

            Register("stamalert", (s) =>
            {
                if (s == null || s.Length < 2 || !int.TryParse(s[1], out int pct))
                {
                    GameActions.Print("Usage: -stamalert <pct>  (0=off)", 0x21);
                    return;
                }
                ManaStamAlertManager.StamPct = System.Math.Max(0, System.Math.Min(99, pct));
                GameActions.Print($"Stamina alert at <{ManaStamAlertManager.StamPct}%.", 0x35);
            });

            Register("autorearm", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("pick", System.StringComparison.OrdinalIgnoreCase))
                {
                    AutoRearmManager.Pick();
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !AutoRearmManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoRearmManager.SetEnabled(on);
            });

            Register("castbar", (s) =>
            {
                UI.CastProgressOverlay.EnsureHooked();
                bool on = s == null || s.Length < 2
                    ? !UI.CastProgressOverlay.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.CastProgressOverlay.Enabled = on;
                GameActions.Print($"Cast bar {(on ? "ON" : "OFF")}.", (ushort)(on ? 0x35 : 0x21));
            });

            Register("hungeralert", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !HungerThirstAlertManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                HungerThirstAlertManager.SetEnabled(on);
            });

            Register("lowhp", (s) =>
            {
                if (s != null && s.Length >= 2 && int.TryParse(s[1], out int pct))
                {
                    UI.LowHpVignette.ThresholdPct = System.Math.Max(1, System.Math.Min(95, pct));
                    UI.LowHpVignette.Enabled = true;
                    GameActions.Print($"Low-HP vignette ON, threshold {UI.LowHpVignette.ThresholdPct}%.", 0x35);
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !UI.LowHpVignette.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                UI.LowHpVignette.Enabled = on;
                GameActions.Print($"Low-HP vignette {(on ? "ON" : "OFF")} (threshold {UI.LowHpVignette.ThresholdPct}%).",
                    (ushort)(on ? 0x35 : 0x21));
            });

            Register("automount", (s) =>
            {
                if (s != null && s.Length >= 2 && s[1].Trim().Equals("pick", System.StringComparison.OrdinalIgnoreCase))
                {
                    AutoMountManager.Pick();
                    return;
                }
                bool on = s == null || s.Length < 2
                    ? !AutoMountManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                AutoMountManager.SetEnabled(on);
            });

            Register("loadout", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -loadout save|load|del <name>  |  -loadout list", 0x21);
                    return;
                }
                string sub = s[1].Trim().ToLowerInvariant();
                if (sub == "list")
                {
                    foreach (var n in QuickLoadoutManager.Names) GameActions.Print(" - " + n, 0x44);
                    return;
                }
                if (s.Length < 3) { GameActions.Print("Need a name.", 0x21); return; }
                string name = string.Join(" ", s, 2, s.Length - 2);
                if (sub == "save") QuickLoadoutManager.SaveCurrent(name);
                else if (sub == "load") QuickLoadoutManager.LoadSet(name);
                else if (sub == "del") QuickLoadoutManager.Delete(name);
                else GameActions.Print("Unknown subcommand.", 0x21);
            });

            Register("mobhp", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    UI.CombatMobHpBars.Range = UI.CombatMobHpBars.Range > 0 ? 0 : 12;
                    GameActions.Print($"Mob HP bars {(UI.CombatMobHpBars.Range > 0 ? $"ON (range {UI.CombatMobHpBars.Range})" : "OFF")}.",
                        (ushort)(UI.CombatMobHpBars.Range > 0 ? 0x35 : 0x21));
                    return;
                }
                if (int.TryParse(s[1], out int r))
                {
                    UI.CombatMobHpBars.Range = System.Math.Max(0, r);
                    GameActions.Print($"Mob HP bars {(UI.CombatMobHpBars.Range > 0 ? $"ON (range {UI.CombatMobHpBars.Range})" : "OFF")}.",
                        (ushort)(UI.CombatMobHpBars.Range > 0 ? 0x35 : 0x21));
                }
                else GameActions.Print("Usage: -mobhp <range>  (0=off)", 0x21);
            });

            Register("poisonalert", (s) =>
            {
                bool on = s == null || s.Length < 2
                    ? !PoisonAlertManager.Enabled
                    : s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase);
                PoisonAlertManager.SetEnabled(on);
            });

            Register("autobuff", (s) =>
            {
                if (s == null || s.Length < 2)
                {
                    GameActions.Print("Usage: -autobuff <buffname> <spellname>  |  -autobuff off", 0x21);
                    return;
                }
                if (s[1].Trim().Equals("off", System.StringComparison.OrdinalIgnoreCase)) { AutoBuffManager.Disable(); return; }
                if (s.Length < 3)
                {
                    GameActions.Print("Need both buff and spell name. Example: -autobuff MagicReflection \"Magic Reflection\"", 0x21);
                    return;
                }
                string buff = s[1];
                string spell = string.Join(" ", s, 2, s.Length - 2);
                AutoBuffManager.Arm(buff, spell);
            });

            Register("automation", (s) =>
            {
                bool enabled = s != null && s.Length >= 2
                    ? s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase)
                    : !AutomationCoordinator.Enabled;
                AutomationCoordinator.SetEnabled(enabled);
                if (ProfileManager.CurrentProfile != null)
                    ProfileManager.CurrentProfile.AutomationEnabled = enabled;
            });

            Register("nativechat", (s) =>
            {
                if (ProfileManager.CurrentProfile == null) return;
                bool enabled = s != null && s.Length >= 2
                    ? s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase)
                    : !ProfileManager.CurrentProfile.UseNativeGlobalChatReplacement;
                ProfileManager.CurrentProfile.UseNativeGlobalChatReplacement = enabled;
                ProfileManager.CurrentProfile.NativeGlobalChatPreferenceVersion = 1;
                GameActions.Print($"Native global-chat replacement {(enabled ? "ON" : "OFF")}.", (ushort)(enabled ? 0x35 : 0x21));
            });

            Register("repairauto", (s) =>
            {
                if (ProfileManager.CurrentProfile == null) return;
                bool enabled = s != null && s.Length >= 2
                    ? s[1].Trim().Equals("on", System.StringComparison.OrdinalIgnoreCase)
                    : !ProfileManager.CurrentProfile.EnableRepairBenchAutomation;
                ProfileManager.CurrentProfile.EnableRepairBenchAutomation = enabled;
                GameActions.Print($"Repair-bench automation {(enabled ? "ON" : "OFF")}.", (ushort)(enabled ? 0x35 : 0x21));
            });

            Register("diagnostics", (s) =>
            {
                if (s != null && s.Length >= 3 &&
                    s[1].Equals("reenable", System.StringComparison.OrdinalIgnoreCase))
                {
                    string name = string.Join(" ", s, 2, s.Length - 2);
                    bool ok = FeatureDiagnostics.Reenable(name);
                    GameActions.Print(ok ? $"Re-enabled '{name}'." : $"No diagnostic entry named '{name}'.", ok ? (ushort)0x35 : (ushort)0x21);
                    return;
                }

                FeatureDiagnostics.Entry[] entries = FeatureDiagnostics.Snapshot();
                if (entries.Length == 0)
                {
                    GameActions.Print("TazUO diagnostics: no feature failures.", 0x35);
                    return;
                }

                foreach (FeatureDiagnostics.Entry entry in entries)
                    GameActions.Print($"{entry.Name}: failures={entry.FailureCount}, disabled={entry.Disabled}, last={entry.LastError}",
                        (ushort)(entry.Disabled ? 0x21 : 0x44));
            });

            // Load aliases after all built-ins so aliases can target commands
            // declared near the end of this method.
            CommandAliasManager.Load();
            CommandMetadata.Synchronize(_commands.Keys);
        }

        private static void SetGumpOpacity(string[] args)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null)
            {
                GameActions.Print("Not in game.", 0x21);
                return;
            }

            const string usage = "Usage: -gumpopacity <all|custom|paperdoll|durability|container|corpse|gridborder|journal|buff|slayer|hovermin> <percent> | <altscroll|hoverboost> [on|off|toggle]";
            if (args == null || args.Length < 2 || args[1].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                GameActions.Print($"Opacity: custom {profile.CustomGumpOpacity}%, paperdoll {profile.PaperdollOpacity}%, durability {profile.DurabilityGumpOpacity}%, container {profile.ContainerOpacity}%, corpse {profile.CorpseContainerOpacity}%, gridborder {profile.GridBorderAlpha}%.", 0x35);
                GameActions.Print($"Journal {profile.JournalOpacity}%, buff {profile.BuffBarOpacity}%, slayer {profile.SlayerBarOpacity}%, hover minimum {profile.GumpHoverOpacityPercent}%.", 0x35);
                GameActions.Print($"Alt-scroll {(profile.EnableAlphaScrollingOnGumps ? "ON" : "OFF")}; hover boost {(profile.BoostGumpOpacityOnHover ? "ON" : "OFF")}.", 0x35);
                GameActions.Print(usage, 0x35);
                return;
            }

            SetGumpOpacityOption(args[1], args, 2);
        }

        private static void SetGumpOpacityOption(string area, string[] args, int valueIndex)
        {
            Profile profile = ProfileManager.CurrentProfile;
            if (profile == null)
            {
                GameActions.Print("Not in game.", 0x21);
                return;
            }

            area = area.Trim().ToLowerInvariant();
            int argCount = args?.Length ?? 0;
            if (area == "all")
            {
                if (argCount != valueIndex + 1 || !int.TryParse(args[valueIndex], out int allPercent) || allPercent < 0 || allPercent > 100)
                {
                    GameActions.Print("Usage: -gumpopacityall <0-100> (or -gumpopacity all <0-100>).", 0x21);
                    return;
                }

                profile.DurabilityGumpOpacity = (byte)allPercent;
                profile.PaperdollOpacity = (byte)allPercent;
                profile.ContainerOpacity = (byte)allPercent;
                profile.CorpseContainerOpacity = (byte)allPercent;
                profile.JournalOpacity = (byte)allPercent;
                profile.BuffBarOpacity = (byte)allPercent;
                profile.SlayerBarOpacity = (byte)allPercent;

                CustomGumpThemeManager.SetOpacity(allPercent);
                GridContainer.UpdateAllGridContainers();
                ContainerGump.UpdateAllCorpseOpacity();
                ResizableJournal.UpdateJournalOptions();
                PaperDollBackpackEquipmentGump.UpdateAllOptions();
                profile.Save(ProfileManager.ProfilePath, false);
                CustomGumpThemeManager.RefreshOptionsGump();
                GameActions.Print($"Gump opacity: {allPercent}% (custom {profile.CustomGumpOpacity}%; grid borders and hover unchanged).", 0x35);
                return;
            }

            if (area == "altscroll" || area == "hoverboost")
            {
                bool current = area == "altscroll"
                    ? profile.EnableAlphaScrollingOnGumps
                    : profile.BoostGumpOpacityOnHover;
                string mode = argCount == valueIndex ? "toggle"
                    : argCount == valueIndex + 1 ? args[valueIndex].Trim().ToLowerInvariant() : string.Empty;
                if (mode != "on" && mode != "off" && mode != "toggle")
                {
                    GameActions.Print($"Usage: -{(argCount > 0 ? args[0] : "gumpopacity" + area)} [on|off|toggle]", 0x21);
                    return;
                }

                bool enabled = mode == "on" || mode == "toggle" && !current;
                if (area == "altscroll") profile.EnableAlphaScrollingOnGumps = enabled;
                else profile.BoostGumpOpacityOnHover = enabled;
                profile.Save(ProfileManager.ProfilePath, false);
                CustomGumpThemeManager.RefreshOptionsGump();
                GameActions.Print($"{area} {(enabled ? "ON" : "OFF")}.", 0x35);
                return;
            }

            int currentPercent;
            int minimum = 0;
            Action<int> apply;
            switch (area)
            {
                case "custom":
                case "utility":
                case "chat":
                    currentPercent = profile.CustomGumpOpacity;
                    minimum = 20;
                    apply = CustomGumpThemeManager.SetOpacity;
                    break;
                case "durability":
                    currentPercent = profile.DurabilityGumpOpacity;
                    apply = value => { profile.DurabilityGumpOpacity = (byte)value; DurabilitysGump.UpdateAllOpacity(); };
                    break;
                case "paperdoll":
                    currentPercent = profile.PaperdollOpacity;
                    apply = value => profile.PaperdollOpacity = (byte)value;
                    break;
                case "container":
                case "containers":
                    currentPercent = profile.ContainerOpacity;
                    apply = value => { profile.ContainerOpacity = (byte)value; GridContainer.UpdateAllGridContainers(); };
                    break;
                case "corpse":
                case "corpsecontainer":
                    currentPercent = profile.CorpseContainerOpacity;
                    apply = value =>
                    {
                        profile.CorpseContainerOpacity = (byte)value;
                        GridContainer.UpdateAllGridContainers();
                        ContainerGump.UpdateAllCorpseOpacity();
                    };
                    break;
                case "gridborder":
                    currentPercent = profile.GridBorderAlpha;
                    apply = value => profile.GridBorderAlpha = (byte)value;
                    break;
                case "journal":
                    currentPercent = profile.JournalOpacity;
                    apply = value => { profile.JournalOpacity = (byte)value; ResizableJournal.UpdateJournalOptions(); };
                    break;
                case "buff":
                case "buffbar":
                    currentPercent = profile.BuffBarOpacity;
                    apply = value => profile.BuffBarOpacity = (byte)value;
                    break;
                case "slayer":
                case "equipment":
                    currentPercent = profile.SlayerBarOpacity;
                    apply = value => { profile.SlayerBarOpacity = (byte)value; PaperDollBackpackEquipmentGump.UpdateAllOptions(); };
                    break;
                case "hovermin":
                case "hoverminimum":
                    currentPercent = profile.GumpHoverOpacityPercent;
                    apply = value => profile.GumpHoverOpacityPercent = (byte)value;
                    break;
                default:
                    GameActions.Print($"Unknown gump opacity option: {area}.", 0x21);
                    return;
            }

            if (argCount == valueIndex)
            {
                GameActions.Print($"{area} opacity: {currentPercent}%.", 0x35);
                return;
            }

            if (argCount != valueIndex + 1 || !int.TryParse(args[valueIndex], out int percent) || percent < minimum || percent > 100)
            {
                GameActions.Print($"{area} opacity must be {minimum}-100%.", 0x21);
                return;
            }

            apply(percent);
            profile.Save(ProfileManager.ProfilePath, false);
            CustomGumpThemeManager.RefreshOptionsGump();
            GameActions.Print($"{area} opacity: {percent}%.", 0x35);
        }

        private static bool TryParseUshort(string str, out ushort val)
        {
            val = 0;
            if (string.IsNullOrEmpty(str)) return false;
            if (str.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
                return ushort.TryParse(str.Substring(2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out val);
            return ushort.TryParse(str, out val);
        }

        private static int CountInWornBags(ushort graphic, ushort hue)
        {
            if (World.Player == null) return 0;
            int total = 0;
            for (Game.GameObjects.Item bag = (Game.GameObjects.Item)World.Player.Items; bag != null; bag = (Game.GameObjects.Item)bag.Next)
            {
                if (bag.ItemData.IsContainer && !bag.IsEmpty &&
                    bag.Layer >= Game.Data.Layer.OneHanded && bag.Layer <= Game.Data.Layer.Legs)
                    total += Count(bag, graphic, hue);
            }
            return total;
        }

        private static int Count(Game.GameObjects.Item parent, ushort graphic, ushort hue)
        {
            int total = 0;
            for (Game.LinkedObject i = parent.Items; i != null; i = i.Next)
            {
                Game.GameObjects.Item it = (Game.GameObjects.Item)i;
                if (it.Graphic == graphic && (hue == 0 || it.Hue == hue) && it.Exists) total += it.Amount;
                if (it.ItemData.IsContainer && !it.IsEmpty) total += Count(it, graphic, hue);
            }
            return total;
        }

        /// <summary>
        /// Scans nearby mobiles, picks the closest hostile (Criminal / Enemy /
        /// Murderer / Attackable), sets it as LastAttack target, and optionally
        /// fires an Attack request.
        /// </summary>
        private static void TargetOrAttackNearestEnemy(bool attack)
        {
            if (World.Player == null || !World.InGame)
            {
                GameActions.Print("Not in game.", 0x21);
                return;
            }

            Mobile best = null;
            int bestDist = int.MaxValue;
            int range = World.ClientViewRange;

            foreach (var m in World.Mobiles.Values)
            {
                if (m == null || m.IsDestroyed) continue;
                if (m == World.Player) continue;
                if (m.IsDead) continue;
                var n = m.NotorietyFlag;
                if (n != Data.NotorietyFlag.Criminal &&
                    n != Data.NotorietyFlag.Enemy &&
                    n != Data.NotorietyFlag.Murderer &&
                    n != Data.NotorietyFlag.Gray)
                    continue;

                int d = m.Distance;
                if (d > range) continue;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = m;
                }
            }

            if (best == null)
            {
                GameActions.Print("No hostile target in range.", 0x21);
                return;
            }

            TargetManager.LastAttack = best.Serial;
            if (attack)
            {
                GameActions.Attack(best.Serial);
                GameActions.Print($"Attacking {best.Name ?? "<unknown>"} (d={bestDist}).", 0x35);
            }
            else
            {
                GameActions.Print($"Targeted {best.Name ?? "<unknown>"} (d={bestDist}).", 0x35);
            }
        }


        public static void Register(string name, Action<string[]> callback)
        {
            name = name.ToLower();

            if (!_commands.ContainsKey(name))
            {
                _commands.Add(name, callback);
            }
            else
            {
                Log.Error($"Attempted to register command: '{name}' twice.");
            }
        }

        public static void UnRegister(string name)
        {
            name = name.ToLower();

            if (_commands.ContainsKey(name))
            {
                _commands.Remove(name);
            }
        }

        public static void UnRegisterAll()
        {
            _commands.Clear();
        }

        public static void Execute(string name, params string[] args)
        {
            name = name.ToLower();

            if (_commands.TryGetValue(name, out Action<string[]> action))
            {
                CommandHistoryManager.Record(name, args);
                action.Invoke(args);
                CommandMetadata.RecordExecution(name, args);
            }
            else
            {
                GameActions.Print(string.Format(Language.Instance.ErrorsLanguage.CommandNotFound, name));
                Log.Warn($"Command: '{name}' not exists");
            }
        }

        public static void OnHueTarget(Entity entity)
        {
            if (entity != null)
            {
                TargetManager.Target(entity);
            }

            Mouse.LastLeftButtonClickTime = 0;
            GameActions.Print(string.Format(ResGeneral.ItemID0Hue1, entity.Graphic, entity.Hue));
        }
    }
}

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal class VersionHistory : NineSliceGump
    {
        // Add the new release notes here whenever CUOEnviroment.Version changes.
        private static readonly string[] updateTexts =
        {
            "[0.6.2]\n" +
            """
            Graphics and visual presentation
            - Magnifying-glass cursor: added illustrated UO artwork with adjustable 1x–4x magnification, saved per-character zoom and a central crosshair for precise item selection. Activate using -magnifier, -magnifier 1 through -magnifier 4, or a ClientCommand macro hotkey.
            - Magnifier controls: Escape disables the magnifier. The lens displays an opaque image and supports optional smooth magnification. Mouse targeting coordinates remain unchanged.
            - Pixel-art scaling: added a pixel-art processing filter with adjustable sharpness alongside the existing point, linear, anisotropic and xBR options.
            - World anti-aliasing: added optional FXAA with adjustable strength. Processing occurs before overhead text, healthbars and interface rendering, keeping those elements sharp.
            - Lighting controls: added optional linear-light composition, coordinated atmospheric light colors, selective bloom around emissive sources and refined contact shadows.
            - Particle controls: added configurable visual budgets, adaptive optional particle density and softer particle contacts with scenery.
            - Texture caching: added an optional bounded texture cache.
            - Local artwork packs: added support for optional local replacement-art packs.
            - Body scaling: now disabled by default for new settings while preserving existing saved character preferences.
            - Rendering fixes: corrected black hues remaining after disabling linear-light composition. Fixed background and magnifier rendering across lighting and anti-aliasing passes.
            - GPU resource handling: rendering buffers are reused and cleaned up when resizing, disabling effects or logging out.

            Gump themes and interface
            - 21 illustrated premium themes: added Sovereign Gold, Lunar Silver, Dragon Ember, Verdant Cathedral, Runic Obsidian, Ivory Citadel, Stormforged, Sunken Treasury, Astral Observatory, Crimson Velvet, Jade Dynasty, Amber Alchemist, Glacial Crown, Phoenix Imperial, Sapphire Reliquary, Ancient Sandstone, Pearl Sanctum, Nocturne, Prismatic Vault, Rose Quartz Court and Eternal Eclipse.
            - Theme selection: added favorites, per-window theme overrides and reduced-decoration options.
            - Daily theme rotation: optionally advance to the next available theme once per local day, or on the next login. Per-window overrides remain fixed.
            - Theme previews: corrected the selector so cards display their respective themes.
            - Theme cleanup: retired palette-only styles while retaining Minimal and TazUO.
            - Interface scaling: added independent scaling for supported interface windows.
            - Window controls: improved resize and lock grips.
            - Enhancements options: corrected category layout, content positioning and scrolling.
            - Settings search: expanded search synonyms to make relevant options easier to find.
            - Settings history: added saved settings history with selective undo.
            - Compact status windows: added horizontal and vertical layouts showing health, mana, stamina, stats and weight. Select a layout in Options → General; Alt+left-click a compact status window to change its layout.
            - Native label appearance: retained original label rendering without automatic rectangular backing plates.

            Weather, ambience and spell effects
            - Ambient weather: expanded biome-, season- and time-aware weather behavior and gradual transitions between compatible conditions.
            - Fog: improved world anchoring and fixed gaps at viewport edges when zooming or scaling.
            - Environmental detail: added shared wind, seasonal details and coordinated atmospheric presentation.
            - Storm controls: added separate controls for lightning, thunder and screen shake.
            - Regional soundscapes: added Classic UO, Britannian Wilderness and Quiet Exploration presets.
            - Local soundscape packs: added optional local regional audio packs with asynchronous loading and native UO fallback for missing or invalid regions.
            - Spell and combat effects: added per-effect intensity, density and glow controls, smoother effect geometry and distant-effect decluttering.
            - Effect previews: added local classic/enhanced comparison previews.

            Inventory and container tools
            - Bulk selection: select grid-container items by equipment layer, graphic or name, or select all items, before adding them to Multi Move. Access through Alt+left-click on the original-container-view icon.
            - Configurable grid sections: group items using weapon, armor, reagent, equipment-layer, graphic or name rules. Configure backpack, corpse and other-container categories separately, with per-container overrides.
            - Locked slots: bulk selection and section presentation preserve locked slot positions.
            - Container breadcrumbs: click a grid-container title or an Item Finder location label to navigate known parent containers. Unavailable parents are identified as last-known locations.
            - Item comparison: enhanced the existing Ctrl-hover comparison with enlarged native item artwork, hue swatches and complete candidate/equipped tooltips side by side.
            - Comparison appearance: uses configured tooltip colors, font and background in one content-sized gump without scrolling.
            - Comparison controls: fixed Pin/Unpin click handling and premature closing when hovering child controls. Removed the unnecessary Rows/full tooltips toggle. Right-click and Escape still close the gump.
            - Ground-drop preview: optionally show translucent item artwork at the proposed drop tile and elevation, with a range indicator. The server remains responsible for accepting the drop.
            - Restock preview: show planned moves and shortages before execution.
            - Restock allocation: prevent overlapping rules from reserving the same stock more than once.
            - Queued operations: added shared progress reporting for Multi Move, Organizer and Restock. Open the panel with -operations.
            - Cancellation: cancel requests that have not yet been sent. Closing the progress window does not cancel an operation.
            - Grid placement: reduced repeated free-slot scans while preserving locked slots and item order.

            Action bars, shortcuts and cooldowns
            - Expanded counter/action bar: cells can now run skills, primary and secondary weapon abilities, macros and Restock loadouts alongside existing item and spell actions.
            - Action feedback: weapon-ability cells display the current ability icon and active state.
            - Shortcut labels: optionally display assigned shortcut labels on counter/action-bar cells.
            - Direct shortcut assignment: Ctrl+Shift+click supported spell, skill, ability and macro icons, or supported paperdoll Feature Tools actions, to assign a shortcut.
            - Binding controls: capture, confirm or clear bindings, with checks for supported client macro and spell-bar conflicts. Razor Enhanced bindings remain managed separately.
            - Cooldown repeat handling: added Keep existing, allowing repeated triggers to leave an active timer running. Existing add-another and replace-existing behavior remains available.
            - Cooldown rule identity: stable saved rule IDs distinguish rules with identical names or triggers.

            Healthbars and nameplates
            - Permanent healthbar drag filters: added one active default filter with individual modifier-key overrides: All Mobiles, Players only, Friendly Players only, Guild only, Hostile Mobiles only, Grey and Hostile Mobiles only, and Neutral Mobiles only.
            - Notoriety filtering: filters use the specified notoriety values; All Mobiles includes every notoriety.
            - Healthbar cleanup: removed the B button.
            - Crowded nameplates: improved overlap avoidance with bounded placement lanes and leader lines. Last target, hovered entities and party members receive priority; manually locked labels retain their positions.

            Chat, alerts and equipment switching
            - Original Global Chat access: -nativechat open opens the original UOAlive Global Chat regardless of the replacement-chat preference, without changing that setting.
            - Native chat history: with replacement enabled, bare [c opens the original chat history; [c message continues to post normally.
            - Chat mentions: added configurable local mention highlighting.
            - Chat history resources: bounded visible rows to retained history and released expired text resources.
            - Chat resizing: reflow messages while preserving the reading position.
            - Pet loyalty alerts: restrict checks to pets for which the player has rename permission and ignore unrelated pet messages.
            - Bandage alerts: suppress zero-bandage warnings for non-users until bandage stock has been observed or bandage automation is enabled.
            - Muted alerts: muting or snoozing pet and bandage alerts also silences their sounds.
            - Shield restoration: remember shields displaced by a two-handed weapon and restore them when switching back to a one-handed weapon or spellbook. Preserve another off-hand item subsequently equipped by the player or Razor Enhanced.

            Maps and travel
            - World-map loading: moved loading into timed main-thread slices, cancelled stale work when changing maps or sessions, and improved texture disposal.
            - Marker names: added an option to show marker names at every zoom level, with bounded label counts, overlap suppression and hover fallback.
            - Marker CSV: added support for quoted fields, multiline names and culture-safe coordinates while preserving custom zoom settings.
            - Travel-source identification: World Explorer displays source names, serials and rune slots to distinguish destinations with identical names.
            - Travel-source details: added observed source hue and scan-time information; unavailable facet information remains unknown.
            - Preferred travel sources: save a preferred source for matching destination names. Travel still uses the explicitly selected entry.

            Performance and stability
            - Incremental Item Finder search: added cached query plans and a text candidate index, updated when observed item data changes. Queries unsuitable for indexing retain full evaluation.
            - Shared inventory counts: counters, Restock/readiness checks and reagent/bandage watchers reuse observed inventory snapshots, invalidated when relevant items or sessions change.
            - Background screenshots: PNG encoding and disk writes run on a worker after GPU capture. Pending captures are bounded, files complete atomically, and pending saves finish on exit.
            - Safe layout saving: save gump layouts atomically and preserve the previous file if saving fails.
            - Container traversal: guard missing and cyclic container links.
            - Session cleanup: reset stale feature failures and pending notices between sessions.
            - Performance diagnostics: corrected draw-cadence measurements and slowest-1% FPS calculations.
            - Binary and network handling: strengthened reader bounds, packet-field writing and Huffman streaming.
            - Asset validation: improved animation, map and texture-atlas validation.
            - Resource management: corrected resource lifetimes and collection retention; reduced repeated work and allocations.
            - Property parsing: improved culture-safe item-property handling.
            - Utility fixes: corrected issues in collection views, averaging, logging, regular-expression handling and string-builder replacement.

            Razor Enhanced and installation
            - Plugin picker: choose Razor Enhanced through Options on Windows and save the selected plugin path to settings.json while preserving other settings. Restart the client to load it.
            - Supported platform: Windows x64, .NET Framework 4.7.2, using the existing pinned SDL3/FNA runtime.
            - Replace the complete client files when upgrading so new artwork and shaders are included.
            - Back up the existing client folder and profiles before upgrading.
            """ + "\n",
            "[0.6.1]\n" +
            """
            1. Chat channels: Implemented the new UOAlive Chat channels including color code! View/Send selectors for Global, Trade, Events, Help, LFG, Guild and Party, with correct posting commands. All shows public channels.
            2. Chat restoration: Global Chat, Guild Chat and Nearby Speech windows reopen after login.
            3. Equipment sorting: backpack/container grid views can sort now also by equipment layer, place non-equipment last while preserving locked slots.
            4. Game window fixes: now resize (blue dot) and relocation (grab the edges) of the game window is working.
            5. Runic Atlas fixes: World Explorer now scans the Runic Atlases correctly.
            """,
            "[0.6]\n" +
            """
            - Added an in-game chess board for local two-player games or play against a separately installed Stockfish engine, with piece tooltips and visible computer moves
            - Recreated the login, shard, character-selection, and loading screens with high-resolution art and revised layouts
            - Added HD gump themes and paperdoll skins, including wood, metal, marble, glass, and stained glass; new profiles default to HD Wood gumps
            - Expanded the paperdoll tools panel with links to client features and durability tracking
            - Added equipment insurance checks to Restock Agent readiness
            - Stopped Item Finder scans from opening spellbooks, bulk-order books, runebooks, atlases, and other books as containers while keeping them searchable as items
            - Improved nameplate-window reopening, chat and nearby-loot UI behavior, and client performance
            """ +
            "\n",
            "[0.6 Beta]\n" +
            """
            - Fix update: Item Finder scans nested containers, preserves its catalog between scans, skips temporary Arcane Focus items, and warns when a located item has moved
            - Equipment Guru scores all eligible items as swaps, improves its shortlist and jewelry search, and reports missing item properties; results update when Analyze gear is clicked
            - Equipment Guru projects stamina from DEX, hit points from STR, and inherent armor LMC; grouped stats use illustrated icons and comparisons show numeric benefits and tradeoffs
            - Updated Equipment Guru with MIN/KEEP targets, stat caps, individual resist and regeneration goals, current-equipment comparison, and live transformation penalties
            - Color-coded Item Finder property tooltips and refreshed Restock Agent source stock when its window opens
            - Added a native-art TOOLS button and a movable, position-saving tools panel to the paperdoll; kept the launcher opaque and themed button borders visible on hover
            - Added Universal Item Finder with persistent area scans, multi-property ALL/NOT/count queries, saved searches, full property help, and item or house-position location tracking
            - Preserved catalog items from unreachable containers during later scans and closed container gumps opened by scanning
            - Added Restock Agent with multiple ordered sources, supply presets, custom destination containers, live stock counts, reusable loadouts, and integrated readiness checks
            - Added Equipment Guru with editable build and skill targets, three recommended loadouts, clear item comparisons, fixed weapon/spellbook/talisman rules, and race-compatible candidates
            - Added Alert Center history, severity filters, snooze, mute, source suppression, and category settings
            - Made low durability, low pet loyalty, and legendary creature alerts persistent until right-clicked; recurring warnings return after ten minutes
            - Added dedicated UO-style art and controls for Item Finder, Restock Agent, Equipment Guru, and Alert Center
            - Added paperdoll quick-access buttons for the new tools and World Explorer
            - Extended opacity controls to the world-map border, main menu, buff bar, compact World Explorer, and independently controlled paperdoll
            - Improved critical durability rows with deep-plum styling, violet borders and bars, red Repair actions, and a brief first-warning pulse
            - Renamed the primary nearby speech command to -nearbychat while keeping -speechhistory as an alias
            - Fixed Global and Guild Chat input layout and retained separate auto-open settings for Global, Guild/Alliance, and Nearby Chat
            - Restored compact World Explorer after restart when it was previously open
            - Allowed pinned command groups to be detached with Alt
            """ +
            "\n",
            "[0.5.2]\n" +
            """
            - Restored compact World Explorer buttons while retaining the 1-4 column layout
            - Added -petguardtint on|off to gray out pets that are not guarding you
            - Added per-profile auto-open options for Global Chat, Guild and Alliance Chat, and Nearby Speech in Speech settings
            - Closing a chat window now disables its auto-open setting; reopening it manually enables it again
            """ +
            "\n",
            "[0.5.1]\n" +
            """
            - Made compact World Explorer fit its pinned destinations, with 1-4 columns, narrower buttons, and wrapped labels
            - Added -gumpopacityall and an options control to set supported gump opacity values together; grid borders and hover remain separate
            - Applied custom gump opacity to macro buttons
            - Corrected themed equipment-durability row spacing
            - Removed pet-bandage success toasts
            - Fixed a crash when traveling through a runebook with empty gump response fields
            """ +
            "\n",
            "[0.5]\n" +
            """
            - Added custom gump themes and improved decorative-border spacing and macro-button readability
            - Added World Explorer with rune and rune-book scanning, pinned destinations, and travel options
            - Added per-area gump opacity commands and controls
            - Improved Razor Enhanced hotkey labels on macro buttons
            - Added a Windows system-DPI default with an optional native-DPI mode
            - Moved themed toast alerts to the top center with a movable, resizable anchor
            - Added low-tithing toast alerts for Chivalry users
            """ +
            "\n",
            "[0.4]\n" +
            """
            - Updated the graphics and input stack from SDL2 to pinned SDL3/FNA while retaining .NET Framework 4.7.2 and Razor Enhanced compatibility
            - Added native graphics runtime details to crash diagnostics
            - Restored AltGr as Ctrl+Alt for Razor Enhanced hotkeys
            - Preserved Ctrl-click pinned backpack and grid-container items in their selected cells across restarts
            - Made corpse-container and Grid Loot positions persist reliably across restarts
            - Fixed a logout crash while grid-container layouts were being saved
            - Bundled the official TazUO fonts and restored Chakra Petch
            """ +
            "\n",
            "[0.3]\n" +
            """
            - Added nearby player-speech history with session clearing
            - Added nearby-speech filters for the player and owned pets
            - Removed the speech-composer field from nearby speech history
            - Added adjustable utility and chat gump background opacity while preserving container opacity
            - Added separate corpse-container and buff/debuff-bar opacity controls
            - Added durability-gump opacity and an absolute 0-100% hover-opacity target
            - Consolidated individual gump-opacity controls on a dedicated options page
            - Showed a gump's real opacity while Alt is held for opacity adjustment
            - Excluded classic and modern paperdolls from hover-opacity changes
            - Added slayer/equipment-bar opacity and collapse controls
            - Extended Alt+mouse-wheel opacity adjustment through modern gump child controls
            - Preserved Alt-scroll opacity when script gumps rebuild their controls
            - Fixed zero-opacity corpse grid slots and buff-bar hover flickering
            - Applied buff-bar hover opacity consistently across all rows without fading icons or text
            - Saved the last corpse-container position across client restarts
            - Matched the durability-gump background to the selected custom theme
            - Added freshness-aware tithing display and low-tithing warnings for Chivalry users
            - Added value-based performance HUD colors
            - Kept Perf HUD ping values green through 150 ms
            - Wrapped long Perf HUD stall diagnostics within the panel
            - Improved login-screen alignment for the MW Edition logo and version information
            - Made the base -speechhistory command directly pinnable in the command palette
            - Added an expanded performance HUD with frame time, 1% low FPS, ping/jitter history, network queue, GC, and stall diagnostics
            - Added profile and gump-layout recovery snapshots with an in-game recovery gump
            - Added safe graphics startup mode with the -safegraphics argument
            - Added startup checks for missing or incomplete Ultima Online data
            - Added visible surface-aware footstep effects for water, snow, grass, sand, mud, dirt, wood, stone, mines, and dungeons
            - Added mount-aware boot, hoof, claw, and large-creature tracks with surface particles
            - Added selectable blood, shadow, arcane, fire, frost, poison, holy, necromantic, lightning, petal, leaf, rune, stardust, ethereal, rainbow, and lava movement trails
            - Replaced generated fantasy-trail symbols with varied native UO artwork
            - Reworked inconsistent fantasy trails with larger coherent native-UO effect frames and removed tree artwork from Falling Leaves
            - Increased Fire, Poison, and Lava Cracks trails to normal effect size
            - Added the -trailfx gump for trail style, intensity, lifetime, surface-track, and particle controls
            - Preserved the trail-effects gump position when changing its options
            - Disabled blood splatter, blood trail, and blood ground decals by default
            - Reduced unnecessary feature-settings disk writes
            - Added event-handler fault isolation so one optional feature cannot interrupt other handlers
            - Added incoming network queue limits to prevent runaway memory use during stalls

            Bug fixes
            - Fixed startup recovery when global settings cannot be loaded
            - Fixed disabled movement trails continuing to collect positions
            - Fixed legacy blood-trail settings remaining enabled after an upgrade
            - Fixed footstep decals disappearing when the full ambience overlay is disabled
            - Fixed Alt+mouse-wheel and hover opacity changes fading gump text
            - Fixed zero-opacity gumps retaining a forced 10% background
            - Fixed corpse grid slots ignoring corpse-container opacity
            - Fixed buff-bar hover opacity flickering
            """ +
            "\n",
            "[0.21]\n" +
            """
            - Replaced the original TazUO history with MW Edition release notes
            - Added a centered TazUO MW Edition GitHub link to the version-history gump
            - Updated the version-history title and version formatting
            """ +
            "\n",
            "[0.2]\n" +
            """
            - Improved client performance and reduced render-loop allocations
            - Optimized network processing and improved reconnect stability
            - Added a session-log option; logging is disabled by default
            - Added main-thread hang diagnostics and safer log handling
            - Improved malformed cliloc and PNG artwork error handling
            - Improved external artwork loading and caching
            - Added low-durability equipment highlighting
            - Improved skill-gain diagnostics
            - Disabled Legion/Python scripting while retaining its implementation
            - Removed built-in Discord, anonymous metrics, update notifier, and UOAssist compatibility
            - Added automated Windows tests, publishing, and release artifacts

            Bug fixes
            - Fixed quoted text parsing
            - Fixed settings initialization when the executable path is unavailable
            - Added packet, configuration, durability, and hang-diagnostic regression tests
            """ +
            "\n",
            "[0.1]\n" +
            """
            - Initial TazUO MW Edition release based on TazUO 4.5.22.0
            - Added TazUO MW Edition branding and version information
            - Added the MW Edition logo to the login screen
            - Added the TazUO MW Edition GitHub link to the login screen
            """ +
            "\n"
        };

        private ScrollArea _scrollArea;
        private VBoxContainer _vBoxContainer;
        private CustomGumpTheme _theme;

        public VersionHistory() : base(0, 0, 400, 500, ModernUIConstants.ModernUIPanel, ModernUIConstants.ModernUIPanel_BoderSize, true, 200, 200)
        {
            CanCloseWithRightClick = true;
            CanMove = true;
            _theme = CustomGumpThemeManager.Current;

            Build();

            CenterXInViewPort();
            CenterYInViewPort();
        }

        private void Build()
        {
            Clear();
            DrawNineSliceBackground = _theme == CustomGumpTheme.TazUO;
            if (!DrawNineSliceBackground)
            {
                AlphaBlendControl background = new(0.95f)
                {
                    Width = Width,
                    Height = Height,
                    ArtPanel = true
                };
                CustomGumpThemeManager.ApplyDataSurface(background, 0.95f);
                Add(background);
            }

            int inset = CustomThemeArt.ContentInset;
            int contentWidth = Width - 26 - inset * 2;
            bool lightTheme = _theme == CustomGumpTheme.BritannianChronicle || _theme == CustomGumpTheme.HdMarble;
            Color textColor = lightTheme ? Color.Black : Color.Orange;
            Color titleColor = lightTheme || _theme == CustomGumpTheme.Classic
                ? Color.Black : Color.White;
            Positioner pos = new(13 + inset, 13);
            pos.Y += inset;

            Add(pos.Position(TextBox.GetOne(Language.Instance.TazuoVersionHistory, TrueTypeLoader.EMBEDDED_FONT, 22, titleColor, TextBox.RTLOptions.DefaultCentered(contentWidth))));

            Add(pos.Position(TextBox.GetOne(Language.Instance.CurrentVersion + CUOEnviroment.DisplayVersion, TrueTypeLoader.EMBEDDED_FONT, 20, textColor, TextBox.RTLOptions.DefaultCentered(contentWidth))));

            int footerTop = Height - 43 - inset;
            _scrollArea = new ScrollArea(0, 0, contentWidth, System.Math.Max(1, footerTop - pos.Y - 6), true) { ScrollbarBehaviour = ScrollbarBehaviour.ShowAlways };
            _vBoxContainer = new VBoxContainer(_scrollArea.Width - _scrollArea.ScrollBarWidth());
            _scrollArea.Add(_vBoxContainer);

            foreach (string s in updateTexts)
            {
                _vBoxContainer.Add(TextBox.GetOne(s, TrueTypeLoader.EMBEDDED_FONT, 15, textColor, TextBox.RTLOptions.Default(_scrollArea.Width - _scrollArea.ScrollBarWidth())));
            }

            Add(pos.Position(_scrollArea));

            Color linkColor = lightTheme ? new Color(95, 50, 20) : Color.Orange;
            int linkY = Height - 22 - inset;
            Add(pos.PositionExact(new HttpClickableLink(Language.Instance.TazUOWiki, "https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki", linkColor, 15), 13 + inset, footerTop));

            HttpClickableLink githubLink = new("TazUO MW Edition Github", "https://github.com/mike-walker-uo/tazUO-MW-Edition", linkColor, 15);
            Add(pos.PositionExact(githubLink, (Width - githubLink.Width) / 2, linkY));

            Add(pos.PositionExact(new HttpClickableLink(Language.Instance.TazUODiscord, "https://discord.gg/QvqzkB95G4", linkColor, 15), Width - 110 - inset, footerTop));
        }

        public override void Update()
        {
            base.Update();
            if (_theme != CustomGumpThemeManager.Current)
            {
                _theme = CustomGumpThemeManager.Current;
                Build();
            }
        }

        protected override void OnResize(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            base.OnResize(oldWidth, oldHeight, newWidth, newHeight);
            Build();
        }
    }
}

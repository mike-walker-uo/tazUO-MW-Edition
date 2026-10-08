# TazUO MW Edition

Custom Ultima Online client based on the legacy 4.5.22.0 release of tazUO.

[Download version 0.6.2](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/tag/0.6.2)

It has many features, some of them are:
  - Custom weather and light effects
  - Illustrated gump themes, including Eternal Eclipse, Sovereign Gold, Lunar Silver, Dragon Ember, and 17 other premium styles; Minimal and TazUO remain available
  - Custom hit effects
  - Custom spell effects (Chiv, Magery, Myst, SW)
  - Optional custom mob size, disabled by default
  - UO-style magnifying-glass cursor with 1×–4× zoom, precise crosshair and command/hotkey activation
  - Pixel-art upscaling and optional world anti-aliasing while keeping UI and text sharp
  - Custom mob death effect "ghost"
  - Custom music incl. music player (and AncientFM integration)
  - Custom Guild Chat, Global Chat, searchable Journal
  - Custom Bandage Agent
  - Skill Gain tracker, Damage tracker, Death recap
  - Integrated weapon, spellbook, shield, talisman switcher (like slayer bar)
  - Tracking "line" (not just arrow)
  - Nearby speech history with search and filters
  - Surface-aware and fantasy movement trails
  - Performance HUD with ping and jitter history
  - Profile recovery and safe graphics startup
  - Individual opacity controls for supported gumps, with optional Alt + scroll and hover boost
  - World Explorer for scanning rune books, pinning destinations, and quick travel, with 1–4 compact columns
  - Universal Item Finder with persistent area scans, multi-property queries, saved searches, and item location tracking
  - Restock Agent with multiple sources, custom destinations, reusable loadouts, and readiness checks
  - Equipment Guru for target-driven equipment recommendations using the Item Finder catalog
  - Alert Center with history, severity filters, snooze, mute, and persistent critical alerts
  - Optional boss health bar with compact and ornate layouts
  - Tithing-point tracking and low-point warnings for Chivalry users
...and many many more features.

See the [complete client command reference](https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki/Client-Commands) for every built-in command, its usage, and a short explanation. You can also type `-commands` in game to open the searchable command palette.

## Version 0.6.2 — changes since 0.6.1

## Graphics and visual presentation

- **Magnifying-glass cursor:** added illustrated UO artwork with adjustable 1×–4× magnification, saved per-character zoom and a central crosshair for precise item selection. Activate using `-magnifier`, `-magnifier 1` through `-magnifier 4`, or a ClientCommand macro hotkey.
- **Magnifier controls:** Escape disables the magnifier. The lens displays an opaque image and supports optional smooth magnification. Mouse targeting coordinates remain unchanged.
- **Pixel-art scaling:** added a pixel-art processing filter with adjustable sharpness alongside the existing point, linear, anisotropic and xBR options.
- **World anti-aliasing:** added optional FXAA with adjustable strength. Processing occurs before overhead text, healthbars and interface rendering, keeping those elements sharp.
- **Lighting controls:** added optional linear-light composition, coordinated atmospheric light colors, selective bloom around emissive sources and refined contact shadows.
- **Particle controls:** added configurable visual budgets, adaptive optional particle density and softer particle contacts with scenery.
- **Texture caching:** added an optional bounded texture cache.
- **Local artwork packs:** added support for optional local replacement-art packs.
- **Body scaling:** now disabled by default for new settings while preserving existing saved character preferences.
- **Rendering fixes:** corrected black hues remaining after disabling linear-light composition. Fixed background and magnifier rendering across lighting and anti-aliasing passes.
- **GPU resource handling:** rendering buffers are reused and cleaned up when resizing, disabling effects or logging out.

### Gump themes and interface

- **21 illustrated premium themes:** added Sovereign Gold, Lunar Silver, Dragon Ember, Verdant Cathedral, Runic Obsidian, Ivory Citadel, Stormforged, Sunken Treasury, Astral Observatory, Crimson Velvet, Jade Dynasty, Amber Alchemist, Glacial Crown, Phoenix Imperial, Sapphire Reliquary, Ancient Sandstone, Pearl Sanctum, Nocturne, Prismatic Vault, Rose Quartz Court and Eternal Eclipse.
- **Theme selection:** added favorites, per-window theme overrides and reduced-decoration options.
- **Daily theme rotation:** optionally advance to the next available theme once per local day, or on the next login. Per-window overrides remain fixed.
- **Theme previews:** corrected the selector so cards display their respective themes.
- **Theme cleanup:** retired palette-only styles while retaining Minimal and TazUO.
- **Interface scaling:** added independent scaling for supported interface windows.
- **Window controls:** improved resize and lock grips.
- **Enhancements options:** corrected category layout, content positioning and scrolling.
- **Settings search:** expanded search synonyms to make relevant options easier to find.
- **Settings history:** added saved settings history with selective undo.
- **Compact status windows:** added horizontal and vertical layouts showing health, mana, stamina, stats and weight. Select a layout in **Options → General**; Alt+left-click a compact status window to change its layout.
- **Native label appearance:** retained original label rendering without automatic rectangular backing plates.

### Weather, ambience and spell effects

- **Ambient weather:** expanded biome-, season- and time-aware weather behavior and gradual transitions between compatible conditions.
- **Fog:** improved world anchoring and fixed gaps at viewport edges when zooming or scaling.
- **Environmental detail:** added shared wind, seasonal details and coordinated atmospheric presentation.
- **Storm controls:** added separate controls for lightning, thunder and screen shake.
- **Regional soundscapes:** added Classic UO, Britannian Wilderness and Quiet Exploration presets.
- **Local soundscape packs:** added optional local regional audio packs with asynchronous loading and native UO fallback for missing or invalid regions.
- **Spell and combat effects:** added per-effect intensity, density and glow controls, smoother effect geometry and distant-effect decluttering.
- **Effect previews:** added local classic/enhanced comparison previews.

### Inventory and container tools

- **Bulk selection:** select grid-container items by equipment layer, graphic or name, or select all items, before adding them to Multi Move. Access through Alt+left-click on the original-container-view icon.
- **Configurable grid sections:** group items using weapon, armor, reagent, equipment-layer, graphic or name rules. Configure backpack, corpse and other-container categories separately, with per-container overrides.
- **Locked slots:** bulk selection and section presentation preserve locked slot positions.
- **Container breadcrumbs:** click a grid-container title or an Item Finder location label to navigate known parent containers. Unavailable parents are identified as last-known locations.
- **Item comparison:** enhanced the existing Ctrl-hover comparison with enlarged native item artwork, hue swatches and complete candidate/equipped tooltips side by side.
- **Comparison appearance:** uses configured tooltip colors, font and background in one content-sized gump without scrolling.
- **Comparison controls:** fixed Pin/Unpin click handling and premature closing when hovering child controls. Removed the unnecessary Rows/full tooltips toggle. Right-click and Escape still close the gump.
- **Ground-drop preview:** optionally show translucent item artwork at the proposed drop tile and elevation, with a range indicator. The server remains responsible for accepting the drop.
- **Restock preview:** show planned moves and shortages before execution.
- **Restock allocation:** prevent overlapping rules from reserving the same stock more than once.
- **Queued operations:** added shared progress reporting for Multi Move, Organizer and Restock. Open the panel with `-operations`.
- **Cancellation:** cancel requests that have not yet been sent. Closing the progress window does not cancel an operation.
- **Grid placement:** reduced repeated free-slot scans while preserving locked slots and item order.

### Action bars, shortcuts and cooldowns

- **Expanded counter/action bar:** cells can now run skills, primary and secondary weapon abilities, macros and Restock loadouts alongside existing item and spell actions.
- **Action feedback:** weapon-ability cells display the current ability icon and active state.
- **Shortcut labels:** optionally display assigned shortcut labels on counter/action-bar cells.
- **Direct shortcut assignment:** Ctrl+Shift+click supported spell, skill, ability and macro icons, or supported paperdoll Feature Tools actions, to assign a shortcut.
- **Binding controls:** capture, confirm or clear bindings, with checks for supported client macro and spell-bar conflicts. Razor Enhanced bindings remain managed separately.
- **Cooldown repeat handling:** added **Keep existing**, allowing repeated triggers to leave an active timer running. Existing add-another and replace-existing behavior remains available.
- **Cooldown rule identity:** stable saved rule IDs distinguish rules with identical names or triggers.

### Healthbars and nameplates

- **Permanent healthbar drag filters:** added one active default filter with individual modifier-key overrides: All Mobiles, Players only, Friendly Players only, Guild only, Hostile Mobiles only, Grey and Hostile Mobiles only, and Neutral Mobiles only.
- **Notoriety filtering:** filters use the specified notoriety values; All Mobiles includes every notoriety.
- **Healthbar cleanup:** removed the B button.
- **Crowded nameplates:** improved overlap avoidance with bounded placement lanes and leader lines. Last target, hovered entities and party members receive priority; manually locked labels retain their positions.

### Chat, alerts and equipment switching

- **Original Global Chat access:** `-nativechat open` opens the original UOAlive Global Chat regardless of the replacement-chat preference, without changing that setting.
- **Native chat history:** with replacement enabled, bare `[c` opens the original chat history; `[c message` continues to post normally.
- **Chat mentions:** added configurable local mention highlighting.
- **Chat history resources:** bounded visible rows to retained history and released expired text resources.
- **Chat resizing:** reflow messages while preserving the reading position.
- **Pet loyalty alerts:** restrict checks to pets for which the player has rename permission and ignore unrelated pet messages.
- **Bandage alerts:** suppress zero-bandage warnings for non-users until bandage stock has been observed or bandage automation is enabled.
- **Muted alerts:** muting or snoozing pet and bandage alerts also silences their sounds.
- **Shield restoration:** remember shields displaced by a two-handed weapon and restore them when switching back to a one-handed weapon or spellbook. Preserve another off-hand item subsequently equipped by the player or Razor Enhanced.

### Maps and travel

- **World-map loading:** moved loading into timed main-thread slices, cancelled stale work when changing maps or sessions, and improved texture disposal.
- **Marker names:** added an option to show marker names at every zoom level, with bounded label counts, overlap suppression and hover fallback.
- **Marker CSV:** added support for quoted fields, multiline names and culture-safe coordinates while preserving custom zoom settings.
- **Travel-source identification:** World Explorer displays source names, serials and rune slots to distinguish destinations with identical names.
- **Travel-source details:** added observed source hue and scan-time information; unavailable facet information remains unknown.
- **Preferred travel sources:** save a preferred source for matching destination names. Travel still uses the explicitly selected entry.

### Performance and stability

- **Incremental Item Finder search:** added cached query plans and a text candidate index, updated when observed item data changes. Queries unsuitable for indexing retain full evaluation.
- **Shared inventory counts:** counters, Restock/readiness checks and reagent/bandage watchers reuse observed inventory snapshots, invalidated when relevant items or sessions change.
- **Background screenshots:** PNG encoding and disk writes run on a worker after GPU capture. Pending captures are bounded, files complete atomically, and pending saves finish on exit.
- **Safe layout saving:** save gump layouts atomically and preserve the previous file if saving fails.
- **Container traversal:** guard missing and cyclic container links.
- **Session cleanup:** reset stale feature failures and pending notices between sessions.
- **Performance diagnostics:** corrected draw-cadence measurements and slowest-1% FPS calculations.
- **Binary and network handling:** strengthened reader bounds, packet-field writing and Huffman streaming.
- **Asset validation:** improved animation, map and texture-atlas validation.
- **Resource management:** corrected resource lifetimes and collection retention; reduced repeated work and allocations.
- **Property parsing:** improved culture-safe item-property handling.
- **Utility fixes:** corrected issues in collection views, averaging, logging, regular-expression handling and string-builder replacement.

### Razor Enhanced and installation

- **Plugin picker:** choose Razor Enhanced through Options on Windows and save the selected plugin path to `settings.json` while preserving other settings. Restart the client to load it.
- **Supported platform:** Windows x64, .NET Framework 4.7.2, using the existing pinned SDL3/FNA runtime.
- Replace the complete client files when upgrading so new artwork and shaders are included.
- Back up the existing client folder and profiles before upgrading.


See the [0.6.2 setup guide](https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki/0.6.2-Guide) and [complete client command reference](https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki/Client-Commands). Open `-enhancements` for graphics and ambience, `-gumpthemes` for themes, and `-operations` for queued actions.

## Version 0.6.1 highlights

1. **Chat channels:** Implemented the new UOAlive Chat channels including color code! View/Send selectors for Global, Trade, Events, Help, LFG, Guild and Party, with correct posting commands. All shows public channels.
2. **Chat restoration:** Global Chat, Guild Chat and Nearby Speech windows reopen after login.
3. **Equipment sorting:** backpack/container grid views can sort now also by equipment layer, place non-equipment last while preserving locked slots.
4. **Game window fixes:** now resize (blue dot) and relocation (grab the edges) of the game window is working.
5. **Runic Atlas fixes:** World Explorer now scans the Runic Atlases correctly.

## Version 0.6 highlights

- Added an in-game chess board for local two-player games or play against Stockfish, with piece-name tooltips and visible computer moves. Chess requires a separate Stockfish executable; place `stockfish.exe` beside the client or use `-chess path <file>`.
- Recreated the login, shard, character-selection, and loading screens with high-resolution art and revised layouts.
- Added high-resolution gump themes and paperdoll skins, including wood, metal, marble, glass, and stained glass. New profiles default to HD Wood gumps and a marble paperdoll; existing profile choices are retained.
- Reworked paperdoll tool access: the circular launcher opens the tools panel, which includes durability tracking and links to other client tools.
- Added an insurance check to Restock Agent readiness and prevented Item Finder from opening spellbooks, bulk-order books, runebooks, atlases, and other books as containers. Books remain searchable as items.
- Improved nameplate-window reopening, chat and nearby-loot UI behavior, and several client hot paths.

### Chess and Stockfish setup

Chess needs a separate Stockfish executable for legal-move checks in both **Play Stockfish** and **Two players**. Stockfish is not included in the client download.

1. Download the Windows x86-64 build from the [official Stockfish download page](https://stockfishchess.org/download/) and extract it.
2. Put the extracted executable beside `ClassicUO.exe` and name it `stockfish.exe`. If you keep it elsewhere, enter `-chess path "C:\Games\Stockfish\stockfish.exe"` in game, using its actual full path. Repeat this command after restarting the client.
3. Enter `-chess` or open Chess from the client commands, then choose **Play Stockfish** or **Two players**. Two-player games are local to the same client; they are not shared over the network.

## Version 0.6 Beta highlights

- Latest fix update: Item Finder scans nested containers within nearby chests, keeps catalog entries between scans, excludes temporary Arcane Focus items, and warns when a located item has moved since indexing.
- Equipment Guru now scores every eligible item as an individual swap, improves its candidate shortlist and jewelry pair search, and reports items it could not compare because properties are missing. Its results refresh when **Analyze gear** is clicked.
- Equipment Guru projects stamina from Dexterity, hit points from Strength, and armor's inherent Lower Mana Cost. The grouped stat overview uses illustrated icons; item comparisons show a benefits and tradeoffs summary with numeric differences.
- Updated **Equipment Guru** recommendations with required minimums, keep-current targets, stat caps, individual resist and regeneration goals, and a current-equipment comparison. Live resist penalties such as Vampiric Embrace are included when evaluating replacements.
- Improved the **Item Finder** property tooltip colors and made **Restock Agent** scan selected source containers when opened so stock counts are available immediately.
- Reworked paperdoll tool access into a native-art **TOOLS** button and a movable, position-saving panel. Its button stays opaque with the paperdoll background faded, and the panel buttons keep their artwork visible on hover.
- Added **Universal Item Finder**. Scan reachable containers into a persistent, character-specific catalog; combine text and numeric properties with ALL, NOT, and count logic; save searches; inspect full properties; and locate items or their last known house position. Area scans close containers they opened and preserve catalog entries from containers that were unreachable during a later scan.
- Added **Restock Agent** with ordered source containers, supply presets, exact target amounts, custom destination containers, source and destination stock counts, reusable loadouts, and integrated supply, equipment, durability, weight, and backpack readiness checks.
- Added **Equipment Guru**. It reads real and effective skills, current equipment, and Item Finder candidates to recommend three target-driven fighter, archer, tamer, or mage loadouts. Goals and active skill targets are editable per character; comparisons highlight gains and losses; weapons, spellbooks, and talismans remain fixed; race equipment restrictions are respected.
- Added **Alert Center** with active/history views, category and severity filters, snooze, mute, source suppression, and per-category severity settings. Low durability, low pet loyalty, and legendary creature alerts remain visible until right-clicked; recurring low durability and loyalty warnings return after ten minutes.
- Added dedicated UO-style artwork and controls for Item Finder, Restock Agent, Equipment Guru, and Alert Center. Paperdolls expose matching quick-access buttons for these tools and World Explorer.
- Expanded gump opacity support to the world-map border, main menu, buff bar, compact World Explorer, and paperdoll. Paperdoll opacity has its own option and `-gumpopacitypaperdoll` command.
- Improved the durability display with prominent deep-plum critical rows, violet borders and bars, red Repair actions, and a brief pulse when an item first drops below 10 durability.
- Renamed the primary nearby speech command to `-nearbychat`; `-speechhistory` remains available as an alias. Fixed Global and Guild Chat input layout and retained separate auto-open options for Global, Guild/Alliance, and Nearby Chat.
- Compact World Explorer now restores after restart when it was open. Pinned command groups can be detached with Alt.

This is a beta release. Keep a backup of your existing client folder and profile before installing.

## Version 0.5.2 highlights

- Restored the compact World Explorer buttons while keeping the 1–4 column layout.
- Tint pets gray when they are not guarding you with `-petguardtint on|off`.
- Set Global Chat, Guild and Alliance Chat, and Nearby Speech to open on new messages in **Options → Speech**. Global and guild chat default on; nearby speech defaults off. Closing a chat window disables its auto-open setting until you reopen it or enable the setting again.

## Version 0.5.1 highlights

- Compact World Explorer now fits its pinned destinations, with a 1–4 column selector, narrower buttons, wrapped labels, and less unused space.
- Set supported gump opacity values together with `-gumpopacityall <0-100>` or the new options control. Grid item borders and hover opacity stay separate. Custom utility and chat gumps retain their 20% minimum.
- Macro buttons now follow custom gump opacity. Themed equipment durability rows have corrected spacing.
- Pet bandaging no longer shows a success toast. Runebook travel no longer crashes when a gump response has empty fields.

## Version 0.5 highlights

- Open the visual theme picker with `-gumpthemes`, or switch directly with `-gumptheme <name>`. The themes now keep decorative borders clear of window content and use readable macro button text.
- Open World Explorer with `-worldexplorer`. Scan your backpack for runes and rune books, drag destinations into the pinned list, and choose a travel method. Scan again after moving or changing a source item.
- Set opacity with `-gumpopacity <area> <percent>` or its dedicated commands, such as `-gumpopacitycustom 80`. Run `-gumpopacity` to see current values and supported areas.
- Razor Enhanced hotkey macro buttons support displayed key names, including mouse X Button 1, Page Down, and Windows keys.
- Windows defaults to system DPI scaling; use `-native-dpi` for native per-monitor pixels.
- Toast alerts, including low tithing points for Chivalry users, use the selected gump theme and appear at the top center. Use `-toastanchor` to drag their position or resize their width, then right-click to save.

See the [Client Commands wiki](https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki/Client-Commands) for the complete command list and theme names.

## Compatibility

Version 0.6.2 uses a pinned FNA/SDL3 graphics and input stack while remaining on .NET Framework 4.7.2 for Razor Enhanced compatibility.

On Windows, the client uses Windows system DPI scaling by default so the interface remains readable on high-resolution displays. Start the client with `-native-dpi` to use SDL3's native per-monitor pixels instead. The legacy `-highdpi` option also selects native DPI mode.

## Razor Enhanced plugin setup

On Windows, open **Options → General → Razor Enhanced** and choose the Razor Enhanced plugin DLL or compatible executable. The client validates its plugin entry point and writes the absolute path to `settings.json`, preserving other plugins and settings. Restart the client to load the selected plugin.

## Magnifying glass cursor

Use `-magnifier` to toggle a UO-style magnifying-glass cursor. Its round lens enlarges the world and client UI, with a small central crosshair at the actual click position.
Press **Escape** to turn it off; the selected zoom level is kept.

- `-magnifier 1`, `2`, `3`, or `4`: select a zoom level and enable the lens. The default is 2×; the chosen level is saved per character.
- `-magnifier next` / `-magnifier prev`: cycle through the four levels.
- `-magnifier on`, `off`, or `status`: enable, disable, or inspect the current setting.
- For a hotkey, create a macro in **Options → Macros**, assign its key, and add a **ClientCommand** action with text `magnifier` (without the leading `-`). Use `magnifier next` or `magnifier prev` for separate zoom hotkeys.

The lens does not move the click location or change world zoom. Magnifier activation resets on logout. Included in stable 0.6.2.

## World scaling and anti-aliasing

Under **Options → Video → Misc**, enable **post processing effects** and choose **Processing type**:

- **pixel art**: preserves crisp texel interiors while smoothing their boundaries when enlarging the world. Tune **Pixel art filter sharpness (%)** under **Options → Enhancements → Graphics**: 0 gives linear filtering, 100 keeps more of the pixel structure; default 75.
- **xbr**: the existing edge-aware pixel-art scaler. Both pixel-art filters apply when enlarging; native-size and downscaled views fall back to linear filtering.
- **point**, **linear**, and **anisotropic** retain their existing behavior and saved selections.

Under **Options → Enhancements → Graphics**, enable **World edge anti-aliasing (FXAA)** and adjust its strength from 0–100%. It works at native resolution and with upscaling. Start with 35%; higher values soften more edges. AA defaults off, and strength 0 skips its render pass. It runs after world lighting and before overhead text, healthbars and UI; those remain sharp. Quality buffers are reused, resize with the window, and are released when disabled or on logout. No CPU screenshot readback is used.

**Smooth magnifier image**, in the same Graphics page, defaults on. Disable it for the original pixelated lens. The crosshair still marks the actual click position; the lens remains opaque and Escape turns it off.

These additions are included in stable 0.6.2. Replace the complete client files when updating so `WorldQuality.fxc` and `Magnifier.fxc` are included.

## Razor Enhanced script buttons

1. Add a script to Razor Enhanced and assign it a hotkey, for example `Ctrl+Alt+F6`.
2. In tazUO's macro options, create a named macro with the `RazorEnhancedHotkey` action. Enter the same hotkey in its text field, for example `Ctrl+Alt+F6`. The Razor Enhanced display strings `Oem6, Control, Alt`, `LWin, Shift`, `Next, Control, Alt`, `X Button 1, Shift`, and `X Button 1, Control` are also supported.
3. Use **Create Macro Button** to place a button for that macro in game.

The button sends the hotkey to Razor Enhanced's plugin callback. Razor Enhanced must be loaded and its hotkeys enabled. The action does not send the key to the game or run another tazUO macro bound to that key.

## Videos of the features:  
Version 0.6 Features: https://youtu.be/B9YZbGYMdFA

Version 0.5 Features: https://youtu.be/cbgis1d0R6E  
Version 0.3 Features: https://youtu.be/4MyUOeN3P4A  
Custom Chivalry Effects: https://youtu.be/81xNXowYFro  
Custom Spell Effects: https://youtu.be/YFjpZDvHfBE  
Custom Nether Blast Effects: https://youtu.be/5MwT_lsuTXw  
Custom Water Skins: https://youtu.be/fs3JoYBPhLc  
Custom Weather Effects: https://youtu.be/4BqKIRYgKqI  

## Installation

1. Make a backup of your ClassicUO folder.
2. Download and extract `tazUO-MW-Edition-win-x64.zip` from the [0.6.2 release](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/tag/0.6.2).
3. Place its contents in your ClassicUO or tazUO folder, where `ClassicUO.exe` is located.
4. Optional: Download [UOMusic.zip](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/download/0.1/UOMusic.zip), then place the contents of its `UOMusic` folder in your UO folder under `Music/Digital`.
5. Start `ClassicUO.exe`.
6. Optional: After login, click **Scan** in the Music Player gump.


License:
This project is a fork of software developed by andreakarasho:
https://github.com/andreakarasho
Licensed under the BSD-4-Clause license. See LICENSE.

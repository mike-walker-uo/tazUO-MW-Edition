# TazUO MW Edition

Custom Ultima Online client based on the legacy 4.5.22.0 release of tazUO.

[Download version 0.6 Beta](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/tag/0.6-beta)

It has many features, some of them are:
  - Custom weather and light effects
  - Custom gump themes, including Gilded Grove, Aetherglass, Ornate, Chronicle, Arcane, Relic, Mariner, Runestone, Oak & Iron, Exodus, Blood Oath, Celestial, Heartwood Sanctuary, and Hildebrandt
  - Custom hit effects
  - Custom spell effects (Chiv, Magery, Myst, SW)
  - Custom mob size (Greater Dragons mean "greater")
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

## Version 0.6 Beta highlights

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

Version 0.6 Beta uses a pinned FNA/SDL3 graphics and input stack while remaining on .NET Framework 4.7.2 for Razor Enhanced compatibility.

On Windows, the client uses Windows system DPI scaling by default so the interface remains readable on high-resolution displays. Start the client with `-native-dpi` to use SDL3's native per-monitor pixels instead. The legacy `-highdpi` option also selects native DPI mode.

## Razor Enhanced script buttons

1. Add a script to Razor Enhanced and assign it a hotkey, for example `Ctrl+Alt+F6`.
2. In tazUO's macro options, create a named macro with the `RazorEnhancedHotkey` action. Enter the same hotkey in its text field, for example `Ctrl+Alt+F6`. The Razor Enhanced display strings `Oem6, Control, Alt`, `LWin, Shift`, `Next, Control, Alt`, `X Button 1, Shift`, and `X Button 1, Control` are also supported.
3. Use **Create Macro Button** to place a button for that macro in game.

The button sends the hotkey to Razor Enhanced's plugin callback. Razor Enhanced must be loaded and its hotkeys enabled. The action does not send the key to the game or run another tazUO macro bound to that key.

## Videos of the features:  
Version 0.5 Features: https://youtu.be/cbgis1d0R6E  
Version 0.3 Features: https://youtu.be/4MyUOeN3P4A  
Custom Chivalry Effects: https://youtu.be/81xNXowYFro  
Custom Spell Effects: https://youtu.be/YFjpZDvHfBE  
Custom Nether Blast Effects: https://youtu.be/5MwT_lsuTXw  
Custom Water Skins: https://youtu.be/fs3JoYBPhLc  
Custom Weather Effects: https://youtu.be/4BqKIRYgKqI  

## Installation

1. Make a backup of your ClassicUO folder.
2. Download and extract `tazUO-MW-Edition-win-x64.zip` from the [0.6 Beta release](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/tag/0.6-beta).
3. Place its contents in your ClassicUO or tazUO folder, where `ClassicUO.exe` is located.
4. Optional: Place the contents of the `UOMusic` folder in your UO folder under `Music/Digital`.
5. Start `ClassicUO.exe`.
6. Optional: After login, click **Scan** in the Music Player gump.


License:
This project is a fork of software developed by andreakarasho:
https://github.com/andreakarasho
Licensed under the BSD-4-Clause license. See LICENSE.

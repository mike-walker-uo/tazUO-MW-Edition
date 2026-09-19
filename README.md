# TazUO MW Edition

Custom Ultima Online client based on the legacy 4.5.22.0 release of tazUO.

[Download the latest release](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/latest)

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
  - World Explorer for scanning rune books, pinning destinations, and quick travel
  - Optional boss health bar with compact and ornate layouts
  - Tithing-point tracking and low-point warnings for Chivalry users
...and many many more features.

See the [complete client command reference](https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki/Client-Commands) for every built-in command, its usage, and a short explanation. You can also type `-commands` in game to open the searchable command palette.

## Version 0.5 highlights

- Open the visual theme picker with `-gumpthemes`, or switch directly with `-gumptheme <name>`. The themes now keep decorative borders clear of window content and use readable macro button text.
- Open World Explorer with `-worldexplorer`. Scan your backpack for runes and rune books, drag destinations into the pinned list, and choose a travel method. Scan again after moving or changing a source item.
- Set opacity with `-gumpopacity <area> <percent>` or its dedicated commands, such as `-gumpopacitycustom 80`. Run `-gumpopacity` to see current values and supported areas.
- Razor Enhanced hotkey macro buttons support displayed key names, including mouse X Button 1, Page Down, and Windows keys.
- Windows defaults to system DPI scaling; use `-native-dpi` for native per-monitor pixels.
- Toast alerts, including low tithing points for Chivalry users, use the selected gump theme and appear at the top center. Use `-toastanchor` to drag their position or resize their width, then right-click to save.

See the [Client Commands wiki](https://github.com/mike-walker-uo/tazUO-MW-Edition/wiki/Client-Commands) for the complete command list and theme names.

## Compatibility

Version 0.5 uses a pinned FNA/SDL3 graphics and input stack while remaining on .NET Framework 4.7.2 for Razor Enhanced compatibility.

On Windows, the client uses Windows system DPI scaling by default so the interface remains readable on high-resolution displays. Start the client with `-native-dpi` to use SDL3's native per-monitor pixels instead. The legacy `-highdpi` option also selects native DPI mode.

## Razor Enhanced script buttons

1. Add a script to Razor Enhanced and assign it a hotkey, for example `Ctrl+Alt+F6`.
2. In tazUO's macro options, create a named macro with the `RazorEnhancedHotkey` action. Enter the same hotkey in its text field, for example `Ctrl+Alt+F6`. The Razor Enhanced display strings `Oem6, Control, Alt`, `LWin, Shift`, `Next, Control, Alt`, `X Button 1, Shift`, and `X Button 1, Control` are also supported.
3. Use **Create Macro Button** to place a button for that macro in game.

The button sends the hotkey to Razor Enhanced's plugin callback. Razor Enhanced must be loaded and its hotkeys enabled. The action does not send the key to the game or run another tazUO macro bound to that key.

## Videos of the features:
https://youtu.be/4MyUOeN3P4A  
https://youtu.be/81xNXowYFro  
https://youtu.be/YFjpZDvHfBE  
https://youtu.be/5MwT_lsuTXw  
https://youtu.be/fs3JoYBPhLc  
https://youtu.be/4BqKIRYgKqI

## Installation

1. Make a backup of your ClassicUO folder.
2. Download and extract `tazUO-MW-Edition-win-x64.zip` from the [latest release](https://github.com/mike-walker-uo/tazUO-MW-Edition/releases/latest).
3. Place its contents in your ClassicUO or tazUO folder, where `ClassicUO.exe` is located.
4. Optional: Place the contents of the `UOMusic` folder in your UO folder under `Music/Digital`.
5. Start `ClassicUO.exe`.
6. Optional: After login, click **Scan** in the Music Player gump.


License:
This project is a fork of software developed by andreakarasho:
https://github.com/andreakarasho
Licensed under the BSD-4-Clause license. See LICENSE.

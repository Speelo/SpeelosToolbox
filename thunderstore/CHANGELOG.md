# Changelog

## 1.0.0

First release of Speelo's Toolbox. Forked from Skrip's SkToolbox 1.10.8 (source: derekShaheen/SkToolbox-for-Valheim, GPL-3.0) and rebuilt for Valheim 1.0.7 on BepInExPack_Valheim 5.4.2350.

### New
- Clickable menu. F6 opens it (Escape closes it), categories on the left, actions on the right, search box for long lists. The old NumPad navigation and `/alt` are gone.
- While the menu is open the mouse is freed and the game ignores player input: no camera turning, no attacking when you click a button, and no hotbar/inventory/chat keys firing while you type in the search box.

- `KeepAchievements` (on by default) keeps Steam achievements working. Valheim disables achievement progress as soon as anything counts as cheated, and loading BepInEx alone already counts, so this restores normal behaviour for modded players. It also stops cheat commands from permanently flagging the character.

### Fixed for Valheim 1.0 (versus SkToolbox 1.10.8)
- Compiles against the 1.0 assemblies. Reflection into game internals walks base types (Valheim moved the console's buffer, command table and history into `Terminal`) and logs missing members instead of crashing.
- `/imacheater` unlocks vanilla cheat commands as a client on someone else's server (1.0 gates them behind "is server"). It marks the character as having used cheats, exactly like vanilla `confirmcheats`.
- `/give` argument parsing and item quality; `/spawntamed` no longer recurses; `/env` lists real 1.0 environments; `/portals` uses the game's portal list; `/whois` reads the 1.0 roster; `/removedrops` skips fish and pieces; `/tp` lands on the surface; `/stopevent` explains it needs host.
- `/nosup` makes pieces truly unbreakable-by-support; `/nores` no longer fights the placement ghost and clears the red highlight.
- Commands typed in chat no longer also run vanilla's slash-stripped copy.
- AutoRun on spawn works again; Reload re-applies patches; god/fly/nocost use the 1.0 player API and no longer stomp each other.
- Removed the pastebin version check.

# Changelog

## 1.0.1

### Fixed

- **God mode now blocks damage.** Valheim's own god mode never granted immunity: `Character.ApplyDamage` subtracts the damage first and only rescues you at the moment a hit would kill you, clamping health to 1. You could not die, but your health bar still drained and never recovered. Damage aimed at you while god mode is active is now discarded before it lands, covering ordinary hits, attack recoil, and the burning, poison and smoke effects. Set `GodModeBlocksDamage` to false in the config for Valheim's unmodified behaviour.
- Adapted to Valheim build 25253764, which turned `PlayerProfile.s_bypassCheatChecks` from a writable field into a read-only property. The achievement bypass patches the property getter instead. Every other patch target was re-verified against the new assemblies and is unchanged.
- The Give tab rebuilds its item list when the game swaps its item database, so a second world no longer shows the first world's items.

## 1.0.0

First release. Forked from Skrip's SkToolbox 1.10.8 (source: derekShaheen/SkToolbox-for-Valheim, GPL-3.0), which was last updated for Valheim 0.217.24, and rebuilt for Valheim 1.0.7 on BepInExPack_Valheim 5.4.2350.

### New interface

- Mouse-driven menu on **F6**, Escape to close, rebindable in the config. The old NumPad navigation and the `/alt` alternate keys are gone.
- Four tabs: Player, Give, World, Console. Icon grids throughout, using the game's own item art, with hover text and a close button.
- **Give tab**: every item in the game as an icon grid with a search box and a quantity slider. Items go into your inventory. Modded items appear automatically, since the list is read from the live game database.
- **Player tab**: four one-click food buttons that fill all three slots, for best health, best stamina, best eitr, and balanced. Toggles show a green outline while active, read from live game state so they stay correct when Valheim's own hotkeys change them.
- **World tab**: terrain tools with radius and height sliders instead of fixed presets, plus weather, time of day, wind, events, map reveal and reset, portals, drop cleanup and the seed. The old `T - Reset` is now `Undo Terrain Edits`, which is what it actually does.
- While the menu is open the mouse is freed and player input is paused: no camera turning, no attacking when you click, and hotbar, inventory and chat keys stay quiet while you type in a search box.
- Menu background is solid and its opacity is configurable with `MenuOpacity`.
- `KeepAchievements`, on by default, keeps Steam achievements working. Valheim disables achievement progress the moment anything counts as cheated, and loading BepInEx alone already counts, so this restores normal behaviour for modded players. It also stops cheat commands from permanently flagging your character.
- New `/listicons` command, which lists item prefabs that have an icon.

### Fixed for Valheim 1.0, versus SkToolbox 1.10.8

- Compiles and runs against the 1.0 assemblies. Reflection into game internals now walks base types, because 1.0 moved the console's buffer, command table and history up into `Terminal`, and it logs a missing member instead of crashing the command.
- `/imacheater` unlocks vanilla cheat commands as a client on someone else's server, which 1.0 gates behind an is-server check.
- `/give` argument parsing and item quality. Giving previously threw on every call and silently did nothing.
- `/spawntamed` no longer calls itself. `/env` lists the real 1.0 environments. `/portals` uses the game's portal list. `/whois` reads the 1.0 player roster. `/removedrops` skips live fish and placed pieces. `/tp` lands on the surface instead of under it. `/stopevent` explains that it needs the host.
- `/nosup` makes pieces genuinely unaffected by support. `/nores` no longer fights the placement ghost every frame and clears the red highlight.
- Commands typed in chat no longer run twice.
- Auto-run on spawn works again, having been stubbed out since Hearth and Home. Reload re-applies the patches. God, fly and no-cost use the 1.0 player API and no longer overwrite each other's debug flag.
- Removed the upstream version check that called out to a pastebin URL on startup.

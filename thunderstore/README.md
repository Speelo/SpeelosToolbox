# Speelo's Menu

A clickable in-game cheat menu for Valheim 1.0, plus a console/chat command extender. Press **F6** to open the menu and click what you want. No keybinds to memorise.

Based on Skrip's SkToolbox ([Nexus](https://www.nexusmods.com/valheim/mods/8), source by derekShaheen on [GitHub](https://github.com/derekShaheen/SkToolbox-for-Valheim)), rebuilt for Valheim 1.0.7 and given a mouse-driven menu. GPL-3.0.

Use discretion in multiplayer. These commands change your character and the world around you, and other players will notice.

## Menu

- **F6** opens and closes the menu (rebindable: `MenuToggleKey` in the config). F6 was picked because Valheim and Steam both use F11 and F12 for screenshots, F5 is the game console, and F9 cycles the controller layout.
- Categories on the left, actions on the right. Toggles show their current state. Long lists (Give Item) get a search box.
- The mouse is freed and player input is paused while the menu is open: the camera stops turning, clicks never swing your weapon, and typing in the search box will not swap hotbar slots or open the inventory.
- Escape closes the menu.

## Console and chat commands

Open the console with F5 (it is enabled automatically) and type `/?` for the full list, or use `/command` in chat.

Player: `/god` `/ghost` `/fly` `/heal` `/infstam` `/nocost` `/nores` `/nosup` `/repair` `/clearinventory` `/give` `/set` `/tame` `/killall` `/tp` `/coords` `/findtomb` `/farinteract` `/imacheater`

World: `/spawn` `/spawntamed` `/env` `/tod` `/wind` `/resetwind` `/event` `/randomevent` `/stopevent` `/tr` `/td` `/tl` `/tu` `/optterrain` `/removedrops` `/portals` `/revealmap` `/resetmap` `/seed` `/whois` `/detect`

Console: `/?` `/echo` `/clear` `/console` `/listitems` `/listprefabs` `/listskills` `/q`

Commands chain with `;` (`/god; /fly`). The config supports auto-run on spawn, aliases and hotkeys.

## Achievements

Valheim switches off achievement progress the moment anything counts as cheated, and merely loading BepInEx already counts, because BepInEx sets the game's `isModded` flag. So achievements are off for every modded player before this mod does anything.

`KeepAchievements` (on by default) flips the game's own `PlayerProfile.s_bypassCheatChecks` switch, which every cheat check reads. Achievement progress keeps counting, and spawned items, pieces and world objects are no longer stamped as cheated. It also stops cheat commands from permanently flagging your character, and clears that flag if it was already set.

Two things it cannot undo: a world whose starting global keys were set outside the world-modifier menu stays flagged as a cheated world when you play unmodded, and achievements you already missed are not retroactively awarded. Set `KeepAchievements` to false if you would rather Valheim behave normally.

## Config

`BepInEx/config/com.speelo.speelosmenu.cfg`

## Requirements

- BepInExPack_Valheim 5.4.2350+

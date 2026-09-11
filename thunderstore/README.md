# Speelo's Toolbox

A clickable in-game cheat menu for Valheim 1.0, plus a console and chat command extender. Press **F6**, click what you want. No keybinds to memorise.

Based on Skrip's SkToolbox ([Nexus](https://www.nexusmods.com/valheim/mods/8), source by derekShaheen on [GitHub](https://github.com/derekShaheen/SkToolbox-for-Valheim)), rebuilt for Valheim 1.0.7 and given a mouse-driven interface. GPL-3.0.

Use discretion in multiplayer. These commands change your character and the world around you, and other players will notice.

## The menu

**F6** opens and closes it, **Escape** closes it. The key is rebindable with `MenuToggleKey` in the config. F6 is the default because Valheim and Steam both use F11 and F12 for screenshots, F5 is the game console, and F9 cycles the controller layout.

Four tabs across the top:

- **Player** - one-click food, healing, repair, taming, and toggles for god mode, flying, no-cost building, build anywhere, infinite stamina, enemy detection and coordinates. Toggles light up with a green outline while they are on, read from live game state rather than remembered, so they stay correct even when you use Valheim's own hotkeys.
- **Give** - every item in the game as a grid of icons, with a search box and a quantity slider. Items go straight into your inventory. Modded items appear automatically.
- **World** - terrain tools with radius and height sliders, plus weather, time of day, wind, events, map reveal and reset, portal list, drop cleanup and the world seed.
- **Console** - reload or unload the toolbox, open the log folder.

While the menu is open the mouse is freed and the game ignores player input. The camera stops turning, clicks never swing your weapon, and typing in a search box will not swap hotbar slots or open the inventory.

### Food

The Player tab has four one-click buttons that fill all three food slots: best health, best stamina, best eitr, and a balanced option that takes the best of each. Hover a button to see exactly which foods it will eat.

## Console and chat commands

Open the console with F5, which this mod enables for you, and type `/?` for the full list. Most commands also work in chat with a `/` prefix.

Player: `/god` `/ghost` `/fly` `/heal` `/infstam` `/nocost` `/nores` `/nosup` `/repair` `/clearinventory` `/give` `/set` `/tame` `/killall` `/tp` `/coords` `/findtomb` `/farinteract` `/imacheater`

World: `/spawn` `/spawntamed` `/env` `/tod` `/wind` `/resetwind` `/event` `/randomevent` `/stopevent` `/tr` `/td` `/tl` `/tu` `/optterrain` `/removedrops` `/portals` `/revealmap` `/resetmap` `/seed` `/whois` `/detect`

Console: `/?` `/echo` `/clear` `/console` `/listitems` `/listprefabs` `/listicons` `/listskills` `/q`

Commands chain with `;`, so `/god; /fly` runs both. The config supports auto-run on spawn, aliases and hotkeys.

## Achievements

Valheim switches off achievement progress the moment anything counts as cheated, and merely loading BepInEx already counts, because BepInEx sets the game's `isModded` flag. So achievements are off for every modded player before this mod does anything.

`KeepAchievements`, on by default, flips the game's own `PlayerProfile.s_bypassCheatChecks` switch, which every cheat check reads. Achievement progress keeps counting, and spawned items, pieces and world objects are no longer stamped as cheated. It also stops cheat commands from permanently flagging your character, and clears that flag if it was already set.

Two things it cannot undo: a world whose starting global keys were set outside the world-modifier menu stays flagged as a cheated world when you play unmodded, and achievements you already missed are not awarded retroactively. Set `KeepAchievements` to false if you would rather Valheim behave normally.

## Config

`BepInEx/config/com.speelo.speelostoolbox.cfg`

Notable settings: `MenuToggleKey`, `MenuOpacity` for how solid the menu background is, `KeepAchievements`, and the console appearance options inherited from SkToolbox.

## Requirements

- BepInExPack_Valheim 5.4.2350 or newer

Jotunn is **not** required.

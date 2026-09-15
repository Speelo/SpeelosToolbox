# Speelo's Toolbox

A clickable in-game cheat menu for Valheim 1.0, plus a console and chat command extender. Press **F6**, click what you want. No keybinds to memorise.

Based on Skrip's SkToolbox ([Nexus](https://www.nexusmods.com/valheim/mods/8), source by derekShaheen on [GitHub](https://github.com/derekShaheen/SkToolbox-for-Valheim)), rebuilt for Valheim 1.0.7 and given a mouse-driven interface. GPL-3.0.

**This is a client-side mod. Do not install it on a dedicated server.** It is a menu, and a server has no screen to draw one on. Installed on a client it works normally whether you play alone, host a world, or join someone else's; the buttons say which of those they need.

Use discretion in multiplayer. These commands change your character and the world around you, and other players will notice.

## The menu

**F6** opens and closes it, **Escape** closes it. The key is rebindable with `MenuToggleKey` in the config. F6 is the default because Valheim and Steam both use F11 and F12 for screenshots, F5 is the game console, and F9 cycles the controller layout.

Five tabs across the top. Hovering anything shows what it does next to the cursor.

- **Player** - grouped into Character, Cheats and Info. One-click food, healing, repair, skills, puke, inventory size and stats; toggles for god mode, flying, no-cost building, build anywhere, infinite stamina and taming; status effects; and readouts for position, portals, coordinates and nearby enemies. Toggles light up with a green outline while they are on, read from live game state rather than remembered, so they stay correct even when you use Valheim's own hotkeys.
- **Give** - every item in the game as a grid of icons, with a search box and a quantity slider. Items go straight into your inventory. Modded items appear automatically.
- **Spawn** - every creature in the game as a grid, with sliders for how many and what star level, and a switch to spawn them tamed.
- **World** - grouped into Terrain, Time & Weather, Rules, Progression, Travel, Cleanup and Server. Terrain tools with radius and height sliders; weather, time, wind and raids; difficulty, world modifiers and presets; boss and world keys; teleporting and map search; cleanup commands; and kick, ban and ping for servers.
- **System** - display settings (field of view, frame limit, detail distance, snow, free fly camera, debug mode), and the toolbox itself: reload, unload, open the log folder.

Anything that needs a value first opens a small form inside the menu, with a search where the list is long. The commands that change the world for everyone ask for a confirmation before they run.

Every button's hover tip also says who a click affects, because Valheim's split between local and shared commands is not guessable from their names. See below.

While the menu is open the mouse is freed for the menu, but you can still walk: WASD moves you as normal, and holding the right mouse button turns the camera until you release it. Attacking, blocking, jumping and dodging stay switched off, clicks never swing your weapon, and typing in a search box moves nothing and will not swap hotbar slots or open the inventory. `WalkWithMenuOpen` and `MenuLookKey` in the config control this, and with a gamepad active the menu freezes you in place instead.

### Food

The Player tab has four one-click buttons that fill all three food slots: best health, best stamina, best eitr, and a balanced option that takes the best of each. Hover a button to see exactly which foods it will eat.

## Multiplayer and dedicated servers

Hover any button and the tip ends with one of these:

- **(client)** - changes nothing for anyone else.
- **(server)** - changes the world for everyone on it.
- **(server), needs you to be admin** - sent to the server to run there, which means your ID has to be in its `adminlist.txt`.
- **(host only)** - does nothing unless you are the one running the world.

On a dedicated server the world keys, world modifiers and presets, save, kick, ban and unban are forwarded to the server and refused unless you are an admin there. Sleep, Skip Time, Difficulty and List Keys are forwarded as well, but the server additionally wants `devcommands` typed into its own console before it will accept them.

Weather, time of day and wind only ever change your own game, on any setup, including a world you host yourself. They set the renderer's override rather than the world's clock.

Terrain edits, spawned creatures, taming, No Portals and the cleanup buttons write shared world state and need no admin at all, so they land on everyone whether or not you are running the server. Worth knowing before you click one on someone else's world.

## Console and chat commands

Open the console with F5, which this mod enables for you, and type `/?` for the full list. Most commands also work in chat with a `/` prefix.

Player: `/god` `/ghost` `/fly` `/heal` `/infstam` `/nocost` `/nores` `/nosup` `/repair` `/clearinventory` `/give` `/set` `/tame` `/killall` `/tp` `/coords` `/findtomb` `/farinteract` `/imacheater`

World: `/spawn` `/spawntamed` `/env` `/tod` `/wind` `/resetwind` `/stopevent` `/tr` `/td` `/tl` `/tu` `/optterrain` `/removedrops` `/portals` `/revealmap` `/resetmap` `/seed` `/whois` `/detect`

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
- A client. This mod refuses to load on a headless server (`-nographics -batchmode`) and logs that it has done so.

Jotunn is **not** required.

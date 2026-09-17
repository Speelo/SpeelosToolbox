# Changelog

## 1.1.4

### New

- **Guardian Power.** Set a power without visiting its stone, fire it on the spot, or just clear the cooldown after using one normally. Shows which power you have and how long until it is ready. Player tab, under Cheats.
- **Immunities.** The dial between god mode and vanilla: pick which damage sources are allowed to hurt you, so the cliff and the campfire stop killing you while a greydwarf still can. Creature and player attacks are deliberately not on the list - switching those off is what god mode is for, and it is one cell away.
- **No Carry Limit.** Being over the limit is what causes the slowdown, the warning and the stamina cost, so this lifts the limit rather than papering over the symptoms. Turning it off puts back whatever the Tweaks slider asked for, or the value your game started with.
- **Infinite Eitr.** Staves and spells cost nothing.
- **Refuel Fires.** Fills every fireplace around you to its own maximum, whatever it burns, with a radius slider - and a second button to put them all out. Reaches other people's fires. World tab, under Building.
- **Reveal Around Me.** Uncovers the map out to a distance you choose, 50 to 3000 metres, centred on where you are standing - for when you want the fog gone locally without handing yourself the whole world. World tab, beside Reveal Map.
- **Jump and crouch with the menu open.** Walking rides on the state Valheim puts you in just after closing its own menus, which passes movement and sprint through but switches jump and crouch off along with attacking. Those two are now put back. Typing a space into a search box still does not launch you.

### Fixed

- **Infinite Stamina now actually means infinite.** Swimming, sneaking and fishing all still charged you. Fishing bills stamina from the float and the fish rather than from the player, so the previous approach could never have reached it. It now covers every source at once, including attacks, jumps and dodges - which always cost before.
- **A horizontal scrollbar across the sectioned tabs.** The rows inside a section were laid out against the width of the whole tab, not the narrower space inside the section box, so a full row overflowed by about eighteen pixels. The menu is thirty pixels wider to keep the same twelve columns.

## 1.1.3

### New

- **Bookmarks.** Save where you are standing under a name and teleport back to it later. Bookmarks belong to the world you took them in, matched on its seed, so coordinates from one world never show up in another. Player tab, under Info.
- **Loadouts.** Snapshot your whole inventory, not just what you are wearing: every stack with its size, quality, durability and crafter, and which pieces were equipped. Loading one puts all of it back on, which makes it useful after a death rather than only for swapping gear you already have. Loading replaces what you are carrying, so it asks first.
- **Repair Nearby.** Repairs every worn building piece around you, with a radius slider. Works on other people's builds too, since the repair goes to whoever owns the piece. World tab, under a new Building section.
- **Items can be given at a chosen level.** A Level slider sits beside Quantity on the Give tab, so an iron pickaxe at level 4 is two sliders and a click. Items that do not upgrade that far are given at their own maximum, and the message says so.

### Changed

- Naming a bookmark or a loadout now happens in a small window over the menu, rather than a text box sitting in the list. Type, press Enter, or press Escape to back out.
- An action that cannot run yet is drawn greyed out with the reason above it, instead of accepting the click and then refusing. Anything that asks for a confirmation now shows plainly that it is waiting for one.

## 1.1.2

### Fixed

- **1.1.1 crashed a dedicated server a couple of seconds into startup.** The menu sampled Unity's keyboard-focus state once a frame to work out whether you were typing, and did so before checking whether the menu was even open. On a headless server there is no interface state to read, and reading it anyway is not an error the game can catch - it takes the whole process down. The check now happens only while the menu is actually open. Clients are unaffected either way; nothing about the menu changes.
- **The mod now refuses to load on a headless server at all**, and says so in the log. It is a client-side menu and has nothing to offer a server: the commands that change the world already reach the server through the game's own admin channel from your client. Installing it server-side was never doing anything, and now it fails politely instead of fatally.

## 1.1.1

### New

- **You can walk around with the menu open.** WASD moves you as normal; hold the right mouse button to turn the camera and release it to get the cursor back. Attacking, blocking, jumping and dodging stay switched off, which is the same state the game puts you in for a moment after closing its own menus. Typing in a search box moves nothing. Set `WalkWithMenuOpen` to false to go back to being frozen in place, or change `MenuLookKey` if you would rather leave the right button alone. Keyboard and mouse only: with a gamepad active the menu freezes you as before.

### Fixed

- **The menu dragged your cursor back to the middle of the screen.** Freeing the cursor was done after the game had already locked it, and locking recentres the pointer, so it was being yanked to the centre every frame. It is now suppressed before it happens, which is how Valheim's own menus do it. Thanks to BristleBeard for finding and fixing this.
- **Kill Enemies destroyed every creature spawner nearby.** The game's `killenemies` kills the creatures and then sweeps every spawner in the scene with no distance check, so a button described as "kill every hostile creature nearby" was permanently removing greydwarf nests and draugr piles across every loaded zone, for everyone on the world. It now runs `killenemycreatures`, which stops at the creatures. The old behaviour is still available as **Kill Enemies & Nests**, which says what it does and asks first.
- **Your cheats no longer switch off when you die.** Dying does not reset your character, it replaces it: the game destroys the old one and builds a new one from the prefab, so god mode, flying, no cost building, ghost, infinite stamina and far interact were all silently lost on every respawn while the menu still showed some of them as on. Whatever you had switched on is switched back on once you respawn, and if you turned something off in Valheim's own console, that is respected rather than undone. Set `PersistCheatsOnDeath` to false in the config to let each death clear them instead.
- **Status Effects did nothing on someone else's server.** It ran the game's `addstatus`, which is admin-gated, while the button claimed to be a client-side action. Status effects are local, so it now applies them directly and the marker is honest.
- Infinite Stamina flipped its own switch before reading it, so after a respawn the first click turned it off and it took two clicks to turn back on. It was also the one toggle with no check for a missing player, so clicking it during the seconds you are dead threw an error. `/farinteract` had both faults and is fixed the same way; the reach you pick is remembered, so a respawn restores it.
- Walking while over the carry limit no longer drains stamina with Infinite Stamina on. This also removes a trap where being encumbered with no stamina left stopped you moving at all.
- Infinite Stamina's description claimed stamina never drains. Attacks, jumps and dodges always cost stamina, so it now says what it actually does.

### New

- **Eight commands the toolbox already had are now buttons**, instead of being reachable only by typing them into the console: **No Support Needed** (the partner to Build Anywhere), **Ghost**, **Reach**, **Tweaks**, **Clear Inventory**, **Find Tombstone** and **Who's Online**.
- **Tweaks** covers carry weight, auto pickup range, jump force, run and swim speed, and the map reveal radius. These are restored after you die, which they never were through the old `/set` command. Defaults puts back the values your game actually started with rather than hard-coded numbers, so another mod's tuning survives.
- Actions that destroy something now lead with a red warning saying so, above everything else on the form. Clear Inventory spells out that equipped gear and extra slots go too, that nothing is dropped and no tombstone is left, and asks you to tick a box that says you understand before the button will do anything.

## 1.1.0

Includes everything from 1.0.1, which was never published separately.

### New

- **Spawn tab.** Every creature in the game as an icon grid, with sliders for how many to spawn (up to 20) and what star level (up to 3), and a switch to spawn them tamed. Icons borrow each creature's trophy art where one exists.
- **Forms.** Anything that needs a value now opens a dialog inside the menu instead of being impossible without the console: skills, status effects, inventory size, events, skip time, game speed, difficulty, world modifiers and presets, world and player keys, go to, find, find biome, kick, ban, unban, field of view, frame limit, detail distance and snow. Long lists get their own search box, and the commands that change the world for everyone ask for a confirmation first.
- **Scope markers.** Every button's hover tip says who a click actually affects: only you, everyone on the world, the server if you are an admin, or nothing at all unless you are the one hosting. Valheim's split between local and shared commands is not guessable from their names.
- **Cursor tooltips.** Hover text follows the cursor instead of sitting in a footer.
- **Player tab sections.** Character, Cheats and Info, with new entries for skills, puke, inventory size, stats, status effects and position.
- **World tab reorganised** into Terrain, Time & Weather, Rules, Progression, Travel, Cleanup and Server, and filled out: sleep, skip time, game speed, forced difficulty, no spawn, no map, no portals, world modifiers and presets, world keys, player keys, go to, recall, find, find biome, list locations, kill enemies, kill tame, remove fish, remove birds, stop fire, stop smoke, save world, kick, ban, unban, the banned list and ping.
- **System tab**, which replaces Console: field of view, frame limit, detail distance, snow buildup, free fly camera and debug mode, alongside reload, unload and the log folder.
- Readouts print into a panel in the menu rather than only into the game console, so position, stats, world keys, loaded locations and the banned list are readable with the console closed.

### Fixed

- **The event buttons never did anything.** Random event and the named events sent `/randomevent` and `/event`, which the toolbox never registered, so the lookup missed silently. They now use the game's own commands and share one picker, which lists each raid with the banner the game shows when it starts. Events are refused with a reason when you are not the one running the world, since the game only accepts them there.
- Vanilla cheat commands run from a menu button were refused unless you had typed `/imacheater` first, because Valheim gates them behind `devcommands`. Menu buttons now lift that gate for the length of the click and put it straight back, so clicking a button never leaves cheats switched on afterwards.
- Hover text was never cleared between frames, so a tooltip stayed on screen after the cursor moved away, and a tab could show another tab's text.
- The Spawn tab's "spawn tamed" switch rendered without a checkbox and looked like a dead control.

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

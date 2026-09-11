# Speelo's Toolbox - dev notes

Fork of [derekShaheen/SkToolbox-for-Valheim](https://github.com/derekShaheen/SkToolbox-for-Valheim) (Skrip's SkToolbox 1.10.8, upstream commit `274eddd`), GPL-3.0, see `LICENSE`. Upstream README kept as `UPSTREAM_README.md`. Code namespaces are still `SkToolbox.*`; only the user-facing identity changed.

Toolchain: BepInEx 5.4.23.5 (BepInExPack_Valheim 5.4.2350), Valheim 1.0.7, Unity 6000.0.75f1, target `net48`.

## Layout

- `SpeelosToolbox.csproj` - references game DLLs from the Steam install and BepInEx from the Thunderstore Mod Manager profile.
- `Directory.Build.props` - machine paths. Override with env vars `VALHEIM_INSTALL` / `TMM_PROFILE` or a git-ignored `Directory.Build.props.user`.
- `src/` - the mod, upstream's `SkToolboxValheim/SkToolbox/` layout.
- `thunderstore/` - package metadata.

## How it works

- `SkBepInExLoader` is the BepInEx plugin (GUID `com.speelo.speelostoolbox`). It binds config, then `SkLoader` creates the `SpeelosToolbox` GameObject with the controllers.
- `SkMenuController` draws the clickable IMGUI menu (F6). Modules (`SkModules/ModPlayer`, `ModWorld`, `ModConsole`) build `SkMenu` item lists; the controller keeps a submenu stack and refreshes toggle labels after every click.
- `SkCommandProcessor` holds every `/command`. `SkCommandPatcher` holds the Harmony patches (console enable, cheat unlock, chat interception, free support, build anywhere, and the mouse-unlock / input-pause patches used while the menu is open).
- `SkUtilities` has the reflection helpers that poke private game fields by name. Those names are what break between game versions; check them against `assembly_valheim.dll` with Mono.Cecil after every Valheim patch.

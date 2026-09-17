using SkToolbox.Configuration;
using SkToolbox.SkModules;
using SkToolbox.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SkToolbox
{
    internal static class SkCommandProcessor
    {
        public static bool flyEnabled = false;
        public static bool godEnabled = false;
        public static bool farInteract = false;
        // The chosen reach only ever existed on the Player instance, so a respawn had nothing to restore from.
        public static float farInteractDistance = 50f;
        public static bool infStamina = false;
        //public static bool infStacks = false;
        public static bool noCostEnabled = false;
        public static bool ghostEnabled = false;
        public static bool infEitr = false;
        public static bool noCarryLimit = false;

        // Player tuning. Zero means "never touched", so a respawn only rewrites what the user actually set and
        // everything else is left at whatever the prefab (or another mod) decided.
        public static int carryWeight = 0;
        public static int pickupRange = 0;
        public static int jumpForce = 0;
        public static int runSpeed = 0;
        public static int swimSpeed = 0;
        public static int exploreRadius = 0;
        public static bool bTeleport = false;
        public static bool bDebugTime = false;

        public static bool bDetectEnemies = false;
        public static bool btDetectEnemiesSwitch = true;
        public static int bDetectRange = 20;

        public static bool altOnScreenControls = false;

        public static bool bCoords = false;

        public static int pageSize = 11;
        private static Vector3 chatPos = new Vector3(0, 0 - 99);

        private static SkModules.ModConsole consoleOpt = null;
        internal static ModConsole ConsoleOpt { get => consoleOpt; set => consoleOpt = value; }

        // ---------------------------------------------------------------------------------------------------------
        // Surviving death.
        //
        // Dying destroys the Player GameObject and the game instantiates a fresh one from the prefab, so anything a
        // cheat wrote onto the character is back at its default. The toggles that live in our own statics kept saying
        // "on" while the effect was gone. The fix is to keep the intent (the static) separate from the effect (the
        // write), so the spawn hook can re-assert the effect without inverting the intent.
        //
        // Never re-apply by running the toggle commands: they flip their own state, so re-running one turns it off.
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes the stamina cheat onto a player. applyOffValues is false on the respawn path: the "off" numbers are
        /// this class's idea of vanilla, and writing them on every spawn would stomp whatever another mod had tuned.
        /// </summary>
        internal static void ApplyInfStamina(Player lp, bool applyOffValues)
        {
            if (lp == null) return;
            if (infStamina)
            {
                lp.m_staminaRegenDelay = 0.05f;
                lp.m_staminaRegen = 999f;
                lp.m_runStaminaDrain = 0f;
                // Each of these charges stamina every frame you keep moving, and a continuous drain resets the
                // regen delay before regen ever runs - which is why a huge regen rate does not outrun them.
                lp.m_encumberedStaminaDrain = 0f;   // over the carry limit
                lp.m_swimStaminaDrainMinSkill = 0f; // swimming, lerped between these two by the Swim skill
                lp.m_swimStaminaDrainMaxSkill = 0f;
                lp.m_sneakStaminaDrain = 0f;        // crouched and moving
            }
            else if (applyOffValues)
            {
                lp.m_staminaRegenDelay = 1f;
                lp.m_staminaRegen = 5f;
                lp.m_runStaminaDrain = 10f;
                lp.m_encumberedStaminaDrain = 10f;
                lp.m_swimStaminaDrainMinSkill = 5f;
                lp.m_swimStaminaDrainMaxSkill = 2f;
                lp.m_sneakStaminaDrain = 5f;
            }
        }

        private static bool tuningBaselineTaken = false;
        private static float baseCarry, basePickup, baseJump, baseRun, baseSwim, baseExplore;

        /// <summary>
        /// Remembers the untouched values so "Defaults" can put them back. Taken from a live player before anything
        /// is written, rather than hard-coded, so another mod's tuning is what gets restored if it got there first.
        /// </summary>
        internal static void CaptureTuningBaseline(Player lp)
        {
            if (lp == null || tuningBaselineTaken) return;
            tuningBaselineTaken = true;
            baseCarry = lp.m_maxCarryWeight;
            basePickup = lp.m_autoPickupRange;
            baseJump = lp.m_jumpForce;
            baseRun = lp.m_runSpeed;
            baseSwim = lp.m_swimSpeed;
            baseExplore = Minimap.instance != null ? Minimap.instance.m_exploreRadius : 100f;
        }

        /// <summary>Writes every tuning value the user has set. All of these live on the player, so a respawn loses them.</summary>
        internal static void ApplyTuning(Player lp)
        {
            if (lp == null) return;
            CaptureTuningBaseline(lp);
            // The toggle wins over the slider: someone who asked for no limit at all did not mean 300.
            if (noCarryLimit) lp.m_maxCarryWeight = 99999f;
            else if (carryWeight > 0) lp.m_maxCarryWeight = carryWeight;
            if (pickupRange > 0) lp.m_autoPickupRange = pickupRange;
            if (jumpForce > 0) lp.m_jumpForce = jumpForce;
            if (runSpeed > 0) lp.m_runSpeed = runSpeed;
            if (swimSpeed > 0) lp.m_swimSpeed = swimSpeed;
            if (exploreRadius > 0 && Minimap.instance != null) Minimap.instance.m_exploreRadius = exploreRadius;
        }

        /// <summary>Puts only the carry limit back, leaving the other tuning values alone.</summary>
        internal static void RestoreCarryBaseline(Player lp)
        {
            if (lp == null || !tuningBaselineTaken) return;
            lp.m_maxCarryWeight = baseCarry;
        }

        /// <summary>Forgets every tuning value and puts the captured originals back.</summary>
        internal static void ResetTuning(Player lp)
        {
            noCarryLimit = false;
            carryWeight = 0;
            pickupRange = 0;
            jumpForce = 0;
            runSpeed = 0;
            swimSpeed = 0;
            exploreRadius = 0;
            if (lp == null || !tuningBaselineTaken) return;
            lp.m_maxCarryWeight = baseCarry;
            lp.m_autoPickupRange = basePickup;
            lp.m_jumpForce = baseJump;
            lp.m_runSpeed = baseRun;
            lp.m_swimSpeed = baseSwim;
            if (Minimap.instance != null) Minimap.instance.m_exploreRadius = baseExplore;
        }

        /// <summary>Writes the interaction reach onto a player. Same applyOffValues rule as ApplyInfStamina.</summary>
        internal static void ApplyFarInteract(Player lp, bool applyOffValues)
        {
            if (lp == null) return;
            if (farInteract)
            {
                lp.m_maxInteractDistance = farInteractDistance;
                lp.m_maxPlaceDistance = farInteractDistance;
            }
            else if (applyOffValues)
            {
                lp.m_maxInteractDistance = 5f;
                lp.m_maxPlaceDistance = 5f;
            }
        }

        /// <summary>
        /// Remembers what was actually on at the moment of death. god, fly and no cost are stored on the character
        /// rather than here, and Valheim's own console can change them behind our back, so the flags are refreshed
        /// from the live player before it is destroyed. Without this, turning god off in the console and then dying
        /// would switch it back on.
        /// </summary>
        internal static void SnapshotOnDeath(Player lp)
        {
            if (lp == null) return;
            godEnabled = lp.InGodMode();
            flyEnabled = lp.IsDebugFlying();
            noCostEnabled = lp.NoCostCheat();
            ghostEnabled = lp.InGhostMode();
        }

        /// <summary>
        /// Called once per spawn. Puts back every cheat whose effect lives on the character. Each branch checks the
        /// remembered intent and the live state, so it is idempotent and does nothing at all on a fresh character.
        /// </summary>
        internal static void ReapplyOnSpawn(Player lp)
        {
            if (lp == null) return;
            if (Configuration.SkConfigEntry.CPersistCheatsOnDeath != null
                && !Configuration.SkConfigEntry.CPersistCheatsOnDeath.Value)
            {
                // The user wants death to clear them. Forget the intent too, so the menu stops claiming they are on.
                infStamina = false;
                farInteract = false;
                godEnabled = false;
                flyEnabled = false;
                noCostEnabled = false;
                ghostEnabled = false;
                infEitr = false;
                return;
            }

            ApplyInfStamina(lp, applyOffValues: false);
            ApplyFarInteract(lp, applyOffValues: false);
            ApplyTuning(lp);

            if (ghostEnabled && !lp.InGhostMode())
            {
                lp.SetGhostMode(true);
            }

            if (godEnabled && !lp.InGodMode())
            {
                lp.SetGodMode(true);
            }
            if (noCostEnabled && !lp.NoCostCheat())
            {
                lp.SetNoPlacementCost(true);
            }
            if (flyEnabled && !lp.IsDebugFlying())
            {
                // ToggleDebugFly writes the flag to the ZDO as well, so remote players and creature AI agree.
                ZNetView view = lp.GetComponent<ZNetView>();
                if (view != null && view.IsValid())
                {
                    lp.ToggleDebugFly();
                }
            }

            // Or-form: never switch off a debug mode the user turned on from the System tab.
            Player.m_debugMode = Player.m_debugMode || flyEnabled || noCostEnabled || SkCommandPatcher.BCheat;
        }

        [Flags]
        public enum LogTo
        {
            Console,
            Chat,
            DebugConsole
        }

        // Valheim 1.0: the hard-coded weatherList is gone; /env reads EnvMan.instance.m_environments (Mistlands/Ashlands/Deep North envs included).

        public static Dictionary<string, string> commandList = new Dictionary<string, string>()
        {
             {"/alt", "- (Removed) The menu is mouse-driven now. Press the MenuToggleKey (default F6) to open it."}
             ,{"/listicons", "[Filter] - List item prefabs that have an icon, for picking menu icons"}
            ,{"/console", "[1/0] - Toggle the console. No parameter with toggle. 1 = Open, 0 = Closed. Intended for use with hotkeys and aliases."}
            ,{"/coords", "- Show coords in corner of the screen"}
            ,{"/clear", "- Clear the current output shown in the console"}
            ,{"/clearinventory", "- Removes all items from your inventory. There is no confirmation, be careful."}
            ,{"/detect", "[Range=20] - Toggle enemy detection"}
            ,{"/farinteract", "[Distance=50] - Toggles far interactions (building as well). To change distance, toggle this off then back on with new distance"}
            ,{"/echo", "[Text] - Echo the text back to the console. This is intended for use with aliases and the autorun features."}
            ,{"/env", "[Weather] - Change the weather. No parameter provided will list all weather. -1 will allow the game to control the weather again."}
            //,{"/event", "[Event] - Begin an event"}
            ,{"/findtomb", "- Pin nearby dead player tombstones on the map if any currently exist"}
            ,{"/fly", "- Toggle flying"}
            ,{"/freecam", "- Toggle freecam"}
            ,{"/ghost", "- Toggle Ghostmode (enemy creatures cannot see you)"}
            ,{"/give", "[Item] [Qty=1], OR /give [Item] [Qty=1] [Player] [Level=1] - Gives item to player. If player has a space in name, only provide name before the space. Capital letters matter in item / player name!"}
            ,{"/god", "- Toggle Godmode"}
            ,{"/heal", "[Player=local] - Heal Player"}
            ,{"/imacheater", "- Use the toolbox to force enable standard cheats on any server"}
            ,{"/infstam", "- Toggles infinite stamina"}
            ,{"/killall", "- Kills all nearby creatures"}
            ,{"/listitems", "[Name Contains] - List all items. Optionally include name starts with. Ex. /listitems Woo returns any item that contains the letters 'Woo'"}
            ,{"/listprefabs", "[Name Contains] - Lists all prefabs - List all prefabs/creatures. Optionally include name starts with. Ex. /listprefabs Troll returns any prefab that starts with the letters 'Troll'"}
            ,{"/listskills", "- Lists all skills"}
            ,{"/nocost", "- Toggle no requirement building"}
            ,{"/nores", "- Toggle no restrictions to where you can build (except ward zones)"}
            ,{"/nosup", "- Toggle no supports required for buildings - WARNING! - IF YOU REJOIN AND THIS IS DISABLED, YOUR STRUCTURES MAY FALL APART - USE WITH CARE. Maybe use the AutoRun functionality?"}
            ,{"/optterrain", "- Optimize old terrain modifications"}
            ,{"/portals", "- List all portal tags"}
            //,{"/randomevent", "- Begins a random event"}
            ,{"/removedrops", "- Removes items from the ground"}
            ,{"/resetwind", "- If wind has been set, this will allow the game to take control of the wind again" }
            ,{"/repair", "- Repair your inventory"}
            ,{"/resetmap", "- Reset the map exploration"}
            ,{"/revealmap", "- Reveals the entire minimap"}
            ,{"/q", "- Quickly exit the game. Commands are sometimes just more convenient."}
            ,{"/seed", "- Reveals the map seed"}
            ,{"/set cw", "[Weight] - Set your weight limit (default 300)"}
            ,{"/set difficulty", "[Player Count] - Set the difficulty (default is number of connected players)"}
            ,{"/set exploreradius", "[Radius=100] - Set the explore radius"}
            ,{"/set jumpforce", "[Force] - Set jump force (default 10). Careful if you fall too far!"}
            ,{"/set pickup", "[Radius] - Set your auto pickup radius (default 2)"}
            ,{"/set skill", "[Skill] [Level] - Set your skill level"}
            ,{"/set speed", "[Speed Type] [Speed] - Speed Types: crouch (def: 2), run (def: 7), swim (def: 2)"}
            ,{"/td", "[Radius=5] [Height=1] - Dig nearby terrain. Radius 30 max."}
            ,{"/tl", "[Radius=5] - Level nearby terrain. Radius 30 max."}
            ,{"/tr", "[Radius=5] [Height=1] - Raise nearby terrain. Radius 30 max."}
            ,{"/tu", "[Radius=5] - Undo terrain modifications around you. Radius 50 max."}
            ,{"/spawn", "[Creature Name] [Level=1] - Spawns a creature or prefab in front of you. Capitals in name matter! Ex. /spawn Boar 3 (use /give for items!)"}
            ,{"/spawntamed", "[Creature Name] [Level=1] - Spawns a tamed creature in front of you. Capitals in name matter! Ex. /spawntamed Boar 3"}
            ,{"/stopevent", "- Stops a current event"}
            ,{"/tame", "- Tame all nearby creatures"}
            ,{"/tod", "[0-1] - Set (and lock) time of day (-1 to unlock time) - Ex. /tod 0.5"}
            ,{"/tp", "[X,Y] - Teleport you to the coords provided" }
            //,{"/tp [x, y] OR /tp [TO PLAYER] [FROM PLAYER=SELF]", "Teleport you to the coords or the target player to target other player If player has a space in name, only use first portion of the name.. "
            //                                                        + "\nEx. /tp 60,40 | /tp Skrip (Teleport to Skrip) | /tp TSkrip FSkrip (Teleport player FSkrip to player TSkrip)"}
            ,{"/wind", "[Angle] [Intensity] - Set the wind direction and intensity"}
            ,{"/whois", "- List all players"}
        };

        public static void Announce()
        {
            if (SkConfigEntry.CScrollable != null & !SkConfigEntry.CScrollable.Value)
            {
                if (SkVersionChecker.VersionCurrent())
                {
                    PrintOut("====  Toolbox (" + SkVersionChecker.currentVersion + ") by Skrip (DS) is enabled.\t\t====", LogTo.Console);
                }
                else
                {
                    PrintOut("Toolbox by Skrip (DS) is enabled.", LogTo.Console);
                    PrintOut("New Version Available on NexusMods!\t► Current: " + SkVersionChecker.currentVersion + " Latest: " + SkVersionChecker.latestVersion, LogTo.Console | LogTo.DebugConsole);
                }

                PrintOut("====  Press numpad 0 to open on-screen menu or type /? 1\t====", LogTo.Console);
            }
            try
            {
                commandList = commandList.OrderBy(obj => obj.Key).ToDictionary(obj => obj.Key, obj => obj.Value); // Try to sort the commands in case I gave up with it eventually, lol.
                                                                                                                  // Add the aliases to the list
                SkCommandPatcher.InitPatch();
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// This decomposes alias commands into commands that can be passed into the processor or back through the console
        /// Alias                   /creative: /god; /ghost; /fly; /nores; /nocost;
        /// User typed			    /god; /creative; /fly
        /// Alias transformed to    /god; /ghost; /fly; /nores; /nocost;
        /// Pass to processor	    /god; /god; /ghost; /fly; /nores; /nocost; /fly;
        /// </summary>
        /// <param name="inCommand"></param>
        /// <returns></returns>
        public static string DecomposeAlias(string inCommand)
        {
            string[] tempCommandSpl = inCommand.Split(';');
            string buildCommand = inCommand; //default if not alias
            if (ConsoleOpt != null && ConsoleOpt.AliasList != null && ConsoleOpt.AliasList.Count > 0)
            {
                for (int commandIndex = 0; commandIndex < tempCommandSpl.Length; commandIndex++)
                {
                    if (!string.IsNullOrEmpty(tempCommandSpl[commandIndex]))
                    {
                        tempCommandSpl[commandIndex] = tempCommandSpl[commandIndex].Trim();
                    }

                    if (ConsoleOpt.AliasList.Keys.Contains(tempCommandSpl[commandIndex]))
                    {
                        string tempCommand = string.Empty;
                        ConsoleOpt.AliasList.TryGetValue(tempCommandSpl[commandIndex], out tempCommand);
                        tempCommand = tempCommand.Trim();
                        if (tempCommand.EndsWith(";"))
                        {
                            tempCommand = tempCommand.Substring(0, tempCommand.Length - 1);
                        }
                        tempCommandSpl[commandIndex] = tempCommand; // we found an alias
                    }
                }
                buildCommand = string.Empty;
                foreach (string str in tempCommandSpl)
                {
                    if (tempCommandSpl.Length > 1)
                    {
                        buildCommand = buildCommand + str + ";";
                    }
                    else
                    {
                        buildCommand = buildCommand + str;
                    }
                }
            }
            return buildCommand; // otherwise return the original command
        }

        public static void ProcessCommands(string inCommand, LogTo source, GameObject go = null)
        {
            if (!string.IsNullOrEmpty(inCommand))
            {
                if (Console.instance != null)
                {
                    // Valheim 1.0: m_lastEntry is gone; the up-arrow history is Terminal.m_history (private).
                    var history = SkUtilities.GetPrivateField<List<string>>(Console.instance, "m_history");
                    if (history != null)
                    {
                        if (history.Count == 0 || history[history.Count - 1] != inCommand)
                        {
                            history.Add(inCommand);
                        }
                        // Valheim 1.0: Mirror Terminal.SendInput(): reset the Up/Down cursor to the end after every submit,
                        // even when the duplicate entry was skipped (Terminal.cs:2927-2931).
                        SkUtilities.SetPrivateField(Console.instance, "m_historyPosition", history.Count);
                    }
                }
                inCommand = inCommand.Trim();

                inCommand = DecomposeAlias(inCommand); // Does the input command contain an alias? Decompose it into separate commands for the processor

                string[] inCommandSplt = inCommand.Split(';');
                foreach (string command in inCommandSplt)
                {
                    string commandTrimmed = command.Trim();
                    if (!string.IsNullOrEmpty(commandTrimmed))
                    {
                        Console.instance.TryRunCommand(commandTrimmed);
                        //if (!ProcessCommand(commandTrimmed, source, go)) // Process SkToolbox Command
                        //{ // Unless the command wasn't found, then push it to the console to try and run it elsewhere
                        //    if (inCommandSplt.Length > 1)
                        //    {
                        //        if (!string.IsNullOrEmpty(commandTrimmed))
                        //        {
                        //            //Console.instance.m_input.text = commandTrimmed;
                        //            //Console.instance.GetType().GetMethod("InputText", SkUtilities.BindFlags).Invoke(Console.instance, null);
                                    
                        //        }
                        //    }
                        //}
                    }
                }
                Console.instance.m_input.text = string.Empty;
            }
        }

        public static void InitCommands()
        {
            
            if(Console.instance != null)
            {
                //var cmdList = SkUtilities.GetPrivateField<Dictionary<string, Terminal.ConsoleCommand>>(Console.instance, "commands");
                //if(cmdList != null)
                //{
                //    PrintOut("Found:" + cmdList.Count);
                //    foreach(KeyValuePair<string, Terminal.ConsoleCommand> entry in cmdList)
                //    {
                //        entry.Value.IsSecret = false;
                //        entry.Value.IsCheat = false;
                //    }
                //}
                new Terminal.ConsoleCommand("echo", "Echo the text back to the console. This is intended for use with aliases and the autorun features. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length > 1)
                    {
                        string str = String.Empty;

                        for (int x = 0; x < args.Length; x++)
                        {
                            str = str + args[x] + " ";
                        }
                            PrintOut(str, LogTo.Console, false);
                    }
                });
                new Terminal.ConsoleCommand("/console", "[1/0] - Toggle the console. No parameter with toggle. 1 = Open, 0 = Closed. Intended for use with hotkeys and aliases. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length > 1)
                    {
                        if (args[1].Equals("1"))
                        {
                            Console.instance.m_chatWindow.gameObject.SetActive(true);
                            SkConfigEntry.CConsoleEnabled.Value = true;
                        }
                        else
                        {
                            Console.instance.m_chatWindow.gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        Console.instance.m_chatWindow.gameObject.SetActive(!Console.instance.m_chatWindow.gameObject.activeSelf);
                        if (Console.instance.m_chatWindow.gameObject.activeInHierarchy)
                        {
                            SkConfigEntry.CConsoleEnabled.Value = true;
                        }
                    }
                });
                new Terminal.ConsoleCommand("/q", "Quickly exit the game. Commands are sometimes just more convenient. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    PrintOut("Quitting game...", LogTo.Console | LogTo.DebugConsole);
                    Application.Quit();
                });
                new Terminal.ConsoleCommand("/clear", "Clear the current output shown in the console. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Console.instance.m_output.text = string.Empty;
                    try
                    {
                        SkUtilities.SetPrivateField(Console.instance, "m_chatBuffer", new List<string>());
                        ConsoleOpt.consoleOutputHistory.Clear();
                    }
                    catch (Exception)
                    {

                    }
                });
                new Terminal.ConsoleCommand("/repair", "Repair your inventory. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    List<ItemDrop.ItemData> itemList = new List<ItemDrop.ItemData>();
                    Player.m_localPlayer.GetInventory().GetWornItems(itemList);
                    foreach (ItemDrop.ItemData itemData in itemList)
                    {
                        try
                        {
                            itemData.m_durability = itemData.GetMaxDurability();
                        }
                        catch (Exception)
                        {

                        }
                    }
                    PrintOut("All items repaired!");
                });
                new Terminal.ConsoleCommand("/portals", "List all portal tags. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    PrintOut(ListPortals(), LogTo.Console);
                });
                new Terminal.ConsoleCommand("/tl", "[Radius=5] - Level nearby terrain. Radius 30 max. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    GameObject tLevel = ZNetScene.instance.GetPrefab("digg_v2");
                    if (tLevel == null)
                    {
                        PrintOut("Terrain level failed. Report to mod author - terrain level error 1");
                        return;
                    }
                    float radius = 5f;
                    if (args.Length > 1)
                    {
                        try
                        {
                            radius = int.Parse(args[1]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    TerrainModification.ModifyTerrain(Player.m_localPlayer.transform.position, tLevel, radius);

                    PrintOut("Terrain levelled!");
                });
                new Terminal.ConsoleCommand("/tu", "[Radius=5] - Undo terrain modifications around you. Radius 50 max. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    float radius = 5f;
                    if (args.Length > 1)
                    {
                        try
                        {
                            radius = int.Parse(args[1]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    TerrainModification.ResetTerrain(Player.m_localPlayer.transform.position, radius);

                    PrintOut("Terrain reset!");
                });
                new Terminal.ConsoleCommand("/tr", "[Radius=5] [Height=1] - Raise nearby terrain. Radius 30 max. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    GameObject tLevel = ZNetScene.instance.GetPrefab("raise");
                    if (tLevel == null)
                    {
                        PrintOut("Terrain raise failed. Report to mod author - terrain raise error 1");
                        return;
                    }
                    float radius = 5f;
                    float height = 2f;
                    if (args.Length > 1)
                    {
                        try
                        {
                            radius = int.Parse(args[1]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (args.Length > 2)
                    {
                        try
                        {
                            height = int.Parse(args[2]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    TerrainModification.ModifyTerrain(Player.m_localPlayer.transform.position + (Vector3.up * height), tLevel, radius);

                    PrintOut("Terrain raised!");
                });
                new Terminal.ConsoleCommand("/td", "[Radius=5] [Height=1] - Dig nearby terrain. Radius 30 max. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    GameObject tLevel = ZNetScene.instance.GetPrefab("digg_v2");
                    if (tLevel == null)
                    {
                        PrintOut("Terrain dig failed. Report to mod author - terrain dig error 1");
                        return;
                    }
                    float radius = 5f;
                    float height = 1f;
                    if (args.Length > 1)
                    {
                        try
                        {
                            radius = int.Parse(args[1]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (args.Length > 2)
                    {
                        try
                        {
                            height = int.Parse(args[2]);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    TerrainModification.ModifyTerrain(Player.m_localPlayer.transform.position - (Vector3.up * height), tLevel, radius);

                    PrintOut("Terrain dug!");
                    return;
                });
                new Terminal.ConsoleCommand("/resetwind", "If wind has been set, this will allow the game to take control of the wind again. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    EnvMan.instance.ResetDebugWind();
                    PrintOut("Wind unlocked and under game control.");
                });
                new Terminal.ConsoleCommand("/wind", "[Angle] [Intensity] - Set the wind direction and intensity. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length == 3)
                    {
                        float angle = float.Parse(args[1]);
                        float intensity = float.Parse(args[2]);
                        EnvMan.instance.SetDebugWind(angle, intensity);
                    }
                    else
                    {
                        PrintOut("Failed to set wind. Check parameters! Ex. /wind 240 5");
                    }
                });
                new Terminal.ConsoleCommand("/env", "[Weather] - Change the weather. No parameter provided will list all weather. -1 will allow the game to control the weather again. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length == 1)
                    {
                        // Valheim 1.0: list the live EnvMan environments instead of the stale hard-coded weatherList.
                        if (EnvMan.instance == null)
                        {
                            PrintOut("Can't find environment manager.");
                            return;
                        }
                        PrintOut("Current environment: " + (EnvMan.instance.GetCurrentEnvironment()?.m_name ?? "(none)"));
                        foreach (string weather in EnvMan.instance.m_environments.Select(e => e.m_name).OrderBy(q => q))
                        {
                            PrintOut(weather);
                        }
                    }
                    else if (args.Length >= 2)
                    {
                        if (args[1].Equals("-1"))
                        {
                            if (EnvMan.instance != null)
                            {
                                EnvMan.instance.m_debugEnv = "";
                                PrintOut("Weather unlocked and under game control.");
                            }
                        }
                        else
                        {
                            string finalWeatherName = string.Empty;
                            if (args.Length > 2)
                            {
                                foreach (string str in args.Args)
                                {
                                    if (!str.Equals(args[0])) // Make sure it doesn't pass /env into the weather name
                                    {
                                        finalWeatherName += " " + str;
                                    }
                                }
                                finalWeatherName = finalWeatherName.Trim();
                            }
                            else
                            {
                                finalWeatherName = args[1];
                            }

                            // Valheim 1.0: validate against EnvMan.m_environments (case-insensitive) instead of the stale weatherList.
                            EnvSetup match = EnvMan.instance != null
                                ? EnvMan.instance.m_environments.FirstOrDefault(e => string.Equals(e.m_name, finalWeatherName, StringComparison.OrdinalIgnoreCase))
                                : null;
                            if (match != null)
                            {
                                EnvMan.instance.m_debugEnv = match.m_name; // canonical name: EnvMan.GetEnv is a case-sensitive exact match
                                PrintOut("Weather set to: " + EnvMan.instance.m_debugEnv);
                            }
                            else if (EnvMan.instance == null)
                            {
                                PrintOut("Failed to set weather to '" + finalWeatherName + "'. Can't find environment manager.");
                            }
                            else
                            {
                                PrintOut("Failed to set weather to '" + finalWeatherName + "'. Unknown environment. Use /env to list, /env -1 to unlock.");
                            }
                        }
                    }
                    else
                    {
                        PrintOut("Failed to set weather. Check parameters! Ex. /env, /env -1, /env Misty");
                    }
                });
                new Terminal.ConsoleCommand("/fly", "Toggle flying. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: use Player.ToggleDebugFly() (flips m_debugFly AND writes ZDOVars.s_debugFly so remote clients/monsters see IsDebugFlying()).
                    Player lp = Player.m_localPlayer;
                    if (lp == null || lp.GetComponent<ZNetView>() == null || !lp.GetComponent<ZNetView>().IsValid())
                    {
                        PrintOut("Fly: no local player yet. Spawn into a world first.");
                        return;
                    }
                    flyEnabled = lp.ToggleDebugFly();          // public, Player.cs:7518; returns the new state
                    Player.m_debugMode = flyEnabled || noCostEnabled || SkCommandPatcher.BCheat; // never stomp debugmode enabled by /imacheater or /nocost
                    PrintOut("Fly toggled! (" + flyEnabled.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/listicons", "[Filter] - List item prefabs that have an icon, for picking menu icons. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (ObjectDB.instance == null || ObjectDB.instance.m_items == null)
                    {
                        PrintOut("Item database is not loaded yet.");
                        return;
                    }
                    string filter = args.Length > 1 ? args[1].ToLower() : string.Empty;
                    int count = 0;
                    foreach (UnityEngine.GameObject prefab in ObjectDB.instance.m_items)
                    {
                        if (prefab == null) continue;
                        ItemDrop drop = prefab.GetComponent<ItemDrop>();
                        if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                        UnityEngine.Sprite[] icons = drop.m_itemData.m_shared.m_icons;
                        if (icons == null || icons.Length == 0 || icons[0] == null) continue;
                        if (filter.Length > 0 && prefab.name.ToLower().IndexOf(filter) < 0) continue;
                        PrintOut(prefab.name);
                        count++;
                    }
                    PrintOut(count + " item icon(s) listed.");
                });

                new Terminal.ConsoleCommand("/alt", "(Removed) The menu is mouse-driven now; press the MenuToggleKey (default F6). (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    SkMenuController menu = SkLoader.Load != null ? SkLoader.Load.GetComponent<SkMenuController>() : null;
                    PrintOut("The menu is mouse-driven now. Press " + (menu != null ? menu.ToggleKey.ToString() : "F6") + " to open it; the /alt keyboard controls were removed.");
                });
                new Terminal.ConsoleCommand("/stopevent", "Stops a current event. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: on a joined server the server re-broadcasts the running event every 2 s (RPC_SetEvent), so a local reset is undone.
                    if (ZNet.instance == null || !ZNet.instance.IsServer())
                    {
                        PrintOut("Events can only be stopped by the host/server (the server re-syncs the active event every 2s).");
                        return;
                    }
                    RandEventSystem.instance.ResetRandomEvent();
                    PrintOut("Event stopped!");
                });
                new Terminal.ConsoleCommand("/revealmap", "Reveals the entire minimap. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Minimap.instance.ExploreAll();
                    PrintOut("Map revealed!");
                });
                new Terminal.ConsoleCommand("/whois", "List all players. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: GetConnectedPeers() is only the server on a client; use the server-synced roster.
                    string playerStr = string.Empty;
                    List<ZNet.PlayerInfo> players = ZNet.instance.GetPlayerList(); // server-synced roster (RPC_PlayerList), valid on host, client and dedicated server
                    foreach (ZNet.PlayerInfo pl in players)
                    {
                        playerStr = playerStr + ", " + pl.m_name + "(" + pl.m_characterID.UserID + ")";
                    }
                    if (playerStr.Length > 2)
                    {
                        playerStr = playerStr.Remove(0, 2);
                    }
                    PrintOut("Active Players (" + players.Count + ") - " + playerStr);
                });
                new Terminal.ConsoleCommand("/give", "[Item] [Qty=1], OR /give [Item] [Qty=1] [Player] [Level=1] - Gives item to player. If player has a space in name, only provide name before the space. Capital letters matter in item / player name! (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    string cmdPlr = Player.m_localPlayer.GetPlayerName();
                    string cmdItem = string.Empty;
                    int cmdQty = 1;
                    int cmdLvl = 1;
                    // Valheim 1.0: ConsoleEventArgs.Args is the raw line split on spaces: args[0] is "/give", real parameters start at args[1]
                    if (args.Length < 2)
                    {
                        PrintOut("Failed. No item provided. /give [Item] [Qty=1] [Player] [Level=1]");
                        return;
                    }
                    cmdItem = args[1];
                    if (args.Length >= 3 && !int.TryParse(args[2], out cmdQty)) cmdQty = 1;
                    if (args.Length >= 4) cmdPlr = args[3];
                    if (args.Length >= 5 && !int.TryParse(args[4], out cmdLvl)) cmdLvl = 1;

                    //bool itemExists = false; // verify the item exists

                    //foreach (GameObject itm in ObjectDB.instance.m_items)
                    //{
                    //    ItemDrop component = itm.GetComponent<ItemDrop>();
                    //    if (component.name.StartsWith(args[0]))
                    //    {
                    //        itemExists = true;
                    //        break;
                    //    }
                    //}

                    //foreach(string Item in ConsoleOpt.ItemList)
                    //{
                    //    if (Item.ToLower().StartsWith(cmdSplt[0]))
                    //    {
                    //        itemExists = true;
                    //        break;
                    //    }
                    //}

                    //if (!itemExists)
                    //{
                    //    PrintOut("Failed. Item does not exist. /give [Item] [Qty=1] [Player] [Level=1]. Check for items with /listitems. Capital letters matter on this command!");
                    //    return;
                    //}

                    Player plrObj = null;
                    foreach (Player pl in Player.GetAllPlayers())
                    {
                        if (pl != null)
                        {
                            if (pl.GetPlayerName().ToLower().StartsWith(cmdPlr.ToLower()))
                            {
                                plrObj = pl;
                                break;
                            }
                        }
                    }

                    if (plrObj == null)
                    {
                        PrintOut("Failed. Player does not exist. /give [Item] [Qty=1] [Player] [Level=1]");
                        return;
                    }

                    GameObject item = ZNetScene.instance.GetPrefab(cmdItem); // Valheim 1.0: args[0] is the command word
                    if (item)
                    {
                        PrintOut("Spawning " + cmdQty + " of item " + cmdItem + "(" + cmdLvl + ") on " + cmdPlr);

                        //for (int x = 0; x < cmdQty; x++)
                        //{
                        try
                        {
                            GameObject createdObj = UnityEngine.Object.Instantiate<GameObject>(item, plrObj.transform.position + plrObj.transform.forward * 1.5f + Vector3.up, Quaternion.identity);
                            ItemDrop itemObj = (ItemDrop)createdObj.GetComponent(typeof(ItemDrop));
                            if (itemObj != null && itemObj.m_itemData != null)
                            {
                                itemObj.SetQuality(cmdLvl); // Valheim 1.0: public; sets m_quality and updates the drop's localScale like vanilla spawn
                                itemObj.m_itemData.m_stack = cmdQty;
                                itemObj.m_itemData.m_durability = itemObj.m_itemData.GetMaxDurability();
                            }
                        }
                        catch (Exception ex)
                        {
                            PrintOut("Something unexpected failed.");
                            SkUtilities.Logz(new string[] { "ERR", "/give" }, new string[] { ex.Message, ex.Source }, LogType.Warning);
                        }
                        //}
                    }
                    else
                    {
                        PrintOut("Failed. Check parameters. /give [Item] [Qty=1] [Player] [Level=1]");
                    }
                    return;
                });
                new Terminal.ConsoleCommand("/god", "Toggle Godmode. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: derive the toggle from the live Player state (m_godMode is per Player instance, the static survives relog).
                    if (Player.m_localPlayer == null)
                    {
                        PrintOut("No local player.");
                        return;
                    }
                    godEnabled = !Player.m_localPlayer.InGodMode(); // Player.cs:4470
                    Player.m_localPlayer.SetGodMode(godEnabled);    // Player.cs:4465
                    PrintOut("Godmode " + (godEnabled ? "ON" : "OFF")
                        + (godEnabled && (SkConfigEntry.CGodModeBlocksDamage == null || SkConfigEntry.CGodModeBlocksDamage.Value)
                           ? " - damage is blocked outright." : "."));
                });
                new Terminal.ConsoleCommand("/clearinventory", "Removes all items from your inventory. There is no confirmation, be careful. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Player.m_localPlayer.UnequipAllItems(); // Valheim 1.0: RemoveAll() leaves Humanoid m_rightItem/m_leftItem/armor slots pointing at removed ItemData
                    Player.m_localPlayer.GetInventory().RemoveAll();
                    PrintOut("All items removed from inventory.");
                });
                new Terminal.ConsoleCommand("/findtomb", "Pin nearby dead player tombstones on the map if any currently exist. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    TombStone[] listTraders = GameObject.FindObjectsByType<TombStone>(FindObjectsSortMode.None);
                    if (listTraders.Length > 0)
                    {
                        foreach (TombStone tr in listTraders)
                        {
                            if (tr != null && tr.enabled)
                            {
                                Minimap.instance.AddPin(tr.transform.position, Minimap.PinType.Ping, "TS", true, true);
                            }
                        }
                    }
                    PrintOut("Tombstone sought out! Potentially " + listTraders.Length + " found.");
                    return;
                });
                new Terminal.ConsoleCommand("/seed", "Reveals the map seed. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    World wrld = SkUtilities.GetPrivateField<World>(WorldGenerator.instance, "m_world");
                    PrintOut("Map seed: " + wrld.m_seedName);
                });
                new Terminal.ConsoleCommand("/freecam", "Toggle freecam. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    GameCamera.instance.ToggleFreeFly();
                    PrintOut("Free cam toggled " + GameCamera.InFreeFly().ToString());
                });
                new Terminal.ConsoleCommand("/heal", "[Player=local] - Heal Player. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length > 1)
                    {
                        foreach (Player pl in Player.GetAllPlayers())
                        {
                            if (pl != null)
                            {
                                if (pl.GetPlayerName().ToLower().Equals(args[1].ToLower()))
                                {
                                    pl.Heal(pl.GetMaxHealth());
                                    PrintOut("Player healed: " + pl.GetPlayerName());
                                }
                            }
                        }
                    }
                    else
                    {
                        Player.m_localPlayer.Heal(Player.m_localPlayer.GetMaxHealth(), true);
                        PrintOut("Self healed.");
                    }
                });
                new Terminal.ConsoleCommand("/nores", "Toggle no restrictions to where you can build (except ward zones). (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    SkCommandPatcher.InitPatch();
                    SkCommandPatcher.bBuildAnywhere = !SkCommandPatcher.bBuildAnywhere;
                    PrintOut("No build restrictions toggled! (" + SkCommandPatcher.bBuildAnywhere.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/nocost", "Toggle no requirement building. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: use the public setter (refreshes the hammer piece table) and derive the state from the live Player.
                    Player lp = Player.m_localPlayer;
                    if (lp == null)
                    {
                        PrintOut("No local player.");
                        return;
                    }
                    noCostEnabled = !lp.NoCostCheat();         // Player.cs:6177
                    lp.SetNoPlacementCost(noCostEnabled);      // public, Player.cs:7526; calls UpdateAvailablePiecesList() so "unlock all pieces" applies immediately
                    Player.m_debugMode = flyEnabled || noCostEnabled || SkCommandPatcher.BCheat; // never stomp debugmode enabled by /imacheater or /fly
                    PrintOut("No build cost/requirements toggled! (" + noCostEnabled.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/tp", "[X,Y] - Teleport you to the coords provided. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length != 2 || !args[1].Contains(","))
                    {
                        PrintOut("Syntax /tp X,Z");
                        return;
                    }
                    try
                    {
                        string[] loc = args[1].Split(',');
                        float x = float.Parse(loc[0]);
                        float z = float.Parse(loc[1]);
                        // Valheim 1.0: non-distant TeleportTo bounces back ("$msg_portal_blocked") when no floor is within 1 m; use distantTeleport like vanilla goto.
                        float y = ZoneSystem.instance.GetGroundHeight(new Vector3(x, 750, z));
                        bool haveGround = y < 749f; // GetGroundHeight returns the 750 we passed in when the zone's terrain collider is not loaded
                        Player localPlayer2 = Player.m_localPlayer;
                        if (localPlayer2)
                        {
                            float targetY;
                            if (haveGround)
                            {
                                // Zone is loaded: land 0.5 m above the terrain (never below the water surface).
                                targetY = Mathf.Max(y + 0.5f, ZoneSystem.instance.m_waterLevel);
                            }
                            else
                            {
                                // Zone not loaded: same as vanilla `goto` - y=30 (water level) unless debug-flying.
                                // All land is above y=30, so FindFloor fails and Player.UpdateTeleport lands us on
                                // ZoneSystem.GetSolidHeight(target)+0.5 after 15 s instead of dropping us from our current height.
                                float max = localPlayer2.IsDebugFlying() ? 400f : 30f;
                                targetY = Mathf.Clamp(localPlayer2.transform.position.y, 30f, max);
                            }
                            Vector3 pos2 = new Vector3(x, targetY, z);
                            // distantTeleport: true waits for the zone to load and never bounces back with "$msg_portal_blocked".
                            if (localPlayer2.TeleportTo(pos2, localPlayer2.transform.rotation, true))
                                PrintOut("Teleporting...");
                            else
                                PrintOut("Teleport refused (already teleporting or <2s since last teleport). Try again in a moment.");
                        }
                    }
                    catch (Exception)
                    {
                        PrintOut("Syntax /tp X,Z");
                    }
                });
                new Terminal.ConsoleCommand("/detect", "[Range=20] - Toggle enemy detection. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    bDetectEnemies = !bDetectEnemies;
                    if (args.Length > 0)
                    {
                        try
                        {
                            bDetectRange = int.Parse(args[1]);
                            bDetectRange = bDetectRange < 5 ? 5 : bDetectRange;
                        }
                        catch (Exception)
                        {
                            bDetectRange = 20;
                        }
                    }
                    PrintOut("Detect enemies toggled! (" + bDetectEnemies.ToString() + ", range: " + bDetectRange + ")");
                });
                new Terminal.ConsoleCommand("/imacheater", "Use the toolbox to force enable standard cheats on any server. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    SkCommandPatcher.InitPatch();
                    SkCommandPatcher.BCheat = !SkCommandPatcher.BCheat;

                    try
                    {
                        Player.m_debugMode = SkCommandPatcher.BCheat; // public static since Valheim 1.0
                        Terminal.m_cheat = SkCommandPatcher.BCheat;   // moved to Terminal and made public static

                        // Valheim 1.0: ConsoleCommand.RunAction refuses every isCheat command until
                        // Achievements.IsCheatedAtAll() is true and otherwise only prints
                        // "$achievements_confirm_cheat". Vanilla clears that by making the user type
                        // 'confirmcheats' once, whose only effect on the local player is
                        // PlayerProfile.m_usedCheats = true (persisted). Do the same here so the first
                        // vanilla cheat after /imacheater works. (IsCheatedAtAll is cached per frame, so a
                        // vanilla cheat chained in the same command line still needs one more attempt.)
                        if (SkCommandPatcher.BCheat && Game.instance != null)
                        {
                            PlayerProfile profile = Game.instance.GetPlayerProfile();
                            if (profile != null && !profile.m_usedCheats)
                            {
                                profile.m_usedCheats = true;
                                PrintOut(Localization.instance.Localize("$achievements_permanently_cheated_character"));
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }

                    // Valheim 1.0: Terminal.m_commandList is built lazily once and only refreshed by updateCommandList() (vanilla devcommands calls it),
                    // otherwise tab completion / search keep the pre-toggle list until the next map load.
                    if (args.Context != null)
                    {
                        SkUtilities.InvokePrivateMethod(args.Context, "updateCommandList", null);
                    }
                    if (Console.instance != null && !ReferenceEquals(Console.instance, args.Context))
                    {
                        SkUtilities.InvokePrivateMethod(Console.instance, "updateCommandList", null);
                    }

                    PrintOut("Cheats toggled! (" + SkCommandPatcher.BCheat.ToString() + ")");
                    if (SkCommandPatcher.BCheat)
                    {
                        PrintOut("Vanilla cheat commands (god/fly/ghost/nocost/debugmode...) are now valid on this client, including on joined servers.");
                    }
                });
                new Terminal.ConsoleCommand("/nosup", "Toggle no supports required for buildings - WARNING! - IF YOU REJOIN AND THIS IS DISABLED, YOUR STRUCTURES MAY FALL APART - USE WITH CARE. Maybe use the AutoRun functionality? (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    SkCommandPatcher.InitPatch();
                    SkCommandPatcher.BFreeSupport = !SkCommandPatcher.BFreeSupport;
                    PrintOut("No build support requirements toggled! (" + SkCommandPatcher.BFreeSupport.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/coords", "Show coords in corner of the screen. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    bCoords = !bCoords;
                    PrintOut("Show coords toggled! (" + bCoords.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/resetmap", "Reset the map exploration. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Minimap.instance.Reset();
                });
                new Terminal.ConsoleCommand("/infstam", "Toggles infinite stamina. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: SetMaxStamina(9999f) removed - Player.UpdateFood recomputes max stamina from food
                    // every second, so it was dead code that only flashed the bar.
                    Player lpStam = Player.m_localPlayer;
                    if (lpStam == null)
                    {
                        PrintOut("Infinite stamina: no local player yet. Spawn into a world first.");
                        return;
                    }
                    infStamina = !infStamina;
                    ApplyInfStamina(lpStam, applyOffValues: true);
                    PrintOut("Infinite stamina toggled! (" + infStamina.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/tame", "Tame all nearby creatures. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Tameable.TameAllInArea(Player.m_localPlayer.transform.position, 20f);
                    PrintOut("Creatures tamed!");
                });
                new Terminal.ConsoleCommand("/farinteract", "[Distance=50] - Toggles far interactions (building as well). To change distance, toggle this off then back on with new distance. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Player lpReach = Player.m_localPlayer;
                    if (lpReach == null)
                    {
                        PrintOut("Far interactions: no local player yet. Spawn into a world first.");
                        return;
                    }
                    farInteract = !farInteract;
                    if (farInteract && args.Length > 1)
                    {
                        try
                        {
                            // Kept on a static rather than only on the player, so a respawn can restore the reach.
                            farInteractDistance = int.Parse(args[1]) < 20 ? 20 : int.Parse(args[1]);
                        }
                        catch (Exception)
                        {
                            PrintOut("Failed to set far interaction distance. Check params. /farinteract 50");
                        }
                    }
                    ApplyFarInteract(lpReach, applyOffValues: true);
                    PrintOut(farInteract
                        ? "Far interactions toggled! (True Distance: " + farInteractDistance + ")"
                        : "Far interactions toggled! (False)");
                });
                new Terminal.ConsoleCommand("/ghost", "Toggle Ghostmode (enemy creatures cannot see you). (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Player lpGhost = Player.m_localPlayer;
                    if (lpGhost == null)
                    {
                        PrintOut("Ghost: no local player yet. Spawn into a world first.");
                        return;
                    }
                    lpGhost.SetGhostMode(!lpGhost.InGhostMode());
                    ghostEnabled = lpGhost.InGhostMode(); // so a respawn can put it back
                    PrintOut("Ghost mode toggled! (" + ghostEnabled.ToString() + ")");
                });
                new Terminal.ConsoleCommand("/tod", "[0-1] - Set (and lock) time of day (-1 to unlock time). Ex. /tod 0.5 (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args) // Valheim 1.0: description was a copy-paste from /portals
                {
                    if (args.Length > 1)
                    {
                        float num10;
                        if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out num10))
                        {
                            return;
                        }
                        if (num10 < 0f)
                        {
                            EnvMan.instance.m_debugTimeOfDay = false;
                            PrintOut("Time unlocked and under game control.");
                        }
                        else
                        {
                            EnvMan.instance.m_debugTimeOfDay = true;
                            EnvMan.instance.m_debugTime = Mathf.Clamp01(num10);
                            PrintOut("Setting time of day:" + num10);
                        }
                    }
                    else
                    {
                        PrintOut("Failed. Syntax /tod [0-1] Ex. /tod 0.5");
                    }
                });
                new Terminal.ConsoleCommand("/optterrain", "Optimize old terrain modifications. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    TerrainComp.UpgradeTerrain();
                    Heightmap.UpdateTerrainAlpha(); // Valheim 1.0: vanilla optterrain also re-syncs the paint-mask alpha into the TerrainComp
                });
                new Terminal.ConsoleCommand("/set", "[Option] [value] [value]. Option can be one of [cw,difficulty,exploreradius,jumpforce,pickup,skill,speed]. All options take 1 value except [skill,speed]. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length > 1)
                    {
                        if (args[1].Equals("cw"))
                        {
                            try
                            {
                                int newWeight = int.Parse(args[2]);
                                Player.m_localPlayer.m_maxCarryWeight = newWeight;
                                PrintOut("New carry weight set to: " + newWeight);
                            }
                            catch (Exception)
                            {
                                PrintOut("Failed to set new carry weight. Check params.");
                            }
                            return;
                        }

                        if (args[1].Equals("skill"))
                        {
                            if (args.Length == 4 && !args[2].Contains("None") && !args[2].Contains("All") && !args[2].Contains("FireMagic") && !args[2].Contains("FrostMagic"))
                            {
                                try
                                {
                                    string cmdSkill = args[2];
                                    int cmdLvl = int.Parse(args[3]);
                                    Player.m_localPlayer.GetSkills().CheatResetSkill(cmdSkill.ToLower());
                                    Player.m_localPlayer.GetSkills().CheatRaiseSkill(cmdSkill.ToLower(), (float)cmdLvl);
                                    return;
                                }
                                catch (Exception)
                                {
                                    PrintOut("Failed to set skill. Check params / skill name. See /listskills. /set skill [skill] [level]");
                                    return;
                                }
                            }
                            PrintOut("Failed to set skill. Check params / skill name. See /listskills.  /set skill [skill] [level]");
                            return;
                        }

                        if (args[1].Equals("pickup"))
                        {
                            if (args.Length >= 3)
                            {
                                try
                                {
                                    int cmdRange = int.Parse(args[2]);
                                    Player.m_localPlayer.m_autoPickupRange = cmdRange;
                                    PrintOut("New range set to: " + cmdRange);
                                    return;
                                }
                                catch (Exception)
                                {
                                    PrintOut("Failed to set pickup range. Check params. /set pickup 2");
                                    return;
                                }
                            }

                            PrintOut("Failed to set pickup range. Check params.  /set pickup 2");
                            return;
                        }

                        if (args[1].Equals("jumpforce"))
                        {
                            if (args.Length >= 3)
                            {
                                try
                                {
                                    int cmdRange = int.Parse(args[2]);
                                    Player.m_localPlayer.m_jumpForce = cmdRange;
                                    PrintOut("New range set to: " + cmdRange);
                                    return;
                                }
                                catch (Exception)
                                {
                                    PrintOut("Failed to set jump force. Check params. /set jumpforce 10");
                                    return;
                                }
                            }

                            PrintOut("Failed to set jump force. Check params.  /set jumpforce 10");
                            return;
                        }

                        if (args[1].Equals("exploreradius"))
                        {
                            if (args.Length >= 3)
                            {
                                try
                                {
                                    int cmdRange = int.Parse(args[2]);
                                    Minimap.instance.m_exploreRadius = cmdRange;
                                    PrintOut("New range set to: " + cmdRange);
                                    return;
                                }
                                catch (Exception)
                                {
                                    PrintOut("Failed to set explore radius. Check params. /set exploreradius 100");
                                    return;
                                }
                            }

                            PrintOut("Failed to set explore radius. Check params.  /set exploreradius 100");
                            return;
                        }

                        if (args[1].Equals("speed"))
                        {
                            int cmdSpeed;
                            if (args.Length >= 4)
                            {
                                string cmdType = args[2];
                                String[] types = { "crouch", "run", "swim" };
                                if (types.Contains(cmdType))
                                {
                                    float wasSpeed = 0f;
                                    try
                                    {

                                        cmdSpeed = int.Parse(args[3]);
                                        switch (cmdType)
                                        {
                                            case "crouch":
                                                wasSpeed = Player.m_localPlayer.m_crouchSpeed;
                                                Player.m_localPlayer.m_crouchSpeed = cmdSpeed;
                                                break;
                                            case "run":
                                                wasSpeed = Player.m_localPlayer.m_runSpeed;
                                                Player.m_localPlayer.m_runSpeed = cmdSpeed;
                                                break;
                                            case "swim":
                                                wasSpeed = Player.m_localPlayer.m_swimSpeed;
                                                Player.m_localPlayer.m_swimSpeed = cmdSpeed;
                                                break;
                                        }
                                        PrintOut("New " + cmdType + " speed set to: " + cmdSpeed + " (was: " + wasSpeed + ")");
                                    }
                                    catch (Exception)
                                    {
                                        PrintOut("Failed to set speed. Check params name. Ex. /set speed crouch 2");
                                        return;
                                    }
                                }
                                else
                                {
                                    PrintOut("Failed to set speed. Check params name. Ex.  /set speed crouch 2");
                                }
                                return;
                            }
                            PrintOut("Failed to set speed. Check params name. Ex.  /set speed crouch 2");
                            return;
                        }

                        if (args[1].Equals("difficulty"))
                        {
                            if (args.Length >= 3)
                            {
                                try
                                {
                                    int diffLvl = int.Parse(args[2]);
                                    Game.instance.SetForcePlayerDifficulty(diffLvl);
                                    PrintOut("Difficulty set to " + diffLvl.ToString());
                                    return;
                                }
                                catch (Exception)
                                {
                                    PrintOut("Failed to set difficulty. Check params. /set difficulty 5");
                                    return;
                                }
                            }
                            PrintOut("Failed to set difficulty. Check params.  /set difficulty 5");
                            return;
                        }
                    }
                });
                new Terminal.ConsoleCommand("/removedrops", "Removes items from the ground. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    // Valheim 1.0: skip live fish and item-pieces (like vanilla removedrops) and claim ownership so ZNetScene.Destroy really destroys the ZDO.
                    ItemDrop[] array2 = UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None);
                    int removed = 0;
                    for (int i = 0; i < array2.Length; i++)
                    {
                        Fish fish = array2[i].GetComponent<Fish>();
                        if ((fish && !fish.IsOutOfWater()) || array2[i].IsPiece())
                        {
                            continue; // live fish and items placed as building pieces are not 'drops' (matches vanilla Terminal.cs:999-1000)
                        }
                        ZNetView component = array2[i].GetComponent<ZNetView>();
                        if (component && component.IsValid())
                        {
                            if (!component.IsOwner())
                            {
                                component.ClaimOwnership(); // otherwise ZNetScene.Destroy (ZNetScene.cs:124) skips ZDOMan.DestroyZDO and the item respawns; same pattern as Fish.RPC_Pickup (Fish.cs:313-314)
                            }
                            component.Destroy();
                            removed++;
                        }
                    }
                    PrintOut("Items cleared (" + removed + ").");
                });
                // Valheim 1.0: /spawntamed no longer re-invokes itself (StackOverflow) nor mass-tames via TameAllInArea (which now ignores point/radius); it tames only the spawned creature.
                new Terminal.ConsoleCommand("/spawntamed", "[Creature Name] [Level=1] - Spawns a tamed creature in front of you. Capitals in name matter! Ex. /spawntamed Boar 3. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    GameObject spawned = SpawnPrefab(args);
                    if (spawned == null) return;
                    MonsterAI ai = spawned.GetComponent<MonsterAI>();
                    if (ai == null)
                    {
                        PrintOut("Spawned, but " + args[1] + " has no MonsterAI and cannot be tamed.");
                        return;
                    }
                    ai.MakeTame(); // public in 1.0.7; -> Character.SetTamed -> RPC_SetTamed on the locally-owned fresh ZDO
                    Tameable tameable = spawned.GetComponent<Tameable>();
                    if (tameable != null)
                    {
                        tameable.m_tamedEffect.Create(spawned.transform.position, spawned.transform.rotation);
                    }
                    PrintOut("Spawned tamed - " + args[1]);
                });
                // Valheim 1.0: /spawn body moved to SpawnPrefab(); the dead "spawn_id"/"alive_time" writes onto a random ZDO were dropped.
                new Terminal.ConsoleCommand("/spawn", "[Creature Name] [Level=1] - Spawns a creature or prefab in front of you. Capitals in name matter! Ex. /spawn Boar 3 (use /give for items!) (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    GameObject spawned = SpawnPrefab(args);
                    if (spawned != null)
                    {
                        PrintOut("Spawned - " + args[1]);
                    }
                });
                new Terminal.ConsoleCommand("/killall", "Kills all nearby creatures. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    List<Character> CharList = new List<Character>();
                    Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, CharList);
                    foreach (Character character in CharList)
                    {
                        if (!character.IsPlayer())
                        {
                            HitData hitData = new HitData();
                            hitData.m_damage.m_damage = 1E+10f;
                            character.Damage(hitData);
                        }
                    }
                    PrintOut("Nearby creatures killed! (50m)");
                });
                new Terminal.ConsoleCommand("/listitems", "[Name Contains] - List all items. Optionally include name starts with. Ex. /listitems Woo returns any item that contains the letters 'Woo'. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    if (args.Length > 1)
                    { //starts with
                        foreach (GameObject gameObject in ObjectDB.instance.m_items)
                        {
                            ItemDrop component = gameObject.GetComponent<ItemDrop>();
                            if (component.name.ToLower().Contains(args[1].ToLower()))
                            {
                                PrintOut("Item: '" + component.name + "'", LogTo.Console | LogTo.DebugConsole);
                            }
                        }
                    }
                    else
                    { // return all
                        foreach (GameObject gameObject in ObjectDB.instance.m_items)
                        {
                            ItemDrop component = gameObject.GetComponent<ItemDrop>();
                            PrintOut("Item: '" + component.name + "'", LogTo.Console | LogTo.DebugConsole);
                        }
                    }
                });
                new Terminal.ConsoleCommand("/listprefabs", "[Name Contains] - Lists all prefabs - List all prefabs / creatures. Optionally include name starts with.Ex. / listprefabs Troll returns any prefab that starts with the letters 'Troll'. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    ConsoleOpt.BuildPrefabs();
                    if (args.Length > 1)
                    { //starts with
                        foreach (string prefab in ConsoleOpt.PrefabList)
                        {
                            if (prefab.ToLower().StartsWith(args[1].ToLower()))
                            {
                                PrintOut("Prefab: '" + prefab + "'", LogTo.Console | LogTo.DebugConsole);
                            }
                        }
                    }
                    else
                    { // return all
                        foreach (string prefab in ConsoleOpt.PrefabList)
                        {
                            PrintOut("Prefab: '" + prefab + "'", LogTo.Console | LogTo.DebugConsole);
                        }
                    }
                });
                new Terminal.ConsoleCommand("/listskills", "Lists all skills. (Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    string skillList = "Skills found: ";
                    foreach (object obj in Enum.GetValues(typeof(Skills.SkillType)))
                    {
                        if (!obj.ToString().Contains("None") && !obj.ToString().Contains("All") && !obj.ToString().Contains("FireMagic") && !obj.ToString().Contains("FrostMagic"))
                            skillList = skillList + obj.ToString() + ", ";
                    }
                    skillList = skillList.Remove(skillList.Length - 2);
                    PrintOut(skillList, LogTo.Console | LogTo.DebugConsole);
                    return;
                });
                new Terminal.ConsoleCommand("/?", "(Speelo's Toolbox)", delegate (Terminal.ConsoleEventArgs args)
                {
                    Console.instance.TryRunCommand("help");
                    return;
                }, false, false, false, false, true);

            }
        }

        public static bool ProcessCommand(string inCommand, LogTo source, GameObject go = null)
        {
            Console.instance.TryRunCommand(inCommand);
            return true;
        }


        public static bool ProcessCommand(string inCommand, LogTo source, GameObject go, bool TEMP)
        {
            // This is effectively disabled at this time.
            if (string.IsNullOrEmpty(inCommand) || string.IsNullOrWhiteSpace(inCommand))
            {
                return true;
            }
            else
            {
                inCommand = inCommand.Trim();
            }

            string[] inCommandSpl = inCommand.Split(' ');

            if (inCommand.StartsWith("help") && source.HasFlag(LogTo.Console))
            {
                Console.instance.Print("devcommands - Enable standard developer/cheat commands");
                Console.instance.Print("/? [Page] - Speelo's Toolbox Commands - Pages 1 through " + (Mathf.Ceil(commandList.Count / pageSize) + (commandList.Count % pageSize == 0 ? 0 : 1)) + " - Example /? 1");
                Console.instance.Print("Speelo's Toolbox - Close the console and press " + (SkLoader.Load != null && SkLoader.Load.GetComponent<SkMenuController>() != null ? SkLoader.Load.GetComponent<SkMenuController>().ToggleKey.ToString() : "F6") + " to open the menu.");
                return false;
            }

            if (inCommandSpl[0].Equals("/?"))
            {
                int displayPage = 1;
                if (inCommandSpl.Length > 1 && int.TryParse(inCommandSpl[1], out displayPage))
                {
                    if (displayPage > (Mathf.Ceil(commandList.Count / pageSize) + (commandList.Count % pageSize == 0 ? 0 : 1)))
                    {
                        displayPage = Mathf.RoundToInt(Mathf.Ceil(commandList.Count / pageSize) + (commandList.Count % pageSize == 0 ? 0 : 1));
                    }
                    List<string> commands = new List<string>(commandList.Keys);
                    List<string> descriptions = new List<string>(commandList.Values);
                    PrintOut("Command List Page " + displayPage + " / " + (Mathf.Ceil(commandList.Count / pageSize) + (commandList.Count % pageSize == 0 ? 0 : 1)));
                    for (int x = ((pageSize * displayPage) - pageSize); // This will iterate over all items by page. The ternary on next line allows final page to have correct number of elements
                            x < ((commandList.Count > ((pageSize * (displayPage + 1)) - pageSize)) ? ((pageSize * (displayPage + 1)) - pageSize) : commandList.Count);
                            // Example: Is 35 items > (( 10 * (2 + 1)) - 10)? If it is, then there is another page after this one, and we can select a full page worth of items. Otherwise, use the final menu item as the end so we over get an index exception.
                            x++) // This selects the correct menu items to display for this page number
                    {
                        PrintOut(commands[x] + " " + descriptions[x]);
                    }

                }
                else
                {
                    PrintOut("Type /? # to see the help for that page number. Ex. /? 1", source, false);
                }

                return true;
            }

            //if (inCommand.StartsWith("/echo") && source.HasFlag(LogTo.Console))
            //{
            //    if (inCommandSpl.Length > 1)
            //    {
            //        string str = String.Empty;

            //        for (int x = 0; x < Ass)

            //        string tempStr = inCommand.Remove(0, 5);
            //        tempStr = tempStr.Trim();
            //        PrintOut(tempStr, source, false);
            //    }
            //    return true;
            //}

            //if (inCommand.StartsWith("/console"))
            //{
            //    if (Console.instance != null)
            //    {
            //        if (inCommandSpl.Length > 1)
            //        {
            //            if (inCommandSpl[1].Equals("1"))
            //            {
            //                Console.instance.m_chatWindow.gameObject.SetActive(true);
            //                SkConfigEntry.CConsoleEnabled.Value = true;
            //            }
            //            else
            //            {
            //                Console.instance.m_chatWindow.gameObject.SetActive(false);
            //            }
            //        }
            //        else
            //        {
            //            Console.instance.m_chatWindow.gameObject.SetActive(!Console.instance.m_chatWindow.gameObject.activeSelf);
            //            if (Console.instance.m_chatWindow.gameObject.activeInHierarchy)
            //            {
            //                SkConfigEntry.CConsoleEnabled.Value = true;
            //            }
            //        }
            //    }
            //    return true;
            //}
            
            //if (inCommandSpl[0].Equals("/q"))
            //{
            //    PrintOut("Quitting game...", source | LogTo.DebugConsole);
            //    Application.Quit();
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/clear"))
            //{
            //    if (Console.instance != null)
            //    {
            //        Console.instance.m_output.text = string.Empty;
            //        try
            //        {
            //            SkUtilities.SetPrivateField(Console.instance, "m_chatBuffer", new List<string>());
            //            ConsoleOpt.consoleOutputHistory.Clear();
            //        }
            //        catch (Exception)
            //        {

            //        }
            //    }
            //    return true;
            //}


            //if (commandList.ContainsKey(inCommandSpl[0]) && Player.m_localPlayer == null)
            //{
            //    PrintOut("You must be in-game to run this command.", source, false);
            //    return true;
            //}


            //if (inCommandSpl[0].Equals("/repair"))
            //{
            //    List<ItemDrop.ItemData> itemList = new List<ItemDrop.ItemData>();
            //    Player.m_localPlayer.GetInventory().GetWornItems(itemList);
            //    foreach (ItemDrop.ItemData itemData in itemList)
            //    {
            //        try
            //        {
            //            itemData.m_durability = itemData.GetMaxDurability();
            //        }
            //        catch (Exception)
            //        {

            //        }
            //    }
            //    PrintOut("All items repaired!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/portals"))
            //{
            //    PrintOut(ListPortals(), source, true);
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/tl"))
            //{
            //    GameObject tLevel = ZNetScene.instance.GetPrefab("digg_v2");
            //    if (tLevel == null)
            //    {
            //        PrintOut("Terrain level failed. Report to mod author - terrain level error 1");
            //        return true;
            //    }
            //    float radius = 5f;
            //    if (inCommandSpl.Length > 1)
            //    {
            //        try
            //        {
            //            radius = int.Parse(inCommandSpl[1]);
            //        }
            //        catch (Exception)
            //        {
            //        }
            //    }
            //    TerrainModification.ModifyTerrain(Player.m_localPlayer.transform.position, tLevel, radius);

            //    PrintOut("Terrain levelled!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/tu"))
            //{
            //    float radius = 5f;
            //    if (inCommandSpl.Length > 1)
            //    {
            //        try
            //        {
            //            radius = int.Parse(inCommandSpl[1]);
            //        }
            //        catch (Exception)
            //        {
            //        }
            //    }
            //    TerrainModification.ResetTerrain(Player.m_localPlayer.transform.position, radius);

            //    PrintOut("Terrain reset!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/tr"))
            //{
            //    GameObject tLevel = ZNetScene.instance.GetPrefab("raise");
            //    if (tLevel == null)
            //    {
            //        PrintOut("Terrain raise failed. Report to mod author - terrain raise error 1");
            //        return true;
            //    }
            //    float radius = 5f;
            //    float height = 2f;
            //    if (inCommandSpl.Length > 1)
            //    {
            //        try
            //        {
            //            radius = int.Parse(inCommandSpl[1]);
            //        }
            //        catch (Exception)
            //        {
            //        }
            //    }
            //    if (inCommandSpl.Length > 2)
            //    {
            //        try
            //        {
            //            height = int.Parse(inCommandSpl[2]);
            //        }
            //        catch (Exception)
            //        {
            //        }
            //    }
            //    TerrainModification.ModifyTerrain(Player.m_localPlayer.transform.position + (Vector3.up * height), tLevel, radius);

            //    PrintOut("Terrain raised!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/td"))
            //{
            //    GameObject tLevel = ZNetScene.instance.GetPrefab("digg_v2");
            //    if (tLevel == null)
            //    {
            //        PrintOut("Terrain dig failed. Report to mod author - terrain dig error 1");
            //        return true;
            //    }
            //    float radius = 5f;
            //    float height = 1f;
            //    if (inCommandSpl.Length > 1)
            //    {
            //        try
            //        {
            //            radius = int.Parse(inCommandSpl[1]);
            //        }
            //        catch (Exception)
            //        {
            //        }
            //    }
            //    if (inCommandSpl.Length > 2)
            //    {
            //        try
            //        {
            //            height = int.Parse(inCommandSpl[2]);
            //        }
            //        catch (Exception)
            //        {
            //        }
            //    }
            //    TerrainModification.ModifyTerrain(Player.m_localPlayer.transform.position - (Vector3.up * height), tLevel, radius);

            //    PrintOut("Terrain dug!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/resetwind"))
            //{
            //    EnvMan.instance.ResetDebugWind();
            //    PrintOut("Wind unlocked and under game control.");
            //}

            //if (inCommandSpl[0].Equals("/wind"))
            //{
            //    string[] inCommandSpli = inCommand.Split(' ');
            //    if (inCommandSpli.Length == 3)
            //    {
            //        float angle = float.Parse(inCommandSpli[1]);
            //        float intensity = float.Parse(inCommandSpli[2]);
            //        EnvMan.instance.SetDebugWind(angle, intensity);
            //    }
            //    else
            //    {
            //        PrintOut("Failed to set wind. Check parameters! Ex. /wind 240 5");
            //    }
            //}

            //if (inCommandSpl[0].Equals("/env"))
            //{
            //    if (inCommandSpl.Length == 1)
            //    {
            //        foreach (string weather in weatherList.OrderBy(q => q).ToList())
            //        {
            //            PrintOut(weather, source, false);
            //        }
            //    }
            //    else if (inCommandSpl.Length >= 2)
            //    {
            //        if (inCommandSpl[1].Equals("-1"))
            //        {
            //            if (EnvMan.instance != null)
            //            {
            //                EnvMan.instance.m_debugEnv = "";
            //                PrintOut("Weather unlocked and under game control.");
            //            }
            //        }
            //        else
            //        {
            //            string finalWeatherName = string.Empty;
            //            if (inCommandSpl.Length > 2)
            //            {
            //                foreach (string str in inCommandSpl)
            //                {
            //                    if (!str.Equals(inCommandSpl[0])) // Make sure it doesn't pass /env into the weather name
            //                    {
            //                        finalWeatherName += " " + str;
            //                    }
            //                }
            //                finalWeatherName = finalWeatherName.Trim();
            //            }
            //            else
            //            {
            //                finalWeatherName = inCommandSpl[1];
            //            }

            //            if (weatherList.Contains(finalWeatherName))
            //            {
            //                if (EnvMan.instance != null)
            //                {
            //                    EnvMan.instance.m_debugEnv = finalWeatherName;
            //                    PrintOut("Weather set to: " + EnvMan.instance.m_debugEnv);
            //                }
            //                else
            //                {
            //                    PrintOut("Failed to set weather to '" + finalWeatherName + "'. Can't find environment manager.");
            //                }
            //            }
            //            else
            //            {
            //                PrintOut("Failed to set weather to '" + finalWeatherName + "'. Check parameters! Ex. /env, /env -1, /env Misty");
            //            }
            //        }
            //    }
            //    else
            //    {
            //        PrintOut("Failed to set weather. Check parameters! Ex. /env, /env -1, /env Misty");
            //    }
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/fly"))
            //{
            //    flyEnabled = !flyEnabled;
            //    Player.m_debugMode = flyEnabled;
            //    SkUtilities.SetPrivateField(Player.m_localPlayer, "m_debugFly", flyEnabled);
            //    PrintOut("Fly toggled! (" + flyEnabled.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/alt"))
            //{
            //    altOnScreenControls = !altOnScreenControls;
            //    PrintOut("Alt controls toggled! (" + altOnScreenControls.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/stopevent"))
            //{
            //    RandEventSystem.instance.ResetRandomEvent();
            //    PrintOut("Event stopped!");
            //    return true;
            ////}

            //if (inCommandSpl[0].Equals("/revealmap"))
            //{
            //    Minimap.instance.ExploreAll();
            //    PrintOut("Map revealed!");
            //}

            //if (inCommandSpl[0].Equals("/whois"))
            //{
            //    string playerStr = string.Empty;
            //    //foreach (Player pl in Player.GetAllPlayers())
            //    foreach (ZNetPeer pl in ZNet.instance.GetConnectedPeers())
            //    {
            //        if (pl != null)
            //        {
            //            playerStr = playerStr + ", " + pl.m_playerName + "(" + pl.m_uid + ")";
            //        }
            //    }
            //    if (playerStr.Length > 2)
            //    {
            //        playerStr = playerStr.Remove(0, 2);
            //    }
            //    PrintOut("Active Players (" + Player.GetAllPlayers().Count + ") - " + playerStr, source, true);
            //}

            //if (inCommandSpl[0].Equals("/give"))
            //{
            //    inCommand = inCommand.Remove(0, 6);
            //    string[] cmdSplt = inCommand.Split(' ');
            //    string cmdPlr = Player.m_localPlayer.GetPlayerName();
            //    string cmdItem = string.Empty;
            //    int cmdQty = 1;
            //    int cmdLvl = 1;
            //    if (cmdSplt.Length == 0)
            //    {
            //        PrintOut("Failed. No item provided. /give [Item] [Qty=1] [Player] [Level=1]");
            //    }
            //    if (cmdSplt.Length >= 1) // if they provided an item
            //    {
            //        cmdItem = cmdSplt[0];
            //    }
            //    if (cmdSplt.Length == 2) // if they provided a player name
            //    {
            //        try
            //        {
            //            cmdQty = int.Parse(cmdSplt[1]);
            //        }
            //        catch (Exception)
            //        {

            //        }

            //    }
            //    if (cmdSplt.Length > 2) // if they provided a player name
            //    {
            //        try
            //        {
            //            cmdQty = int.Parse(cmdSplt[1]);
            //        }
            //        catch (Exception)
            //        {
            //            cmdQty = 1;
            //        }

            //    }
            //    if (cmdSplt.Length >= 3) // if they provided a quantity
            //    {
            //        try
            //        {
            //            cmdPlr = cmdSplt[2];

            //        }
            //        catch (Exception)
            //        {

            //        }
            //    }
            //    if (cmdSplt.Length >= 4) // if they provided a level
            //    {
            //        try
            //        {
            //            cmdLvl = int.Parse(cmdSplt[3]);
            //        }
            //        catch (Exception)
            //        {
            //            cmdLvl = 1;
            //        }
            //    }

            //    bool itemExists = false; // verify the item exists

            //    foreach (GameObject itm in ObjectDB.instance.m_items)
            //    {
            //        ItemDrop component = itm.GetComponent<ItemDrop>();
            //        if (component.name.StartsWith(cmdSplt[0]))
            //        {
            //            itemExists = true;
            //            break;
            //        }
            //    }

            //    //foreach(string Item in ConsoleOpt.ItemList)
            //    //{
            //    //    if (Item.ToLower().StartsWith(cmdSplt[0]))
            //    //    {
            //    //        itemExists = true;
            //    //        break;
            //    //    }
            //    //}

            //    if (!itemExists)
            //    {
            //        PrintOut("Failed. Item does not exist. /give [Item] [Qty=1] [Player] [Level=1]. Check for items with /listitems. Capital letters matter on this command!");
            //        return true;
            //    }

            //    Player plrObj = null;
            //    foreach (Player pl in Player.GetAllPlayers())
            //    {
            //        if (pl != null)
            //        {
            //            if (pl.GetPlayerName().ToLower().StartsWith(cmdPlr.ToLower()))
            //            {
            //                plrObj = pl;
            //                break;
            //            }
            //        }
            //    }

            //    if (plrObj == null)
            //    {
            //        PrintOut("Failed. Player does not exist. /give [Item] [Qty=1] [Player] [Level=1]");
            //        return true;
            //    }

            //    GameObject item = ZNetScene.instance.GetPrefab(cmdSplt[0]);
            //    if (item)
            //    {
            //        PrintOut("Spawning " + cmdQty + " of item " + cmdSplt[0] + "(" + cmdLvl + ") on " + cmdPlr, source, true);

            //        //for (int x = 0; x < cmdQty; x++)
            //        //{
            //        try
            //        {
            //            GameObject createdObj = UnityEngine.Object.Instantiate<GameObject>(item, plrObj.transform.position + plrObj.transform.forward * 1.5f + Vector3.up, Quaternion.identity);
            //            ItemDrop itemObj = (ItemDrop)createdObj.GetComponent(typeof(ItemDrop));
            //            if (itemObj != null && itemObj.m_itemData != null)
            //            {
            //                itemObj.m_itemData.m_quality = cmdLvl;
            //                itemObj.m_itemData.m_stack = cmdQty;
            //                itemObj.m_itemData.m_durability = itemObj.m_itemData.GetMaxDurability();
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            PrintOut("Something unexpected failed.");
            //            SkUtilities.Logz(new string[] { "ERR", "/give" }, new string[] { ex.Message, ex.Source }, LogType.Warning);
            //        }
            //        //}
            //    }
            //    else
            //    {
            //        PrintOut("Failed. Check parameters. /give [Item] [Qty=1] [Player] [Level=1]");
            //    }
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/god"))
            //{
            //    godEnabled = !godEnabled;
            //    Player.m_localPlayer.SetGodMode(godEnabled);
            //    PrintOut("God toggled! (" + godEnabled.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/clearinventory"))
            //{
            //    Player.m_localPlayer.GetInventory().RemoveAll();
            //    PrintOut("All items removed from inventory.");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/findtomb"))
            //{
            //    TombStone[] listTraders = GameObject.FindObjectsByType<TombStone>(FindObjectsSortMode.None);
            //    if (listTraders.Length > 0)
            //    {
            //        foreach (TombStone tr in listTraders)
            //        {
            //            if (tr != null && tr.enabled)
            //            {
            //                Minimap.instance.AddPin(tr.transform.position, Minimap.PinType.Ping, "TS", true, true);
            //            }
            //        }
            //    }
            //    PrintOut("Tombstone sought out! Potentially " + listTraders.Length + " found.");
            //    return true;
            //}


            //if (inCommandSpl[0].Equals("/seed"))
            //{
            //    World wrld = SkUtilities.GetPrivateField<World>(WorldGenerator.instance, "m_world");
            //    PrintOut("Map seed: " + wrld.m_seedName, source, true);
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/freecam"))
            //{
            //    GameCamera.instance.ToggleFreeFly();
            //    PrintOut("Free cam toggled " + GameCamera.InFreeFly().ToString(), source, true);
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/heal"))
            //{
            //    if (inCommandSpl.Length > 1)
            //    {
            //        foreach (Player pl in Player.GetAllPlayers())
            //        {
            //            if (pl != null)
            //            {
            //                if (pl.GetPlayerName().ToLower().Equals(inCommandSpl[1].ToLower()))
            //                {
            //                    pl.Heal(pl.GetMaxHealth());
            //                    PrintOut("Player healed: " + pl.GetPlayerName(), source, true);
            //                }
            //            }
            //        }
            //    }
            //    else
            //    {
            //        Player.m_localPlayer.Heal(Player.m_localPlayer.GetMaxHealth(), true);
            //        PrintOut("Self healed.", source, true);
            //    }

            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/nores"))
            //{
            //    SkCommandPatcher.InitPatch();
            //    SkCommandPatcher.bBuildAnywhere = !SkCommandPatcher.bBuildAnywhere;
            //    PrintOut("No build restrictions toggled! (" + SkCommandPatcher.bBuildAnywhere.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/nocost"))
            //{
            //    noCostEnabled = !noCostEnabled;
            //    Player.m_debugMode = noCostEnabled;
            //    SkUtilities.SetPrivateField(Player.m_localPlayer, "m_noPlacementCost", noCostEnabled);
            //    PrintOut("No build cost/requirements toggled! (" + noCostEnabled.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/event"))
            //{
            //    if(inCommandSpl.Length > 1)
            //    {
            //        if(RandEventSystem.instance.HaveEvent(inCommandSpl[1]))
            //        {
            //            RandEventSystem.instance.SetRandomEventByName(inCommandSpl[1], Player.m_localPlayer.transform.position);
            //            PrintOut("Event started!");
            //        } else
            //        {
            //            PrintOut("Event does not exist, please try again.");
            //        }
            //    } else
            //    {
            //        PrintOut("Please provide an event name. Ex. /event NAME");
            //    }
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/randomevent"))
            //{
            //    RandEventSystem.instance.StartRandomEvent();
            //    PrintOut("Random event started!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/tp"))
            //{
            //    if (inCommandSpl.Length != 2 || !inCommandSpl[1].Contains(","))
            //    {
            //        PrintOut("Syntax /tp X,Z");
            //        return true;
            //    }
            //    try
            //    {
            //        string[] loc = inCommandSpl[1].Split(',');
            //        float x = float.Parse(loc[0]);
            //        float z = float.Parse(loc[1]);
            //        float y = ZoneSystem.instance.GetGroundHeight(new Vector3(x, 750, z));
            //        y = Mathf.Clamp(y, 0, 100);
            //        if (y > 99)
            //        {
            //            y = Player.m_localPlayer.transform.position.y;
            //        }
            //        Player localPlayer2 = Player.m_localPlayer;
            //        if (localPlayer2)
            //        {
            //            Vector3 pos2 = new Vector3(x, y, z);
            //            localPlayer2.TeleportTo(pos2, localPlayer2.transform.rotation, false);
            //            PrintOut("Teleporting...", source, true);
            //        }
            //    }
            //    catch (Exception)
            //    {
            //        PrintOut("Syntax /tp X,Z");
            //    }

                //}
                //else
                //{
                //    List<ZDO> playerList = new List<ZDO>();
                //    if (ZNet.instance != null)
                //    {
                //        playerList = ZNet.instance.GetAllCharacterZDOS();
                //    }

                //    if (playerList == null)
                //    {
                //        PrintOut("Could not access player list for some reason?");
                //        return true;
                //    }
                //    if (playerList != null)
                //    {
                //        if (playerList.Count > 0)
                //        {
                //            foreach (var pl in playerList) // Debug
                //            {
                //                PrintOut("!!" + pl. + " - " + pl.transform.position.ToString(), source, true);
                //            }
                //            inCommand = inCommand.Remove(0, 4);
                //            string[] inCommandSplit = inCommand.Split(' ');
                //            if (inCommandSplit.Length > 0)
                //            {
                //                Player pl1 = null;
                //                Player pl2 = null;
                //                if (inCommandSplit.Length == 1)
                //                {
                //                    pl2 = Player.m_localPlayer;
                //                }

                //                foreach (var pl in playerList)
                //                {
                //                    if (pl != null)
                //                    {
                //                        if (pl.GetPlayerName().StartsWith(inCommandSplit[0]))
                //                        {
                //                            PrintOut("P1 = " + pl.GetPlayerName(), source, true);
                //                            pl1 = pl;
                //                        }
                //                        if (inCommandSplit.Length > 1)
                //                        {
                //                            if (pl.GetPlayerName().StartsWith(inCommandSplit[1]))
                //                            {
                //                                PrintOut("P2 = " + pl.GetPlayerName(), source, true);
                //                                pl2 = pl;
                //                            }
                //                        }
                //                        if (pl1 != null && pl2 != null)
                //                        {
                //                            break;
                //                        }
                //                    }
                //                }

                //                if (pl1 != null && pl2 != null)
                //                {
                //                    pl2.TeleportTo(pl1.transform.position, pl1.transform.rotation, false);
                //                    PrintOut("Teleporting...", source, true);
                //                }
                //                else
                //                {
                //                    PrintOut("Player not found. P1:'" + pl1.GetPlayerName() + "' P2:'" + pl2.GetPlayerName(), source, true);
                //                }
                //            }
                //        }
                //        else
                //        {
                //            PrintOut("No other connected players found.", source, true);
                //        }
                //    }
                //    else
                //    {
                //        PrintOut("No other connected players found.", source, true);
                //    }
                //}
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/detect"))
            //{
            //    bDetectEnemies = !bDetectEnemies;
            //    if (inCommandSpl.Length > 0)
            //    {
            //        try
            //        {
            //            bDetectRange = int.Parse(inCommandSpl[1]);
            //            bDetectRange = bDetectRange < 5 ? 5 : bDetectRange;
            //        }
            //        catch (Exception)
            //        {
            //            bDetectRange = 20;
            //        }
            //    }
            //    PrintOut("Detect enemies toggled! (" + bDetectEnemies.ToString() + ", range: " + bDetectRange + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/imacheater"))
            //{
            //    SkCommandPatcher.InitPatch();
            //    SkCommandPatcher.BCheat = !SkCommandPatcher.BCheat;

            //    if (Player.m_localPlayer != null)
            //    {
            //        try
            //        {
            //            Player.m_debugMode = SkCommandPatcher.BCheat; // public static since Valheim 1.0
            //            Terminal.m_cheat = SkCommandPatcher.BCheat; // moved to Terminal and made public static
            //        }
            //        catch (Exception)
            //        {

            //        }
            //    }

            //    PrintOut("Cheats toggled! (" + SkCommandPatcher.BCheat.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/nosup"))
            //{
            //    SkCommandPatcher.InitPatch();
            //    SkCommandPatcher.BFreeSupport = !SkCommandPatcher.BFreeSupport;
            //    PrintOut("No build support requirements toggled! (" + SkCommandPatcher.BFreeSupport.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/coords"))
            //{
            //    bCoords = !bCoords;
            //    PrintOut("Show coords toggled! (" + bCoords.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/resetmap"))
            //{
            //    Minimap.instance.Reset();
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/infstam"))
            //{
            //    infStamina = !infStamina;
            //    if (infStamina)
            //    {
            //        Player.m_localPlayer.m_staminaRegenDelay = 0.1f;
            //        Player.m_localPlayer.m_staminaRegen = 99f;
            //        Player.m_localPlayer.m_runStaminaDrain = 0f;
            //        Player.m_localPlayer.SetMaxStamina(999f, true);
            //    }
            //    else
            //    {
            //        Player.m_localPlayer.m_staminaRegenDelay = 1f;
            //        Player.m_localPlayer.m_staminaRegen = 5f;
            //        Player.m_localPlayer.m_runStaminaDrain = 10f;
            //        Player.m_localPlayer.SetMaxStamina(100f, true);
            //    }
            //    PrintOut("Infinite stamina toggled! (" + infStamina.ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/tame"))
            //{
            //    Tameable.TameAllInArea(Player.m_localPlayer.transform.position, 20f);
            //    PrintOut("Creatures tamed!");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/farinteract"))
            //{
            //    farInteract = !farInteract;
            //    if (farInteract)
            //    {
            //        if (inCommandSpl.Length > 1)
            //        {
            //            try
            //            {
            //                int value = int.Parse(inCommandSpl[1]) < 20 ? 20 : int.Parse(inCommandSpl[1]);
            //                Player.m_localPlayer.m_maxInteractDistance = value;
            //                Player.m_localPlayer.m_maxPlaceDistance = value;
            //            }
            //            catch (Exception)
            //            {
            //                PrintOut("Failed to set far interaction distance. Check params. /farinteract 50");
            //            }
            //        }
            //        else
            //        {
            //            Player.m_localPlayer.m_maxInteractDistance = 50f;
            //            Player.m_localPlayer.m_maxPlaceDistance = 50f;
            //        }
            //        PrintOut("Far interactions toggled! (" + farInteract.ToString() + " Distance: " + Player.m_localPlayer.m_maxInteractDistance + ")");
            //    }
            //    else
            //    {
            //        Player.m_localPlayer.m_maxInteractDistance = 5f;
            //        Player.m_localPlayer.m_maxPlaceDistance = 5f;
            //        PrintOut("Far interactions toggled! (" + farInteract.ToString() + ")");
            //    }

            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/ghost"))
            //{
            //    Player.m_localPlayer.SetGhostMode(!Player.m_localPlayer.InGhostMode());
            //    PrintOut("Ghost mode toggled! (" + Player.m_localPlayer.InGhostMode().ToString() + ")");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/tod"))
            //{
            //    if (inCommandSpl.Length > 1)
            //    {
            //        float num10;
            //        if (!float.TryParse(inCommandSpl[1], NumberStyles.Float, CultureInfo.InvariantCulture, out num10))
            //        {
            //            return true;
            //        }
            //        if (num10 < 0f)
            //        {
            //            EnvMan.instance.m_debugTimeOfDay = false;
            //            PrintOut("Time unlocked and under game control.");
            //        }
            //        else
            //        {
            //            EnvMan.instance.m_debugTimeOfDay = true;
            //            EnvMan.instance.m_debugTime = Mathf.Clamp01(num10);
            //            PrintOut("Setting time of day:" + num10);
            //        }
            //    }
            //    else
            //    {
            //        PrintOut("Failed. Syntax /tod [0-1] Ex. /tod 0.5");
            //    }

            //    return true;
            //}

            //if(inCommandSpl[0].Equals("/optterrain"))
            //{
            //    TerrainComp.UpgradeTerrain();
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/set"))
            //{
            //    if (inCommandSpl.Length > 1)
            //    {
            //        if (inCommandSpl[1].Equals("cw"))
            //        {
            //            try
            //            {
            //                int newWeight = int.Parse(inCommandSpl[2]);
            //                Player.m_localPlayer.m_maxCarryWeight = newWeight;
            //                PrintOut("New carry weight set to: " + newWeight);
            //            }
            //            catch (Exception)
            //            {
            //                PrintOut("Failed to set new carry weight. Check params.");
            //            }
            //            return true;
            //        }

            //        if (inCommandSpl[1].Equals("skill"))
            //        {
            //            if (inCommandSpl.Length == 4 && !inCommandSpl[2].Contains("None") && !inCommandSpl[2].Contains("All") && !inCommandSpl[2].Contains("FireMagic") && !inCommandSpl[2].Contains("FrostMagic"))
            //            {
            //                try
            //                {
            //                    string cmdSkill = inCommandSpl[2];
            //                    int cmdLvl = int.Parse(inCommandSpl[3]);
            //                    Player.m_localPlayer.GetSkills().CheatResetSkill(cmdSkill.ToLower());
            //                    Player.m_localPlayer.GetSkills().CheatRaiseSkill(cmdSkill.ToLower(), (float)cmdLvl);
            //                    return true;
            //                }
            //                catch (Exception)
            //                {
            //                    PrintOut("Failed to set skill. Check params / skill name. See /listskills. /set skill [skill] [level]");
            //                    return true;
            //                }
            //            }
            //            PrintOut("Failed to set skill. Check params / skill name. See /listskills.  /set skill [skill] [level]");
            //            return true;
            //        }

            //        if (inCommandSpl[1].Equals("pickup"))
            //        {
            //            if (inCommandSpl.Length >= 3)
            //            {
            //                try
            //                {
            //                    int cmdRange = int.Parse(inCommandSpl[2]);
            //                    Player.m_localPlayer.m_autoPickupRange = cmdRange;
            //                    PrintOut("New range set to: " + cmdRange);
            //                    return true;
            //                }
            //                catch (Exception)
            //                {
            //                    PrintOut("Failed to set pickup range. Check params. /set pickup 2");
            //                    return true;
            //                }
            //            }

            //            PrintOut("Failed to set pickup range. Check params.  /set pickup 2");
            //            return true;
            //        }

            //        if (inCommandSpl[1].Equals("jumpforce"))
            //        {
            //            if (inCommandSpl.Length >= 3)
            //            {
            //                try
            //                {
            //                    int cmdRange = int.Parse(inCommandSpl[2]);
            //                    Player.m_localPlayer.m_jumpForce = cmdRange;
            //                    PrintOut("New range set to: " + cmdRange);
            //                    return true;
            //                }
            //                catch (Exception)
            //                {
            //                    PrintOut("Failed to set jump force. Check params. /set jumpforce 10");
            //                    return true;
            //                }
            //            }

            //            PrintOut("Failed to set jump force. Check params.  /set jumpforce 10");
            //            return true;
            //        }

            //        if (inCommandSpl[1].Equals("exploreradius"))
            //        {
            //            if (inCommandSpl.Length >= 3)
            //            {
            //                try
            //                {
            //                    int cmdRange = int.Parse(inCommandSpl[2]);
            //                    Minimap.instance.m_exploreRadius = cmdRange;
            //                    PrintOut("New range set to: " + cmdRange);
            //                    return true;
            //                }
            //                catch (Exception)
            //                {
            //                    PrintOut("Failed to set explore radius. Check params. /set exploreradius 100");
            //                    return true;
            //                }
            //            }

            //            PrintOut("Failed to set explore radius. Check params.  /set exploreradius 100");
            //            return true;
            //        }

            //        if (inCommandSpl[1].Equals("speed"))
            //        {
            //            int cmdSpeed;
            //            if (inCommandSpl.Length >= 4)
            //            {
            //                string cmdType = inCommandSpl[2];
            //                String[] types = { "crouch", "run", "swim" };
            //                if (types.Contains(cmdType))
            //                {
            //                    float wasSpeed = 0f;
            //                    try
            //                    {

            //                        cmdSpeed = int.Parse(inCommandSpl[3]);
            //                        switch (cmdType)
            //                        {
            //                            case "crouch":
            //                                wasSpeed = Player.m_localPlayer.m_crouchSpeed;
            //                                Player.m_localPlayer.m_crouchSpeed = cmdSpeed;
            //                                break;
            //                            case "run":
            //                                wasSpeed = Player.m_localPlayer.m_runSpeed;
            //                                Player.m_localPlayer.m_runSpeed = cmdSpeed;
            //                                break;
            //                            case "swim":
            //                                wasSpeed = Player.m_localPlayer.m_swimSpeed;
            //                                Player.m_localPlayer.m_swimSpeed = cmdSpeed;
            //                                break;
            //                        }
            //                        PrintOut("New " + cmdType + " speed set to: " + cmdSpeed + " (was: " + wasSpeed + ")");
            //                    }
            //                    catch (Exception)
            //                    {
            //                        PrintOut("Failed to set speed. Check params name. Ex. /set speed crouch 2");
            //                        return true;
            //                    }
            //                }
            //                else
            //                {
            //                    PrintOut("Failed to set speed. Check params name. Ex.  /set speed crouch 2");
            //                }
            //                return true;
            //            }
            //            PrintOut("Failed to set speed. Check params name. Ex.  /set speed crouch 2");
            //            return true;
            //        }

            //        if (inCommandSpl[1].Equals("difficulty"))
            //        {
            //            if (inCommandSpl.Length >= 3)
            //            {
            //                try
            //                {
            //                    int diffLvl = int.Parse(inCommandSpl[2]);
            //                    Game.instance.SetForcePlayerDifficulty(diffLvl);
            //                    PrintOut("Difficulty set to " + diffLvl.ToString());
            //                    return true;
            //                }
            //                catch (Exception)
            //                {
            //                    PrintOut("Failed to set difficulty. Check params. /set difficulty 5");
            //                    return true;
            //                }
            //            }
            //            PrintOut("Failed to set difficulty. Check params.  /set difficulty 5");
            //            return true;
            //        }
            //    }

            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/removedrops"))
            //{
            //    ItemDrop[] array2 = UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None);
            //    for (int i = 0; i < array2.Length; i++)
            //    {
            //        ZNetView component = array2[i].GetComponent<ZNetView>();
            //        if (component)
            //        {
            //            component.Destroy();
            //        }
            //    }
            //    PrintOut("Items cleared.", source, true);
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/spawntamed"))
            //{
            //    string tempStr = inCommand.Replace("tamed", string.Empty);
            //    SkCommandProcessor.ProcessCommand(tempStr);
            //    Tameable.TameAllInArea(Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 1.5f, 1f);
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/spawn"))
            //{
            //    ZNetView[] ZNetObject = GameObject.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
            //    if (ZNetObject.Length == 0)
            //    {
            //        PrintOut("Couldn't find zdo...");
            //    }

            //    if (inCommandSpl.Length > 1)
            //    {
            //        GameObject creature = ZNetScene.instance.GetPrefab(inCommandSpl[1]);
            //        if (creature == null)
            //        {
            //            PrintOut("Creature not found.");
            //            return true;
            //        }

            //        Vector3 position = Player.m_localPlayer.transform.position;

            //        GameObject createdCreature = UnityEngine.Object.Instantiate<GameObject>(creature, Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 1.5f, Quaternion.identity);

            //        ZNetView component = createdCreature.GetComponent<ZNetView>();
            //        BaseAI component2 = createdCreature.GetComponent<BaseAI>();

            //        if (inCommandSpl.Length > 2) // A level was included
            //        {
            //            try
            //            {
            //                Character creatureComponent = createdCreature.GetComponent<Character>();
            //                if (creatureComponent != null)
            //                {
            //                    int lvl = int.Parse(inCommandSpl[2]);
            //                    if (lvl > 10) lvl = 10;
            //                    creatureComponent.SetLevel(lvl);
            //                }
            //                else
            //                {
            //                    ItemDrop itemObj = (ItemDrop)createdCreature.GetComponent(typeof(ItemDrop));
            //                    if (itemObj != null && itemObj.m_itemData != null)
            //                    {
            //                        itemObj.m_itemData.m_quality = 1;
            //                        itemObj.m_itemData.m_stack = int.Parse(inCommandSpl[2]);
            //                        itemObj.m_itemData.m_durability = itemObj.m_itemData.GetMaxDurability();
            //                    }
            //                }
            //            }
            //            catch (Exception)
            //            {
            //                //
            //            }
            //        }
            //        if (ZNetObject.Length > 0)
            //        {
            //            try
            //            {
            //                component.GetZDO().SetPGWVersion(ZNetObject[0].GetZDO().GetPGWVersion());
            //                ZNetObject[0].GetZDO().Set("spawn_id", component.GetZDO().m_uid);
            //                ZNetObject[0].GetZDO().Set("alive_time", ZNet.instance.GetTime().Ticks);
            //            }
            //            catch (Exception)
            //            {
            //                //
            //            }
            //            //this.SpawnEffect(createdCreature);
            //        }
            //        PrintOut("Spawned - " + inCommandSpl[1]);
            //    }

            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/killall"))
            //{
            //    List<Character> CharList = new List<Character>();
            //    Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 50f, CharList);
            //    foreach (Character character in CharList)
            //    {
            //        if (!character.IsPlayer())
            //        {
            //            HitData hitData = new HitData();
            //            hitData.m_damage.m_damage = 1E+10f;
            //            character.Damage(hitData);
            //        }
            //    }
            //    PrintOut("Nearby creatures killed! (50m)");
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/listitems"))
            //{
            //    if (inCommandSpl.Length > 1)
            //    { //starts with
            //        foreach (GameObject gameObject in ObjectDB.instance.m_items)
            //        {
            //            ItemDrop component = gameObject.GetComponent<ItemDrop>();
            //            if (component.name.ToLower().Contains(inCommandSpl[1].ToLower()))
            //            {
            //                PrintOut("Item: '" + component.name + "'", source | LogTo.DebugConsole);
            //            }
            //        }
            //    }
            //    else
            //    { // return all
            //        foreach (GameObject gameObject in ObjectDB.instance.m_items)
            //        {
            //            ItemDrop component = gameObject.GetComponent<ItemDrop>();
            //            PrintOut("Item: '" + component.name + "'", source | LogTo.DebugConsole);
            //        }
            //    }
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/listprefabs"))
            //{
            //    ConsoleOpt.BuildPrefabs();
            //    if (inCommandSpl.Length > 1)
            //    { //starts with
            //        foreach (string prefab in ConsoleOpt.PrefabList)
            //        {
            //            if (prefab.ToLower().StartsWith(inCommandSpl[1].ToLower()))
            //            {
            //                PrintOut("Prefab: '" + prefab + "'", source | LogTo.DebugConsole);
            //            }
            //        }
            //    }
            //    else
            //    { // return all
            //        foreach (string prefab in ConsoleOpt.PrefabList)
            //        {
            //            PrintOut("Prefab: '" + prefab + "'", source | LogTo.DebugConsole);
            //        }
            //    }
            //    return true;
            //}

            //if (inCommandSpl[0].Equals("/listskills"))
            //{
            //    string skillList = "Skills found: ";
            //    foreach (object obj in Enum.GetValues(typeof(Skills.SkillType)))
            //    {
            //        if (!obj.ToString().Contains("None") && !obj.ToString().Contains("All") && !obj.ToString().Contains("FireMagic") && !obj.ToString().Contains("FrostMagic"))
            //            skillList = skillList + obj.ToString() + ", ";
            //    }
            //    skillList = skillList.Remove(skillList.Length - 2);
            //    PrintOut(skillList, source | LogTo.DebugConsole);
            //    return true;
            //}
            return false;
        }

        // Valheim 1.0: shared spawn logic for /spawn and /spawntamed (no more self-recursion, no random-ZDO "spawn_id"/"alive_time" writes).
        private static GameObject SpawnPrefab(Terminal.ConsoleEventArgs args)
        {
            if (args.Length <= 1)
            {
                PrintOut("Usage: [Creature Name] [Level=1]");
                return null;
            }
            if (ZNetScene.instance == null || Player.m_localPlayer == null)
            {
                PrintOut("Not in a world.");
                return null;
            }
            GameObject creature = ZNetScene.instance.GetPrefab(args[1]);
            if (creature == null)
            {
                PrintOut("Creature not found.");
                return null;
            }

            Transform pt = Player.m_localPlayer.transform;
            GameObject createdCreature = UnityEngine.Object.Instantiate<GameObject>(creature, pt.position + pt.forward * 1.5f, Quaternion.identity);

            if (args.Length > 2) // A level was included
            {
                try
                {
                    Character creatureComponent = createdCreature.GetComponent<Character>();
                    if (creatureComponent != null)
                    {
                        int lvl = int.Parse(args[2]);
                        if (lvl > 10) lvl = 10;
                        creatureComponent.SetLevel(lvl);
                    }
                    else
                    {
                        ItemDrop itemObj = createdCreature.GetComponent<ItemDrop>();
                        if (itemObj != null && itemObj.m_itemData != null)
                        {
                            itemObj.m_itemData.m_quality = 1;
                            itemObj.m_itemData.m_stack = int.Parse(args[2]);
                            itemObj.m_itemData.m_durability = itemObj.m_itemData.GetMaxDurability();
                        }
                    }
                }
                catch (Exception)
                {
                    //
                }
            }
            return createdCreature;
        }

        public static void PrintOut(string text)
        {
            PrintOut(text, LogTo.Console, false);
        }

        // Speelo's Toolbox: command output normally goes to the F5 console, which is closed while the clickable menu is
        // up, so clicking a button looked like it did nothing. While a menu item runs, mirror the first line of its
        // output to the on-screen message area.
        internal static bool MenuFeedbackActive = false;
        internal static bool MenuFeedbackShown = false;

        /// <summary>Shows a short message in the game's top-left message area.</summary>
        internal static void Notify(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            try
            {
                int cut = text.IndexOf('\n');
                if (cut > 0)
                {
                    text = text.Substring(0, cut);
                }
                if (text.Length > 160)
                {
                    text = text.Substring(0, 160);
                }
                if (Player.m_localPlayer != null)
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text, 0, null);
                    MenuFeedbackShown = true;
                }
                else if (MessageHud.instance != null)
                {
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
                    MenuFeedbackShown = true;
                }
            }
            catch (Exception)
            {
            }
        }

        public static void PrintOut(string text, LogTo source, bool playerSay = false)
        {
            if (text.Equals(string.Empty) || text.Equals(" "))
            {
                return;
            }
            if ((source.HasFlag(LogTo.Console) || (SkConfigEntry.CAllowChatOutput != null && !SkConfigEntry.CAllowChatOutput.Value)) && Console.instance != null)
            {
                Console.instance.Print("(Speelo's Toolbox) " + text);
                //if (ConsoleOpt != null && ConsoleOpt.conWriteToFile)
                //{
                //    SkUtilities.Logz(new string[] { "DUMP", "ITEM" }, new string[] { text, });
                //}

            }
            if (source.HasFlag(LogTo.Chat) && Chat.instance != null && SkConfigEntry.CAllowChatOutput != null && SkConfigEntry.CAllowChatOutput.Value)
            {
                if (playerSay && Player.m_localPlayer != null && (SkConfigEntry.CAllowPublicChatOutput != null && SkConfigEntry.CAllowPublicChatOutput.Value))
                {
                    Player.m_localPlayer.GetComponent<Talker>().Say(Talker.Type.Normal, text);
                }
                else
                {
                    ChatPrint(text);
                }
            }
            if (source.HasFlag(LogTo.DebugConsole))
            {
                SkUtilities.Logz(new string[] { "TOOLBOX" }, new string[] { text });
            }
            // Speelo's Toolbox: echo to the screen while a menu item is running, so the click is visibly acknowledged.
            if (MenuFeedbackActive)
            {
                Notify(text);
            }
        }

        public static void ChatPrint(string ln, string source = "(Speelo's Toolbox) ")
        {
            if (Chat.instance != null)
            {
                // Valheim 1.0: OnNewChatMessage lost its separate sender-id string; fold the source tag into the text.
                Chat.instance.OnNewChatMessage(null, 999, chatPos, Talker.Type.Normal, UserInfo.GetLocalUser(), source + ln);
                SkUtilities.SetPrivateField(Chat.instance, "m_hideTimer", 0f);
                Chat.instance.m_chatWindow.gameObject.SetActive(true);
                Chat.instance.m_input.gameObject.SetActive(true);
                List<Chat.WorldTextInstance> worldTexts = SkUtilities.GetPrivateField<List<Chat.WorldTextInstance>>(Chat.instance, "m_worldTexts") as List<Chat.WorldTextInstance>;

                //Destroy in-world text
                foreach (Chat.WorldTextInstance worldTextInstance in worldTexts)
                {
                    if (worldTextInstance.m_talkerID == 999)
                    {
                        worldTextInstance.m_timer = 999f;
                        continue;

                    }
                }
            }
        }

        public static string ListPortals(bool printToChat = false)
        {
            // Valheim 1.0: portals of every prefab in Game.PortalPrefabHash (wood + Ashlands stone) live in ZDOMan.m_portalObjects;
            // GetPortalList() is what Game.ConnectPortals() itself uses. Filtering on m_portalPrefabs[0].name dropped stone portals.
            List<ZDO> m_tempPortalList = ZDOMan.instance.GetPortalList();
            m_tempPortalList.RemoveAll(z => !z.IsValid()); // parity with GetAllZDOsWithPrefabIterative's RemoveAll(InvalidZDO)

            string outStr = string.Empty;
            foreach (ZDO zdo in m_tempPortalList)
            {
                string str = zdo.GetString(ZDOVars.s_tag, "");
                if (!str.Equals(string.Empty) && !str.Equals(" "))
                {
                    outStr = outStr + "'" + str + "', ";
                }
            }
            if (!outStr.Equals(string.Empty))
            {
                outStr = outStr.Substring(0, outStr.Length - 2);
                return ("Portals found: " + outStr);
            }
            else
            {
                return ("No portals found.");
            }
        }

        internal static class TerrainModification
        {
            // Thank you to BlueAmulet for this code
            private static void CreateTerrain(GameObject prefab, Vector3 position, ZNetView component)
            {
                float levelOffset = prefab.GetComponent<TerrainModifier>().m_levelOffset;
                GameObject terrainObject = UnityEngine.Object.Instantiate(prefab, position - Vector3.up * levelOffset, Quaternion.identity);
                //terrainObject.GetComponent<ZNetView>().GetZDO().SetPGWVersion(component.GetZDO().GetPGWVersion());
            }

            //Thank you to BlueAmulet for this code
            public static void ModifyTerrain(Vector3 centerLocation, GameObject prefab, float radius)
            {
                if (radius > 30f)
                {
                    PrintOut("Radius clamped to 30 max!", LogTo.Console);
                }
                //radius = Mathf.Clamp(radius, 0f, 25f) / 2f + 0.25f;
                radius = Mathf.Clamp(radius, 0f, 30f);
                Vector3 a = centerLocation;
                ZNetView component = Player.m_localPlayer.gameObject.GetComponent<ZNetView>();
                int iSize = Mathf.CeilToInt(radius / 3) * 3;
                for (int x = -iSize; x <= iSize; x += 3)
                {
                    for (int z = -iSize; z <= iSize; z += 3)
                    {
                        Vector3 vector = new Vector3(a.x + x, a.y, a.z + z);
                        if (Utils.DistanceXZ(a, vector) <= radius)
                        {
                            CreateTerrain(prefab, vector, component);
                        }
                    }
                }

                int lx = int.MaxValue;
                int lz = int.MaxValue;
                for (int x = -iSize; x <= 0; x++)
                {
                    for (int z = -iSize; z <= -iSize / 2; z++)
                    {
                        Vector3 vector = new Vector3(a.x + x, a.y, a.z + z);
                        if (Utils.DistanceXZ(a, vector) <= radius)
                        {
                            if (x >= z && (z < lz || x > lx + 2 || x == 0))
                            {
                                lx = x;
                                lz = z;
                                if (x / 3 * 3 != x || z / 3 * 3 != z)
                                {
                                    CreateTerrain(prefab, new Vector3(a.x + x, a.y, a.z + z), component);
                                    CreateTerrain(prefab, new Vector3(a.x + x, a.y, a.z - z), component);
                                    if (x != 0)
                                    {
                                        CreateTerrain(prefab, new Vector3(a.x - x, a.y, a.z + z), component);
                                        CreateTerrain(prefab, new Vector3(a.x - x, a.y, a.z - z), component);
                                    }
                                    if (x != z)
                                    {
                                        CreateTerrain(prefab, new Vector3(a.x + z, a.y, a.z + x), component);
                                        CreateTerrain(prefab, new Vector3(a.x - z, a.y, a.z + x), component);
                                        if (x != 0)
                                        {
                                            CreateTerrain(prefab, new Vector3(a.x + z, a.y, a.z - x), component);
                                            CreateTerrain(prefab, new Vector3(a.x - z, a.y, a.z - x), component);
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }

            public static void ResetTerrain(Vector3 centerLocation, float radius)
            {
                if (radius > 50)
                {
                    PrintOut("Radius clamped to 50 max!", LogTo.Console);
                }
                radius = Mathf.Clamp(radius, 2, 50);
                Vector3 centerpos = centerLocation;
                try
                {
                    List<TerrainModifier> tList = TerrainModifier.GetAllInstances();
                    foreach (TerrainModifier terrainModifier in tList)
                    {
                        if (terrainModifier != null)
                        {
                            if (Utils.DistanceXZ(Player.m_localPlayer.transform.position, terrainModifier.transform.position) < radius)
                            {
                                ZNetView component = terrainModifier.GetComponent<ZNetView>();
                                if (component != null && component.IsValid())
                                {
                                    component.ClaimOwnership();
                                    component.Destroy();
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    //
                }
                return;
            }
        }
    }
}

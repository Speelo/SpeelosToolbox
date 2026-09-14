using BepInEx;
using SkToolbox.Configuration;
using SkToolbox.Utility;
using System;
using UnityEngine;
// Thank you to wh0am15533 for the BepInEx examples
namespace SkToolbox
{
    [BepInPlugin(GUID, MODNAME, VERSION)]
    public class SkBepInExLoader : BaseUnityPlugin
    {
        // Speelo's Toolbox: a fork of Skrip's SkToolbox for Valheim 1.0. Distinct GUID so it never
        // collides with the original (deprecated) package if both end up in a profile.
        public const string
            MODNAME = "SpeelosToolbox",
            DISPLAYNAME = "Speelo's Toolbox",
            AUTHOR = "Speelo",
            ORIGINAL_AUTHOR = "Skrip",
            GUID = "com.speelo.speelostoolbox",
            VERSION = "1.1.0";

        private void Start()
        {
            InitConfig();
            // Speelo's Toolbox: flip the achievement bypass as early as possible, before the player can spawn anything.
            SkCommandPatcher.ApplyAchievementBypass();

            base.transform.parent = null;
            UnityEngine.Object.DontDestroyOnLoad(this);
            SkLoader.InitBepThreading(this);
        }

        public void InitConfig()
        {
            try
            {
                SkConfigEntry.CDescriptor = Config.Bind("- Index", "ThisIsJustAnIndex-NotASetting", true
                    , "Config sections:" +
                    "\n0 - General" +
                    "\n1 - Auto Run" + // Valheim 1.0: AutoRun re-enabled
                    "\n2 - Customize Console Look" +
                    "\n3 - Command Aliasing" + // Valheim 1.0: aliasing is live (BuildAliases/DecomposeAlias)
                    "\n4 - On-Screen Menu" +
                    "\n5 - Command Hotkeys [Currently Disabled due to Hearth and Home patch. Fix coming soon]" +
                    "\n");

                SkConfigEntry.CConsoleEnabled = Config.Bind("0 - General", "ConsoleEnabled", true
                    , "Enables the console without launch option.");
                SkConfigEntry.CScrollable = Config.Bind("0 - General", "ConsoleScrollable", true
                    , "Enables the console to be scrollable.");
                SkConfigEntry.CScrollableLimit = Config.Bind("0 - General", "ConsoleScrollableLimit", 500
                    , "Maximum number of lines to store in the console. Game default = 30 (lol)");
                SkConfigEntry.CConsoleAutoComplete = Config.Bind("0 - General", "AutoComplete", true
                    , "Press tab to auto-complete Speelo's Toolbox commands if you have partially typed a command.");
                SkConfigEntry.CAllowChatCommandInput = Config.Bind("0 - General", "AllowChatCommandInput", true
                    , "Toggle this if you want to allow or disable the entry of commands in the chat. If this is disabled, you can only input commands into the console.");
                SkConfigEntry.CAllowPublicChatOutput = Config.Bind("0 - General", "AllowPublicResponse", true
                    , "Toggle this to allow the mod to respond publicly with certain commands, when entered into chat." +
                    "\nThe /portal command for example, if used in chat and this is true, others nearby will be able to see the response." +
                    "\nNOTE: If you see a response from your name, it is shown publicly and everyone can see it. If it is a response from (Speelo's Toolbox), only you see it.");
                SkConfigEntry.CAllowChatOutput = Config.Bind("0 - General", "AllowResponseInChat", true
                    , "Toggle this to allow the mod to show output in the chat. If this is disabled, the mod will not output to the chat at all, publicly or not.");
                SkConfigEntry.CAllowExecuteOnClear = Config.Bind("0 - General", "AllowExecuteOnClear", false
                    , "Toggle this to enable the ability to execute commands by clearing the input (by hitting escape, or selecting all and removing).");
                SkConfigEntry.CKeepAchievements = Config.Bind("0 - General", "KeepAchievements", true
                    , "Keep Steam achievements working while cheats are used." +
                    "\nValheim disables achievement progress as soon as anything counts as cheated, and simply loading BepInEx already counts (it sets Game.isModded)." +
                    "\nThis flips the game's own PlayerProfile.s_bypassCheatChecks switch, which every cheat check reads, so progress keeps counting." +
                    "\nIt also stops cheat commands from permanently flagging the character, and clears that flag if it was already set." +
                    "\nSet to false to let Valheim disable achievements normally.");
                SkConfigEntry.CPersistCheatsOnDeath = Config.Bind("0 - General", "PersistCheatsOnDeath", true
                    , "Put your cheats back on after you die." +
                    "\nDying destroys your character object and the game builds a new one from the prefab, so every cheat" +
                    " that is stored on the character is silently lost: god mode, flying, no cost building, infinite" +
                    " stamina and far interact all switch themselves off on the respawn." +
                    "\nWith this on, whatever you had switched on is switched back on once you respawn." +
                    "\nSet to false to let each death clear them, which is what Valheim does on its own.");
                SkConfigEntry.CGodModeBlocksDamage = Config.Bind("0 - General", "GodModeBlocksDamage", true
                    , "Make god mode block damage outright." +
                    "\nValheim's own god mode does not stop damage: it subtracts health as normal and only rescues you at" +
                    " the moment the hit would kill you, clamping health to 1. So your health bar still drains." +
                    "\nWith this on, any damage aimed at you while god mode is active is discarded before it lands," +
                    " including fire, poison and smoke." +
                    "\nSet to false for Valheim's unmodified behaviour.");
                SkConfigEntry.COpenConsoleWithSlash = Config.Bind("0 - General", "OpenConsoleWithSlash", false
                    , "Toggle this to enable the ability to open the console with the slash (/) button." +
                    "\nThis option takes precedence over OpenChatWithSlash if both are true.");
                SkConfigEntry.COpenChatWithSlash = Config.Bind("0 - General", "OpenChatWithSlash", false
                    , "Toggle this to enable the ability to open chat with the slash (/) button.");

                SkConfigEntry.CAutoRun = Config.Bind("1 - AutoRun", "AutoRunEnabled", false
                    , "Toggle this to run commands automatically upon spawn into server." +
                    "\nNote this will only occur once per game launch to prevent unintended command executions.");
                SkConfigEntry.CAutoRunCommand = Config.Bind("1 - AutoRun", "AutoRunCommand", "/nosup; /god"
                    , "Enter the commands to run upon spawn here. Seperate commands with a semicolon (;).");

                SkConfigEntry.OMenuToggleKey = Config.Bind("4 - OnScreenMenu", "MenuToggleKey", "F6"
                    , "Key that opens and closes the clickable menu. Valid key names: https://docs.unity3d.com/ScriptReference/KeyCode.html" +
                    "\nAlready taken: F11 and F12 take screenshots (Valheim and Steam), F5 opens the game console, F4 opens this mod's log console," +
                    " F2 opens the connect panel, F9 cycles the controller layout, Ctrl+F1 toggles mouse capture, Ctrl+F3 hides the HUD." +
                    "\nFree function keys: F6, F7, F8, F10.");
                SkConfigEntry.OMenuOpacity = Config.Bind("4 - OnScreenMenu", "MenuOpacity", 0.96f
                    , "How solid the menu background is. 1 = fully opaque, 0.5 = half see-through. Takes effect immediately.");


                SkConfigEntry.CAllowLookCustomizations = Config.Bind("2 - CustomizeConsoleLook", "ConsoleAllowLookCustomizations", true
                    , "Toggle this to enable or disable the customization settings below.");

                SkConfigEntry.CConsoleFont = Config.Bind("2 - CustomizeConsoleLook", "ConsoleFont", "Consola.ttl"
                    , "Set the font size of the text in the console. Game default = AveriaSansLibre-Bold");
                SkConfigEntry.CConsoleFontSize = Config.Bind("2 - CustomizeConsoleLook", "ConsoleFontSize", 18
                    , "Set the font size of the text in the console. Game default = 18");

                SkConfigEntry.CConsoleOutputTextColor = Config.Bind("2 - CustomizeConsoleLook", "ConsoleOutputTextColor", "#E6F7FFFF"
                    , "Set the color of the output text shown in the console. Game default = #FFFFFFFF. Color format is #RRGGBBAA");
                SkConfigEntry.CConsoleInputTextColor = Config.Bind("2 - CustomizeConsoleLook", "ConsoleInputTextColor", "#E6F7FFFF"
                    , "Set the color of the input text shown in the console. Game default = #FFFFFFFF. Color format is #RRGGBBAA");
                SkConfigEntry.CConsoleSelectionColor = Config.Bind("2 - CustomizeConsoleLook", "ConsoleSelectionColor", "#A8CEFFC0"
                    , "Set the color of the selection highlight in the console. Game default = #A8CEFFC0. Color format is #RRGGBBAA");
                SkConfigEntry.CConsoleCaretColor = Config.Bind("2 - CustomizeConsoleLook", "ConsoleCaretColor", "#DCE6F5FF"
                    , "Set the color of the input text caret shown in the console. Game default = #FFFFFFFF. Color format is #RRGGBBAA");

                SkConfigEntry.CAlias1 = Config.Bind("3 - CommandAliasing", "Alias1", "/creative: /god; /ghost; /imacheater; /nores; /nocost; /echo Creative mode toggled!"
                    , "Set this to create a command alias. Specify what the user will type, and what will be executed. " +
                    "Command chaining does work here, and these aliases can be used with the AutoRun functionality above." +
                    " Note that aliases cannot reference other aliases. The slash (/) prefix is also not required - command aliases can be set to anything you want!" +
                    " Aliases do work from chat as well, if the execute from chat setting is enabled above. Alias commands also appear in the /? help menus." +
                    "\nFormat: WhatToType: WhatToExecute" +
                    "\nExample: This will create a new command, '/Cmd1', and when entered it will execute /god and /fly" +
                    "\n/Cmd1: /god; /fly" +
                    "\n##################" +
                    "\nSet this to create command alias 1.");
                SkConfigEntry.CAlias2 = Config.Bind("3 - CommandAliasing", "Alias2", ""
                    , "Set this to create command alias 2.");
                SkConfigEntry.CAlias3 = Config.Bind("3 - CommandAliasing", "Alias3", ""
                    , "Set this to create command alias 3.");
                SkConfigEntry.CAlias4 = Config.Bind("3 - CommandAliasing", "Alias4", ""
                    , "Set this to create command alias 4.");
                SkConfigEntry.CAlias5 = Config.Bind("3 - CommandAliasing", "Alias5", ""
                    , "Set this to create command alias 5.");
                SkConfigEntry.CAlias6 = Config.Bind("3 - CommandAliasing", "Alias6", ""
                    , "Set this to create command alias 6.");
                SkConfigEntry.CAlias7 = Config.Bind("3 - CommandAliasing", "Alias7", ""
                    , "Set this to create command alias 7.");
                SkConfigEntry.CAlias8 = Config.Bind("3 - CommandAliasing", "Alias8", ""
                    , "Set this to create command alias 8.");
                SkConfigEntry.CAlias9 = Config.Bind("3 - CommandAliasing", "Alias9", ""
                    , "Set this to create command alias 9.");
                SkConfigEntry.CAlias10 = Config.Bind("3 - CommandAliasing", "Alias10", ""
                    , "Set this to create command alias 10.");
                SkConfigEntry.CAlias11 = Config.Bind("3 - CommandAliasing", "Alias11", ""
                    , "Set this to create command alias 11.");
                SkConfigEntry.CAlias12 = Config.Bind("3 - CommandAliasing", "Alias12", ""
                    , "Set this to create command alias 12.");
                SkConfigEntry.CAlias13 = Config.Bind("3 - CommandAliasing", "Alias13", ""
                    , "Set this to create command alias 13.");
                SkConfigEntry.CAlias14 = Config.Bind("3 - CommandAliasing", "Alias14", ""
                    , "Set this to create command alias 14.");
                SkConfigEntry.CAlias15 = Config.Bind("3 - CommandAliasing", "Alias15", ""
                    , "Set this to create command alias 15.");

                SkConfigEntry.CHotkey1 = Config.Bind("5 - CommandHotkeys", "Hotkey 1", ""
                    , "Set this to create a hotkey for a command or command chain." +
                    "\nValid Format is WhatToPress: WhatToExecute" +
                    "\nExample 1 Press z to input /fly; /god        | z: /fly; /god" +
                    "\nExample 2 Press Shift+Z to input /creative   | Z: /creative" +
                    "\nExample 3 Press Tilde to open the console    | `: /console" +
                    "\nOnly 'Printable ASCII characters' are valid! (https://theasciicode.com.ar/). Capital letters make the hotkey require Shift before the hotkey press." +
                    " Only one command or command chain can be assigned to each key (if hotkey 1 assigns to q, hotkey 2 cannot also assign to q).");
                SkConfigEntry.CHotkey2 = Config.Bind("5 - CommandHotkeys", "Hotkey 2", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey3 = Config.Bind("5 - CommandHotkeys", "Hotkey 3", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey4 = Config.Bind("5 - CommandHotkeys", "Hotkey 4", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey5 = Config.Bind("5 - CommandHotkeys", "Hotkey 5", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey6 = Config.Bind("5 - CommandHotkeys", "Hotkey 6", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey7 = Config.Bind("5 - CommandHotkeys", "Hotkey 7", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey8 = Config.Bind("5 - CommandHotkeys", "Hotkey 8", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey9 = Config.Bind("5 - CommandHotkeys", "Hotkey 9", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey10 = Config.Bind("5 - CommandHotkeys", "Hotkey 10", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey11 = Config.Bind("5 - CommandHotkeys", "Hotkey 11", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey12 = Config.Bind("5 - CommandHotkeys", "Hotkey 12", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey13 = Config.Bind("5 - CommandHotkeys", "Hotkey 13", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey14 = Config.Bind("5 - CommandHotkeys", "Hotkey 14", ""
                    , "Set this to create a hotkey for a command or command chain.");
                SkConfigEntry.CHotkey15 = Config.Bind("5 - CommandHotkeys", "Hotkey 15", ""
                    , "Set this to create a hotkey for a command or command chain.");

            }
            catch (Exception Ex)
            {
                SkUtilities.Logz(new string[] { "ERR" }, new string[] { "Could not load config. Please confirm there is a working version of BepInEx installed.",
                                                                            Ex.Message, Ex.Source}, LogType.Error);
            }
        }
    }
}
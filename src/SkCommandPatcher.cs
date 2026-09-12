using HarmonyLib;
using SkToolbox.Utility;
using System;
using UnityEngine;

namespace SkToolbox
{
    internal static class SkCommandPatcher
    {
        private static Harmony harmony = null;

        private static bool initComplete = false;

        // Speelo's Toolbox: set by SkMenuController while it polls its own keys, so the menu's toggle key survives the
        // input block installed by PatchMenuKeyDown/PatchMenuButtonDown below.
        internal static bool BypassInputBlock = false;

        private static bool bCheat = false;
        private static bool bFreeSupport = false;
        public static bool bBuildAnywhere = false;

        public static Harmony Harmony { get => harmony; set => harmony = value; }
        public static bool BCheat { get => bCheat; set => bCheat = value; }
        public static bool BFreeSupport { get => bFreeSupport; set => bFreeSupport = value; }
        public static bool InitComplete { get => initComplete; set => initComplete = value; }

        public static void InitPatch()
        {
            
            if (!InitComplete)
            {
                //SkUtilities.Logz(new string[] { "SkCommandPatcher", "INJECT" }, new string[] { "Attempting injection..." });
                try
                {
                    //Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
                    harmony = Harmony.CreateAndPatchAll(typeof(SkCommandPatcher).Assembly);
                    //SkUtilities.Logz(new string[] { "SkCommandPatcher", "INJECT" }, new string[] { "INJECT => COMPLETE" });
                }
                catch (Exception ex)
                //catch (Exception)
                {
                    SkCommandProcessor.PrintOut("Something failed, there is a strong possibility another mod blocked this operation.", SkCommandProcessor.LogTo.Console);
                    SkUtilities.Logz(new string[] { "SkCommandPatcher", "PATCH" }, new string[] { "PATCH => FAILED. CHECK FOR OTHER MODS BLOCKING PATCHES.\n", ex.Message, ex.StackTrace }, UnityEngine.LogType.Error);
                }
                finally
                {
                    InitComplete = true;
                }
            }
        }

        [HarmonyPatch(typeof(Console), "IsConsoleEnabled")]
        public static class PatchIsConsoleEnabled
        {
            // Upstream declared __result without ref, so the assignment never reached the game.
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref bool __result)
            {
                if (Configuration.SkConfigEntry.CConsoleEnabled != null && Configuration.SkConfigEntry.CConsoleEnabled.Value)
                {
                    __result = true;
                }
            }
        }

        // Valheim 1.0: /imacheater must lift Terminal.IsCheatsEnabled() (m_cheat && ZNet.instance.IsServer()) and the
        // ConsoleCommand OnlyServer gate, otherwise it is a no-op on a joined server.
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.IsCheatsEnabled))]
        private static class PatchIsCheatsEnabled
        {
            // Terminal.IsCheatsEnabled() (1.0.7) is m_cheat && ZNet.instance.IsServer(); on a joined server it is always false.
            // Console and Chat inherit this non-virtual method, so patching Terminal covers Console.instance.IsCheatsEnabled() too.
            private static void Postfix(ref bool __result)
            {
                if (SkCommandPatcher.BCheat) __result = true;
            }
        }

        [HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.IsValid))]
        private static class PatchConsoleCommandIsValid
        {
            // protected virtual; Chat overrides it to refuse cheat commands typed in the chat box - keep that.
            private static readonly System.Reflection.MethodInfo s_isAllowedCommand =
                AccessTools.Method(typeof(Terminal), "isAllowedCommand");

            private static void Postfix(Terminal.ConsoleCommand __instance, Terminal context, bool skipAllowedCheck, ref bool __result)
            {
                if (__result || !SkCommandPatcher.BCheat || context == null) return;

                // Only lift the OnlyServer gate (vanilla: ZNet.instance.IsServer()). If the command failed for any other
                // reason (isAllowedCommand, IsNetwork without ZNet) leave it invalid exactly as vanilla does.
                if (!__instance.OnlyServer) return;

                // Failable-ctor admin commands (kick/ban/...) set OnlyServer = onlyServer | onlyAdmin - keep them server-side.
                if (__instance.OnlyAdmin) return;

                // RemoteCommand commands are forwarded to the server by TryRunCommand and admin-checked there (ZNet.RPC_RemoteCommand).
                // Keep that, except 'confirmcheats': it only flags the local PlayerProfile, and Terminal.ConsoleCommand.RunAction
                // refuses every other IsCheat command until Achievements.IsCheatedAtAll() is true.
                if (__instance.RemoteCommand && !string.Equals(__instance.Command, "confirmcheats", StringComparison.OrdinalIgnoreCase)) return;

                if (__instance.IsNetwork && ZNet.instance == null) return;

                if (!skipAllowedCheck && s_isAllowedCommand != null
                    && !(bool)s_isAllowedCommand.Invoke(context, new object[] { __instance })) return;

                __result = true;
            }
        }

        // Valheim 1.0: Chat.InputText strips the leading '/' and runs the remainder in the Chat context with silentFail=true,
        // which prints "$achievements_confirm_cheat" for every vanilla cheat name that collides with a SkToolbox command
        // (god, fly, ghost, heal, env, tod, wind, resetwind, spawn, tame, killall, removedrops, resetmap, nocost) and runs
        // vanilla `clear` for "/clear". Only SkToolbox registers keys beginning with '/', so skip vanilla for those lines.
        // Terminal.SendInput still clears m_input.text afterwards, so ModConsole.HandleChat -> ProcessCommands runs the
        // command exactly once as before.
        [HarmonyPatch(typeof(Chat), "InputText")]
        private static class PatchChatInputText
        {
            // Terminal.commands is `protected static Dictionary<string, ConsoleCommand>` (Terminal.cs:285).
            // NOTE: HarmonyX 2.9's AccessTools.StaticFieldRefAccess<F>(Type, string) returns `ref F`, not FieldRef<F>,
            // so use a plain FieldInfo (null-safe: AccessTools.Field logs and returns null instead of throwing).
            private static readonly System.Reflection.FieldInfo commandsField = AccessTools.Field(typeof(Terminal), "commands");

            private static bool Prefix(Chat __instance)
            {
                // Only intercept when SkToolbox's chat command handling is enabled; otherwise HandleChat never runs the
                // line and swallowing it here would turn every slash command into a silent no-op.
                if (SkToolbox.Configuration.SkConfigEntry.CAllowChatCommandInput != null
                    && !SkToolbox.Configuration.SkConfigEntry.CAllowChatCommandInput.Value)
                {
                    return true;
                }

                string text = __instance.m_input.text;
                if (!string.IsNullOrEmpty(text) && text.Length > 1 && text[0] == '/'
                    && commandsField?.GetValue(null) is System.Collections.Generic.Dictionary<string, Terminal.ConsoleCommand> commands
                    && commands.ContainsKey(text.Split(' ')[0].ToLower()))
                {
                    return false; // SkToolbox slash command: skip vanilla's slash-stripped Chat-context run
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(WearNTear), "UpdateSupport")]
        private static class PatchUpdateSupport
        {
            private static bool Prefix(ref float ___m_support, ref ZNetView ___m_nview)
            {
                if (SkCommandPatcher.BFreeSupport)
                {
                    // Valheim 1.0: doubling m_support was a no-op at 0 (already-unsupported pieces still collapsed) and overflowed to Infinity.
                    // UpdateSupport is only reached from UpdateWear inside m_nview.IsOwner(); force full support
                    // so HaveSupport() (m_support >= GetMinSupport()) stays true and the 100-damage collapse never fires.
                    if (___m_support != float.MaxValue)
                    {
                        ___m_support = float.MaxValue;
                        ___m_nview.GetZDO().Set(ZDOVars.s_support, ___m_support);
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static class PatchUpdatePlacementGhost
        {
            private static void Postfix(bool flashGuardStone)
            {
                if (SkCommandPatcher.bBuildAnywhere)
                {
                    try
                    {
                        if (Player.m_localPlayer != null)
                        {
                            // Valheim 1.0: m_placementStatus is the Player.PlacementStatus enum, not an int.
                            // Keep ward protection (PrivateZone) and override every other rejection reason.
                            var status = SkUtilities.GetPrivateField<Player.PlacementStatus>(Player.m_localPlayer, "m_placementStatus");
                            // Valheim 1.0: skip NoRayHits (ghost inactive, stale position) and clear the red invalid highlight the original already applied.
                            if (status != Player.PlacementStatus.Valid && status != Player.PlacementStatus.PrivateZone && status != Player.PlacementStatus.NoRayHits)
                            {
                                // The game leaves the ghost inactive (and never positions it) when it bails out early:
                                // NoRayHits (Player.cs:4052-4057) and m_groundPiece && heightmap == null (Player.cs:3902-3907).
                                // Placing then would use the ghost's stale previous position, so only override when the ghost is active.
                                var ghost = SkUtilities.GetPrivateField<UnityEngine.GameObject>(Player.m_localPlayer, "m_placementGhost");
                                if (ghost != null && ghost.activeSelf)
                                {
                                    SkUtilities.SetPrivateField(Player.m_localPlayer, "m_placementStatus", Player.PlacementStatus.Valid);
                                    // The original method already ran SetPlacementGhostValid(false) (Player.cs:4112-4113); clear the red highlight.
                                    ghost.GetComponent<Piece>()?.SetInvalidPlacementHeightlight(false);
                                }
                            }
                        }

                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Location), "IsInsideNoBuildLocation")]
        private static class PatchIsInsideNoBuildLocation
        {
            private static void Postfix(ref bool __result)
            {
                if (SkCommandPatcher.bBuildAnywhere)
                {
                    __result = false;
                }
            }
        }

        // Speelo's Toolbox: while the clickable menu is open, free the mouse cursor. GameCamera.UpdateMouseCapture runs every
        // frame and re-locks the cursor unless one of the vanilla GUIs is open, so a postfix is the reliable place to override it.
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
        private static class PatchMenuMouseCapture
        {
            private static void Postfix()
            {
                if (SkMenuController.IsOpen)
                {
                    ZCursor.LockState = UnityEngine.CursorLockMode.None;
                    ZCursor.Show();
                }
            }
        }

        // Speelo's Toolbox: Player.TakeInput gates hovering, "Use", building placement and similar. It does NOT gate
        // camera look or attacks - those live on PlayerController, patched below.
        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static class PatchMenuTakeInput
        {
            private static void Postfix(ref bool __result)
            {
                if (SkMenuController.IsOpen)
                {
                    __result = false;
                }
            }
        }

        // Speelo's Toolbox: THE fix for "camera still rotates" and "clicking punches".
        // PlayerController has its own private TakeInput(bool look), separate from Player.TakeInput:
        //   FixedUpdate():  if (!TakeInput()) { m_character.SetControls(Vector3.zero, attack: false, ...); return; }
        //   LateUpdate():   if (!TakeInput(look: true) || InInventoryEtc()) { m_character.SetMouseLook(Vector2.zero); return; }
        // Forcing it false while the menu is open stops mouse look, movement, jump, block and attacks in one place,
        // exactly the way the game stops them for its own menus.
        [HarmonyPatch(typeof(PlayerController), "TakeInput", new Type[] { typeof(bool) })]
        private static class PatchMenuPlayerControllerInput
        {
            private static void Postfix(ref bool __result)
            {
                if (SkMenuController.IsOpen)
                {
                    __result = false;
                }
            }
        }

        // Speelo's Toolbox: GameCamera.UpdateCamera reads the scroll wheel through its own gate list (which cannot know
        // about this mod), so scrolling the menu list would also zoom the camera. IMGUI scrolling uses Event.current,
        // not ZInput, so zeroing this only affects the game.
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
        private static class PatchMenuScrollWheel
        {
            private static void Postfix(ref float __result)
            {
                if (SkMenuController.IsOpen)
                {
                    __result = 0f;
                }
            }
        }

        // Speelo's Toolbox: while the menu owns the screen, swallow keyboard/gamepad "pressed this frame" input so typing in
        // the search box cannot swap hotbar slots (HotkeyBar), open the inventory (InventoryGui), open chat (Chat) or
        // open the vanilla pause menu (Menu) - none of those consult PlayerController.TakeInput.
        // SkMenuController sets BypassInputBlock while polling its own toggle key, so the menu can always be closed.
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetKeyDown), new Type[] { typeof(KeyCode), typeof(bool) })]
        private static class PatchMenuKeyDown
        {
            private static void Postfix(ref bool __result)
            {
                if (__result && SkMenuController.IsOpen && !BypassInputBlock)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown), new Type[] { typeof(string) })]
        private static class PatchMenuButtonDown
        {
            private static void Postfix(ref bool __result)
            {
                if (__result && SkMenuController.IsOpen && !BypassInputBlock)
                {
                    __result = false;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Speelo's Toolbox: keep achievements working while cheating.
        //
        // Achievements.CanGetAchievements(cheated) is the single gate every achievement stat goes through:
        //     if (cheated || IsCheatedAtAll()) return PlayerProfile.s_bypassCheatChecks;
        //     return true;
        // IsCheatedAtAll() is true if the character used a cheat command (PlayerProfile.m_usedCheats, saved), the
        // inventory holds a cheated item, the world has cheated global keys, OR Game.isModded - and BepInEx sets
        // Game.isModded itself, so achievements are already dead the moment the game loads modded.
        //
        // s_bypassCheatChecks is the game's own escape hatch: a public static that every cheat check reads and that
        // nothing in the game ever sets. Turning it on both restores achievements and stops newly spawned items,
        // pieces and world objects from being stamped as cheated in the first place.
        // ---------------------------------------------------------------------------------------------------------

        internal static bool KeepAchievementsEnabled =>
            Configuration.SkConfigEntry.CKeepAchievements == null || Configuration.SkConfigEntry.CKeepAchievements.Value;

        private static bool clearedUsedCheats = false;

        /// <summary>Applies the achievement bypass. Safe to call repeatedly and before the game has loaded a profile.</summary>
        internal static void ApplyAchievementBypass()
        {
            if (!KeepAchievementsEnabled)
            {
                return;
            }
            try
            {
                // Valheim build 25253764 (2026-09-11) turned PlayerProfile.s_bypassCheatChecks from a writable
                // public field into a getter-only property, so it can no longer be assigned. The getter is patched
                // instead, in PatchBypassCheatChecks below, which has the same effect everywhere the game reads it.

                // m_usedCheats is saved in the character file and is the only permanent, character-scoped source of
                // "cheated". Clear it once per session so the character is not left flagged when playing unmodded.
                if (!clearedUsedCheats && Game.instance != null)
                {
                    PlayerProfile profile = Game.instance.GetPlayerProfile();
                    if (profile != null)
                    {
                        clearedUsedCheats = true;
                        if (profile.m_usedCheats)
                        {
                            profile.m_usedCheats = false;
                            SkUtilities.Logz(new string[] { "ACHIEVEMENTS" }, new string[] { "Cleared the permanent 'used cheats' flag on this character (KeepAchievements is on)." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SkUtilities.Logz(new string[] { "ACHIEVEMENTS", "ERROR" }, new string[] { ex.Message }, UnityEngine.LogType.Error);
            }
        }

        // Valheim build 25253764 made s_bypassCheatChecks a read-only property. Every cheat check in the game
        // reads it, so patching the getter restores what assigning the old field used to do: spawned items, pieces
        // and world objects stop being stamped as cheated.
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.s_bypassCheatChecks), MethodType.Getter)]
        private static class PatchBypassCheatChecks
        {
            private static void Postfix(ref bool __result)
            {
                if (KeepAchievementsEnabled)
                {
                    __result = true;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Speelo's Toolbox: make god mode actually block damage.
        //
        // Valheim's god mode does not grant immunity. Character.ApplyDamage subtracts the damage first and only
        // then checks it:
        //     health -= totalDamage2;
        //     if (health <= 0f && (InGodMode() || InGhostMode())) health = 1f;
        // So you cannot die, but every hit still drains your health bar, which is not what "god mode" implies.
        //
        // ApplyDamage is the common path for every damage source: ordinary hits (Character.Damage), attack recoil,
        // and the burning, poison and smoke status effects. Skipping it for the local player while god mode is on
        // discards the hit before any health is lost.
        // ---------------------------------------------------------------------------------------------------------
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        private static class PatchGodModeBlocksDamage
        {
            private static bool Prefix(Character __instance)
            {
                if (Configuration.SkConfigEntry.CGodModeBlocksDamage != null
                    && !Configuration.SkConfigEntry.CGodModeBlocksDamage.Value)
                {
                    return true; // player asked for Valheim's unmodified behaviour
                }
                Player local = Player.m_localPlayer;
                if (local != null && (object)__instance == (object)local && local.InGodMode())
                {
                    return false; // swallow the hit entirely
                }
                return true;
            }
        }

        // Belt and braces: force the gate open even if something else resets the static at runtime.
        [HarmonyPatch(typeof(Achievements), nameof(Achievements.CanGetAchievements), new Type[] { typeof(bool) })]
        private static class PatchCanGetAchievements
        {
            private static void Postfix(ref bool __result)
            {
                if (!KeepAchievementsEnabled)
                {
                    return;
                }
                ApplyAchievementBypass();
                __result = true;
            }
        }

        // s_bypassCheatChecks does NOT stop Terminal.ConsoleCommand.RunAction from setting m_usedCheats after any
        // command whose IsCheat flag is set, so restore whatever the value was before the command ran.
        [HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.RunAction))]
        private static class PatchRunActionKeepAchievements
        {
            private static void Prefix(out bool __state)
            {
                __state = false;
                try
                {
                    if (Game.instance != null)
                    {
                        PlayerProfile profile = Game.instance.GetPlayerProfile();
                        __state = profile != null && profile.m_usedCheats;
                    }
                }
                catch (Exception)
                {
                }
            }

            private static void Postfix(bool __state)
            {
                if (!KeepAchievementsEnabled || __state)
                {
                    return; // disabled, or the character was already flagged and ApplyAchievementBypass will clear it
                }
                try
                {
                    if (Game.instance != null)
                    {
                        PlayerProfile profile = Game.instance.GetPlayerProfile();
                        if (profile != null)
                        {
                            profile.m_usedCheats = false;
                        }
                    }
                }
                catch (Exception)
                {
                }
                ApplyAchievementBypass();
            }
        }
    }
}

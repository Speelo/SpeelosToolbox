using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkToolbox.Utility;
using UnityEngine;
using SkScope = SkToolbox.SkMenuController.SkScope;

namespace SkToolbox.SkModules
{
    internal class ModPlayer : SkBaseModule, IModule
    {
        //private Rect EnemyWindow;
        bool bTeleport = false;

        //GameObject gParent;
        //GameObject gObject;
        //bool gPickPut = false;

        Rect rectClock =    new Rect(05, 005, 125, 20);
        Rect rectCoords =   new Rect(05, 027, 125, 20);
        Rect rectEnemy =    new Rect(05, 280, 425, 50);

        List<Character> nearbyCharacters = new List<Character>();

        public ModPlayer() : base()
        {
            base.ModuleName = "Player";
            base.Loading();
        }

        public void Start()
        {
            BeginMenu();
            base.Ready(); // Must be called when the module has completed initialization. // End of Start
        }

        /// <summary>This tab renders an icon grid rather than a list, so the normal item list stays empty.</summary>
        public void BeginMenu()
        {
            MenuOptions = new SkMenu();
        }

        /// <summary>Entry point for the tab.</summary>
        internal void ShowGrid()
        {
            // Valheim 1.0: resync from the live Player. Per-instance flags reset on respawn or relog, and the
            // vanilla hotkeys flip them behind our back.
            Player lp = Player.m_localPlayer;
            if (lp != null)
            {
                SkCommandProcessor.godEnabled = lp.InGodMode();      // Player.cs:4470
                SkCommandProcessor.flyEnabled = lp.IsDebugFlying();  // Player.cs:4507 (owner path -> m_debugFly)
                SkCommandProcessor.noCostEnabled = lp.NoCostCheat(); // Player.cs:6177
            }
            SkMC.RequestGridMenu(BuildGrid(), (List<SkMenuController.SkMenuSlider>)null, "Player", showFilter: false);
        }

        /// <summary>
        /// The Player tab as icon cells. Names live in the footer tooltip; toggles carry a live predicate so the
        /// controller can outline them while they are on, without rebuilding the grid after every click.
        /// </summary>
        private List<SkMenuController.SkGridItem> BuildGrid()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>();

            AddFoodCell(grid, "Best Health Food", FoodKind.Health, null);
            AddFoodCell(grid, "Best Stamina Food", FoodKind.Stamina, null);
            AddFoodCell(grid, "Best Eitr Food", FoodKind.Eitr, null);
            AddFoodCell(grid, "Balanced Food", FoodKind.Balanced, SkIcons.First("Tankard", "BarleyWine"));
            Action(grid, "Character", "Heal Self", "Restore health and stamina", SkIcons.First("MeadHealthMedium", "MeadHealthMinor", "Honey"), Heal);
            Action(grid, "Character", "Repair All", "Repair every item you carry", SkIcons.First("Hammer"), RepairAll);
            Action(grid, "Character", "Skills", "Set or reset a skill level", SkIcons.First("Wishbone", "Coins"), ShowSkillForm);
            Action(grid, "Character", "Puke", "Empty your stomach, clearing all three food slots", SkIcons.First("Entrails", "RawMeat"),
                   () => SkRun.CmdNotify("puke", "Stomach emptied."));
            Action(grid, "Character", "Inventory Size", "Change how many rows your inventory has", SkIcons.First("ArmorLeatherChest", "ArmorRagsChest"),
                   ShowInventoryForm);
            Action(grid, "Character", "Stats", "Show what the game has recorded for this character", SkIcons.First("Ruby", "Coins"),
                   () => SkRun.Show("Player Stats", "stats", "No stats to show yet."));
            Action(grid, "Character", "Tweaks", "Carry weight, pickup range, jump, speeds and map reveal radius",
                   SkIcons.First("Bronze", "Copper"), ShowTuningForm);
            Action(grid, "Character", "Clear Inventory", "Throw away everything you are carrying",
                   SkIcons.First("Flint", "Stone"), ShowClearInventoryForm);

            Toggle(grid, "Cheats", "Godmode", "Take no damage", SkIcons.First("HelmetOdin", "CapeOdin"),
                   ToggleGodmode, () => Player.m_localPlayer != null && Player.m_localPlayer.InGodMode());
            Toggle(grid, "Cheats", "Flying", "Free flight", SkIcons.First("Feathers"),
                   ToggleFlying, () => Player.m_localPlayer != null && Player.m_localPlayer.IsDebugFlying());
            Toggle(grid, "Cheats", "Infinite Stamina", "No running or carry-weight drain, and stamina refills almost instantly", SkIcons.First("MeadStaminaMedium", "MeadStaminaMinor"),
                   ToggleInfStam, () => Player.m_localPlayer != null && SkCommandProcessor.infStamina);
            Toggle(grid, "Cheats", "No Cost Building", "Unlock all pieces and build for free", SkIcons.First("Wood", "Stone"),
                   ToggleNoCost, () => Player.m_localPlayer != null && Player.m_localPlayer.NoCostCheat());
            Toggle(grid, "Cheats", "Build Anywhere", "Remove build restrictions", SkIcons.First("Cultivator", "Hoe"),
                   ToggleAnywhere, () => SkCommandPatcher.bBuildAnywhere);
            Toggle(grid, "Cheats", "No Support Needed", "Pieces float without support. Turning this off lets unsupported builds collapse",
                   SkIcons.First("FineWood", "RoundLog", "Wood"), ToggleNoSupport, () => SkCommandPatcher.BFreeSupport);
            Toggle(grid, "Cheats", "Ghost", "Creatures cannot see you", SkIcons.First("CapeLinen", "CapeDeerHide", "Feathers"),
                   ToggleGhost, () => Player.m_localPlayer != null && Player.m_localPlayer.InGhostMode());
            Action(grid, "Cheats", "Reach", "Interact with and place things from far away",
                   SkIcons.First("Chain", "LeatherScraps"), ShowReachForm);
            Action(grid, "Cheats", "Tame", "Tame all nearby creatures", SkIcons.First("Carrot", "Raspberry"), Tame, SkScope.Server);
            Action(grid, "Cheats", "Status Effects", "Apply a status effect, or clear the ones you have",
                   SkIcons.First("MeadFrostResist", "MeadPoisonResist", "MeadHealthMinor"), ShowStatusForm);

            Toggle(grid, "Info", "Teleport to Mouse", "Press tilde (~) to teleport where you look", SkIcons.First("SurtlingCore", "Thunderstone"),
                   ToggleTeleport, () => bTeleport);
            Action(grid, "Info", "Portals", "List every portal tag", SkIcons.First("SurtlingCore"),
                   () => SkCommandProcessor.ProcessCommand("/portals", SkCommandProcessor.LogTo.Chat));
            Toggle(grid, "Info", "Display Coordinates", "Show coords in the top left corner", SkIcons.First("FishingRodFloat", "Thunderstone", "Ruby"),
                   ToggleCoords, () => SkCommandProcessor.bCoords);
            Toggle(grid, "Info", "Detect Nearby Enemies", "Range: 20m", SkIcons.First("Wishbone"),
                   ToggleESPEnemies, () => SkCommandProcessor.bDetectEnemies);
            Action(grid, "Info", "Position", "Print your coordinates, zone and distance from the centre", SkIcons.First("Amber", "Coins"),
                   () => SkRun.Show("Position", "pos", "No position yet."));
            Action(grid, "Info", "Find Tombstone", "Pin your nearby graves on the map", SkIcons.First("AmberPearl", "Ruby"),
                   () => SkCommandProcessor.ProcessCommand("/findtomb", SkCommandProcessor.LogTo.Chat));
            Action(grid, "Info", "Bookmarks", "Save where you are standing, and teleport back to it later",
                   SkIcons.First("SurtlingCore", "Thunderstone"), ShowBookmarkForm);
            Action(grid, "Info", "Loadouts", "Snapshot your whole inventory, and restore it later",
                   SkIcons.First("ArmorBronzeChest", "ArmorLeatherChest", "ArmorRagsChest"), ShowLoadoutForm);

            return grid;
        }

        private static void Action(List<SkMenuController.SkGridItem> grid, string section, string label, string tip, Sprite icon,
                                   System.Action run, SkScope scope = SkScope.Client)
        {
            grid.Add(new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                Section = section,
                Scope = scope,
                OnClick = (string ignored) => run(),
            });
        }

        private static void Toggle(List<SkMenuController.SkGridItem> grid, string section, string label, string tip, Sprite icon,
                                   System.Action run, Func<bool> isOn, SkScope scope = SkScope.Client)
        {
            grid.Add(new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                Section = section,
                Scope = scope,
                OnClick = (string ignored) => run(),
                IsOn = isOn,
            });
        }

        // ------------------------------------------------------------------ skills

        /// <summary>
        /// Skill picker. Levels are shown next to each name so the current state is visible before changing it.
        /// The game's CheatRaiseSkill adds to a skill and clamps to 0-100, so setting an exact level means raising
        /// by the difference, which also accepts a negative value.
        /// </summary>
        private void ShowSkillForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            SkMenuController.SkFormField picker = new SkMenuController.SkFormField
            {
                Id = "skill",
                Label = "Skill",
                Kind = SkMenuController.SkFieldKind.Choice,
            };
            picker.Options.Add("All");
            picker.OptionLabels.Add("All skills");

            Skills skills = player.GetSkills();
            foreach (Skills.SkillType type in Enum.GetValues(typeof(Skills.SkillType)))
            {
                if (type == Skills.SkillType.None || type == Skills.SkillType.All) continue;
                float level = 0f;
                try { level = skills != null ? skills.GetSkillLevel(type) : 0f; } catch (Exception) { }
                picker.Options.Add(type.ToString());
                picker.OptionLabels.Add(type.ToString() + "     <color=#9FB6CC>level " + Mathf.RoundToInt(level) + "</color>");
            }
            picker.Selected = 1; // first real skill rather than "All"

            SkMenuController.SkFormField amount = new SkMenuController.SkFormField
            {
                Id = "level",
                Label = "Level to set",
                Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = 0,
                Max = 100,
                IntValue = 50,
            };

            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Skills",
                Note = "Pick a skill, choose a level, then Set. Reset puts it back to zero.",
            };
            form.Fields.Add(picker);
            form.Fields.Add(amount);

            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Set level",
                Run = (SkMenuController.SkForm f) => SetSkill(f.Field("skill").SelectedOption, f.Field("level").IntValue),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Reset to zero",
                Run = (SkMenuController.SkForm f) => ResetSkill(f.Field("skill").SelectedOption),
            });

            SkMC.ShowForm(form);
        }

        private static void SetSkill(string name, int target)
        {
            Player player = Player.m_localPlayer;
            if (player == null || string.IsNullOrEmpty(name)) return;
            Skills skills = player.GetSkills();
            if (skills == null) return;

            if (name == "All")
            {
                foreach (Skills.SkillType type in Enum.GetValues(typeof(Skills.SkillType)))
                {
                    if (type == Skills.SkillType.None || type == Skills.SkillType.All) continue;
                    skills.CheatRaiseSkill(type.ToString(), target - skills.GetSkillLevel(type), false);
                }
                SkCommandProcessor.Notify("All skills set to " + target);
                return;
            }

            Skills.SkillType chosen;
            try { chosen = (Skills.SkillType)Enum.Parse(typeof(Skills.SkillType), name, true); }
            catch (Exception) { SkCommandProcessor.Notify("Unknown skill: " + name); return; }

            skills.CheatRaiseSkill(name, target - skills.GetSkillLevel(chosen), false);
            SkCommandProcessor.Notify(name + " set to " + target);
        }

        private static void ResetSkill(string name)
        {
            Player player = Player.m_localPlayer;
            if (player == null || string.IsNullOrEmpty(name)) return;
            Skills skills = player.GetSkills();
            if (skills == null) return;
            skills.CheatResetSkill(name == "All" ? "all" : name);
            SkCommandProcessor.Notify((name == "All" ? "All skills" : name) + " reset to zero");
        }

        // ------------------------------------------------------------------ bookmarks and loadouts

        /// <summary>Named places in this world. Coordinates from another seed are meaningless, so they are hidden.</summary>
        private void ShowBookmarkForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            List<SkBookmarks.Bookmark> saved = SkBookmarks.ForThisWorld();
            List<string> names = new List<string>();
            List<string> labels = new List<string>();
            foreach (SkBookmarks.Bookmark mark in saved)
            {
                names.Add(mark.Name);
                labels.Add(mark.Name + "     <color=#9FB6CC>" + Mathf.RoundToInt(mark.Pos.x) + ", " + Mathf.RoundToInt(mark.Pos.z) + "</color>");
            }

            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Bookmarks",
                Note = saved.Count > 0
                    ? "Places you saved in this world."
                    : "Nothing saved in this world yet. Press Save here to name your current spot.",
            };
            if (names.Count > 0)
            {
                form.Fields.Add(new SkMenuController.SkFormField
                {
                    Id = "mark", Label = "Saved places", Kind = SkMenuController.SkFieldKind.Choice, Height = 180,
                });
                form.Field("mark").Options.AddRange(names);
                form.Field("mark").OptionLabels.AddRange(labels);
            }
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Save here",
                // Asks for the name in a prompt over this form, rather than a text box sitting in it that has
                // nothing to do with the list above it.
                Run = (SkMenuController.SkForm f) => SkMC.ShowPrompt(new SkMenuController.SkPrompt
                {
                    Title = "Save bookmark",
                    Label = "A name for where you are standing.",
                    Accept = "Save",
                    OnAccept = (string name) =>
                    {
                        Player lp = Player.m_localPlayer;
                        if (lp == null) { SkCommandProcessor.Notify("No player yet."); return; }
                        if (name.Length == 0) { SkCommandProcessor.Notify("Give the bookmark a name first."); return; }
                        SkCommandProcessor.Notify(SkBookmarks.Save(name, lp.transform.position)
                            ? "Saved bookmark: " + name
                            : "Load into a world first.");
                        ShowBookmarkForm(); // rebuild so the new one is in the list
                    },
                }),
            });
            if (names.Count > 0)
            {
                form.Actions.Add(new SkMenuController.SkFormAction
                {
                    Label = "Teleport",
                    Run = (SkMenuController.SkForm f) =>
                    {
                        Player lp = Player.m_localPlayer;
                        string pick = f.Field("mark").SelectedOption;
                        if (lp == null || string.IsNullOrEmpty(pick)) return;
                        foreach (SkBookmarks.Bookmark mark in SkBookmarks.ForThisWorld())
                        {
                            if (!string.Equals(mark.Name, pick, StringComparison.OrdinalIgnoreCase)) continue;
                            lp.TeleportTo(mark.Pos, lp.transform.rotation, true);
                            SkCommandProcessor.Notify("Teleporting to " + mark.Name);
                            return;
                        }
                    },
                });
                form.Actions.Add(new SkMenuController.SkFormAction
                {
                    Label = "Delete",
                    Run = (SkMenuController.SkForm f) =>
                    {
                        string pick = f.Field("mark").SelectedOption;
                        if (string.IsNullOrEmpty(pick)) return;
                        SkBookmarks.Delete(pick);
                        SkCommandProcessor.Notify("Deleted bookmark: " + pick);
                    },
                });
            }
            SkMC.ShowForm(form);
        }

        /// <summary>Equipment sets. Restoring only re-equips what you are actually carrying.</summary>
        private void ShowLoadoutForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            List<string> names = SkLoadouts.Names();
            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Loadouts",
                Note = "Saves your whole inventory, not just what you are wearing: every stack, its size, quality, "
                     + "durability and crafter, and which pieces were equipped. Loading one puts all of it back.",
            };
            if (names.Count > 0)
            {
                form.Warning = "Loading a set REPLACES everything you are carrying.";
                SkMenuController.SkFormField picker = new SkMenuController.SkFormField
                {
                    Id = "set", Label = "Saved sets", Kind = SkMenuController.SkFieldKind.Choice, Height = 150,
                };
                foreach (string entry in names)
                {
                    picker.Options.Add(entry);
                    int count = SkLoadouts.CountOf(entry);
                    picker.OptionLabels.Add(count >= 0
                        ? entry + "     <color=#9FB6CC>" + count + " stacks</color>"
                        : entry);
                }
                form.Fields.Add(picker);
                form.Fields.Add(new SkMenuController.SkFormField
                {
                    Id = "confirm",
                    Label = "Replace what I am carrying",
                    Kind = SkMenuController.SkFieldKind.Toggle,
                });
            }
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Save worn",
                Run = (SkMenuController.SkForm f) => SkMC.ShowPrompt(new SkMenuController.SkPrompt
                {
                    Title = "Save loadout",
                    Label = "A name for your inventory as it is now.",
                    Accept = "Save",
                    OnAccept = (string name) =>
                    {
                        Player lp = Player.m_localPlayer;
                        if (lp == null) { SkCommandProcessor.Notify("No player yet."); return; }
                        if (name.Length == 0) { SkCommandProcessor.Notify("Give the set a name first."); return; }
                        int count = SkLoadouts.Save(name, lp);
                        SkCommandProcessor.Notify(count > 0
                            ? "Saved " + count + " stacks as: " + name
                            : "Your inventory is empty, nothing to save.");
                        ShowLoadoutForm();
                    },
                }),
            });
            if (names.Count > 0)
            {
                // Only the load is gated: saving and deleting cannot lose you anything you are holding.
                form.Validate = (SkMenuController.SkForm f) =>
                {
                    SkMenuController.SkFormField box = f.Field("confirm");
                    return (box != null && !box.BoolValue) ? "Switch the confirmation to ON to load a set." : null;
                };
                form.Actions.Add(new SkMenuController.SkFormAction
                {
                    Label = "Load set",
                    Run = (SkMenuController.SkForm f) =>
                    {
                        Player lp = Player.m_localPlayer;
                        string pick = f.Field("set").SelectedOption;
                        if (lp == null || string.IsNullOrEmpty(pick)) return;
                        int stacks, equipped;
                        if (!SkLoadouts.Restore(pick, lp, out stacks, out equipped))
                        {
                            SkCommandProcessor.Notify("Could not read that set.");
                            return;
                        }
                        SkCommandProcessor.Notify("Restored " + stacks + " stacks, " + equipped + " equipped.");
                    },
                });
                form.Actions.Add(new SkMenuController.SkFormAction
                {
                    Label = "Delete",
                    Run = (SkMenuController.SkForm f) =>
                    {
                        string pick = f.Field("set").SelectedOption;
                        if (string.IsNullOrEmpty(pick)) return;
                        SkLoadouts.Delete(pick);
                        SkCommandProcessor.Notify("Deleted set: " + pick);
                    },
                });
            }
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ tuning, reach, inventory

        /// <summary>
        /// The knobs that used to be reachable only through /set. They are written straight onto the player rather
        /// than run as commands, which means ReapplyOnSpawn can restore them; through /set they quietly reverted on
        /// every death.
        /// </summary>
        private void ShowTuningForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }
            SkCommandProcessor.CaptureTuningBaseline(player);

            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Tweaks",
                Note = "Each slider starts where the game has it now. These are restored after you die, unless PersistCheatsOnDeath is off.",
            };
            form.Fields.Add(Slider("carry", "Carry weight", 50, 2000,
                                   SkCommandProcessor.carryWeight > 0 ? SkCommandProcessor.carryWeight : Mathf.RoundToInt(player.m_maxCarryWeight)));
            // The pickup sweep uses a fixed 100-entry buffer, so a huge radius quietly collects less, not more.
            form.Fields.Add(Slider("pickup", "Auto pickup range", 2, 20,
                                   SkCommandProcessor.pickupRange > 0 ? SkCommandProcessor.pickupRange : Mathf.RoundToInt(player.m_autoPickupRange)));
            form.Fields.Add(Slider("jump", "Jump force", 4, 40,
                                   SkCommandProcessor.jumpForce > 0 ? SkCommandProcessor.jumpForce : Mathf.RoundToInt(player.m_jumpForce)));
            form.Fields.Add(Slider("run", "Run speed", 2, 40,
                                   SkCommandProcessor.runSpeed > 0 ? SkCommandProcessor.runSpeed : Mathf.RoundToInt(player.m_runSpeed)));
            form.Fields.Add(Slider("swim", "Swim speed", 1, 20,
                                   SkCommandProcessor.swimSpeed > 0 ? SkCommandProcessor.swimSpeed : Mathf.RoundToInt(player.m_swimSpeed)));
            form.Fields.Add(Slider("explore", "Map reveal radius", 50, 500,
                                   SkCommandProcessor.exploreRadius > 0 ? SkCommandProcessor.exploreRadius
                                   : (Minimap.instance != null ? Mathf.RoundToInt(Minimap.instance.m_exploreRadius) : 100)));

            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) =>
                {
                    Player lp = Player.m_localPlayer;
                    if (lp == null) { SkCommandProcessor.Notify("No player yet."); return; }
                    SkCommandProcessor.carryWeight = f.Field("carry").IntValue;
                    SkCommandProcessor.pickupRange = f.Field("pickup").IntValue;
                    SkCommandProcessor.jumpForce = f.Field("jump").IntValue;
                    SkCommandProcessor.runSpeed = f.Field("run").IntValue;
                    SkCommandProcessor.swimSpeed = f.Field("swim").IntValue;
                    SkCommandProcessor.exploreRadius = f.Field("explore").IntValue;
                    SkCommandProcessor.ApplyTuning(lp);
                    SkCommandProcessor.Notify("Tweaks applied.");
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Defaults",
                Run = (SkMenuController.SkForm f) =>
                {
                    SkCommandProcessor.ResetTuning(Player.m_localPlayer);
                    SkCommandProcessor.Notify("Tweaks back to normal.");
                },
            });
            SkMC.ShowForm(form);
        }

        /// <summary>Interaction reach. A slider rather than a toggle, since the distance is the point.</summary>
        private void ShowReachForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Reach",
                Note = "How far away you can interact with things and place pieces. Normal is about 5.",
            };
            form.Fields.Add(Slider("reach", "Reach", 20, 100, Mathf.RoundToInt(SkCommandProcessor.farInteractDistance)));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) =>
                {
                    Player lp = Player.m_localPlayer;
                    if (lp == null) { SkCommandProcessor.Notify("No player yet."); return; }
                    SkCommandProcessor.farInteractDistance = f.Field("reach").IntValue;
                    SkCommandProcessor.farInteract = true;
                    SkCommandProcessor.ApplyFarInteract(lp, applyOffValues: true);
                    SkCommandProcessor.Notify("Reach set to " + f.Field("reach").IntValue);
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Back to normal",
                Run = (SkMenuController.SkForm f) =>
                {
                    Player lp = Player.m_localPlayer;
                    if (lp == null) { SkCommandProcessor.Notify("No player yet."); return; }
                    SkCommandProcessor.farInteract = false;
                    SkCommandProcessor.ApplyFarInteract(lp, applyOffValues: true);
                    SkCommandProcessor.Notify("Reach back to normal.");
                },
            });
            SkMC.ShowForm(form);
        }

        /// <summary>Emptying your pockets has no undo, so it asks first rather than being a bare cell.</summary>
        private void ShowClearInventoryForm()
        {
            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Clear Inventory",
                Warning = "This DESTROYS every item you are carrying.",
                Note = "Equipped gear included, and everything in any extra slots you have. Nothing is dropped on the ground, "
                     + "no tombstone is left, and there is no way to get any of it back. Put anything you want to keep in a chest first.",
            };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "confirm",
                Label = "I understand everything will be destroyed",
                Kind = SkMenuController.SkFieldKind.Toggle,
            });
            form.Validate = (SkMenuController.SkForm f) =>
            {
                SkMenuController.SkFormField box = f.Field("confirm");
                return (box != null && !box.BoolValue) ? "Switch the confirmation to ON first." : null;
            };
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Destroy everything",
                Run = (SkMenuController.SkForm f) =>
                    SkCommandProcessor.ProcessCommand("/clearinventory", SkCommandProcessor.LogTo.Chat),
            });
            SkMC.ShowForm(form);
        }

        private static SkMenuController.SkFormField Slider(string id, string label, int min, int max, int value)
        {
            return new SkMenuController.SkFormField
            {
                Id = id,
                Label = label,
                Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = min,
                Max = max,
                IntValue = Mathf.Clamp(value, min, max),
            };
        }

        public void ToggleNoSupport()
        {
            SkCommandProcessor.ProcessCommand("/nosup", SkCommandProcessor.LogTo.Chat);
        }

        public void ToggleGhost()
        {
            SkCommandProcessor.ProcessCommand("/ghost", SkCommandProcessor.LogTo.Chat);
        }

        // ------------------------------------------------------------------ inventory and status

        /// <summary>Inventory rows. The game clamps this to 9 and drops anything that no longer fits.</summary>
        private void ShowInventoryForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            int rows = 4;
            try { rows = player.GetInventory().GetHeight(); } catch (Exception) { }

            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Inventory Size",
                Note = "Rows in your inventory. Four is the normal size; shrinking it drops whatever no longer fits.",
            };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "rows",
                Label = "Rows",
                Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = 1,
                Max = 9,
                IntValue = Mathf.Clamp(rows, 1, 9),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) =>
                    SkRun.CmdNotify("inventorysize " + f.Field("rows").IntValue, "Inventory set to " + f.Field("rows").IntValue + " rows."),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Back to 4",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("inventorysize 4", "Inventory back to 4 rows."),
            });
            SkMC.ShowForm(form);
        }

        /// <summary>Every status effect the game ships, by prefab name, which is what addstatus expects.</summary>
        private void ShowStatusForm()
        {
            ObjectDB db = ObjectDB.instance;
            List<string> names = new List<string>();
            if (db != null && db.m_StatusEffects != null)
            {
                foreach (StatusEffect effect in db.m_StatusEffects)
                {
                    if (effect != null && !string.IsNullOrEmpty(effect.name) && !names.Contains(effect.name))
                    {
                        names.Add(effect.name);
                    }
                }
            }
            if (names.Count == 0)
            {
                SkCommandProcessor.Notify("Status effect list is not available yet. Load into a world first.");
                return;
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);

            SkMenuController.SkFormField picker = new SkMenuController.SkFormField
            {
                Id = "status",
                Label = "Status effect",
                Kind = SkMenuController.SkFieldKind.Choice,
                Height = 220,
            };
            picker.Options.AddRange(names);

            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Status Effects",
                Note = "Adds one effect at its normal duration. Clearing removes everything you currently have, food included.",
            };
            form.Fields.Add(picker);
            // Both buttons do the work directly rather than through addstatus / clearstatus. addstatus is registered
            // with the failable constructor and onlyAdmin, which folds into OnlyServer, so running it as a client on
            // someone else's server is refused - and status effects are purely local, so there is nothing to forward.
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Add effect",
                Run = (SkMenuController.SkForm f) =>
                {
                    string name = f.Field("status").SelectedOption;
                    if (string.IsNullOrEmpty(name)) return;
                    Player target = Player.m_localPlayer;
                    if (target == null)
                    {
                        SkCommandProcessor.Notify("No player yet.");
                        return;
                    }
                    target.GetSEMan().AddStatusEffect(name.GetStableHashCode(), true, 0, 0f, -1);
                    SkCommandProcessor.Notify("Added " + name);
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Clear all",
                Run = (SkMenuController.SkForm f) =>
                {
                    Player target = Player.m_localPlayer;
                    if (target == null)
                    {
                        SkCommandProcessor.Notify("No player yet.");
                        return;
                    }
                    target.ClearHardDeath();
                    target.GetSEMan().RemoveAllStatusEffects();
                    SkCommandProcessor.Notify("Cleared status effects.");
                },
            });
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ food

        private enum FoodKind { Health, Stamina, Eitr, Balanced }

        private static List<ItemDrop> foodCache;
        private static int foodCacheFor = 0;

        /// <summary>Every consumable in the game that grants health, stamina or eitr. Cached per ObjectDB.</summary>
        private static List<ItemDrop> AllFoods()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null)
            {
                return new List<ItemDrop>();
            }
            int key = db.GetInstanceID();
            if (foodCache != null && foodCacheFor == key)
            {
                return foodCache;
            }

            List<ItemDrop> found = new List<ItemDrop>();
            foreach (GameObject prefab in db.m_items)
            {
                if (prefab == null) continue;
                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
                if (shared.m_food <= 0f && shared.m_foodStamina <= 0f && shared.m_foodEitr <= 0f) continue;
                if (shared.m_foodBurnTime <= 0f) continue; // not actually edible as a buff
                // The prefab asset does not carry m_dropPrefab; EatFood dereferences it, so record it now.
                drop.m_itemData.m_dropPrefab = prefab;
                found.Add(drop);
            }
            foodCache = found;
            foodCacheFor = key;
            return foodCache;
        }

        private static float Stat(ItemDrop drop, FoodKind kind)
        {
            ItemDrop.ItemData.SharedData s = drop.m_itemData.m_shared;
            switch (kind)
            {
                case FoodKind.Health: return s.m_food;
                case FoodKind.Stamina: return s.m_foodStamina;
                case FoodKind.Eitr: return s.m_foodEitr;
                default: return s.m_food + s.m_foodStamina + s.m_foodEitr;
            }
        }

        /// <summary>Top food for one stat, ignoring anything already chosen.</summary>
        private static ItemDrop Best(List<ItemDrop> pool, FoodKind kind, List<ItemDrop> taken)
        {
            ItemDrop best = null;
            float bestValue = 0f;
            foreach (ItemDrop candidate in pool)
            {
                if (Stat(candidate, kind) <= 0f) continue;
                bool already = false;
                foreach (ItemDrop chosen in taken)
                {
                    if (chosen.m_itemData.m_shared.m_name == candidate.m_itemData.m_shared.m_name) { already = true; break; }
                }
                if (already) continue;
                float value = Stat(candidate, kind);
                if (best == null || value > bestValue)
                {
                    best = candidate;
                    bestValue = value;
                }
            }
            return best;
        }

        /// <summary>The three foods to eat for a given goal. Balanced takes the best of each stat.</summary>
        private static List<ItemDrop> PickFoods(FoodKind kind)
        {
            List<ItemDrop> pool = AllFoods();
            List<ItemDrop> picked = new List<ItemDrop>();
            if (pool.Count == 0) return picked;

            if (kind == FoodKind.Balanced)
            {
                foreach (FoodKind part in new FoodKind[] { FoodKind.Health, FoodKind.Stamina, FoodKind.Eitr })
                {
                    ItemDrop choice = Best(pool, part, picked);
                    if (choice != null) picked.Add(choice);
                }
            }
            else
            {
                while (picked.Count < 3)
                {
                    ItemDrop choice = Best(pool, kind, picked);
                    if (choice == null) break;
                    picked.Add(choice);
                }
            }
            return picked;
        }

        private static string FoodName(ItemDrop drop)
        {
            try
            {
                if (Localization.instance != null)
                {
                    string localized = Localization.instance.Localize(drop.m_itemData.m_shared.m_name);
                    if (!string.IsNullOrEmpty(localized)) return localized;
                }
            }
            catch (Exception)
            {
            }
            return drop.name;
        }

        /// <summary>
        /// Replaces all three food slots. ClearFood empties them first, so each EatFood lands in a free slot
        /// instead of being refused by the "already eaten / slots full" checks.
        /// </summary>
        private static void Feed(FoodKind kind)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            List<ItemDrop> picks = PickFoods(kind);
            if (picks.Count == 0)
            {
                SkCommandProcessor.Notify("No food found for that. Load into a world first.");
                return;
            }

            player.ClearFood();
            List<string> eaten = new List<string>();
            foreach (ItemDrop drop in picks)
            {
                ItemDrop.ItemData data = drop.m_itemData.Clone();
                data.m_dropPrefab = drop.m_itemData.m_dropPrefab;
                data.m_stack = 1;
                if (player.EatFood(data))
                {
                    eaten.Add(FoodName(drop));
                }
            }

            SkCommandProcessor.Notify(eaten.Count > 0 ? "Ate: " + string.Join(", ", eaten.ToArray()) : "Could not eat anything.");
        }

        private static Sprite IconOf(ItemDrop drop)
        {
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return null;
            Sprite[] icons = drop.m_itemData.m_shared.m_icons;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        /// <summary>One food cell. The icon is whatever it will eat first, unless an override is given.</summary>
        private void AddFoodCell(List<SkMenuController.SkGridItem> grid, string label, FoodKind kind, Sprite iconOverride)
        {
            List<ItemDrop> picks = PickFoods(kind);
            string tip = picks.Count == 0
                ? label + "  -  fills all three food slots"
                : label + "  -  " + string.Join(", ", picks.ConvertAll(FoodName).ToArray());
            grid.Add(new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = tip,
                Icon = iconOverride != null ? iconOverride : (picks.Count > 0 ? IconOf(picks[0]) : null),
                Section = "Character",
                Scope = SkScope.Client,
                OnClick = (string ignored) => Feed(kind),
            });
        }

        public void ToggleTeleport()
        {
            bTeleport = !bTeleport;
            if (bTeleport)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Teleport enabled. Press tilde (~)!", 0, null);
            }
            else
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Teleport disabled.", 0, null);
            }
            BeginMenu();
        }

        public void Heal()
        {
            SkCommandProcessor.ProcessCommand("/heal", SkCommandProcessor.LogTo.Chat);
        }
        public void Tame()
        {
            SkCommandProcessor.ProcessCommand("/tame", SkCommandProcessor.LogTo.Chat);
        }

        public void ToggleNoCost()
        {
            SkCommandProcessor.ProcessCommand("/nocost", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void ToggleESPEnemies()
        {
            SkCommandProcessor.ProcessCommand("/detect 20", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void ToggleCoords()
        {
            SkCommandProcessor.ProcessCommand("/coords", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void ToggleAnywhere()
        {
            SkCommandProcessor.ProcessCommand("/nores", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void ToggleGodmode()
        {
            SkCommandProcessor.ProcessCommand("/god", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void ToggleFlying()
        {
            SkCommandProcessor.ProcessCommand("/fly", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void ToggleInfStam()
        {
            SkCommandProcessor.ProcessCommand("/infstam", SkCommandProcessor.LogTo.Chat);
            BeginMenu();
        }

        public void RepairAll()
        {
            SkCommandProcessor.ProcessCommand("/repair", SkCommandProcessor.LogTo.Chat);
        }

        void Update()
        {
            if (bTeleport)
            {
                if (Input.GetKeyDown(KeyCode.BackQuote))
                {
                    Ray rayCast = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    if (Physics.Raycast(rayCast, out hit))
                    {
                        Vector3 targetLoc = hit.point;
                        Debug.DrawRay(Player.m_localPlayer.transform.position, targetLoc, Color.white);
                        Player.m_localPlayer.transform.position = targetLoc;
                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Warp!", 0, null);
                    }
                }
            }
            //if (Input.GetKeyDown(KeyCode.KeypadMinus))
            //{
            //    Ray rayCast = Camera.main.ScreenPointToRay(Input.mousePosition);
            //    RaycastHit hit;
            //    if (Physics.Raycast(rayCast, out hit))
            //    {
            //        Destroy(hit.collider.gameObject);
            //        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Removed!", 0, null);
            //    }
            //}
            //if (Input.GetKeyDown(KeyCode.KeypadDivide))
            //{
            //    Ray rayCast = Camera.main.ScreenPointToRay(Input.mousePosition);
            //    RaycastHit hit;
            //    if (Physics.Raycast(rayCast, out hit))
            //    {
            //        CommandProcessor.PrintOut("GO Name: " + hit.collider.gameObject.name, false);
            //    }
            //}

            //if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            //{

            //    Ray rayCast = Camera.main.ScreenPointToRay(Input.mousePosition);
            //    RaycastHit hit;
            //    if (Physics.Raycast(rayCast, out hit))
            //    {
            //        CommandProcessor.PrintOut("GO Name: " + hit.collider.gameObject.name, false);
            //        if (!gPickPut)
            //        {
            //            gPickPut = true;
            //            CommandProcessor.PrintOut("PICK" + hit.collider.gameObject.name, false);
            //            gObject = hit.collider.gameObject;
            //            gParent = gObject.transform.parent.gameObject;
            //            gObject.transform.parent = Player.m_localPlayer.gameObject.transform;
            //        } else
            //        {
            //            gPickPut = false;
            //            CommandProcessor.PrintOut("PUT" + gObject.name, false);
            //            gObject.transform.parent = gParent.transform;
            //            gParent = null;
            //        }
                    
            //    }
            //}

            //if (bDetectEnemies)
            //{
            //    List<Character> charList = Character.GetAllCharacters();
            //    if (charList.Count > 0)
            //    {
            //        foreach (Character character in charList)
            //        {
            //            if (character != null && !character.IsDead() && !character.IsPlayer())
            //            {
            //                if (Vector3.Distance(character.transform.position, Player.m_localPlayer.transform.position) < 20f)
            //                {
            //                    if (!nearbyCharacters.Contains(character))
            //                    {
            //                        nearbyCharacters.Add(character);
            //                    }
            //                }
            //                else
            //                {
            //                    if (nearbyCharacters.Contains(character))
            //                    {
            //                        nearbyCharacters.Remove(character);
            //                    }
            //                }
            //            }
            //            else
            //            {
            //                if (nearbyCharacters.Contains(character))
            //                {
            //                    nearbyCharacters.Remove(character);
            //                }
            //            }
            //        }

            //        if (nearbyCharacters.Count > 0)
            //        {
            //            List<Character> tempCharList = new List<Character>(nearbyCharacters);
            //            foreach (Character character in tempCharList)
            //            {
            //                if (!charList.Contains(character))
            //                {
            //                    nearbyCharacters.Remove(character);
            //                }
            //            }
            //        }
            //    }
            //    if (nearbyCharacters.Count > 0 && btDetectEmeiesSwitch)
            //    {
            //        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Enemy nearby!", 0, null);
            //        btDetectEmeiesSwitch = false;
            //    }
            //    else if (nearbyCharacters.Count == 0)
            //    {
            //        btDetectEmeiesSwitch = true;
            //    }
            //}
        }

        void OnGUI()
        {
            //if (Player.m_localPlayer != null)
            //{
            //    if (bDetectEnemies && nearbyCharacters.Count > 0)
            //    {
            //        EnemyWindow = GUILayout.Window(39999, rectEnemy, ProcessEnemies, "Enemy Information");
            //    }
            //    if (bClock)
            //    {
            //        GUI.Label(rectClock, "Time (0-1): " + EnvMan.instance.m_debugTime);
            //    }
            //    if (bCoords)
            //    {
            //        Vector3 plPos = Player.m_localPlayer.transform.position;
            //        GUI.Label(rectCoords, "Coords: " + Mathf.RoundToInt(plPos.x) + "/" + Mathf.RoundToInt(plPos.y));
            //    }
            //}
        }

        //void ProcessEnemies(int WindowID)
        //{
        //    GUILayout.BeginVertical();
        //    if (nearbyCharacters?.Count > 0)
        //    {
        //        Vector3 playerPos = Player.m_localPlayer.transform.position;

        //        foreach (Character toon in nearbyCharacters)
        //        {
        //            float toonDist = Vector3.Distance(playerPos, toon.transform.position);

        //            if(toonDist > 15)
        //            {
        //                GUI.color = Color.green;
        //            }
        //            else if (toonDist > 10 && toonDist < 15)
        //            {
        //                GUI.color = Color.yellow;
        //            }
        //            else if (toonDist > 5 && toonDist < 10)
        //            {
        //                GUI.color = Color.yellow + Color.red;
        //            }
        //            else if (toonDist > 0 && toonDist < 5)
        //            {
        //                GUI.color = Color.red;
        //            }

        //            //GUI.color = Faction.getColor();
        //            GUILayout.BeginHorizontal();

        //            GUILayout.Label("Name: " + toon.GetHoverName());
        //            GUILayout.Label("HP: " + Mathf.RoundToInt(toon.GetHealth()) + "/" + toon.GetMaxHealth() 
        //                + " | Level: " + toon.GetLevel()
        //                + " | Dist: " + Mathf.RoundToInt(toonDist));

        //            GUILayout.EndHorizontal();
        //            GUI.color = Color.white;
        //        }
        //    }
        //    GUILayout.EndVertical();
        //    GUI.DragWindow(new Rect(0, 0, 10000, 20));
        //}

    }
}

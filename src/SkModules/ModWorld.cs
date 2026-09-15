using System;
using System.Collections.Generic;
using SkToolbox.Utility;
using UnityEngine;
using SkScope = SkToolbox.SkMenuController.SkScope;

namespace SkToolbox.SkModules
{
    /// <summary>
    /// Speelo's Toolbox: the World tab. Grouped into sections rather than one flat grid, because the tab now covers
    /// everything from terraforming to world rules, progression keys, travel, cleanup and server administration.
    /// Anything that needs a value first opens a form; everything else runs straight away.
    /// </summary>
    internal class ModWorld : SkBaseModule, IModule
    {
        private int radius = 5;
        private int height = 5;
        private int timeOfDay = 50;      // percent of a day, sent to /tod as 0..1
        private int windAngle = 0;       // degrees
        private int windIntensity = 50;  // percent, sent to /wind as 0..1

        public ModWorld() : base()
        {
            base.ModuleName = "World";
            base.Loading();
        }

        public void Start()
        {
            BeginMenu();
            base.Ready(); // Must be called when the module has completed initialization.
        }

        /// <summary>This tab renders an icon grid rather than a list, so the normal item list stays empty.</summary>
        public void BeginMenu()
        {
            MenuOptions = new SkMenu();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Runs one of the toolbox's own commands, which are registered with a leading slash.</summary>
        private static void Run(string command)
        {
            SkCommandProcessor.ProcessCommand(command, SkCommandProcessor.LogTo.Chat);
        }

        /// <summary>Formats a 0..1 value the way the console expects, regardless of the machine's decimal comma.</summary>
        private static string Fraction(int percent)
        {
            return (percent / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static SkMenuController.SkGridItem Cell(string label, string tip, Sprite icon, Action run, SkScope scope)
        {
            return Cell(null, label, tip, icon, run, scope);
        }

        private static SkMenuController.SkGridItem Cell(string section, string label, string tip, Sprite icon, Action run, SkScope scope)
        {
            return new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                Section = section,
                Scope = scope,
                OnClick = (string ignored) => run(),
            };
        }

        /// <summary>A cell that lights up while the thing it controls is on.</summary>
        private static SkMenuController.SkGridItem Toggle(string section, string label, string tip, Sprite icon, Action run, Func<bool> isOn, SkScope scope)
        {
            SkMenuController.SkGridItem cell = Cell(section, label, tip, icon, run, scope);
            cell.IsOn = isOn;
            return cell;
        }

        /// <summary>True when this game is the one running the world, rather than a client joined to someone else's.</summary>
        private static bool IsWorldHost()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }

        /// <summary>
        /// The game's event commands are marked onlyServer and are not forwarded to a server the way the world-key
        /// commands are. Our IsValid patch would happily run them on a client, but RandEventSystem is driven by
        /// whoever runs the world, so the change is discarded on the next sync. Saying why beats a dead button.
        /// </summary>
        private static void RunEvent(string command, string done)
        {
            if (!IsWorldHost())
            {
                SkCommandProcessor.Notify("Only the host or the server can change events.");
                return;
            }
            SkRun.CmdNotify(command, done);
        }

        private SkMenuController.SkForm NewForm(string title, string note)
        {
            return new SkMenuController.SkForm { Title = title, Note = note };
        }

        private static SkMenuController.SkFormField Choice(string id, string label, List<string> options, List<string> labels, int height)
        {
            SkMenuController.SkFormField field = new SkMenuController.SkFormField
            {
                Id = id,
                Label = label,
                Kind = SkMenuController.SkFieldKind.Choice,
                Height = height,
            };
            if (options != null) field.Options.AddRange(options);
            if (labels != null) field.OptionLabels.AddRange(labels);
            return field;
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
                IntValue = value,
            };
        }

        private static SkMenuController.SkFormField TextBox(string id, string label, string value)
        {
            return new SkMenuController.SkFormField
            {
                Id = id,
                Label = label,
                Kind = SkMenuController.SkFieldKind.Text,
                TextValue = value ?? "",
            };
        }

        /// <summary>The tick a destructive command has to have before it will run.</summary>
        private static SkMenuController.SkFormField ConfirmBox(string label)
        {
            return new SkMenuController.SkFormField
            {
                Id = "confirm",
                Label = label,
                Kind = SkMenuController.SkFieldKind.Toggle,
            };
        }

        private static readonly Func<SkMenuController.SkForm, string> ConfirmRequired =
            (SkMenuController.SkForm f) =>
            {
                SkMenuController.SkFormField box = f.Field("confirm");
                return (box != null && !box.BoolValue) ? "Switch the confirmation to ON first." : null;
            };

        private static List<string> EnumNames(Type type, params string[] skip)
        {
            List<string> names = new List<string>();
            foreach (string name in Enum.GetNames(type))
            {
                bool skipped = false;
                foreach (string unwanted in skip)
                {
                    if (string.Equals(name, unwanted, StringComparison.Ordinal)) { skipped = true; break; }
                }
                if (!skipped) names.Add(name);
            }
            return names;
        }

        // ------------------------------------------------------------------ root

        internal void ShowGrid()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                // -------- terrain
                Cell("Terrain", "Terrain Tools", "Level, raise, dig or undo terrain around you",
                     SkIcons.First("Hoe"), ShowTerrain, SkScope.Server),
                Cell("Building", "Repair Nearby", "Repair every building piece around you",
                     SkIcons.First("Hammer"), ShowRepairForm, SkScope.Server),

                Cell("Terrain", "Optimize Terrain", "Compact old terrain edits to help performance",
                     SkIcons.First("PickaxeIron", "PickaxeStone", "PickaxeAntler"), () => Run("/optterrain"), SkScope.Server),

                // -------- time and weather
                Cell("Time & Weather", "Weather", "Force a weather type, or hand control back to the game",
                     SkIcons.First("Thunderstone", "Ruby"), ShowWeather, SkScope.Client),
                Cell("Time & Weather", "Time of Day", "Set and lock the time, or unlock it",
                     SkIcons.First("SurtlingCore", "Coins"), ShowTime, SkScope.Client),
                Cell("Time & Weather", "Wind", "Set wind direction and strength, or reset it",
                     SkIcons.First("Feathers"), ShowWind, SkScope.Client),
                Cell("Time & Weather", "Events", "Start or stop a random event",
                     SkIcons.First("TrophyEikthyr", "Wishbone"), ShowEvents, SkScope.HostOnly),
                Cell("Time & Weather", "Sleep", "Skip straight to the next morning",
                     SkIcons.First("DeerHide", "LeatherScraps"), () => SkRun.CmdNotify("sleep", "Skipped to morning."), SkScope.Admin),
                Cell("Time & Weather", "Skip Time", "Jump forward by a number of in-game hours",
                     SkIcons.First("Coins", "Ruby"), ShowSkipTimeForm, SkScope.Admin),
                Cell("Time & Weather", "Game Speed", "Speed the whole world up or slow it down",
                     SkIcons.First("GreydwarfEye", "SurtlingCore"), ShowTimescaleForm, SkScope.Client),

                // -------- rules
                Cell("Rules", "Difficulty", "Scale enemies as if a set number of players were nearby",
                     SkIcons.First("TrophyDeer", "Coins"), ShowDifficultyForm, SkScope.Admin),
                Toggle("Rules", "No Spawn", "Stop creatures spawning naturally",
                       SkIcons.First("TrophyGreydwarf", "GreydwarfEye"),
                       () => SkRun.CmdNotify("nospawn", "Toggled natural spawning."),
                       () => SpawnSystem.m_nospawn, SkScope.Client),
                Toggle("Rules", "No Map", "Disable the map for this character",
                       SkIcons.First("Chain", "LeatherScraps"),
                       () => SkRun.CmdNotify("nomap", "Toggled the map."),
                       () => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoMap), SkScope.Client),
                Toggle("Rules", "No Portals", "Disable portals for the world",
                       SkIcons.First("SurtlingCore"),
                       () => SkRun.CmdNotify("noportals", "Toggled portals."),
                       () => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPortals), SkScope.Server),
                Cell("Rules", "World Modifier", "Change one world modifier, the same values the world settings screen offers",
                     SkIcons.First("Bronze", "Copper"), ShowWorldModifierForm, SkScope.Admin),
                Cell("Rules", "World Preset", "Apply a whole difficulty preset, replacing every modifier",
                     SkIcons.First("Iron", "Bronze"), ShowWorldPresetForm, SkScope.Admin),

                // -------- progression
                Cell("Progression", "Set Key", "Mark a boss as defeated or set another world key",
                     SkIcons.First("CryptKey", "Wishbone"), ShowSetKeyForm, SkScope.Admin),
                Cell("Progression", "Remove Key", "Clear a world key that is currently set",
                     SkIcons.First("Flint", "Stone"), ShowRemoveKeyForm, SkScope.Admin),
                Cell("Progression", "List Keys", "Show every key set on this world and character",
                     SkIcons.First("Coins"), () => SkRun.Show("World Keys", "listkeys", "No keys are set."), SkScope.Admin),
                Cell("Progression", "Reset World Keys", "Put every world modifier back to its default",
                     SkIcons.First("Ruby"), ShowResetWorldKeysForm, SkScope.Admin),
                Cell("Progression", "Player Keys", "Add or remove a key on your character rather than the world",
                     SkIcons.First("Amber", "Coins"), ShowPlayerKeyForm, SkScope.Client),

                // -------- travel
                Cell("Travel", "Go To", "Teleport to a map coordinate",
                     SkIcons.First("SurtlingCore", "Thunderstone"), ShowGoToForm, SkScope.HostOnly),
                Cell("Travel", "Recall Players", "Pull every other player to you",
                     SkIcons.First("Chain", "LeatherScraps"), () => SkRun.CmdNotify("recall", "Recalled other players."), SkScope.HostOnly),
                Cell("Travel", "Find", "Ping loaded objects and locations on the map, or teleport to the closest",
                     SkIcons.First("Wishbone"), ShowFindForm, SkScope.Client),
                Cell("Travel", "Find Biome", "Mark a biome on the map, or teleport to it",
                     SkIcons.First("Raspberry", "Blueberries"), ShowFindBiomeForm, SkScope.Client),
                Cell("Travel", "List Locations", "Show the loaded locations around you",
                     SkIcons.First("Stone", "Wood"), () => SkRun.Show("Loaded Locations", "printlocations", "No locations are loaded."), SkScope.Client),
                Cell("Travel", "Reveal Map", "Explore the entire minimap",
                     SkIcons.First("Torch"), () => Run("/revealmap"), SkScope.Client),
                Cell("Travel", "Reset Map", "Erase map exploration",
                     SkIcons.First("Coins", "Ruby"), () => Run("/resetmap"), SkScope.Client),
                Cell("Travel", "List Portals", "Print every portal tag",
                     SkIcons.First("GreydwarfEye", "SurtlingCore"), () => Run("/portals"), SkScope.Client),
                Cell("Travel", "Show Seed", "Print the world seed",
                     SkIcons.First("Barley", "Carrot"), () => Run("/seed"), SkScope.Client),

                // -------- cleanup
                // killenemycreatures kills creatures and stops there. Its sibling killenemies also sweeps every
                // SpawnArea in the scene with no distance test, which is why that one lives behind a confirmation.
                Cell("Cleanup", "Kill Enemies", "Kill every hostile creature nearby. Leaves their nests alone",
                     SkIcons.First("SwordIron", "SwordBronze", "AxeStone"), () => SkRun.CmdNotify("killenemycreatures", "Killed nearby enemies."), SkScope.Server),
                Cell("Cleanup", "Kill Enemies & Nests", "Also destroys every creature spawner in every loaded zone, permanently",
                     SkIcons.First("PickaxeIron", "SledgeStagbreaker", "Club"), ShowKillSpawnersForm, SkScope.Server),
                Cell("Cleanup", "Kill Tame", "Kill every tamed creature nearby",
                     SkIcons.First("Carrot", "Raspberry"), () => SkRun.CmdNotify("killtame", "Killed nearby tame creatures."), SkScope.Server),
                Cell("Cleanup", "Remove Drops", "Clear dropped items in the area",
                     SkIcons.First("Wood"), () => Run("/removedrops"), SkScope.Server),
                Cell("Cleanup", "Remove Fish", "Clear fish in the area",
                     SkIcons.First("FishRaw", "FishingRod"), () => SkRun.CmdNotify("removefish", "Removed nearby fish."), SkScope.Server),
                Cell("Cleanup", "Remove Birds", "Clear birds in the area",
                     SkIcons.First("Feathers"), () => SkRun.CmdNotify("removebirds", "Removed nearby birds."), SkScope.Server),
                Cell("Cleanup", "Stop Fire", "Put out spreading fire and its smoke",
                     SkIcons.First("Resin", "Coal"), () => SkRun.CmdNotify("stopfire", "Put out spreading fires."), SkScope.HostOnly),
                Cell("Cleanup", "Stop Smoke", "Clear lingering smoke",
                     SkIcons.First("Coal", "Resin"), () => SkRun.CmdNotify("stopsmoke", "Cleared smoke."), SkScope.HostOnly),
                Cell("Cleanup", "Save World", "Force a save now and restart the save timer",
                     SkIcons.First("Coins", "Ruby"), () => SkRun.CmdNotify("save", "World saved."), SkScope.Admin),

                // -------- server
                Cell("Server", "Kick", "Disconnect a player",
                     SkIcons.First("Club", "AxeStone"), () => ShowUserForm("Kick Player", "kick", "Kick"), SkScope.Admin),
                Cell("Server", "Ban", "Ban a player from the world",
                     SkIcons.First("SledgeStagbreaker", "Club"), () => ShowUserForm("Ban Player", "ban", "Ban"), SkScope.Admin),
                Cell("Server", "Unban", "Lift a ban by IP or user ID",
                     SkIcons.First("MeadHealthMinor", "Honey"), ShowUnbanForm, SkScope.Admin),
                Cell("Server", "Who's Online", "List the players connected right now",
                     SkIcons.First("Amber", "Coins"), () => SkRun.Show("Players Online", "/whois", "Nobody else is connected."), SkScope.Client),
                Cell("Server", "Banned List", "Show who is banned",
                     SkIcons.First("Coins"), () => SkRun.Show("Banned Users", "banned", "Nobody is banned."), SkScope.Admin),
                Cell("Server", "Ping", "Measure the round trip to the server",
                     SkIcons.First("Feathers"), () => SkRun.CmdNotify("ping", "Ping sent. The reply prints in the console."), SkScope.Client),
            };

            // Long enough now to be worth searching; the filter keeps the section boxes it matches.
            SkMC.RequestGridMenu(grid, (List<SkMenuController.SkMenuSlider>)null, "World", showFilter: true);
        }

        // ------------------------------------------------------------------ terrain

        private void ShowTerrain()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Level Terrain", "Flatten to your height, uses Radius", SkIcons.First("Hoe"),
                     () => Run("/tl " + radius), SkScope.Server),
                Cell("Raise Terrain", "Raise ground, uses Radius and Height", SkIcons.First("Stone", "Wood"),
                     () => Run("/tr " + radius + " " + height), SkScope.Server),
                Cell("Dig Terrain", "Lower ground, uses Radius and Height", SkIcons.First("PickaxeIron", "PickaxeStone", "PickaxeAntler"),
                     () => Run("/td " + radius + " " + height), SkScope.Server),
                Cell("Undo Terrain Edits", "Revert player terraforming back to the original ground, uses Radius", SkIcons.First("Cultivator"),
                     () => Run("/tu " + radius), SkScope.Server),
            };

            List<SkMenuController.SkMenuSlider> sliders = new List<SkMenuController.SkMenuSlider>
            {
                new SkMenuController.SkMenuSlider
                {
                    Label = "Radius", Min = 1, Max = 50,
                    Get = () => radius, Set = (int value) => radius = value,
                },
                new SkMenuController.SkMenuSlider
                {
                    Label = "Height", Min = 1, Max = 8,
                    Get = () => height, Set = (int value) => height = value,
                },
            };

            SkMC.RequestGridMenu(grid, sliders, "Terrain", showFilter: false);
        }

        // ------------------------------------------------------------------ weather

        private void ShowWeather()
        {
            SkMenu menu = new SkMenu();
            menu.AddItem("Let the game control weather", new Action(() => Run("/env -1")), "Stop forcing a weather type");

            if (EnvMan.instance != null && EnvMan.instance.m_environments != null)
            {
                List<string> names = new List<string>();
                foreach (EnvSetup setup in EnvMan.instance.m_environments)
                {
                    if (setup != null && !string.IsNullOrEmpty(setup.m_name))
                    {
                        names.Add(setup.m_name);
                    }
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string name in names)
                {
                    string captured = name;
                    menu.AddItem(captured, new Action(() => Run("/env " + captured)), "Force weather: " + captured);
                }
            }
            else
            {
                menu.AddItem("Weather list unavailable", new Action(() => { }), "Load into a world first");
            }

            SkMC.RequestSubMenu(menu.FlushMenu());
        }

        // ------------------------------------------------------------------ time of day

        private void ShowTime()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Apply Time", "Set and lock the time to the slider value", SkIcons.First("SurtlingCore"),
                     () => Run("/tod " + Fraction(timeOfDay)), SkScope.Client),
                Cell("Morning", "Set the time to sunrise", SkIcons.First("Coins"), () => Run("/tod 0.25"), SkScope.Client),
                Cell("Noon", "Set the time to midday", SkIcons.First("Ruby"), () => Run("/tod 0.5"), SkScope.Client),
                Cell("Night", "Set the time to midnight", SkIcons.First("Thunderstone"), () => Run("/tod 0"), SkScope.Client),
                Cell("Unlock Time", "Hand the clock back to the game", SkIcons.First("Feathers"), () => Run("/tod -1"), SkScope.Client),
            };

            List<SkMenuController.SkMenuSlider> sliders = new List<SkMenuController.SkMenuSlider>
            {
                new SkMenuController.SkMenuSlider
                {
                    Label = "Time %", Min = 0, Max = 100,
                    Get = () => timeOfDay, Set = (int value) => timeOfDay = value,
                },
            };

            SkMC.RequestGridMenu(grid, sliders, "Time of Day", showFilter: false);
        }

        /// <summary>The game counts time in seconds, so the slider is converted using this world's day length.</summary>
        private void ShowSkipTimeForm()
        {
            SkMenuController.SkForm form = NewForm("Skip Time", "Jumps the clock forward. Everything that depends on time moves with it, including crops, food and raids.");
            form.Fields.Add(Slider("hours", "In-game hours", 1, 72, 12));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Skip",
                Run = (SkMenuController.SkForm f) =>
                {
                    long dayLength = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1200L;
                    int seconds = Mathf.RoundToInt(f.Field("hours").IntValue * (dayLength / 24f));
                    SkRun.CmdNotify("skiptime " + seconds, "Skipped " + f.Field("hours").IntValue + " hours.");
                },
            });
            SkMC.ShowForm(form);
        }

        private void ShowTimescaleForm()
        {
            SkMenuController.SkForm form = NewForm("Game Speed", "Changes how fast the whole world runs. 100% is normal.");
            form.Fields.Add(Slider("speed", "Speed %", 25, 500, 100));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) =>
                {
                    SkRun.Cmd("timescale " + Fraction(f.Field("speed").IntValue) + " 1");
                    SkCommandProcessor.Notify("Game speed: " + f.Field("speed").IntValue + "%");
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Back to normal",
                Run = (SkMenuController.SkForm f) =>
                {
                    SkRun.Cmd("timescale 1 1");
                    SkCommandProcessor.Notify("Game speed back to normal.");
                },
            });
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ wind

        private void ShowWind()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Apply Wind", "Set wind to the slider values", SkIcons.First("Feathers"),
                     () => Run("/wind " + windAngle + " " + Fraction(windIntensity)), SkScope.Client),
                Cell("Reset Wind", "Hand the wind back to the game", SkIcons.First("Thunderstone"), () => Run("/resetwind"), SkScope.Client),
            };

            List<SkMenuController.SkMenuSlider> sliders = new List<SkMenuController.SkMenuSlider>
            {
                new SkMenuController.SkMenuSlider
                {
                    Label = "Angle", Min = 0, Max = 360,
                    Get = () => windAngle, Set = (int value) => windAngle = value,
                },
                new SkMenuController.SkMenuSlider
                {
                    Label = "Strength %", Min = 0, Max = 100,
                    Get = () => windIntensity, Set = (int value) => windIntensity = value,
                },
            };

            SkMC.RequestGridMenu(grid, sliders, "Wind", showFilter: false);
        }

        // ------------------------------------------------------------------ events

        /// <summary>
        /// The raids. Each one is a named entry on RandEventSystem with its own spawn table, so the list comes from
        /// the game rather than being hard-coded, and mods that add events show up here too. Starting a raid runs
        /// the game's "event" command; the commands themselves are onlyServer, which RunEvent explains.
        /// </summary>
        private void ShowEvents()
        {
            if (RandEventSystem.instance == null || RandEventSystem.instance.m_events == null)
            {
                SkCommandProcessor.Notify("Event list is not available yet. Load into a world first.");
                return;
            }

            List<string> names = new List<string>();
            List<string> labels = new List<string>();
            foreach (RandomEvent randomEvent in RandEventSystem.instance.m_events)
            {
                if (randomEvent == null || string.IsNullOrEmpty(randomEvent.m_name)) continue;
                names.Add(randomEvent.m_name);
                labels.Add(randomEvent.m_name + EventBlurb(randomEvent));
            }
            if (names.Count == 0)
            {
                SkCommandProcessor.Notify("This world has no events registered.");
                return;
            }
            SortPairs(names, labels);

            SkMenuController.SkForm form = NewForm("Events", IsWorldHost()
                ? "Raids spawn around you and run for a set time. Starting one replaces whatever is already running."
                : "Only the host or the server can change events, so these will be refused while you are a client.");
            form.Fields.Add(Choice("event", "Event", names, labels, 220));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Start event",
                Run = (SkMenuController.SkForm f) =>
                {
                    string name = f.Field("event").SelectedOption;
                    if (string.IsNullOrEmpty(name)) return;
                    RunEvent("event " + name, "Started event: " + name);
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Random",
                Run = (SkMenuController.SkForm f) => RunEvent("randomevent", "Started a random event."),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Stop current",
                Run = (SkMenuController.SkForm f) => RunEvent("stopevent", "Stopped the current event."),
            });
            SkMC.ShowForm(form);
        }

        /// <summary>The banner the game shows when a raid starts, which reads better than the internal name.</summary>
        private static string EventBlurb(RandomEvent randomEvent)
        {
            try
            {
                if (Localization.instance != null && !string.IsNullOrEmpty(randomEvent.m_startMessage))
                {
                    string localized = Localization.instance.Localize(randomEvent.m_startMessage);
                    if (!string.IsNullOrEmpty(localized) && !localized.StartsWith("["))
                    {
                        return "     <color=#9FB6CC>" + localized + "</color>";
                    }
                }
            }
            catch (Exception)
            {
            }
            return "";
        }

        /// <summary>Sorts a value list and its label list together, so the two stay lined up.</summary>
        private static void SortPairs(List<string> values, List<string> labels)
        {
            int[] order = new int[values.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (int a, int b) => string.Compare(values[a], values[b], StringComparison.OrdinalIgnoreCase));

            List<string> sortedValues = new List<string>(values.Count);
            List<string> sortedLabels = new List<string>(labels.Count);
            foreach (int index in order)
            {
                sortedValues.Add(values[index]);
                sortedLabels.Add(labels[index]);
            }
            values.Clear();
            values.AddRange(sortedValues);
            labels.Clear();
            labels.AddRange(sortedLabels);
        }

        /// <summary>
        /// The game's killenemies runs a FindObjectsByType&lt;SpawnArea&gt; sweep with no distance check, so it wipes out
        /// every greydwarf nest and draugr pile in every loaded zone, for everyone, with no way back. That is a long
        /// way from what a button called "kill nearby enemies" implies, so it asks first.
        /// </summary>
        private void ShowKillSpawnersForm()
        {
            SkMenuController.SkForm form = NewForm("Kill Enemies & Nests",
                "Spawners do not regenerate. Everyone on this world loses them, and the only way back is a world with those zones never loaded.");
            form.Warning = "This DESTROYS every creature spawner in every loaded zone.";
            form.Fields.Add(ConfirmBox("Destroy the spawners too"));
            form.Validate = ConfirmRequired;
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Kill and destroy",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("killenemies", "Killed nearby enemies and their spawners."),
            });
            SkMC.ShowForm(form);
        }

        /// <summary>
        /// Repairs every building piece in range. WearNTear.Repair refuses a piece that is already whole and
        /// rate-limits itself to once a second per piece, so the count reported is genuinely what needed work.
        /// The repair goes out as an RPC to whoever owns the piece, so it works on someone else's build too.
        /// </summary>
        private void ShowRepairForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            SkMenuController.SkForm form = NewForm("Repair Nearby",
                "Repairs worn and damaged building pieces around you. Pieces that are already whole are left alone.");
            form.Fields.Add(Slider("radius", "Radius", 5, 100, 30));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Repair",
                Run = (SkMenuController.SkForm f) =>
                {
                    Player lp = Player.m_localPlayer;
                    if (lp == null) { SkCommandProcessor.Notify("No player yet."); return; }

                    float radius = f.Field("radius").IntValue;
                    Vector3 origin = lp.transform.position;
                    int repaired = 0;

                    WearNTear[] pieces = UnityEngine.Object.FindObjectsByType<WearNTear>(FindObjectsSortMode.None);
                    foreach (WearNTear piece in pieces)
                    {
                        if (piece == null) continue;
                        if (Vector3.Distance(piece.transform.position, origin) > radius) continue;
                        try
                        {
                            if (piece.Repair()) repaired++;
                        }
                        catch (Exception)
                        {
                        }
                    }

                    SkCommandProcessor.Notify(repaired > 0
                        ? "Repaired " + repaired + " pieces."
                        : "Nothing nearby needed repairing.");
                },
            });
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ rules

        private void ShowDifficultyForm()
        {
            SkMenuController.SkForm form = NewForm("Force Difficulty",
                "Scales enemy health and damage as if this many players were nearby. 0 hands it back to the game.");
            form.Fields.Add(Slider("players", "Players", 0, 10, 1));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) =>
                    SkRun.CmdNotify("players " + f.Field("players").IntValue, "Difficulty set."),
            });
            SkMC.ShowForm(form);
        }

        private void ShowWorldModifierForm()
        {
            SkMenuController.SkForm form = NewForm("World Modifier",
                "The same settings the world options screen offers. They apply to everyone on this world.");
            form.Fields.Add(Choice("modifier", "Modifier", EnumNames(typeof(WorldModifiers), "Default"), null, 120));
            form.Fields.Add(Choice("value", "Value", EnumNames(typeof(WorldModifierOption)), null, 120));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) =>
                {
                    string modifier = f.Field("modifier").SelectedOption;
                    string value = f.Field("value").SelectedOption;
                    if (string.IsNullOrEmpty(modifier) || string.IsNullOrEmpty(value)) return;
                    SkRun.Cmd("setworldmodifier " + modifier + " " + value);
                    SkCommandProcessor.Notify(modifier + ": " + value);
                },
            });
            SkMC.ShowForm(form);
        }

        private void ShowWorldPresetForm()
        {
            SkMenuController.SkForm form = NewForm("World Preset",
                "Replaces every world modifier with the preset's values. Anything you set by hand is lost.");
            form.Fields.Add(Choice("preset", "Preset", EnumNames(typeof(WorldPresets), "Custom"), null, 150));
            form.Fields.Add(ConfirmBox("Replace all current modifiers"));
            form.Validate = ConfirmRequired;
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply preset",
                Run = (SkMenuController.SkForm f) =>
                {
                    string preset = f.Field("preset").SelectedOption;
                    if (string.IsNullOrEmpty(preset)) return;
                    SkRun.Cmd("setworldpreset " + preset);
                    SkCommandProcessor.Notify("World preset: " + preset);
                },
            });
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ progression keys

        /// <summary>
        /// Every key the game knows about. The enum covers world modifiers and the five original boss keys; later
        /// bosses carry theirs on the prefab instead (Character.m_defeatSetGlobalKey), so those are collected too.
        /// </summary>
        private static List<string> KnownKeys()
        {
            List<string> keys = EnumNames(typeof(GlobalKeys), "NonServerOption", "Count", "Preset");

            if (ZNetScene.instance != null && ZNetScene.instance.m_prefabs != null)
            {
                foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
                {
                    if (prefab == null) continue;
                    Character character = prefab.GetComponent<Character>();
                    if (character == null || string.IsNullOrEmpty(character.m_defeatSetGlobalKey)) continue;
                    if (!keys.Contains(character.m_defeatSetGlobalKey)) keys.Add(character.m_defeatSetGlobalKey);
                }
            }
            return keys;
        }

        private void ShowSetKeyForm()
        {
            if (ZoneSystem.instance == null)
            {
                SkCommandProcessor.Notify("Load into a world first.");
                return;
            }

            List<string> keys = KnownKeys();
            List<string> labels = new List<string>();
            foreach (string key in keys)
            {
                bool already = false;
                try { already = ZoneSystem.instance.GetGlobalKey(key); } catch (Exception) { }
                labels.Add(already ? key + "     <color=#7CFC00>already set</color>" : key);
            }

            SkMenuController.SkForm form = NewForm("Set World Key",
                "Boss keys unlock crafting and change what spawns. This is the whole world, for everyone on it.");
            form.Fields.Add(Choice("key", "Key", keys, labels, 200));
            form.Fields.Add(ConfirmBox("Change this world for everyone"));
            form.Validate = ConfirmRequired;
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Set key",
                Run = (SkMenuController.SkForm f) =>
                {
                    string key = f.Field("key").SelectedOption;
                    if (string.IsNullOrEmpty(key)) return;
                    SkRun.CmdNotify("setkey " + key, "Set key: " + key);
                },
            });
            SkMC.ShowForm(form);
        }

        private void ShowRemoveKeyForm()
        {
            List<string> keys = ZoneSystem.instance != null ? ZoneSystem.instance.GetGlobalKeys() : null;
            if (keys == null || keys.Count == 0)
            {
                SkCommandProcessor.Notify("No world keys are set.");
                return;
            }

            SkMenuController.SkForm form = NewForm("Remove World Key", "Only keys currently set on this world are listed.");
            form.Fields.Add(Choice("key", "Key", keys, null, 220));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Remove key",
                Run = (SkMenuController.SkForm f) =>
                {
                    string key = f.Field("key").SelectedOption;
                    if (string.IsNullOrEmpty(key)) return;
                    SkRun.CmdNotify("removekey " + key, "Removed key: " + key);
                },
            });
            SkMC.ShowForm(form);
        }

        private void ShowResetWorldKeysForm()
        {
            SkMenuController.SkForm form = NewForm("Reset World Keys",
                "Puts every world modifier back to its default. Boss progress set through the world keys goes with it.");
            form.Fields.Add(ConfirmBox("Reset the world modifiers"));
            form.Validate = ConfirmRequired;
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Reset",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("resetworldkeys", "World keys reset."),
            });
            SkMC.ShowForm(form);
        }

        private void ShowPlayerKeyForm()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            List<string> keys = EnumNames(typeof(PlayerKeys));
            List<string> owned = player.GetUniqueKeys();
            if (owned != null)
            {
                foreach (string key in owned)
                {
                    if (!string.IsNullOrEmpty(key) && !keys.Contains(key)) keys.Add(key);
                }
            }

            List<string> labels = new List<string>();
            foreach (string key in keys)
            {
                bool has = owned != null && owned.Contains(key);
                labels.Add(has ? key + "     <color=#7CFC00>on your character</color>" : key);
            }

            SkMenuController.SkForm form = NewForm("Player Keys",
                "Keys stored on your character rather than the world. They are saved with the character.");
            form.Fields.Add(Choice("key", "Key", keys, labels, 220));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Add to player",
                Run = (SkMenuController.SkForm f) =>
                {
                    string key = f.Field("key").SelectedOption;
                    if (string.IsNullOrEmpty(key)) return;
                    SkRun.CmdNotify("setkeyplayer " + key, "Added player key: " + key);
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Remove from player",
                Run = (SkMenuController.SkForm f) =>
                {
                    string key = f.Field("key").SelectedOption;
                    if (string.IsNullOrEmpty(key)) return;
                    SkRun.CmdNotify("removekeyplayer " + key, "Removed player key: " + key);
                },
            });
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ travel

        private void ShowGoToForm()
        {
            Player player = Player.m_localPlayer;
            string x = "0", z = "0";
            if (player != null)
            {
                x = Mathf.RoundToInt(player.transform.position.x).ToString(System.Globalization.CultureInfo.InvariantCulture);
                z = Mathf.RoundToInt(player.transform.position.z).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            SkMenuController.SkForm form = NewForm("Go To", "Map coordinates, the same pair the position readout shows. Starts on where you are now.");
            form.Fields.Add(TextBox("x", "X", x));
            form.Fields.Add(TextBox("z", "Z", z));
            form.Validate = (SkMenuController.SkForm f) =>
            {
                int ignored;
                if (!int.TryParse((f.Field("x").TextValue ?? "").Trim(), out ignored)) return "X has to be a whole number.";
                if (!int.TryParse((f.Field("z").TextValue ?? "").Trim(), out ignored)) return "Z has to be a whole number.";
                return null;
            };
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Teleport",
                Run = (SkMenuController.SkForm f) =>
                {
                    string target = (f.Field("x").TextValue ?? "").Trim() + " " + (f.Field("z").TextValue ?? "").Trim();
                    SkRun.Cmd("goto " + target);
                    SkCommandProcessor.Notify("Teleporting to " + target);
                },
            });
            SkMC.ShowForm(form);
        }

        private void ShowFindForm()
        {
            SkMenuController.SkForm form = NewForm("Find",
                "Searches loaded objects and known locations by name. Pinging marks them on the map; teleporting takes you to the closest.");
            form.Fields.Add(TextBox("text", "Search for", ""));
            form.Fields.Add(Slider("pings", "Most pins to place", 1, 20, 1));
            form.Validate = (SkMenuController.SkForm f) =>
                string.IsNullOrEmpty((f.Field("text").TextValue ?? "").Trim()) ? "Type something to search for." : null;
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Pin on map",
                Run = (SkMenuController.SkForm f) =>
                    SkRun.CmdNotify("find " + (f.Field("text").TextValue ?? "").Trim() + " " + f.Field("pings").IntValue, "Searched the map."),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Teleport to closest",
                Run = (SkMenuController.SkForm f) =>
                    SkRun.CmdNotify("findtp " + (f.Field("text").TextValue ?? "").Trim(), "Looking for the closest match."),
            });
            SkMC.ShowForm(form);
        }

        private void ShowFindBiomeForm()
        {
            SkMenuController.SkForm form = NewForm("Find Biome", "Marks every sector of that biome on the map, or takes you to the closest one.");
            form.Fields.Add(Choice("biome", "Biome", EnumNames(typeof(Heightmap.Biome), "None", "All", "Land"), null, 180));
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Pin on map",
                Run = (SkMenuController.SkForm f) =>
                    SkRun.CmdNotify("findbiome " + f.Field("biome").SelectedOption, "Marked that biome."),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Teleport there",
                Run = (SkMenuController.SkForm f) =>
                    SkRun.CmdNotify("findbiometp " + f.Field("biome").SelectedOption, "Looking for that biome."),
            });
            SkMC.ShowForm(form);
        }

        // ------------------------------------------------------------------ server

        /// <summary>Names of everyone else connected, for the kick and ban pickers.</summary>
        private static List<string> ConnectedPlayers()
        {
            List<string> names = new List<string>();
            if (ZNet.instance == null) return names;
            List<ZNetPeer> peers = ZNet.instance.GetPeers();
            if (peers == null) return names;
            foreach (ZNetPeer peer in peers)
            {
                if (peer != null && !string.IsNullOrEmpty(peer.m_playerName) && !names.Contains(peer.m_playerName))
                {
                    names.Add(peer.m_playerName);
                }
            }
            return names;
        }

        /// <summary>Kick and ban differ only in the command they send, so they share one form.</summary>
        private void ShowUserForm(string title, string command, string actionLabel)
        {
            List<string> players = ConnectedPlayers();
            SkMenuController.SkForm form = NewForm(title, players.Count > 0
                ? "Pick someone who is connected, or type a name, IP or user ID instead."
                : "Nobody else is connected. Type a name, IP or user ID.");

            if (players.Count > 0)
            {
                form.Fields.Add(Choice("peer", "Connected players", players, null, 130));
            }
            form.Fields.Add(TextBox("name", "Or type a name / IP / user ID", ""));
            form.Fields.Add(ConfirmBox(actionLabel + " this player"));
            form.Validate = (SkMenuController.SkForm f) =>
            {
                if (string.IsNullOrEmpty(UserTarget(f))) return "Pick a player or type a name.";
                return ConfirmRequired(f);
            };
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = actionLabel,
                Run = (SkMenuController.SkForm f) =>
                {
                    string target = UserTarget(f);
                    SkRun.CmdNotify(command + " " + target, actionLabel + ": " + target);
                },
            });
            SkMC.ShowForm(form);
        }

        /// <summary>A typed name wins over the picker, so an offline player can still be named.</summary>
        private static string UserTarget(SkMenuController.SkForm form)
        {
            SkMenuController.SkFormField typed = form.Field("name");
            string text = typed != null ? (typed.TextValue ?? "").Trim() : "";
            if (text.Length > 0) return text;
            SkMenuController.SkFormField picker = form.Field("peer");
            return picker != null ? picker.SelectedOption : null;
        }

        private void ShowUnbanForm()
        {
            SkMenuController.SkForm form = NewForm("Unban", "Lifts a ban. The banned list shows what to type here.");
            form.Fields.Add(TextBox("name", "IP or user ID", ""));
            form.Validate = (SkMenuController.SkForm f) =>
                string.IsNullOrEmpty((f.Field("name").TextValue ?? "").Trim()) ? "Type an IP or user ID." : null;
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Unban",
                Run = (SkMenuController.SkForm f) =>
                {
                    string target = (f.Field("name").TextValue ?? "").Trim();
                    SkRun.CmdNotify("unban " + target, "Unbanned " + target);
                },
            });
            SkMC.ShowForm(form);
        }
    }
}

using System;
using System.Collections.Generic;
using SkToolbox.Utility;
using UnityEngine;

namespace SkToolbox.SkModules
{
    /// <summary>
    /// Speelo's Toolbox: the World tab. Terrain tools, weather, time, wind, events, map and cleanup.
    /// Terrain radius and height are sliders rather than the old fixed-preset submenus, and every entry spells
    /// out what it does instead of the "T - ..." shorthand.
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

        private static void Run(string command)
        {
            SkCommandProcessor.ProcessCommand(command, SkCommandProcessor.LogTo.Chat);
        }

        /// <summary>Formats a 0..1 value the way the console expects, regardless of the machine's decimal comma.</summary>
        private static string Fraction(int percent)
        {
            return (percent / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static SkMenuController.SkGridItem Cell(string label, string tip, Sprite icon, Action run)
        {
            return new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                OnClick = (string ignored) => run(),
            };
        }

        // ------------------------------------------------------------------ root

        internal void ShowGrid()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Terrain", "Level, raise, dig or undo terrain around you", SkIcons.First("Hoe"), ShowTerrain),
                Cell("Weather", "Force a weather type, or hand control back to the game", SkIcons.First("Thunderstone", "Ruby"), ShowWeather),
                Cell("Time of Day", "Set and lock the time, or unlock it", SkIcons.First("SurtlingCore", "Coins"), ShowTime),
                Cell("Wind", "Set wind direction and strength, or reset it", SkIcons.First("Feathers"), ShowWind),
                Cell("Events", "Start or stop a random event", SkIcons.First("TrophyEikthyr", "Wishbone"), ShowEvents),
                Cell("Reveal Map", "Explore the entire minimap", SkIcons.First("Torch"), () => Run("/revealmap")),
                Cell("Reset Map", "Erase map exploration", SkIcons.First("Coins", "Ruby"), () => Run("/resetmap")),
                Cell("List Portals", "Print every portal tag", SkIcons.First("SurtlingCore"), () => Run("/portals")),
                Cell("Remove Drops", "Clear dropped items in the area", SkIcons.First("Wood"), () => Run("/removedrops")),
                Cell("Show Seed", "Print the world seed", SkIcons.First("Carrot", "Barley"), () => Run("/seed")),
                Cell("Optimize Terrain", "Compact old terrain edits to help performance", SkIcons.First("PickaxeIron", "PickaxeStone", "PickaxeAntler"), () => Run("/optterrain")),
            };
            SkMC.RequestGridMenu(grid, (List<SkMenuController.SkMenuSlider>)null, "World", showFilter: false);
        }

        // ------------------------------------------------------------------ terrain

        private void ShowTerrain()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Level Terrain", "Flatten to your height, uses Radius", SkIcons.First("Hoe"),
                     () => Run("/tl " + radius)),
                Cell("Raise Terrain", "Raise ground, uses Radius and Height", SkIcons.First("Stone", "Wood"),
                     () => Run("/tr " + radius + " " + height)),
                Cell("Dig Terrain", "Lower ground, uses Radius and Height", SkIcons.First("PickaxeIron", "PickaxeStone", "PickaxeAntler"),
                     () => Run("/td " + radius + " " + height)),
                Cell("Undo Terrain Edits", "Revert player terraforming back to the original ground, uses Radius", SkIcons.First("Cultivator"),
                     () => Run("/tu " + radius)),
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
                     () => Run("/tod " + Fraction(timeOfDay))),
                Cell("Morning", "Set the time to sunrise", SkIcons.First("Coins"), () => Run("/tod 0.25")),
                Cell("Noon", "Set the time to midday", SkIcons.First("Ruby"), () => Run("/tod 0.5")),
                Cell("Night", "Set the time to midnight", SkIcons.First("Thunderstone"), () => Run("/tod 0")),
                Cell("Unlock Time", "Hand the clock back to the game", SkIcons.First("Feathers"), () => Run("/tod -1")),
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

        // ------------------------------------------------------------------ wind

        private void ShowWind()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Apply Wind", "Set wind to the slider values", SkIcons.First("Feathers"),
                     () => Run("/wind " + windAngle + " " + Fraction(windIntensity))),
                Cell("Reset Wind", "Hand the wind back to the game", SkIcons.First("Thunderstone"), () => Run("/resetwind")),
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

        private void ShowEvents()
        {
            SkMenu menu = new SkMenu();
            menu.AddItem("Stop current event", new Action(() => Run("/stopevent")), "Ends whatever event is running");
            menu.AddItem("Random event", new Action(() => Run("/randomevent")), "Starts a random event");

            if (RandEventSystem.instance != null && RandEventSystem.instance.m_events != null)
            {
                List<string> names = new List<string>();
                foreach (RandomEvent randomEvent in RandEventSystem.instance.m_events)
                {
                    if (randomEvent != null && !string.IsNullOrEmpty(randomEvent.m_name))
                    {
                        names.Add(randomEvent.m_name);
                    }
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);
                foreach (string name in names)
                {
                    string captured = name;
                    menu.AddItem(captured, new Action(() => Run("/event " + captured)), "Start event: " + captured);
                }
            }
            else
            {
                menu.AddItem("Event list unavailable", new Action(() => { }), "Load into a world first");
            }

            SkMC.RequestSubMenu(menu.FlushMenu());
        }
    }
}

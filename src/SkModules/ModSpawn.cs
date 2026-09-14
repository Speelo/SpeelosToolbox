using System;
using System.Collections.Generic;
using SkToolbox.Utility;
using UnityEngine;

namespace SkToolbox.SkModules
{
    /// <summary>
    /// Speelo's Toolbox: the Spawn tab. Every creature in the game as a grid, with sliders for how many and what
    /// star level, and a toggle to spawn them tamed.
    /// </summary>
    internal class ModSpawn : SkBaseModule, IModule
    {
        private const int MaxCount = 20;   // more than this hitches badly when they all wake at once
        // Level 1 is a plain creature, 2 is one star, 3 is two stars. SetLevel accepts more, and health, damage and
        // loot all keep scaling, but LevelEffects.SetupLevelVisualization bails out when a creature has no art for
        // the level, and creatures ship two setups. Above 3 you get an invisible buff, so the slider stops there.
        private const int MaxLevel = 3;

        private int count = 1;
        private int level = 1;
        private bool tamed = false;

        private List<SkMenuController.SkGridItem> cache;
        private int cachedFor = 0;

        /// <summary>
        /// Creatures whose trophy is not named after the prefab. Everything else is tried as "Trophy" + prefab name,
        /// and anything with no trophy at all simply shows its name instead of an icon.
        /// </summary>
        private static readonly Dictionary<string, string> TrophyAliases = new Dictionary<string, string>
        {
            { "Troll", "TrophyForestTroll" },
            { "gd_king", "TrophyTheElder" },
            { "Dragon", "TrophyDragonQueen" },
            { "StoneGolem", "TrophySGolem" },
            { "Greyling", "TrophyGreydwarf" },
            { "Greydwarf_Elite", "TrophyGreydwarfBrute" },
            { "Greydwarf_Shaman", "TrophyGreydwarfShaman" },
            { "Draugr_Elite", "TrophyDraugrElite" },
            { "BlobTar", "TrophyBlobTar" },
            { "Hen", "TrophyHen" },
            { "Chicken", "TrophyHen" },
        };

        public ModSpawn() : base()
        {
            base.ModuleName = "Spawn";
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

        internal void ShowGrid()
        {
            // ZNetScene is rebuilt per world load, so rebuild the list when it changes.
            int key = ZNetScene.instance != null ? ZNetScene.instance.GetInstanceID() : 0;
            if (cache == null || cache.Count == 0 || cachedFor != key)
            {
                cache = BuildCreatures();
                cachedFor = key;
            }
            if (cache.Count == 0)
            {
                SkCommandProcessor.Notify("Creature list is not available yet. Load into a world first.");
                return;
            }

            List<SkMenuController.SkMenuSlider> sliders = new List<SkMenuController.SkMenuSlider>
            {
                new SkMenuController.SkMenuSlider
                {
                    Label = "How many", Min = 1, Max = MaxCount,
                    Get = () => count, Set = (int value) => count = value,
                },
                new SkMenuController.SkMenuSlider
                {
                    Label = "Level", Min = 1, Max = MaxLevel,
                    Get = () => level, Set = (int value) => level = value,
                },
            };

            List<SkMenuController.SkMenuToggle> toggles = new List<SkMenuController.SkMenuToggle>
            {
                new SkMenuController.SkMenuToggle
                {
                    Label = "Spawn tamed",
                    Get = () => tamed, Set = (bool value) => tamed = value,
                },
            };

            SkMC.RequestGridMenu(cache, sliders, "Spawn", showFilter: true, toggles: toggles);
        }

        /// <summary>Every prefab with a Character component, sorted by its localized name.</summary>
        private List<SkMenuController.SkGridItem> BuildCreatures()
        {
            List<SkMenuController.SkGridItem> list = new List<SkMenuController.SkGridItem>();
            if (ZNetScene.instance == null || ZNetScene.instance.m_prefabs == null)
            {
                return list;
            }

            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null || string.IsNullOrEmpty(prefab.name))
                {
                    continue;
                }
                Character character = prefab.GetComponent<Character>();
                if (character == null)
                {
                    continue;
                }

                string display = prefab.name;
                try
                {
                    if (Localization.instance != null && !string.IsNullOrEmpty(character.m_name))
                    {
                        string localized = Localization.instance.Localize(character.m_name);
                        if (!string.IsNullOrEmpty(localized) && !localized.StartsWith("["))
                        {
                            display = localized;
                        }
                    }
                }
                catch (Exception)
                {
                }

                list.Add(new SkMenuController.SkGridItem
                {
                    Name = prefab.name,
                    Display = display,
                    Tip = display + "   (" + prefab.name + ")" + (character.m_boss ? "   [boss]" : ""),
                    Icon = TrophyIcon(prefab.name),
                    Scope = SkMenuController.SkScope.Server,
                    OnClick = Spawn,
                });
            }

            list.Sort((SkMenuController.SkGridItem a, SkMenuController.SkGridItem b) =>
                string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>Creatures have no icon of their own, so borrow the trophy's where one exists.</summary>
        private static Sprite TrophyIcon(string prefabName)
        {
            string alias;
            if (TrophyAliases.TryGetValue(prefabName, out alias))
            {
                Sprite aliased = SkIcons.Item(alias);
                if (aliased != null)
                {
                    return aliased;
                }
            }
            return SkIcons.Item("Trophy" + prefabName);
        }

        /// <summary>Spawns the chosen creature in a small ring in front of the player.</summary>
        private void Spawn(string prefabName)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            if (prefab == null)
            {
                SkCommandProcessor.Notify("Creature not found: " + prefabName);
                return;
            }

            int wanted = Mathf.Clamp(count, 1, MaxCount);
            int spawned = 0;
            Vector3 origin = player.transform.position + player.transform.forward * 2.5f + Vector3.up;

            for (int i = 0; i < wanted; i++)
            {
                try
                {
                    // Spread them around a ring so they do not all land inside each other.
                    float angle = (wanted <= 1) ? 0f : (360f / wanted) * i;
                    float radius = (wanted <= 1) ? 0f : Mathf.Clamp(wanted * 0.25f, 1f, 4f);
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

                    GameObject instance = UnityEngine.Object.Instantiate(prefab, origin + offset, Quaternion.identity);
                    if (instance == null)
                    {
                        continue;
                    }
                    spawned++;

                    Character character = instance.GetComponent<Character>();
                    if (character != null && level > 1)
                    {
                        character.SetLevel(Mathf.Clamp(level, 1, MaxLevel));
                    }
                    if (tamed)
                    {
                        // MonsterAI.MakeTame sets the AI up as well, which Character.SetTamed alone does not.
                        MonsterAI ai = instance.GetComponent<MonsterAI>();
                        if (ai != null)
                        {
                            ai.MakeTame();
                        }
                        else if (character != null)
                        {
                            character.SetTamed(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SkUtilities.Logz(new string[] { "SPAWN", "ERROR" }, new string[] { ex.Message }, LogType.Error);
                }
            }

            SkCommandProcessor.Notify(spawned > 0
                ? "Spawned " + spawned + "x " + prefabName + (level > 1 ? " (level " + level + ")" : "") + (tamed ? ", tamed" : "")
                : "Could not spawn " + prefabName);
        }
    }
}

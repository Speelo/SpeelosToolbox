using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkToolbox.SkModules
{
    /// <summary>
    /// Speelo's Toolbox: the Give tab. Shows every item in the game as a grid of icons with a search box and a
    /// quantity slider, and puts the picked item straight into the player's inventory.
    /// Split out of ModPlayer so it gets its own tab instead of being buried in a submenu.
    /// </summary>
    internal class ModGive : SkBaseModule, IModule
    {
        private int quantity = 1;
        private List<SkMenuController.SkGridItem> cache;
        private int cachedFor = 0;

        public ModGive() : base()
        {
            base.ModuleName = "Give";
            base.Loading();
        }

        public void Start()
        {
            BeginMenu();
            base.Ready(); // Must be called when the module has completed initialization.
        }

        /// <summary>This tab renders a grid rather than a list, so the normal item list stays empty.</summary>
        public void BeginMenu()
        {
            MenuOptions = new SkMenu();
        }

        /// <summary>Entry point for the tab. Builds the grid once, then shows it.</summary>
        internal void ShowGrid()
        {
            // ObjectDB is rebuilt on every world load, and mods register their items into it, so a cache built
            // against a previous world would both miss new modded items and hold dead prefab references.
            int key = ObjectDB.instance != null ? ObjectDB.instance.GetInstanceID() : 0;
            if (cache == null || cache.Count == 0 || cachedFor != key)
            {
                cache = BuildItems();
                cachedFor = key;
            }
            if (cache.Count == 0)
            {
                SkCommandProcessor.Notify("Item list is not available yet. Load into a world first.");
                return;
            }
            SkMC.RequestGridMenu(cache, new SkMenuController.SkMenuSlider
            {
                Label = "Quantity",
                Min = 1,
                Max = 100,
                Get = () => quantity,
                Set = (int value) => quantity = value,
            }, "Give", showFilter: true);
        }

        /// <summary>Every item prefab that has an ItemDrop, sorted by its display name.</summary>
        private List<SkMenuController.SkGridItem> BuildItems()
        {
            List<SkMenuController.SkGridItem> list = new List<SkMenuController.SkGridItem>();
            if (ObjectDB.instance == null || ObjectDB.instance.m_items == null)
            {
                return list;
            }

            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                if (prefab == null || string.IsNullOrEmpty(prefab.name))
                {
                    continue;
                }
                // ObjectDB holds entries without an ItemDrop; skip them rather than crash on their data.
                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                {
                    continue;
                }

                Sprite icon = null;
                Sprite[] icons = drop.m_itemData.m_shared.m_icons;
                if (icons != null && icons.Length > 0)
                {
                    icon = icons[0];
                }

                string display = prefab.name;
                try
                {
                    if (Localization.instance != null && !string.IsNullOrEmpty(drop.m_itemData.m_shared.m_name))
                    {
                        string localized = Localization.instance.Localize(drop.m_itemData.m_shared.m_name);
                        if (!string.IsNullOrEmpty(localized))
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
                    Icon = icon,
                    Tip = display + "   (" + prefab.name + ")",
                    OnClick = Give,
                });
            }

            list.Sort((SkMenuController.SkGridItem a, SkMenuController.SkGridItem b) =>
                string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        /// <summary>
        /// Adds the item to the player's inventory, in stacks the item actually allows.
        /// Upstream instantiated the prefab on the ground and called SetLevel on a Character component that items
        /// do not have, so every give threw a swallowed NullReferenceException.
        /// </summary>
        private void Give(string prefabName)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                SkCommandProcessor.Notify("No player yet.");
                return;
            }

            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
            {
                SkCommandProcessor.Notify("Item not found: " + prefabName);
                return;
            }

            int remaining = Mathf.Max(1, quantity);
            int maxStack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            // Matches what the game does when it spawns items: only flag them as cheated when cheat checks are live.
            bool cheated = !PlayerProfile.s_bypassCheatChecks;
            int given = 0;

            while (remaining > 0)
            {
                int take = Mathf.Min(remaining, maxStack);
                ItemDrop.ItemData added = player.GetInventory().AddItem(
                    prefabName, take, drop.m_itemData.m_quality, drop.m_itemData.m_variant, 0L, "", cheated, true);
                if (added == null)
                {
                    break; // no room left
                }
                given += take;
                remaining -= take;
            }

            if (given > 0)
            {
                SkCommandProcessor.Notify("Gave " + given + "x " + prefabName + (remaining > 0 ? " (inventory full)" : ""));
            }
            else
            {
                SkCommandProcessor.Notify("Inventory full, could not give " + prefabName);
            }
        }
    }
}

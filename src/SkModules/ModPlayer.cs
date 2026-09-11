using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkToolbox.Utility;
using UnityEngine;

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

            Action(grid, "Repair All", "Repair all your items", SkIcons.First("Hammer"), RepairAll);
            Action(grid, "Heal Self", "Heal yourself", SkIcons.First("MeadHealthMedium", "MeadHealthMinor", "Honey"), Heal);
            Action(grid, "Tame", "Tame all nearby creatures", SkIcons.First("Carrot", "Raspberry"), Tame);

            Toggle(grid, "Teleport to Mouse", "Press tilde (~) to teleport", SkIcons.First("SurtlingCore", "Thunderstone"),
                   ToggleTeleport, () => bTeleport);
            Toggle(grid, "Build Anywhere", "Remove build restrictions", SkIcons.First("Cultivator", "Hoe"),
                   ToggleAnywhere, () => SkCommandPatcher.bBuildAnywhere);
            Toggle(grid, "No Cost Building", "Unlock all pieces and build for free", SkIcons.First("Wood", "Stone"),
                   ToggleNoCost, () => Player.m_localPlayer != null && Player.m_localPlayer.NoCostCheat());
            Toggle(grid, "Detect Nearby Enemies", "Range: 20m", SkIcons.First("Wishbone"),
                   ToggleESPEnemies, () => SkCommandProcessor.bDetectEnemies);
            Toggle(grid, "Display Coordinates", "Show coords in the top left corner", SkIcons.First("FishingRodFloat", "Thunderstone", "Ruby"),
                   ToggleCoords, () => SkCommandProcessor.bCoords);
            Toggle(grid, "Godmode", "Take no damage", SkIcons.First("HelmetOdin", "CapeOdin"),
                   ToggleGodmode, () => Player.m_localPlayer != null && Player.m_localPlayer.InGodMode());
            Toggle(grid, "Flying", "Free flight", SkIcons.First("Feathers"),
                   ToggleFlying, () => Player.m_localPlayer != null && Player.m_localPlayer.IsDebugFlying());
            Toggle(grid, "Infinite Stamina", "Stamina never drains", SkIcons.First("MeadStaminaMedium", "MeadStaminaMinor"),
                   ToggleInfStam, () => SkCommandProcessor.infStamina);

            return grid;
        }

        private static void Action(List<SkMenuController.SkGridItem> grid, string label, string tip, Sprite icon, System.Action run)
        {
            grid.Add(new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                OnClick = (string ignored) => run(),
            });
        }

        private static void Toggle(List<SkMenuController.SkGridItem> grid, string label, string tip, Sprite icon,
                                   System.Action run, Func<bool> isOn)
        {
            grid.Add(new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                OnClick = (string ignored) => run(),
                IsOn = isOn,
            });
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

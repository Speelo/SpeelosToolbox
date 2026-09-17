using SkToolbox.Configuration;
using SkToolbox.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;
using static SkToolbox.Utility.SkUtilities;

namespace SkToolbox
{
    /// <summary>
    /// Speelo's Toolbox: the click-driven on-screen menu (IMGUI). A configurable key (default F6) toggles it.
    /// Categories (modules) sit on the left, the current item list on the right with a submenu stack,
    /// a search box for long lists, and a hover tip at the bottom. While it is open, the Harmony patches in
    /// SkCommandPatcher free the mouse cursor and pause player input.
    /// Public surface kept for the modules: RequestSubMenu(...), UpdateMenuOptions(...), CloseMenu(), logResponse.
    /// </summary>
    public class SkMenuController : MonoBehaviour
    {
        internal static System.Version SkMenuControllerVersion = new System.Version(2, 0, 0); // 09/2026 clickable rewrite
        internal static Status SkMenuControllerStatus = Status.Initialized;

        internal static SkMenuController Instance { get; private set; }

        /// <summary>True while the menu window is on screen. Read every frame by the mouse/input patches in SkCommandPatcher.</summary>
        public static bool IsOpen => Instance != null && Instance.menuOpen;

        private readonly string appName = SkBepInExLoader.DISPLAYNAME;

        private bool initialCheck = true;
        internal bool logResponse = false;
        private bool menuOpen = false;

        public List<SkModules.SkBaseModule> menuOptions;
        public SkModuleController SkModuleController;

        // ---- navigation state ----
        private int selectedModule = -1;
        private readonly List<MenuFrame> frames = new List<MenuFrame>();
        private Action pendingAction; // structural changes are deferred to the end of the IMGUI pass
        private SkMenuSlider pendingSlider; // attached to the next frame RequestSubMenu pushes
        private string hoverTip = "";
        private const int FilterThreshold = 12; // lists longer than this get a search box
        private const float GridCell = 54f;
        private const float GridPad = 4f;

        private class MenuFrame
        {
            public string Title;
            public List<SkMenuItem> Items;
            public Vector2 Scroll;
            public string Filter = "";
            public List<SkMenuSlider> Sliders;
            public List<SkMenuToggle> Toggles;
            public List<SkGridItem> Grid;          // when set, this level draws as an icon grid
            public List<SkGridItem> GridFiltered;  // cached result of Filter, rebuilt only when Filter changes
            public string GridFilterKey;
            public bool ShowFilter = true;         // small grids do not need a search box
        }

        /// <summary>One cell of an icon grid, e.g. an item in the Give tab or an action in the Player tab.</summary>
        /// <summary>
        /// Who a button actually affects once clicked. Valheim splits console commands between things that only
        /// touch your own game and things that write shared world state, and the split is not guessable from the
        /// name, so every cell says which it is on its hover tip.
        /// </summary>
        public enum SkScope { Unset, Client, Server, Admin, HostOnly }

        public class SkGridItem
        {
            public string Name;          // prefab name, passed to OnClick
            public string Display;       // localized label used for search and the tooltip
            public string Tip;
            public Sprite Icon;
            public Action<string> OnClick;

            /// <summary>Optional live predicate. When it returns true the cell is outlined as "on".
            /// Evaluated every frame, so a toggle lights up without rebuilding the grid.</summary>
            public Func<bool> IsOn;

            /// <summary>Optional group heading. Cells sharing one are drawn together inside a labelled box,
            /// in the order the sections first appear.</summary>
            public string Section;

            /// <summary>Who this affects. Appended to the hover tip as a short coloured marker.</summary>
            public SkScope Scope = SkScope.Unset;

            private string hoverCache;

            /// <summary>The tip plus its scope marker. Built once, since tips never change after the cell is made.</summary>
            internal string HoverText
            {
                get
                {
                    if (hoverCache == null)
                    {
                        string body = Tip ?? Display ?? Name ?? "";
                        string mark = ScopeMark(Scope);
                        hoverCache = mark.Length == 0 ? body : body + "\n" + mark;
                    }
                    return hoverCache;
                }
            }

            private static string ScopeMark(SkScope scope)
            {
                switch (scope)
                {
                    case SkScope.Client:
                        return "<color=#8FB8D8>(client)</color>  <color=#9FB6CC>changes nothing for anyone else</color>";
                    case SkScope.Server:
                        return "<color=#E5A959>(server)</color>  <color=#9FB6CC>changes the world for everyone on it</color>";
                    case SkScope.Admin:
                        return "<color=#E5A959>(server)</color>  <color=#9FB6CC>sent to the server, which needs you to be admin</color>";
                    case SkScope.HostOnly:
                        return "<color=#E08585>(host only)</color>  <color=#9FB6CC>does nothing unless you are running the world</color>";
                    default:
                        return "";
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Forms: a modal used by any command that needs parameters before it runs, e.g. picking a skill and a level.
        // Drawn in place of the tab content rather than over it, because IMGUI delivers clicks to whatever was drawn
        // first, so an overlay would let clicks fall through to the grid underneath.
        // ---------------------------------------------------------------------------------------------------------

        public enum SkFieldKind { Choice, IntSlider, Text, Toggle, Info, Checklist }

        public class SkFormField
        {
            public string Id = "";
            public string Label = "";
            public SkFieldKind Kind = SkFieldKind.Text;

            /// <summary>Choice: the values handed back, and the text shown for each. Labels may be left empty.</summary>
            public List<string> Options = new List<string>();
            public List<string> OptionLabels = new List<string>();
            public int Selected = 0;

            /// <summary>Checklist: one flag per option, independently on or off. Grown to match Options when drawn.</summary>
            public List<bool> Checked = new List<bool>();

            public int Min = 0;
            public int Max = 100;
            public int IntValue = 0;
            public string TextValue = "";
            public bool BoolValue = false;

            /// <summary>Choice and Info: height of the scrolling area in pixels. 0 picks a default.</summary>
            public int Height = 0;

            /// <summary>Choice: live search text. The box appears once the list is long enough to need it.</summary>
            internal string Filter = "";

            internal Vector2 Scroll;

            public string SelectedOption
            {
                get { return (Options != null && Selected >= 0 && Selected < Options.Count) ? Options[Selected] : null; }
            }
        }

        public class SkFormAction
        {
            public string Label = "Accept";
            public Action<SkForm> Run;
        }

        public class SkForm
        {
            public string Title = "";
            public string Note = "";
            public List<SkFormField> Fields = new List<SkFormField>();
            public List<SkFormAction> Actions = new List<SkFormAction>();

            /// <summary>
            /// Shown above everything else, in red. For the handful of actions that destroy something outright, so
            /// the consequence is the first thing read rather than a line of grey body text.
            /// </summary>
            public string Warning = "";

            /// <summary>
            /// Optional gate run before any action. Returning a message refuses the action and keeps the form open
            /// with that message shown, which is how the dangerous commands ask for a confirmation tick.
            /// </summary>
            public Func<SkForm, string> Validate;

            internal string Error;

            public SkFormField Field(string id)
            {
                foreach (SkFormField field in Fields)
                {
                    if (field.Id == id) return field;
                }
                return null;
            }
        }

        /// <summary>
        /// A small modal asking for one piece of text, drawn OVER whatever is already on screen rather than
        /// replacing it. Forms replace the tab content because IMGUI hands clicks to whatever was drawn first,
        /// but a prompt can genuinely float: everything underneath is drawn disabled, so it takes no clicks, and
        /// the prompt is drawn last and takes them all.
        /// </summary>
        public class SkPrompt
        {
            public string Title = "";
            public string Label = "";
            public string Accept = "Save";
            public string Value = "";
            public Action<string> OnAccept;
        }

        private SkPrompt activePrompt;
        private bool promptNeedsFocus;
        private const string PromptFieldName = "SkPromptField";

        /// <summary>Opens the modal. It floats over the current tab or form until accepted or cancelled.</summary>
        public void ShowPrompt(SkPrompt prompt)
        {
            if (prompt == null) return;
            activePrompt = prompt;
            promptNeedsFocus = true;
            menuOpen = true;
        }

        public void ClosePrompt()
        {
            activePrompt = null;
            promptNeedsFocus = false;
            GUIUtility.keyboardControl = 0; // let the menu have the keyboard back
        }

        internal bool PromptOpen { get { return activePrompt != null; } }

        private SkForm activeForm;

        /// <summary>Opens a modal form. It replaces the tab content until an action runs or it is cancelled.</summary>
        public void ShowForm(SkForm form)
        {
            if (form == null) return;
            activeForm = form;
            menuOpen = true;
        }

        public void CloseForm()
        {
            activeForm = null;
        }

        /// <summary>A checkbox drawn with the sliders, e.g. "Spawn tamed".</summary>
        public class SkMenuToggle
        {
            public string Label = "Toggle";
            public Func<bool> Get;
            public Action<bool> Set;
        }

        /// <summary>A numeric slider drawn under the search box of a menu level, e.g. the Give Item quantity.</summary>
        public class SkMenuSlider
        {
            public string Label = "Value";
            public int Min = 1;
            public int Max = 100;
            public Func<int> Get;
            public Action<int> Set;
        }

        // ---- window / styles ----
        private const int WindowId = 49000;
        private Rect windowRect = new Rect(24f, 80f, 740f, 580f);
        private bool windowPlaced = false;
        private bool stylesReady = false;
        private float stylesAlpha = -1f;
        private GUIStyle styleWindow, styleItem, styleHeader, styleTip, styleSmall, styleBack, styleFilter;
        private GUIStyle styleTab, styleTabOn, styleClose, styleGridCell, styleSectionBox, styleSectionHeader, styleTooltip, styleError, styleWarning, stylePrompt;
        private static Texture2D texWindow, texPanel, texItem, texItemHover, texItemActive, texAccent, texWhite, texTooltip, texPrompt;

        private static float ConfiguredOpacity =>
            SkConfigEntry.OMenuOpacity == null ? 0.96f : Mathf.Clamp(SkConfigEntry.OMenuOpacity.Value, 0.25f, 1f);

        /// <summary>Speelo's Toolbox: Unity's built-in IMGUI skin is largely see-through, which made the menu hard to
        /// read over bright terrain. Every surface gets an explicit solid texture instead.</summary>
        private static Texture2D MakeTex(Color color)
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            tex.hideFlags = HideFlags.HideAndDontSave; // not tied to a scene, never serialized
            return tex;
        }

        // ---- toggle key ----
        private KeyCode toggleKey = KeyCode.F6;
        internal KeyCode ToggleKey => toggleKey;

        // ZInput maps unknown KeyCodes to Key.None and Unity.InputSystem's Keyboard[Key.None] throws, so a bad config
        // binding is logged once and ignored instead of throwing every frame.
        private static readonly HashSet<KeyCode> s_unmappedKeys = new HashSet<KeyCode>();
        private static bool KeyDown(KeyCode key)
        {
            if (s_unmappedKeys.Contains(key)) return false;
            // Speelo's Toolbox: bypass this mod's own input block (SkCommandPatcher.PatchMenuKeyDown) so the menu can
            // always be closed again, even though every other key is swallowed while it is open.
            SkCommandPatcher.BypassInputBlock = true;
            try { return ZInput.GetKeyDown(key, false); }
            catch (ArgumentOutOfRangeException)
            {
                s_unmappedKeys.Add(key);
                SkUtilities.Logz(new string[] { "CONTROLLER", "WARN" }, new string[] { "KeyCode " + key + " has no Input System mapping in ZInput; bind ignored." });
                return false;
            }
            finally { SkCommandPatcher.BypassInputBlock = false; }
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ReleaseTextures();
                stylesReady = false;
            }
        }

        void Start()
        {
            SkMenuControllerStatus = Status.Loading;
            SkUtilities.Logz(new string[] { "CONTROLLER", "NOTIFY" }, new string[] { "LOADING...", "WAITING FOR TOOLBOX." });
            SkModuleController = gameObject.AddComponent<SkModuleController>(); // Load our module controller
            ApplyToggleKey();
        }

        /// <summary>Reads [4 - OnScreenMenu] MenuToggleKey (a UnityEngine.KeyCode name). Falls back to F6.</summary>
        internal void ApplyToggleKey()
        {
            string s = SkConfigEntry.OMenuToggleKey?.Value;
            if (!string.IsNullOrWhiteSpace(s)
                && Enum.TryParse<KeyCode>(s.Trim(), true, out KeyCode k)
                && Enum.IsDefined(typeof(KeyCode), k)
                && k != KeyCode.None)
            {
                toggleKey = k;
                return;
            }
            if (!string.IsNullOrWhiteSpace(s))
            {
                SkUtilities.Logz(new string[] { "CONTROLLER", "CONFIG" }, new string[] { "Invalid MenuToggleKey '" + s + "' in config, using F6" }, LogType.Warning);
            }
            toggleKey = KeyCode.F6;
        }

        /// <summary>
        /// One sample per frame of "can the player walk" and "is the look key down", read by the input and cursor
        /// patches. Taken here rather than in each patch because Harmony does not order patch classes, so two of them
        /// polling the key separately could disagree within a frame and leave the cursor and the camera out of step.
        /// </summary>
        private void SampleInputState()
        {
            if (!menuOpen)
            {
                // Nothing to sample while the menu is shut - and nothing safe to read, either. GUIUtility.keyboardControl
                // below is a native call into Unity's IMGUI state, which does not exist on a headless server, so reading
                // it there is an access violation rather than an exception. The && chain does not save us: `!held` is
                // true whenever the look key is up, so the read happened on every single frame.
                SkCommandPatcher.LookHeld = false;
                SkCommandPatcher.WalkAllowed = false;
                return;
            }

            bool enabled = menuOpen
                && !ZInput.IsGamepadActive() // a gamepad cannot look at all in this mode, so keep the old full block
                && (Configuration.SkConfigEntry.OMenuWalk == null || Configuration.SkConfigEntry.OMenuWalk.Value);

            bool held = enabled && ZInput.GetKey(LookKey, false);

            // A press of the look key that landed on a search box will have focused it, and a focused field stops
            // movement. Clearing on the release edge means a look never leaves the player unable to walk.
            if (!held && SkCommandPatcher.LookHeld)
            {
                GUIUtility.keyboardControl = 0;
            }

            SkCommandPatcher.LookHeld = held;

            // Typing must not drive the character. While the cursor is captured for a look the player demonstrably
            // is not typing, so focus is ignored then.
            bool typing = !held && GUIUtility.keyboardControl != 0;
            SkCommandPatcher.WalkAllowed = enabled && !typing;

            if (enabled)
            {
                // Valheim's own "move but do nothing else" state: with this armed, PlayerController passes the move
                // vector through and forces attack, block, jump and crouch to false. FixedUpdate decays it, so it is
                // re-armed every frame. The tail that outlives closing the menu is what stops a click landing as a
                // swing the instant the menu goes away.
                PlayerController.SetTakeInputDelay(0.25f);
            }
        }

        /// <summary>The configured hold-to-look key, falling back to the right mouse button.</summary>
        private static KeyCode LookKey
        {
            get
            {
                string name = Configuration.SkConfigEntry.OMenuLookKey != null
                    ? Configuration.SkConfigEntry.OMenuLookKey.Value
                    : null;
                if (string.IsNullOrEmpty(name)) return KeyCode.Mouse1;
                try { return (KeyCode)Enum.Parse(typeof(KeyCode), name, true); }
                catch (Exception) { return KeyCode.Mouse1; }
            }
        }

        void Update()
        {
            SampleInputState(); // before every early return below, so the flags are never left stale

            if (initialCheck) // It takes a frame to load the components. Attempt to load menu options in second frame.
            {
                if (menuOptions == null || menuOptions.Count == 0)
                {
                    UpdateMenuOptions(SkModuleController.GetOptions());
                }
                else
                {
                    SkMenuControllerStatus = Status.Ready;
                    if (SkModuleController.SkMainStatus == Status.Ready)
                    {
                        initialCheck = false;
                        SkUtilities.Logz(new string[] { "CONTROLLER", "NOTIFY" }, new string[] { "READY. Press " + toggleKey + " to open the menu." });
                    }
                }
            }

            // Don't steal the toggle key while a vanilla text field owns the keyboard.
            if (global::Console.IsVisible()
                || (Chat.instance != null && Chat.instance.HasFocus())
                || TextInput.IsVisible()
                || Minimap.InTextInput())
            {
                return;
            }

            if (KeyDown(toggleKey))
            {
                if (menuOpen) CloseMenu(); else OpenMenu();
            }
            // Speelo's Toolbox: Escape closes this menu instead of stacking the vanilla pause menu on top of it,
            // but a prompt gets first refusal - it is the innermost thing on screen.
            else if (menuOpen && KeyDown(KeyCode.Escape))
            {
                if (activePrompt != null) ClosePrompt(); else CloseMenu();
            }
        }

        public void OpenMenu()
        {
            if (menuOptions == null || menuOptions.Count == 0) return;
            // Speelo's Toolbox: the cursor/input patches live in SkCommandPatcher, which is normally applied by
            // SkCommandProcessor.Announce(). Applying here too (idempotent) guarantees they exist before the first
            // frame the menu is interactive, otherwise the cursor would stay locked to the camera.
            SkCommandPatcher.InitPatch();
            menuOpen = true;
            if (selectedModule < 0 || selectedModule >= menuOptions.Count || frames.Count == 0)
            {
                SelectModule(0);
            }
            else
            {
                RefreshRootFrame();
            }
        }

        public void CloseMenu()
        {
            menuOpen = false;
            activeForm = null;
            activePrompt = null;
        }

        // ------------------------------------------------------------------ navigation

        private void SelectModule(int index)
        {
            if (menuOptions == null || index < 0 || index >= menuOptions.Count) return;
            selectedModule = index;
            frames.Clear();
            SkModules.SkBaseModule module = menuOptions[index];
            // The CallerEntry action rebuilds the module's menu where needed and calls RequestSubMenu, which pushes the root frame.
            try
            {
                module.CallerEntry?.ItemClass?.Invoke();
            }
            catch (Exception ex)
            {
                SkUtilities.Logz(new string[] { "CONTROLLER", "ERROR" }, new string[] { ex.Message });
            }
            if (frames.Count == 0)
            {
                frames.Add(new MenuFrame { Title = ModuleTitle(module), Items = module.FlushMenu() ?? new List<SkMenuItem>() });
            }
            else
            {
                frames[0].Title = ModuleTitle(module);
            }
        }

        /// <summary>Re-reads the selected module's current item list into the root frame so toggle labels ([ON]/[OFF], radius numbers) stay current.</summary>
        private void RefreshRootFrame()
        {
            if (frames.Count == 0 || selectedModule < 0 || menuOptions == null || selectedModule >= menuOptions.Count) return;
            List<SkMenuItem> items = menuOptions[selectedModule].FlushMenu();
            if (items != null && items.Count > 0) frames[0].Items = items;
        }

        private void PopFrame()
        {
            if (frames.Count > 1) frames.RemoveAt(frames.Count - 1);
            if (frames.Count == 1) RefreshRootFrame();
        }

        private void InvokeItem(SkMenuItem item)
        {
            if (item == null) return;
            bool opensSubmenu = item.ItemText != null && item.ItemText.Contains("►");
            int depthBefore = frames.Count;

            // Speelo's Toolbox: mirror whatever the action prints to the on-screen message area, because the console it
            // normally prints to is closed while this menu is up.
            SkCommandProcessor.MenuFeedbackActive = true;
            SkCommandProcessor.MenuFeedbackShown = false;
            try
            {
                if (item.ItemClass != null)
                {
                    item.ItemClass.Invoke();
                }
                else if (item.ItemClassStr != null)
                {
                    item.ItemClassStr.Invoke(item.ItemText); // upstream contract: the raw item text is the argument (prefab name, number...)
                }
            }
            catch (Exception ex)
            {
                SkUtilities.Logz(new string[] { "CONTROLLER", "ERROR" }, new string[] { "Error running menu item '" + item.ItemText + "': " + ex.Message }, LogType.Error);
                SkCommandProcessor.Notify("That failed: " + ex.Message);
            }
            finally
            {
                SkCommandProcessor.MenuFeedbackActive = false;
            }

            if (frames.Count == 1) RefreshRootFrame();

            // Nothing printed and no submenu appeared: acknowledge the click with the item's own (refreshed) label,
            // so silent toggles like Godmode still show their new state.
            if (!SkCommandProcessor.MenuFeedbackShown && !opensSubmenu && frames.Count == depthBefore)
            {
                SkCommandProcessor.Notify(CurrentLabelFor(item));
            }
        }

        // ------------------------------------------------------------------ layout sections

        /// <summary>Close box in the title bar's top-right corner. Absolute rect so it sits in the title strip.</summary>
        private void DrawCloseButton()
        {
            if (GUI.Button(new Rect(windowRect.width - 30f, 5f, 24f, 21f), "X", styleClose))
            {
                pendingAction = CloseMenu;
            }
        }

        /// <summary>Module tabs across the top, sharing the row evenly.</summary>
        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < menuOptions.Count; i++)
            {
                SkModules.SkBaseModule m = menuOptions[i];
                if (m == null) continue;
                bool on = i == selectedModule;
                if (GUILayout.Button(new GUIContent(ModuleTitle(m), m.CallerEntry?.ItemTip ?? ""),
                                     on ? styleTabOn : styleTab, GUILayout.ExpandWidth(true)) && !on)
                {
                    int captured = i;
                    pendingAction = () => SelectModule(captured);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        /// <summary>Back button plus the current level's title. Hidden at the top level of a tab.</summary>
        private void DrawFrameHeader()
        {
            if (frames.Count <= 1) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Back", styleBack, GUILayout.Width(80f)))
            {
                pendingAction = PopFrame;
            }
            GUILayout.Space(8f);
            GUILayout.Label(frames[frames.Count - 1].Title, styleHeader);
            GUILayout.EndHorizontal();
        }

        private bool CurrentFrameIsFilterable()
        {
            if (frames.Count == 0) return false;
            MenuFrame frame = frames[frames.Count - 1];
            if (frame.Grid != null) return frame.ShowFilter && frame.Grid.Count > FilterThreshold;
            return frame.Items != null && frame.Items.Count > FilterThreshold;
        }

        /// <summary>Search box, shown only for long lists.</summary>
        private void DrawFilterRow()
        {
            if (!CurrentFrameIsFilterable()) return;
            MenuFrame frame = frames[frames.Count - 1];
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", styleSmall, GUILayout.Width(52f));
            frame.Filter = GUILayout.TextField(frame.Filter ?? "", styleFilter);
            if (GUILayout.Button("x", styleBack, GUILayout.Width(28f))) frame.Filter = "";
            GUILayout.EndHorizontal();
        }

        /// <summary>Numeric sliders for this level (Give quantity, terrain radius and height), under the search box.</summary>
        private void DrawSliderRow()
        {
            if (frames.Count == 0) return;
            List<SkMenuSlider> sliders = frames[frames.Count - 1].Sliders;
            if (sliders == null) return;

            foreach (SkMenuSlider sl in sliders)
            {
                if (sl == null || sl.Get == null || sl.Set == null) continue;

                int current = Mathf.Clamp(sl.Get(), sl.Min, sl.Max);
                int updated = current;
                GUILayout.BeginHorizontal();
                GUILayout.Label(sl.Label + ": " + current, styleSmall, GUILayout.Width(110f));
                if (GUILayout.Button("-", styleBack, GUILayout.Width(28f))) updated = current - 1;
                float raw = GUILayout.HorizontalSlider(current, sl.Min, sl.Max, GUILayout.MinWidth(120f));
                int rounded = Mathf.RoundToInt(raw);
                if (rounded != current) updated = rounded;
                if (GUILayout.Button("+", styleBack, GUILayout.Width(28f))) updated = current + 1;
                GUILayout.EndHorizontal();

                updated = Mathf.Clamp(updated, sl.Min, sl.Max);
                if (updated != current) sl.Set(updated);
            }

            List<SkMenuToggle> toggles = frames[frames.Count - 1].Toggles;
            if (toggles != null)
            {
                foreach (SkMenuToggle tg in toggles)
                {
                    if (tg == null || tg.Get == null || tg.Set == null) continue;
                    bool was = tg.Get();
                    // Drawn as a button with explicit state rather than GUILayout.Toggle: a Toggle needs a style
                    // carrying checkbox art for its on and off states, and this menu's flat styles have none, so
                    // the two states looked identical. This also matches the [ON]/[OFF] used elsewhere.
                    GUILayout.BeginHorizontal();
                    string label = tg.Label + "   " + (was ? "<color=#7CFC00>[ON]</color>" : "<color=#FF8080>[OFF]</color>");
                    bool clicked = GUILayout.Button(new GUIContent(label, tg.Label), styleBack, GUILayout.Width(190f));
                    if (Event.current.type == EventType.Repaint && was)
                    {
                        DrawOutline(GUILayoutUtility.GetLastRect(), OnColor, 2f);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    if (clicked) tg.Set(!was);
                }
            }
        }

        /// <summary>The scrolling list of the current level, honouring the search box.</summary>
        private void DrawItemList()
        {
            if (frames.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Pick a tab above.", styleTip);
                GUILayout.FlexibleSpace();
                return;
            }

            MenuFrame frame = frames[frames.Count - 1];
            if (frame.Grid != null)
            {
                DrawGrid(frame);
                return;
            }

            frame.Scroll = GUILayout.BeginScrollView(frame.Scroll, false, true);
            string filter = CurrentFrameIsFilterable() ? (frame.Filter ?? "").Trim() : "";
            int shown = 0;
            if (frame.Items != null)
            {
                for (int i = 0; i < frame.Items.Count; i++)
                {
                    SkMenuItem item = frame.Items[i];
                    if (item == null || string.IsNullOrEmpty(item.ItemText)) continue;
                    if (filter.Length > 0 && item.ItemText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    shown++;
                    // An icon sits in the row's left gutter, so indent the label to make room for it.
                    string label = Display(item.ItemText);
                    if (item.Icon != null) label = "        " + label;
                    if (GUILayout.Button(new GUIContent(label, item.ItemTip ?? ""), styleItem))
                    {
                        SkMenuItem captured = item;
                        pendingAction = () => InvokeItem(captured);
                    }
                    if (item.Icon != null && Event.current.type == EventType.Repaint)
                    {
                        Rect row = GUILayoutUtility.GetLastRect();
                        DrawSprite(new Rect(row.x + 3f, row.y, row.height, row.height), item.Icon, 2f);
                    }
                }
            }
            if (shown == 0) GUILayout.Label("No matches.", styleTip);
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Icon grid. Only the rows inside the viewport are drawn: the Give tab holds hundreds of items and
        /// drawing every cell each frame would cost far more than the handful actually on screen.
        /// </summary>
        private void DrawGrid(MenuFrame frame)
        {
            string filter = (frame.Filter ?? "").Trim();
            if (frame.GridFiltered == null || frame.GridFilterKey != filter)
            {
                frame.GridFilterKey = filter;
                if (filter.Length == 0)
                {
                    frame.GridFiltered = frame.Grid;
                }
                else
                {
                    List<SkGridItem> matches = new List<SkGridItem>();
                    foreach (SkGridItem candidate in frame.Grid)
                    {
                        if (candidate == null) continue;
                        if ((candidate.Display != null && candidate.Display.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            || (candidate.Name != null && candidate.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            matches.Add(candidate);
                        }
                    }
                    frame.GridFiltered = matches;
                }
            }

            List<SkGridItem> shown = frame.GridFiltered;
            float available = Mathf.Max(80f, windowRect.width - 42f); // window padding + scrollbar
            float step = GridCell + GridPad;
            int columns = Mathf.Max(1, Mathf.FloorToInt(available / step));
            int rows = Mathf.CeilToInt(shown.Count / (float)columns);

            // A grid whose cells carry section names is drawn as labelled boxes instead. These grids are small
            // (the Player tab), so they skip the row virtualisation the big item and creature grids need.
            bool sectioned = false;
            foreach (SkGridItem probe in shown)
            {
                if (probe != null && !string.IsNullOrEmpty(probe.Section)) { sectioned = true; break; }
            }
            if (sectioned)
            {
                DrawSectionedGrid(frame, shown, columns);
                return;
            }

            frame.Scroll = GUILayout.BeginScrollView(frame.Scroll, false, true);

            if (shown.Count == 0)
            {
                GUILayout.Label("No matches.", styleTip);
                GUILayout.EndScrollView();
                return;
            }

            float viewHeight = Mathf.Max(120f, windowRect.height - 240f);
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(frame.Scroll.y / step) - 1);
            int lastRow = Mathf.Min(rows - 1, Mathf.CeilToInt((frame.Scroll.y + viewHeight) / step) + 1);

            if (firstRow > 0) GUILayout.Space(firstRow * step);

            for (int row = firstRow; row <= lastRow; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (index >= shown.Count)
                    {
                        GUILayout.Space(step);
                        continue;
                    }
                    SkGridItem cell = shown[index];
                    string caption = cell.Icon == null ? ShortLabel(cell.Display ?? cell.Name) : "";
                    if (GUILayout.Button(new GUIContent(caption, cell.HoverText),
                                         styleGridCell, GUILayout.Width(GridCell), GUILayout.Height(GridCell)))
                    {
                        SkGridItem captured = cell;
                        pendingAction = () => InvokeItem(new SkMenuItem(captured.Name, captured.OnClick, captured.Tip));
                    }
                    if (Event.current.type == EventType.Repaint)
                    {
                        Rect cellRect = GUILayoutUtility.GetLastRect();
                        if (cell.Icon != null)
                        {
                            DrawSprite(cellRect, cell.Icon);
                        }
                        // A toggle that is currently on gets a bright border, so state is readable at a glance.
                        if (cell.IsOn != null && cell.IsOn())
                        {
                            DrawOutline(cellRect, OnColor, 2f);
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (lastRow < rows - 1) GUILayout.Space((rows - 1 - lastRow) * step);

            GUILayout.EndScrollView();
        }

        /// <summary>Grid split into labelled boxes, one per section, in the order the sections first appear.</summary>
        private void DrawSectionedGrid(MenuFrame frame, List<SkGridItem> shown, int columns)
        {
            List<string> order = new List<string>();
            Dictionary<string, List<SkGridItem>> groups = new Dictionary<string, List<SkGridItem>>();
            foreach (SkGridItem cell in shown)
            {
                if (cell == null) continue;
                string section = string.IsNullOrEmpty(cell.Section) ? "Other" : cell.Section;
                if (!groups.ContainsKey(section))
                {
                    groups[section] = new List<SkGridItem>();
                    order.Add(section);
                }
                groups[section].Add(cell);
            }

            frame.Scroll = GUILayout.BeginScrollView(frame.Scroll, false, true);
            foreach (string section in order)
            {
                List<SkGridItem> cells = groups[section];
                GUILayout.BeginVertical(styleSectionBox);
                GUILayout.Label(section, styleSectionHeader);

                for (int i = 0; i < cells.Count; i += columns)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < columns; c++)
                    {
                        int index = i + c;
                        if (index >= cells.Count)
                        {
                            GUILayout.Space(GridCell + GridPad);
                            continue;
                        }
                        DrawGridCell(cells[index]);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        /// <summary>One grid cell: the button, its icon, and the "on" outline for toggles.</summary>
        private void DrawGridCell(SkGridItem cell)
        {
            string caption = cell.Icon == null ? ShortLabel(cell.Display ?? cell.Name) : "";
            if (GUILayout.Button(new GUIContent(caption, cell.HoverText),
                                 styleGridCell, GUILayout.Width(GridCell), GUILayout.Height(GridCell)))
            {
                SkGridItem captured = cell;
                pendingAction = () => InvokeItem(new SkMenuItem(captured.Name, captured.OnClick, captured.Tip));
            }
            if (Event.current.type == EventType.Repaint)
            {
                Rect cellRect = GUILayoutUtility.GetLastRect();
                if (cell.Icon != null)
                {
                    DrawSprite(cellRect, cell.Icon);
                }
                if (cell.IsOn != null && cell.IsOn())
                {
                    DrawOutline(cellRect, OnColor, 2f);
                }
            }
        }

        /// <summary>Draws a sprite inside a rect, honouring its atlas rect so packed sprites are not smeared.</summary>
        private static void DrawSprite(Rect area, Sprite sprite, float pad = 5f)
        {
            if (sprite == null || sprite.texture == null || area.width <= 0f) return;
            Rect source = sprite.textureRect;
            Rect coords = new Rect(source.x / sprite.texture.width,
                                   source.y / sprite.texture.height,
                                   source.width / sprite.texture.width,
                                   source.height / sprite.texture.height);
            Rect inner = new Rect(area.x + pad, area.y + pad, area.width - pad * 2f, area.height - pad * 2f);
            GUI.DrawTextureWithTexCoords(inner, sprite.texture, coords, true);
        }

        private static readonly Color OnColor = new Color(0.48f, 0.95f, 0.48f);

        /// <summary>Draws a hollow rectangle by stretching the shared white pixel along each edge.</summary>
        private static void DrawOutline(Rect area, Color color, float thickness)
        {
            if (texWhite == null) return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, thickness), texWhite);
            GUI.DrawTexture(new Rect(area.x, area.yMax - thickness, area.width, thickness), texWhite);
            GUI.DrawTexture(new Rect(area.x, area.y, thickness, area.height), texWhite);
            GUI.DrawTexture(new Rect(area.xMax - thickness, area.y, thickness, area.height), texWhite);
            GUI.color = previous;
        }

        private static string ShortLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return "?";
            return text.Length <= 7 ? text : text.Substring(0, 7);
        }

        /// <summary>
        /// The floating prompt. Centred, opaque, outlined, and drawn after everything else so it paints on top.
        /// Enter accepts and Escape cancels, which is what anyone typing a name will try first.
        /// </summary>
        private void DrawPrompt()
        {
            SkPrompt prompt = activePrompt;
            float width = Mathf.Min(380f, Mathf.Max(220f, windowRect.width - 80f));
            float height = 152f;
            Rect area = new Rect((windowRect.width - width) / 2f, (windowRect.height - height) / 2f, width, height);

            GUI.Box(area, GUIContent.none, stylePrompt);
            DrawOutline(area, new Color(0.42f, 0.54f, 0.70f), 2f);

            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, area.width - 28f, area.height - 24f));
            GUILayout.Label(prompt.Title, styleHeader);
            if (!string.IsNullOrEmpty(prompt.Label))
            {
                GUILayout.Label(prompt.Label, styleSmall);
            }
            GUILayout.Space(4f);

            GUI.SetNextControlName(PromptFieldName);
            prompt.Value = GUILayout.TextField(prompt.Value ?? "", styleFilter);
            if (promptNeedsFocus && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(PromptFieldName); // so the player can just start typing
                promptNeedsFocus = false;
            }

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            bool accept = GUILayout.Button(prompt.Accept, styleItem, GUILayout.Height(27f));
            GUILayout.Space(8f);
            bool cancel = GUILayout.Button("Cancel", styleBack, GUILayout.Height(27f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Keyboard shortcuts, read before the buttons so a held Return does not double-fire.
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { accept = true; e.Use(); }
                else if (e.keyCode == KeyCode.Escape) { cancel = true; e.Use(); }
            }

            if (accept)
            {
                SkPrompt captured = prompt;
                string typed = (prompt.Value ?? "").Trim();
                pendingAction = () =>
                {
                    ClosePrompt();
                    if (captured.OnAccept != null)
                    {
                        try { captured.OnAccept(typed); }
                        catch (Exception ex)
                        {
                            SkUtilities.Logz(new string[] { "PROMPT", "ERROR" }, new string[] { ex.Message }, LogType.Error);
                            SkCommandProcessor.Notify("That failed: " + ex.Message);
                        }
                    }
                };
            }
            else if (cancel)
            {
                pendingAction = ClosePrompt;
            }
        }

        /// <summary>Renders the active form: title, one block per field, then its action buttons.</summary>
        private void DrawForm()
        {
            SkForm form = activeForm;

            // Asked every frame rather than only on a click, so an action that cannot run yet is visibly
            // unavailable instead of silently refusing when pressed.
            string blockedBecause = null;
            if (form.Validate != null)
            {
                try { blockedBecause = form.Validate(form); } catch (Exception) { blockedBecause = null; }
            }

            GUILayout.Label(form.Title, styleHeader);
            if (!string.IsNullOrEmpty(form.Warning))
            {
                GUILayout.Space(2f);
                GUILayout.Label(form.Warning, styleWarning);
                GUILayout.Space(2f);
            }
            if (!string.IsNullOrEmpty(form.Note))
            {
                GUILayout.Label(form.Note, styleTip);
            }
            string problem = !string.IsNullOrEmpty(form.Error) ? form.Error : blockedBecause;
            if (!string.IsNullOrEmpty(problem))
            {
                GUILayout.Label(problem, styleError);
            }
            GUILayout.Space(6f);

            foreach (SkFormField field in form.Fields)
            {
                if (field == null) continue;
                GUILayout.BeginVertical(styleSectionBox);
                GUILayout.Label(field.Label, styleSectionHeader);

                switch (field.Kind)
                {
                    case SkFieldKind.Choice:
                        DrawChoiceField(field);
                        break;

                    case SkFieldKind.IntSlider:
                    {
                        int current = Mathf.Clamp(field.IntValue, field.Min, field.Max);
                        int updated = current;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(current.ToString(), styleSmall, GUILayout.Width(42f));
                        if (GUILayout.Button("-", styleBack, GUILayout.Width(28f))) updated = current - 1;
                        int rounded = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, field.Min, field.Max, GUILayout.MinWidth(140f)));
                        if (rounded != current) updated = rounded;
                        if (GUILayout.Button("+", styleBack, GUILayout.Width(28f))) updated = current + 1;
                        GUILayout.EndHorizontal();
                        field.IntValue = Mathf.Clamp(updated, field.Min, field.Max);
                        break;
                    }

                    case SkFieldKind.Text:
                        field.TextValue = GUILayout.TextField(field.TextValue ?? "", styleFilter);
                        break;

                    case SkFieldKind.Info:
                        DrawInfoField(field);
                        break;

                    case SkFieldKind.Checklist:
                        DrawChecklistField(field);
                        break;

                    case SkFieldKind.Toggle:
                    {
                        string label = (field.BoolValue ? "<color=#7CFC00>[ON]</color>" : "<color=#FF8080>[OFF]</color>");
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(label, styleBack, GUILayout.Width(90f))) field.BoolValue = !field.BoolValue;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            bool formWasEnabled = GUI.enabled;
            if (!string.IsNullOrEmpty(blockedBecause)) GUI.enabled = false;
            foreach (SkFormAction action in form.Actions)
            {
                if (action == null || action.Run == null) continue;
                if (GUILayout.Button(action.Label, styleItem, GUILayout.Width(150f), GUILayout.Height(28f)))
                {
                    SkFormAction captured = action;
                    SkForm capturedForm = form;
                    pendingAction = () =>
                    {
                        if (capturedForm.Validate != null)
                        {
                            string refused = capturedForm.Validate(capturedForm);
                            if (!string.IsNullOrEmpty(refused))
                            {
                                capturedForm.Error = refused; // stay open so the field can be corrected
                                return;
                            }
                        }
                        capturedForm.Error = null;
                        try { captured.Run(capturedForm); }
                        catch (Exception ex)
                        {
                            SkUtilities.Logz(new string[] { "FORM", "ERROR" }, new string[] { ex.Message }, LogType.Error);
                            SkCommandProcessor.Notify("That failed: " + ex.Message);
                        }
                        CloseForm();
                    };
                }
                GUILayout.Space(6f);
            }
            GUI.enabled = formWasEnabled; // Cancel is never gated
            GUILayout.FlexibleSpace();
            // A form with no actions is a readout, so the only way out reads as Close rather than Cancel.
            if (GUILayout.Button(form.Actions.Count == 0 ? "Close" : "Cancel", styleBack, GUILayout.Width(110f), GUILayout.Height(28f)))
            {
                pendingAction = CloseForm;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// A scrolling list of options, the chosen one highlighted. Long lists get their own search box; it hides
        /// rows rather than renumbering them, so Selected stays an index into Options.
        /// </summary>
        private void DrawChoiceField(SkFormField field)
        {
            if (field.Options.Count > FilterThreshold)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search", styleSmall, GUILayout.Width(52f));
                field.Filter = GUILayout.TextField(field.Filter ?? "", styleFilter);
                if (GUILayout.Button("x", styleBack, GUILayout.Width(28f))) field.Filter = "";
                GUILayout.EndHorizontal();
            }

            string needle = (field.Filter ?? "").Trim();
            field.Scroll = GUILayout.BeginScrollView(field.Scroll, false, true, GUILayout.Height(field.Height > 0 ? field.Height : 196));
            int shown = 0;
            for (int i = 0; i < field.Options.Count; i++)
            {
                string label = (field.OptionLabels != null && i < field.OptionLabels.Count && !string.IsNullOrEmpty(field.OptionLabels[i]))
                    ? field.OptionLabels[i]
                    : field.Options[i];
                if (needle.Length > 0
                    && field.Options[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0
                    && label.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                shown++;
                bool chosen = i == field.Selected;
                if (GUILayout.Button(label, chosen ? styleTabOn : styleItem))
                {
                    field.Selected = i;
                }
            }
            if (shown == 0) GUILayout.Label("Nothing matches that.", styleTip);
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// A scrolling list where every row is independently on or off, for the cases a single Choice cannot express.
        /// Same search box as Choice once the list is long enough to need one.
        /// </summary>
        private void DrawChecklistField(SkFormField field)
        {
            while (field.Checked.Count < field.Options.Count) field.Checked.Add(false);

            if (field.Options.Count > FilterThreshold)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Search", styleSmall, GUILayout.Width(52f));
                field.Filter = GUILayout.TextField(field.Filter ?? "", styleFilter);
                if (GUILayout.Button("x", styleBack, GUILayout.Width(28f))) field.Filter = "";
                GUILayout.EndHorizontal();
            }

            string needle = (field.Filter ?? "").Trim();
            field.Scroll = GUILayout.BeginScrollView(field.Scroll, false, true, GUILayout.Height(field.Height > 0 ? field.Height : 196));
            int shown = 0;
            for (int i = 0; i < field.Options.Count; i++)
            {
                string label = (field.OptionLabels != null && i < field.OptionLabels.Count && !string.IsNullOrEmpty(field.OptionLabels[i]))
                    ? field.OptionLabels[i]
                    : field.Options[i];
                if (needle.Length > 0
                    && field.Options[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0
                    && label.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                shown++;
                bool on = field.Checked[i];
                string mark = on ? "<color=#7CFC00>[ON]</color>  " : "<color=#7A8796>[OFF]</color>  ";
                if (GUILayout.Button(mark + label, on ? styleTabOn : styleItem))
                {
                    field.Checked[i] = !on;
                }
            }
            if (shown == 0) GUILayout.Label("Nothing matches that.", styleTip);
            GUILayout.EndScrollView();
        }

        /// <summary>Read-only panel, used to show what a console command printed without opening the console.</summary>
        private void DrawInfoField(SkFormField field)
        {
            field.Scroll = GUILayout.BeginScrollView(field.Scroll, false, true, GUILayout.Height(field.Height > 0 ? field.Height : 260));
            GUILayout.Label(field.TextValue ?? "", styleTip);
            GUILayout.EndScrollView();
        }

        /// <summary>Toggle-key reminder. Hover text moved to the cursor, see DrawCursorTooltip.</summary>
        private void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(toggleKey + " opens / closes, Escape closes", styleSmall, GUILayout.Height(18f));
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Hover text as a floating box next to the cursor. GUI.tooltip holds whatever control the mouse is over,
        /// but it is only meaningful during the repaint pass, and it has to be drawn after everything else or the
        /// controls below would paint over it. Flips to the other side of the cursor near an edge so it never
        /// spills outside the window, where it would be clipped.
        /// </summary>
        private void DrawCursorTooltip()
        {
            if (Event.current.type != EventType.Repaint) return;
            hoverTip = GUI.tooltip ?? "";
            if (string.IsNullOrEmpty(hoverTip)) return;

            GUIContent content = new GUIContent(hoverTip);
            float maxWidth = Mathf.Min(320f, Mathf.Max(120f, windowRect.width - 32f));
            float width = Mathf.Min(maxWidth, styleTooltip.CalcSize(content).x);
            float height = styleTooltip.CalcHeight(content, width);

            Vector2 mouse = Event.current.mousePosition;
            float x = mouse.x + 16f;
            float y = mouse.y + 18f;
            if (x + width > windowRect.width - 6f) x = mouse.x - width - 10f;
            if (y + height > windowRect.height - 6f) y = mouse.y - height - 10f;
            x = Mathf.Max(6f, x);
            y = Mathf.Max(6f, y);

            Rect area = new Rect(x, y, width, height);
            GUI.Label(area, content, styleTooltip);
            DrawOutline(area, new Color(0.35f, 0.42f, 0.55f), 1f);
        }

        /// <summary>Finds the item's current text after a refresh, so a toggle reports its new [ON]/[OFF] state.</summary>
        private string CurrentLabelFor(SkMenuItem item)
        {
            string wanted = ToggleFree(item.ItemText);
            if (frames.Count > 0 && frames[frames.Count - 1].Items != null)
            {
                foreach (SkMenuItem candidate in frames[frames.Count - 1].Items)
                {
                    if (candidate != null && ToggleFree(candidate.ItemText) == wanted)
                    {
                        return CleanText(candidate.ItemText);
                    }
                }
            }
            return CleanText(item.ItemText);
        }

        /// <summary>
        /// Called by modules (SkBaseModule.RequestMenu) to show a list. Pushes a new level, unless the list is the same
        /// one refreshed (same texts ignoring [ON]/[OFF]), in which case it is replaced in place.
        /// refreshTime / subWidth are accepted for source compatibility and ignored.
        /// </summary>
        /// <summary>Pushes a level that renders as an icon grid instead of a list.</summary>
        public void RequestGridMenu(List<SkGridItem> grid, SkMenuSlider slider = null, string title = "Items", bool showFilter = true)
        {
            RequestGridMenu(grid, slider == null ? null : new List<SkMenuSlider> { slider }, title, showFilter);
        }

        /// <summary>Grid level with any number of sliders and toggles stacked above it.</summary>
        public void RequestGridMenu(List<SkGridItem> grid, List<SkMenuSlider> sliders, string title = "Items", bool showFilter = true, List<SkMenuToggle> toggles = null)
        {
            if (grid == null || grid.Count == 0) return;
            frames.Add(new MenuFrame { Title = title, Items = null, Grid = grid, Sliders = sliders, ShowFilter = showFilter, Toggles = toggles });
            pendingSlider = null;
            menuOpen = true;
        }

        /// <summary>Pushes a list and attaches a slider to that level (drawn under the search box).</summary>
        public void RequestSubMenu(List<SkMenuItem> subMenuOptions, SkMenuSlider slider)
        {
            pendingSlider = slider;
            RequestSubMenu(subMenuOptions);
        }

        public void RequestSubMenu(List<SkMenuItem> subMenuOptions, float refreshTime = 0, int subWidth = 0)
        {
            if (subMenuOptions == null || subMenuOptions.Count == 0) { pendingSlider = null; return; }
            if (frames.Count > 0 && SameMenu(frames[frames.Count - 1].Items, subMenuOptions))
            {
                frames[frames.Count - 1].Items = subMenuOptions;
                if (pendingSlider != null) frames[frames.Count - 1].Sliders = new List<SkMenuSlider> { pendingSlider };
            }
            else
            {
                string title = frames.Count == 0
                    ? (selectedModule >= 0 && menuOptions != null && selectedModule < menuOptions.Count ? ModuleTitle(menuOptions[selectedModule]) : appName)
                    : "Submenu";
                frames.Add(new MenuFrame { Title = title, Items = subMenuOptions, Sliders = pendingSlider == null ? null : new List<SkMenuSlider> { pendingSlider } });
            }
            pendingSlider = null;
            menuOpen = true;
            if (logResponse) SkUtilities.Logz(new string[] { "CONTROLLER", "RESP" }, new string[] { "Submenu created." });
        }

        public void RequestSubMenu(SkMenu subMenuOptions, float refreshTime = 0, int subWidth = 0)
        {
            if (subMenuOptions != null)
            {
                RequestSubMenu(subMenuOptions.FlushMenu(), refreshTime, subWidth);
            }
        }

        public void UpdateMenuOptions(List<SkModules.SkBaseModule> newMenuOptions)
        {
            menuOptions = newMenuOptions;
            frames.Clear();
            selectedModule = -1;
            menuOpen = false;
        }

        // ------------------------------------------------------------------ text helpers

        private static string CleanText(string s)
        {
            return s == null ? "" : s.Replace("\t►", "").Replace("►", "").Replace("\t", " ").Trim();
        }

        private static string ToggleFree(string s)
        {
            return CleanText(s).Replace("[ON]", "").Replace("[OFF]", "").Trim();
        }

        private static string Display(string s)
        {
            string t = CleanText(s);
            if (t.EndsWith("[ON]")) return t.Substring(0, t.Length - 4) + "<color=#7CFC00>[ON]</color>";
            if (t.EndsWith("[OFF]")) return t.Substring(0, t.Length - 5) + "<color=#FF8080>[OFF]</color>";
            return t;
        }

        private static string ModuleTitle(SkModules.SkBaseModule m)
        {
            string t = CleanText(m?.CallerEntry?.ItemText);
            return t.Length > 0 ? t : (m?.ModuleName ?? "Menu");
        }

        private static bool SameMenu(List<SkMenuItem> a, List<SkMenuItem> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (ToggleFree(a[i]?.ItemText) != ToggleFree(b[i]?.ItemText)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ drawing

        private void EnsureStyles()
        {
            float alpha = ConfiguredOpacity;
            if (stylesReady && Mathf.Abs(alpha - stylesAlpha) < 0.001f) return;
            stylesAlpha = alpha;

            // Speelo's Toolbox: opacity can be dragged live in a config manager, which would rebuild these every frame.
            // HideAndDontSave textures are never collected on their own, so release the previous set first.
            ReleaseTextures();

            texWindow = MakeTex(new Color(0.06f, 0.07f, 0.09f, alpha));
            texPanel = MakeTex(new Color(0.11f, 0.12f, 0.15f, alpha));
            texItem = MakeTex(new Color(0.17f, 0.19f, 0.23f, alpha));
            texItemHover = MakeTex(new Color(0.27f, 0.31f, 0.38f, alpha));
            texItemActive = MakeTex(new Color(0.13f, 0.42f, 0.55f, alpha));
            texAccent = MakeTex(new Color(0.10f, 0.30f, 0.40f, alpha));
            texWhite = MakeTex(Color.white); // tinted per use by GUI.color, for outlines

            Color text = new Color(0.93f, 0.94f, 0.96f);
            Color textDim = new Color(0.72f, 0.75f, 0.80f);

            styleWindow = new GUIStyle(GUI.skin.window) { fontSize = 14, fontStyle = FontStyle.Bold };
            styleWindow.padding = new RectOffset(12, 12, 28, 12);
            styleWindow.normal.background = texWindow;
            styleWindow.onNormal.background = texWindow;
            styleWindow.border = new RectOffset(0, 0, 0, 0);
            styleWindow.normal.textColor = text;
            styleWindow.onNormal.textColor = text;

            styleItem = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 13, fixedHeight = 27f, richText = true };
            styleItem.padding = new RectOffset(10, 8, 3, 3);
            styleItem.border = new RectOffset(0, 0, 0, 0);
            styleItem.margin = new RectOffset(0, 0, 1, 1);
            Paint(styleItem, texItem, texItemHover, texItemActive, text);

            styleBack = new GUIStyle(styleItem) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fixedHeight = 25f, richText = true };

            styleHeader = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            styleHeader.normal.textColor = text;

            styleTip = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };

            styleError = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, fontStyle = FontStyle.Bold };
            styleError.normal.textColor = new Color(1f, 0.55f, 0.45f);

            // Louder than styleError: this one carries "you are about to destroy something".
            styleWarning = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, fontStyle = FontStyle.Bold };
            styleWarning.normal.textColor = new Color(1f, 0.38f, 0.32f);
            styleWarning.padding = new RectOffset(0, 0, 4, 4);
            styleTip.normal.textColor = new Color(0.95f, 0.90f, 0.65f);

            styleSmall = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            styleSmall.normal.textColor = textDim;

            styleFilter = new GUIStyle(GUI.skin.textField) { fontSize = 13, fixedHeight = 25f };
            styleFilter.normal.background = texPanel;
            styleFilter.focused.background = texPanel;
            styleFilter.hover.background = texPanel;
            styleFilter.border = new RectOffset(0, 0, 0, 0);
            styleFilter.padding = new RectOffset(6, 6, 4, 4);
            styleFilter.normal.textColor = text;
            styleFilter.focused.textColor = Color.white;

            // Tabs across the top: unselected sits back, selected reads as the active page.
            styleTab = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fixedHeight = 30f, richText = true };
            styleTab.padding = new RectOffset(10, 10, 4, 4);
            styleTab.border = new RectOffset(0, 0, 0, 0);
            styleTab.margin = new RectOffset(0, 2, 0, 0);
            Paint(styleTab, texPanel, texItemHover, texItemActive, textDim);

            styleTabOn = new GUIStyle(styleTab) { fontStyle = FontStyle.Bold, fontSize = 14 };
            Paint(styleTabOn, texAccent, texAccent, texItemActive, Color.white);

            styleGridCell = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 10, richText = false, wordWrap = true };
            styleGridCell.border = new RectOffset(0, 0, 0, 0);
            styleGridCell.padding = new RectOffset(2, 2, 2, 2);
            styleGridCell.margin = new RectOffset(0, (int)GridPad, (int)GridPad, 0);
            Paint(styleGridCell, texItem, texItemHover, texItemActive, textDim);

            // Floating tooltip that follows the cursor. Fully opaque regardless of MenuOpacity, since it sits over
            // the menu's own content and has to stay readable.
            styleTooltip = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
            texPrompt = MakeTex(new Color(0.09f, 0.11f, 0.15f, 1f));
            texTooltip = MakeTex(new Color(0.03f, 0.04f, 0.06f, 0.98f));
            styleTooltip.normal.background = texTooltip;
            styleTooltip.border = new RectOffset(0, 0, 0, 0);
            styleTooltip.padding = new RectOffset(8, 8, 6, 6);
            styleTooltip.normal.textColor = new Color(0.96f, 0.93f, 0.72f);

            // Opaque regardless of MenuOpacity: a prompt sits over the menu's own content and has to read as
            // a separate surface, not a smudge of it.
            stylePrompt = new GUIStyle(GUI.skin.box);
            stylePrompt.normal.background = texPrompt;
            stylePrompt.border = new RectOffset(0, 0, 0, 0);

            styleSectionBox = new GUIStyle(GUI.skin.box);
            styleSectionBox.normal.background = texPanel;
            styleSectionBox.border = new RectOffset(0, 0, 0, 0);
            styleSectionBox.padding = new RectOffset(8, 8, 6, 8);
            styleSectionBox.margin = new RectOffset(0, 0, 0, 8);

            styleSectionHeader = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            styleSectionHeader.normal.textColor = new Color(0.62f, 0.78f, 0.95f);
            styleSectionHeader.padding = new RectOffset(2, 2, 0, 4);

            styleClose = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
            styleClose.border = new RectOffset(0, 0, 0, 0);
            styleClose.padding = new RectOffset(0, 0, 0, 0);
            Paint(styleClose, texPanel, MakeTex(new Color(0.65f, 0.16f, 0.16f, alpha)), texItemActive, text);

            stylesReady = true;
        }

        private static void ReleaseTextures()
        {
            foreach (Texture2D tex in new Texture2D[] { texWindow, texPanel, texItem, texItemHover, texItemActive, texAccent, texWhite, texTooltip, texPrompt })
            {
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            texWindow = texPanel = texItem = texItemHover = texItemActive = texAccent = texWhite = texTooltip = texPrompt = null;
        }

        private static void Paint(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active, Color text)
        {
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = normal;
            style.onNormal.background = normal;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = text;
            style.onNormal.textColor = text;
            style.onHover.textColor = Color.white;
            style.onActive.textColor = Color.white;
        }

        void OnGUI()
        {
            if (menuOptions == null || menuOptions.Count == 0) // There will be at least one frame where there is no menu when initialized
            {
                UpdateMenuOptions(SkModuleController.GetOptions());
                return;
            }
            if (!menuOpen) return;

            EnsureStyles();
            if (!windowPlaced)
            {
                windowRect.x = 24f;
                windowRect.y = Mathf.Max(24f, (Screen.height - windowRect.height) / 2f);
                windowPlaced = true;
            }

            GUI.color = Color.white;
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, appName + "  v" + SkBepInExLoader.VERSION, styleWindow);
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));

            // Swallow clicks that land outside the window so they don't reach other IMGUI handlers while the menu is open.
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseUp) && !windowRect.Contains(e.mousePosition))
            {
                e.Use();
            }
        }

        private void DrawWindow(int windowID)
        {
            try
            {
                // Unity never clears GUI.tooltip between frames: it is only written when the mouse is over a
                // control that has one. So hovering a control with no tooltip, or nothing at all, left the previous
                // one on screen. Clearing it here means anything still set by the end of the pass is genuinely
                // what the cursor is over now.
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.tooltip = string.Empty;
                }

                // A prompt makes everything behind it inert. Disabled IMGUI controls do not take clicks, so the
                // layout below still draws normally and simply cannot be interacted with.
                bool blocked = activePrompt != null;
                bool wasEnabled = GUI.enabled;
                if (blocked) GUI.enabled = false;

                // Layout, top to bottom. Each section is its own method so the arrangement can be changed
                // without touching the others.
                DrawCloseButton();

                if (activeForm != null)
                {
                    DrawForm();
                    if (blocked)
                    {
                        GUI.enabled = wasEnabled;
                        DrawPrompt();
                    }
                    DrawCursorTooltip();
                    if (!blocked) GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, windowRect.width - 36f), 26f));
                    return;
                }

                DrawTabs();
                DrawFrameHeader();
                DrawFilterRow();
                DrawSliderRow();
                DrawItemList();
                DrawFooter();
                if (blocked)
                {
                    GUI.enabled = wasEnabled;
                    DrawPrompt();
                }
                DrawCursorTooltip(); // last, so it sits above every other control

                if (blocked) return; // the window must not drag while a prompt owns the clicks

                // Everything except the close button's corner drags the window.
                GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, windowRect.width - 36f), 26f));
            }
            catch (ArgumentException)
            {
                // IMGUI layout mismatch within a frame (e.g. list changed mid-pass); the next frame redraws cleanly.
            }
            catch (Exception ex)
            {
                SkUtilities.Logz(new string[] { "CONTROLLER", "ERROR" }, new string[] { ex.Message });
            }
            finally
            {
                if (pendingAction != null)
                {
                    Action a = pendingAction;
                    pendingAction = null;
                    try { a(); }
                    catch (Exception ex)
                    {
                        SkUtilities.Logz(new string[] { "CONTROLLER", "ERROR" }, new string[] { ex.Message });
                    }
                }
            }
        }
    }
}

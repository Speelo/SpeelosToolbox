using SkToolbox.Configuration;
using SkToolbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SkToolbox.SkModules
{
    internal class ModConsole : SkBaseModule, IModule
    {
        internal bool conWriteToFile = false;
        private string consoleLastMessage = string.Empty;
        private string lastOutput = string.Empty;
        private string chatInLastMessage = string.Empty;

        private Rect EnemyWindow;
        private Rect ConsoleOutput;
        private GUIStyle ConsoleOutputStyle;
        private History consoleHistory = new History();
        public List<string> consoleOutputHistory = new List<string>();
        private Vector2 scrollPosition;
        private Rect rectCoords = new Rect(05, 020, 125, 20);
        private Rect rectEnemy = new Rect(05, 415, 375, 50);
        private Rect rectConsoleOutput = new Rect(0, 0, 500, 500);
        private bool anncounced1 = false;
        private bool anncounced2 = false;

        private int ConsoleOutputMaxHistory = 250;

        public Dictionary<string, string> AliasList = new Dictionary<string, string>();

        public List<string> PrefabList = new List<string>();
        public List<string> ItemList = new List<string>();
        /// <summary>Bound character to the command line it runs. Keyed by the character the player types, which
        /// is what the config documents and what the Input System hands us - so a capital really does mean Shift,
        /// and a non-US layout works without a translation table.</summary>
        private readonly Dictionary<char, string> hotkeys = new Dictionary<char, string>();

        /// <summary>Characters typed since the last frame, filled by the Input System callback below.</summary>
        private readonly List<char> typedChars = new List<char>();
        private Keyboard typedFrom;
        private List<Character> nearbyCharacters = new List<Character>();
        private Terminal terminal;

        //SkConsole SkConsole;
        public ModConsole() : base()
        {
            base.ModuleName = "CC Controller";
            base.Loading();
        }

        public void Start()
        {
            SkCommandProcessor.ConsoleOpt = this;
            SkCommandPatcher.InitPatch(); // idempotent; the respawn hooks must be in place before the first spawn
            //SkConsole = gameObject.AddComponent<SkConsole>();
            BeginMenu();
            try
            {
                ConsoleOutputStyle = new GUIStyle();
                ConsoleOutputStyle.wordWrap = true;
                //Texture2D OutputTexture = new Texture2D(2, 2);
                //OutputTexture.SetPixels(new Color[] { Color.red, Color.red, Color.black, Color.black });
                //ConsoleOutputStyle.active.background = OutputTexture;
                BuildAliases();
                BuildHotkeys();
                //SkVersionChecker.VersionCurrent(); // Try to load the version so we don't have to when they open the console
                if(SkConfigEntry.CScrollableLimit != null)
                {
                    ConsoleOutputMaxHistory = SkConfigEntry.CScrollableLimit.Value;
                }
            }
            catch (Exception)
            {

            }
            base.Ready();
        }

        public void BeginMenu()
        {
            SkMenu consoleOptMenu = new SkMenu();
            consoleOptMenu.AddItem("Reload Toolbox", new Action(ReloadMenu));
            consoleOptMenu.AddItem("Unload Toolbox", new Action(UnloadMenu));
            consoleOptMenu.AddItem("Open Log Folder", new Action(OpenLogFolder));
            //consoleOptMenu.AddItemToggle("Write to File", ref conWriteToFile, new Action(ToggleWriteFile), "Write log output to file?");
            MenuOptions = consoleOptMenu;
        }

        // ------------------------------------------------------------------ the System tab
        //
        // Everything here sits outside the game world: what the renderer is doing, and the toolbox itself.

        // Nothing on this tab leaves your own machine: the display settings are renderer state and the toolbox
        // entries only touch the toolbox, so every cell here is marked as client-side.
        private static SkMenuController.SkGridItem Cell(string section, string label, string tip, Sprite icon, Action run)
        {
            return new SkMenuController.SkGridItem
            {
                Name = label,
                Display = label,
                Tip = label + "  -  " + tip,
                Icon = icon,
                Section = section,
                Scope = SkMenuController.SkScope.Client,
                OnClick = (string ignored) => run(),
            };
        }

        internal void ShowGrid()
        {
            List<SkMenuController.SkGridItem> grid = new List<SkMenuController.SkGridItem>
            {
                Cell("Display", "Field of View", "Widen or narrow the camera", SkIcons.First("Torch"), ShowFovForm),
                Cell("Display", "Frame Limit", "Cap the frame rate", SkIcons.First("Feathers"), ShowFpsForm),
                Cell("Display", "Detail Distance", "How far away the game keeps full detail", SkIcons.First("Stone", "Wood"), ShowLodForm),
                Cell("Display", "Snow Buildup", "Add or clear snow on nearby building pieces", SkIcons.First("FreezeGland", "WolfPelt"), ShowSnowForm),
                Cell("Display", "Free Fly Camera", "Detach the camera for screenshots. Run it again to come back",
                     SkIcons.First("FeatherCape", "Feathers"), () => SkRun.CmdNotify("freefly", "Toggled the free fly camera.")),
                new SkMenuController.SkGridItem
                {
                    Name = "Debug Mode",
                    Display = "Debug Mode",
                    Tip = "Debug Mode  -  the game's own developer mode, which some other cheats rely on",
                    Icon = SkIcons.First("Wishbone"),
                    Section = "Display",
                    Scope = SkMenuController.SkScope.Client,
                    OnClick = (string ignored) => SkRun.CmdNotify("debugmode", "Toggled debug mode."),
                    IsOn = () => Player.m_debugMode,
                },

                Cell("Toolbox", "Reload Toolbox", "Unload and load the toolbox again", SkIcons.First("Hammer"), ReloadMenu),
                Cell("Toolbox", "Unload Toolbox", "Shut the toolbox down until the next launch", SkIcons.First("Flint", "Stone"), UnloadMenu),
                Cell("Toolbox", "Open Log Folder", "Open the folder holding the log files", SkIcons.First("Coins"), OpenLogFolder),
            };

            SkMC.RequestGridMenu(grid, (List<SkMenuController.SkMenuSlider>)null, "System", showFilter: false);
        }

        private void ShowFovForm()
        {
            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Field of View",
                Note = "The game's default is 65. Higher shows more of the world and feels faster.",
            };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "fov", Label = "Degrees", Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = 40, Max = 120, IntValue = CurrentFov(),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("fov " + f.Field("fov").IntValue, "Field of view: " + f.Field("fov").IntValue),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Back to 65",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("fov 65", "Field of view back to 65."),
            });
            SkMC.ShowForm(form);
        }

        private static int CurrentFov()
        {
            Camera camera = Camera.main;
            return camera != null ? Mathf.Clamp(Mathf.RoundToInt(camera.fieldOfView), 40, 120) : 65;
        }

        private void ShowFpsForm()
        {
            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Frame Limit",
                Note = "Caps the frame rate. This is saved with the graphics settings, the same as changing it in the options screen.",
            };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "fps", Label = "Frames per second", Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = 30, Max = 360, IntValue = 60,
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("maxfps " + f.Field("fps").IntValue, "Frame limit: " + f.Field("fps").IntValue),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Uncapped",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("maxfps 0", "Frame limit removed."),
            });
            SkMC.ShowForm(form);
        }

        private void ShowLodForm()
        {
            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Detail Distance",
                Note = "How far away the game keeps full detail. 1 is normal; higher looks better and costs performance.",
            };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "bias", Label = "Bias", Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = 1, Max = 10, IntValue = Mathf.Clamp(Mathf.RoundToInt(QualitySettings.lodBias), 1, 10),
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Apply",
                Run = (SkMenuController.SkForm f) => SkRun.CmdNotify("lodbias " + f.Field("bias").IntValue, "Detail distance: " + f.Field("bias").IntValue),
            });
            SkMC.ShowForm(form);
        }

        private void ShowSnowForm()
        {
            SkMenuController.SkForm form = new SkMenuController.SkForm
            {
                Title = "Snow Buildup",
                Note = "Adds or removes snow on every building piece nearby. Adding only affects pieces that can hold snow.",
            };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "amount", Label = "Amount %", Kind = SkMenuController.SkFieldKind.IntSlider,
                Min = 5, Max = 100, IntValue = 100,
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Add snow",
                Run = (SkMenuController.SkForm f) =>
                {
                    SkRun.Cmd("snow " + Fraction(f.Field("amount").IntValue));
                    SkCommandProcessor.Notify("Added snow to nearby pieces.");
                },
            });
            form.Actions.Add(new SkMenuController.SkFormAction
            {
                Label = "Clear snow",
                Run = (SkMenuController.SkForm f) =>
                {
                    SkRun.Cmd("snow -1");
                    SkCommandProcessor.Notify("Cleared snow from nearby pieces.");
                },
            });
            SkMC.ShowForm(form);
        }

        /// <summary>Formats a 0..1 value the way the console expects, regardless of the machine's decimal comma.</summary>
        private static string Fraction(int percent)
        {
            return (percent / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }


        public void OpenLogFolder()
        {
            SkUtilities.Logz(new string[] { "CMD", "REQ" }, new string[] { "Opening Log Directory" });
            Application.OpenURL(Application.persistentDataPath);
        }

        public void ReloadMenu()
        {
            SkUtilities.Logz(new string[] { "BASE", "CMD", "REQ" }, new string[] { "UNLOADING CONTROLLERS AND MODULES...", "SKTOOLBOX RELOAD REQUESTED.", });
            SkLoader.Reload();
        }

        public void UnloadMenu()
        {
            SkUtilities.Logz(new string[] { "BASE", "CMD", "REQ" }, new string[] { "SKTOOLBOX UNLOAD REQUESTED.", "UNLOADING CONTROLLERS AND MODULES..." });
            if (SkConfigEntry.CScrollable != null & SkConfigEntry.CScrollable.Value && Console.instance != null)
            {
                Console.instance.m_output.gameObject.SetActive(true);
            }
            SkLoader.Unload();
        }

        public void HandleConsole()
        {
            if(terminal == null)
            {
                if (Console.instance != null)
                {
                    terminal = SkUtilities.GetPrivateProperty<Terminal>(Console.instance, "m_terminalInstance");
                }
            }
            if (Console.instance != null && terminal != null)
            {
                //Scrollable console
                if (SkConfigEntry.CScrollable != null & SkConfigEntry.CScrollable.Value)
                {
                    List<string> consoleOutputBuffer = SkUtilities.GetPrivateField<List<string>>(Console.instance, "m_chatBuffer");
                    if (consoleOutputBuffer != null
                           && consoleOutputBuffer.Count > 0)
                    {
                        try
                        {
                            while (consoleOutputBuffer.Count > 0)
                            {
                                consoleOutputHistory.Add(consoleOutputBuffer[0]);
                                consoleOutputBuffer.RemoveAt(0);
                            }
                        } catch (Exception)
                        {
                            //
                        }

                        if (consoleOutputBuffer.Count == 0)
                        {
                            Console.instance.m_output.text = string.Empty;
                        }
                        scrollPosition = new Vector2(0, Int32.MaxValue);

                        // Make sure we don't actually make too many console entries
                        while (consoleOutputHistory.Count > ConsoleOutputMaxHistory)
                        {
                            consoleOutputHistory.RemoveAt(0);
                        }
                    }
                }
                if (Console.instance.m_chatWindow.gameObject.activeInHierarchy)
                {
                    string inputText = Console.instance.m_input.text;

                    if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
                    {
                        consoleLastMessage = string.Empty;
                    }
                    if (!inputText.Equals(string.Empty) && !inputText.Equals(consoleLastMessage))
                    {
                        consoleLastMessage = inputText;
                    }
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        if (inputText.Equals(string.Empty) && !consoleLastMessage.Equals(string.Empty))
                        {
                            consoleHistory.Add(consoleLastMessage);
                            //SkCommandProcessor.ProcessCommands(consoleLastMessage, SkCommandProcessor.LogTo.Console);

                            consoleLastMessage = string.Empty;
                        }
                    }
                    //if (Input.GetKeyDown(KeyCode.UpArrow))
                    //{
                    //    Console.instance.m_input.text = consoleHistory.Fetch(inputText, true);
                    //    Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                    //}
                    //if (Input.GetKeyDown(KeyCode.DownArrow))
                    //{
                    //    Console.instance.m_input.text = consoleHistory.Fetch(inputText, false);
                    //}
                }
                if (Input.GetKeyDown(KeyCode.Slash) // Open console with slash
                    && (SkConfigEntry.COpenConsoleWithSlash != null && SkConfigEntry.COpenConsoleWithSlash.Value)
                    && !global::Console.IsVisible() && (global::Chat.instance == null || !global::Chat.instance.IsChatDialogWindowVisible()) && !TextInput.IsVisible()) // Valheim 1.0: Chat.instance is null at the main menu
                {
                    Console.instance.m_chatWindow.gameObject.SetActive(true);
                    Console.instance.m_input.ActivateInputField(); // Valheim 1.0: GuiInputField needs explicit focus; vanilla only activates it in its own toggle branch
                    Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                }

                //if (Input.GetKeyDown(KeyCode.Tab)) // Autocomplete
                //{
                //    if (SkConfigEntry.CConsoleAutoComplete != null && SkConfigEntry.CConsoleAutoComplete.Value
                //        && Console.instance != null && global::Console.IsVisible())
                //    {
                //        if (!string.IsNullOrEmpty(consoleLastMessage))
                //        {
                //            if (consoleLastMessage.StartsWith("/spawn")) // Spawn command
                //            {
                //                BuildPrefabs();

                //                string matchString = consoleLastMessage.Remove(0, 6);
                //                matchString = matchString.Trim();

                //                if (string.IsNullOrEmpty(matchString))
                //                {
                //                    matchString = "a";
                //                }
                //                try
                //                {
                //                    string matchPrefab = PrefabList.FirstOrDefault(item => !item.Equals(matchString)
                //                    && item.StartsWith(matchString, true,
                //                    System.Globalization.CultureInfo.InvariantCulture));
                //                    if (!string.IsNullOrEmpty(matchString))
                //                    {
                //                        Console.instance.m_input.text = "/spawn " + matchPrefab;
                //                        Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                //                    }
                //                }
                //                catch (Exception)
                //                {
                //                    //
                //                }
                //            }
                //            else if (consoleLastMessage.StartsWith("/spawntamed")) // Spawn command
                //            {
                //                BuildPrefabs();

                //                string matchString = consoleLastMessage.Remove(0, 11);
                //                matchString = matchString.Trim();

                //                if (string.IsNullOrEmpty(matchString))
                //                {
                //                    matchString = "a";
                //                }

                //                try
                //                {
                //                    string matchItem = ItemList.FirstOrDefault(item => !item.Equals(matchString)
                //                    && item.StartsWith(matchString, true,
                //                    System.Globalization.CultureInfo.InvariantCulture));
                //                    if (!string.IsNullOrEmpty(matchString))
                //                    {
                //                        Console.instance.m_input.text = "/spawntamed " + matchItem;
                //                        Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                //                    }
                //                }
                //                catch (Exception)
                //                {
                //                    //
                //                }
                //            }
                //            else if (consoleLastMessage.StartsWith("/give")) // Spawn command
                //            {
                //                BuildPrefabs();

                //                string matchString = consoleLastMessage.Remove(0, 5);
                //                matchString = matchString.Trim();

                //                if (string.IsNullOrEmpty(matchString))
                //                {
                //                    matchString = "Woo";
                //                }

                //                try
                //                {
                //                    string matchItem = ItemList.FirstOrDefault(item => !item.Equals(matchString)
                //                    && item.StartsWith(matchString, true,
                //                    System.Globalization.CultureInfo.InvariantCulture));
                //                    if (!string.IsNullOrEmpty(matchString))
                //                    {
                //                        Console.instance.m_input.text = "/give " + matchItem;
                //                        Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                //                    }
                //                }
                //                catch (Exception)
                //                {
                //                    //
                //                }
                //            }
                //            else if (consoleLastMessage.StartsWith("/give")) // Spawn command
                //            {
                //                BuildPrefabs();

                //                string matchString = consoleLastMessage.Remove(0, 5);
                //                matchString = matchString.Trim();

                //                if (string.IsNullOrEmpty(matchString))
                //                {
                //                    matchString = "Woo";
                //                }

                //                try
                //                {
                //                    string matchItem = ItemList.FirstOrDefault(item => !item.Equals(matchString)
                //                    && item.StartsWith(matchString, true,
                //                    System.Globalization.CultureInfo.InvariantCulture));
                //                    if (!string.IsNullOrEmpty(matchString))
                //                    {
                //                        Console.instance.m_input.text = "/give " + matchItem;
                //                        Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                //                    }
                //                }
                //                catch (Exception)
                //                {
                //                    //
                //                }
                //            }
                //            else if (consoleLastMessage.StartsWith("/env")) // env command
                //            {
                //                string matchString = consoleLastMessage.Remove(0, 4);
                //                matchString = matchString.Trim();

                //                if (string.IsNullOrEmpty(matchString))
                //                {
                //                    matchString = "Cle";
                //                }

                //                try
                //                {
                //                    string matchEnv = SkCommandProcessor.weatherList.FirstOrDefault(item => !item.Equals(matchString)
                //                    && item.StartsWith(matchString, true,
                //                    System.Globalization.CultureInfo.InvariantCulture));
                //                    if (!string.IsNullOrEmpty(matchString))
                //                    {
                //                        Console.instance.m_input.text = "/env " + matchEnv;
                //                        Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                //                    }
                //                }
                //                catch (Exception)
                //                {
                //                    //
                //                }
                //            }
                //            else // Sktoolbox command or Alias
                //            {
                //                try
                //                {
                //                    KeyValuePair<string, string> matchCommand = SkCommandProcessor.commandList.FirstOrDefault(item => !item.Key.Equals(consoleLastMessage)
                //                        && item.Key.StartsWith(consoleLastMessage.Substring(0, Console.instance.m_input.caretPosition), true,
                //                        System.Globalization.CultureInfo.InvariantCulture));

                //                    KeyValuePair<string, string> matchAlias = AliasList.FirstOrDefault(item => !item.Key.Equals(consoleLastMessage)
                //                        && item.Key.StartsWith(consoleLastMessage.Substring(0, Console.instance.m_input.caretPosition), true,
                //                        System.Globalization.CultureInfo.InvariantCulture));

                //                    if (!string.IsNullOrEmpty(matchCommand.Key) || !string.IsNullOrEmpty(matchAlias.Key))
                //                    {
                //                        Console.instance.m_input.text = (string.IsNullOrEmpty(matchCommand.Key) ? matchAlias.Key : matchCommand.Key);
                //                        Console.instance.m_input.caretPosition = Console.instance.m_input.text.Length;
                //                    }
                //                }
                //                catch (Exception)
                //                {
                //                    //
                //                }

                //            }
                //        }
                //    }
                //}
            }
            base.ModuleStatus = SkUtilities.Status.Ready;
        }

        public void HandleChat()
        {             //Add chat commands
            if (Chat.instance != null)
            {
                if (Chat.instance.m_chatWindow.gameObject.activeInHierarchy) // Chat is open
                {
                    string inputText = Chat.instance.m_input.text;
                    string outputText = Chat.instance.m_output.text;

                    //Input
                    if ((Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Escape))
                        && ((SkConfigEntry.CAllowExecuteOnClear != null && !SkConfigEntry.CAllowExecuteOnClear.Value) || SkConfigEntry.CAllowExecuteOnClear == null))
                    {
                        chatInLastMessage = string.Empty;
                    }
                    if (!inputText.Equals(string.Empty) && !inputText.Equals(consoleLastMessage))
                    {
                        chatInLastMessage = inputText;
                    }
                    if (inputText.Equals(string.Empty) && !chatInLastMessage.Equals(string.Empty)) // Enter was pressed
                    {
                        SkCommandProcessor.ProcessCommands(chatInLastMessage, SkCommandProcessor.LogTo.Chat);

                        chatInLastMessage = string.Empty;
                    }
                } // Chat is closed
                if (Input.GetKeyDown(KeyCode.Slash) && Player.m_localPlayer != null
                        && (SkConfigEntry.COpenConsoleWithSlash != null && !SkConfigEntry.COpenConsoleWithSlash.Value)
                        && (SkConfigEntry.COpenChatWithSlash != null && SkConfigEntry.COpenChatWithSlash.Value)
                        && !global::Console.IsVisible() && !global::Chat.instance.IsChatDialogWindowVisible() && !TextInput.IsVisible()
                        && !Minimap.InTextInput() && !Menu.IsVisible())
                {
                    SkUtilities.SetPrivateField(Chat.instance, "m_hideTimer", 0f);
                    Chat.instance.m_chatWindow.gameObject.SetActive(true);
                    Chat.instance.m_input.gameObject.SetActive(true);
                    Chat.instance.m_input.ActivateInputField();
                }
            }
        }

        public void LoadConsoleCustomizations()
        {
            if (SkConfigEntry.CAllowLookCustomizations != null && SkConfigEntry.CAllowLookCustomizations.Value)
            {
                try
                {
                    ConsoleOutputStyle = new GUIStyle();
                    ConsoleOutputStyle.wordWrap = true;

                    int fontSize = (int)Console.instance.m_output.fontSize;
                    string fontName = SkConfigEntry.CConsoleFont.Value;
                    string fontPath = string.Empty;
                    string[] fontPaths = Font.GetPathsToOSFonts();
                    foreach (var path in fontPaths)
                    {
                        if (path.IndexOf(fontName, StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            fontPath = path;
                            break;
                        }
                    }

                    if(!string.IsNullOrEmpty(fontPath))
                    {
                        Font foundFont = new Font(fontPath);
                        Font consoleFontFont = Font.CreateDynamicFontFromOSFont(fontName, fontSize);
                        TMP_FontAsset consoleFont = TMP_FontAsset.CreateFontAsset(foundFont);
                        Console.instance.m_input.textComponent.font = consoleFont;
                        Console.instance.m_output.font = consoleFont;
                        ConsoleOutputStyle.font = consoleFontFont;
                    }   
                    
                    Color outputColor = Console.instance.m_output.color;
                    Color inputColor = Console.instance.m_input.textComponent.color;
                    Color selectionColor = Console.instance.m_input.selectionColor;
                    Color caretColor = Color.white;
                    try
                    {
                        fontSize = SkConfigEntry.CConsoleFontSize.Value;
                        fontName = SkConfigEntry.CConsoleFont.Value;
                        ColorUtility.TryParseHtmlString(SkConfigEntry.CConsoleOutputTextColor.Value, out outputColor);
                        ColorUtility.TryParseHtmlString(SkConfigEntry.CConsoleInputTextColor.Value, out inputColor);
                        ColorUtility.TryParseHtmlString(SkConfigEntry.CConsoleSelectionColor.Value, out selectionColor);
                        ColorUtility.TryParseHtmlString(SkConfigEntry.CConsoleCaretColor.Value, out caretColor);
                    }
                    catch (Exception Ex)
                    {
                        SkUtilities.Logz(new string[] { "ERR" }, new string[] { "Failed to load something from the config.", Ex.Message }, LogType.Warning);
                    }

                    
                    
                    Console.instance.m_input.textComponent.color = inputColor;
                    Console.instance.m_input.selectionColor = selectionColor;
                    Console.instance.m_input.caretColor = caretColor;
                    Console.instance.m_input.customCaretColor = true;

                    Console.instance.m_output.fontSize = fontSize;
                    Console.instance.m_output.color = outputColor;

                    if (SkConfigEntry.CScrollable != null & SkConfigEntry.CScrollable.Value)
                    {
                        ConsoleOutputStyle.fontSize = fontSize;
                        ConsoleOutputStyle.normal.textColor = outputColor;
                    }
                }
                catch (Exception Ex)
                {
                    SkUtilities.Logz(new string[] { "ERR" }, new string[] { "Failed to set when customizing console style.", Ex.Message }, LogType.Warning);
                }
            } else
            {
                if (SkConfigEntry.CScrollable != null & SkConfigEntry.CScrollable.Value)
                {
                    Font consoleFont = Font.CreateDynamicFontFromOSFont("Consolas", 18);
                    ConsoleOutputStyle.fontSize = 18;
                    ConsoleOutputStyle.font = consoleFont;
                    ConsoleOutputStyle.normal.textColor = Color.white;
                }
            }
            Console.instance.m_output.gameObject.SetActive(false);
            SkCommandProcessor.ProcessCommand("/clear", SkCommandProcessor.LogTo.Console);
            //Console.instance.Print("type \"help\" - for commands");
            //Console.instance.Print("");
        }

        private void OnGUI()
        {
            if (Player.m_localPlayer != null)
            {
                if (SkCommandProcessor.bDetectEnemies && nearbyCharacters.Count > 0)
                {
                    EnemyWindow = GUILayout.Window(39999, rectEnemy, ProcessEnemies, "Enemy Information");
                }
                if (SkCommandProcessor.bCoords)
                {
                    Vector3 plPos = Player.m_localPlayer.transform.position;
                    GUI.Label(rectCoords, "Coords: " + Mathf.RoundToInt(plPos.x) + "/" + Mathf.RoundToInt(plPos.z));
                }
            }
                if (Console.instance != null && global::Console.IsVisible())
                {
                    rectConsoleOutput = new Rect
                    {
                        x = 7,
                        y = 1,
                        width = Console.instance.m_output.rectTransform.rect.width - 7,
                        height = Console.instance.m_output.rectTransform.rect.height
                    };
                    if (SkVersionChecker.VersionCurrent())
                    {
                        ConsoleOutput = GUILayout.Window(39979, rectConsoleOutput, ProcessConsoleOutput, " Speelo's Toolbox (" + SkVersionChecker.currentVersion + ") by Skrip (DS)", ConsoleOutputStyle);
                    }
                    else
                    {
                        ConsoleOutput = GUILayout.Window(39979, rectConsoleOutput, ProcessConsoleOutput, " Speelo's Toolbox, based on SkToolbox by Skrip ► " +
                            "New Version Available on NexusMods!\t► Current: " + SkVersionChecker.currentVersion + " Latest: " + SkVersionChecker.latestVersion, ConsoleOutputStyle);
                    }
                }
        }

        private void ProcessConsoleOutput(int windowID)
        {
            if (!Cursor.visible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (SkConfigEntry.CConsoleFontSize != null)
            {
                GUILayout.Space(SkConfigEntry.CConsoleFontSize.Value);
            } else
            {
                GUILayout.Space(18);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            foreach (string msg in consoleOutputHistory)
            {
                GUILayout.Label(msg, ConsoleOutputStyle);
                GUILayout.Space(7);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public void BuildPrefabs()
        {
            if (PrefabList.Count == 0 && ZNetScene.instance != null)
            {
                try
                {
                    foreach (GameObject Prefab in SkUtilities.GetPrivateField<Dictionary<int, GameObject>>(ZNetScene.instance, "m_namedPrefabs").Values)
                    {
                        PrefabList.Add(Utils.GetPrefabName(Prefab));
                        ItemDrop ItemComponent = Prefab.GetComponent<ItemDrop>();
                        if (ItemComponent != null)
                        {
                            ItemList.Add(Utils.GetPrefabName(Prefab));
                        }
                    }
                }
                catch(Exception)
                {

                }
            }
        }

        private void Update()
        {
            if (Console.instance != null && SkConfigEntry.CConsoleEnabled.Value && !Console.instance.IsConsoleEnabled())
            {
                // Valheim 1.0: m_consoleEnabled is static; use the public setter instead of reflection.
                Console.SetConsoleEnabled(true);
            }

            try
            {
                HandleConsole();
            }
            catch (Exception Ex)
            {
                base.ModuleStatus = SkUtilities.Status.Error;
                SkUtilities.Logz(new string[] { "ERR" }, new string[] { "Error detected!", Ex.Message, Ex.Source }, LogType.Warning);
                SkCommandProcessor.PrintOut("An error was detected. You may need to restart your game.", SkCommandProcessor.LogTo.Console);
            }


            if ((SkConfigEntry.CAllowChatCommandInput != null && SkConfigEntry.CAllowChatCommandInput.Value) || SkConfigEntry.CAllowChatCommandInput == null)
            {
                try
                {
                    HandleChat();
                }
                catch (Exception)
                {
                    base.ModuleStatus = SkUtilities.Status.Error;
                }
            }

            if (!anncounced1)
            {
                if (Console.instance != null && Player.m_localPlayer == null) // Only announce at main menu
                {
                    SkCommandProcessor.Announce();
                    LoadConsoleCustomizations();
                    SkCommandProcessor.InitCommands();
                    anncounced1 = true;
                }
            }
            else
            {
                if (Player.m_localPlayer != null)
                {
                    anncounced1 = false;
                }
            }

            if (!anncounced2) // Announce in game
            {
                if (Chat.instance != null)
                {
                    SkCommandProcessor.Announce();
                    LoadConsoleCustomizations();
                    anncounced2 = true;
                }
            }
            else
            {
                if (Chat.instance == null)
                {
                    anncounced2 = false;
                }
            }

            ProcessAutoRun();

            ProcessHotkeys();

            if (SkCommandProcessor.bTeleport)
            {
                if (Input.GetKeyDown(KeyCode.BackQuote))
                {
                    try
                    {
                        Ray rayCast = Camera.main.ScreenPointToRay(Input.mousePosition);
                        RaycastHit hit;
                        if (Physics.Raycast(rayCast, out hit))
                        {
                            Vector3 targetLoc = hit.point;
                            Player.m_localPlayer.transform.position = targetLoc;
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Warp!", 0, null);
                        }
                    }
                    catch (Exception)
                    {
                        //
                    }
                }
            }
            if (SkCommandProcessor.bDetectEnemies)
            {
                try
                {
                    List<Character> charList = Character.GetAllCharacters();
                    if (charList.Count > 0)
                    {
                        foreach (Character character in charList)
                        {
                            if (character != null && Player.m_localPlayer != null && !character.IsDead() && !character.IsPlayer())
                            {
                                if (Vector3.Distance(character.transform.position, Player.m_localPlayer.transform.position) < SkCommandProcessor.bDetectRange)
                                {
                                    if (!nearbyCharacters.Contains(character))
                                    {
                                        nearbyCharacters.Add(character);
                                    }
                                }
                                else
                                {
                                    if (nearbyCharacters.Contains(character))
                                    {
                                        nearbyCharacters.Remove(character);
                                    }
                                }
                            }
                            else
                            {
                                if (nearbyCharacters.Contains(character))
                                {
                                    nearbyCharacters.Remove(character);
                                }
                            }
                        }

                        if (nearbyCharacters.Count > 0)
                        {
                            List<Character> tempCharList = new List<Character>(nearbyCharacters);
                            foreach (Character character in tempCharList)
                            {
                                if (!charList.Contains(character))
                                {
                                    nearbyCharacters.Remove(character);
                                }
                            }
                        }
                    }
                    if (nearbyCharacters.Count > 0 && SkCommandProcessor.btDetectEnemiesSwitch)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Enemy nearby!", 0, null);
                        SkCommandProcessor.btDetectEnemiesSwitch = false;
                    }
                    else if (nearbyCharacters.Count == 0)
                    {
                        SkCommandProcessor.btDetectEnemiesSwitch = true;
                    }
                }
                catch (Exception)
                {
                    //
                }
            }
        }

        private void OnDestroy()
        {
            if (typedFrom != null)
            {
                typedFrom.onTextInput -= OnTextInput;
                typedFrom = null;
            }

            if (SkConfigEntry.CScrollable != null & SkConfigEntry.CScrollable.Value && Console.instance != null)
            {
                Console.instance.m_output.gameObject.SetActive(true);
            }
            if (SkCommandPatcher.Harmony != null)
            {
                try
                {
                    SkCommandPatcher.Harmony.UnpatchSelf();
                }
                catch (Exception ex)
                {
                    SkUtilities.Logz(new string[] { "CONSOLE", "UNPATCH" }, new string[] { "UnpatchSelf failed: " + ex.Message }, UnityEngine.LogType.Warning);
                }
                finally
                {
                    // Valheim 1.0: Reset so the reloaded ModConsole's Announce() -> SkCommandPatcher.InitPatch()
                    // re-applies the Harmony patches instead of being skipped by the InitComplete guard.
                    SkCommandPatcher.Harmony = null;
                    SkCommandPatcher.InitComplete = false;
                }
            }
        }

        /// <summary>
        /// Runs any command bound to a character the player just typed.
        ///
        /// Characters rather than key codes: the config has always documented itself in terms of printable ASCII,
        /// with a capital meaning "hold Shift", and the Input System hands us exactly that through onTextInput.
        /// Polling key codes instead would mean a US-QWERTY translation table, which gets punctuation wrong on
        /// every other layout and cannot express half of what the config already allows.
        ///
        /// This runs before any of the game's own input handling and is not filtered by the menu's key block, so
        /// HotkeysAllowed below is the only thing standing between a bound key and a command while you are typing.
        /// The buffer is cleared on rejected frames as well, or a character typed into chat would fire a frame later.
        /// </summary>
        private void ProcessHotkeys()
        {
            try
            {
                SyncTextInput();
                if (typedChars.Count == 0) return;

                if (hotkeys.Count == 0 || !HotkeysAllowed())
                {
                    typedChars.Clear();
                    return;
                }

                // Taken and cleared before anything runs: a command that throws must not leave characters behind
                // to fire again on the next frame.
                char[] batch = typedChars.ToArray();
                typedChars.Clear();

                foreach (char typed in batch)
                {
                    string command;
                    if (!hotkeys.TryGetValue(typed, out command)) continue;
                    SkUtilities.Logz(new string[] { "CONTROLLER", "HOTKEY" }, new string[] { typed + " -> " + command });
                    SkCommandProcessor.ProcessCommands(command, SkCommandProcessor.LogTo.Console);
                }
            }
            catch (Exception ex)
            {
                typedChars.Clear();
                SkUtilities.Logz(new string[] { "CONTROLLER", "HOTKEY" }, new string[] { "Hotkey failed: " + ex.Message },
                                 LogType.Warning);
            }
        }

        /// <summary>
        /// Keeps the onTextInput subscription pointed at the live keyboard. Keyboard.current is replaced when a
        /// device is removed and re-added, and a subscription to the old one simply stops delivering.
        /// </summary>
        private void SyncTextInput()
        {
            Keyboard current = Keyboard.current;
            if ((object)current == (object)typedFrom) return;

            if (typedFrom != null) typedFrom.onTextInput -= OnTextInput;
            typedFrom = current;
            if (current != null) current.onTextInput += OnTextInput;
        }

        private void OnTextInput(char c)
        {
            // Bounded, so a key held down through a frame the game never processes cannot grow this without end.
            if (typedChars.Count < 32) typedChars.Add(c);
        }

        /// <summary>
        /// Every way the player might be typing rather than playing. Deliberately broader than the game's own
        /// bind command, which only checks chat and the console: a hotkey that fires while you are naming a map
        /// pin is worse than one that occasionally does not fire.
        /// </summary>
        private static bool HotkeysAllowed()
        {
            if (Player.m_localPlayer == null) return false;          // not in a world yet
            if (global::Console.IsVisible()) return false;
            if (Chat.instance != null && Chat.instance.HasFocus()) return false;
            if (TextInput.IsVisible()) return false;                 // signs, portals, ping names
            if (Minimap.InTextInput()) return false;                 // naming a map pin
            if (Menu.IsVisible()) return false;
            if (InventoryGui.IsVisible()) return false;
            if (StoreGui.IsVisible()) return false;
            if (ZInput.VirtualKeyboardOpen) return false;            // Steam Deck and console on-screen keyboard
            if (SkMenuController.IsOpen) return false;               // our own menu, which has a search box
            return true;
        }

        private void ProcessAutoRun()
        {
            if (!SkConfigEntry.CAutoRunComplete)
            {
                if (SkConfigEntry.CAutoRun != null && SkConfigEntry.CAutoRun.Value == true)
                {
                    if (Player.m_localPlayer != null && Chat.instance != null && Console.instance != null) // Wait until fully logged in
                    {
                        try
                        {
                            // Valheim 1.0: AutoRun re-enabled (the Hearth and Home stub only printed an apology); ProcessCommands is the same entry point HandleChat uses.
                            SkUtilities.Logz(new string[] { "CONTROLLER", "AUTORUN" }, new string[] { "Command List: " + SkConfigEntry.CAutoRunCommand.Value });
                            SkCommandProcessor.PrintOut("==> AutoRun enabled! Command line: " + SkConfigEntry.CAutoRunCommand.Value, SkCommandProcessor.LogTo.Console);
                            SkCommandProcessor.ProcessCommands(SkConfigEntry.CAutoRunCommand.Value, SkCommandProcessor.LogTo.Console);
                        }
                        catch (Exception)
                        {
                            SkUtilities.Logz(new string[] { "CONTROLLER" }, new string[] { "AutoRun Failed. Something went wrong. Command line: " + SkConfigEntry.CAutoRunCommand.Value }, LogType.Warning);
                            SkCommandProcessor.PrintOut("==> AutoRun Failed. Something went wrong. Command line: " + SkConfigEntry.CAutoRunCommand.Value, SkCommandProcessor.LogTo.Console);
                        }
                        finally
                        {
                            SkConfigEntry.CAutoRunComplete = true;
                        }
                    }
                }
                else
                {
                    SkConfigEntry.CAutoRunComplete = true;
                }
            }
        }

        /// <summary>
        /// Reads the fifteen hotkey slots into the dispatch table.
        ///
        /// Every rejection is reported with its slot number. The old code failed in complete silence, which is
        /// what left the one person who reported this unable to tell whether the syntax was wrong or the feature
        /// was dead - it was the feature.
        /// </summary>
        private void BuildHotkeys()
        {
            hotkeys.Clear();

            BepInEx.Configuration.ConfigEntry<string>[] slots =
            {
                SkConfigEntry.CHotkey1,  SkConfigEntry.CHotkey2,  SkConfigEntry.CHotkey3,
                SkConfigEntry.CHotkey4,  SkConfigEntry.CHotkey5,  SkConfigEntry.CHotkey6,
                SkConfigEntry.CHotkey7,  SkConfigEntry.CHotkey8,  SkConfigEntry.CHotkey9,
                SkConfigEntry.CHotkey10, SkConfigEntry.CHotkey11, SkConfigEntry.CHotkey12,
                SkConfigEntry.CHotkey13, SkConfigEntry.CHotkey14, SkConfigEntry.CHotkey15,
            };

            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i + 1;
                try
                {
                    if (slots[i] == null || string.IsNullOrEmpty(slots[i].Value)) continue;
                    string raw = slots[i].Value;

                    // Only the FIRST colon separates, so the command itself may contain one.
                    int sep = raw.IndexOf(':');
                    if (sep < 0)
                    {
                        RejectHotkey(slot, "needs a colon between the key and the command, as in \"l: /fly\".");
                        continue;
                    }

                    string keyPart = raw.Substring(0, sep).Trim();
                    string command = raw.Substring(sep + 1).Trim();

                    if (keyPart.Length != 1)
                    {
                        RejectHotkey(slot, keyPart.Length == 0
                            ? "has no key before the colon."
                            : "the key must be a single character, not \"" + keyPart + "\".");
                        continue;
                    }
                    char key = keyPart[0];
                    if (key < ' ' || key > '~')
                    {
                        RejectHotkey(slot, "'" + key + "' is not a printable ASCII character.");
                        continue;
                    }
                    if (command.Length == 0)
                    {
                        RejectHotkey(slot, "has no command after the colon.");
                        continue;
                    }
                    if (hotkeys.ContainsKey(key))
                    {
                        // Dictionary.Add would throw here, and all fifteen slots used to share one try block, so
                        // a duplicate quietly discarded every hotkey after it.
                        RejectHotkey(slot, "'" + key + "' is already bound; this slot is ignored.");
                        continue;
                    }

                    hotkeys.Add(key, command);
                }
                catch (Exception ex)
                {
                    RejectHotkey(slot, ex.Message);
                }
            }

            if (hotkeys.Count > 0)
            {
                SkUtilities.Logz(new string[] { "CONTROLLER", "HOTKEYS" },
                                 new string[] { hotkeys.Count + " hotkey(s) bound." });
            }
        }

        private static void RejectHotkey(int slot, string why)
        {
            string message = "Hotkey " + slot + " ignored: " + why;
            SkUtilities.Logz(new string[] { "CONTROLLER", "HOTKEYS" }, new string[] { message }, LogType.Warning);
            SkCommandProcessor.PrintOut("==> " + message, SkCommandProcessor.LogTo.Console);
        }

        private void BuildAliases()
        {
            try
            {
                if (SkConfigEntry.CAlias1 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias1.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias1.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias2 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias2.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias2.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias3 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias3.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias3.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias4 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias4.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias4.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias5 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias5.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias5.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias6 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias6.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias6.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias7 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias7.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias7.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias8 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias8.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias8.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias9 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias9.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias9.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias10 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias10.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias10.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias11 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias11.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias11.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias12 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias12.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias12.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias13 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias13.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias13.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias14 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias14.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias14.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }
                if (SkConfigEntry.CAlias15 != null && !string.IsNullOrEmpty(SkConfigEntry.CAlias15.Value))
                {
                    string[] AliasSplit = SkConfigEntry.CAlias15.Value.Split(':');
                    if (AliasSplit.Length == 2)
                    {
                        AliasList.Add(AliasSplit[0], AliasSplit[1]);
                    }
                }

                if (AliasList != null && AliasList.Count > 0)
                {
                    // Valheim 1.0: aliasing is live - DecomposeAlias runs on every command - but this said
                    // otherwise on every startup, and then cleared the HOTKEY dictionary rather than the alias one.
                    SkUtilities.Logz(new string[] { "CONTROLLER", "ALIASES" },
                                     new string[] { AliasList.Count + " alias(es) loaded." });
                    //List<string> trashKeys = new List<string>();
                    //foreach (string key in AliasList.Keys)
                    //{
                    //    if (!SkCommandProcessor.commandList.Keys.Contains(key))
                    //    {
                    //        SkCommandProcessor.commandList.Add(key, "- (Alias) " + AliasList[key].Trim());
                    //    }
                    //    else
                    //    {
                    //        SkCommandProcessor.PrintOut("Alias\t► '" + key + "' is invalid. This command is already in the list.", SkCommandProcessor.LogTo.Console | SkCommandProcessor.LogTo.DebugConsole);
                    //        trashKeys.Add(key);
                    //    }
                    //}
                    //if (trashKeys.Count > 0)
                    //{
                    //    foreach (string key in trashKeys)
                    //    {
                    //        AliasList.Remove(key);
                    //    }
                    //}
                }
            }
            catch (Exception Ex)
            {
                SkCommandProcessor.PrintOut("Something failed while loading command aliases! Check your config file. " + Ex.Message, SkCommandProcessor.LogTo.Console | SkCommandProcessor.LogTo.DebugConsole);
            }
        }

        private void ProcessEnemies(int WindowID)
        {
            if (Player.m_localPlayer != null)
            {
                GUILayout.BeginVertical();
                if (nearbyCharacters?.Count > 0)
                {
                    Vector3 playerPos = Player.m_localPlayer.transform.position;

                    foreach (Character toon in nearbyCharacters)
                    {
                        if(toon != null)
                        {
                            float toonDist = Vector3.Distance(playerPos, toon.transform.position);

                            if (toonDist > 15)
                            {
                                GUI.color = Color.green;
                            }
                            else if (toonDist > 10 && toonDist < 15)
                            {
                                GUI.color = Color.yellow;
                            }
                            else if (toonDist > 5 && toonDist < 10)
                            {
                                GUI.color = Color.yellow + Color.red;
                            }
                            else if (toonDist > 0 && toonDist < 5)
                            {
                                GUI.color = Color.red;
                            }
                            GUILayout.BeginHorizontal();

                            GUILayout.Label("Name: " + toon.GetHoverName());
                            GUILayout.Label("HP: " + Mathf.RoundToInt(toon.GetHealth()) + "/" + toon.GetMaxHealth()
                                + " | Level: " + toon.GetLevel()
                                + " | Dist: " + Mathf.RoundToInt(toonDist));

                            GUILayout.EndHorizontal();
                            GUI.color = Color.white;
                        }
                    }
                }
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0, 0, 10000, 20));
            }
        }

        private class History
        {
            public void Add(string item)
            {
                this.history.Add(item);
                this.index = 0;
            }

            public string Fetch(string current, bool next)
            {
                if (this.index == 0)
                {
                    this.current = current;
                }
                if (this.history.Count == 0)
                {
                    return current;
                }
                this.index += ((!next) ? 1 : -1);
                if (this.history.Count + this.index < 0 || this.history.Count + this.index > this.history.Count - 1)
                {
                    this.index = 0;
                    return this.current;
                }
                return this.history[this.history.Count + this.index];
            }

            public List<string> history = new List<string>();
            private int index;
            private string current;
        }
    }
}

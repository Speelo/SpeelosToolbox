using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkToolbox.Utility
{
    /// <summary>
    /// Speelo's Toolbox: runs the game's own console commands from the menu.
    ///
    /// Vanilla commands are registered without a leading slash, unlike the toolbox's own, and they report back by
    /// appending to the console's chat buffer rather than returning anything. Readouts such as pos or listkeys would
    /// therefore be invisible with the console closed, so Show() snapshots that buffer around the call and puts
    /// whatever was added into a form panel.
    /// </summary>
    internal static class SkRun
    {
        /// <summary>Runs a vanilla console command, e.g. "puke" or "goto 0 0".</summary>
        internal static void Cmd(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            if (Console.instance == null)
            {
                SkCommandProcessor.Notify("The game console is not ready yet.");
                return;
            }
            RunRaw(command);
        }

        /// <summary>
        /// Runs the command with the cheat gate held open.
        ///
        /// Anything the game flags as a cheat is refused by Terminal.ConsoleCommand.IsValid until someone types
        /// devcommands, or the toolbox's own /imacheater, which is what "'puke' is not valid in the current context"
        /// means. A menu button should not need either, and the toolbox's own commands never hit this because they
        /// are all registered as non-cheat. The flag the toolbox already uses for /imacheater is therefore lifted
        /// for the length of the call and put straight back, so clicking a button does not quietly leave cheats on.
        /// </summary>
        private static void RunRaw(string command)
        {
            bool was = SkCommandPatcher.BCheat;
            SkCommandPatcher.BCheat = true;
            try
            {
                Console.instance.TryRunCommand(command);
            }
            finally
            {
                SkCommandPatcher.BCheat = was;
            }
        }

        /// <summary>Runs a command and reports it, using the first line it printed when there is one.</summary>
        internal static void CmdNotify(string command, string fallback)
        {
            List<string> printed = Capture(command);
            SkCommandProcessor.Notify(printed.Count > 0 ? printed[0] : fallback);
        }

        /// <summary>Runs a command and shows everything it printed in a read-only form.</summary>
        internal static void Show(string title, string command, string emptyMessage = null)
        {
            List<string> printed = Capture(command);
            if (printed.Count == 0)
            {
                SkCommandProcessor.Notify(emptyMessage ?? "Nothing to show.");
                return;
            }
            Text(title, string.Join("\n", printed.ToArray()));
        }

        /// <summary>Shows text we already have in the same read-only form.</summary>
        internal static void Text(string title, string body)
        {
            SkMenuController controller = SkMenuController.Instance;
            if (controller == null) return;

            SkMenuController.SkForm form = new SkMenuController.SkForm { Title = title };
            form.Fields.Add(new SkMenuController.SkFormField
            {
                Id = "text",
                Label = "Output",
                Kind = SkMenuController.SkFieldKind.Info,
                TextValue = body,
                Height = 300,
            });
            controller.ShowForm(form); // Cancel is the only way out, which reads as Close here
        }

        /// <summary>Runs a command and returns the lines it added to the console output.</summary>
        internal static List<string> Capture(string command)
        {
            List<string> added = new List<string>();
            if (string.IsNullOrEmpty(command) || Console.instance == null)
            {
                SkCommandProcessor.Notify("The game console is not ready yet.");
                return added;
            }

            List<string> buffer = SkUtilities.GetPrivateField<List<string>>(Console.instance, "m_chatBuffer");
            if (buffer == null)
            {
                RunRaw(command);
                return added;
            }

            // The buffer is capped at 300 lines, so counting before and after under-reports once it is full.
            // A marker line survives that trimming in every realistic case and says exactly where our output starts.
            // It is a zero-width space, so it costs nothing if it is ever briefly visible.
            const string Marker = "\u200b";
            buffer.Add(Marker);

            RunRaw(command);

            int at = buffer.LastIndexOf(Marker);
            if (at < 0) return added;
            for (int i = at + 1; i < buffer.Count; i++)
            {
                if (!string.IsNullOrEmpty(buffer[i])) added.Add(buffer[i]);
            }
            buffer.RemoveAt(at);
            SkUtilities.InvokePrivateMethod(Console.instance, "UpdateChat", null); // repaint without the marker
            return added;
        }
    }
}

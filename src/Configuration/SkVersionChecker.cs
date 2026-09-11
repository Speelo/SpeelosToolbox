namespace SkToolbox.Configuration
{
    /// <summary>
    /// Upstream phoned a pastebin URL on startup to compare versions. The fork does not
    /// call home; Thunderstore Mod Manager handles update notices.
    /// The game's global <c>Version</c> class shadows <c>System.Version</c>, hence the full name.
    /// </summary>
    internal static class SkVersionChecker
    {
        internal static System.Version currentVersion = new System.Version(SkBepInExLoader.VERSION);
        internal static System.Version latestVersion = currentVersion;

        public static bool VersionCurrent()
        {
            return true;
        }
    }
}

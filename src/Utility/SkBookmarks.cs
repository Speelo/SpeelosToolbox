using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SkToolbox.Utility
{
    /// <summary>
    /// Speelo's Toolbox: named places you can teleport back to.
    ///
    /// Stored in the mod's own config as one flat string, because coordinates are worthless in the wrong world:
    /// each entry carries the seed it was taken in, and only entries matching the world you are standing in are
    /// ever shown. The seed rather than the world name, since two worlds can share a name.
    /// </summary>
    internal static class SkBookmarks
    {
        internal class Bookmark
        {
            public string Name = "";
            public Vector3 Pos;
        }

        private const char EntrySeparator = ';';
        private const char FieldSeparator = '|';

        /// <summary>The seed of the world you are in, or 0 when there is none yet.</summary>
        private static int CurrentSeed
        {
            get
            {
                try
                {
                    World world = ZNet.instance != null ? ZNet.instance.GetWorld() : null;
                    return world != null ? world.m_seed : 0;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        /// <summary>Bookmarks taken in the world you are currently in, oldest first.</summary>
        internal static List<Bookmark> ForThisWorld()
        {
            List<Bookmark> found = new List<Bookmark>();
            int seed = CurrentSeed;
            if (seed == 0) return found;

            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] parts = entry.Split(FieldSeparator);
                if (parts.Length != 5) continue;

                int entrySeed;
                float x, y, z;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out entrySeed)) continue;
                if (entrySeed != seed) continue;
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) continue;

                found.Add(new Bookmark { Name = parts[1], Pos = new Vector3(x, y, z) });
            }
            return found;
        }

        /// <summary>Saves a spot under a name, replacing any bookmark of that name in this world.</summary>
        internal static bool Save(string name, Vector3 pos)
        {
            int seed = CurrentSeed;
            if (seed == 0) return false;

            name = Clean(name);
            if (name.Length == 0) return false;

            List<string> kept = new List<string>();
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (Matches(entry, seed, name)) continue; // overwrite rather than duplicate the name
                kept.Add(entry);
            }

            kept.Add(string.Join(FieldSeparator.ToString(), new string[]
            {
                seed.ToString(CultureInfo.InvariantCulture),
                name,
                pos.x.ToString("0.##", CultureInfo.InvariantCulture),
                pos.y.ToString("0.##", CultureInfo.InvariantCulture),
                pos.z.ToString("0.##", CultureInfo.InvariantCulture),
            }));

            Write(kept);
            return true;
        }

        internal static void Delete(string name)
        {
            int seed = CurrentSeed;
            if (seed == 0 || string.IsNullOrEmpty(name)) return;

            List<string> kept = new List<string>();
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (Matches(entry, seed, name)) continue;
                kept.Add(entry);
            }
            Write(kept);
        }

        private static bool Matches(string entry, int seed, string name)
        {
            string[] parts = entry.Split(FieldSeparator);
            if (parts.Length != 5) return false;
            int entrySeed;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out entrySeed)) return false;
            return entrySeed == seed && string.Equals(parts[1], name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The separators are structural, so a name may not contain them.</summary>
        private static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace(EntrySeparator, ' ').Replace(FieldSeparator, ' ').Trim();
        }

        private static string Raw()
        {
            return Configuration.SkConfigEntry.OBookmarks != null
                ? (Configuration.SkConfigEntry.OBookmarks.Value ?? "")
                : "";
        }

        private static void Write(List<string> entries)
        {
            if (Configuration.SkConfigEntry.OBookmarks == null) return;
            Configuration.SkConfigEntry.OBookmarks.Value = string.Join(EntrySeparator.ToString(), entries.ToArray());
        }
    }
}

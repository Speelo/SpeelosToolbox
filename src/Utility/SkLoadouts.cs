using System;
using System.Collections.Generic;
using System.Globalization;

namespace SkToolbox.Utility
{
    /// <summary>
    /// Speelo's Toolbox: saved equipment sets.
    ///
    /// A loadout records what you had equipped by prefab name and quality, not by item reference, because the
    /// items themselves are gone after a death. Restoring re-equips whatever in your inventory matches, and
    /// reports what it could not find rather than pretending it worked.
    ///
    /// Not scoped to a world, unlike bookmarks: gear travels with you, so a set saved in one world is still a
    /// reasonable thing to ask for in another.
    /// </summary>
    internal static class SkLoadouts
    {
        private const char EntrySeparator = ';';
        private const char FieldSeparator = '|';
        private const char ItemSeparator = ',';
        private const char QualitySeparator = ':';

        internal static List<string> Names()
        {
            List<string> names = new List<string>();
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] parts = entry.Split(FieldSeparator);
                if (parts.Length == 2 && parts[0].Length > 0) names.Add(parts[0]);
            }
            return names;
        }

        /// <summary>Records what is equipped right now. Returns how many pieces were captured.</summary>
        internal static int Save(string name, Player player)
        {
            if (player == null) return 0;
            name = Clean(name);
            if (name.Length == 0) return 0;

            List<string> items = new List<string>();
            List<ItemDrop.ItemData> equipped = player.GetInventory().GetEquippedItems();
            if (equipped != null)
            {
                foreach (ItemDrop.ItemData item in equipped)
                {
                    string prefab = PrefabName(item);
                    if (string.IsNullOrEmpty(prefab)) continue;
                    items.Add(prefab + QualitySeparator + item.m_quality.ToString(CultureInfo.InvariantCulture));
                }
            }
            if (items.Count == 0) return 0;

            List<string> kept = new List<string>();
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (NameOf(entry).Equals(name, StringComparison.OrdinalIgnoreCase)) continue; // overwrite
                kept.Add(entry);
            }
            kept.Add(name + FieldSeparator + string.Join(ItemSeparator.ToString(), items.ToArray()));
            Write(kept);
            return items.Count;
        }

        /// <summary>
        /// Re-equips a saved set from what you are carrying. Unequips everything first, so swapping between two
        /// sets does not leave pieces of the old one on. Returns how many were equipped and how many were missing.
        /// </summary>
        internal static void Restore(string name, Player player, out int equippedCount, out int missingCount)
        {
            equippedCount = 0;
            missingCount = 0;
            if (player == null || string.IsNullOrEmpty(name)) return;

            string wanted = null;
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (NameOf(entry).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = entry.Split(FieldSeparator);
                    if (parts.Length == 2) wanted = parts[1];
                    break;
                }
            }
            if (string.IsNullOrEmpty(wanted)) return;

            player.UnequipAllItems();

            // A copy, because equipping mutates the inventory's own list while we walk it.
            List<ItemDrop.ItemData> carried = new List<ItemDrop.ItemData>(player.GetInventory().GetAllItems());

            foreach (string token in wanted.Split(ItemSeparator))
            {
                if (string.IsNullOrEmpty(token)) continue;
                string[] pair = token.Split(QualitySeparator);
                if (pair.Length != 2) continue;
                int quality;
                if (!int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out quality)) quality = 1;

                ItemDrop.ItemData match = Find(carried, pair[0], quality);
                if (match == null)
                {
                    missingCount++;
                    continue;
                }
                if (player.EquipItem(match)) equippedCount++;
                else missingCount++;
                carried.Remove(match); // never equip the same stack twice
            }
        }

        internal static void Delete(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            List<string> kept = new List<string>();
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (NameOf(entry).Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(entry);
            }
            Write(kept);
        }

        /// <summary>Exact quality first, then the best one at or below it, so an upgrade still counts as a match.</summary>
        private static ItemDrop.ItemData Find(List<ItemDrop.ItemData> carried, string prefab, int quality)
        {
            ItemDrop.ItemData best = null;
            foreach (ItemDrop.ItemData item in carried)
            {
                if (item == null) continue;
                if (!string.Equals(PrefabName(item), prefab, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.m_quality == quality) return item;
                if (best == null || Math.Abs(item.m_quality - quality) < Math.Abs(best.m_quality - quality)) best = item;
            }
            return best;
        }

        private static string PrefabName(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            if (item.m_dropPrefab != null) return item.m_dropPrefab.name;
            // Items that never came from a prefab reference still carry their shared name, which is enough to
            // match against another item of the same kind.
            return item.m_shared != null ? item.m_shared.m_name : null;
        }

        private static string NameOf(string entry)
        {
            string[] parts = entry.Split(FieldSeparator);
            return parts.Length > 0 ? parts[0] : "";
        }

        private static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace(EntrySeparator, ' ').Replace(FieldSeparator, ' ')
                       .Replace(ItemSeparator, ' ').Replace(QualitySeparator, ' ').Trim();
        }

        private static string Raw()
        {
            return Configuration.SkConfigEntry.OLoadouts != null
                ? (Configuration.SkConfigEntry.OLoadouts.Value ?? "")
                : "";
        }

        private static void Write(List<string> entries)
        {
            if (Configuration.SkConfigEntry.OLoadouts == null) return;
            Configuration.SkConfigEntry.OLoadouts.Value = string.Join(EntrySeparator.ToString(), entries.ToArray());
        }
    }
}

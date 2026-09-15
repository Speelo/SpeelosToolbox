using System;
using System.Collections.Generic;

namespace SkToolbox.Utility
{
    /// <summary>
    /// Speelo's Toolbox: saved inventories.
    ///
    /// The whole bag, not just what is worn. Valheim already knows how to serialise an inventory - it is what
    /// the character file is made of - so a loadout is that same blob turned into text: every stack, its size,
    /// quality, durability, grid position, crafter name, and which pieces were equipped.
    ///
    /// Restoring REPLACES what you are carrying, because Inventory.Load clears the list before it reads. The
    /// caller is responsible for asking first.
    ///
    /// Not scoped to a world, unlike bookmarks: gear travels with you.
    /// </summary>
    internal static class SkLoadouts
    {
        private const char EntrySeparator = ';';
        private const char FieldSeparator = '|';

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

        /// <summary>How many stacks a saved set holds, for the list label. -1 when it cannot be read.</summary>
        internal static int CountOf(string name)
        {
            string blob = BlobOf(name);
            if (string.IsNullOrEmpty(blob)) return -1;
            try
            {
                Inventory scratch = new Inventory("scratch", null, 8, 4);
                scratch.Load(new ZPackage(blob));
                return scratch.GetAllItems().Count;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Snapshots the whole inventory. Returns how many stacks were captured.</summary>
        internal static int Save(string name, Player player)
        {
            if (player == null) return 0;
            name = Clean(name);
            if (name.Length == 0) return 0;

            Inventory inv = player.GetInventory();
            if (inv == null || inv.GetAllItems().Count == 0) return 0;

            ZPackage pkg = new ZPackage();
            inv.Save(pkg);
            string blob = pkg.GetBase64();

            List<string> kept = new List<string>();
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (NameOf(entry).Equals(name, StringComparison.OrdinalIgnoreCase)) continue; // overwrite
                kept.Add(entry);
            }
            kept.Add(name + FieldSeparator + blob);
            Write(kept);
            return inv.GetAllItems().Count;
        }

        /// <summary>
        /// Replaces the inventory with a saved one, then puts back on whatever was equipped when it was saved.
        ///
        /// Unequipping first matters: Inventory.Load throws the old ItemData objects away, and the character's
        /// equipment slots would otherwise be left pointing at items that no longer exist.
        /// </summary>
        internal static bool Restore(string name, Player player, out int stacks, out int equipped)
        {
            stacks = 0;
            equipped = 0;
            if (player == null) return false;

            string blob = BlobOf(name);
            if (string.IsNullOrEmpty(blob)) return false;

            Inventory inv = player.GetInventory();
            if (inv == null) return false;

            player.UnequipAllItems();
            inv.Load(new ZPackage(blob));

            // A copy: equipping mutates the inventory's own list while we walk it.
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                if (item == null) continue;
                stacks++;
                if (!item.m_equipped) continue;
                // Clear the flag first or EquipItem sees it as already on and does none of the real work.
                item.m_equipped = false;
                if (player.EquipItem(item, false)) equipped++; // no equip effects: this is a bulk restore
            }
            return true;
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

        private static string BlobOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (string entry in Raw().Split(EntrySeparator))
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] parts = entry.Split(FieldSeparator);
                if (parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase)) return parts[1];
            }
            return null;
        }

        private static string NameOf(string entry)
        {
            string[] parts = entry.Split(FieldSeparator);
            return parts.Length > 0 ? parts[0] : "";
        }

        /// <summary>The separators are structural. Base64 never contains them, so only the name needs cleaning.</summary>
        private static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace(EntrySeparator, ' ').Replace(FieldSeparator, ' ').Trim();
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

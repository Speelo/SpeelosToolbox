using System.Collections.Generic;
using UnityEngine;

namespace SkToolbox.Utility
{
    /// <summary>
    /// Speelo's Menu: resolves menu icons from the game's own art instead of shipping any.
    /// Every icon is an existing item's sprite, looked up by prefab name through ObjectDB.
    /// Lookups are cached, and an unknown name resolves to null so the row simply draws without an icon.
    /// </summary>
    internal static class SkIcons
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static int cachedFor = 0;

        /// <summary>Icon of an item prefab, e.g. "Hammer" or "Feathers". Null if the item does not exist yet.</summary>
        internal static Sprite Item(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return null;
            }

            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                return null; // not in a world yet; caller draws without an icon
            }

            // ObjectDB is rebuilt per world load, so drop stale sprites rather than hand back dead references.
            int key = db.GetInstanceID();
            if (cachedFor != key)
            {
                cache.Clear();
                cachedFor = key;
            }

            Sprite cached;
            if (cache.TryGetValue(prefabName, out cached))
            {
                return cached;
            }

            Sprite found = null;
            GameObject prefab;
            if (db.TryGetItemPrefab(prefabName, out prefab) && prefab != null)
            {
                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
                {
                    Sprite[] icons = drop.m_itemData.m_shared.m_icons;
                    if (icons != null && icons.Length > 0)
                    {
                        found = icons[0];
                    }
                }
            }

            cache[prefabName] = found;
            return found;
        }

        /// <summary>First of several candidate item prefabs that resolves, so a rename in a future patch degrades quietly.</summary>
        internal static Sprite First(params string[] prefabNames)
        {
            if (prefabNames == null)
            {
                return null;
            }
            foreach (string name in prefabNames)
            {
                Sprite sprite = Item(name);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }
    }
}

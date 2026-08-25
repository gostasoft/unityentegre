using System;
using UnityEngine;

namespace Metin3Dev.Panel
{
    [CreateAssetMenu(menuName = "Metin3/Entity Prefab Catalog", fileName = "Metin3EntityPrefabCatalog")]
    public sealed class Metin3EntityPrefabCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public GameObject prefab;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public GameObject Resolve(string folder)
        {
            string key = Normalize(folder);
            if (string.IsNullOrEmpty(key)) return null;
            foreach (Entry entry in entries)
                if (entry != null && entry.prefab != null && Normalize(entry.key) == key)
                    return entry.prefab;
            return null;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            value = value.Replace('\\', '/').Trim().TrimEnd('/');
            int slash = value.LastIndexOf('/');
            if (slash >= 0) value = value.Substring(slash + 1);
            return value.ToLowerInvariant().Replace("_lod_01", string.Empty).Replace("_lod", string.Empty);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metin3Dev.Panel;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Metin3Dev.Panel.Editor
{
    public static class Metin3EntityPrefabCatalogBuilder
    {
        const string AssetPath = "Assets/Metin3Panel/Resources/Metin3EntityPrefabCatalog.asset";

        [InitializeOnLoadMethod]
        static void BuildWhenMissing()
        {
            Metin3EntityPrefabCatalog existing = AssetDatabase.LoadAssetAtPath<Metin3EntityPrefabCatalog>(AssetPath);
            if (existing != null && existing.entries != null && existing.entries.Length > 0) return;
            EditorApplication.delayCall += Build;
        }

        [DidReloadScripts]
        static void BuildAfterScriptsReload()
        {
            BuildWhenMissing();
        }

        [MenuItem("Tools/Metin3/Build Runtime Mob Catalog", priority = 60)]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath) ?? "Assets/Metin3Panel/Resources");
            Metin3EntityPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<Metin3EntityPrefabCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<Metin3EntityPrefabCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }
            Dictionary<string, GameObject> candidates = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf("_lod_", StringComparison.OrdinalIgnoreCase) >= 0 || IsMotion(file)) continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                Add(candidates, file, asset);
                string parent = Path.GetFileName(Path.GetDirectoryName(path));
                if (!string.IsNullOrWhiteSpace(parent) && string.Equals(file, parent, StringComparison.OrdinalIgnoreCase)) Add(candidates, parent, asset);
            }
            catalog.entries = candidates.OrderBy(pair => pair.Key).Select(pair => new Metin3EntityPrefabCatalog.Entry { key = pair.Key, prefab = pair.Value }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Metin3 Panel] Runtime mob kataloğu hazır: {catalog.entries.Length} model eşleşmesi. Eksik modeller oyunda işaretleyiciyle gösterilir.");
        }

        static void Add(IDictionary<string, GameObject> entries, string key, GameObject asset)
        {
            key = Metin3EntityPrefabCatalog.Normalize(key);
            if (!string.IsNullOrEmpty(key) && !entries.ContainsKey(key)) entries[key] = asset;
        }

        static bool IsMotion(string value)
        {
            string[] names = { "walk", "run", "wait", "attack", "damage", "dead", "death", "combo", "skill", "selected", "not_selected", "pick_up" };
            return names.Any(name => value.Equals(name, StringComparison.OrdinalIgnoreCase) || value.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase));
        }
    }
}

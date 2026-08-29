using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Metin3Dev.Panel;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Metin3Dev.Panel.Editor
{
    public static class Metin3EntityPrefabCatalogBuilder
    {
        const string AssetPath = "Assets/Metin3Panel/Resources/Metin3EntityPrefabCatalog.asset";
        const string GeneratedRoot = "Assets/Metin2/EntitiesGenerated";

        [InitializeOnLoadMethod]
        static void BuildWhenMissing()
        {
            Metin3EntityPrefabCatalog existing = AssetDatabase.LoadAssetAtPath<Metin3EntityPrefabCatalog>(AssetPath);
            if (existing != null && existing.Resolve("stray_dog") != null && existing.Resolve("fire_dragon") != null && existing.Resolve("arms") != null && existing.Resolve("metinstone_05") != null) return;
            EditorApplication.delayCall += ImportOriginalModels;
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
            Debug.Log($"[Metin3 Panel] Runtime mob kataloğu hazır: {catalog.entries.Length} model eşleşmesi.");
        }

        [MenuItem("Tools/Metin3/Import All Original Mob NPC Metin Models", priority = 59)]
        public static void ImportOriginalModels()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string pack = Path.Combine(desktop, "Metin 2 Mobil Dönüşüm Pack");
            (string source, string destination)[] sources =
            {
                (Path.Combine(pack, "Monster", "ymir work", "monster"), Path.Combine(GeneratedRoot, "Monster")),
                (Path.Combine(pack, "monster2", "ymir work", "monster2"), Path.Combine(GeneratedRoot, "Monster2")),
                (Path.Combine(pack, "NPC", "ymir work", "npc"), Path.Combine(GeneratedRoot, "NPC")),
                (Path.Combine(pack, "npc2", "ymir work", "npc2"), Path.Combine(GeneratedRoot, "NPC2")),
            };
            if (sources.Any(entry => !Directory.Exists(entry.source)))
            {
                Debug.LogError($"[Metin3 Panel] Dönüştürülmüş model paketi bulunamadı: {pack}");
                return;
            }
            Directory.CreateDirectory(GeneratedRoot);
            foreach ((string source, string destination) in sources) CopyTree(source, destination);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
            BuildExactCatalog();
        }

        static void BuildExactCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath) ?? "Assets/Metin3Panel/Resources");
            Metin3EntityPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<Metin3EntityPrefabCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<Metin3EntityPrefabCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }
            Dictionary<string, GameObject> candidates = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { GeneratedRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                string parent = Path.GetFileName(Path.GetDirectoryName(path));
                if (!file.Equals(parent, StringComparison.OrdinalIgnoreCase)) continue;
                Add(candidates, parent, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            foreach (string absoluteMsm in Directory.GetFiles(Path.GetFullPath(GeneratedRoot), "*.msm", SearchOption.AllDirectories))
            {
                string key = Path.GetFileNameWithoutExtension(absoluteMsm);
                string directory = Path.GetDirectoryName(absoluteMsm) ?? string.Empty;
                string script = File.ReadAllText(absoluteMsm);
                Match modelMatch = Regex.Match(script, "(?:BaseModelFileName|Model)\\s+\\\"(?<path>[^\\\"]+\\.gr2)\\\"", RegexOptions.IgnoreCase);
                string modelName = modelMatch.Success ? Path.GetFileNameWithoutExtension(modelMatch.Groups["path"].Value.Replace('\\', '/')) : Path.GetFileName(directory);
                string modelPath = Directory.GetFiles(directory, modelName + ".fbx", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (string.IsNullOrEmpty(modelPath)) continue;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ToAssetPath(modelPath));
                if (model == null) continue;
                Match skinMatch = Regex.Match(script, "TargetSkin\\s+\\\"(?<path>[^\\\"]+\\.(?:dds|png|tga))\\\"", RegexOptions.IgnoreCase);
                if (skinMatch.Success)
                {
                    string texturePath = FindTexture(directory, Path.GetFileNameWithoutExtension(skinMatch.Groups["path"].Value.Replace('\\', '/')));
                    if (!string.IsNullOrEmpty(texturePath)) model = CreateSkinnedPrefab(key, model, texturePath);
                }
                Add(candidates, key, model);
            }
            catalog.entries = candidates.OrderBy(pair => pair.Key).Select(pair => new Metin3EntityPrefabCatalog.Entry { key = pair.Key, prefab = pair.Value }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Metin3 Panel] Orijinal VNUM model kataloğu hazır: {catalog.entries.Length} gerçek mob/NPC/metin modeli.");
        }

        static GameObject CreateSkinnedPrefab(string key, GameObject source, string texturePath)
        {
            string prefabFolder = GeneratedRoot + "/Prefabs";
            string materialFolder = GeneratedRoot + "/Materials";
            Directory.CreateDirectory(prefabFolder);
            Directory.CreateDirectory(materialFolder);
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ToAssetPath(texturePath));
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index] != null ? new Material(materials[index]) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.name = key + "_" + index;
                    material.mainTexture = texture;
                    string materialPath = $"{materialFolder}/{key}_{renderer.name}_{index}.mat";
                    AssetDatabase.DeleteAsset(materialPath);
                    AssetDatabase.CreateAsset(material, materialPath);
                    materials[index] = material;
                }
                renderer.sharedMaterials = materials;
            }
            string prefabPath = $"{prefabFolder}/{key}.prefab";
            AssetDatabase.DeleteAsset(prefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        static string FindTexture(string directory, string name)
        {
            foreach (string extension in new[] { ".png", ".dds", ".tga", ".jpg", ".jpeg" })
            {
                string candidate = Path.Combine(directory, name + extension);
                if (File.Exists(candidate)) return candidate;
            }
            return string.Empty;
        }

        static string ToAssetPath(string absolute) => absolute.Replace('\\', '/').Replace(Path.GetFullPath(".").Replace('\\', '/') + "/", string.Empty);

        static void CopyTree(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(directory.Replace(source, destination));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = file.Replace(source, destination);
                if (!File.Exists(target) || new FileInfo(file).Length != new FileInfo(target).Length) File.Copy(file, target, true);
            }
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

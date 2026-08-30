using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Metin2Dev.Editor
{
    public static class Metin2ItemAssetImporter
    {
        const string TargetRoot = "Assets/Metin2/Generated/Items/Resources/Metin2Items";
        const string ItemListTarget = TargetRoot + "/item_list.txt";
        const string ArmorMapTarget = TargetRoot + "/armor_shapes.txt";

        [InitializeOnLoadMethod]
        static void ScheduleFirstImport()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ItemListTarget) && File.Exists(ArmorMapTarget))
                {
                    ValidateImportedAssets();
                    return;
                }
                if (!EditorApplication.isPlayingOrWillChangePlaymode && TryFindSources(out _, out _, out _, false))
                    Rebuild();
            };
        }

        [MenuItem("Tools/Metin2/Rebuild Item Assets", priority = 205)]
        public static void Rebuild()
        {
            if (!TryFindSources(out string convertedRoot, out string itemList, out string itemProto, true)) return;
            string iconRoot = Path.Combine(convertedRoot, "icon", "item");
            string modelRoot = Path.Combine(convertedRoot, "item");
            try
            {
                Directory.CreateDirectory(TargetRoot);
                AssetDatabase.StartAssetEditing();
                File.Copy(itemList, ItemListTarget, true);
                File.Copy(itemProto, TargetRoot + "/item_proto.txt", true);
                CopyTree(iconRoot, TargetRoot + "/Icons", path => IsExtension(path, ".tga", ".png", ".jpg", ".jpeg"));
                CopyTree(modelRoot, TargetRoot + "/Models", path => IsExtension(path, ".fbx", ".png", ".tga", ".jpg", ".jpeg", ".dds", ".mat"));
                ImportArmorShapes();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Metin2 Item Importer", exception.Message, "Tamam");
                return;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureIconImporters(TargetRoot + "/Icons");
            AssetDatabase.SaveAssets();
            Debug.Log("[Metin2 Items] item_list/item_proto doğrulandı; gerçek ikon ve FBX varlıkları oyuna bağlandı.");
            ValidateImportedAssets();
        }

        [MenuItem("Tools/Metin2/Validate Item Assets", priority = 206)]
        public static void ValidateImportedAssets()
        {
            TextAsset list = Resources.Load<TextAsset>("Metin2Items/item_list");
            TextAsset proto = Resources.Load<TextAsset>("Metin2Items/item_proto");
            Texture2D swordIcon = Resources.Load<Texture2D>("Metin2Items/Icons/00010");
            Texture2D potionIcon = Resources.Load<Texture2D>("Metin2Items/Icons/27001");
            GameObject swordModel = Resources.Load<GameObject>("Metin2Items/Models/weapon/00010");
            TextAsset armorShapes = Resources.Load<TextAsset>("Metin2Items/armor_shapes");
            GameObject warriorArmor = Resources.Load<GameObject>("Metin2Items/ArmorModels/warrior_m/warrior_nahan");
            Texture2D warriorArmorTexture = Resources.Load<Texture2D>("Metin2Items/ArmorTextures/warrior_m/warrior_nahan");
            if (list == null || proto == null || swordIcon == null || potionIcon == null || swordModel == null ||
                armorShapes == null || warriorArmor == null || warriorArmorTexture == null)
            {
                Debug.LogError("[Metin2 Items] Doğrulama başarısız: proto, gerçek ikon, silah FBX'i veya shape.msm zırh kaynağı eksik.");
                return;
            }
            Debug.Log("[Metin2 Items] Doğrulama başarılı: VNUM 10 ikon+FBX, VNUM 27001 ikon ve ShapeIndex 3 savaşçı zırhı hazır.");
        }

        static bool TryFindSources(out string convertedRoot, out string itemList, out string itemProto, bool showError)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            convertedRoot = FirstDirectory(
                Path.Combine(desktop, "Metin 2 Mobil Dönüşüm Pack"),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Metin2,5", "Converted")));
            itemList = convertedRoot == null ? null : FirstFile(
                Path.Combine(convertedRoot, "locale_tr", "locale", "tr", "item_list.txt"),
                Path.Combine(convertedRoot, "locale", "tr", "item_list.txt"));
            itemProto = FirstFile(
                @"C:\Metin3Web\item_proto.txt",
                Path.Combine(desktop, "Seyir2 Bilgiler", "Proto", "item_proto.txt"),
                Path.Combine(desktop, "Rastgele", "item_proto.txt"));
            bool valid = convertedRoot != null && itemList != null && itemProto != null &&
                         Directory.Exists(Path.Combine(convertedRoot, "icon", "item")) &&
                         Directory.Exists(Path.Combine(convertedRoot, "item"));
            if (!valid && showError)
                EditorUtility.DisplayDialog("Metin2 Item Importer",
                    "Dönüştürülmüş item paketi, item_list.txt veya item_proto.txt bulunamadı.", "Tamam");
            return valid;
        }

        static void CopyTree(string source, string target, Func<string, bool> include)
        {
            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories).Where(include).ToArray();
            for (int index = 0; index < files.Length; index++)
            {
                string relative = files[index].Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? target);
                File.Copy(files[index], destination, true);
                if (index % 50 == 0)
                    EditorUtility.DisplayProgressBar("Metin2 Item Assets", relative, index / (float)Math.Max(1, files.Length));
            }
        }

        static void ConfigureIconImporters(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                bool changed = importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != TextureImporterCompression.CompressedHQ;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                if (changed) importer.SaveAndReimport();
            }
        }

        static void ImportArmorShapes()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shapeRoot = Path.Combine(desktop, "RootPackUnlocker", "Source");
            Directory.CreateDirectory(TargetRoot + "/ArmorModels");
            Directory.CreateDirectory(TargetRoot + "/ArmorTextures");
            Dictionary<string, string> models = IndexAssets(Application.dataPath, ".fbx");
            Dictionary<string, string> textures = IndexAssets(Application.dataPath, ".png", ".tga", ".jpg", ".jpeg");
            List<string> map = new List<string>();
            foreach (string key in new[] { "warrior_m", "warrior_w", "assassin_m", "assassin_w", "sura_m", "sura_w", "shaman_m", "shaman_w" })
            {
                string shapePath = Path.Combine(shapeRoot, key + ".msm");
                if (!File.Exists(shapePath)) continue;
                int shapeIndex = -1;
                string modelName = null;
                string textureName = null;
                foreach (string raw in File.ReadLines(shapePath).Concat(new[] { "Group ShapeData_END" }))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("Group ShapeData", StringComparison.OrdinalIgnoreCase))
                    {
                        CommitArmorShape(key, shapeIndex, modelName, textureName, models, textures, map);
                        shapeIndex = -1; modelName = null; textureName = null;
                        continue;
                    }
                    if (line.StartsWith("ShapeIndex", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = Regex.Match(line, @"-?\d+").Value;
                        int.TryParse(value, out shapeIndex);
                    }
                    else if (line.StartsWith("Model", StringComparison.OrdinalIgnoreCase)) modelName = Quoted(line);
                    else if (line.StartsWith("TargetSkin", StringComparison.OrdinalIgnoreCase)) textureName = Quoted(line);
                }
            }
            File.WriteAllLines(ArmorMapTarget, map.Distinct(StringComparer.OrdinalIgnoreCase));
            Debug.Log($"[Metin2 Items] shape.msm üzerinden {map.Count} gerçek zırh görünümü eşlendi.");
        }

        static void CommitArmorShape(string key, int shapeIndex, string modelName, string textureName,
            Dictionary<string, string> models, Dictionary<string, string> textures, List<string> map)
        {
            if (shapeIndex < 0 || string.IsNullOrWhiteSpace(modelName)) return;
            string modelBase = Path.GetFileNameWithoutExtension(modelName);
            if (!models.TryGetValue(modelBase, out string modelSource)) return;
            string modelRelative = key + "/" + modelBase;
            string modelTarget = TargetRoot + "/ArmorModels/" + modelRelative + Path.GetExtension(modelSource).ToLowerInvariant();
            Directory.CreateDirectory(Path.GetDirectoryName(modelTarget) ?? TargetRoot);
            File.Copy(modelSource, modelTarget, true);

            string textureRelative = string.Empty;
            string textureBase = Path.GetFileNameWithoutExtension(textureName ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(textureBase) && textures.TryGetValue(textureBase, out string textureSource))
            {
                textureRelative = key + "/" + textureBase;
                string textureTarget = TargetRoot + "/ArmorTextures/" + textureRelative + Path.GetExtension(textureSource).ToLowerInvariant();
                Directory.CreateDirectory(Path.GetDirectoryName(textureTarget) ?? TargetRoot);
                File.Copy(textureSource, textureTarget, true);
            }
            map.Add(key + "\t" + shapeIndex + "\t" + modelRelative + "\t" + textureRelative);
        }

        static Dictionary<string, string> IndexAssets(string root, params string[] extensions)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))) continue;
                if (path.IndexOf(Path.Combine("Metin2", "Generated"), StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string key = Path.GetFileNameWithoutExtension(path);
                if (!result.ContainsKey(key)) result.Add(key, path);
            }
            return result;
        }

        static string Quoted(string line)
        {
            Match match = Regex.Match(line ?? string.Empty, "\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        static bool IsExtension(string path, params string[] extensions) =>
            extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

        static string FirstDirectory(params string[] paths) => paths.FirstOrDefault(Directory.Exists);
        static string FirstFile(params string[] paths) => paths.FirstOrDefault(File.Exists);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metin2Dev.Frontend.Editor
{
    public static class Metin2FrontendBuilder
    {
        const string Root = "Assets/Metin2/Frontend";
        const string ArtRoot = Root + "/Art";
        const string BackgroundRoot = ArtRoot + "/Backgrounds";
        const string CharacterRoot = ArtRoot + "/Characters";
        const string UiRoot = ArtRoot + "/UI";
        const string EmpireMapPath = Root + "/Resources/Metin2Frontend/empire_map.png";
        const string ConfigPath = Root + "/Metin2FrontendConfig.asset";
        const string SceneFolder = Root + "/Scenes";
        const string ScenePath = SceneFolder + "/Metin2_Intro.unity";
        const string ReportPath = "Assets/Metin2/Generated/FrontendBuildReport.txt";

        static readonly string[] RaceFolders =
        {
            "warrior_m", "warrior_w",
            "assassin_m", "assassin_w",
            "sura_m", "sura_w",
            "shaman_m", "shaman_w",
        };
        static readonly string[] PortraitPaths =
        {
            Root + "/Resources/Metin2Frontend/Portraits/face_warrior.png",
            Root + "/Resources/Metin2Frontend/Portraits/face_assassin.png",
            Root + "/Resources/Metin2Frontend/Portraits/face_sura.png",
            Root + "/Resources/Metin2Frontend/Portraits/face_shaman.png",
        };

        [MenuItem("Tools/Metin2/Open Login Flow", priority = 20)]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
            {
                const string message = "Kaydedilmiş giriş sahnesi bulunamadı: " + ScenePath +
                    "\n\nGüvenlik için bu menü yeni sahne üretmez ve mevcut tasarımın üzerine yazmaz.";
                Debug.LogError("[Metin2 Frontend] " + message);
                EditorUtility.DisplayDialog("Metin2 - Giriş Sahnesi Bulunamadı", message, "Tamam");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Metin2/Reset Local Login Data", priority = 22)]
        public static void ResetLocalData()
        {
            string lastAccount = PlayerPrefs.GetString("Metin2.Frontend.LastAccount.v2", string.Empty);
            if (!string.IsNullOrWhiteSpace(lastAccount))
                PlayerPrefs.DeleteKey("Metin2.Frontend.Account.v2." + lastAccount.Trim().ToLowerInvariant());
            PlayerPrefs.DeleteKey("Metin2.Frontend.LastAccount.v2");
            PlayerPrefs.DeleteKey("Metin2.Frontend.Save.v1");
            PlayerPrefs.Save();
            Debug.Log("[Metin2 Frontend] Last local account, empire and character slots were reset.");
        }

        [MenuItem("Tools/Metin2/Validate Frontend Character Models", priority = 23)]
        public static void ValidateCharacterModels()
        {
            Metin2FrontendConfig config = AssetDatabase.LoadAssetAtPath<Metin2FrontendConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("Frontend config is missing: " + ConfigPath);

            List<string> problems = new List<string>();
            for (int index = 0; index < RaceFolders.Length; index++)
            {
                GameObject prefab = config.racePrefabs != null && index < config.racePrefabs.Length
                    ? config.racePrefabs[index]
                    : null;
                if (prefab == null) problems.Add(RaceFolders[index] + ": FBX missing");
                else if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                    problems.Add(RaceFolders[index] + ": FBX has no renderer");

                if (config.bodyTextures == null || index >= config.bodyTextures.Length || config.bodyTextures[index] == null)
                    problems.Add(RaceFolders[index] + ": body texture missing");
                if (config.faceTextures == null || index >= config.faceTextures.Length || config.faceTextures[index] == null)
                    problems.Add(RaceFolders[index] + ": face texture missing");
            }
            foreach (string portraitPath in PortraitPaths)
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(portraitPath) == null)
                    problems.Add(Path.GetFileNameWithoutExtension(portraitPath) + ": class portrait missing");

            if (problems.Count > 0)
                throw new InvalidOperationException("Frontend character validation failed:\n" + string.Join("\n", problems));
            Debug.Log("[Metin2 Frontend] All 8 male/female character FBX models, textures and 4 original class portraits are valid.");
        }

        public static void BuildFromCommandLine()
        {
            BuildInternal(false);
        }

        public static void BuildPreviewPlayerFromCommandLine()
        {
            BuildInternal(false);
            string output = Path.GetFullPath("Builds/FrontendPreview/Metin2Frontend.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? string.Empty);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Frontend preview build failed: " + report.summary.result);
            Debug.Log("[Metin2 Frontend] Preview player built: " + output);
        }

        static void BuildInternal(bool showDialog)
        {
            EnsureFolder(Root);
            EnsureFolder(SceneFolder);
            EnsureFolder("Assets/Metin2/Generated");
            List<string> missing = new List<string>();

            ImportFrontendArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Metin2FrontendConfig config = AssetDatabase.LoadAssetAtPath<Metin2FrontendConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<Metin2FrontendConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            config.loginBackground = Load<Texture2D>(BackgroundRoot + "/login.jpg", missing);
            config.serverBackground = Load<Texture2D>(BackgroundRoot + "/serverlist.jpg", missing);
            config.selectionBackground = Load<Texture2D>(BackgroundRoot + "/select.jpg", missing);
            config.empireMap = Load<Texture2D>(EmpireMapPath, missing);
            config.loadingBackgrounds = new Texture2D[4];
            for (int i = 0; i < config.loadingBackgrounds.Length; i++)
                config.loadingBackgrounds[i] = Load<Texture2D>(BackgroundRoot + "/loading" + i + ".jpg", missing);
            config.inventoryBoardFrame = Load<Sprite>(UiRoot + "/inventory_board.png", missing);
            config.inventoryBoardCenter = Load<Sprite>(UiRoot + "/inventory_board_center.png", missing);
            config.previewShader = Load<Shader>(Root + "/Runtime/Metin2CharacterPreviewUnlit.shader", missing);

            config.racePrefabs = new GameObject[RaceFolders.Length];
            config.hairPrefabs = new GameObject[RaceFolders.Length];
            config.bodyTextures = new Texture2D[RaceFolders.Length];
            config.faceTextures = new Texture2D[RaceFolders.Length];
            config.hairTextures = new Texture2D[RaceFolders.Length];
            for (int i = 0; i < RaceFolders.Length; i++)
            {
                string race = RaceFolders[i];
                string raceFolder = CharacterRoot + "/" + race;
                string className = race.Substring(0, race.LastIndexOf('_'));
                config.racePrefabs[i] = Load<GameObject>(raceFolder + "/" + race + ".fbx", missing);
                config.hairPrefabs[i] = Load<GameObject>(raceFolder + "/" + race + "_hair.fbx", missing);
                config.bodyTextures[i] = Load<Texture2D>(raceFolder + "/" + BodyTexture(className), missing);
                config.faceTextures[i] = Load<Texture2D>(raceFolder + "/" + className + "_face.png", missing);
                config.hairTextures[i] = Load<Texture2D>(raceFolder + "/" + HairTexture(className), missing);
            }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Metin2_Intro";
            config = AssetDatabase.LoadAssetAtPath<Metin2FrontendConfig>(ConfigPath);
            if (config == null)
                throw new InvalidOperationException("Frontend config could not be reloaded: " + ConfigPath);
            GameObject root = new GameObject("Metin2 Frontend");
            Metin2FrontendController controller = root.AddComponent<Metin2FrontendController>();
            controller.Configure(config);
            controller.BuildEditableHierarchy();
            Canvas editableCanvas = root.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(candidate => candidate.name == "Metin2 Frontend Editable Layout");
            if (editableCanvas != null)
                Metin2FrontendCompositionAuthoring.EnsureCanvas(editableCanvas);
            Transform characterSelection = root.transform.Find(
                "Metin2 Frontend Editable Layout/Character Selection");
            if (characterSelection != null)
                Metin2FrontendHierarchyPreview.PrepareCharacterSlotTemplate(characterSelection);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            UpdateBuildSettings();
            WriteReport(config, missing);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log("[Metin2 Frontend] Login flow built: " + ScenePath + ". Missing references: " + missing.Count);

            if (showDialog && !Application.isBatchMode)
            {
                string message = missing.Count == 0
                    ? "Giriş, imparatorluk, karakter seçme, karakter oluşturma ve yükleme akışı hazır."
                    : "Akış hazır; " + missing.Count + " eksik kaynak rapora yazıldı.";
                EditorUtility.DisplayDialog("Metin2 Frontend", message + "\n\n" + ReportPath, "Tamam");
            }
        }

        static void ImportFrontendArt()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { BackgroundRoot, CharacterRoot, UiRoot,
                         Root + "/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool background = path.StartsWith(BackgroundRoot, StringComparison.OrdinalIgnoreCase);
                bool userInterface = path.StartsWith(UiRoot, StringComparison.OrdinalIgnoreCase);
                bool empireMap = string.Equals(path, EmpireMapPath, StringComparison.OrdinalIgnoreCase);
                bool hair = Path.GetFileNameWithoutExtension(path).IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0;
                importer.textureType = userInterface ? TextureImporterType.Sprite : TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = !background && !userInterface && !empireMap;
                importer.alphaIsTransparency = hair || userInterface || empireMap;
                importer.wrapMode = background || empireMap || (userInterface && path.EndsWith("inventory_board.png", StringComparison.OrdinalIgnoreCase))
                    ? TextureWrapMode.Clamp
                    : TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = background ? 2048 : 1024;
                importer.textureCompression = userInterface ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                if (userInterface)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100f;
                    importer.spriteBorder = path.EndsWith("inventory_board.png", StringComparison.OrdinalIgnoreCase)
                        ? new Vector4(32f, 32f, 32f, 32f)
                        : Vector4.zero;
                }
                importer.SaveAndReimport();
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.isReadable = false;
                importer.materialSearch = ModelImporterMaterialSearch.Local;
                importer.SaveAndReimport();
            }

        }

        static void UpdateBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(item => !string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            foreach (string mapName in new[] { "metin2_map_a1", "metin2_map_b1", "metin2_map_c1" })
            {
                string mapPath = AssetDatabase.FindAssets(mapName + " t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), mapName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(mapPath) || scenes.Any(item => string.Equals(item.path, mapPath, StringComparison.OrdinalIgnoreCase))) continue;
                scenes.Add(new EditorBuildSettingsScene(mapPath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void WriteReport(Metin2FrontendConfig config, List<string> missing)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Metin2 Frontend Build Report");
            report.AppendLine(DateTime.Now.ToString("u"));
            report.AppendLine();
            report.AppendLine("Source flow:");
            report.AppendLine("Login -> Empire -> Character Select -> Character Create -> Loading -> Empire Map");
            report.AppendLine();
            report.AppendLine("Backgrounds: " + Count(config.loadingBackgrounds.Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[]
            {
                config.loginBackground, config.serverBackground, config.selectionBackground, config.empireMap,
            })) + "/8");
            report.AppendLine("Race previews: " + Count(config.racePrefabs.Cast<UnityEngine.Object>()) + "/8");
            report.AppendLine("Hair previews: " + Count(config.hairPrefabs.Cast<UnityEngine.Object>()) + "/8");
            report.AppendLine("Body textures: " + Count(config.bodyTextures.Cast<UnityEngine.Object>()) + "/8");
            report.AppendLine("Face textures: " + Count(config.faceTextures.Cast<UnityEngine.Object>()) + "/8");
            report.AppendLine("Hair textures: " + Count(config.hairTextures.Cast<UnityEngine.Object>()) + "/8");
            report.AppendLine();
            report.AppendLine("Missing references:");
            if (missing.Count == 0) report.AppendLine("- none");
            else foreach (string item in missing.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item)) report.AppendLine("- " + item);

            string absolute = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? string.Empty);
            File.WriteAllText(absolute, report.ToString(), Encoding.UTF8);
        }

        static int Count(IEnumerable<UnityEngine.Object> objects)
        {
            return objects.Count(item => item != null);
        }

        static string BodyTexture(string className)
        {
            switch (className)
            {
                case "warrior": return "warrior_novice_red.png";
                case "assassin": return "assassin_novice_green.png";
                case "sura": return "sura_novice_red.png";
                case "shaman": return "shaman_novice_green.png";
                default: return className + "_novice.png";
            }
        }

        static string HairTexture(string className)
        {
            return className + "_hair_01.png";
        }

        static T Load<T>(string path, List<string> missing) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) missing.Add(path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

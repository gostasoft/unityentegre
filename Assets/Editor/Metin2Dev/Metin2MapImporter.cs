using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Metin2Dev
{
    /// <summary>Builds Unity scenes from extracted Metin2 maps and converted FBX/PNG assets.</summary>
    public static class Metin2MapImporter
    {
        const string Output = "Assets/Metin2/Generated";
        const string Raw = "Assets/Metin2/Raw";
        const float MetinUnitsPerUnityUnit = 100f;
        const float BuildingModelScale = 1.5f;
        const float TerrainTextureTileSize = 20f;
        const string EnvironmentProfilePath = Output + "/Metin2Environment.asset";
        const string SkyboxMaterialPath = Output + "/Metin2Skybox.mat";
        static readonly string[] ModelExtensions = { ".fbx", ".obj", ".dae" };
        static readonly string[] ImageExtensions = { ".png", ".dds", ".tga", ".jpg", ".jpeg", ".bmp" };
        static readonly Dictionary<string, string> ImportedModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ImportedModelDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> ImportedAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> ImportedEffectPrefabs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Tools/Metin2/Build All Maps", priority = 100)]
        public static void BuildAllMaps()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            List<string> roots = FindRoots(project);
            if (roots.Count == 0)
            {
                string selected = EditorUtility.OpenFolderPanel("Select extracted Metin2 data root", project, "");
                if (!string.IsNullOrEmpty(selected)) roots.Add(selected);
            }
            if (roots.Count == 0) return;

            Report report = new Report();
            try
            {
                Folders(Output); Folders(Raw); Folders(Output + "/Maps"); Folders(Output + "/Scenes");
                ImportedModels.Clear(); ImportedModelDirectories.Clear(); ImportedAssets.Clear(); ImportedEffectPrefabs.Clear();
                List<string> all = roots.SelectMany(SafeFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                int archives = all.Count(p => p.EndsWith(".eix", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".epk", StringComparison.OrdinalIgnoreCase));
                FileIndex models = new FileIndex(all.Where(p => ModelExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())), ".fbx");
                FileIndex textures = new FileIndex(all.Where(p => ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())), ".png");
                FileIndex textFiles = new FileIndex(all.Where(p => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
                FileIndex effects = new FileIndex(all.Where(IsEffectFile), ".mse");
                Dictionary<string, PropertyEntry> properties = LoadProperties(all, report);
                List<MapSource> maps = DiscoverMaps(all);

                if (archives > 0 && models.Count == 0)
                    report.Warnings.Add($"Found {archives} EIX/EPK archives but no FBX/OBJ/DAE files. Extract packs and convert GR2 models first.");
                if (maps.Count == 0) report.Errors.Add("No extracted Setting.txt or MapProperty.txt map roots were found.");

                for (int i = 0; i < maps.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Metin2 - Build All Maps", $"[{i + 1}/{maps.Count}] {maps[i].Name}", i / (float)Math.Max(1, maps.Count));
                    try { BuildMap(maps[i], properties, models, textures, textFiles, effects, report); }
                    catch (Exception ex) { report.Errors.Add(maps[i].Name + ": " + ex); }
                }

                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                report.Save(roots, maps.Count, properties.Count, models.Count, textures.Count);
                Debug.Log($"Metin2 import finished: {report.BuiltMaps} maps, {report.TerrainTiles} terrain tiles, {report.PlacedObjects} objects, {report.Missing.Count} missing references. Report: {Output}/ImportReport.txt");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Tools/Metin2/Open Last Import Report", priority = 101)]
        static void OpenReport()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(Output + "/ImportReport.txt");
            if (asset != null) AssetDatabase.OpenAsset(asset); else Debug.LogWarning("No Metin2 import report exists yet.");
        }

        [MenuItem("Tools/Metin2/Upgrade Generated Scenes", priority = 102)]
        public static void UpgradeGeneratedScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = SceneManager.GetActiveScene().path;
            string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { Output + "/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            Report effectReport = new Report();
            LoadEffectSources(out Dictionary<string, PropertyEntry> properties, out FileIndex effects, out FileIndex textures, out Dictionary<string, MapSource> maps, effectReport);
            int upgraded = 0, collidersAdded = 0, buildingsScaled = 0, wallsAdjusted = 0, bridgesReset = 0;
            try
            {
                Folders(Output);
                ImportedAssets.Clear(); ImportedEffectPrefabs.Clear();
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Metin2 - Upgrade Generated Scenes", $"[{i + 1}/{scenePaths.Length}] {Path.GetFileNameWithoutExtension(scenePaths[i])}", i / (float)Math.Max(1, scenePaths.Length));
                    Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
                    GameObject root = scene.GetRootGameObjects().FirstOrDefault();
                    if (root == null) continue;
                    if (maps.TryGetValue(scene.name, out MapSource map)) RebuildMapEffects(map, root, properties, effects, textures, effectReport);
                    buildingsScaled += ApplyBuildingScale(root, out int sceneBridgesReset, out int sceneWallsAdjusted);
                    bridgesReset += sceneBridgesReset;
                    wallsAdjusted += sceneWallsAdjusted;
                    collidersAdded += EnsureMapColliders(root);
                    SetupEnvironment(root);
                    SetupPreviewCamera(root);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, scenePaths[i]);
                    upgraded++;
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Metin2 scene upgrade finished: {upgraded} scenes, {buildingsScaled} regular buildings set to {BuildingModelScale:0.0}, {wallsAdjusted} walls enlarged without extending their run axis, {bridgesReset} bridges kept at 1.0, {collidersAdded} collider components added, {effectReport.PlacedEffects} source effects placed.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, previousScene)))
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/Metin2/Upgrade Current Scene", priority = 103)]
        public static void UpgradeCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = scene.GetRootGameObjects().FirstOrDefault();
            if (!scene.IsValid() || root == null)
            {
                Debug.LogWarning("No open Metin2 scene could be upgraded.");
                return;
            }
            Report effectReport = new Report();
            LoadEffectSources(out Dictionary<string, PropertyEntry> properties, out FileIndex effects, out FileIndex textures, out Dictionary<string, MapSource> maps, effectReport);
            ImportedAssets.Clear(); ImportedEffectPrefabs.Clear();
            if (maps.TryGetValue(scene.name, out MapSource map)) RebuildMapEffects(map, root, properties, effects, textures, effectReport);
            int buildingsScaled = ApplyBuildingScale(root, out int bridgesReset, out int wallsAdjusted);
            int collidersAdded = EnsureMapColliders(root);
            SetupEnvironment(root);
            SetupPreviewCamera(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Metin2 current scene upgraded: {scene.name}, {buildingsScaled} regular buildings set to {BuildingModelScale:0.0}, {wallsAdjusted} walls enlarged without extending their run axis, {bridgesReset} bridges kept at 1.0, {collidersAdded} collider components added, {effectReport.PlacedEffects} source effects placed.");
        }

        [MenuItem("Tools/Metin2/Apply Building Scale (Bridges 1.0)", priority = 104)]
        public static void ApplyBuildingScaleToGeneratedScenes()
        {
            ApplyBuildingScaleToGeneratedScenesInternal(!Application.isBatchMode);
        }

        public static void ApplyBuildingScaleFromCommandLine()
        {
            ApplyBuildingScaleToGeneratedScenesInternal(false);
        }

        static void ApplyBuildingScaleToGeneratedScenesInternal(bool askToSave)
        {
            if (askToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = SceneManager.GetActiveScene().path;
            string[] scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { Output + "/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            int scenesUpdated = 0, modelsUpdated = 0, wallsAdjusted = 0, bridgesReset = 0;
            try
            {
                for (int i = 0; i < scenePaths.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Metin2 - Apply Building Scale", $"[{i + 1}/{scenePaths.Length}] {Path.GetFileNameWithoutExtension(scenePaths[i])}", i / (float)Math.Max(1, scenePaths.Length));
                    Scene scene = EditorSceneManager.OpenScene(scenePaths[i], OpenSceneMode.Single);
                    GameObject root = scene.GetRootGameObjects().FirstOrDefault();
                    int changed = ApplyBuildingScale(root, out int sceneBridgesReset, out int sceneWallsAdjusted);
                    if (changed == 0 && sceneBridgesReset == 0 && sceneWallsAdjusted == 0) continue;
                    modelsUpdated += changed;
                    wallsAdjusted += sceneWallsAdjusted;
                    bridgesReset += sceneBridgesReset;
                    scenesUpdated++;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, scenePaths[i]);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"Metin2 building scale applied: {modelsUpdated} regular buildings set to ({BuildingModelScale:0.0}, {BuildingModelScale:0.0}, {BuildingModelScale:0.0}), {wallsAdjusted} walls enlarged without extending their run axis, {bridgesReset} bridges set to (1.0, 1.0, 1.0) in {scenesUpdated} scenes; map positions unchanged.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!string.IsNullOrEmpty(previousScene) && File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, previousScene)))
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        static void LoadEffectSources(out Dictionary<string, PropertyEntry> properties, out FileIndex effects, out FileIndex textures, out Dictionary<string, MapSource> maps, Report report)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            List<string> roots = FindRoots(project);
            List<string> all = roots.SelectMany(SafeFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            properties = LoadProperties(all, report);
            effects = new FileIndex(all.Where(IsEffectFile), ".mse");
            textures = new FileIndex(all.Where(path => ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())), ".png");
            maps = DiscoverMaps(all).GroupBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        }

        static List<string> FindRoots(string project)
        {
            string client = Path.Combine(project, "Metin2,5");
            string[] preferred = { Path.Combine(client, "Extracted"), Path.Combine(client, "unpacked"), Path.Combine(client, "Source") };
            string source = preferred.FirstOrDefault(Directory.Exists) ?? (Directory.Exists(client) ? client : null);
            List<string> roots = new List<string>();
            if (source != null) roots.Add(source);
            string raw = Path.Combine(Application.dataPath, "Metin2", "Raw");
            if (source == null && Directory.Exists(raw)) roots.Add(raw);
            return roots;
        }

        static IEnumerable<string> SafeFiles(string root)
        {
            try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
            catch { return Array.Empty<string>(); }
        }

        static List<MapSource> DiscoverMaps(IEnumerable<string> files)
        {
            Dictionary<string, MapSource> found = new Dictionary<string, MapSource>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                if (!name.Equals("Setting.txt", StringComparison.OrdinalIgnoreCase) && !name.Equals("MapProperty.txt", StringComparison.OrdinalIgnoreCase)) continue;
                string directory = Directory.GetParent(path).FullName;
                if (!found.TryGetValue(directory, out MapSource map))
                {
                    map = new MapSource { Root = directory, Name = Clean(new DirectoryInfo(directory).Name) };
                    found.Add(directory, map);
                }
                if (name.Equals("Setting.txt", StringComparison.OrdinalIgnoreCase)) map.SettingPath = path;
            }
            foreach (IGrouping<string, MapSource> duplicate in found.Values.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                foreach (MapSource map in duplicate) map.Name = Clean(map.Name + "__" + (Directory.GetParent(map.Root)?.Name ?? "package") + "__" + Hash(map.Root).Substring(0, 6));
            return found.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static Dictionary<string, PropertyEntry> LoadProperties(IEnumerable<string> all, Report report)
        {
            Dictionary<string, PropertyEntry> result = new Dictionary<string, PropertyEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in all.Where(IsPropertyFile))
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    if (!lines.Any(l => l.Trim().Equals("YPRT", StringComparison.OrdinalIgnoreCase))) continue;
                    string crc = lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && l.All(char.IsDigit));
                    Dictionary<string, string> values = KeyValues(lines);
                    string asset = Pick(values, "buildingfile", "dungeonblockfile", "treefile", "effectfile", "modelfile", "filename");
                    string type = Pick(values, "propertytype", "type");
                    if (!string.IsNullOrEmpty(crc)) result[NormalizeId(crc)] = new PropertyEntry { AssetReference = asset.Trim('"'), Type = type.Trim('"'), Source = path };
                }
                catch (Exception ex) { report.Warnings.Add("Property parse failed: " + path + " | " + ex.Message); }
            }
            return result;
        }

        static bool IsPropertyFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".prb", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".prd", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".prt", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".pre", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".prx", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsEffectFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".mse", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mde", StringComparison.OrdinalIgnoreCase);
        }

        static void BuildMap(MapSource map, Dictionary<string, PropertyEntry> properties, FileIndex models, FileIndex textures, FileIndex textFiles, FileIndex effects, Report report)
        {
            Folders(Output + "/Maps/" + map.Name);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int terrainBefore = report.TerrainTiles, objectsBefore = report.PlacedObjects;
            GameObject root = new GameObject(map.Name);
            GameObject terrainRoot = Child(root, "Terrain"), buildings = Child(root, "Buildings"), trees = Child(root, "Trees"), rocks = Child(root, "Rocks"), props = Child(root, "Props"), water = Child(root, "Water");
            GameObject effectRoot = Child(root, "Effects"); SetupEnvironment(root); Child(root, "SpawnPoints");

            MapSettings settings = ReadSettings(map.SettingPath);
            Dictionary<int, TerrainLayer> layers = CreateTerrainLayers(map, settings, textures, textFiles, report);
            List<string> heightFiles = SafeFiles(map.Root).Where(p => Path.GetFileName(p).Equals("height.raw", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (string height in heightFiles)
                BuildTerrainTile(map, settings, height, layers, terrainRoot, water, report);
            foreach (string area in SafeFiles(map.Root).Where(p => Path.GetFileName(p).StartsWith("AreaData", StringComparison.OrdinalIgnoreCase)))
                PlaceArea(area, properties, models, textures, effects, buildings, trees, rocks, props, effectRoot, report);

            if (heightFiles.Count > 0 && report.TerrainTiles == terrainBefore)
                report.Warnings.Add($"{map.Name}: {heightFiles.Count} height.raw file(s) exist, but none contains a supported 16-bit square height grid. Empty or still-encrypted source data was not replaced with guessed terrain.");
            if (report.PlacedObjects == objectsBefore && SafeFiles(map.Root).Any(p => Path.GetFileName(p).StartsWith("AreaData", StringComparison.OrdinalIgnoreCase)))
                report.Warnings.Add($"{map.Name}: AreaData exists, but no referenced object could be placed. Check property and converted model coverage.");

            SetupPreviewCamera(root);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, Output + "/Scenes/" + map.Name + ".unity"); report.BuiltMaps++;
        }

        static MapSettings ReadSettings(string path)
        {
            MapSettings settings = new MapSettings();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return settings;
            Dictionary<string, string> values = KeyValues(File.ReadAllLines(path));
            if (TryFloat(Pick(values, "CellScale"), out float cellScale)) settings.CellScale = cellScale;
            if (TryFloat(Pick(values, "HeightScale"), out float heightScale)) settings.HeightScale = heightScale;
            settings.TextureSet = Pick(values, "TextureSet").Trim('"');
            return settings;
        }

        static Dictionary<int, TerrainLayer> CreateTerrainLayers(MapSource map, MapSettings settings, FileIndex textures, FileIndex textFiles, Report report)
        {
            Dictionary<int, TerrainLayer> result = new Dictionary<int, TerrainLayer>();
            if (string.IsNullOrEmpty(settings.TextureSet)) return result;
            string textureSetPath = textFiles.Resolve(settings.TextureSet);
            if (textureSetPath == null) { report.Missing.Add(map.Name + " | TextureSet | " + settings.TextureSet); return result; }
            List<TerrainTextureEntry> entries = ParseTextureSet(textureSetPath, report);
            foreach (TerrainTextureEntry entry in entries)
            {
                string source = textures.Resolve(entry.Reference);
                if (source == null) { report.Missing.Add(map.Name + " | TerrainTexture | " + entry.Reference); continue; }
                string textureAssetPath = CopyAsset(source, Raw + "/Textures");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
                if (texture == null) { report.Missing.Add(map.Name + " | TextureImport | " + source); continue; }
                TerrainLayer layer = new TerrainLayer
                {
                    diffuseTexture = texture,
                    // Keep every generated layer consistent with the Unity terrain
                    // authoring value used by the project. Source splat masks still
                    // decide where each texture is painted; only its visual repeat
                    // size is normalized here.
                    tileSize = new Vector2(TerrainTextureTileSize, TerrainTextureTileSize),
                    tileOffset = Vector2.zero
                };
                string layerPath = Output + "/Maps/" + map.Name + "/Layer_" + entry.Index.ToString("D3") + ".terrainlayer";
                if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath) != null) AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layer, layerPath); result[entry.Index] = layer;
            }
            return result;
        }

        static List<TerrainTextureEntry> ParseTextureSet(string path, Report report)
        {
            List<TerrainTextureEntry> result = new List<TerrainTextureEntry>();
            string[] lines = File.ReadAllLines(path); int fallbackIndex = 1;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex].Trim();
                if (!line.StartsWith("Start Texture", StringComparison.OrdinalIgnoreCase)) continue;
                string suffix = line.Substring("Start Texture".Length).Trim();
                int index = int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallbackIndex;
                List<string> values = new List<string>();
                while (++lineIndex < lines.Length)
                {
                    string value = StripComment(lines[lineIndex]).Trim();
                    if (value.StartsWith("End Texture", StringComparison.OrdinalIgnoreCase)) break;
                    if (value.Length > 0) values.Add(value.Trim('"'));
                }
                fallbackIndex = Mathf.Max(fallbackIndex + 1, index + 1);
                if (values.Count == 0 || !ImageExtensions.Contains(Path.GetExtension(values[0]).ToLowerInvariant()))
                {
                    report.Warnings.Add("Invalid TextureSet block " + index + " | " + path); continue;
                }
                TerrainTextureEntry entry = new TerrainTextureEntry { Index = index, Reference = values[0] };
                if (values.Count > 1 && TryFloat(values[1], out float uScale)) entry.UScale = uScale;
                if (values.Count > 2 && TryFloat(values[2], out float vScale)) entry.VScale = vScale;
                if (values.Count > 3 && TryFloat(values[3], out float uOffset)) entry.UOffset = uOffset;
                if (values.Count > 4 && TryFloat(values[4], out float vOffset)) entry.VOffset = vOffset;
                result.Add(entry);
            }
            return result;
        }

        static void BuildTerrainTile(MapSource map, MapSettings settings, string heightPath, Dictionary<int, TerrainLayer> availableLayers, GameObject terrainRoot, GameObject waterRoot, Report report)
        {
            byte[] raw = File.ReadAllBytes(heightPath); int sourceSize = Square(raw.Length / 2);
            if (sourceSize < 3) { report.Warnings.Add("Unsupported height data: " + heightPath); return; }
            int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(sourceSize - 3) + 1, 33, 4097);
            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
            {
                int sx = Mathf.Clamp(x + 1, 0, sourceSize - 1), sz = Mathf.Clamp(z + 1, 0, sourceSize - 1), offset = (sz * sourceSize + sx) * 2;
                ushort value = (ushort)(raw[offset] | raw[offset + 1] << 8); heights[z, x] = value / 65535f;
            }
            float tileSize = (resolution - 1) * settings.CellScale / MetinUnitsPerUnityUnit;
            float terrainHeight = 65535f * settings.HeightScale / MetinUnitsPerUnityUnit;
            TerrainData data = new TerrainData { heightmapResolution = resolution, size = new Vector3(tileSize, terrainHeight, tileSize), baseMapResolution = 256 };
            Vector2Int tile = ParseTile(Directory.GetParent(heightPath).Name); string tileName = tile.x.ToString("D3") + tile.y.ToString("D3");
            string assetPath = Output + "/Maps/" + map.Name + "/Terrain_" + tileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            // Terrain alphamaps are stored as generated sub-assets. The TerrainData
            // must already be a project asset before SetAlphamaps is called or Unity
            // keeps only the default first-layer splat when the object is saved.
            AssetDatabase.CreateAsset(data, assetPath);
            data.SetHeights(0, 0, heights);
            ApplyTileTextures(data, Directory.GetParent(heightPath).FullName, availableLayers);
            EditorUtility.SetDirty(data);
            GameObject terrain = Terrain.CreateTerrainGameObject(data); terrain.name = tileName; terrain.transform.SetParent(terrainRoot.transform, false); terrain.transform.localPosition = new Vector3(tile.x * tileSize, 0f, tile.y * tileSize);
            // Unity's generated distant-terrain basemap can be unavailable during a
            // freshly generated scene and renders magenta. Keep the source splat
            // layers active across the map; this also preserves their exact tiling.
            terrain.GetComponent<Terrain>().basemapDistance = 100000f;
            report.TerrainTiles++; BuildWater(map, settings, Directory.GetParent(heightPath).FullName, tile, tileSize, raw, sourceSize, waterRoot, report);
        }

        static void ApplyTileTextures(TerrainData data, string tileDirectory, Dictionary<int, TerrainLayer> available)
        {
            string path = Directory.EnumerateFiles(tileDirectory).FirstOrDefault(p => Path.GetFileName(p).Equals("tile.raw", StringComparison.OrdinalIgnoreCase));
            if (path == null || available.Count == 0) return;
            byte[] values = File.ReadAllBytes(path); int sourceSize = Square(values.Length); if (sourceSize < 2) return;
            List<int> used = values.Distinct().Select(v => (int)v).Where(v => v > 0 && available.ContainsKey(v)).OrderBy(v => v).ToList(); if (used.Count == 0) return;
            int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(sourceSize - 2), 16, 2048);
            data.alphamapResolution = resolution; data.terrainLayers = used.Select(i => available[i]).ToArray();
            Dictionary<int, float[,]> masks = used.ToDictionary(i => i, i => BuildMetin2SplatMask(values, sourceSize, resolution, i));
            int[,] patchTileCounts = CountMetin2PatchTiles(values, sourceSize, resolution);
            int patchPixels = 32;
            int patchCount = Mathf.Max(1, resolution / patchPixels);
            float[,,] alpha = new float[resolution, resolution, used.Count];
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
            {
                int patchX = Mathf.Min(patchCount - 1, x / patchPixels);
                int patchZ = Mathf.Min(patchCount - 1, z / patchPixels);
                int patch = patchZ * patchCount + patchX;
                int firstLayer = -1;
                for (int layer = 0; layer < used.Count; layer++)
                {
                    if (patchTileCounts[patch, used[layer]] > 0) { firstLayer = layer; break; }
                }
                if (firstLayer < 0) firstLayer = 0;

                // Metin2 renders splats in ascending texture-number order. The first
                // texture used by each 16x16-cell patch is opaque; every following
                // texture is composited over it with its generated alpha texture.
                // Convert that source-over sequence to Unity's normalized layer weights.
                alpha[z, x, firstLayer] = 1f;
                for (int layer = firstLayer + 1; layer < used.Count; layer++)
                {
                    if (patchTileCounts[patch, used[layer]] == 0) continue;
                    float sourceAlpha = masks[used[layer]][z, x];
                    if (sourceAlpha <= 0f) continue;
                    for (int previous = 0; previous < layer; previous++) alpha[z, x, previous] *= 1f - sourceAlpha;
                    alpha[z, x, layer] = sourceAlpha;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        static float[,] BuildMetin2SplatMask(byte[] tiles, int sourceSize, int resolution, int textureId)
        {
            byte[] rawMask = new byte[sourceSize * sourceSize];
            for (int y = 0; y < sourceSize; y++) for (int x = 0; x < sourceSize; x++)
            {
                int offset = y * sourceSize + x;
                int tile = tiles[offset];
                bool painted = tile == textureId;
                if (!painted && tile > textureId)
                {
                    for (int dy = -1; dy <= 1 && !painted; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < sourceSize && ny < sourceSize && tiles[ny * sourceSize + nx] == textureId)
                        { painted = true; break; }
                    }
                }
                rawMask[offset] = painted ? (byte)255 : (byte)0;
            }

            float[,] result = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++)
            {
                int top = y * sourceSize + x;
                int neighbours = rawMask[top] + rawMask[top + 1] + rawMask[top + 2]
                    + rawMask[top + sourceSize] + rawMask[top + sourceSize + 2]
                    + rawMask[top + sourceSize * 2] + rawMask[top + sourceSize * 2 + 1] + rawMask[top + sourceSize * 2 + 2];
                int filtered = ((neighbours >> 3) + rawMask[top + sourceSize + 1]) >> 1;
                result[y, x] = filtered / 255f;
            }
            return result;
        }

        static int[,] CountMetin2PatchTiles(byte[] tiles, int sourceSize, int resolution)
        {
            const int patchPixels = 32;
            int patchCount = Mathf.Max(1, resolution / patchPixels);
            int[,] counts = new int[patchCount * patchCount, 256];
            Action<int, int, int> add = (patchX, patchY, tile) =>
            {
                patchX = Mathf.Clamp(patchX, 0, patchCount - 1); patchY = Mathf.Clamp(patchY, 0, patchCount - 1);
                counts[patchY * patchCount + patchX, tile]++;
            };

            for (int y = 0; y < sourceSize; y++)
            {
                int patchY = Mathf.Clamp((y - 1) / patchPixels, 0, patchCount - 1);
                for (int x = 0; x < sourceSize; x++)
                {
                    int patchX = Mathf.Clamp((x - 1) / patchPixels, 0, patchCount - 1);
                    int tile = tiles[y * sourceSize + x];
                    add(patchX, patchY, tile);
                    bool nextY = y % patchPixels == 0 && y != 0 && y != sourceSize - 2;
                    bool previousY = y % patchPixels == 1 && y != 1 && y != sourceSize - 1;
                    bool nextX = x % patchPixels == 0 && x != 0 && x != sourceSize - 2;
                    bool previousX = x % patchPixels == 1 && x != 1 && x != sourceSize - 1;
                    if (nextY)
                    {
                        add(patchX, patchY + 1, tile);
                        if (nextX) add(patchX + 1, patchY + 1, tile);
                        else if (previousX) add(patchX - 1, patchY + 1, tile);
                    }
                    else if (previousY)
                    {
                        add(patchX, patchY - 1, tile);
                        if (nextX) add(patchX + 1, patchY - 1, tile);
                        else if (previousX) add(patchX - 1, patchY - 1, tile);
                    }
                    if (nextX) add(patchX + 1, patchY, tile);
                    else if (previousX) add(patchX - 1, patchY, tile);
                }
            }
            return counts;
        }

        static void BuildWater(MapSource map, MapSettings settings, string tileDirectory, Vector2Int tile, float tileSize, byte[] heightRaw, int heightSourceSize, GameObject parent, Report report)
        {
            string path = Directory.EnumerateFiles(tileDirectory).FirstOrDefault(p => Path.GetFileName(p).Equals("water.wtr", StringComparison.OrdinalIgnoreCase));
            if (path == null) return; byte[] data = File.ReadAllBytes(path); if (data.Length < 7) return;
            int width = data[2] | data[3] << 8, height = data[4] | data[5] << 8, waterCount = data[6], gridOffset = 7, heightsOffset = gridOffset + width * height;
            if (width <= 0 || height <= 0 || waterCount <= 0 || heightsOffset >= data.Length) return;
            int remaining = data.Length - heightsOffset, bytesPerLevel = remaining >= waterCount * 4 ? 4 : remaining >= waterCount * 2 ? 2 : 0;
            if (bytesPerLevel == 0) { report.Warnings.Add("Unsupported water data: " + path); return; }
            int[] levels = new int[waterCount];
            for (int i = 0; i < waterCount; i++) levels[i] = bytesPerLevel == 4 ? BitConverter.ToInt32(data, heightsOffset + i * 4) : BitConverter.ToUInt16(data, heightsOffset + i * 2);
            List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>(); List<Vector2> uv = new List<Vector2>(); float cellX = tileSize / width, cellZ = tileSize / height;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                byte id = data[gridOffset + z * width + x]; if (id == 0xFF || id >= levels.Length) continue;
                int rawWaterHeight = levels[id];
                if (!WaterIsAboveTerrain(rawWaterHeight, x, z, heightRaw, heightSourceSize)) continue;
                float y = rawWaterHeight * settings.HeightScale / MetinUnitsPerUnityUnit; int start = vertices.Count;
                vertices.Add(new Vector3(x * cellX, y, z * cellZ)); vertices.Add(new Vector3((x + 1) * cellX, y, z * cellZ)); vertices.Add(new Vector3((x + 1) * cellX, y, (z + 1) * cellZ)); vertices.Add(new Vector3(x * cellX, y, (z + 1) * cellZ));
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
            if (vertices.Count == 0) return;
            Mesh mesh = new Mesh { name = "Water_" + tile.x.ToString("D3") + tile.y.ToString("D3"), indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string meshPath = Output + "/Maps/" + map.Name + "/" + mesh.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh, meshPath);
            GameObject go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent.transform, false); go.transform.localPosition = new Vector3(tile.x * tileSize, 0f, tile.y * tileSize);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = GetWaterMaterial(); report.WaterTiles++;
        }

        static bool WaterIsAboveTerrain(int waterHeight, int x, int z, byte[] heightRaw, int sourceSize)
        {
            if (heightRaw == null || sourceSize < 3) return true;
            int x0 = Mathf.Clamp(x + 1, 0, sourceSize - 1), x1 = Mathf.Clamp(x + 2, 0, sourceSize - 1);
            int z0 = Mathf.Clamp(z + 1, 0, sourceSize - 1), z1 = Mathf.Clamp(z + 2, 0, sourceSize - 1);
            return waterHeight > RawHeight(heightRaw, sourceSize, x0, z0)
                || waterHeight > RawHeight(heightRaw, sourceSize, x1, z0)
                || waterHeight > RawHeight(heightRaw, sourceSize, x0, z1)
                || waterHeight > RawHeight(heightRaw, sourceSize, x1, z1);
        }

        static ushort RawHeight(byte[] raw, int size, int x, int z)
        {
            int offset = (z * size + x) * 2; return (ushort)(raw[offset] | raw[offset + 1] << 8);
        }

        static Material GetWaterMaterial()
        {
            string path = Output + "/Water.mat"; Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null) material = new Material(shader) { name = "Metin2 Water" };
            else if (material.shader != shader) material.shader = shader;
            material.color = new Color(0.08f, 0.35f, 0.55f, 0.72f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = (int)RenderQueue.Transparent;
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        static void PlaceArea(string path, Dictionary<string, PropertyEntry> properties, FileIndex models, FileIndex textures, FileIndex effects, GameObject buildings, GameObject trees, GameObject rocks, GameObject props, GameObject effectRoot, Report report)
        {
            foreach (AreaObject item in ParseArea(path))
            {
                if (!properties.TryGetValue(NormalizeId(item.Crc), out PropertyEntry property)) { report.Missing.Add("Property CRC " + item.Crc + " | " + path); continue; }
                if (string.IsNullOrEmpty(property.AssetReference)) { report.Missing.Add("Empty property asset " + item.Crc + " | " + property.Source); continue; }
                if (property.Type.Equals("Effect", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(property.AssetReference).Equals(".mse", StringComparison.OrdinalIgnoreCase))
                {
                    PlaceEffect(item, property, effects, textures, effectRoot, report);
                    continue;
                }
                bool wallOrFence = IsWallOrFence(property);
                // Linear pieces must never fall back to a similarly named model: lin, lin2,
                // corner, door and the fence variants each have their own source pivot.
                string source = wallOrFence ? models.ResolveExact(property.AssetReference) : models.Resolve(property.AssetReference);
                if (source == null)
                {
                    report.Missing.Add((wallOrFence ? "Exact wall/fence model | " : "") + property.AssetReference + " | CRC " + item.Crc);
                    continue;
                }
                string assetPath = CopyModelWithTextures(source, textures, report); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) { report.Missing.Add("FBX import failed | " + source); continue; }
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject; if (instance == null) continue;
                instance.name = Path.GetFileNameWithoutExtension(source);
                GameObject category = Category(property, instance.name, buildings, trees, rocks, props);
                instance.transform.SetParent(category.transform, false);
                // AreaData positions are already map-wide Metin2 coordinates. The six-digit
                // parent folder only identifies which terrain sector owns the record.
                instance.transform.localPosition = new Vector3(item.Position.x / MetinUnitsPerUnityUnit, (item.Position.z + item.HeightBias) / MetinUnitsPerUnityUnit, -item.Position.y / MetinUnitsPerUnityUnit);
                instance.transform.localRotation = Metin2Rotation(item.Rotation);
                // Scale each building root around its own source pivot. Scaling the Buildings
                // parent would also multiply map coordinates and destroy the AreaData layout.
                if (category == buildings)
                {
                    if (IsBridge(property, instance.name))
                    {
                        instance.transform.localScale = Vector3.one;
                        report.SourceScaleBridges++;
                    }
                    else if (IsWall(property, instance.name))
                    {
                        ApplyWallScale(instance.transform);
                        report.ScaledWalls++;
                    }
                    else
                    {
                        instance.transform.localScale = Vector3.one * BuildingModelScale;
                        report.ScaledBuildings++;
                    }
                }
                if (wallOrFence)
                {
                    report.WallFencePlacements++;
                    report.WallFenceModels.Add(property.AssetReference.Replace('\\', '/'));
                }
                report.MeshColliders += AddColliders(instance);
                report.PlacedObjects++;
            }
        }

        static void RebuildMapEffects(MapSource map, GameObject root, Dictionary<string, PropertyEntry> properties, FileIndex effects, FileIndex textures, Report report)
        {
            GameObject effectRoot = Child(root, "Effects");
            for (int i = effectRoot.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(effectRoot.transform.GetChild(i).gameObject);
            foreach (string area in SafeFiles(map.Root).Where(path => Path.GetFileName(path).StartsWith("AreaData", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (AreaObject item in ParseArea(area))
                {
                    if (!properties.TryGetValue(NormalizeId(item.Crc), out PropertyEntry property)) continue;
                    if (!property.Type.Equals("Effect", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(property.AssetReference).Equals(".mse", StringComparison.OrdinalIgnoreCase)) continue;
                    PlaceEffect(item, property, effects, textures, effectRoot, report);
                }
            }
        }

        static void PlaceEffect(AreaObject item, PropertyEntry property, FileIndex effects, FileIndex textures, GameObject effectRoot, Report report)
        {
            string msePath = ResolveSourceReference(property.AssetReference, Path.GetDirectoryName(property.Source), effects);
            if (msePath == null)
            {
                report.Missing.Add("Effect | " + property.AssetReference + " | CRC " + item.Crc);
                return;
            }

            if (!ImportedEffectPrefabs.TryGetValue(msePath, out string prefabPath))
            {
                MseEffect definition;
                try { definition = ParseMse(msePath); }
                catch (Exception ex)
                {
                    report.Warnings.Add("Effect parse failed | " + msePath + " | " + ex.Message);
                    return;
                }

                GameObject prototype = new GameObject(Clean(Path.GetFileNameWithoutExtension(msePath)));
                int created = 0;
                foreach (MseMesh meshDefinition in definition.Meshes)
                    created += BuildEffectMesh(meshDefinition, msePath, effects, textures, prototype, report);
                for (int i = 0; i < definition.Particles.Count; i++)
                    if (BuildEffectParticles(definition.Particles[i], msePath, textures, prototype, i, report)) created++;
                if (created == 0)
                {
                    UnityEngine.Object.DestroyImmediate(prototype);
                    report.Unsupported.Add("Effect has no supported mesh or particle data | " + property.AssetReference + " | CRC " + item.Crc);
                    return;
                }

                string prefabFolder = Output + "/Effects/Prefabs";
                Folders(prefabFolder);
                prefabPath = prefabFolder + "/" + Clean(Path.GetFileNameWithoutExtension(msePath)) + "_" + Hash(msePath) + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);
                PrefabUtility.SaveAsPrefabAsset(prototype, prefabPath);
                UnityEngine.Object.DestroyImmediate(prototype);
                ImportedEffectPrefabs[msePath] = prefabPath;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = prefab != null ? PrefabUtility.InstantiatePrefab(prefab) as GameObject : null;
            if (instance == null) { report.Missing.Add("Effect prefab import failed | " + msePath); return; }
            instance.name = Clean(Path.GetFileNameWithoutExtension(msePath)) + "_" + item.Crc;
            instance.transform.SetParent(effectRoot.transform, false);
            instance.transform.localPosition = new Vector3(item.Position.x / MetinUnitsPerUnityUnit, (item.Position.z + item.HeightBias) / MetinUnitsPerUnityUnit, -item.Position.y / MetinUnitsPerUnityUnit);
            instance.transform.localRotation = Metin2Rotation(item.Rotation);
            report.PlacedEffects++;
        }

        static int BuildEffectMesh(MseMesh definition, string msePath, FileIndex effects, FileIndex textures, GameObject parent, Report report)
        {
            string mdePath = ResolveSourceReference(definition.Reference, Path.GetDirectoryName(msePath), effects);
            if (mdePath == null)
            {
                report.Missing.Add("Effect mesh | " + definition.Reference + " | " + msePath);
                return 0;
            }

            MdeEffect data;
            try { data = ReadMde(mdePath); }
            catch (Exception ex)
            {
                report.Warnings.Add("MDE parse failed | " + mdePath + " | " + ex.Message);
                return 0;
            }

            string folder = Output + "/Effects/" + Clean(Path.GetFileNameWithoutExtension(msePath)) + "_" + Hash(mdePath);
            Folders(folder);
            int created = 0;
            for (int elementIndex = 0; elementIndex < data.Elements.Count; elementIndex++)
            {
                MdeElement element = data.Elements[elementIndex];
                if (element.Frames.Count == 0) continue;
                MseMeshElement settings = elementIndex < definition.Elements.Count ? definition.Elements[elementIndex] : new MseMeshElement();
                string textureSource = ResolveSourceReference(element.TextureReference, Path.GetDirectoryName(mdePath), textures);
                Texture2D texture = ImportEffectTexture(textureSource, report, element.TextureReference, msePath);
                Material material = GetEffectMaterial(texture, settings.Color, settings.SourceBlend, settings.DestinationBlend, folder, elementIndex);

                Mesh[] frameMeshes = new Mesh[element.Frames.Count];
                float[] visibility = new float[element.Frames.Count];
                for (int frameIndex = 0; frameIndex < element.Frames.Count; frameIndex++)
                {
                    MdeFrame sourceFrame = element.Frames[frameIndex];
                    Mesh mesh = new Mesh
                    {
                        name = Clean(element.Name) + "_" + frameIndex.ToString("D3"),
                        indexFormat = sourceFrame.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                    };
                    mesh.vertices = sourceFrame.Vertices;
                    mesh.uv = sourceFrame.Uv;
                    mesh.triangles = Enumerable.Range(0, sourceFrame.Vertices.Length).ToArray();
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    string meshPath = folder + "/" + mesh.name + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    frameMeshes[frameIndex] = mesh;
                    visibility[frameIndex] = sourceFrame.Visibility;
                }

                GameObject part = new GameObject(Clean(element.Name), typeof(MeshFilter), typeof(MeshRenderer), typeof(Metin2EffectMeshAnimator));
                part.transform.SetParent(parent.transform, false);
                MeshFilter filter = part.GetComponent<MeshFilter>();
                filter.sharedMesh = frameMeshes[0];
                MeshRenderer renderer = part.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                Metin2EffectMeshAnimator animator = part.GetComponent<Metin2EffectMeshAnimator>();
                animator.frames = frameMeshes;
                animator.visibility = visibility;
                animator.frameDelay = Mathf.Max(0.001f, definition.FrameDelay);
                animator.loop = definition.Loop;
                animator.baseColor = settings.Color;
                created++;
            }
            return created;
        }

        static bool BuildEffectParticles(MseParticle definition, string msePath, FileIndex textures, GameObject parent, int index, Report report)
        {
            string textureSource = ResolveSourceReference(definition.TextureReference, Path.GetDirectoryName(msePath), textures);
            Texture2D texture = ImportEffectTexture(textureSource, report, definition.TextureReference, msePath);
            if (texture == null) return false;

            GameObject particleObject = new GameObject("Particle_" + index.ToString("D2"), typeof(ParticleSystem));
            particleObject.transform.SetParent(parent.transform, false);
            particleObject.transform.localPosition = MetinVector(definition.Position) / MetinUnitsPerUnityUnit;
            ParticleSystem system = particleObject.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = Mathf.Max(0.1f, definition.CycleLength);
            main.startLifetime = Mathf.Max(0.01f, definition.Lifetime);
            main.maxParticles = Mathf.Max(1, definition.MaxParticles);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = Mathf.Max(0.001f, definition.SizeX * 2f / MetinUnitsPerUnityUnit);
            main.startSizeY = Mathf.Max(0.001f, definition.SizeY * 2f / MetinUnitsPerUnityUnit);
            main.startSizeZ = main.startSizeX;
            main.gravityModifier = Mathf.Max(0f, definition.Gravity / (MetinUnitsPerUnityUnit * 9.81f));
            main.startRotation = new ParticleSystem.MinMaxCurve(
                definition.RotationStartMin * Mathf.Deg2Rad,
                definition.RotationStartMax * Mathf.Deg2Rad);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, definition.EmissionRate);

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = definition.EmitterShape == 2 ? ParticleSystemShapeType.Box : ParticleSystemShapeType.Sphere;
            Vector3 emittingSize = MetinVector(definition.EmittingSize);
            shape.scale = new Vector3(Mathf.Max(0.01f, Mathf.Abs(emittingSize.x) / MetinUnitsPerUnityUnit), Mathf.Max(0.01f, Mathf.Abs(emittingSize.y) / MetinUnitsPerUnityUnit), Mathf.Max(0.01f, Mathf.Abs(emittingSize.z) / MetinUnitsPerUnityUnit));

            Vector3 sourceVelocity = definition.Direction * definition.Velocity;
            Vector3 velocity = MetinVector(sourceVelocity) / MetinUnitsPerUnityUnit;
            ParticleSystem.VelocityOverLifetimeModule velocityModule = system.velocityOverLifetime;
            velocityModule.enabled = velocity.sqrMagnitude > 0.000001f;
            velocityModule.space = ParticleSystemSimulationSpace.Local;
            velocityModule.x = velocity.x;
            velocityModule.y = velocity.y;
            velocityModule.z = velocity.z;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = definition.ScaleX.Count > 0 || definition.ScaleY.Count > 0;
            size.separateAxes = true;
            size.x = ToMinMaxCurve(definition.ScaleX, 1f);
            size.y = ToMinMaxCurve(definition.ScaleY, 1f);
            size.z = ToMinMaxCurve(definition.ScaleX, 1f);

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(ToGradient(definition));

            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = Mathf.Abs(definition.RotationSpeed) > 0.001f;
            rotation.z = definition.RotationSpeed * Mathf.Deg2Rad;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = definition.BillboardType == 3 ? ParticleSystemRenderMode.HorizontalBillboard
                : definition.BillboardType == 2 ? ParticleSystemRenderMode.VerticalBillboard : ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sharedMaterial = GetEffectMaterial(texture, Color.white, definition.SourceBlend, definition.DestinationBlend,
                Output + "/Effects/Materials", int.Parse(Hash(textureSource + "|" + definition.SourceBlend + "|" + definition.DestinationBlend).Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            return true;
        }

        static Vector3 MetinVector(Vector3 source)
        {
            return new Vector3(source.x, source.z, -source.y);
        }

        static Texture2D ImportEffectTexture(string source, Report report, string reference, string owner)
        {
            if (source == null)
            {
                report.Missing.Add("Effect texture | " + reference + " | " + owner);
                return null;
            }
            string assetPath = CopyAsset(source, Raw + "/Effects/Textures");
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Repeat || importer.filterMode != FilterMode.Bilinear || !importer.mipmapEnabled))
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static Material GetEffectMaterial(Texture2D texture, Color color, int sourceBlend, int destinationBlend, string folder, int elementIndex)
        {
            Folders(folder);
            string textureKey = texture != null ? AssetDatabase.GetAssetPath(texture) : "none";
            string path = folder + "/EffectMaterial_" + elementIndex.ToString("D3") + "_" + Hash(textureKey + "|" + color + "|" + sourceBlend + "|" + destinationBlend) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null) material.shader = shader;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", UnityBlend(sourceBlend));
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", UnityBlend(destinationBlend));
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static int UnityBlend(int direct3DBlend)
        {
            switch (direct3DBlend)
            {
                case 1: return (int)BlendMode.Zero;
                case 2: return (int)BlendMode.One;
                case 3: return (int)BlendMode.SrcColor;
                case 4: return (int)BlendMode.OneMinusSrcColor;
                case 5: return (int)BlendMode.SrcAlpha;
                case 6: return (int)BlendMode.OneMinusSrcAlpha;
                case 7: return (int)BlendMode.DstAlpha;
                case 8: return (int)BlendMode.OneMinusDstAlpha;
                case 9: return (int)BlendMode.DstColor;
                case 10: return (int)BlendMode.OneMinusDstColor;
                default: return (int)BlendMode.One;
            }
        }

        static string ResolveSourceReference(string reference, string sourceDirectory, FileIndex index)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;
            string localName = reference.Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string candidate = Path.Combine(sourceDirectory ?? "", Path.GetFileName(localName));
            if (File.Exists(candidate)) return candidate;
            return index.Resolve(reference);
        }

        static MdeEffect ReadMde(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                string header = Encoding.ASCII.GetString(reader.ReadBytes(11)).TrimEnd('\0');
                bool version2 = header.Equals("MDEData002", StringComparison.Ordinal);
                if (!version2 && !header.Equals("EffectData", StringComparison.Ordinal)) throw new InvalidDataException("Unsupported MDE header: " + header);
                int geometryCount = reader.ReadInt32();
                int frameCount = reader.ReadInt32();
                if (geometryCount < 0 || geometryCount > 1024 || frameCount <= 0 || frameCount > 4096) throw new InvalidDataException("Invalid MDE geometry/frame counts.");
                MdeEffect result = new MdeEffect();
                for (int geometry = 0; geometry < geometryCount; geometry++)
                {
                    MdeElement element = new MdeElement
                    {
                        Name = ReadFixedString(reader, 32),
                        TextureReference = ReadFixedString(reader, 128)
                    };
                    uint sharedVertexCount = 0, sharedIndexCount = 0, sharedTextureVertexCount = 0;
                    if (!version2)
                    {
                        sharedVertexCount = reader.ReadUInt32();
                        sharedIndexCount = reader.ReadUInt32();
                        sharedTextureVertexCount = reader.ReadUInt32();
                    }
                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        if (version2) reader.ReadByte();
                        float visibility = reader.ReadSingle();
                        uint vertexCount = version2 ? reader.ReadUInt32() : sharedVertexCount;
                        uint indexCount = version2 ? reader.ReadUInt32() : sharedIndexCount;
                        uint textureVertexCount = version2 ? reader.ReadUInt32() : sharedTextureVertexCount;
                        ValidateMdeCounts(vertexCount, indexCount, textureVertexCount, reader.BaseStream.Length - reader.BaseStream.Position);
                        Vector3[] sourceVertices = new Vector3[(int)vertexCount];
                        for (int i = 0; i < sourceVertices.Length; i++) sourceVertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        int[] vertexIndices = new int[(int)indexCount];
                        for (int i = 0; i < vertexIndices.Length; i++) vertexIndices[i] = reader.ReadInt32();
                        Vector2[] sourceUv = new Vector2[(int)textureVertexCount];
                        for (int i = 0; i < sourceUv.Length; i++) sourceUv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                        int[] textureIndices = new int[(int)indexCount];
                        for (int i = 0; i < textureIndices.Length; i++) textureIndices[i] = reader.ReadInt32();
                        Vector3[] vertices = new Vector3[(int)indexCount];
                        Vector2[] uv = new Vector2[(int)indexCount];
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (vertexIndices[i] < 0 || vertexIndices[i] >= sourceVertices.Length || textureIndices[i] < 0 || textureIndices[i] >= sourceUv.Length) throw new InvalidDataException("MDE index is outside its vertex data.");
                            vertices[i] = MetinVector(sourceVertices[vertexIndices[i]]) / MetinUnitsPerUnityUnit;
                            uv[i] = new Vector2(sourceUv[textureIndices[i]].x, -sourceUv[textureIndices[i]].y);
                        }
                        element.Frames.Add(new MdeFrame { Visibility = visibility, Vertices = vertices, Uv = uv });
                    }
                    result.Elements.Add(element);
                }
                return result;
            }
        }

        static void ValidateMdeCounts(uint vertices, uint indices, uint textureVertices, long bytesRemaining)
        {
            if (vertices > 10000000 || indices > 30000000 || textureVertices > 10000000 || indices % 3 != 0) throw new InvalidDataException("Invalid MDE mesh counts.");
            long needed = vertices * 12L + indices * 4L + textureVertices * 8L + indices * 4L;
            if (needed > bytesRemaining) throw new EndOfStreamException("MDE mesh data is truncated.");
        }

        static string ReadFixedString(BinaryReader reader, int length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(length)).TrimEnd('\0').Trim();
        }

        static MseEffect ParseMse(string path)
        {
            string text = File.ReadAllText(path);
            MseEffect result = new MseEffect();
            foreach (string block in ExtractGroupBlocks(text, "Mesh"))
            {
                MseMesh mesh = new MseMesh
                {
                    Reference = ReadQuotedToken(block, "MeshFileName"),
                    FrameDelay = ReadFloatToken(block, "MeshAnimationFrameDelay", 0.02f),
                    Loop = ReadIntToken(block, "MeshAnimationLoopEnable", 1) != 0
                };
                foreach (string elementBlock in ExtractGroupBlocks(block, "MeshElement", true))
                {
                    Vector4 factor = ReadVector4Token(elementBlock, "ColorFactor", Vector4.one);
                    float alpha = FirstEventValue(elementBlock, "TimeEventAlpha", factor.w);
                    mesh.Elements.Add(new MseMeshElement
                    {
                        SourceBlend = ReadIntToken(elementBlock, "BlendingSrcType", 5),
                        DestinationBlend = ReadIntToken(elementBlock, "BlendingDestType", 6),
                        Color = new Color(factor.x, factor.y, factor.z, factor.w * alpha)
                    });
                }
                if (!string.IsNullOrEmpty(mesh.Reference)) result.Meshes.Add(mesh);
            }
            foreach (string block in ExtractGroupBlocks(text, "Particle")) result.Particles.Add(ParseMseParticle(block));
            return result;
        }

        static MseParticle ParseMseParticle(string block)
        {
            string emitter = ExtractGroupBlocks(block, "EmitterProperty").FirstOrDefault() ?? "";
            string particle = ExtractGroupBlocks(block, "ParticleProperty").FirstOrDefault() ?? "";
            return new MseParticle
            {
                Position = ReadEffectPosition(block),
                CycleLength = ReadFloatToken(emitter, "CycleLength", 1f),
                MaxParticles = ReadIntToken(emitter, "MaxEmissionCount", 10),
                EmitterShape = ReadIntToken(emitter, "EmitterShape", 0),
                EmittingSize = ReadVector3Token(emitter, "EmittingSize", Vector3.zero),
                Direction = new Vector3(FirstEventValue(emitter, "TimeEventEmittingDirectionX", 0f), FirstEventValue(emitter, "TimeEventEmittingDirectionY", 0f), FirstEventValue(emitter, "TimeEventEmittingDirectionZ", 0f)),
                Velocity = FirstEventValue(emitter, "TimeEventEmittingVelocity", 0f),
                EmissionRate = FirstEventValue(emitter, "TimeEventEmissionCountPerSecond", 0f),
                Lifetime = FirstEventValue(emitter, "TimeEventLifeTime", 1f),
                SizeX = FirstEventValue(emitter, "TimeEventSizeX", 100f),
                SizeY = FirstEventValue(emitter, "TimeEventSizeY", 100f),
                SourceBlend = ReadIntToken(particle, "SrcBlendType", 5),
                DestinationBlend = ReadIntToken(particle, "DestBlendType", 6),
                BillboardType = ReadIntToken(particle, "BillboardType", 1),
                RotationSpeed = ReadFloatToken(particle, "RotationSpeed", 0f),
                RotationStartMin = ReadFloatToken(particle, "RotationRandomStartingBegin", 0f),
                RotationStartMax = ReadFloatToken(particle, "RotationRandomStartingEnd", 0f),
                Gravity = FirstEventValue(particle, "TimeEventGravity", 0f),
                TextureReference = ReadFirstQuotedListValue(particle, "TextureFiles"),
                ScaleX = ReadEventFloats(particle, "TimeEventScaleX"),
                ScaleY = ReadEventFloats(particle, "TimeEventScaleY"),
                Red = ReadEventFloats(particle, "TimeEventColorRed"),
                Green = ReadEventFloats(particle, "TimeEventColorGreen"),
                Blue = ReadEventFloats(particle, "TimeEventColorBlue"),
                Alpha = ReadEventFloats(particle, "TimeEventAlpha")
            };
        }

        static List<string> ExtractGroupBlocks(string text, string groupName, bool prefix = false)
        {
            string suffix = prefix ? @"\w*" : @"\b";
            MatchCollection matches = Regex.Matches(text ?? "", @"(?i)\bGroup\s+" + Regex.Escape(groupName) + suffix);
            List<string> result = new List<string>();
            foreach (Match match in matches)
            {
                int open = text.IndexOf('{', match.Index + match.Length);
                if (open < 0) continue;
                int depth = 0;
                for (int i = open; i < text.Length; i++)
                {
                    if (text[i] == '{') depth++;
                    else if (text[i] == '}' && --depth == 0) { result.Add(text.Substring(open + 1, i - open - 1)); break; }
                }
            }
            return result;
        }

        static string ExtractList(string text, string listName)
        {
            Match match = Regex.Match(text ?? "", @"(?i)\bList\s+" + Regex.Escape(listName) + @"\b");
            if (!match.Success) return "";
            int open = text.IndexOf('{', match.Index + match.Length);
            if (open < 0) return "";
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) return text.Substring(open + 1, i - open - 1);
            }
            return "";
        }

        static List<EventFloat> ReadEventFloats(string text, string listName)
        {
            List<EventFloat> result = new List<EventFloat>();
            foreach (string line in ExtractList(text, listName).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] values = Tokens(line);
                if (values.Length >= 2 && TryFloat(values[0], out float time) && TryFloat(values[1], out float value)) result.Add(new EventFloat { Time = time, Value = value });
            }
            return result.OrderBy(value => value.Time).ToList();
        }

        static float FirstEventValue(string text, string listName, float fallback)
        {
            List<EventFloat> values = ReadEventFloats(text, listName);
            return values.Count > 0 ? values[0].Value : fallback;
        }

        static string ReadQuotedToken(string text, string name)
        {
            Match match = Regex.Match(text ?? "", @"(?im)^\s*" + Regex.Escape(name) + @"\s+""([^""]+)""");
            return match.Success ? match.Groups[1].Value : "";
        }

        static string ReadFirstQuotedListValue(string text, string listName)
        {
            Match match = Regex.Match(ExtractList(text, listName), "\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        static float ReadFloatToken(string text, string name, float fallback)
        {
            Match match = Regex.Match(text ?? "", @"(?im)^\s*" + Regex.Escape(name) + @"\s+([-+0-9.eE]+)");
            return match.Success && TryFloat(match.Groups[1].Value, out float value) ? value : fallback;
        }

        static int ReadIntToken(string text, string name, int fallback)
        {
            float value = ReadFloatToken(text, name, fallback);
            return Mathf.RoundToInt(value);
        }

        static Vector3 ReadVector3Token(string text, string name, Vector3 fallback)
        {
            Match match = Regex.Match(text ?? "", @"(?im)^\s*" + Regex.Escape(name) + @"\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)");
            if (!match.Success || !TryFloat(match.Groups[1].Value, out float x) || !TryFloat(match.Groups[2].Value, out float y) || !TryFloat(match.Groups[3].Value, out float z)) return fallback;
            return new Vector3(x, y, z);
        }

        static Vector4 ReadVector4Token(string text, string name, Vector4 fallback)
        {
            Match match = Regex.Match(text ?? "", @"(?im)^\s*" + Regex.Escape(name) + @"\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)");
            if (!match.Success || !TryFloat(match.Groups[1].Value, out float x) || !TryFloat(match.Groups[2].Value, out float y) || !TryFloat(match.Groups[3].Value, out float z) || !TryFloat(match.Groups[4].Value, out float w)) return fallback;
            return new Vector4(x, y, z, w);
        }

        static Vector3 ReadEffectPosition(string text)
        {
            Match match = Regex.Match(ExtractList(text, "TimeEventPosition"), @"[-+0-9.eE]+\s+""[^""]+""\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)\s+([-+0-9.eE]+)");
            if (!match.Success || !TryFloat(match.Groups[1].Value, out float x) || !TryFloat(match.Groups[2].Value, out float y) || !TryFloat(match.Groups[3].Value, out float z)) return Vector3.zero;
            return new Vector3(x, y, z);
        }

        static ParticleSystem.MinMaxCurve ToMinMaxCurve(List<EventFloat> values, float fallback)
        {
            if (values == null || values.Count == 0) return new ParticleSystem.MinMaxCurve(fallback);
            AnimationCurve curve = new AnimationCurve(values.Select(value => new Keyframe(value.Time, value.Value)).ToArray());
            return new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static Gradient ToGradient(MseParticle particle)
        {
            List<float> colorTimes = particle.Red.Concat(particle.Green).Concat(particle.Blue).Select(value => value.Time).Distinct().OrderBy(value => value).ToList();
            if (colorTimes.Count == 0) colorTimes.Add(0f);
            GradientColorKey[] colors = colorTimes.Select(time => new GradientColorKey(new Color(EvaluateEvents(particle.Red, time, 1f), EvaluateEvents(particle.Green, time, 1f), EvaluateEvents(particle.Blue, time, 1f)), Mathf.Clamp01(time))).ToArray();
            List<EventFloat> alphaEvents = particle.Alpha.Count > 0 ? particle.Alpha : new List<EventFloat> { new EventFloat { Time = 0f, Value = 1f } };
            GradientAlphaKey[] alpha = alphaEvents.Select(value => new GradientAlphaKey(Mathf.Clamp01(value.Value), Mathf.Clamp01(value.Time))).ToArray();
            Gradient gradient = new Gradient();
            gradient.SetKeys(colors, alpha);
            return gradient;
        }

        static float EvaluateEvents(List<EventFloat> values, float time, float fallback)
        {
            if (values == null || values.Count == 0) return fallback;
            if (time <= values[0].Time) return values[0].Value;
            for (int i = 1; i < values.Count; i++)
            {
                if (time > values[i].Time) continue;
                float amount = Mathf.InverseLerp(values[i - 1].Time, values[i].Time, time);
                return Mathf.Lerp(values[i - 1].Value, values[i].Value, amount);
            }
            return values[values.Count - 1].Value;
        }

        static IEnumerable<AreaObject> ParseArea(string path)
        {
            List<string> block = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("Start Object", StringComparison.OrdinalIgnoreCase)) { block = new List<string>(); continue; }
                if (line.StartsWith("End Object", StringComparison.OrdinalIgnoreCase)) { AreaObject parsed = ParseAreaBlock(block); if (parsed != null) yield return parsed; block = null; continue; }
                if (block != null && line.Length > 0) block.Add(line);
            }
        }

        static AreaObject ParseAreaBlock(List<string> lines)
        {
            if (lines == null || lines.Count < 2) return null; string[] position = Tokens(lines[0]);
            if (position.Length < 3 || !TryFloat(position[0], out float x) || !TryFloat(position[1], out float y) || !TryFloat(position[2], out float z)) return null;
            Vector3 rotation = Vector3.zero;
            if (lines.Count > 2)
            {
                string[] rot = lines[2].Split(new[] { '#', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (rot.Length >= 3 && TryFloat(rot[0], out float rx) && TryFloat(rot[1], out float ry) && TryFloat(rot[2], out float rz)) rotation = new Vector3(rx, ry, rz);
            }
            float heightBias = 0f;
            if (lines.Count > 3) TryFloat(lines[3], out heightBias);
            return new AreaObject { Crc = lines[1].Trim(), Position = new Vector3(x, y, z), Rotation = rotation, HeightBias = heightBias };
        }

        static Quaternion Metin2Rotation(Vector3 yawPitchRoll)
        {
            float halfYaw = yawPitchRoll.x * Mathf.Deg2Rad * 0.5f;
            float halfPitch = yawPitchRoll.y * Mathf.Deg2Rad * 0.5f;
            float halfRoll = yawPitchRoll.z * Mathf.Deg2Rad * 0.5f;
            float sy = Mathf.Sin(halfYaw), cy = Mathf.Cos(halfYaw);
            float sp = Mathf.Sin(halfPitch), cp = Mathf.Cos(halfPitch);
            float sr = Mathf.Sin(halfRoll), cr = Mathf.Cos(halfRoll);

            // D3DXQuaternionRotationYawPitchRoll, followed by the same Z-up to
            // Unity Y-up basis change already applied by Unity's FBX importer.
            float d3dX = cy * sp * cr + sy * cp * sr;
            float d3dY = sy * cp * cr - cy * sp * sr;
            float d3dZ = cy * cp * sr - sy * sp * cr;
            float d3dW = cy * cp * cr + sy * sp * sr;
            return new Quaternion(d3dX, d3dZ, -d3dY, d3dW).normalized;
        }

        static string CopyModelWithTextures(string source, FileIndex textures, Report report)
        {
            if (ImportedModels.TryGetValue(source, out string existing)) return existing;
            string sourceDirectory = Directory.GetParent(source).FullName;
            string folder = Raw + "/Models/" + Clean(new DirectoryInfo(sourceDirectory).Name) + "_" + Hash(sourceDirectory); Folders(folder);
            if (ImportedModelDirectories.Add(sourceDirectory))
                foreach (string texture in Directory.EnumerateFiles(sourceDirectory).Where(p => ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())))
                {
                    CopyAsset(texture, folder, false);
                    // GrannyExporter replaces these characters in the FBX material
                    // reference, while the separately converted PNG keeps them.
                    string alias = SanitizeExporterTextureName(Path.GetFileName(texture));
                    if (!alias.Equals(Path.GetFileName(texture), StringComparison.OrdinalIgnoreCase)) CopyAssetAs(texture, folder, alias);
                }
            foreach (string expectedName in ReadFbxTextureNames(source))
            {
                string expectedPath = Path.Combine(sourceDirectory, expectedName);
                string texture = File.Exists(expectedPath) ? expectedPath : textures.Resolve(expectedName);
                if (texture == null) { report.Missing.Add("FBX texture | " + expectedName + " | " + source); continue; }
                CopyAssetAs(texture, folder, expectedName);
            }
            string asset = CopyAsset(source, folder, false); AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive); ImportedModels[source] = asset; return asset;
        }

        static IEnumerable<string> ReadFbxTextureNames(string source)
        {
            if (!Path.GetExtension(source).Equals(".fbx", StringComparison.OrdinalIgnoreCase)) yield break;
            string text;
            try { text = Encoding.ASCII.GetString(File.ReadAllBytes(source)); }
            catch { yield break; }
            HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(text, @"(?i)[a-z0-9_#() .-]+\.png"))
            {
                string name = Path.GetFileName(match.Value.Trim());
                if (name.Length > 4 && found.Add(name)) yield return name;
            }
        }

        static string SanitizeExporterTextureName(string name)
        {
            return (name ?? "").Replace('-', '_').Replace(' ', '_').Replace('#', '_');
        }

        static string CopyAsset(string source, string folder, bool hashName = true)
        {
            string cacheKey = source + "|" + folder;
            if (ImportedAssets.TryGetValue(cacheKey, out string cached)) return cached;
            Folders(folder); string name = Clean(Path.GetFileNameWithoutExtension(source)); if (hashName) name += "_" + Hash(source);
            string target = folder + "/" + name + Path.GetExtension(source).ToLowerInvariant(); string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, target);
            if (!File.Exists(absolute) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(absolute)) File.Copy(source, absolute, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport); ImportedAssets[cacheKey] = target; return target;
        }

        static string CopyAssetAs(string source, string folder, string targetFileName)
        {
            targetFileName = Clean(Path.GetFileName(targetFileName)); string cacheKey = source + "|" + folder + "|" + targetFileName;
            if (ImportedAssets.TryGetValue(cacheKey, out string cached)) return cached;
            Folders(folder); string target = folder + "/" + targetFileName; string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, target);
            if (!File.Exists(absolute) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(absolute)) File.Copy(source, absolute, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport); ImportedAssets[cacheKey] = target; return target;
        }

        static int EnsureMapColliders(GameObject root)
        {
            int added = 0;
            foreach (string groupName in new[] { "Buildings", "Trees", "Rocks", "Props" })
            {
                Transform group = root.transform.Find(groupName);
                if (group != null) added += AddColliders(group.gameObject);
            }
            return added;
        }

        static int AddColliders(GameObject root)
        {
            int added = 0;
            StaticEditorFlags staticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject, GameObjectUtility.GetStaticEditorFlags(filter.gameObject) | staticFlags);
                added++;
            }

            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null || renderer.GetComponent<Collider>() != null) continue;
                MeshCollider collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = renderer.sharedMesh;
                collider.convex = false;
                collider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
                added++;
            }

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponent<Collider>() != null || renderer.GetComponent<MeshFilter>() != null) continue;
                BoxCollider collider = renderer.gameObject.AddComponent<BoxCollider>();
                collider.center = renderer.localBounds.center;
                collider.size = renderer.localBounds.size;
                added++;
            }
            return added;
        }

        static GameObject Category(PropertyEntry property, string name, GameObject buildings, GameObject trees, GameObject rocks, GameObject props)
        {
            string value = (property.Type + " " + name).ToLowerInvariant();
            if (value.Contains("tree") || value.Contains("bush") || value.Contains("grass")) return trees;
            if (value.Contains("rock") || value.Contains("stone") || value.Contains("cliff")) return rocks;
            if (value.Contains("building") || value.Contains("dungeonblock") || value.Contains("house") || value.Contains("bridge") || IsWallOrFenceName(value)) return buildings;
            return props;
        }

        static bool IsWallOrFence(PropertyEntry property)
        {
            string value = ((property?.AssetReference ?? "") + " " + (property?.Type ?? "")).ToLowerInvariant();
            return IsWallOrFenceName(value);
        }

        static bool IsWall(PropertyEntry property, string modelName = null)
        {
            string value = (property?.AssetReference ?? "") + " " + (modelName ?? "");
            return IsWallName(value);
        }

        static bool IsWallName(string value)
        {
            return (value ?? "").IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsWallOrFenceName(string value)
        {
            value = (value ?? "").ToLowerInvariant();
            return value.Contains("wall") || value.Contains("fence") || value.Contains("palisade")
                || value.Contains("barricade") || value.Contains("rail");
        }

        static bool IsBridge(PropertyEntry property, string modelName = null)
        {
            string value = (property?.AssetReference ?? "") + " " + (property?.Type ?? "") + " " + (modelName ?? "");
            return IsBridgeName(value);
        }

        static bool IsBridgeName(string value)
        {
            return (value ?? "").IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static int ApplyBuildingScale(GameObject root, out int bridgesReset, out int wallsAdjusted)
        {
            bridgesReset = 0;
            wallsAdjusted = 0;
            Transform buildings = root != null ? root.transform.Find("Buildings") : null;
            if (buildings == null) return 0;
            buildings.localScale = Vector3.one;
            int count = 0;
            foreach (Transform model in FindPlacedModelRoots(buildings))
            {
                if (IsBridgeName(ModelIdentity(model)))
                {
                    ApplyModelScale(model, 1f);
                    bridgesReset++;
                }
                else if (IsWallName(ModelIdentity(model)))
                {
                    ApplyWallScale(model);
                    wallsAdjusted++;
                }
                else count += ApplyModelScale(model, BuildingModelScale);
            }
            // Older generated scenes could contain fence-like Building properties under
            // Props. Update only those named source models; ordinary props stay at 1.0.
            Transform props = root.transform.Find("Props");
            if (props != null) foreach (Transform model in FindPlacedModelRoots(props))
            {
                string identity = ModelIdentity(model);
                if (model == null || !IsWallOrFenceName(identity) || IsBridgeName(identity)) continue;
                if (IsWallName(identity)) { ApplyWallScale(model); wallsAdjusted++; }
                else count += ApplyModelScale(model, BuildingModelScale);
            }
            return count;
        }

        static IEnumerable<Transform> FindPlacedModelRoots(Transform category)
        {
            HashSet<int> yielded = new HashSet<int>();
            foreach (Transform candidate in category.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == category) continue;
                GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidate.gameObject);
                Transform model = prefabRoot != null ? prefabRoot.transform : (candidate.parent == category ? candidate : null);
                if (model == null || model == category || !model.IsChildOf(category) || !yielded.Add(model.GetInstanceID())) continue;
                yield return model;
            }
        }

        static string ModelIdentity(Transform model)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject);
            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : "";
            return model.name + " " + sourcePath;
        }

        static int ApplyModelScale(Transform model, float scale)
        {
            Vector3 sourcePosition = model.localPosition;
            Quaternion sourceRotation = model.localRotation;
            model.localScale = Vector3.one * scale;
            if ((model.localPosition - sourcePosition).sqrMagnitude > 0.000001f || Quaternion.Angle(model.localRotation, sourceRotation) > 0.0001f)
                throw new InvalidOperationException("Building scale changed source placement: " + model.name);
            return 1;
        }

        static int ApplyWallScale(Transform model)
        {
            Vector3 sourcePosition = model.localPosition;
            Quaternion sourceRotation = model.localRotation;
            Bounds bounds = LocalModelBounds(model);
            // Preserve the wall run axis so adjacent source placements still meet without
            // overlapping. Only thickness and height receive the requested 1.5 factor.
            model.localScale = bounds.size.x >= bounds.size.z
                ? new Vector3(1f, BuildingModelScale, BuildingModelScale)
                : new Vector3(BuildingModelScale, BuildingModelScale, 1f);
            if ((model.localPosition - sourcePosition).sqrMagnitude > 0.000001f || Quaternion.Angle(model.localRotation, sourceRotation) > 0.0001f)
                throw new InvalidOperationException("Wall scale changed source placement: " + model.name);
            return 1;
        }

        static Bounds LocalModelBounds(Transform model)
        {
            bool found = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 worldToModel = model.worldToLocalMatrix;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Matrix4x4 meshToModel = worldToModel * filter.transform.localToWorldMatrix;
                Vector3 min = meshBounds.min, max = meshBounds.max;
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                {
                    Vector3 point = meshToModel.MultiplyPoint3x4(new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z));
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                    else result.Encapsulate(point);
                }
            }
            return found ? result : new Bounds(Vector3.zero, Vector3.one);
        }

        static void SetupEnvironment(GameObject root)
        {
            GameObject environment = Child(root, "Environment");
            GameObject lightObject = Child(environment, "Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = GetOrAddComponent<Light>(lightObject);
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.956f, 0.86f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.35f;

            RenderSettings.skybox = GetSkyboxMaterial();
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.61f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.36f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.15f);
            RenderSettings.ambientIntensity = 1.08f;
            RenderSettings.reflectionIntensity = 0.85f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.52f, 0.62f, 0.72f);
            RenderSettings.fogStartDistance = 450f;
            RenderSettings.fogEndDistance = 1800f;

            GameObject volumeObject = Child(environment, "Global Volume");
            Volume volume = GetOrAddComponent<Volume>(volumeObject);
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = GetEnvironmentProfile();
        }

        static void SetupPreviewCamera(GameObject root)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero); bool hasBounds = false;
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                Bounds terrainBounds = terrain.terrainData.bounds; terrainBounds.center += terrain.transform.position;
                if (hasBounds) bounds.Encapsulate(terrainBounds); else { bounds = terrainBounds; hasBounds = true; }
            }
            if (!hasBounds)
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (hasBounds) bounds.Encapsulate(renderer.bounds); else { bounds = renderer.bounds; hasBounds = true; }
                }
            }
            if (!hasBounds) bounds = new Bounds(Vector3.zero, new Vector3(256f, 100f, 256f));

            float span = Mathf.Max(256f, bounds.size.x, bounds.size.z);
            bool hasFocus = TryFindStructureFocus(root, span, out Bounds focusBounds);
            float framingSpan = hasFocus ? Mathf.Clamp(Mathf.Max(focusBounds.size.x, focusBounds.size.z) * 1.25f, 130f, span * 0.48f) : span;
            Vector3 target = hasFocus ? focusBounds.center : bounds.center;
            if (TrySampleTerrainHeight(root, target, out float terrainHeight)) target.y = terrainHeight + Mathf.Clamp(framingSpan * 0.05f, 7f, 22f);
            float distance = framingSpan * 0.90f;
            float elevation = framingSpan * 0.38f;
            Vector3 horizontal = Quaternion.Euler(0f, -32f, 0f) * (Vector3.back * distance);
            Vector3 cameraPosition = target + horizontal + Vector3.up * elevation;
            if (TrySampleTerrainHeight(root, cameraPosition, out float cameraGround)) cameraPosition.y = Mathf.Max(cameraPosition.y, cameraGround + Mathf.Clamp(framingSpan * 0.12f, 28f, 65f));
            RenderSettings.fogStartDistance = Mathf.Max(450f, span * 0.75f);
            RenderSettings.fogEndDistance = Mathf.Max(1400f, span * 2f);
            GameObject environment = root.transform.Find("Environment")?.gameObject ?? root;
            GameObject cameraObject = Child(environment, "Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.position = cameraPosition;
            cameraObject.transform.LookAt(target);
            Camera camera = GetOrAddComponent<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = Mathf.Max(5000f, span * 5f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            UniversalAdditionalCameraData cameraData = GetOrAddComponent<UniversalAdditionalCameraData>(cameraObject);
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            GetOrAddComponent<AudioListener>(cameraObject);
        }

        static bool TryFindStructureFocus(GameObject root, float mapSpan, out Bounds focus)
        {
            focus = new Bounds();
            Renderer[] renderers = Array.Empty<Renderer>();
            foreach (string groupName in new[] { "Buildings", "Props", "Rocks", "Trees" })
            {
                Transform group = root.transform.Find(groupName);
                if (group == null) continue;
                renderers = group.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled).ToArray();
                if (renderers.Length > 0) break;
            }
            if (renderers.Length == 0) return false;

            float cellSize = Mathf.Clamp(mapSpan * 0.13f, 70f, 180f);
            Dictionary<Vector2Int, List<Renderer>> cells = new Dictionary<Vector2Int, List<Renderer>>();
            foreach (Renderer renderer in renderers)
            {
                Vector3 center = renderer.bounds.center;
                Vector2Int cell = new Vector2Int(Mathf.FloorToInt(center.x / cellSize), Mathf.FloorToInt(center.z / cellSize));
                if (!cells.TryGetValue(cell, out List<Renderer> list)) cells[cell] = list = new List<Renderer>();
                list.Add(renderer);
            }

            KeyValuePair<Vector2Int, List<Renderer>> densest = cells.OrderByDescending(pair => pair.Value.Count).First();
            Vector2 cellCenter = new Vector2((densest.Key.x + 0.5f) * cellSize, (densest.Key.y + 0.5f) * cellSize);
            List<Renderer> nearby = renderers.Where(renderer =>
            {
                Vector3 center = renderer.bounds.center;
                return Vector2.Distance(new Vector2(center.x, center.z), cellCenter) <= cellSize * 1.35f;
            }).ToList();
            if (nearby.Count == 0) nearby = densest.Value;
            focus = nearby[0].bounds;
            for (int i = 1; i < nearby.Count; i++) focus.Encapsulate(nearby[i].bounds);
            return true;
        }

        static bool TrySampleTerrainHeight(GameObject root, Vector3 point, out float height)
        {
            height = 0f; float bestDistance = float.PositiveInfinity; bool found = false;
            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                if (terrain.terrainData == null) continue;
                Vector3 position = terrain.transform.position, size = terrain.terrainData.size;
                float x = Mathf.Clamp(point.x, position.x, position.x + size.x);
                float z = Mathf.Clamp(point.z, position.z, position.z + size.z);
                float distance = (new Vector2(point.x, point.z) - new Vector2(x, z)).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                height = terrain.SampleHeight(new Vector3(x, 0f, z)) + position.y;
                found = true;
            }
            return found;
        }

        static Material GetSkyboxMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (material == null)
            {
                material = new Material(shader) { name = "Metin2 Natural Sky" };
                AssetDatabase.CreateAsset(material, SkyboxMaterialPath);
            }
            else if (shader != null) material.shader = shader;
            if (material.HasProperty("_SunDisk")) material.SetFloat("_SunDisk", 2f);
            if (material.HasProperty("_SunSize")) material.SetFloat("_SunSize", 0.035f);
            if (material.HasProperty("_SunSizeConvergence")) material.SetFloat("_SunSizeConvergence", 5f);
            if (material.HasProperty("_AtmosphereThickness")) material.SetFloat("_AtmosphereThickness", 0.85f);
            if (material.HasProperty("_SkyTint")) material.SetColor("_SkyTint", new Color(0.42f, 0.57f, 0.82f));
            if (material.HasProperty("_GroundColor")) material.SetColor("_GroundColor", new Color(0.33f, 0.30f, 0.26f));
            if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static VolumeProfile GetEnvironmentProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(EnvironmentProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Metin2 Environment";
                AssetDatabase.CreateAsset(profile, EnvironmentProfilePath);
            }
            Tonemapping tonemapping = GetOrAddOverride<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = GetOrAddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0.12f);
            color.contrast.Override(10f);
            color.saturation.Override(8f);
            Bloom bloom = GetOrAddOverride<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.16f);
            bloom.scatter.Override(0.55f);
            Vignette vignette = GetOrAddOverride<Vignette>(profile);
            vignette.color.Override(new Color(0.08f, 0.10f, 0.14f));
            vignette.intensity.Override(0.10f);
            vignette.smoothness.Override(0.42f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T GetOrAddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component)) component = profile.Add<T>(true);
            component.active = true;
            return component;
        }

        static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        static GameObject Child(GameObject parent, string name)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child;
        }
        static Vector2Int ParseTile(string name) { return name.Length == 6 && name.All(char.IsDigit) ? new Vector2Int(int.Parse(name.Substring(0, 3)), int.Parse(name.Substring(3, 3))) : Vector2Int.zero; }
        static int Square(int value) { int size = Mathf.RoundToInt(Mathf.Sqrt(value)); return size * size == value ? size : 0; }

        static Dictionary<string, string> KeyValues(IEnumerable<string> lines)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in lines) { string[] tokens = Tokens(StripComment(raw)); if (tokens.Length > 1) result[tokens[0].TrimEnd(':')] = string.Join(" ", tokens.Skip(1)); }
            return result;
        }
        static string Pick(Dictionary<string, string> values, params string[] keys) { foreach (string key in keys) if (values.TryGetValue(key, out string value)) return value; return ""; }
        static string[] Tokens(string value) { return value.Split(new[] { ' ', '\t', '=', ',' }, StringSplitOptions.RemoveEmptyEntries); }
        static string StripComment(string value) { int comment = value.IndexOf("//", StringComparison.Ordinal); return comment >= 0 ? value.Substring(0, comment) : value; }
        static bool TryFloat(string value, out float result) { return float.TryParse((value ?? "").Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out result); }
        static string NormalizeId(string value) { string id = (value ?? "").Trim().Trim('"').TrimStart('0'); return id.Length == 0 ? "0" : id; }
        static string Clean(string value) { return string.Concat((value ?? "map").Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '/' || c == '\\' ? '_' : c)); }
        static string Hash(string value) { unchecked { uint hash = 2166136261; foreach (char c in value.ToLowerInvariant()) { hash ^= c; hash *= 16777619; } return hash.ToString("x8"); } }
        static void Folders(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }

        sealed class FileIndex
        {
            readonly Dictionary<string, List<string>> files = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); readonly string preferredExtension;
            public int Count { get; private set; }
            public FileIndex(IEnumerable<string> paths, string preferred = null)
            {
                preferredExtension = preferred;
                foreach (string path in paths) { Count++; string key = Stem(Path.GetFileNameWithoutExtension(path)); if (!files.TryGetValue(key, out List<string> list)) files[key] = list = new List<string>(); list.Add(path); }
            }
            public string Resolve(string reference)
            {
                string key = Stem(Path.GetFileNameWithoutExtension((reference ?? "").Replace('\\', '/'))); if (key.Length == 0) return null;
                if (files.TryGetValue(key, out List<string> exact)) return Best(exact, reference);
                List<string> fuzzy = files.Where(x => x.Key.Contains(key) || key.Contains(x.Key)).SelectMany(x => x.Value).ToList(); return fuzzy.Count == 0 ? null : Best(fuzzy, reference);
            }
            public string ResolveExact(string reference)
            {
                string key = Stem(Path.GetFileNameWithoutExtension((reference ?? "").Replace('\\', '/')));
                return key.Length > 0 && files.TryGetValue(key, out List<string> exact) ? Best(exact, reference) : null;
            }
            string Best(IEnumerable<string> candidates, string reference)
            {
                string normalized = WithoutExtension((reference ?? "").Replace('\\', '/').ToLowerInvariant());
                return candidates.OrderBy(p => preferredExtension != null && Path.GetExtension(p).Equals(preferredExtension, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenByDescending(p => Suffix(normalized, WithoutExtension(p.Replace('\\', '/').ToLowerInvariant()))).ThenBy(p => p.Length).First();
            }
            static string WithoutExtension(string path) { string extension = Path.GetExtension(path); return extension.Length > 0 ? path.Substring(0, path.Length - extension.Length) : path; }
            static string Stem(string value) { return new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()); }
            static int Suffix(string a, string b) { int count = 0; while (count < Math.Min(a.Length, b.Length) && a[a.Length - count - 1] == b[b.Length - count - 1]) count++; return count; }
        }

        sealed class MseEffect
        {
            public readonly List<MseMesh> Meshes = new List<MseMesh>();
            public readonly List<MseParticle> Particles = new List<MseParticle>();
        }
        sealed class MseMesh
        {
            public string Reference;
            public float FrameDelay = 0.02f;
            public bool Loop = true;
            public readonly List<MseMeshElement> Elements = new List<MseMeshElement>();
        }
        sealed class MseMeshElement
        {
            public int SourceBlend = 5, DestinationBlend = 6;
            public Color Color = Color.white;
        }
        sealed class MseParticle
        {
            public Vector3 Position, EmittingSize, Direction;
            public float CycleLength = 1f, Velocity, EmissionRate, Lifetime = 1f, SizeX = 100f, SizeY = 100f, Gravity;
            public float RotationSpeed, RotationStartMin, RotationStartMax;
            public int MaxParticles = 10, EmitterShape, SourceBlend = 5, DestinationBlend = 6, BillboardType = 1;
            public string TextureReference;
            public List<EventFloat> ScaleX = new List<EventFloat>(), ScaleY = new List<EventFloat>();
            public List<EventFloat> Red = new List<EventFloat>(), Green = new List<EventFloat>(), Blue = new List<EventFloat>(), Alpha = new List<EventFloat>();
        }
        sealed class MdeEffect { public readonly List<MdeElement> Elements = new List<MdeElement>(); }
        sealed class MdeElement
        {
            public string Name, TextureReference;
            public readonly List<MdeFrame> Frames = new List<MdeFrame>();
        }
        sealed class MdeFrame { public float Visibility; public Vector3[] Vertices; public Vector2[] Uv; }
        sealed class EventFloat { public float Time, Value; }
        sealed class MapSource { public string Root; public string Name; public string SettingPath; }
        sealed class MapSettings { public float CellScale = 200f; public float HeightScale = 0.5f; public string TextureSet = ""; }
        sealed class TerrainTextureEntry { public int Index; public string Reference; public float UScale = 1f, VScale = 1f, UOffset, VOffset; }
        sealed class PropertyEntry { public string AssetReference; public string Type; public string Source; }
        sealed class AreaObject { public string Crc; public Vector3 Position; public Vector3 Rotation; public float HeightBias; }
        sealed class Report
        {
            public int BuiltMaps, TerrainTiles, WaterTiles, PlacedObjects, PlacedEffects, MeshColliders, ScaledBuildings, ScaledWalls, SourceScaleBridges, WallFencePlacements;
            public readonly HashSet<string> WallFenceModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase); public readonly HashSet<string> Unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase); public readonly List<string> Warnings = new List<string>(); public readonly List<string> Errors = new List<string>();
            public void Save(IEnumerable<string> roots, int maps, int properties, int models, int textures)
            {
                StringBuilder text = new StringBuilder("Metin2 Map Import Report\n" + DateTime.Now.ToString("u") + "\n\nSources:\n"); foreach (string root in roots) text.AppendLine("- " + root);
                text.AppendLine($"\nMaps found: {maps}\nMaps built: {BuiltMaps}\nTerrain tiles: {TerrainTiles}\nWater tiles: {WaterTiles}\nObjects placed: {PlacedObjects}\nBuilding models scaled to {BuildingModelScale:0.0}: {ScaledBuildings}\nWall models scaled in thickness/height only: {ScaledWalls}\nBridge models kept at source scale 1.0: {SourceScaleBridges}\nWall/fence placements: {WallFencePlacements}\nDistinct wall/fence source models: {WallFenceModels.Count}\nEffects placed: {PlacedEffects}\nMesh colliders added: {MeshColliders}\nProperties indexed: {properties}\nModels indexed: {models}\nTextures indexed: {textures}\nMissing references: {Missing.Count}\nUnsupported source features: {Unsupported.Count}\n");
                text.AppendLine("Wall/fence source models (exact-name matching):"); foreach (string item in WallFenceModels.OrderBy(x => x)) text.AppendLine("- " + item);
                text.AppendLine("Missing references:"); foreach (string item in Missing.OrderBy(x => x)) text.AppendLine("- " + item);
                text.AppendLine("\nUnsupported source features:"); foreach (string item in Unsupported.OrderBy(x => x)) text.AppendLine("- " + item);
                text.AppendLine("\nWarnings:"); foreach (string item in Warnings) text.AppendLine("- " + item); text.AppendLine("\nErrors:"); foreach (string item in Errors) text.AppendLine("- " + item);
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, Output, "ImportReport.txt"), text.ToString(), Encoding.UTF8); AssetDatabase.ImportAsset(Output + "/ImportReport.txt");
            }
        }
    }
}

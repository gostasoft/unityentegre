using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Metin2Dev
{
    /// <summary>Builds Unity scenes from extracted Metin2 maps and converted FBX/PNG assets.</summary>
    public static class Metin2MapImporter
    {
        const string Output = "Assets/Metin2/Generated";
        const string Raw = "Assets/Metin2/Raw";
        const float MetinUnitsPerUnityUnit = 100f;
        static readonly string[] ModelExtensions = { ".fbx", ".obj", ".dae" };
        static readonly string[] ImageExtensions = { ".png", ".dds", ".tga", ".jpg", ".jpeg", ".bmp" };
        static readonly Dictionary<string, string> ImportedModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ImportedModelDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> ImportedAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                ImportedModels.Clear(); ImportedModelDirectories.Clear(); ImportedAssets.Clear();
                List<string> all = roots.SelectMany(SafeFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                int archives = all.Count(p => p.EndsWith(".eix", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".epk", StringComparison.OrdinalIgnoreCase));
                FileIndex models = new FileIndex(all.Where(p => ModelExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())), ".fbx");
                FileIndex textures = new FileIndex(all.Where(p => ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())), ".png");
                FileIndex textFiles = new FileIndex(all.Where(p => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
                Dictionary<string, PropertyEntry> properties = LoadProperties(all, report);
                List<MapSource> maps = DiscoverMaps(all);

                if (archives > 0 && models.Count == 0)
                    report.Warnings.Add($"Found {archives} EIX/EPK archives but no FBX/OBJ/DAE files. Extract packs and convert GR2 models first.");
                if (maps.Count == 0) report.Errors.Add("No extracted Setting.txt or MapProperty.txt map roots were found.");

                for (int i = 0; i < maps.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Metin2 - Build All Maps", $"[{i + 1}/{maps.Count}] {maps[i].Name}", i / (float)Math.Max(1, maps.Count));
                    try { BuildMap(maps[i], properties, models, textures, textFiles, report); }
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
            foreach (string path in all.Where(p => p.EndsWith(".prb", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    string crc = lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && l.All(char.IsDigit));
                    Dictionary<string, string> values = KeyValues(lines);
                    string asset = Pick(values, "buildingfile", "treefile", "effectfile", "modelfile", "filename");
                    string type = Pick(values, "propertytype", "type");
                    if (!string.IsNullOrEmpty(crc)) result[NormalizeId(crc)] = new PropertyEntry { AssetReference = asset.Trim('"'), Type = type.Trim('"'), Source = path };
                }
                catch (Exception ex) { report.Warnings.Add("Property parse failed: " + path + " | " + ex.Message); }
            }
            return result;
        }

        static void BuildMap(MapSource map, Dictionary<string, PropertyEntry> properties, FileIndex models, FileIndex textures, FileIndex textFiles, Report report)
        {
            Folders(Output + "/Maps/" + map.Name);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject(map.Name);
            GameObject terrainRoot = Child(root, "Terrain"), buildings = Child(root, "Buildings"), trees = Child(root, "Trees"), rocks = Child(root, "Rocks"), props = Child(root, "Props"), water = Child(root, "Water");
            Child(root, "Effects"); SetupEnvironment(root); Child(root, "SpawnPoints");

            MapSettings settings = ReadSettings(map.SettingPath);
            Dictionary<int, TerrainLayer> layers = CreateTerrainLayers(map, settings, textures, textFiles, report);
            foreach (string height in SafeFiles(map.Root).Where(p => Path.GetFileName(p).Equals("height.raw", StringComparison.OrdinalIgnoreCase)))
                BuildTerrainTile(map, settings, height, layers, terrainRoot, water, report);
            foreach (string area in SafeFiles(map.Root).Where(p => Path.GetFileName(p).StartsWith("AreaData", StringComparison.OrdinalIgnoreCase)))
                PlaceArea(area, properties, models, buildings, trees, rocks, props, report);

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
            List<string> references = File.ReadAllLines(textureSetPath).Select(l => l.Trim().Trim('"')).Where(l => ImageExtensions.Contains(Path.GetExtension(l).ToLowerInvariant())).ToList();
            for (int i = 0; i < references.Count; i++)
            {
                string source = textures.Resolve(references[i]);
                if (source == null) { report.Missing.Add(map.Name + " | TerrainTexture | " + references[i]); continue; }
                string textureAssetPath = CopyAsset(source, Raw + "/Textures");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
                if (texture == null) { report.Missing.Add(map.Name + " | TextureImport | " + source); continue; }
                TerrainLayer layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(12f, 12f) };
                string layerPath = Output + "/Maps/" + map.Name + "/Layer_" + (i + 1).ToString("D3") + ".terrainlayer";
                if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath) != null) AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layer, layerPath); result[i + 1] = layer;
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
            data.SetHeights(0, 0, heights); ApplyTileTextures(data, Directory.GetParent(heightPath).FullName, availableLayers);
            Vector2Int tile = ParseTile(Directory.GetParent(heightPath).Name); string tileName = tile.x.ToString("D3") + tile.y.ToString("D3");
            string assetPath = Output + "/Maps/" + map.Name + "/Terrain_" + tileName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);
            GameObject terrain = Terrain.CreateTerrainGameObject(data); terrain.name = tileName; terrain.transform.SetParent(terrainRoot.transform, false); terrain.transform.localPosition = new Vector3(tile.x * tileSize, 0f, tile.y * tileSize);
            report.TerrainTiles++; BuildWater(map, Directory.GetParent(heightPath).FullName, tile, tileSize, waterRoot, report);
        }

        static void ApplyTileTextures(TerrainData data, string tileDirectory, Dictionary<int, TerrainLayer> available)
        {
            string path = Directory.EnumerateFiles(tileDirectory).FirstOrDefault(p => Path.GetFileName(p).Equals("tile.raw", StringComparison.OrdinalIgnoreCase));
            if (path == null || available.Count == 0) return;
            byte[] values = File.ReadAllBytes(path); int sourceSize = Square(values.Length); if (sourceSize < 2) return;
            List<int> used = values.Distinct().Select(v => (int)v).Where(v => v > 0 && available.ContainsKey(v)).OrderBy(v => v).ToList(); if (used.Count == 0) return;
            int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(sourceSize - 2), 16, 2048);
            data.alphamapResolution = resolution; data.terrainLayers = used.Select(i => available[i]).ToArray();
            float[,,] alpha = new float[resolution, resolution, used.Count];
            Dictionary<int, int> lookup = used.Select((value, index) => new { value, index }).ToDictionary(x => x.value, x => x.index);
            for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
            {
                int textureId = values[Mathf.Clamp(z + 1, 0, sourceSize - 1) * sourceSize + Mathf.Clamp(x + 1, 0, sourceSize - 1)];
                alpha[z, x, lookup.TryGetValue(textureId, out int layer) ? layer : 0] = 1f;
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        static void BuildWater(MapSource map, string tileDirectory, Vector2Int tile, float tileSize, GameObject parent, Report report)
        {
            string path = Directory.EnumerateFiles(tileDirectory).FirstOrDefault(p => Path.GetFileName(p).Equals("water.wtr", StringComparison.OrdinalIgnoreCase));
            if (path == null) return; byte[] data = File.ReadAllBytes(path); if (data.Length < 7) return;
            int width = data[2] | data[3] << 8, height = data[4] | data[5] << 8, waterCount = data[6], gridOffset = 7, heightsOffset = gridOffset + width * height;
            if (width <= 0 || height <= 0 || waterCount <= 0 || heightsOffset + waterCount * 4 > data.Length) return;
            int[] levels = new int[waterCount]; for (int i = 0; i < waterCount; i++) levels[i] = BitConverter.ToInt32(data, heightsOffset + i * 4);
            List<Vector3> vertices = new List<Vector3>(); List<int> triangles = new List<int>(); List<Vector2> uv = new List<Vector2>(); float cellX = tileSize / width, cellZ = tileSize / height;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                byte id = data[gridOffset + z * width + x]; if (id == 0xFF || id >= levels.Length) continue; float y = levels[id] / MetinUnitsPerUnityUnit; int start = vertices.Count;
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

        static Material GetWaterMaterial()
        {
            string path = Output + "/Water.mat"; Material material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "Metin2 Water", color = new Color(0.08f, 0.35f, 0.55f, 0.72f) }; AssetDatabase.CreateAsset(material, path); return material;
        }

        static void PlaceArea(string path, Dictionary<string, PropertyEntry> properties, FileIndex models, GameObject buildings, GameObject trees, GameObject rocks, GameObject props, Report report)
        {
            Vector2Int tile = ParseTile(Directory.GetParent(path).Name);
            foreach (AreaObject item in ParseArea(path))
            {
                if (!properties.TryGetValue(NormalizeId(item.Crc), out PropertyEntry property)) { report.Missing.Add("Property CRC " + item.Crc + " | " + path); continue; }
                if (string.IsNullOrEmpty(property.AssetReference)) { report.Missing.Add("Empty property asset " + item.Crc + " | " + property.Source); continue; }
                string source = models.Resolve(property.AssetReference); if (source == null) { report.Missing.Add(property.AssetReference + " | CRC " + item.Crc); continue; }
                string assetPath = CopyModelWithTextures(source); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) { report.Missing.Add("FBX import failed | " + source); continue; }
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject; if (instance == null) continue;
                instance.name = Path.GetFileNameWithoutExtension(source); instance.transform.SetParent(Category(property, instance.name, buildings, trees, rocks, props).transform, false);
                instance.transform.localPosition = new Vector3(tile.x * 256f + item.Position.x / MetinUnitsPerUnityUnit, item.Position.z / MetinUnitsPerUnityUnit, tile.y * 256f - item.Position.y / MetinUnitsPerUnityUnit);
                instance.transform.localRotation = Quaternion.Euler(item.Rotation.x, -item.Rotation.z, item.Rotation.y); AddColliders(instance); report.PlacedObjects++;
            }
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
            return new AreaObject { Crc = lines[1].Trim(), Position = new Vector3(x, y, z), Rotation = rotation };
        }

        static string CopyModelWithTextures(string source)
        {
            if (ImportedModels.TryGetValue(source, out string existing)) return existing;
            string sourceDirectory = Directory.GetParent(source).FullName;
            string folder = Raw + "/Models/" + Clean(new DirectoryInfo(sourceDirectory).Name) + "_" + Hash(sourceDirectory); Folders(folder);
            if (ImportedModelDirectories.Add(sourceDirectory))
                foreach (string texture in Directory.EnumerateFiles(sourceDirectory).Where(p => ImageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))) CopyAsset(texture, folder, false);
            string asset = CopyAsset(source, folder, false); AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive); ImportedModels[source] = asset; return asset;
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

        static void AddColliders(GameObject root)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
            }
        }

        static GameObject Category(PropertyEntry property, string name, GameObject buildings, GameObject trees, GameObject rocks, GameObject props)
        {
            string value = (property.Type + " " + name).ToLowerInvariant();
            if (value.Contains("tree") || value.Contains("bush") || value.Contains("grass")) return trees;
            if (value.Contains("rock") || value.Contains("stone") || value.Contains("cliff")) return rocks;
            if (value.Contains("building") || value.Contains("house") || value.Contains("bridge") || value.Contains("wall")) return buildings;
            return props;
        }

        static void SetupEnvironment(GameObject root)
        {
            GameObject environment = Child(root, "Environment"), lightObject = Child(environment, "Directional Light"); lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.Soft; RenderSettings.ambientMode = AmbientMode.Trilight;
        }
        static GameObject Child(GameObject parent, string name) { GameObject child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
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

        sealed class MapSource { public string Root; public string Name; public string SettingPath; }
        sealed class MapSettings { public float CellScale = 200f; public float HeightScale = 0.5f; public string TextureSet = ""; }
        sealed class PropertyEntry { public string AssetReference; public string Type; public string Source; }
        sealed class AreaObject { public string Crc; public Vector3 Position; public Vector3 Rotation; }
        sealed class Report
        {
            public int BuiltMaps, TerrainTiles, WaterTiles, PlacedObjects;
            public readonly HashSet<string> Missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase); public readonly List<string> Warnings = new List<string>(); public readonly List<string> Errors = new List<string>();
            public void Save(IEnumerable<string> roots, int maps, int properties, int models, int textures)
            {
                StringBuilder text = new StringBuilder("Metin2 Map Import Report\n" + DateTime.Now.ToString("u") + "\n\nSources:\n"); foreach (string root in roots) text.AppendLine("- " + root);
                text.AppendLine($"\nMaps found: {maps}\nMaps built: {BuiltMaps}\nTerrain tiles: {TerrainTiles}\nWater tiles: {WaterTiles}\nObjects placed: {PlacedObjects}\nProperties indexed: {properties}\nModels indexed: {models}\nTextures indexed: {textures}\nMissing references: {Missing.Count}\n");
                text.AppendLine("Missing references:"); foreach (string item in Missing.OrderBy(x => x)) text.AppendLine("- " + item);
                text.AppendLine("\nWarnings:"); foreach (string item in Warnings) text.AppendLine("- " + item); text.AppendLine("\nErrors:"); foreach (string item in Errors) text.AppendLine("- " + item);
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, Output, "ImportReport.txt"), text.ToString(), Encoding.UTF8); AssetDatabase.ImportAsset(Output + "/ImportReport.txt");
            }
        }
    }
}

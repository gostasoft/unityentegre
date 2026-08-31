using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Metin2Dev.Gameplay;
using Metin3Dev.Panel;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Metin3Dev.Panel.Editor
{
    public static class Metin3EntityPrefabCatalogBuilder
    {
        const string AssetPath = "Assets/Metin3Panel/Resources/Metin3EntityPrefabCatalog.asset";
        const string GeneratedRoot = "Assets/Metin2/EntitiesGenerated";
        const string RuntimePrefabRoot = "Assets/Metin3Panel/Resources/EntityPrefabs";
        const string ControllerRoot = GeneratedRoot + "/Controllers";
        const string ClipRoot = GeneratedRoot + "/AnimationClips";
        const string AnimationReportPath = GeneratedRoot + "/MobAnimationImportReport.txt";
        const int BuilderVersion = 7;
        static bool buildScheduled;
        static readonly List<string> animationReport = new List<string>();

        [InitializeOnLoadMethod]
        static void BuildWhenMissing()
        {
            Metin3EntityPrefabCatalog existing = AssetDatabase.LoadAssetAtPath<Metin3EntityPrefabCatalog>(AssetPath);
            if (existing != null && existing.builderVersion == BuilderVersion && existing.Resolve("stray_dog") != null &&
                existing.Resolve("fire_dragon") != null && existing.Resolve("arms") != null && existing.Resolve("metinstone_05") != null) return;
            if (buildScheduled) return;
            buildScheduled = true;
            EditorApplication.delayCall += () =>
            {
                buildScheduled = false;
                ImportOriginalModels();
            };
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
            catalog.builderVersion = 0;
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
            animationReport.Clear();
            EnsureAssetFolder("Assets/Metin3Panel/Resources");
            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(ControllerRoot);
            EnsureAssetFolder(ClipRoot);
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
                string texturePath = skinMatch.Success
                    ? FindTexture(directory, Path.GetFileNameWithoutExtension(skinMatch.Groups["path"].Value.Replace('\\', '/')))
                    : string.Empty;
                model = CreateEntityPrefab(key, model, texturePath, directory);
                string normalized = Metin3EntityPrefabCatalog.Normalize(key);
                if (!string.IsNullOrEmpty(normalized) && model != null) candidates[normalized] = model;
            }
            catalog.entries = candidates.OrderBy(pair => pair.Key).Select(pair => new Metin3EntityPrefabCatalog.Entry { key = pair.Key, prefab = pair.Value }).ToArray();
            catalog.builderVersion = BuilderVersion;
            EditorUtility.SetDirty(catalog);
            File.WriteAllText(AnimationReportPath, BuildAnimationReport(), Encoding.UTF8);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AnimationReportPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Metin3 Panel] Orijinal VNUM model kataloğu hazır: {catalog.entries.Length} gerçek mob/NPC/metin modeli. Animasyon raporu: {AnimationReportPath}");
        }

        static GameObject CreateEntityPrefab(string key, GameObject source, string texturePath, string sourceDirectory)
        {
            string prefabFolder = RuntimePrefabRoot;
            string materialFolder = GeneratedRoot + "/Materials";
            EnsureAssetFolder(prefabFolder);
            EnsureAssetFolder(materialFolder);
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);
            Texture2D texture = string.IsNullOrEmpty(texturePath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(ToAssetPath(texturePath));
            Material overrideMaterial = null;
            if (texture != null)
            {
                Material sourceMaterial = instance.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials).FirstOrDefault(material => material != null);
                overrideMaterial = sourceMaterial != null ? new Material(sourceMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                overrideMaterial.name = key + "_skin";
                overrideMaterial.mainTexture = texture;
                string materialPath = $"{materialFolder}/{key}_skin.mat";
                AssetDatabase.DeleteAsset(materialPath);
                AssetDatabase.CreateAsset(overrideMaterial, materialPath);
            }
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (overrideMaterial == null) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = overrideMaterial;
                renderer.sharedMaterials = materials;
            }
            RuntimeAnimatorController controller = CreateMotionController(key, sourceDirectory);
            if (controller != null)
            {
                Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (instance.GetComponent<MobAnimationRuntime>() == null) instance.AddComponent<MobAnimationRuntime>();
            }
            string prefabPath = $"{prefabFolder}/{key}.prefab";
            AssetDatabase.DeleteAsset(prefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        static RuntimeAnimatorController CreateMotionController(string key, string sourceDirectory)
        {
            string motlist = Path.Combine(sourceDirectory, "motlist.txt");
            if (!File.Exists(motlist))
            {
                animationReport.Add($"{key}: motlist.txt yok (statik model)");
                return null;
            }
            Dictionary<string, string> motionFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> stateOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadLines(motlist))
            {
                string[] columns = raw.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 3) continue;
                string state = StateForMotion(columns[1]);
                if (state == null) continue;
                if (stateOccurrences.TryGetValue(state, out int occurrence))
                {
                    occurrence++;
                    stateOccurrences[state] = occurrence;
                    state += occurrence;
                }
                else stateOccurrences[state] = 0;
                string motionFbx = Path.Combine(sourceDirectory, Path.ChangeExtension(columns[2], ".fbx"));
                if (File.Exists(motionFbx)) motionFiles.Add(state, ToAssetPath(motionFbx));
            }
            if (!motionFiles.ContainsKey("Wait"))
            {
                animationReport.Add($"{key}: WAIT hareketi veya FBX karşılığı yok");
                return null;
            }

            string safeKey = Metin3EntityPrefabCatalog.Normalize(key);
            string clipFolder = ClipRoot + "/" + safeKey;
            if (AssetDatabase.IsValidFolder(clipFolder)) AssetDatabase.DeleteAsset(clipFolder);
            EnsureAssetFolder(clipFolder);
            string controllerPath = ControllerRoot + "/" + safeKey + ".controller";
            AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);
            List<string> missingClips = new List<string>();
            foreach (KeyValuePair<string, string> pair in motionFiles)
            {
                AnimationClip sourceClip = LoadMotionClip(pair.Value);
                if (sourceClip == null)
                {
                    missingClips.Add(pair.Key + "=" + Path.GetFileName(pair.Value));
                    continue;
                }
                AnimationClip clip = UnityEngine.Object.Instantiate(sourceClip);
                clip.name = pair.Key;
                clip.legacy = false;
                clip.wrapMode = IsLoopState(pair.Key) ? WrapMode.Loop : WrapMode.Once;
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = IsLoopState(pair.Key);
                settings.loopBlend = IsLoopState(pair.Key);
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                string clipPath = clipFolder + "/" + pair.Key + ".anim";
                AssetDatabase.CreateAsset(clip, clipPath);
                AnimatorState state = machine.AddState(pair.Key);
                state.motion = clip;
                states[pair.Key] = state;
            }
            if (!states.TryGetValue("Wait", out AnimatorState wait))
            {
                animationReport.Add($"{key}: FBX içinden WAIT AnimationClip okunamadı");
                AssetDatabase.DeleteAsset(controllerPath);
                return null;
            }
            machine.defaultState = wait;
            foreach (KeyValuePair<string, AnimatorState> pair in states)
            {
                if (IsLoopState(pair.Key) || pair.Key.StartsWith("Dead", StringComparison.OrdinalIgnoreCase)) continue;
                AddReturnTransition(pair.Value, wait);
            }
            animationReport.Add($"{key}: {states.Count}/{motionFiles.Count} klip etkin" +
                                (missingClips.Count > 0 ? " | eksik: " + string.Join(", ", missingClips) : string.Empty));
            return controller;
        }

        static AnimationClip LoadMotionClip(string assetPath)
        {
            AnimationClip clip = FindMotionClip(assetPath);
            if (clip != null) return clip;

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return null;
            bool changed = !importer.importAnimation || importer.animationType != ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults != null && defaults.Length > 0 && (importer.clipAnimations == null || importer.clipAnimations.Length == 0))
            {
                importer.clipAnimations = defaults;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            clip = FindMotionClip(assetPath);
            if (clip != null) return clip;

            // Some Granny-exported motion FBX files expose their take only to the
            // legacy importer. The copied .anim is converted back to Mecanim above.
            importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return null;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.SaveAndReimport();
            return FindMotionClip(assetPath);
        }

        static AnimationClip FindMotionClip(string assetPath)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip != null && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) return clip;
            clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath).OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
            if (clip != null) return clip;
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
        }

        static void AddReturnTransition(AnimatorState state, AnimatorState wait)
        {
            AnimatorStateTransition transition = state.AddTransition(wait);
            transition.hasExitTime = true;
            transition.exitTime = 0.96f;
            transition.hasFixedDuration = true;
            transition.duration = 0.08f;
        }

        static string StateForMotion(string motion)
        {
            motion = (motion ?? string.Empty).Trim().ToUpperInvariant();
            if (motion.StartsWith("WAIT")) return "Wait";
            if (motion.StartsWith("WALK")) return "Walk";
            if (motion.StartsWith("RUN")) return "Run";
            if (motion.StartsWith("NORMAL_ATTACK") || motion.StartsWith("ATTACK")) return "Attack";
            if (motion.Contains("DAMAGE")) return "Hit";
            if (motion.EndsWith("DEAD") || motion.Contains("DEATH")) return "Dead";
            string[] words = motion.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return null;
            return string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));
        }

        static bool IsLoopState(string state) => state.StartsWith("Wait", StringComparison.OrdinalIgnoreCase) ||
                                                 state.StartsWith("Walk", StringComparison.OrdinalIgnoreCase) ||
                                                 state.StartsWith("Run", StringComparison.OrdinalIgnoreCase);

        static string BuildAnimationReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Metin3 original mob/NPC animation import report");
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine();
            foreach (string line in animationReport.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) report.AppendLine(line);
            return report.ToString();
        }

        static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
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

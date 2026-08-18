#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Metin2Dev.Frontend;
using Metin2Dev.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Metin2Dev.Editor
{
    public static class Metin2PlayerGameplayBuilder
    {
        const int BuilderVersion = 4;
        const string GeneratedRoot = "Assets/Metin2/Gameplay/Generated";
        const string AnimationRoot = GeneratedRoot + "/Animations";
        const string ResourceRoot = GeneratedRoot + "/Resources";
        const string DatabasePath = ResourceRoot + "/Metin2GameplayDatabase.asset";
        const string WarriorEffectRequest = "Metin2WarriorSkillEffects.request";
        static bool requestBuildRunning;

        [InitializeOnLoadMethod]
        static void RunRequestedBuild()
        {
            EditorApplication.update -= ProcessRequestedBuilds;
            EditorApplication.update += ProcessRequestedBuilds;
        }

        static void ProcessRequestedBuilds()
        {
            if (requestBuildRunning || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "Metin2PlayerGameplayBuild.request");
            if (File.Exists(marker))
            {
                File.Delete(marker);
                requestBuildRunning = true;
                try { Build(); }
                finally { requestBuildRunning = false; }
                return;
            }

            string warriorEffectMarker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", WarriorEffectRequest);
            if (File.Exists(warriorEffectMarker))
            {
                File.Delete(warriorEffectMarker);
                requestBuildRunning = true;
                try { BuildWarriorSkillEffects(); }
                finally { requestBuildRunning = false; }
            }
        }

        [MenuItem("Tools/Metin2/Rebuild Warrior 1-6 Skill Effects")]
        public static void BuildWarriorSkillEffects()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string extractedRoot = Path.Combine(projectRoot, "Metin2,5", "Extracted");
            try
            {
                EnsureAssetFolder(ResourceRoot);
                string[] manifests = AssetDatabase.FindAssets("", new[] { AnimationRoot })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
                string skillRoot = Path.Combine(extractedRoot, "PC", "ymir work", "pc", "warrior", "skill");
                string[] warriorSkillFiles = { "samyeon.msa", "palbang.msa", "jeongwi.msa", "geomgyeong.msa", "tanhwan.msa", "gihyeol.msa" };
                // The runtime uses the level variants (for example geomgyeong_2.msa), not only
                // the base skill files. Include each source skill and every authored level variant.
                string[] references = warriorSkillFiles.Select(Path.GetFileNameWithoutExtension)
                    .SelectMany(skill => Directory.GetFiles(skillRoot, skill + "*.msa", SearchOption.TopDirectoryOnly))
                    .Where(File.Exists)
                    .SelectMany(path => Regex.Matches(File.ReadAllText(path), "(?im)^\\s*EffectFileName\\s+\"([^\"]+)\"")
                        .Cast<Match>().Select(match => match.Groups[1].Value))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                Dictionary<string, GameObject> effects = Metin2Dev.Metin2MapImporter.BuildGameplayEffectPrefabs(
                    references, GeneratedRoot + "/Effects");
                Metin2GameplayDatabase database = BuildDatabase(manifests, extractedRoot, effects);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Metin2Player] Warrior skill effects rebuilt: {effects.Count}/{references.Length} source effects linked.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [Serializable] sealed class Manifest
        {
            public string pack;
            public string className;
            public string motionDirectory;
            public string bundle;
            public ClipEntry[] clips;
        }

        [Serializable] sealed class ClipEntry
        {
            public int index;
            public string unityClip;
            public string motion;
            public Definition[] definitions;
        }

        [Serializable] sealed class Definition
        {
            public string name;
            public string msa;
            public string duration;
            public string accumulation;
        }

        [MenuItem("Tools/Metin2/Build Player Gameplay %#g")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string convertedRoot = Path.Combine(projectRoot, "Metin2,5", "PlayerConverted");
            string extractedRoot = Path.Combine(projectRoot, "Metin2,5", "Extracted");
            if (!Directory.Exists(convertedRoot))
            {
                EditorUtility.DisplayDialog("Metin2 Player", "Önce Tools/Metin2Player/Convert-Metin2PlayerAnimations.bat çalıştırılmalı.\n\nBulunamadı:\n" + convertedRoot, "Tamam");
                return;
            }

            try
            {
                EnsureAssetFolder(AnimationRoot);
                EnsureAssetFolder(ResourceRoot);
                string[] sourceManifests = Directory.GetFiles(convertedRoot, "*.json", SearchOption.AllDirectories);
                int copied = CopyGeneratedFiles(convertedRoot, sourceManifests);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                string[] assetManifests = AssetDatabase.FindAssets("", new[] { AnimationRoot })
                    .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToArray();
                ConfigureAnimationImports(assetManifests);
                ConfigureRaceModels();

                // Make the playable character available first. Effect conversion can involve hundreds of source files
                // and must not leave the game without a player while that optional visual pass is running.
                Metin2GameplayDatabase database = BuildDatabase(assetManifests, extractedRoot, new Dictionary<string, GameObject>());
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();

                Dictionary<string, GameObject> effects = Metin2Dev.Metin2MapImporter.BuildGameplayEffectPrefabs(
                    FindEffectReferences(extractedRoot), GeneratedRoot + "/Effects");
                database = BuildDatabase(assetManifests, extractedRoot, effects);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = database;
                Debug.Log($"[Metin2Player] Gameplay hazır: {database.races.Sum(race => race.motions.Count)} hareket, {database.races.Count} karakter gövdesi, {copied} güncel dosya kopyalandı.");
                EditorUtility.DisplayDialog("Metin2 Player", $"Oyuncu sistemi hazır.\n\nKarakter: {database.races.Count}\nHareket kaydı: {database.races.Sum(race => race.motions.Count)}\nGüncellenen dosya: {copied}", "Tamam");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Metin2 Player", "Kurulum durdu:\n" + exception.Message, "Tamam");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static int CopyGeneratedFiles(string convertedRoot, string[] manifests)
        {
            int copied = 0;
            for (int i = 0; i < manifests.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Metin2 Player", "Animasyon paketleri projeye alınıyor", i / (float)Math.Max(1, manifests.Length));
                string sourceJson = manifests[i];
                string sourceFbx = Path.ChangeExtension(sourceJson, ".fbx");
                string relative = Path.GetRelativePath(convertedRoot, sourceJson).Replace('\\', '/');
                string destinationJson = AnimationRoot + "/" + relative;
                string destinationFbx = Path.ChangeExtension(destinationJson, ".fbx").Replace('\\', '/');
                EnsureAssetFolder(Path.GetDirectoryName(destinationJson).Replace('\\', '/'));
                copied += CopyIfChanged(sourceJson, ToAbsolute(destinationJson)) ? 1 : 0;
                if (File.Exists(sourceFbx)) copied += CopyIfChanged(sourceFbx, ToAbsolute(destinationFbx)) ? 1 : 0;
            }
            return copied;
        }

        static void ConfigureAnimationImports(IEnumerable<string> manifestPaths)
        {
            string[] paths = manifestPaths.ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Metin2 Player", "Animasyon klipleri ayarlanıyor", i / (float)Math.Max(1, paths.Length));
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ToAbsolute(paths[i])));
                string fbxPath = Path.ChangeExtension(paths[i], ".fbx").Replace('\\', '/');
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null) continue;
                bool changed = !importer.importAnimation || importer.animationType != ModelImporterAnimationType.Generic ||
                               importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel || importer.importCameras || importer.importLights ||
                               importer.materialImportMode != ModelImporterMaterialImportMode.None;
                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    ClipEntry entry = manifest.clips?.FirstOrDefault(item => item.unityClip == clip.name);
                    bool shouldLoop = entry != null && entry.definitions != null && entry.definitions.Any(item => IsLoop(item.name));
                    changed |= clip.loopTime != shouldLoop || clip.loopPose != shouldLoop;
                    clip.loopTime = shouldLoop;
                    clip.loopPose = clip.loopTime;
                }
                if (changed)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }
        }

        static void ConfigureRaceModels()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Metin2/Frontend/Art/Characters" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("_hair.fbx", StringComparison.OrdinalIgnoreCase)) continue;
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                bool changed = importer.animationType != ModelImporterAnimationType.Generic || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                if (changed) importer.SaveAndReimport();
            }
        }

        static Metin2GameplayDatabase BuildDatabase(string[] manifestPaths, string extractedRoot, Dictionary<string, GameObject> effects)
        {
            // This asset is entirely generated. Recreate it to recover safely from an interrupted Unity domain reload
            // instead of retaining a ScriptableObject with a missing script reference and an empty race list.
            // LoadAssetAtPath<T> returns null for a Missing Script asset, so test the path itself before deleting.
            if (AssetDatabase.LoadMainAssetAtPath(DatabasePath) != null) AssetDatabase.DeleteAsset(DatabasePath);
            Metin2GameplayDatabase database = ScriptableObject.CreateInstance<Metin2GameplayDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            database.races.Clear();

            foreach ((Metin2CharacterClass characterClass, Metin2Gender gender, string pack, string className) race in RaceDefinitions())
            {
                Metin2RaceMotionSet set = new Metin2RaceMotionSet
                {
                    characterClass = race.characterClass,
                    gender = race.gender,
                    sourcePack = race.pack,
                    playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Metin2/Frontend/Art/Characters/{RaceName(race.characterClass, race.gender)}/{RaceName(race.characterClass, race.gender)}.fbx")
                };

                List<(Metin2MotionRecord record, string stateName)> states = new List<(Metin2MotionRecord, string)>();
                foreach (string manifestPath in manifestPaths)
                {
                    Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ToAbsolute(manifestPath)));
                    if (!string.Equals(manifest.pack, race.pack, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(manifest.className, race.className, StringComparison.OrdinalIgnoreCase)) continue;
                    string mode = manifest.motionDirectory.Replace('\\', '/').Split('/').Last();
                    string fbxPath = Path.ChangeExtension(manifestPath, ".fbx").Replace('\\', '/');
                    AnimationClip[] importedClips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                        .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)).ToArray();
                    foreach (ClipEntry clipEntry in manifest.clips ?? Array.Empty<ClipEntry>())
                    {
                        AnimationClip clip = importedClips.FirstOrDefault(item => item.name == clipEntry.unityClip);
                        if (clip == null) continue;
                        foreach (Definition definition in clipEntry.definitions ?? Array.Empty<Definition>())
                        {
                            string msaPath = ResolveMsa(extractedRoot, race.pack, definition.msa);
                            string msa = File.Exists(msaPath) ? File.ReadAllText(msaPath) : string.Empty;
                            Metin2MotionRecord record = ParseMotion(mode, definition, msa, clip, effects);
                            set.motions.Add(record);
                            states.Add((record, Metin2PlayerController.StateName(record)));
                        }
                    }
                }
                set.animatorController = CreateController(race.characterClass, race.gender, states);
                database.races.Add(set);
            }
            return database;
        }

        static RuntimeAnimatorController CreateController(Metin2CharacterClass characterClass, Metin2Gender gender,
            List<(Metin2MotionRecord record, string stateName)> motions)
        {
            string name = RaceName(characterClass, gender);
            string folder = GeneratedRoot + "/Controllers";
            EnsureAssetFolder(folder);
            string path = folder + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach ((Metin2MotionRecord record, string stateName) item in motions.GroupBy(item => item.stateName).Select(group => group.First()))
            {
                AnimatorState state = machine.AddState(item.stateName);
                state.motion = item.record.clip;
                state.writeDefaultValues = false;
                if (item.record.mode == "general" && item.record.name == "wait") machine.defaultState = state;
            }
            return controller;
        }

        static Metin2MotionRecord ParseMotion(string mode, Definition definition, string msa, AnimationClip clip,
            Dictionary<string, GameObject> effects)
        {
            Metin2MotionRecord result = new Metin2MotionRecord
            {
                mode = mode,
                name = definition.name,
                clip = clip,
                duration = Float(msa, "MotionDuration", ParseFloat(definition.duration)),
                accumulation = Vector(msa, "Accumulation"),
                preInputTime = Float(msa, "PreInputTime", -1f),
                directInputTime = Float(msa, "DirectInputTime", -1f),
                inputLimitTime = Float(msa, "InputLimitTime", -1f),
                linkTime = Float(msa, "LinkTime", -1f),
                attackStartTime = Float(msa, "AttackingStartTime", -1f),
                attackEndTime = Float(msa, "AttackingEndTime", -1f),
                weaponLength = Float(msa, "WeaponLength", 0f),
                sourceMsa = definition.msa
            };
            foreach (string block in ExtractGroupBlocks(msa, "Event"))
            {
                int type = Int(block, "MotionEventType", -1);
                if (type != 1 && type != 4) continue;
                string effectReference = Quoted(block, "EffectFileName");
                string bone = Quoted(block, "AttachingBoneName");
                string sphere = ExtractGroupBlocks(block, "SphereData").FirstOrDefault() ?? string.Empty;
                Metin2MotionEvent motionEvent = new Metin2MotionEvent
                {
                    type = type,
                    startTime = Float(block, "StartingTime", 0f),
                    duration = Float(block, "DuringTime", 0f),
                    attachingBone = bone,
                    attachToBone = Int(block, "AttachingEnable", 0) != 0,
                    followAttachment = Int(block, "FollowingEnable", 0) != 0,
                    position = MetinVector(Vector(block, type == 1 ? "EffectPosition" : "Position")),
                    radius = Float(sphere, "Radius", 0f) / 100f,
                    hitLimit = Int(block, "HitLimitCount", 0),
                    effectPrefab = effects.TryGetValue(effectReference, out GameObject prefab) ? prefab : null
                };
                if (type == 4 && !string.IsNullOrWhiteSpace(sphere)) motionEvent.position = MetinVector(Vector(sphere, "Position"));
                result.events.Add(motionEvent);
            }
            result.events = result.events.OrderBy(item => item.startTime).ToList();
            if (result.duration <= 0f) result.duration = clip.length;
            return result;
        }

        static IEnumerable<string> FindEffectReferences(string extractedRoot)
        {
            foreach (string msaPath in Directory.GetFiles(extractedRoot, "*.msa", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(msaPath);
                foreach (Match match in Regex.Matches(text, "(?im)^\\s*EffectFileName\\s+\"([^\"]+)\""))
                    yield return match.Groups[1].Value;
            }
        }

        static List<string> ExtractGroupBlocks(string text, string groupPrefix)
        {
            List<string> result = new List<string>();
            MatchCollection matches = Regex.Matches(text ?? string.Empty, "(?i)\\bGroup\\s+" + Regex.Escape(groupPrefix) + "\\w*");
            foreach (Match match in matches)
            {
                int open = text.IndexOf('{', match.Index + match.Length);
                if (open < 0) continue;
                int depth = 0;
                for (int index = open; index < text.Length; index++)
                {
                    if (text[index] == '{') depth++;
                    else if (text[index] == '}' && --depth == 0) { result.Add(text.Substring(open + 1, index - open - 1)); break; }
                }
            }
            return result;
        }

        static string Quoted(string text, string key)
        {
            Match match = Regex.Match(text ?? string.Empty, "(?im)^\\s*" + Regex.Escape(key) + "\\s+\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        static int Int(string text, string key, int fallback)
        {
            Match match = Regex.Match(text ?? string.Empty, "(?im)^\\s*" + Regex.Escape(key) + "\\s+(-?[0-9]+)");
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        static Vector3 MetinVector(Vector3 value)
        {
            return new Vector3(value.x, value.z, -value.y) / 100f;
        }

        static string ResolveMsa(string extractedRoot, string pack, string relative)
        {
            string tree = pack.Equals("PC2", StringComparison.OrdinalIgnoreCase) ? "pc2" : "pc";
            return Path.Combine(extractedRoot, pack, "ymir work", tree, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        static IEnumerable<(Metin2CharacterClass, Metin2Gender, string, string)> RaceDefinitions()
        {
            yield return (Metin2CharacterClass.Warrior, Metin2Gender.Male, "PC", "warrior");
            yield return (Metin2CharacterClass.Warrior, Metin2Gender.Female, "PC2", "warrior");
            yield return (Metin2CharacterClass.Assassin, Metin2Gender.Female, "PC", "assassin");
            yield return (Metin2CharacterClass.Assassin, Metin2Gender.Male, "PC2", "assassin");
            yield return (Metin2CharacterClass.Sura, Metin2Gender.Male, "PC", "sura");
            yield return (Metin2CharacterClass.Sura, Metin2Gender.Female, "PC2", "sura");
            yield return (Metin2CharacterClass.Shaman, Metin2Gender.Female, "PC", "shaman");
            yield return (Metin2CharacterClass.Shaman, Metin2Gender.Male, "PC2", "shaman");
        }

        static string RaceName(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            string className = characterClass.ToString().ToLowerInvariant();
            return className + "_" + (gender == Metin2Gender.Male ? "m" : "w");
        }

        static bool IsLoop(string name) => name == "wait" || name == "wait_1" || name == "wait_2" || name == "walk" || name == "run" || name == "fishing_wait";

        static float Float(string text, string key, float fallback)
        {
            Match match = Regex.Match(text ?? string.Empty, "(?im)^\\s*" + Regex.Escape(key) + "\\s+(-?[0-9.]+)");
            return match.Success ? ParseFloat(match.Groups[1].Value) : fallback;
        }

        static Vector3 Vector(string text, string key)
        {
            Match match = Regex.Match(text ?? string.Empty, "(?im)^\\s*" + Regex.Escape(key) + "\\s+(-?[0-9.]+)\\s+(-?[0-9.]+)\\s+(-?[0-9.]+)");
            if (!match.Success) return Vector3.zero;
            return new Vector3(ParseFloat(match.Groups[1].Value), ParseFloat(match.Groups[2].Value), ParseFloat(match.Groups[3].Value));
        }

        static float ParseFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 0f;
        }

        static bool CopyIfChanged(string source, string destination)
        {
            FileInfo sourceInfo = new FileInfo(source);
            FileInfo destinationInfo = new FileInfo(destination);
            if (destinationInfo.Exists && destinationInfo.Length == sourceInfo.Length && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
            File.SetLastWriteTimeUtc(destination, sourceInfo.LastWriteTimeUtc);
            return true;
        }

        static string ToAbsolute(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static void EnsureAssetFolder(string path)
        {
            string normalized = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;
            string parent = Path.GetDirectoryName(normalized).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(normalized));
        }
    }
}
#endif

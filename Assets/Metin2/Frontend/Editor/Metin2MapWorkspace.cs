using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Metin2Dev.Frontend.Editor
{
    /// <summary>
    /// Keeps the authored login flow separate from the additive map authoring workspace.
    /// Scene files are never generated, modified or deleted by this utility.
    /// </summary>
    public static class Metin2MapWorkspace
    {
        const string PreferencesKeyPrefix = "Metin2.MapWorkspace.v1.";

        static readonly string[] RecoveryWorkspace =
        {
            "Assets/Metin2/Generated/Scenes/metin2_map_c1.unity",
            "Assets/Metin2/Generated/Scenes/metin2_map_b1.unity",
            "Assets/Map/Assets/Scenes/Örümcek Zindanı 1/OrumcekZindan1.unity",
            "Assets/Map/Assets/Scenes/Tapınak.unity",
            "Assets/Metin2/Generated/Scenes/Tapınak.unity",
        };

        [Serializable]
        sealed class WorkspaceData
        {
            public List<WorkspaceScene> scenes = new List<WorkspaceScene>();
        }

        [Serializable]
        sealed class WorkspaceScene
        {
            public string path;
            public bool isLoaded;
            public bool isActive;
        }

        [MenuItem("Tools/Metin2/Return to Map Workspace", priority = 21)]
        public static void ReturnToMapWorkspace()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            WorkspaceData workspace = LoadRememberedWorkspace();
            if (workspace == null || workspace.scenes.Count == 0)
                workspace = BuildRecoveryWorkspace();

            List<WorkspaceScene> available = workspace.scenes
                .Where(scene => scene != null && SceneExists(scene.path))
                .GroupBy(scene => scene.path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (available.Count == 0)
            {
                const string message = "Açılabilecek kayıtlı bir harita sahnesi bulunamadı. Harita dosyaları Project penceresinden kontrol edilmeli.";
                Debug.LogError("[Metin2 Workspace] " + message);
                EditorUtility.DisplayDialog("Metin2 - Harita Çalışma Alanı", message, "Tamam");
                return;
            }

            SceneSetup[] setup = available.Select(scene => new SceneSetup
            {
                path = scene.path,
                isLoaded = scene.isLoaded,
                isActive = scene.isActive,
                isSubScene = false,
            }).ToArray();

            if (!setup.Any(scene => scene.isLoaded)) setup[0].isLoaded = true;
            if (!setup.Any(scene => scene.isActive)) setup[0].isActive = true;

            try
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                Debug.Log("[Metin2 Workspace] Harita çalışma alanı geri yüklendi: " +
                          string.Join(", ", setup.Select(scene => scene.path)));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Metin2 - Harita Çalışma Alanı",
                    "Harita çalışma alanı açılırken Unity bir hata bildirdi. Hiçbir sahne dosyası silinmedi veya yeniden oluşturulmadı.\n\n" + exception.Message,
                    "Tamam");
            }
        }

        public static void RememberCurrentWorkspace(string loginScenePath)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            List<WorkspaceScene> mapScenes = setup
                .Where(scene => !string.IsNullOrWhiteSpace(scene.path))
                .Where(scene => !string.Equals(scene.path, loginScenePath, StringComparison.OrdinalIgnoreCase))
                .Select(scene => new WorkspaceScene
                {
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    isActive = scene.isActive,
                })
                .ToList();

            // Opening Login Flow while it is already the only open scene must not erase
            // the last useful map workspace.
            if (mapScenes.Count == 0) return;

            EditorPrefs.SetString(PreferencesKey, JsonUtility.ToJson(new WorkspaceData { scenes = mapScenes }));
            Debug.Log("[Metin2 Workspace] Mevcut harita çalışma alanı hatırlandı (" + mapScenes.Count + " sahne).");
        }

        static WorkspaceData LoadRememberedWorkspace()
        {
            string json = EditorPrefs.GetString(PreferencesKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonUtility.FromJson<WorkspaceData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Metin2 Workspace] Kayıtlı çalışma alanı okunamadı; kurtarma düzeni kullanılacak. " + exception.Message);
                return null;
            }
        }

        static WorkspaceData BuildRecoveryWorkspace()
        {
            List<string> paths = RecoveryWorkspace.Where(SceneExists).ToList();

            // There are two Tapınak scene variants in some project copies. Prefer the
            // authored Assets/Map version and do not load both into the Hierarchy.
            string authoredTemple = "Assets/Map/Assets/Scenes/Tapınak.unity";
            if (paths.Contains(authoredTemple))
                paths.Remove("Assets/Metin2/Generated/Scenes/Tapınak.unity");

            WorkspaceData workspace = new WorkspaceData();
            for (int index = 0; index < paths.Count; index++)
            {
                workspace.scenes.Add(new WorkspaceScene
                {
                    path = paths[index],
                    isLoaded = true,
                    isActive = index == paths.Count - 1,
                });
            }
            return workspace;
        }

        static bool SceneExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        static string PreferencesKey
        {
            get
            {
                string projectPath = Application.dataPath.Replace('\\', '/');
                return PreferencesKeyPrefix + projectPath.ToLowerInvariant();
            }
        }
    }
}

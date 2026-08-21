#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metin2Dev.Editor
{
    [InitializeOnLoad]
    public static class MobileHUDAuthoredPrefabExporter
    {
        private const string SourceScene = "Assets/Map/Assets/Scenes/Tapınak.unity";
        private const string OutputFolder = "Assets/Map/Assets/Resources";
        private const string OutputPrefab = OutputFolder + "/MobileHUD.prefab";
        private static bool exportInProgress;

        static MobileHUDAuthoredPrefabExporter()
        {
            EditorApplication.delayCall += Initialize;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private static void Initialize()
        {
            RemoveHudCopiesFromLoadedNonSourceScenes();
            ExportIfNeeded();
        }

        [MenuItem("Tools/Metin2/UI/Export Authored MobileHUD Prefab")]
        public static void ExportAuthoredHud()
        {
            Export(false, SourceScene);
        }

        [MenuItem("Tools/Metin2/UI/Remove Duplicate MobileHUD Copies")]
        public static void RemoveHudCopiesFromLoadedNonSourceScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                return;

            int removed = 0;
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || SameAssetPath(scene.path, SourceScene))
                    continue;

                List<GameObject> copies = new List<GameObject>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (candidate != null && string.Equals(candidate.name, "MobileHUD",
                                StringComparison.OrdinalIgnoreCase))
                            copies.Add(candidate.gameObject);
                    }
                }

                if (copies.Count == 0)
                    continue;
                foreach (GameObject copy in copies)
                {
                    if (copy == null)
                        continue;
                    Undo.DestroyObjectImmediate(copy);
                    removed++;
                }
                EditorSceneManager.MarkSceneDirty(scene);
            }

            if (removed > 0)
                Debug.Log("[MobileHUD] Removed " + removed
                    + " duplicate HUD copies. Tapınak/Canvas/MobileHUD remains the authored source.");
        }

        private static void ExportIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefab) == null)
            {
                Export(true, SourceScene);
                return;
            }
            if (File.Exists(SourceScene) && File.GetLastWriteTimeUtc(SourceScene) > File.GetLastWriteTimeUtc(OutputPrefab))
                Export(false, SourceScene);
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (exportInProgress || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer ||
                !scene.IsValid() || !scene.isLoaded || !SameAssetPath(scene.path, SourceScene) ||
                FindNamedTransform(scene, "MobileHUD") == null)
                return;
            EditorApplication.delayCall += () => Export(false, SourceScene);
        }

        private static bool SameAssetPath(string left, string right)
        {
            return string.Equals((left ?? string.Empty).Replace('\\', '/'),
                (right ?? string.Empty).Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static void Export(bool onlyIfMissing, string sourcePath)
        {
            if (exportInProgress || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                return;
            if (onlyIfMissing && AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefab) != null)
                return;
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                Debug.LogWarning("[MobileHUD] Source scene for the authored HUD was not found: " + sourcePath);
                return;
            }

            exportInProgress = true;
            Scene source = SceneManager.GetSceneByPath(sourcePath);
            bool openedForExport = !source.IsValid() || !source.isLoaded;
            Scene previousActive = SceneManager.GetActiveScene();
            try
            {
                if (openedForExport)
                    source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);

                Transform hud = FindNamedTransform(source, "MobileHUD");
                if (hud == null)
                {
                    Debug.LogError("[MobileHUD] Canvas/MobileHUD was not found in " + sourcePath);
                    return;
                }

                Directory.CreateDirectory(OutputFolder);
                GameObject cleanHud = CloneHierarchy(hud.gameObject);
                MobileHUDCanvasProfile profile = cleanHud.GetComponent<MobileHUDCanvasProfile>();
                if (profile == null) profile = cleanHud.AddComponent<MobileHUDCanvasProfile>();
                profile.CaptureFrom(hud.GetComponentInParent<Canvas>());
                PrefabUtility.SaveAsPrefabAsset(cleanHud, OutputPrefab, out bool success);
                UnityEngine.Object.DestroyImmediate(cleanHud);
                if (!success)
                {
                    Debug.LogError("[MobileHUD] Authored HUD prefab export failed: " + OutputPrefab);
                    return;
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[MobileHUD] Authored HUD prefab exported: " + OutputPrefab);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (openedForExport && source.IsValid() && source.isLoaded)
                    EditorSceneManager.CloseScene(source, true);
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                exportInProgress = false;
            }
        }

        private static Transform FindNamedTransform(Scene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in descendants)
                    if (candidate != null && string.Equals(candidate.name, objectName,
                            StringComparison.OrdinalIgnoreCase))
                        return candidate;
            }
            return null;
        }

        private static GameObject CloneHierarchy(GameObject sourceRoot)
        {
            Dictionary<UnityEngine.Object, UnityEngine.Object> map =
                new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            List<KeyValuePair<Component, Component>> componentPairs =
                new List<KeyValuePair<Component, Component>>();
            GameObject clone = CloneObjects(sourceRoot, null, map, componentPairs);
            foreach (KeyValuePair<Component, Component> pair in componentPairs)
            {
                if (pair.Key == null || pair.Value == null)
                    continue;
                try
                {
                    EditorUtility.CopySerialized(pair.Key, pair.Value);
                    RemapObjectReferences(pair.Value, map);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[MobileHUD] Skipping component data " + pair.Key.GetType().Name
                        + ": " + exception.Message);
                }
            }
            return clone;
        }

        private static GameObject CloneObjects(GameObject source, Transform parent,
            Dictionary<UnityEngine.Object, UnityEngine.Object> map,
            List<KeyValuePair<Component, Component>> componentPairs)
        {
            bool usesRectTransform = source.transform is RectTransform;
            GameObject clone = usesRectTransform
                ? new GameObject(source.name, typeof(RectTransform))
                : new GameObject(source.name);
            clone.layer = source.layer;
            clone.tag = source.tag;
            clone.SetActive(source.activeSelf);
            clone.transform.SetParent(parent, false);
            map[source] = clone;
            map[source.transform] = clone.transform;

            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
            if (source.transform is RectTransform sourceRect && clone.transform is RectTransform cloneRect)
            {
                cloneRect.anchorMin = sourceRect.anchorMin;
                cloneRect.anchorMax = sourceRect.anchorMax;
                cloneRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
                cloneRect.sizeDelta = sourceRect.sizeDelta;
                cloneRect.pivot = sourceRect.pivot;
            }

            foreach (Component sourceComponent in source.GetComponents<Component>())
            {
                if (sourceComponent == null || sourceComponent is Transform)
                    continue;
                Type componentType = sourceComponent.GetType();
                string typeName = componentType.Name;
                if (typeName == "MobileHUDOnly" || typeName == "MobileHUDInputBridge"
                    || typeName == "MobileHUDActionButton" || typeName == "Metin2QuickSlotView"
                    || typeName == "Metin2QuickSlotDragSource")
                    continue;
                try
                {
                    Component cloneComponent = clone.AddComponent(componentType);
                    if (cloneComponent == null)
                    {
                        Debug.LogWarning("[MobileHUD] Skipping component Unity could not clone: " + typeName);
                        continue;
                    }
                    map[sourceComponent] = cloneComponent;
                    componentPairs.Add(new KeyValuePair<Component, Component>(sourceComponent, cloneComponent));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[MobileHUD] Skipping invalid component " + typeName + ": "
                        + exception.Message);
                }
            }

            for (int index = 0; index < source.transform.childCount; index++)
                CloneObjects(source.transform.GetChild(index).gameObject, clone.transform, map, componentPairs);
            return clone;
        }

        private static void RemapObjectReferences(Component destination,
            Dictionary<UnityEngine.Object, UnityEngine.Object> map)
        {
            SerializedObject serialized = new SerializedObject(destination);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                UnityEngine.Object current = property.objectReferenceValue;
                if (current != null && map.TryGetValue(current, out UnityEngine.Object replacement))
                    property.objectReferenceValue = replacement;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif

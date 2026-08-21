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

        static MobileHUDAuthoredPrefabExporter()
        {
            EditorApplication.delayCall += ExportIfMissing;
        }

        [MenuItem("Tools/Metin2/UI/Export Authored MobileHUD Prefab")]
        public static void ExportAuthoredHud()
        {
            Export(false);
        }

        private static void ExportIfMissing()
        {
            Export(true);
        }

        private static void Export(bool onlyIfMissing)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                return;
            if (onlyIfMissing && AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefab) != null)
                return;
            if (!File.Exists(SourceScene))
            {
                Debug.LogWarning("[MobileHUD] Source scene for the authored HUD was not found: " + SourceScene);
                return;
            }

            Scene source = SceneManager.GetSceneByPath(SourceScene);
            bool openedForExport = !source.IsValid() || !source.isLoaded;
            Scene previousActive = SceneManager.GetActiveScene();
            try
            {
                if (openedForExport)
                    source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);

                Transform hud = FindNamedTransform(source, "MobileHUD");
                if (hud == null)
                {
                    Debug.LogError("[MobileHUD] Canvas/MobileHUD was not found in " + SourceScene);
                    return;
                }

                Directory.CreateDirectory(OutputFolder);
                GameObject cleanHud = CloneHierarchy(hud.gameObject);
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
                EditorUtility.CopySerialized(pair.Key, pair.Value);
                RemapObjectReferences(pair.Value, map);
            }
            return clone;
        }

        private static GameObject CloneObjects(GameObject source, Transform parent,
            Dictionary<UnityEngine.Object, UnityEngine.Object> map,
            List<KeyValuePair<Component, Component>> componentPairs)
        {
            bool usesRectTransform = source.transform is RectTransform;
            GameObject clone = new GameObject(source.name,
                usesRectTransform ? typeof(RectTransform) : typeof(Transform));
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
                    || typeName == "MobileHUDActionButton")
                    continue;
                try
                {
                    Component cloneComponent = clone.AddComponent(componentType);
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

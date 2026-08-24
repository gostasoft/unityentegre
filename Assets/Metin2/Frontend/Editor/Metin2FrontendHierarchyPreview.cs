using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Metin2Dev.Frontend.Editor
{
    [InitializeOnLoad]
    public static class Metin2FrontendHierarchyPreview
    {
        const string LayoutName = "Metin2 Frontend Editable Layout";

        static readonly HashSet<string> ScreenNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Login Screen",
            "Empire Selection",
            "Character Selection",
            "Character Creation",
            "Loading Screen",
        };

        static bool changingVisibility;

        static Metin2FrontendHierarchyPreview()
        {
            Selection.selectionChanged += ShowSelectedScreen;
        }

        static void ShowSelectedScreen()
        {
            if (changingVisibility || EditorApplication.isPlayingOrWillChangePlaymode) return;

            Transform selected = Selection.activeTransform;
            if (selected == null) return;

            Transform selectedScreen = null;
            Transform layout = null;
            for (Transform current = selected; current != null; current = current.parent)
            {
                if (selectedScreen == null && ScreenNames.Contains(current.name)) selectedScreen = current;
                if (current.name != LayoutName) continue;
                layout = current;
                break;
            }

            if (layout == null || selectedScreen == null || selectedScreen.parent != layout) return;

            List<GameObject> screens = new List<GameObject>();
            foreach (Transform child in layout)
                if (ScreenNames.Contains(child.name)) screens.Add(child.gameObject);

            bool requiresChange = HideCharacterSlotPlaceholders(selectedScreen);
            foreach (GameObject screen in screens)
            {
                bool shouldBeVisible = screen.transform == selectedScreen;
                if (screen.activeSelf != shouldBeVisible) requiresChange = true;
            }
            if (!requiresChange) return;

            changingVisibility = true;
            try
            {
                Undo.RecordObjects(screens.ToArray(), "Preview frontend screen");
                foreach (GameObject screen in screens)
                    screen.SetActive(screen.transform == selectedScreen);

                if (layout.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
            finally
            {
                changingVisibility = false;
            }
        }

        static bool HideCharacterSlotPlaceholders(Transform selectedScreen)
        {
            if (selectedScreen.name != "Character Selection") return false;

            Transform listPanel = FindChild(selectedScreen, "Saved Characters");
            if (listPanel == null) return false;

            bool changed = false;
            foreach (Transform child in listPanel)
            {
                if (child.GetComponent<Button>() == null ||
                    child.name == "Bayrak Seçimi Button" ||
                    child.name == "Hesaptan Çık Button" ||
                    child.name == "+  Yeni Karakter Button" ||
                    child.name.StartsWith("Runtime Character Slot ", StringComparison.Ordinal))
                    continue;

                if (!child.gameObject.activeSelf) continue;
                Undo.RecordObject(child.gameObject, "Hide frontend character placeholder");
                child.gameObject.SetActive(false);
                changed = true;
            }
            return changed;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

            bool requiresChange = false;
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
    }
}

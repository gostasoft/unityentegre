using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Metin2Dev.Frontend.Editor
{
    [InitializeOnLoad]
    public static class Metin2FrontendCompositionAuthoring
    {
        const string EditableCanvasName = "Metin2 Frontend Editable Layout";

        static Metin2FrontendCompositionAuthoring()
        {
            EditorApplication.delayCall += EnsureLoadedCanvases;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += EnsureLoadedCanvases;
            };
        }

        [MenuItem("Tools/Metin2/Lock Frontend Screen Composition", priority = 24)]
        public static void EnsureLoadedCanvases()
        {
            if (Application.isPlaying) return;
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas.name != EditableCanvasName || !canvas.gameObject.scene.IsValid() ||
                    !canvas.gameObject.scene.isLoaded)
                    continue;
                EnsureCanvas(canvas);
            }
        }

        internal static bool EnsureCanvas(Canvas canvas)
        {
            if (canvas == null) return false;
            Metin2FrontendCompositionScaler composition =
                canvas.GetComponent<Metin2FrontendCompositionScaler>();
            bool changed = false;
            if (composition == null)
            {
                composition = Undo.AddComponent<Metin2FrontendCompositionScaler>(canvas.gameObject);
                changed = true;
            }

            if (!composition.HasCapturedResolution)
            {
                Undo.RecordObjects(new Object[] { composition, canvas.GetComponent<CanvasScaler>() },
                    "Lock frontend composition");
                if (composition.CaptureCurrentLayout()) changed = true;
            }
            else composition.ApplyNow();

            if (changed)
            {
                EditorUtility.SetDirty(composition);
                EditorUtility.SetDirty(canvas.GetComponent<CanvasScaler>());
                EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                Debug.Log("[Metin2 Frontend] Screen composition locked at " +
                    composition.AuthoredResolution + ". Background and UI now scale together.", canvas);
            }
            return changed;
        }
    }
}

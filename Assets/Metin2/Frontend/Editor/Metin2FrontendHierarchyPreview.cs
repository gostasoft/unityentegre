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

            changingVisibility = true;
            try
            {
                bool changed = PrepareCharacterSlotTemplate(selectedScreen);
                Undo.RecordObjects(screens.ToArray(), "Preview frontend screen");
                foreach (GameObject screen in screens)
                {
                    bool shouldBeVisible = screen.transform == selectedScreen;
                    if (screen.activeSelf == shouldBeVisible) continue;
                    screen.SetActive(shouldBeVisible);
                    changed = true;
                }

                if (changed && layout.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                if (changed)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                }
            }
            finally
            {
                changingVisibility = false;
            }
        }

        internal static bool PrepareCharacterSlotTemplate(Transform selectedScreen)
        {
            if (selectedScreen.name != "Character Selection") return false;

            Transform listPanel = FindChild(selectedScreen, "Saved Characters");
            if (listPanel == null) return false;

            Button newCharacter = FindDirectButton(listPanel, "+  Yeni Karakter Button");
            List<Button> candidates = new List<Button>();
            foreach (Transform child in listPanel)
            {
                Button button = child.GetComponent<Button>();
                if (button == null || IsCharacterListControl(child.name)) continue;
                candidates.Add(button);
            }

            Button template = candidates.Find(button => button.name == "Character Slot Template");
            if (template == null && candidates.Count > 0) template = candidates[0];
            if (template == null && newCharacter != null)
            {
                template = UnityEngine.Object.Instantiate(newCharacter, listPanel, false);
                Undo.RegisterCreatedObjectUndo(template.gameObject, "Create character slot template");
                RectTransform rect = template.GetComponent<RectTransform>();
                rect.anchoredPosition += new Vector2(0f, rect.rect.height + 18f);
            }
            if (template == null) return false;

            bool changed = false;
            bool freshTemplate = template.name != "Character Slot Template";
            if (freshTemplate)
            {
                Undo.RecordObject(template.gameObject, "Name character slot template");
                template.name = "Character Slot Template";
                changed = true;
            }
            if (!template.gameObject.activeSelf)
            {
                Undo.RecordObject(template.gameObject, "Show character slot template");
                template.gameObject.SetActive(true);
                changed = true;
            }

            foreach (Button candidate in candidates)
            {
                if (candidate == template || !candidate.gameObject.activeSelf) continue;
                Undo.RecordObject(candidate.gameObject, "Hide extra character slot template");
                candidate.gameObject.SetActive(false);
                changed = true;
            }

            if (EnsureTemplateVisuals(template, freshTemplate)) changed = true;
            return changed;
        }

        static bool EnsureTemplateVisuals(Button template, bool freshTemplate)
        {
            RectTransform row = template.GetComponent<RectTransform>();
            Text name = FindChild(row, "Character Name")?.GetComponent<Text>();
            if (name == null)
            {
                name = template.GetComponentInChildren<Text>(true);
                if (name != null)
                {
                    Undo.RecordObject(name.gameObject, "Prepare character name field");
                    name.name = "Character Name";
                    SetTopLeft(name.rectTransform, new Vector2(78f, -9f),
                        new Vector2(Mathf.Max(72f, row.rect.width - 86f), 28f));
                    name.alignment = TextAnchor.MiddleLeft;
                    name.fontSize = Mathf.Max(14, name.fontSize);
                }
            }
            if (name != null && freshTemplate)
            {
                Undo.RecordObject(name, "Set character template example");
                name.text = "Örnek Karakter";
            }

            Text details = FindChild(row, "Character Details")?.GetComponent<Text>();
            bool changed = false;
            if (details == null)
            {
                GameObject detailsObject = new GameObject("Character Details", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Text));
                Undo.RegisterCreatedObjectUndo(detailsObject, "Create character detail field");
                detailsObject.transform.SetParent(row, false);
                details = detailsObject.GetComponent<Text>();
                details.font = name != null ? name.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                details.fontSize = 11;
                details.fontStyle = FontStyle.Normal;
                details.alignment = TextAnchor.MiddleLeft;
                details.color = new Color(0.82f, 0.74f, 0.59f);
                details.raycastTarget = false;
                details.text = "ŞABLON • Savaşçı • Sv. 1";
                SetTopLeft(details.rectTransform, new Vector2(78f, -39f),
                    new Vector2(Mathf.Max(72f, row.rect.width - 86f), 22f));
                changed = true;
            }

            RectTransform portrait = FindChild(row, "Character Portrait") as RectTransform;
            if (portrait == null)
            {
                GameObject portraitObject = new GameObject("Character Portrait", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(RawImage));
                Undo.RegisterCreatedObjectUndo(portraitObject, "Create character portrait field");
                portraitObject.transform.SetParent(row, false);
                portrait = portraitObject.GetComponent<RectTransform>();
                SetTopLeft(portrait, new Vector2(8f, -8f), new Vector2(62f, 62f));
                RawImage image = portraitObject.GetComponent<RawImage>();
                image.color = new Color(0.12f, 0.105f, 0.09f, 0.96f);
                image.raycastTarget = false;

                GameObject placeholderObject = new GameObject("Portrait Placeholder", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Text));
                Undo.RegisterCreatedObjectUndo(placeholderObject, "Create portrait placeholder");
                placeholderObject.transform.SetParent(portrait, false);
                RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
                placeholderRect.anchorMin = Vector2.zero;
                placeholderRect.anchorMax = Vector2.one;
                placeholderRect.pivot = new Vector2(0.5f, 0.5f);
                placeholderRect.anchoredPosition = Vector2.zero;
                placeholderRect.sizeDelta = new Vector2(-4f, -4f);
                Text placeholder = placeholderObject.GetComponent<Text>();
                placeholder.font = details.font;
                placeholder.text = "KARAKTER\nPORTRESİ";
                placeholder.fontSize = 8;
                placeholder.alignment = TextAnchor.MiddleCenter;
                placeholder.color = new Color(0.78f, 0.63f, 0.34f);
                placeholder.raycastTarget = false;
                changed = true;
            }
            return changed;
        }

        static bool IsCharacterListControl(string name)
        {
            return name == "Bayrak Seçimi Button" ||
                   name == "Hesaptan Çık Button" ||
                   name == "+  Yeni Karakter Button" ||
                   name.StartsWith("Runtime Character Slot ", StringComparison.Ordinal);
        }

        static Button FindDirectButton(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child.GetComponent<Button>();
            return null;
        }

        static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}

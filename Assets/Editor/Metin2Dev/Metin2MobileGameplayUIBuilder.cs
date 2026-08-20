#if UNITY_EDITOR
using System.IO;
using Metin2Dev.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Metin2Dev.Editor
{
    [InitializeOnLoad]
    public static class Metin2MobileGameplayUIBuilder
    {
        const string PrefabFolder = "Assets/Metin2/UI/Resources";
        const string PrefabPath = PrefabFolder + "/Metin2MobileGameplayUI.prefab";
        const string TextureFolder = "Assets/Metin2/UI/Resources/Metin2UI";

        static Metin2MobileGameplayUIBuilder()
        {
            EditorApplication.delayCall += EnsurePrefabAndOpenScene;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += EnsurePrefabAndOpenScene;
        }

        [MenuItem("Tools/Metin2/UI/Create or Update Mobile Gameplay UI Prefab")]
        public static void CreateOrUpdatePrefab()
        {
            BuildPrefab(true);
            AddToOpenScene();
        }

        [MenuItem("Tools/Metin2/UI/Add Mobile Gameplay UI To Open Scene")]
        public static void AddToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!CanAuthorScene(scene)) return;
            if (FindInScene(scene) != null) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                BuildPrefab(false);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            if (prefab == null) return;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null) return;
            instance.name = "Metin2 Mobile Gameplay UI";
            Undo.RegisterCreatedObjectUndo(instance, "Add Metin2 Mobile Gameplay UI");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
            Debug.Log("[Metin2MobileUI] Mobil HUD sahneye eklendi. Hierarchy'den duzenleyip Ctrl+S ile kaydedebilirsiniz.");
        }

        public static void CreateOrUpdatePrefabFromCommandLine()
        {
            BuildPrefab(true);
        }

        static void EnsurePrefabAndOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool needsControls = existing == null || existing.GetComponentInChildren<Metin2MobileJoystick>(true) == null;
            if (needsControls) BuildPrefab(true);
            AddToOpenScene();
        }

        static void BuildPrefab(bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            EnsureTextures();
            Directory.CreateDirectory(PrefabFolder);

            Texture2D taskbar = LoadTexture("taskbar.tga");
            Texture2D publicAtlas = LoadTexture("public.dds");
            Texture2D warriorSkills = LoadTexture("skillwarrior.dds");
            Texture2D hpGauge = LoadTexture("hp_gauge_01.tga");
            Texture2D spGauge = LoadTexture("sp_gauge_01.tga");
            Texture2D staminaGauge = LoadTexture("st_gauge_01.tga");
            if (taskbar == null || publicAtlas == null || warriorSkills == null || hpGauge == null ||
                spGauge == null || staminaGauge == null)
            {
                Debug.LogError("[Metin2MobileUI] Gerekli orijinal Metin2 UI dokulari bulunamadi.");
                return;
            }

            GameObject root = new GameObject("Metin2 Mobile Gameplay UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40010;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 768f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            RectTransform safeArea = CreateStretch(root.transform, "Safe Area");
            RectTransform status = CreateTopLeft(safeArea, "Top Left Status", 18f, 18f, 158f, 88f);
            status.localScale = Vector3.one * 1.25f;

            RectTransform gaugeRoot = CreateTopLeft(status, "HP SP Stamina", 0f, 0f, 158f, 47f);
            RawImage hpFill = CreateRaw(gaugeRoot, "HP Fill", hpGauge, 59f, 14f, 95f, 11f, new Rect(0f, 0f, 1f, 1f));
            RawImage spFill = CreateRaw(gaugeRoot, "SP Fill", spGauge, 59f, 24f, 95f, 11f, new Rect(0f, 0f, 1f, 1f));
            RawImage staminaFill = CreateRaw(gaugeRoot, "Stamina Fill", staminaGauge, 59f, 38f, 95f, 6f, new Rect(0f, 0f, 1f, 1f));
            CreateRaw(gaugeRoot, "Gauge Frame", taskbar, 0f, 0f, 158f, 47f, AtlasUv(taskbar, new Rect(0f, 0f, 158f, 47f)));
            Text hpText = CreateText(gaugeRoot, "HP Value", 59f, 1f, 95f, 11f, "590 / 590");
            Text spText = CreateText(gaugeRoot, "SP Value", 59f, 25f, 95f, 10f, "187 / 187");

            RectTransform experienceRoot = CreateTopLeft(status, "Four Part EXP", 0f, 50f, 105f, 37f);
            CreateRaw(experienceRoot, "EXP Frame", taskbar, 0f, 0f, 105f, 37f,
                AtlasUv(taskbar, new Rect(158f, 0f, 105f, 37f)));
            RawImage[] experiencePoints = new RawImage[4];
            Rect pointUv = AtlasUv(taskbar, new Rect(487f, 0f, 19f, 19f));
            for (int index = 0; index < experiencePoints.Length; index++)
                experiencePoints[index] = CreateRaw(experienceRoot, "EXP Tube " + (index + 1), taskbar,
                    5f + index * 25f, 9f, 19f, 19f, pointUv);

            RectTransform controls = CreateStretch(safeArea, "Mobile Controls");
            Metin2MobileInputDriver inputDriver = controls.gameObject.AddComponent<Metin2MobileInputDriver>();

            RectTransform lookArea = CreateStretch(controls, "Camera Swipe Area");
            lookArea.anchorMin = new Vector2(0.34f, 0.18f);
            Image lookRaycast = lookArea.gameObject.AddComponent<Image>();
            lookRaycast.color = new Color(0f, 0f, 0f, 0.001f);
            lookRaycast.raycastTarget = true;
            lookArea.gameObject.AddComponent<Metin2MobileCameraLookArea>();

            Rect slotSource = new Rect(0f, 348f, 32f, 32f);
            Rect slotUv = AtlasUv(publicAtlas, slotSource);
            RectTransform joystickRoot = CreateCenter(controls, "Movement Joystick", Vector2.zero,
                new Vector2(108f, 108f), new Vector2(154f, 154f));
            RawImage joystickBackground = AddRaw(joystickRoot, publicAtlas, slotUv, true);
            joystickBackground.color = new Color(1f, 1f, 1f, 0.62f);
            RectTransform joystickHandle = CreateCenter(joystickRoot, "Joystick Handle", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(64f, 64f));
            RawImage handleImage = AddRaw(joystickHandle, publicAtlas, slotUv, false);
            handleImage.color = new Color(1f, 0.88f, 0.62f, 0.94f);
            joystickRoot.gameObject.AddComponent<Metin2MobileJoystick>().Configure(joystickRoot, joystickHandle);

            RectTransform actions = CreateCenter(controls, "Combat Controls", new Vector2(1f, 0f),
                new Vector2(-190f, 190f), new Vector2(360f, 360f));
            Vector2[] skillPositions =
            {
                new Vector2(-125f, 0f), new Vector2(-88f, 88f), new Vector2(0f, 125f),
                new Vector2(88f, 88f), new Vector2(125f, 0f), new Vector2(88f, -88f),
                new Vector2(0f, -125f), new Vector2(-88f, -88f),
            };
            string[] skillLabels = { "1", "2", "3", "4", "F1", "F2", "F3", "F4" };
            Rect[] warriorRects =
            {
                new Rect(0, 96, 32, 32), new Rect(160, 64, 32, 32), new Rect(64, 64, 32, 32),
                new Rect(192, 0, 32, 32), new Rect(96, 96, 32, 32), new Rect(32, 32, 32, 32),
            };
            RawImage[] skillIcons = new RawImage[8];
            for (int index = 0; index < skillIcons.Length; index++)
            {
                RectTransform skill = CreateCenter(actions, "Skill " + (index + 1), new Vector2(0.5f, 0.5f),
                    skillPositions[index], new Vector2(58f, 58f));
                RawImage frame = AddRaw(skill, publicAtlas, slotUv, true);
                if (index < warriorRects.Length)
                {
                    RectTransform iconRect = CreateCenter(skill, "Skill Icon", new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(48f, 48f));
                    skillIcons[index] = AddRaw(iconRect, warriorSkills, AtlasUv(warriorSkills, warriorRects[index]), false);
                }
                AddCenteredText(skill, "Skill Key", skillLabels[index], new Vector2(-18f, 19f), 10);
                skill.gameObject.AddComponent<Metin2MobileActionButton>().Configure(
                    Metin2MobileActionButton.MobileAction.QuickSlot, index, frame, slotUv, slotUv);
            }

            RectTransform attack = CreateCenter(actions, "Attack Button", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(92f, 92f));
            Rect attackNormal = AtlasUv(taskbar, new Rect(232f, 87f, 32f, 32f));
            Rect attackPressed = AtlasUv(taskbar, new Rect(296f, 87f, 32f, 32f));
            RawImage attackImage = AddRaw(attack, taskbar, attackNormal, true);
            AddCenteredText(attack, "Attack Label", "SALDIRI", new Vector2(0f, -29f), 10);
            attack.gameObject.AddComponent<Metin2MobileActionButton>().Configure(
                Metin2MobileActionButton.MobileAction.Attack, 0, attackImage, attackNormal, attackPressed);

            Metin2MobileGameplayUI controller = root.AddComponent<Metin2MobileGameplayUI>();
            controller.Configure(canvas, safeArea, hpFill, spFill, staminaFill, experiencePoints, hpText, spText, skillIcons, inputDriver);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Metin2MobileUI] Mobil HUD prefab hazirlandi: " + PrefabPath);
        }

        static void EnsureTextures()
        {
            if (LoadTexture("taskbar.tga") != null && LoadTexture("hp_gauge_01.tga") != null) return;
            Metin2GameplayUIAssetBuilder.Build();
        }

        static Texture2D LoadTexture(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + fileName);
        }

        static bool CanAuthorScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path) && scene.name != "Metin2_Intro";
        }

        static Metin2MobileGameplayUI FindInScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Metin2MobileGameplayUI found = root.GetComponentInChildren<Metin2MobileGameplayUI>(true);
                if (found != null) return found;
            }
            return null;
        }

        static RectTransform CreateStretch(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform CreateTopLeft(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static RectTransform CreateCenter(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RawImage AddRaw(RectTransform rect, Texture2D texture, Rect uv, bool raycast)
        {
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = raycast;
            return image;
        }

        static Text AddCenteredText(RectTransform parent, string name, string value, Vector2 position, int fontSize)
        {
            RectTransform rect = CreateCenter(parent, name, new Vector2(0.5f, 0.5f), position, parent.sizeDelta);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.88f, 0.62f, 1f);
            text.text = value;
            text.raycastTarget = false;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static RawImage CreateRaw(Transform parent, string name, Texture2D texture,
            float x, float y, float width, float height, Rect uv)
        {
            RectTransform rect = CreateTopLeft(parent, name, x, y, width, height);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(Transform parent, string name, float x, float y, float width, float height, string value)
        {
            RectTransform rect = CreateTopLeft(parent, name, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 7;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return text;
        }

        static Rect AtlasUv(Texture2D texture, Rect topLeftPixels)
        {
            return new Rect(
                topLeftPixels.x / texture.width,
                1f - (topLeftPixels.y + topLeftPixels.height) / texture.height,
                topLeftPixels.width / texture.width,
                topLeftPixels.height / texture.height);
        }
    }
}
#endif


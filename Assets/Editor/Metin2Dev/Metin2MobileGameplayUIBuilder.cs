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
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab(false);
            AddToOpenScene();
        }

        static void BuildPrefab(bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            EnsureTextures();
            Directory.CreateDirectory(PrefabFolder);

            Texture2D taskbar = LoadTexture("taskbar.tga");
            Texture2D hpGauge = LoadTexture("hp_gauge_01.tga");
            Texture2D spGauge = LoadTexture("sp_gauge_01.tga");
            Texture2D staminaGauge = LoadTexture("st_gauge_01.tga");
            if (taskbar == null || hpGauge == null || spGauge == null || staminaGauge == null)
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
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

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

            Metin2MobileGameplayUI controller = root.AddComponent<Metin2MobileGameplayUI>();
            controller.Configure(canvas, safeArea, hpFill, spFill, staminaFill, experiencePoints, hpText, spText);

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


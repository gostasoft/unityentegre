#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Metin2Dev.Editor
{
    [InitializeOnLoad]
    public static class MobileHUDStatusMinimapAuthoring
    {
        private const string PrefabPath = "Assets/Map/Assets/Resources/MobileHUD.prefab";
        private const string UiTextureFolder = "Assets/Metin2/UI/Resources/Metin2UI/";

        static MobileHUDStatusMinimapAuthoring()
        {
            EditorApplication.delayCall += EnsureAuthoredPrefab;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsureAuthoredPrefab;
        }

        [MenuItem("Tools/Metin2/UI/Update Authored Mobile HP and Minimap")]
        public static void EnsureAuthoredPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool changed = false;
            try
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(candidate.gameObject) > 0)
                        changed = true;

                Texture2D taskbar = LoadTexture("taskbar.tga");
                Texture2D hpGauge = LoadTexture("hp_gauge_01.tga");
                Texture2D spGauge = LoadTexture("sp_gauge_01.tga");
                Texture2D staminaGauge = LoadTexture("st_gauge_01.tga");
                Texture2D minimapFrame = LoadTexture("minimap.dds");
                if (taskbar == null || hpGauge == null || spGauge == null || staminaGauge == null || minimapFrame == null)
                {
                    Debug.LogWarning("[MobileHUD] HP/minimap icin orijinal Metin2 UI dokulari bekleniyor.");
                    return;
                }

                RectTransform status = FindDeep(root.transform, "TopStatus") as RectTransform;
                if (status == null)
                {
                    status = CreateTopLeft(root.transform, "TopStatus", 22f, 22f, 198f, 112f);
                    BuildStatus(status, taskbar, hpGauge, spGauge, staminaGauge);
                    changed = true;
                }

                RectTransform minimap = FindDeep(root.transform, "MobileMinimap") as RectTransform;
                if (minimap == null)
                {
                    minimap = CreateTopRight(root.transform, "MobileMinimap", 22f, 22f, 148f, 166f);
                    changed = true;
                }
                changed |= EnsureMinimapContents(minimap, minimapFrame);

                MobileHUDStatusAndMinimap controller = root.GetComponent<MobileHUDStatusAndMinimap>();
                if (controller == null)
                {
                    controller = root.AddComponent<MobileHUDStatusAndMinimap>();
                    changed = true;
                }
                controller.Configure(status,
                    FindDeep(status, "HPFill")?.GetComponent<RawImage>(),
                    FindDeep(status, "SPFill")?.GetComponent<RawImage>(),
                    FindDeep(status, "StaminaFill")?.GetComponent<RawImage>(),
                    FindDeep(status, "HPText")?.GetComponent<Text>(),
                    FindDeep(status, "SPText")?.GetComponent<Text>(),
                    minimap,
                    FindDeep(minimap, "MapView")?.GetComponent<RawImage>(),
                    FindDeep(minimap, "PlayerMarker") as RectTransform,
                    FindDeep(minimap, "MapName")?.GetComponent<Text>());

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[MobileHUD] Duzenlenebilir ust HP paneli ve minimap prefab'a eklendi.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildStatus(RectTransform root, Texture2D taskbar, Texture2D hp,
            Texture2D sp, Texture2D stamina)
        {
            RectTransform gauge = CreateTopLeft(root, "HP_SP_Stamina", 0f, 0f, 158f, 47f);
            CreateRaw(gauge, "HPFill", hp, 59f, 14f, 95f, 11f, FullUv());
            CreateRaw(gauge, "SPFill", sp, 59f, 24f, 95f, 11f, FullUv());
            CreateRaw(gauge, "StaminaFill", stamina, 59f, 38f, 95f, 6f, FullUv());
            CreateRaw(gauge, "GaugeFrame", taskbar, 0f, 0f, 158f, 47f,
                AtlasUv(taskbar, new Rect(0f, 0f, 158f, 47f)));
            CreateText(gauge, "HPText", 59f, 1f, 95f, 11f, "590 / 590", 8);
            CreateText(gauge, "SPText", 59f, 25f, 95f, 10f, "187 / 187", 8);

            RectTransform exp = CreateTopLeft(root, "FourPartEXP", 0f, 51f, 105f, 37f);
            CreateRaw(exp, "EXPFrame", taskbar, 0f, 0f, 105f, 37f,
                AtlasUv(taskbar, new Rect(158f, 0f, 105f, 37f)));
            Rect pointUv = AtlasUv(taskbar, new Rect(487f, 0f, 19f, 19f));
            for (int index = 0; index < 4; index++)
                CreateRaw(exp, "EXPTube" + (index + 1), taskbar, 5f + index * 25f, 9f, 19f, 19f, pointUv);
        }

        private static bool EnsureMinimapContents(RectTransform root, Texture2D frameTexture)
        {
            bool changed = false;
            RectTransform background = FindDeep(root, "MapBackground") as RectTransform;
            if (background == null)
            {
                background = CreateTopLeft(root, "MapBackground", 6f, 6f, 124f, 124f);
                changed = true;
            }
            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = background.gameObject.AddComponent<Image>();
                changed = true;
            }
            backgroundImage.color = new Color(0.015f, 0.02f, 0.015f, 1f);
            backgroundImage.raycastTarget = false;

            RectTransform view = FindDeep(root, "MapView") as RectTransform;
            if (view == null)
            {
                view = CreateTopLeft(root, "MapView", 10f, 10f, 116f, 116f);
                changed = true;
            }
            RawImage viewImage = view.GetComponent<RawImage>();
            if (viewImage == null)
            {
                viewImage = view.gameObject.AddComponent<RawImage>();
                changed = true;
            }
            viewImage.color = Color.white;
            viewImage.raycastTarget = false;

            RectTransform marker = FindDeep(root, "PlayerMarker") as RectTransform;
            if (marker == null)
            {
                marker = CreateTopLeft(root, "PlayerMarker", 62f, 61f, 12f, 12f);
                marker.pivot = new Vector2(0.5f, 0.5f);
                marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
                changed = true;
            }
            Image markerImage = marker.GetComponent<Image>();
            if (markerImage == null)
            {
                markerImage = marker.gameObject.AddComponent<Image>();
                changed = true;
            }
            markerImage.color = new Color(1f, 0.82f, 0.16f, 1f);
            markerImage.raycastTarget = false;

            RectTransform frame = FindDeep(root, "OriginalMinimapFrame") as RectTransform;
            if (frame == null)
            {
                frame = CreateRaw(root, "OriginalMinimapFrame", frameTexture, 0f, 0f, 136f, 137f,
                    AtlasUv(frameTexture, new Rect(0f, 0f, 136f, 137f))).rectTransform;
                changed = true;
            }

            RectTransform mapName = FindDeep(root, "MapName") as RectTransform;
            if (mapName == null)
            {
                mapName = CreateText(root, "MapName", 0f, 139f, 136f, 18f, "HARITA", 10).rectTransform;
                changed = true;
            }

            changed |= SetSiblingIndex(background, 0);
            changed |= SetSiblingIndex(view, 1);
            changed |= SetSiblingIndex(marker, 2);
            changed |= SetSiblingIndex(frame, 3);
            changed |= SetSiblingIndex(mapName, root.childCount - 1);
            return changed;
        }

        private static bool SetSiblingIndex(Transform child, int index)
        {
            if (child == null || child.GetSiblingIndex() == index)
                return false;
            child.SetSiblingIndex(index);
            return true;
        }

        private static Texture2D LoadTexture(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(UiTextureFolder + name);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static RectTransform CreateTopLeft(Transform parent, string name, float x, float y,
            float width, float height)
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

        private static RectTransform CreateTopRight(Transform parent, string name, float x, float y,
            float width, float height)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RawImage CreateRaw(Transform parent, string name, Texture texture, float x, float y,
            float width, float height, Rect uv)
        {
            RectTransform rect = CreateTopLeft(parent, name, x, y, width, height);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, float x, float y, float width,
            float height, string value, int size)
        {
            RectTransform rect = CreateTopLeft(parent, name, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return text;
        }

        private static Rect FullUv() => new Rect(0f, 0f, 1f, 1f);

        private static Rect AtlasUv(Texture texture, Rect topLeftPixels)
        {
            return new Rect(topLeftPixels.x / texture.width,
                1f - (topLeftPixels.y + topLeftPixels.height) / texture.height,
                topLeftPixels.width / texture.width,
                topLeftPixels.height / texture.height);
        }
    }
}
#endif

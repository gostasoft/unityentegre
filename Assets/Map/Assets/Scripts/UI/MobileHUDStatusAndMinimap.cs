using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MobileHUDStatusAndMinimap : MonoBehaviour
{
    [Header("Editable hierarchy references")]
    [SerializeField] private RectTransform topStatus;
    [SerializeField] private RawImage hpFill;
    [SerializeField] private RawImage spFill;
    [SerializeField] private RawImage staminaFill;
    [SerializeField] private Text hpText;
    [SerializeField] private Text spText;
    [SerializeField] private RectTransform minimapRoot;
    [SerializeField] private RawImage minimapView;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private Text mapNameText;

    [Header("Minimap")]
    [SerializeField, Range(20f, 150f)] private float visibleWorldRadius = 58f;
    [SerializeField, Range(64, 512)] private int textureResolution = 256;
    [SerializeField] private float cameraHeight = 180f;

    private const float HpFullWidth = 95f;
    private const float SpFullWidth = 95f;
    private const float StaminaFullWidth = 95f;
    private RenderTexture minimapTexture;
    private Camera minimapCamera;
    private Transform player;
    private float nextPlayerSearchAt;

    public void EnsureHierarchy()
    {
        if (topStatus == null)
            topStatus = FindDeep(transform, "TopStatus") as RectTransform;
        if (minimapRoot == null)
            minimapRoot = FindDeep(transform, "MobileMinimap") as RectTransform;

        Texture2D taskbar = Resources.Load<Texture2D>("Metin2UI/taskbar");
        Texture2D hpGauge = Resources.Load<Texture2D>("Metin2UI/hp_gauge_01");
        Texture2D spGauge = Resources.Load<Texture2D>("Metin2UI/sp_gauge_01");
        Texture2D staminaGauge = Resources.Load<Texture2D>("Metin2UI/st_gauge_01");
        Texture2D minimapFrame = Resources.Load<Texture2D>("Metin2UI/minimap");

        if (topStatus == null && taskbar != null && hpGauge != null && spGauge != null && staminaGauge != null)
        {
            topStatus = CreateTopLeft(transform, "TopStatus", 22f, 22f, 198f, 112f);
            RectTransform gauge = CreateTopLeft(topStatus, "HP_SP_Stamina", 0f, 0f, 158f, 47f);
            hpFill = CreateRaw(gauge, "HPFill", hpGauge, 59f, 14f, 95f, 11f, FullUv());
            spFill = CreateRaw(gauge, "SPFill", spGauge, 59f, 24f, 95f, 11f, FullUv());
            staminaFill = CreateRaw(gauge, "StaminaFill", staminaGauge, 59f, 38f, 95f, 6f, FullUv());
            CreateRaw(gauge, "GaugeFrame", taskbar, 0f, 0f, 158f, 47f,
                AtlasUv(taskbar, new Rect(0f, 0f, 158f, 47f)));
            hpText = CreateText(gauge, "HPText", 59f, 1f, 95f, 11f, "590 / 590", 8);
            spText = CreateText(gauge, "SPText", 59f, 25f, 95f, 10f, "187 / 187", 8);

            RectTransform exp = CreateTopLeft(topStatus, "FourPartEXP", 0f, 51f, 105f, 37f);
            CreateRaw(exp, "EXPFrame", taskbar, 0f, 0f, 105f, 37f,
                AtlasUv(taskbar, new Rect(158f, 0f, 105f, 37f)));
            Rect pointUv = AtlasUv(taskbar, new Rect(487f, 0f, 19f, 19f));
            for (int index = 0; index < 4; index++)
                CreateRaw(exp, "EXPTube" + (index + 1), taskbar, 5f + index * 25f, 9f, 19f, 19f, pointUv);
        }

        if (minimapRoot == null && minimapFrame != null)
        {
            minimapRoot = CreateTopRight(transform, "MobileMinimap", 22f, 22f, 148f, 166f);
            RectTransform background = CreateTopLeft(minimapRoot, "MapBackground", 6f, 6f, 124f, 124f);
            Image backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.015f, 0.02f, 0.015f, 1f);
            backgroundImage.raycastTarget = false;

            RectTransform view = CreateTopLeft(minimapRoot, "MapView", 10f, 10f, 116f, 116f);
            minimapView = view.gameObject.AddComponent<RawImage>();
            minimapView.raycastTarget = false;

            playerMarker = CreateTopLeft(minimapRoot, "PlayerMarker", 62f, 61f, 12f, 12f);
            playerMarker.pivot = new Vector2(0.5f, 0.5f);
            Image markerImage = playerMarker.gameObject.AddComponent<Image>();
            markerImage.color = new Color(1f, 0.82f, 0.16f, 1f);
            markerImage.raycastTarget = false;
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);

            CreateRaw(minimapRoot, "OriginalMinimapFrame", minimapFrame, 0f, 0f, 136f, 137f,
                AtlasUv(minimapFrame, new Rect(0f, 0f, 136f, 137f)));
            mapNameText = CreateText(minimapRoot, "MapName", 0f, 139f, 136f, 18f, "HARITA", 10);
        }

        if (topStatus != null)
        {
            hpFill = Resolve(hpFill, topStatus, "HPFill");
            spFill = Resolve(spFill, topStatus, "SPFill");
            staminaFill = Resolve(staminaFill, topStatus, "StaminaFill");
            hpText = Resolve(hpText, topStatus, "HPText");
            spText = Resolve(spText, topStatus, "SPText");
        }
        if (minimapRoot != null)
        {
            minimapView = Resolve(minimapView, minimapRoot, "MapView");
            if (playerMarker == null) playerMarker = FindDeep(minimapRoot, "PlayerMarker") as RectTransform;
            mapNameText = Resolve(mapNameText, minimapRoot, "MapName");
        }
        UpdateMeters();
        UpdateMapName();
    }

    public void Configure(RectTransform status, RawImage hp, RawImage sp, RawImage stamina,
        Text hpValue, Text spValue, RectTransform mapRoot, RawImage mapView,
        RectTransform marker, Text mapName)
    {
        topStatus = status;
        hpFill = hp;
        spFill = sp;
        staminaFill = stamina;
        hpText = hpValue;
        spText = spValue;
        minimapRoot = mapRoot;
        minimapView = mapView;
        playerMarker = marker;
        mapNameText = mapName;
        UpdateMeters();
        UpdateMapName();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureHierarchy();
        UpdateMeters();
        UpdateMapName();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ReleaseMinimapCamera();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        nextPlayerSearchAt = 0f;
        UpdateMapName();
    }

    private void Update()
    {
        UpdateMeters();
        if (Application.isPlaying)
            UpdateMinimap();
    }

    private void UpdateMeters()
    {
        int level = Mathf.Max(1, Metin2Dev.Gameplay.Metin2GameplaySession.Level);
        int vitality = Mathf.Max(1, Metin2Dev.Gameplay.Metin2GameplaySession.Vitality);
        int intelligence = Mathf.Max(1, Metin2Dev.Gameplay.Metin2GameplaySession.Intelligence);
        float maxHp = 500f + vitality * 40f + level * 50f;
        float maxSp = 150f + intelligence * 25f + level * 12f;
        SetHorizontalFill(hpFill, HpFullWidth, 1f);
        SetHorizontalFill(spFill, SpFullWidth, 1f);
        SetHorizontalFill(staminaFill, StaminaFullWidth, 1f);
        if (hpText != null) hpText.text = Mathf.CeilToInt(maxHp) + " / " + Mathf.CeilToInt(maxHp);
        if (spText != null) spText.text = Mathf.CeilToInt(maxSp) + " / " + Mathf.CeilToInt(maxSp);
    }

    private static void SetHorizontalFill(RawImage image, float fullWidth, float ratio)
    {
        if (image == null) return;
        RectTransform rect = image.rectTransform;
        Vector2 size = rect.sizeDelta;
        size.x = fullWidth * Mathf.Clamp01(ratio);
        rect.sizeDelta = size;
    }

    private void UpdateMapName()
    {
        if (mapNameText == null) return;
        string sceneName = SceneManager.GetActiveScene().name;
        mapNameText.text = string.IsNullOrWhiteSpace(sceneName) ? "HARITA" : sceneName.Replace('_', ' ');
    }

    private void UpdateMinimap()
    {
        if (minimapRoot == null || minimapView == null || !minimapRoot.gameObject.activeInHierarchy)
            return;
        if (player == null && Time.unscaledTime >= nextPlayerSearchAt)
        {
            nextPlayerSearchAt = Time.unscaledTime + 0.5f;
            player = FindPlayer();
        }
        if (player == null) return;
        EnsureMinimapCamera();
        if (minimapCamera == null) return;

        Vector3 position = player.position;
        minimapCamera.transform.SetPositionAndRotation(
            new Vector3(position.x, position.y + cameraHeight, position.z),
            Quaternion.Euler(90f, 0f, 0f));
        minimapCamera.orthographicSize = visibleWorldRadius;
        if (playerMarker != null)
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
    }

    private void EnsureMinimapCamera()
    {
        if (minimapCamera != null) return;
        int resolution = Mathf.Clamp(textureResolution, 64, 512);
        minimapTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "Mobile Minimap Render Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        minimapTexture.Create();

        GameObject cameraObject = new GameObject("Mobile Minimap Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        minimapCamera = cameraObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = visibleWorldRadius;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.025f, 0.035f, 0.025f, 1f);
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = Mathf.Max(400f, cameraHeight + 200f);
        minimapCamera.depth = -100f;
        minimapCamera.allowHDR = false;
        minimapCamera.allowMSAA = false;
        minimapCamera.cullingMask = ~(1 << 8);
        minimapCamera.targetTexture = minimapTexture;
        minimapView.texture = minimapTexture;
    }

    private static Transform FindPlayer()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null) continue;
            string typeName = behaviour.GetType().FullName ?? behaviour.GetType().Name;
            if (typeName == "Metin2Dev.Gameplay.Metin2PlayerController" ||
                behaviour.GetType().Name == "PlayerMovement")
                return behaviour.transform;
        }
        return null;
    }

    private void ReleaseMinimapCamera()
    {
        if (minimapView != null && minimapView.texture == minimapTexture)
            minimapView.texture = null;
        if (minimapCamera != null)
            DestroyRuntimeObject(minimapCamera.gameObject);
        if (minimapTexture != null)
        {
            minimapTexture.Release();
            DestroyRuntimeObject(minimapTexture);
        }
        minimapCamera = null;
        minimapTexture = null;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private static T Resolve<T>(T current, Transform root, string name) where T : Component
    {
        if (current != null) return current;
        Transform found = FindDeep(root, name);
        return found != null ? found.GetComponent<T>() : null;
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
        float height, string value, int fontSize)
    {
        RectTransform rect = CreateTopLeft(parent, name, x, y, width, height);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
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

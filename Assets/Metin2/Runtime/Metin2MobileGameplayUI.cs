using UnityEngine;
using UnityEngine.UI;

namespace Metin2Dev.Gameplay
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Metin2MobileGameplayUI : MonoBehaviour
    {
        static Metin2MobileGameplayUI instance;

        [Header("Mobile preview")]
        [SerializeField] bool previewMobileLayoutInEditor = true;
        [SerializeField] bool gameplayVisible = true;

        [Header("Editable hierarchy references")]
        [SerializeField] Canvas mobileCanvas;
        [SerializeField] RectTransform safeAreaRoot;
        [SerializeField] RawImage hpFill;
        [SerializeField] RawImage spFill;
        [SerializeField] RawImage staminaFill;
        [SerializeField] RawImage[] experiencePoints = new RawImage[4];
        [SerializeField] Text hpText;
        [SerializeField] Text spText;

        [Header("Editor preview values")]
        [SerializeField, Min(1f)] float previewMaxHp = 590f;
        [SerializeField, Min(0f)] float previewHp = 590f;
        [SerializeField, Min(1f)] float previewMaxSp = 187f;
        [SerializeField, Min(0f)] float previewSp = 187f;
        [SerializeField, Range(0f, 1f)] float previewStamina = 1f;
        [SerializeField, Range(0f, 1f)] float previewExperience = 0.25f;

        float hpFullWidth = 95f;
        float spFullWidth = 95f;
        float staminaFullWidth = 95f;
        readonly float[] experienceFullWidths = new float[4];
        Rect lastSafeArea;
        Vector2Int lastScreenSize;

        public bool ShouldUseMobileLayout
        {
            get
            {
#if UNITY_EDITOR
                // Editor Play is the desktop control path. The preview remains available while authoring.
                return !Application.isPlaying && previewMobileLayoutInEditor;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        public static bool IsMobileLayoutActive => global::MobileHUDOnly.IsAnyActive ||
                                                   instance != null && instance.ShouldUseMobileLayout;

        public static void SetGlobalGameplayVisible(bool visible)
        {
            if (instance != null) instance.SetGameplayVisible(visible);
        }

        void Awake()
        {
            RegisterInstance();
            CacheFullWidths();
            ApplySafeArea(true);
            ApplyVisibility();
            UpdateMeters();
        }

        void OnEnable()
        {
            RegisterInstance();
            CacheFullWidths();
            ApplySafeArea(true);
            ApplyVisibility();
            UpdateMeters();
        }

        void OnValidate()
        {
            previewHp = Mathf.Clamp(previewHp, 0f, previewMaxHp);
            previewSp = Mathf.Clamp(previewSp, 0f, previewMaxSp);
            CacheFullWidths();
            ApplySafeArea(true);
            ApplyVisibility();
            UpdateMeters();
        }

        void Update()
        {
            ApplySafeArea(false);
            ApplyVisibility();
            UpdateMeters();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void RegisterInstance()
        {
            if (instance == null || instance == this) instance = this;
        }

        public static Metin2MobileGameplayUI EnsureRuntimeInstance()
        {
            // The project already contains an authored Canvas/MobileHUD. Do not cover it with a generated replacement.
            if (global::MobileHUDOnly.IsAnyActive) return null;
            if (instance != null) return instance;
            instance = FindFirstObjectByType<Metin2MobileGameplayUI>(FindObjectsInactive.Include);
            if (instance != null) return instance;

            Metin2MobileGameplayUI prefab = Resources.Load<Metin2MobileGameplayUI>("Metin2MobileGameplayUI");
            if (prefab == null) return null;
            instance = Instantiate(prefab);
            instance.name = "Metin2 Mobile Gameplay UI";
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        public void SetGameplayVisible(bool visible)
        {
            gameplayVisible = visible;
            ApplyVisibility();
        }

        public void Configure(
            Canvas canvas,
            RectTransform safeArea,
            RawImage hp,
            RawImage sp,
            RawImage stamina,
            RawImage[] expPoints,
            Text hpValue,
            Text spValue)
        {
            mobileCanvas = canvas;
            safeAreaRoot = safeArea;
            hpFill = hp;
            spFill = sp;
            staminaFill = stamina;
            experiencePoints = expPoints;
            hpText = hpValue;
            spText = spValue;
            CacheFullWidths();
            ApplySafeArea(true);
            UpdateMeters();
        }

        void ApplyVisibility()
        {
            if (mobileCanvas == null) return;
            bool visible = gameplayVisible && ShouldUseMobileLayout;
            if (mobileCanvas.enabled != visible) mobileCanvas.enabled = visible;
        }

        void CacheFullWidths()
        {
            hpFullWidth = CacheWidth(hpFill, hpFullWidth);
            spFullWidth = CacheWidth(spFill, spFullWidth);
            staminaFullWidth = CacheWidth(staminaFill, staminaFullWidth);
            if (experiencePoints == null) return;
            for (int index = 0; index < experienceFullWidths.Length && index < experiencePoints.Length; index++)
                experienceFullWidths[index] = CacheWidth(experiencePoints[index], experienceFullWidths[index] > 0f ? experienceFullWidths[index] : 19f);
        }

        static float CacheWidth(RawImage image, float fallback)
        {
            if (image == null) return fallback;
            float width = image.rectTransform.sizeDelta.x;
            return width > 0.01f ? width : fallback;
        }

        void ApplySafeArea(bool force)
        {
            if (safeAreaRoot == null) return;
            Rect safe = Screen.safeArea;
            Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == lastSafeArea && screen == lastScreenSize) return;
            lastSafeArea = safe;
            lastScreenSize = screen;

            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            safeAreaRoot.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            safeAreaRoot.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
        }

        void UpdateMeters()
        {
            float hp;
            float maxHp;
            float sp;
            float maxSp;
            float stamina;
            float experience;

            if (Application.isPlaying)
            {
                Metin2PlayerState state = Metin2PlayerState.Local;
                if (state != null)
                {
                    maxHp = state.MaxHp;
                    maxSp = state.MaxSp;
                    hp = state.CurrentHp;
                    sp = state.CurrentSp;
                    stamina = state.MaxStamina > 0 ? state.CurrentStamina / (float)state.MaxStamina : 0f;
                    experience = state.NextExperience > 0 ? state.Experience / (float)state.NextExperience : 0f;
                }
                else
                {
                    int level = Mathf.Max(1, Metin2GameplaySession.Level);
                    int vitality = Mathf.Max(1, Metin2GameplaySession.Vitality);
                    int intelligence = Mathf.Max(1, Metin2GameplaySession.Intelligence);
                    maxHp = 500f + vitality * 40f + level * 50f;
                    maxSp = 150f + intelligence * 25f + level * 12f;
                    hp = maxHp;
                    sp = maxSp;
                    stamina = 1f;
                    experience = 0f;
                }
            }
            else
            {
                maxHp = Mathf.Max(1f, previewMaxHp);
                maxSp = Mathf.Max(1f, previewMaxSp);
                hp = Mathf.Clamp(previewHp, 0f, maxHp);
                sp = Mathf.Clamp(previewSp, 0f, maxSp);
                stamina = previewStamina;
                experience = previewExperience;
            }

            SetHorizontalFill(hpFill, hpFullWidth, hp / maxHp);
            SetHorizontalFill(spFill, spFullWidth, sp / maxSp);
            SetHorizontalFill(staminaFill, staminaFullWidth, stamina);
            if (hpText != null) hpText.text = Mathf.CeilToInt(hp) + " / " + Mathf.CeilToInt(maxHp);
            if (spText != null) spText.text = Mathf.CeilToInt(sp) + " / " + Mathf.CeilToInt(maxSp);

            if (experiencePoints == null) return;
            for (int index = 0; index < experiencePoints.Length && index < 4; index++)
                SetHorizontalFill(experiencePoints[index], experienceFullWidths[index], Mathf.Clamp01(experience * 4f - index));
        }

        static void SetHorizontalFill(RawImage image, float fullWidth, float value)
        {
            if (image == null) return;
            value = Mathf.Clamp01(value);
            RectTransform rect = image.rectTransform;
            Vector2 size = rect.sizeDelta;
            size.x = fullWidth * value;
            rect.sizeDelta = size;
        }
    }
}


using Metin2Dev.Frontend;
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
        [SerializeField] RawImage[] skillIcons = new RawImage[8];
        [SerializeField] Metin2MobileInputDriver inputDriver;

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
        Metin2CharacterClass displayedSkillClass = (Metin2CharacterClass)(-1);

        static readonly Rect[] WarriorSkillIconRects =
        {
            new Rect(0, 96, 32, 32), new Rect(160, 64, 32, 32), new Rect(64, 64, 32, 32),
            new Rect(192, 0, 32, 32), new Rect(96, 96, 32, 32), new Rect(32, 32, 32, 32),
        };
        static readonly Rect[] AssassinSkillIconRects =
        {
            new Rect(0, 0, 32, 32), new Rect(128, 32, 32, 32), new Rect(96, 0, 32, 32),
            new Rect(192, 0, 32, 32), new Rect(0, 96, 32, 32), new Rect(32, 32, 32, 32),
        };
        static readonly Rect[] SuraSkillIconRects =
        {
            new Rect(128, 96, 32, 32), new Rect(64, 128, 32, 32), new Rect(224, 0, 32, 32),
            new Rect(128, 0, 32, 32), new Rect(0, 64, 32, 32), new Rect(32, 96, 32, 32),
        };
        static readonly Rect[] ShamanSkillIconRects =
        {
            new Rect(0, 0, 32, 32), new Rect(32, 128, 32, 32), new Rect(96, 96, 32, 32),
            new Rect(128, 32, 32, 32), new Rect(96, 0, 32, 32), new Rect(32, 32, 32, 32),
        };

        public bool ShouldUseMobileLayout
        {
            get
            {
#if UNITY_EDITOR
                return Application.isPlaying ? Application.isMobilePlatform || previewMobileLayoutInEditor : previewMobileLayoutInEditor;
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        public static bool IsMobileLayoutActive => instance != null && instance.ShouldUseMobileLayout;

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
            UpdateSkillIcons(true);
        }

        void OnEnable()
        {
            RegisterInstance();
            CacheFullWidths();
            ApplySafeArea(true);
            ApplyVisibility();
            UpdateMeters();
            UpdateSkillIcons(true);
        }

        void OnValidate()
        {
            previewHp = Mathf.Clamp(previewHp, 0f, previewMaxHp);
            previewSp = Mathf.Clamp(previewSp, 0f, previewMaxSp);
            CacheFullWidths();
            ApplySafeArea(true);
            ApplyVisibility();
            UpdateMeters();
            UpdateSkillIcons(true);
        }

        void Update()
        {
            ApplySafeArea(false);
            ApplyVisibility();
            UpdateMeters();
            UpdateSkillIcons(false);
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
            Text spValue,
            RawImage[] mobileSkillIcons,
            Metin2MobileInputDriver mobileInputDriver)
        {
            mobileCanvas = canvas;
            safeAreaRoot = safeArea;
            hpFill = hp;
            spFill = sp;
            staminaFill = stamina;
            experiencePoints = expPoints;
            hpText = hpValue;
            spText = spValue;
            skillIcons = mobileSkillIcons;
            displayedSkillClass = (Metin2CharacterClass)(-1);
            inputDriver = mobileInputDriver;
            CacheFullWidths();
            ApplySafeArea(true);
            UpdateMeters();
            UpdateSkillIcons(true);
        }

        void ApplyVisibility()
        {
            if (mobileCanvas == null) return;
            bool visible = gameplayVisible && ShouldUseMobileLayout;
            if (mobileCanvas.enabled != visible) mobileCanvas.enabled = visible;
            if (inputDriver != null && inputDriver.enabled != visible)
                inputDriver.enabled = visible;
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

        void UpdateSkillIcons(bool force)
        {
            Metin2CharacterClass characterClass = Application.isPlaying
                ? Metin2GameplaySession.CharacterClass
                : Metin2CharacterClass.Warrior;
            if (!force && displayedSkillClass == characterClass) return;
            displayedSkillClass = characterClass;
            Texture2D atlas = Resources.Load<Texture2D>("Metin2UI/" + SkillAtlasName(characterClass));
            Rect[] sourceRects = SkillRects(characterClass);
            if (atlas == null || skillIcons == null) return;
            for (int index = 0; index < skillIcons.Length && index < sourceRects.Length; index++)
            {
                if (skillIcons[index] == null) continue;
                skillIcons[index].texture = atlas;
                skillIcons[index].uvRect = AtlasUv(atlas, sourceRects[index]);
            }
        }

        static string SkillAtlasName(Metin2CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case Metin2CharacterClass.Assassin: return "skillassassin";
                case Metin2CharacterClass.Sura: return "skillsura";
                case Metin2CharacterClass.Shaman: return "skillshaman";
                default: return "skillwarrior";
            }
        }

        static Rect[] SkillRects(Metin2CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case Metin2CharacterClass.Assassin: return AssassinSkillIconRects;
                case Metin2CharacterClass.Sura: return SuraSkillIconRects;
                case Metin2CharacterClass.Shaman: return ShamanSkillIconRects;
                default: return WarriorSkillIconRects;
            }
        }

        static Rect AtlasUv(Texture2D texture, Rect topLeftPixels)
        {
            return new Rect(topLeftPixels.x / texture.width,
                1f - (topLeftPixels.y + topLeftPixels.height) / texture.height,
                topLeftPixels.width / texture.width, topLeftPixels.height / texture.height);
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


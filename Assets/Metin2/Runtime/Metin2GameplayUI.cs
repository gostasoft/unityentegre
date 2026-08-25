using System;
using System.Collections.Generic;
using Metin2Dev.Frontend;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Metin2Dev.Gameplay
{
    public sealed class Metin2GameplayUI : MonoBehaviour
    {
        public static int CurrentGold => instance != null ? instance.gold : 0;
        const float ReferenceWidth = 1024f;
        const float ReferenceHeight = 768f;
        static Metin2GameplayUI instance;

        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        readonly List<Image> experiencePoints = new List<Image>();
        readonly Image[] quickSlotHighlights = new Image[8];
        Canvas canvas;
        Font font;
        RectTransform hud;
        RectTransform inventoryWindow;
        RectTransform characterWindow;
        RectTransform[] characterPages;
        Image hpFill;
        Image spFill;
        Image staminaFill;
        RawImage taskbarBase;
        Texture2D eyeViewTexture;
        Text hpText;
        Text spText;
        Text characterNameText;
        Text characterLevelText;
        Metin2PlayerController player;
        bool gameplayVisible;
        float currentHp;
        float currentSp;
        float currentStamina;
        float maxHp;
        float maxSp;
        float maxStamina;
        int level;
        int experience;
        int nextExperience;
        int gold;

        static readonly Rect[] WarriorSkillIconRects =
        {
            new Rect(0, 96, 32, 32),       // samyeon
            new Rect(160, 64, 32, 32),     // palbang
            new Rect(64, 64, 32, 32),      // jeongwi
            new Rect(192, 0, 32, 32),      // geomgyeong
            new Rect(96, 96, 32, 32),      // tanhwan
            new Rect(32, 32, 32, 32),      // geompung (skilldesc id 20)
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            GameObject host = new GameObject("Metin2 Gameplay UI");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<Metin2GameplayUI>();
        }

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadTextures();
            CreateEventSystem();
            CreateCanvas();
            BuildHud();
            BuildInventory();
            BuildCharacterWindow();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (eyeViewTexture != null) Destroy(eyeViewTexture);
            if (instance == this) instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyScene(scene);
        }

        void ApplyScene(Scene scene)
        {
            gameplayVisible = scene.IsValid() && scene.isLoaded && scene.name != "Metin2_Intro";
            if (canvas != null) canvas.gameObject.SetActive(gameplayVisible);
            if (gameplayVisible) Metin2MobileGameplayUI.EnsureRuntimeInstance();
            Metin2MobileGameplayUI.SetGlobalGameplayVisible(gameplayVisible);
            ApplyHudLayoutMode();
            player = null;
            if (!gameplayVisible) return;
            InitializeStatus();
            inventoryWindow.gameObject.SetActive(false);
            characterWindow.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!gameplayVisible) return;
            ApplyHudLayoutMode();
            if (player == null) player = FindFirstObjectByType<Metin2PlayerController>();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.iKey.wasPressedThisFrame) ToggleInventory();
                if (keyboard.cKey.wasPressedThisFrame) ToggleCharacter();
                // Turkish Q layout: the Ü key occupies the physical US Right Bracket key.
                if (keyboard.rightBracketKey.wasPressedThisFrame) ToggleCameraView();
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    inventoryWindow.gameObject.SetActive(false);
                    characterWindow.gameObject.SetActive(false);
                }
                UpdateQuickSlotHighlights(keyboard);
            }
            UpdateStatusVisuals();
        }

        void ApplyHudLayoutMode()
        {
            if (hud == null) return;
            bool showDesktopTaskbar = gameplayVisible && !Metin2MobileGameplayUI.IsMobileLayoutActive;
            if (hud.gameObject.activeSelf != showDesktopTaskbar) hud.gameObject.SetActive(showDesktopTaskbar);
        }

        void LoadTextures()
        {
            string[] names =
            {
                "taskbar", "public", "windows", "skillwarrior", "skillassassin", "skillsura", "skillshaman",
                "taskbar_base", "board_base",
                "board_corner_lefttop", "board_corner_righttop", "board_corner_leftbottom", "board_corner_rightbottom",
                "board_line_top", "board_line_bottom", "board_line_left", "board_line_right",
                "titlebar_left", "titlebar_center", "titlebar_right", "gauge_red", "gauge_blue", "gauge_pink",
                "gauge_purple", "horizontalbar_left", "horizontalbar_center", "horizontalbar_right",
                "hp_gauge_01", "sp_gauge_01", "st_gauge_01"
            };
            foreach (string name in names)
            {
                Texture2D texture = Resources.Load<Texture2D>("Metin2UI/" + name);
                if (texture != null) textures[name] = texture;
            }
        }

        Texture2D Texture(string name)
        {
            textures.TryGetValue(name, out Texture2D texture);
            return texture;
        }

        void CreateEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject eventObject = new GameObject("Metin2 Gameplay EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(transform, false);
            InputSystemUIInputModule input = eventObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
        }

        void CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Metin2 Gameplay Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // The login/character frontend is persistent and uses sorting order 32000.
            // Gameplay UI must be above its now-transparent canvas so pointer events reach this canvas.
            canvas.sortingOrder = 40000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        void BuildHud()
        {
            hud = CreateRect(canvas.transform, "TaskBar", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 47f));

            // ExpandedImageBox keeps the source texture's first 256 px and adds the rendering rect.
            // taskbar.py therefore spans x=263 all the way to SCREEN_WIDTH, not SCREEN_WIDTH-256.
            taskbarBase = CreateRaw(hud, "TaskBar Base", Texture("taskbar_base"), new Vector2(131.5f, 0f),
                new Vector2(-263f, 37f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            taskbarBase.color = Texture("taskbar_base") != null ? Color.white : new Color(0.07f, 0.06f, 0.045f, 0.96f);

            hpFill = CreateGaugeFill(hud, "HP", new Vector2(59f, 22f), new Vector2(95f, 11f), "hp_gauge_01");
            spFill = CreateGaugeFill(hud, "SP", new Vector2(59f, 12f), new Vector2(95f, 11f), "sp_gauge_01");
            staminaFill = CreateGaugeFill(hud, "ST", new Vector2(59f, 3f), new Vector2(95f, 6f), "st_gauge_01");
            CreateAtlasBottom(hud, "Gauge Board", Texture("taskbar"), new Rect(0, 0, 158, 47), Vector2.zero, new Vector2(158f, 47f));

            hpText = CreateText(hud, "HP", 9, TextAnchor.MiddleCenter, new Vector2(59f, 22f), new Vector2(95f, 11f), Color.white, true);
            spText = CreateText(hud, "SP", 9, TextAnchor.MiddleCenter, new Vector2(59f, 12f), new Vector2(95f, 11f), Color.white, true);

            CreateAtlasBottom(hud, "EXP Board", Texture("taskbar"), new Rect(158, 0, 105, 37), new Vector2(158f, 0f), new Vector2(105f, 37f));
            for (int index = 0; index < 4; index++)
            {
                Image point = CreateAtlasImageBottom(hud, "EXP " + (index + 1), Texture("taskbar"),
                    new Rect(487, 0, 19, 19), new Vector2(163f + index * 25f, 9f), new Vector2(19f, 19f));
                point.type = Image.Type.Filled;
                point.fillMethod = Image.FillMethod.Horizontal;
                point.fillOrigin = 0;
                experiencePoints.Add(point);
            }

            BuildQuickSlots();
            BuildTaskBarControls();
            BuildTaskButtons();
            BuildCameraViewButton();
        }

        void BuildQuickSlots()
        {
            string[] keyNames = { "1", "2", "3", "4", "F1", "F2", "F3", "F4" };
            Rect[] keyRects =
            {
                new Rect(506, 7, 5, 7), new Rect(174, 37, 5, 7), new Rect(179, 37, 5, 7), new Rect(184, 37, 5, 7),
                new Rect(496, 19, 8, 7), new Rect(504, 19, 8, 7), new Rect(158, 37, 8, 7), new Rect(166, 37, 8, 7)
            };

            for (int index = 0; index < 8; index++)
            {
                float x = index < 4 ? -86f + index * 32f : 56f + (index - 4) * 32f;
                RectTransform slot = CreateRect(hud, "Quick Slot " + keyNames[index], new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 0f), new Vector2(x, 2f), new Vector2(32f, 32f));
                RawImage slotBase = CreateAtlasStretch(slot, "Slot Base", Texture("public"), new Rect(0, 348, 32, 32));
                slotBase.raycastTarget = true;
                Image empty = CreateImage(slot, "Empty", Vector2.zero, new Vector2(32f, 32f),
                    new Color(0.02f, 0.02f, 0.02f, 0.72f));
                RawImage assignedIcon = CreateAtlasStretch(slot, "Assigned Icon", null, new Rect(0f, 0f, 1f, 1f));
                assignedIcon.raycastTarget = false;
                assignedIcon.enabled = false;

                Image highlight = CreateImage(slot, "Pressed", Vector2.zero, new Vector2(32f, 32f), new Color(1f, 0.78f, 0.20f, 0f));
                highlight.raycastTarget = false;
                quickSlotHighlights[index] = highlight;
                CreateAtlasTop(slot, "Key " + keyNames[index], Texture("taskbar"), keyRects[index], new Vector2(3f, -3f), keyRects[index].size);

                int captured = index;
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = highlight;
                button.onClick.AddListener(() => ActivateQuickSlot(captured));
                Metin2QuickSlotView view = slot.gameObject.AddComponent<Metin2QuickSlotView>();
                view.Configure(index, assignedIcon, empty);
            }
        }

        void BuildTaskButtons()
        {
            CreateTaskButton("Character", new Rect(263, 0, 32, 32), -144f, ToggleCharacter);
            CreateTaskButton("Inventory", new Rect(455, 0, 32, 32), -110f, ToggleInventory);
            CreateTaskButton("Community", new Rect(359, 0, 32, 32), -76f, () => { });
            CreateTaskButton("System", new Rect(320, 127, 32, 32), -42f, CloseAllWindows);
        }

        void BuildCameraViewButton()
        {
            RectTransform rect = CreateRect(hud, "Camera View Button", new Vector2(1f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(-178f, 2f), new Vector2(32f, 32f));
            eyeViewTexture = CreateEyeViewTexture();
            RawImage image = CreateRaw(rect, "Eye", eyeViewTexture, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(ToggleCameraView);
        }

        static Texture2D CreateEyeViewTexture()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Metin2 Eye View Button",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 panel = new Color32(25, 23, 18, 245);
            Color32 outer = new Color32(45, 31, 18, 255);
            Color32 border = new Color32(174, 135, 77, 255);
            Color32 eye = new Color32(238, 221, 174, 255);
            Color32 iris = new Color32(105, 157, 169, 255);
            Color32 pupil = new Color32(15, 18, 17, 255);
            Color32[] pixels = new Color32[size * size];
            for (int index = 0; index < pixels.Length; index++) pixels[index] = transparent;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Abs(x - 15);
                int dy = Mathf.Abs(y - 15);
                bool inside = dx <= 14 && dy <= 14 && dx + dy <= 24;
                if (!inside) continue;
                bool edge = dx >= 13 || dy >= 13 || dx + dy >= 22;
                pixels[y * size + x] = edge ? (dx + dy >= 23 ? outer : border) : panel;
            }

            for (int x = 6; x <= 25; x++)
            {
                float phase = (x - 6f) / 19f;
                int span = Mathf.RoundToInt(Mathf.Sin(phase * Mathf.PI) * 6f);
                SetPixel(pixels, size, x, 15 + span, eye);
                SetPixel(pixels, size, x, 15 + span - 1, eye);
                SetPixel(pixels, size, x, 15 - span, eye);
                SetPixel(pixels, size, x, 15 - span + 1, eye);
            }

            for (int y = 9; y <= 21; y++)
            for (int x = 10; x <= 22; x++)
            {
                int dx = x - 16;
                int dy = y - 15;
                int distance = dx * dx + dy * dy;
                if (distance <= 30) SetPixel(pixels, size, x, y, iris);
                if (distance <= 9) SetPixel(pixels, size, x, y, pupil);
            }
            SetPixel(pixels, size, 18, 18, eye);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static void SetPixel(Color32[] pixels, int size, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            pixels[y * size + x] = color;
        }

        void BuildTaskBarControls()
        {
            CreateHudAtlasButton("Left Mouse Mode", new Rect(32, 127, 32, 32), -128f, 2f, () => { });
            CreateHudAtlasButton("Right Mouse Mode", new Rect(32, 127, 32, 32), 205f, 2f, () => { });
            CreateHudAtlasButton("Chat", new Rect(0, 159, 14, 35), 42f, 1f, () => { });

            CreateAtlasAtHudCenter("Quick Page Board", new Rect(487, 19, 9, 8), 185f, 14f, new Vector2(9f, 8f));
            CreateHudAtlasButton("Quick Page Up", new Rect(272, 32, 9, 5), 185f, 23f, () => { });
            CreateAtlasAtHudCenter("Quick Page Number", new Rect(506, 7, 5, 7), 187f, 15f, new Vector2(5f, 7f));
            CreateHudAtlasButton("Quick Page Down", new Rect(487, 27, 9, 5), 185f, 8f, () => { });
        }

        Button CreateHudAtlasButton(string name, Rect source, float x, float bottom, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(hud, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(x, bottom), source.size);
            RawImage image = CreateAtlasStretch(rect, name + " Image", Texture("taskbar"), source);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            return button;
        }

        void CreateAtlasAtHudCenter(string name, Rect source, float x, float bottom, Vector2 size)
        {
            RectTransform rect = CreateRect(hud, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(x, bottom), size);
            CreateAtlasStretch(rect, name + " Image", Texture("taskbar"), source);
        }

        void CreateTaskButton(string name, Rect source, float right, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(hud, name + " Button", new Vector2(1f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(right, 2f), new Vector2(32f, 32f));
            RawImage raw = CreateAtlasStretch(rect, name, Texture("taskbar"), source);
            raw.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = raw;
            button.onClick.AddListener(action);
        }

        void BuildInventory()
        {
            inventoryWindow = CreateRect(canvas.transform, "InventoryWindow", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-200f, 0f), new Vector2(176f, 565f));
            BuildBoard(inventoryWindow);
            CreateTitle(inventoryWindow, "ENVANTER", 8f, 7f, 161f, inventoryWindow);
            CreateAtlasTop(inventoryWindow, "Equipment Base", Texture("windows"), new Rect(0, 152, 156, 188),
                new Vector2(10f, -33f), new Vector2(156f, 188f));

            CreateAtlasButton(inventoryWindow, "Equipment I", Texture("windows"), new Rect(324, 227, 32, 19),
                new Vector2(96f, -194f), new Vector2(32f, 19f), () => { });
            CreateAtlasButton(inventoryWindow, "Equipment II", Texture("windows"), new Rect(324, 227, 32, 19),
                new Vector2(128f, -194f), new Vector2(32f, 19f), () => { });
            CreateText(inventoryWindow, "I", 10, TextAnchor.MiddleCenter, new Vector2(96f, -194f), new Vector2(32f, 19f), Color.white);
            CreateText(inventoryWindow, "II", 10, TextAnchor.MiddleCenter, new Vector2(128f, -194f), new Vector2(32f, 19f), Color.white);

            CreateAtlasButton(inventoryWindow, "Inventory I", Texture("windows"), new Rect(227, 116, 78, 19),
                new Vector2(10f, -224f), new Vector2(78f, 19f), () => { });
            CreateAtlasButton(inventoryWindow, "Inventory II", Texture("windows"), new Rect(227, 116, 78, 19),
                new Vector2(88f, -224f), new Vector2(78f, 19f), () => { });
            CreateText(inventoryWindow, "I", 10, TextAnchor.MiddleCenter, new Vector2(10f, -224f), new Vector2(78f, 19f), Color.white);
            CreateText(inventoryWindow, "II", 10, TextAnchor.MiddleCenter, new Vector2(88f, -224f), new Vector2(78f, 19f), Color.white);

            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 5; x++)
                {
                    int itemIndex = y * 5 + x;
                    RawImage itemSlot = CreateAtlasTop(inventoryWindow, "Item Slot " + itemIndex, Texture("public"),
                        new Rect(0, 348, 32, 32), new Vector2(8f + x * 32f, -246f - y * 32f), new Vector2(32f, 32f));
                    itemSlot.raycastTarget = true;
                    itemSlot.gameObject.AddComponent<Metin2QuickSlotDragSource>().Clear();
                }

            CreateAtlasTop(inventoryWindow, "Money Slot", Texture("public"), new Rect(0, 124, 130, 18),
                new Vector2(28f, -538f), new Vector2(130f, 18f));
            CreateAtlasTop(inventoryWindow, "Money Icon", Texture("windows"), new Rect(313, 135, 16, 16),
                new Vector2(10f, -539f), new Vector2(16f, 16f));
            Text money = CreateText(inventoryWindow, "0", 11, TextAnchor.MiddleRight, new Vector2(31f, -538f), new Vector2(122f, 18f),
                new Color(0.94f, 0.85f, 0.60f));
            money.name = "Money Value";
            inventoryWindow.gameObject.SetActive(false);
        }

        void BuildCharacterWindow()
        {
            characterWindow = CreateRect(canvas.transform, "CharacterWindow", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(253f, 361f));
            BuildBoard(characterWindow);
            CreateTitle(characterWindow, "KARAKTER", 61f, 7f, 185f, characterWindow);

            characterPages = new RectTransform[4];
            for (int index = 0; index < characterPages.Length; index++)
                characterPages[index] = CreateRect(characterWindow, "Page " + index, Vector2.up, Vector2.up, Vector2.up,
                    Vector2.zero, new Vector2(250f, 320f));
            BuildStatusPage(characterPages[0]);
            BuildSkillPage(characterPages[1]);
            BuildSimplePage(characterPages[2], "EYLEMLER", new[] { "Duygu hareketleri", "Selamlama", "Tezahürat", "Otur / Kalk" });
            BuildSimplePage(characterPages[3], "GÖREVLER", new[] { "Aktif görev bulunmuyor." });

            string[] tabs = { "Karakter", "Beceri", "Eylem", "Görev" };
            float[] widths = { 58f, 62f, 58f, 58f };
            float xPosition = 6f;
            for (int index = 0; index < tabs.Length; index++)
            {
                int captured = index;
                Button button = CreateTextButton(characterWindow, tabs[index], new Vector2(xPosition, -331f), new Vector2(widths[index], 25f),
                    () => ShowCharacterPage(captured));
                button.name = "Tab " + tabs[index];
                xPosition += widths[index] + 2f;
            }
            ShowCharacterPage(0);
            characterWindow.gameObject.SetActive(false);
        }

        void BuildStatusPage(RectTransform page)
        {
            Rect face = FaceRect(Metin2GameplaySession.CharacterClass);
            CreateAtlasTop(page, "Face", Texture("windows"), face, new Vector2(11f, -11f), new Vector2(43f, 43f));
            CreateAtlasTop(page, "Face Frame", Texture("windows"), new Rect(227, 0, 53, 53), new Vector2(7f, -7f), new Vector2(53f, 53f));
            characterNameText = CreateValueSlot(page, "Character Name", new Vector2(62f, -34f), new Vector2(130f, 18f), Metin2GameplaySession.CharacterName);
            characterLevelText = CreateValueSlot(page, "Level", new Vector2(12f, -66f), new Vector2(39f, 18f), "1");
            CreateText(page, "Seviye", 10, TextAnchor.MiddleLeft, new Vector2(12f, -53f), new Vector2(42f, 14f), new Color(0.92f, 0.78f, 0.50f));
            CreateValueSlot(page, "EXP", new Vector2(62f, -66f), new Vector2(130f, 18f), "0");
            CreateText(page, "Tecrübe", 10, TextAnchor.MiddleLeft, new Vector2(62f, -53f), new Vector2(65f, 14f), new Color(0.92f, 0.78f, 0.50f));

            CreateHorizontalBar(page, new Vector2(12f, -104f), 223f);
            CreateText(page, "TEMEL YETENEKLER", 11, TextAnchor.MiddleLeft, new Vector2(16f, -102f), new Vector2(150f, 18f),
                new Color(0.95f, 0.80f, 0.43f));
            string[] names = { "VIT", "INT", "STR", "DEX" };
            int[] values = { Metin2GameplaySession.Vitality, Metin2GameplaySession.Intelligence, Metin2GameplaySession.Strength, Metin2GameplaySession.Dexterity };
            for (int index = 0; index < 4; index++)
            {
                float y = -132f - index * 25f;
                CreateText(page, names[index], 11, TextAnchor.MiddleLeft, new Vector2(20f, y), new Vector2(50f, 18f), Color.white);
                CreateValueSlot(page, names[index] + " Value", new Vector2(72f, y), new Vector2(39f, 18f), values[index].ToString());
                CreateTextButton(page, "+", new Vector2(116f, y - 1f), new Vector2(18f, 18f), () => { });
            }

            CreateHorizontalBar(page, new Vector2(12f, -243f), 223f);
            CreateText(page, "SAVAŞ DEĞERLERİ", 11, TextAnchor.MiddleLeft, new Vector2(16f, -241f), new Vector2(150f, 18f),
                new Color(0.95f, 0.80f, 0.43f));
            int sourceLevel = Mathf.Max(1, Metin2GameplaySession.Level);
            int attack = 20 + Metin2GameplaySession.Strength * 2 + sourceLevel * 3;
            int defence = 10 + Metin2GameplaySession.Vitality * 2 + sourceLevel * 2;
            CreateText(page, "Saldırı Değeri", 10, TextAnchor.MiddleLeft, new Vector2(18f, -270f), new Vector2(100f, 18f), Color.white);
            CreateValueSlot(page, "Attack", new Vector2(142f, -270f), new Vector2(90f, 18f), attack.ToString());
            CreateText(page, "Savunma", 10, TextAnchor.MiddleLeft, new Vector2(18f, -294f), new Vector2(100f, 18f), Color.white);
            CreateValueSlot(page, "Defence", new Vector2(142f, -294f), new Vector2(90f, 18f), defence.ToString());
        }

        void BuildSkillPage(RectTransform page)
        {
            CreateText(page, "BECERİLER", 13, TextAnchor.MiddleCenter, new Vector2(8f, -34f), new Vector2(237f, 24f),
                new Color(0.95f, 0.78f, 0.38f), true);
            string[] names = SkillNamesForClass(Metin2GameplaySession.CharacterClass);
            Rect[] icons = SkillIconRectsForClass(Metin2GameplaySession.CharacterClass);
            Texture2D atlas = SkillAtlasForClass(Metin2GameplaySession.CharacterClass);
            for (int index = 0; index < names.Length; index++)
            {
                float y = -68f - index * 40f;
                RawImage skillIcon = CreateAtlasTop(page, names[index] + " Icon", atlas, icons[index],
                    new Vector2(18f, y), new Vector2(32f, 32f));
                skillIcon.raycastTarget = true;
                skillIcon.gameObject.AddComponent<Metin2QuickSlotDragSource>()
                    .ConfigureSkill(index, names[index], atlas, skillIcon.uvRect);
                CreateText(page, names[index], 11, TextAnchor.MiddleLeft, new Vector2(58f, y), new Vector2(142f, 18f), Color.white);
                CreateText(page, "Sürükle", 9, TextAnchor.MiddleCenter, new Vector2(194f, y), new Vector2(43f, 18f),
                    new Color(0.96f, 0.78f, 0.33f));
            }
        }

        void BuildSimplePage(RectTransform page, string title, string[] lines)
        {
            CreateText(page, title, 13, TextAnchor.MiddleCenter, new Vector2(8f, -34f), new Vector2(237f, 24f),
                new Color(0.95f, 0.78f, 0.38f), true);
            for (int index = 0; index < lines.Length; index++)
                CreateText(page, lines[index], 11, TextAnchor.MiddleLeft, new Vector2(20f, -74f - index * 28f),
                    new Vector2(210f, 22f), new Color(0.88f, 0.86f, 0.80f));
        }

        void ShowCharacterPage(int page)
        {
            for (int index = 0; index < characterPages.Length; index++) characterPages[index].gameObject.SetActive(index == page);
        }

        void BuildBoard(RectTransform root)
        {
            RectTransform baseRect = CreateRect(root, "Board Base", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-64f, -64f));
            RawImage background = baseRect.gameObject.AddComponent<RawImage>();
            background.texture = Texture("board_base");
            background.raycastTarget = false;
            background.uvRect = new Rect(0f, 0f, Mathf.Max(1f, (root.sizeDelta.x - 64f) / 128f),
                Mathf.Max(1f, (root.sizeDelta.y - 64f) / 128f));
            background.color = Texture("board_base") != null ? Color.white : new Color(0.12f, 0.09f, 0.055f, 0.97f);
            CreateBorder(root, "board_line_top", new Vector2(-64f, 32f), true, false);
            CreateBorder(root, "board_line_bottom", new Vector2(-64f, 32f), true, true);
            CreateBorder(root, "board_line_left", new Vector2(32f, -64f), false, false);
            CreateBorder(root, "board_line_right", new Vector2(32f, -64f), false, true);
            CreateCorner(root, "board_corner_lefttop", Vector2.up, Vector2.up);
            CreateCorner(root, "board_corner_righttop", Vector2.one, Vector2.one);
            CreateCorner(root, "board_corner_leftbottom", Vector2.zero, Vector2.zero);
            CreateCorner(root, "board_corner_rightbottom", Vector2.right, Vector2.right);
        }

        void CreateCorner(RectTransform parent, string textureName, Vector2 anchor, Vector2 pivot)
        {
            CreateRaw(parent, textureName, Texture(textureName), Vector2.zero, new Vector2(32f, 32f),
                anchor, anchor, pivot).raycastTarget = false;
        }

        void CreateBorder(RectTransform parent, string textureName, Vector2 size, bool horizontal, bool opposite)
        {
            Vector2 anchorMin;
            Vector2 anchorMax;
            Vector2 pivot;
            Vector2 position;
            if (horizontal)
            {
                anchorMin = new Vector2(0f, opposite ? 0f : 1f);
                anchorMax = new Vector2(1f, opposite ? 0f : 1f);
                pivot = new Vector2(0.5f, opposite ? 0f : 1f);
                position = new Vector2(0f, 0f);
            }
            else
            {
                anchorMin = new Vector2(opposite ? 1f : 0f, 0f);
                anchorMax = new Vector2(opposite ? 1f : 0f, 1f);
                pivot = new Vector2(opposite ? 1f : 0f, 0.5f);
                position = Vector2.zero;
            }
            RawImage line = CreateRaw(parent, textureName, Texture(textureName), position, size, anchorMin, anchorMax, pivot);
            float repeat = horizontal
                ? Mathf.Max(1f, (parent.rect.width - 64f) / 128f)
                : Mathf.Max(1f, (parent.rect.height - 64f) / 128f);
            line.uvRect = horizontal ? new Rect(0f, 0f, repeat, 1f) : new Rect(0f, 0f, 1f, repeat);
            line.raycastTarget = false;
        }

        void CreateTitle(RectTransform parent, string title, float x, float y, float width, RectTransform dragTarget)
        {
            RectTransform titleRoot = CreateRect(parent, title + " Title", Vector2.up, Vector2.up, Vector2.up,
                new Vector2(x, -y), new Vector2(width, 23f));
            CreateRaw(titleRoot, "Title Left", Texture("titlebar_left"), Vector2.zero, new Vector2(32f, 32f),
                Vector2.up, Vector2.up, Vector2.up);
            RawImage titleBackground = CreateRaw(titleRoot, "Title Center", Texture("titlebar_center"), Vector2.zero,
                new Vector2(-64f, 32f), Vector2.up, Vector2.one, new Vector2(0.5f, 1f));
            titleBackground.uvRect = new Rect(0f, 0f, Mathf.Max(1f, (width - 64f) / 32f), 1f);
            titleBackground.raycastTarget = true;
            CreateRaw(titleRoot, "Title Right", Texture("titlebar_right"), Vector2.zero, new Vector2(32f, 32f),
                Vector2.one, Vector2.one, Vector2.one);
            Text label = CreateText(titleRoot, title, 11, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                new Color(0.96f, 0.82f, 0.51f), true);
            Stretch(label.rectTransform);
            Metin2UIDraggable draggable = titleRoot.gameObject.AddComponent<Metin2UIDraggable>();
            draggable.target = dragTarget;
            Button close = CreateAtlasButton(titleRoot, "Close", Texture("public"), new Rect(25, 425, 15, 15),
                new Vector2(width - 18f, -3f), new Vector2(15f, 15f),
                () => parent.gameObject.SetActive(false));
            close.name = "Close";
        }

        void InitializeStatus()
        {
            level = Mathf.Max(1, Metin2GameplaySession.Level);
            int vitality = Mathf.Max(1, Metin2GameplaySession.Vitality);
            int intelligence = Mathf.Max(1, Metin2GameplaySession.Intelligence);
            maxHp = 500f + vitality * 40f + level * 50f;
            maxSp = 150f + intelligence * 25f + level * 12f;
            maxStamina = 800f;
            currentHp = maxHp;
            currentSp = maxSp;
            currentStamina = maxStamina;
            experience = 0;
            nextExperience = Mathf.Max(100, level * level * 100);
            gold = 0;
        }

        void UpdateStatusVisuals()
        {
            if (hpFill == null) return;
            if (taskbarBase != null)
                taskbarBase.uvRect = new Rect(0f, 0f, Mathf.Max(1f, taskbarBase.rectTransform.rect.width / 256f), 1f);
            hpFill.fillAmount = maxHp > 0f ? currentHp / maxHp : 0f;
            spFill.fillAmount = maxSp > 0f ? currentSp / maxSp : 0f;
            staminaFill.fillAmount = maxStamina > 0f ? currentStamina / maxStamina : 0f;
            hpText.text = Mathf.CeilToInt(currentHp) + " / " + Mathf.CeilToInt(maxHp);
            spText.text = Mathf.CeilToInt(currentSp) + " / " + Mathf.CeilToInt(maxSp);
            if (characterNameText != null) characterNameText.text = Metin2GameplaySession.CharacterName;
            if (characterLevelText != null) characterLevelText.text = level.ToString();
            float exp = nextExperience > 0 ? Mathf.Clamp01(experience / (float)nextExperience) : 0f;
            for (int index = 0; index < experiencePoints.Count; index++)
                experiencePoints[index].fillAmount = Mathf.Clamp01(exp * 4f - index);
            Text money = inventoryWindow != null ? inventoryWindow.Find("Money Value")?.GetComponent<Text>() : null;
            if (money != null) money.text = gold.ToString("N0");
        }

        void UpdateQuickSlotHighlights(Keyboard keyboard)
        {
            bool[] pressed =
            {
                keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed,
                keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed,
                keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed,
                keyboard.digit4Key.isPressed || keyboard.numpad4Key.isPressed,
                keyboard.f1Key.isPressed, keyboard.f2Key.isPressed, keyboard.f3Key.isPressed, keyboard.f4Key.isPressed
            };
            for (int index = 0; index < quickSlotHighlights.Length; index++)
                if (quickSlotHighlights[index] != null)
                    quickSlotHighlights[index].color = new Color(1f, 0.78f, 0.20f, pressed[index] ? 0.36f : 0f);
        }

        void ActivateQuickSlot(int index)
        {
            if (player == null) player = FindFirstObjectByType<Metin2PlayerController>();
            if (player != null) player.ActivateQuickSlot(index);
        }

        void ToggleInventory()
        {
            inventoryWindow.gameObject.SetActive(!inventoryWindow.gameObject.activeSelf);
            if (inventoryWindow.gameObject.activeSelf) inventoryWindow.SetAsLastSibling();
        }

        void ToggleCharacter()
        {
            characterWindow.gameObject.SetActive(!characterWindow.gameObject.activeSelf);
            if (characterWindow.gameObject.activeSelf) characterWindow.SetAsLastSibling();
        }

        void ToggleCameraView()
        {
            Metin2GameplayCamera gameplayCamera = FindFirstObjectByType<Metin2GameplayCamera>();
            if (gameplayCamera != null) gameplayCamera.ToggleView();
        }

        void CloseAllWindows()
        {
            inventoryWindow.gameObject.SetActive(false);
            characterWindow.gameObject.SetActive(false);
        }

        static Rect FaceRect(Metin2CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case Metin2CharacterClass.Assassin: return new Rect(227, 73, 43, 43);
                case Metin2CharacterClass.Sura: return new Rect(399, 73, 43, 43);
                case Metin2CharacterClass.Shaman: return new Rect(313, 73, 43, 43);
                default: return new Rect(156, 152, 43, 43);
            }
        }

        Texture2D SkillAtlasForClass(Metin2CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case Metin2CharacterClass.Assassin: return Texture("skillassassin");
                case Metin2CharacterClass.Sura: return Texture("skillsura");
                case Metin2CharacterClass.Shaman: return Texture("skillshaman");
                default: return Texture("skillwarrior");
            }
        }

        static Rect[] SkillIconRectsForClass(Metin2CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case Metin2CharacterClass.Assassin: return AssassinSkillIconRects;
                case Metin2CharacterClass.Sura: return SuraSkillIconRects;
                case Metin2CharacterClass.Shaman: return ShamanSkillIconRects;
                default: return WarriorSkillIconRects;
            }
        }

        static string[] SkillNamesForClass(Metin2CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case Metin2CharacterClass.Assassin:
                    return new[] { "Suikast", "Hızlı Saldırı", "Bıçak Çevirme", "Kamuflaj", "Zehirli Bulut", "Zehirli Ok" };
                case Metin2CharacterClass.Sura:
                    return new[] { "Parmak Darbesi", "Ejderha Dönüşü", "Büyülü Keskinlik", "Dehşet", "Büyülü Zırh", "Büyü Çözme" };
                case Metin2CharacterClass.Shaman:
                    return new[] { "Uçan Tılsım", "Ejderha Atışı", "Ejderha Kükremesi", "Kutsama", "Yansıtma", "Ejderha Yardımı" };
                default:
                    return new[] { "Üç Yönlü Kesme", "Kılıç Çevirme", "Öfke", "Hava Kılıcı", "Hamle", "Kılıç Darbesi" };
            }
        }

        Image CreateGaugeFill(Transform parent, string name, Vector2 bottomLeft, Vector2 size, string textureName)
        {
            Image image = CreateImageBottom(parent, name, bottomLeft, size, Color.white);
            image.sprite = SpriteFor(Texture(textureName), new Rect(0f, 0f,
                Texture(textureName) != null ? Texture(textureName).width : 1f,
                Texture(textureName) != null ? Texture(textureName).height : 1f));
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            return image;
        }

        Image CreateAtlasImageBottom(Transform parent, string name, Texture2D atlas, Rect source, Vector2 bottomLeft, Vector2 size)
        {
            Image image = CreateImageBottom(parent, name, bottomLeft, size, Color.white);
            image.sprite = SpriteFor(atlas, source);
            return image;
        }

        Sprite SpriteFor(Texture2D atlas, Rect source)
        {
            if (atlas == null) return null;
            string key = atlas.GetInstanceID() + ":" + source.x + ":" + source.y + ":" + source.width + ":" + source.height;
            if (sprites.TryGetValue(key, out Sprite sprite)) return sprite;
            Rect unityRect = new Rect(source.x, atlas.height - source.y - source.height, source.width, source.height);
            sprite = Sprite.Create(atlas, unityRect, new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            sprites[key] = sprite;
            return sprite;
        }

        Text CreateValueSlot(Transform parent, string name, Vector2 topLeft, Vector2 size, string value)
        {
            CreateAtlasTop(parent, name + " Slot", Texture("public"), new Rect(0, 124, 130, 18), topLeft, size);
            return CreateText(parent, value, 11, TextAnchor.MiddleCenter, topLeft, size, Color.white);
        }

        void CreateHorizontalBar(Transform parent, Vector2 topLeft, float width)
        {
            RectTransform barRoot = CreateRect(parent, "Horizontal Bar", Vector2.up, Vector2.up, Vector2.up,
                topLeft, new Vector2(width, 17f));
            CreateRaw(barRoot, "Left", Texture("horizontalbar_left"), Vector2.zero, new Vector2(32f, 32f),
                Vector2.up, Vector2.up, Vector2.up);
            RawImage center = CreateRaw(barRoot, "Center", Texture("horizontalbar_center"), Vector2.zero,
                new Vector2(-64f, 32f), Vector2.up, Vector2.one, new Vector2(0.5f, 1f));
            center.uvRect = new Rect(0f, 0f, Mathf.Max(1f, (width - 64f) / 32f), 1f);
            CreateRaw(barRoot, "Right", Texture("horizontalbar_right"), Vector2.zero, new Vector2(32f, 32f),
                Vector2.one, Vector2.one, Vector2.one);
        }

        Button CreateTextButton(Transform parent, string value, Vector2 topLeft, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(parent, value + " Button", Vector2.up, Vector2.up, Vector2.up, topLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.09f, 0.055f, 0.96f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.60f, 0.45f, 0.22f, 0.90f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Text label = CreateText(rect, value, 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, Color.white);
            Stretch(label.rectTransform);
            return button;
        }

        Button CreateAtlasButton(Transform parent, string name, Texture2D atlas, Rect source, Vector2 topLeft, Vector2 size,
            UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreateRect(parent, name, Vector2.up, Vector2.up, Vector2.up, topLeft, size);
            RawImage image = CreateAtlasStretch(rect, name + " Image", atlas, source);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            return button;
        }

        RawImage CreateAtlasTop(Transform parent, string name, Texture2D atlas, Rect source, Vector2 topLeft, Vector2 size)
        {
            RawImage image = CreateRawTop(parent, name, atlas, topLeft, size);
            image.uvRect = AtlasUv(atlas, source);
            return image;
        }

        RawImage CreateAtlasBottom(Transform parent, string name, Texture2D atlas, Rect source, Vector2 bottomLeft, Vector2 size)
        {
            RectTransform rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, bottomLeft, size);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.uvRect = AtlasUv(atlas, source);
            image.raycastTarget = false;
            return image;
        }

        RawImage CreateAtlasStretch(Transform parent, string name, Texture2D atlas, Rect source)
        {
            RawImage image = CreateRaw(parent, name, atlas, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f));
            image.uvRect = AtlasUv(atlas, source);
            return image;
        }

        static Rect AtlasUv(Texture2D atlas, Rect source)
        {
            if (atlas == null) return new Rect(0f, 0f, 1f, 1f);
            return new Rect(source.x / atlas.width, 1f - (source.y + source.height) / atlas.height,
                source.width / atlas.width, source.height / atlas.height);
        }

        RawImage CreateRawTop(Transform parent, string name, Texture texture, Vector2 topLeft, Vector2 size)
        {
            return CreateRaw(parent, name, texture, topLeft, size, Vector2.up, Vector2.up, Vector2.up);
        }

        RawImage CreateRaw(Transform parent, string name, Texture texture, Vector2 position, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, pivot, position, size);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        Image CreateImage(Transform parent, string name, Vector2 topLeft, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, Vector2.up, Vector2.up, Vector2.up, topLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Image CreateImageBottom(Transform parent, string name, Vector2 bottomLeft, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero, bottomLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text CreateText(Transform parent, string value, int size, TextAnchor alignment, Vector2 topLeft, Vector2 dimensions,
            Color color, bool bold = false)
        {
            RectTransform rect = CreateRect(parent, value + " Text", Vector2.up, Vector2.up, Vector2.up, topLeft, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.90f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}


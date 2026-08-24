using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Metin2Dev.Frontend
{
    [DisallowMultipleComponent]
    public sealed class Metin2FrontendController : MonoBehaviour
    {
        const string LastAccountKey = "Metin2.Frontend.LastAccount.v2";
        const string AccountSavePrefix = "Metin2.Frontend.Account.v2.";
        const string EditableLayoutName = "Metin2 Frontend Editable Layout";
        const string RuntimeCanvasName = "Metin2 Frontend Runtime Canvas";
        const float ReferenceWidth = 1024f;
        const float ReferenceHeight = 768f;

        static readonly string[] ClassNames = { "Savaşçı", "Ninja", "Sura", "Şaman" };
        static readonly string[] ClassDescriptions =
        {
            "Güçlü bedeni ve yakın dövüş yeteneğiyle savaş alanının ön saflarında yer alır.",
            "Çevikliği, hızlı saldırıları ve isabetli hamleleriyle rakibini hazırlıksız yakalar.",
            "Kılıç ustalığını karanlık büyüyle birleştirerek dengeli ve tehlikeli bir savaşçı olur.",
            "Ejderha gücünden yararlanır; büyüleriyle hem kendisini hem de müttefiklerini destekler.",
        };
        static readonly int[,] StartingStats =
        {
            { 4, 3, 6, 3 },
            { 3, 3, 4, 6 },
            { 3, 5, 5, 3 },
            { 4, 6, 3, 3 },
        };
        static readonly string[] EmpireNames = { "", "Shinsoo", "Chunjo", "Jinno" };
        static readonly string[] EditableScreenNames =
        {
            "Login Screen",
            "Empire Selection",
            "Character Selection",
            "Character Creation",
            "Loading Screen",
        };
        static readonly Color[] EmpireColors =
        {
            Color.white,
            new Color(0.72f, 0.12f, 0.08f),
            new Color(0.82f, 0.62f, 0.12f),
            new Color(0.10f, 0.33f, 0.70f),
        };

        [SerializeField] Metin2FrontendConfig config;

        readonly Dictionary<Texture2D, Sprite> spriteCache = new Dictionary<Texture2D, Sprite>();
        readonly string[] servers = { "Metin3", "Yerel Test" };
        readonly string[] channels = { "CH1", "CH2", "CH3", "CH4" };

        Metin2FrontendSaveData saveData;
        Canvas canvas;
        RectTransform screenRoot;
        Font uiFont;
        int serverIndex;
        int channelIndex;
        int selectedSlot;
        int createSlot = -1;
        Metin2Empire draftEmpire = Metin2Empire.Shinsoo;
        Metin2CharacterClass draftClass = Metin2CharacterClass.Warrior;
        Metin2Gender draftGender = Metin2Gender.Male;
        string draftName = string.Empty;
        Coroutine loadingRoutine;
        bool authoringLayout;
        bool useEditableHierarchy;
        bool editableCharacterListCached;
        Vector2 editableCharacterSlotBase;
        Vector2 editableNewCharacterBase;

        public Metin2FrontendConfig Config => config;

        public void Configure(Metin2FrontendConfig value)
        {
            config = value;
        }

        void Awake()
        {
            if (config == null)
            {
                Debug.LogError("Metin2 frontend config is missing.", this);
                enabled = false;
                return;
            }

            Scene frontendScene = gameObject.scene;
            DontDestroyOnLoad(gameObject);
            IsolateFrontend(frontendScene);
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadLastAccountHint();
            CreateEventSystem();
            if (!UseEditableHierarchy()) CreateCanvas(RuntimeCanvasName);
            ShowLogin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Environment.GetCommandLineArgs().Any(argument => argument == "-metin2FrontendCapture"))
                StartCoroutine(CapturePreviewFromCommandLine());
#endif
        }

        void OnDestroy()
        {
            foreach (Sprite sprite in spriteCache.Values)
                if (sprite != null) Destroy(sprite);
            spriteCache.Clear();
        }

        void LoadLastAccountHint()
        {
            saveData = new Metin2FrontendSaveData
            {
                accountId = PlayerPrefs.GetString(LastAccountKey, string.Empty),
            };
            saveData.EnsureSlots();
        }

        void LoadAccount(string accountId)
        {
            string normalized = NormalizeAccountId(accountId);
            string json = PlayerPrefs.GetString(AccountSavePrefix + normalized, string.Empty);
            saveData = string.IsNullOrWhiteSpace(json)
                ? new Metin2FrontendSaveData()
                : JsonUtility.FromJson<Metin2FrontendSaveData>(json) ?? new Metin2FrontendSaveData();
            saveData.accountId = accountId.Trim();
            saveData.EnsureSlots();
            selectedSlot = FirstOccupiedSlot();
        }

        void Save()
        {
            saveData.EnsureSlots();
            if (string.IsNullOrWhiteSpace(saveData.accountId)) return;
            PlayerPrefs.SetString(AccountSavePrefix + NormalizeAccountId(saveData.accountId), JsonUtility.ToJson(saveData));
            PlayerPrefs.SetString(LastAccountKey, saveData.accountId);
            PlayerPrefs.Save();
        }

        int FirstOccupiedSlot()
        {
            int occupied = Array.FindIndex(saveData.characters, character => character != null);
            return occupied >= 0 ? occupied : -1;
        }

        static string NormalizeAccountId(string accountId)
        {
            return (accountId ?? string.Empty).Trim().ToLowerInvariant();
        }

        void IsolateFrontend(Scene frontendScene)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            for (int sceneIndex = SceneManager.sceneCount - 1; sceneIndex >= 0; sceneIndex--)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || scene.handle == frontendScene.handle)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    root.SetActive(false);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        void CreateEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(transform, false);
            InputSystemUIInputModule inputModule = eventObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        void CreateCanvas(string canvasName)
        {
            GameObject canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        bool UseEditableHierarchy()
        {
            Transform layoutRoot = transform.Find(EditableLayoutName);
            if (layoutRoot == null) return false;
            canvas = layoutRoot.GetComponent<Canvas>();
            if (canvas == null) return false;
            layoutRoot.gameObject.SetActive(true);
            foreach (Transform child in layoutRoot)
                if (IsEditableScreenName(child.name)) child.gameObject.SetActive(false);
            useEditableHierarchy = true;
            return true;
        }

        bool TryShowEditableScreen(string name, out RectTransform root)
        {
            root = null;
            if (!useEditableHierarchy || authoringLayout || canvas == null) return false;
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
                loadingRoutine = null;
            }
            foreach (Transform child in canvas.transform)
            {
                if (!IsEditableScreenName(child.name)) continue;
                bool selected = child.name == name;
                child.gameObject.SetActive(selected);
                if (selected) root = child as RectTransform;
            }
            if (root == null) return false;
            screenRoot = root;
            return true;
        }

        static bool IsEditableScreenName(string name)
        {
            return EditableScreenNames.Contains(name);
        }

        RectTransform BeginScreen(string name, Texture2D background)
        {
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
                loadingRoutine = null;
            }
            if (screenRoot != null && !authoringLayout)
            {
                screenRoot.gameObject.SetActive(false);
                Destroy(screenRoot.gameObject);
            }

            screenRoot = CreateRect(canvas.transform, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            if (authoringLayout)
            {
                RawImage backdrop = screenRoot.gameObject.AddComponent<RawImage>();
                backdrop.texture = background;
                backdrop.color = Color.white;
                backdrop.raycastTarget = false;
            }
            else
            {
                Image backdrop = screenRoot.gameObject.AddComponent<Image>();
                backdrop.sprite = SpriteFor(background);
                backdrop.color = Color.white;
                backdrop.raycastTarget = false;
            }
            return screenRoot;
        }

        void ShowLogin()
        {
            if (TryShowEditableScreen("Login Screen", out RectTransform editableRoot))
            {
                BindEditableLogin(editableRoot);
                return;
            }
            RectTransform root = BeginScreen("Login Screen", config.loginBackground);
            RectTransform panel = CreatePanel(root, "Login Panel", new Vector2(382f, -390f), new Vector2(260f, 238f));
            CreateText(panel, "HESAP GİRİŞİ", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(16f, -14f), new Vector2(228f, 28f), new Color(0.95f, 0.76f, 0.36f));

            InputField account = CreateInput(panel, "Hesap adı", false, new Vector2(22f, -55f), new Vector2(216f, 32f));
            InputField password = CreateInput(panel, "Şifre", true, new Vector2(22f, -94f), new Vector2(216f, 32f));
            account.text = saveData.accountId ?? string.Empty;

            Text serverLabel;
            Button serverButton = CreateButton(panel, "Sunucu", new Vector2(22f, -136f), new Vector2(105f, 30f), out serverLabel);
            Text channelLabel;
            Button channelButton = CreateButton(panel, "Kanal", new Vector2(133f, -136f), new Vector2(105f, 30f), out channelLabel);
            Action updateConnectionLabels = () =>
            {
                serverLabel.text = servers[serverIndex];
                channelLabel.text = channels[channelIndex];
            };
            serverButton.onClick.AddListener(() => { serverIndex = (serverIndex + 1) % servers.Length; updateConnectionLabels(); });
            channelButton.onClick.AddListener(() => { channelIndex = (channelIndex + 1) % channels.Length; updateConnectionLabels(); });
            updateConnectionLabels();

            Text status = CreateText(panel, string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(18f, -170f), new Vector2(224f, 22f), new Color(0.93f, 0.78f, 0.53f));
            Text loginLabel;
            Button login = CreateButton(panel, "Giriş", new Vector2(22f, -198f), new Vector2(132f, 30f), out loginLabel, true);
            Text quitLabel;
            Button quit = CreateButton(panel, "Çıkış", new Vector2(160f, -198f), new Vector2(78f, 30f), out quitLabel);
            login.onClick.AddListener(() =>
            {
                string id = account.text.Trim();
                if (id.Length < 2 || string.IsNullOrEmpty(password.text))
                {
                    status.text = "Hesap adı ve şifreyi gir.";
                    return;
                }
                LoadAccount(id);
                Save();
                if (saveData.empire == Metin2Empire.None) ShowEmpireSelection();
                else ShowCharacterSelection();
            });
            password.onEndEdit.AddListener(_ =>
            {
                if (!string.IsNullOrEmpty(password.text)) login.onClick.Invoke();
            });
            quit.onClick.AddListener(QuitApplication);

            Text footer = CreateText(root,
                "Unity giriş prototipi • Sunucu bağlantı katmanı sonraki aşamada bağlanacak",
                12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, 12f), new Vector2(620f, 24f), new Color(0.62f, 0.62f, 0.62f), false);
            AnchorBottomCenter(footer.rectTransform);
        }

        void ShowEmpireSelection()
        {
            if (TryShowEditableScreen("Empire Selection", out RectTransform editableRoot))
            {
                BindEditableEmpire(editableRoot);
                return;
            }
            RectTransform root = BeginScreen("Empire Selection", config.serverBackground != null ? config.serverBackground : config.selectionBackground);
            draftEmpire = saveData.empire != Metin2Empire.None ? saveData.empire : draftEmpire;
            CreateText(root, "İMPARATORLUĞUNU SEÇ", 30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, -32f), new Vector2(700f, 48f), new Color(1f, 0.82f, 0.43f), false);

            RectTransform panel = CreatePanel(root, "Empire Map Window", new Vector2(137f, -90f), new Vector2(750f, 610f),
                new Color(0.02f, 0.018f, 0.016f, 0.94f));
            CreateText(panel, "Bayrağına dokunarak imparatorluğunu seç", 15, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(50f, -18f), new Vector2(650f, 28f),
                new Color(0.90f, 0.85f, 0.74f));

            Texture2D empireMapTexture = config.empireMap != null
                ? config.empireMap
                : Resources.Load<Texture2D>("Metin2Frontend/empire_map");
            if (authoringLayout)
            {
                RectTransform mapRect = CreateRect(panel, "Original Empire Map", new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(75f, -58f), new Vector2(600f, 410f));
                RawImage map = mapRect.gameObject.AddComponent<RawImage>();
                map.texture = empireMapTexture;
                map.color = Color.white;
                map.raycastTarget = false;
            }
            else
            {
                Image map = CreateImage(panel, "Original Empire Map", SpriteFor(empireMapTexture),
                    new Vector2(75f, -58f), new Vector2(600f, 410f), Color.white);
                map.preserveAspect = true;
                map.raycastTarget = false;
            }

            // These hit regions follow the three painted territories in the original map.
            CreateEmpireMapButton(panel, Metin2Empire.Chunjo, new Vector2(116f, -116f), new Vector2(190f, 126f));
            CreateEmpireMapButton(panel, Metin2Empire.Jinno, new Vector2(390f, -132f), new Vector2(176f, 142f));
            CreateEmpireMapButton(panel, Metin2Empire.Shinsoo, new Vector2(154f, -304f), new Vector2(220f, 130f));

            CreateText(panel, EmpireNames[(int)draftEmpire].ToUpperInvariant(), 22, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(190f, -486f), new Vector2(370f, 34f),
                EmpireColors[(int)draftEmpire]);
            CreateText(panel, EmpireDescription(draftEmpire), 12, FontStyle.Normal, TextAnchor.UpperCenter,
                new Vector2(125f, -520f), new Vector2(500f, 34f), new Color(0.84f, 0.81f, 0.74f));

            Text confirmLabel;
            Button confirm = CreateButton(panel, "Bu Bayrağı Seç", new Vector2(286f, -564f),
                new Vector2(178f, 34f), out confirmLabel, true);
            confirm.onClick.AddListener(() =>
            {
                saveData.empire = draftEmpire;
                Save();
                ShowCharacterSelection();
            });

            Text backLabel;
            Button back = CreateButton(root, "Geri", new Vector2(40f, 24f), new Vector2(110f, 36f), out backLabel);
            AnchorBottomLeft(back.GetComponent<RectTransform>());
            back.onClick.AddListener(ShowLogin);
        }

        void CreateEmpireMapButton(RectTransform parent, Metin2Empire empire, Vector2 topLeft, Vector2 size)
        {
            Color color = EmpireColors[(int)empire];
            bool selected = empire == draftEmpire;
            RectTransform rect = CreateRect(parent, EmpireNames[(int)empire] + " Flag Selection",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, selected ? 0.04f : 0f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Mathf.Min(1f, color.r + 0.25f), Mathf.Min(1f, color.g + 0.25f),
                Mathf.Min(1f, color.b + 0.25f), selected ? 0.28f : 0f);
            outline.effectDistance = selected ? new Vector2(1f, -1f) : Vector2.zero;
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                draftEmpire = empire;
                ShowEmpireSelection();
            });
            Text label = CreateText(rect, EmpireNames[(int)empire], selected ? 16 : 14, FontStyle.Bold,
                TextAnchor.LowerCenter, Vector2.zero, Vector2.zero,
                selected ? Color.white : new Color(1f, 1f, 1f, 0.78f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            label.rectTransform.sizeDelta = new Vector2(-8f, -8f);
        }

        static string EmpireDescription(Metin2Empire empire)
        {
            switch (empire)
            {
                case Metin2Empire.Shinsoo: return "Kızıl Bayrak — güneyin ticaret ve dayanışma imparatorluğu.";
                case Metin2Empire.Chunjo: return "Sarı Bayrak — batının disiplin ve ruhani öğreti imparatorluğu.";
                case Metin2Empire.Jinno: return "Mavi Bayrak — doğunun askerî güç ve mücadele imparatorluğu.";
                default: return string.Empty;
            }
        }

        void ShowCharacterSelection()
        {
            if (TryShowEditableScreen("Character Selection", out RectTransform editableRoot))
            {
                BindEditableCharacterSelection(editableRoot);
                return;
            }
            RectTransform root = BeginScreen("Character Selection", config.selectionBackground);
            saveData.EnsureSlots();
            if (selectedSlot < 0 || selectedSlot >= saveData.characters.Length || saveData.characters[selectedSlot] == null)
                selectedSlot = FirstOccupiedSlot();
            Metin2CharacterData selected = selectedSlot >= 0 ? saveData.characters[selectedSlot] : null;

            RectTransform listPanel = CreatePanel(root, "Saved Characters", new Vector2(24f, -72f), new Vector2(300f, 632f),
                new Color(0.02f, 0.018f, 0.016f, 0.95f));
            CreateText(listPanel, "KARAKTERLERİM", 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(18f, -15f), new Vector2(264f, 34f), new Color(0.96f, 0.76f, 0.36f));
            CreateText(listPanel, saveData.accountId, 11, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(18f, -47f), new Vector2(264f, 20f), new Color(0.68f, 0.66f, 0.61f));

            int visibleRow = 0;
            for (int slot = 0; slot < saveData.characters.Length; slot++)
            {
                Metin2CharacterData character = saveData.characters[slot];
                if (character == null) continue;
                int captured = slot;
                Text slotLabel;
                Button slotButton = CreateButton(listPanel,
                    character.characterName + "\n" + ClassNames[(int)character.characterClass] + "  •  Sv. " + character.level,
                    new Vector2(20f, -82f - visibleRow * 94f), new Vector2(260f, 78f), out slotLabel,
                    slot == selectedSlot);
                slotLabel.fontSize = 15;
                slotLabel.alignment = TextAnchor.MiddleLeft;
                slotLabel.rectTransform.offsetMin = new Vector2(18f, 4f);
                slotLabel.rectTransform.offsetMax = new Vector2(-10f, -4f);
                slotButton.onClick.AddListener(() =>
                {
                    selectedSlot = captured;
                    ShowCharacterSelection();
                });
                visibleRow++;
            }

            int emptySlot = Array.FindIndex(saveData.characters, character => character == null);
            if (emptySlot >= 0)
            {
                Text newLabel;
                Button newCharacter = CreateButton(listPanel, "+  Yeni Karakter",
                    new Vector2(20f, -82f - visibleRow * 94f), new Vector2(260f, 56f), out newLabel, true);
                newCharacter.onClick.AddListener(() => BeginCreate(emptySlot));
            }

            Text empireLabel;
            Button changeEmpire = CreateButton(listPanel, "Bayrak Seçimi", new Vector2(20f, -553f),
                new Vector2(126f, 38f), out empireLabel);
            changeEmpire.onClick.AddListener(ShowEmpireSelection);
            Text exitLabel;
            Button exit = CreateButton(listPanel, "Hesaptan Çık", new Vector2(154f, -553f),
                new Vector2(126f, 38f), out exitLabel);
            exit.onClick.AddListener(ShowLogin);

            RectTransform previewRect = CreateRect(root, "Selected Character FBX Preview",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(310f, -24f), new Vector2(520f, 650f));
            if (authoringLayout)
                CreatePreviewPlaceholder(previewRect, "SEÇİLEN KARAKTER FBX ALANI");
            else
            {
                RawImage rawImage = previewRect.gameObject.AddComponent<RawImage>();
                rawImage.raycastTarget = false;
                Metin2CharacterPreview preview = previewRect.gameObject.AddComponent<Metin2CharacterPreview>();
                preview.Initialize(rawImage);
                if (selected != null) preview.Show(config, selected.characterClass, selected.gender);
            }
            previewRect.SetAsFirstSibling();

            RectTransform infoPanel = CreatePanel(root, "Selected Character Information",
                new Vector2(760f, -118f), new Vector2(240f, 478f), new Color(0.02f, 0.018f, 0.016f, 0.94f));
            if (selected == null)
            {
                CreateText(infoPanel, "KARAKTER YOK", 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(18f, -32f), new Vector2(204f, 34f), new Color(0.93f, 0.74f, 0.37f));
                CreateText(infoPanel, "Sol taraftaki Yeni Karakter düğmesiyle ilk karakterini oluştur.",
                    14, FontStyle.Normal, TextAnchor.UpperCenter, new Vector2(28f, -92f),
                    new Vector2(184f, 104f), new Color(0.84f, 0.81f, 0.75f));
            }
            else
            {
                CreateText(infoPanel, selected.characterName, 23, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(14f, -20f), new Vector2(212f, 38f), Color.white);
                CreateText(infoPanel, EmpireNames[(int)saveData.empire] + " • " +
                    (selected.gender == Metin2Gender.Male ? "Erkek" : "Kadın"), 12, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(14f, -58f), new Vector2(212f, 24f),
                    EmpireColors[(int)saveData.empire]);
                CreateInfoRow(infoPanel, "Sınıf", ClassNames[(int)selected.characterClass], 105f);
                CreateInfoRow(infoPanel, "Seviye", selected.level.ToString(), 140f);
                CreateInfoRow(infoPanel, "Oynama", FormatPlayTime(selected.playMinutes), 175f);
                CreateCharacterStat(infoPanel, "VIT", selected.vitality, 224f, new Color(0.70f, 0.14f, 0.10f));
                CreateCharacterStat(infoPanel, "INT", selected.intelligence, 264f, new Color(0.65f, 0.26f, 0.68f));
                CreateCharacterStat(infoPanel, "STR", selected.strength, 304f, new Color(0.75f, 0.43f, 0.10f));
                CreateCharacterStat(infoPanel, "DEX", selected.dexterity, 344f, new Color(0.12f, 0.42f, 0.78f));

                Text playLabel;
                Button play = CreateButton(infoPanel, "Oyuna Başla", new Vector2(28f, -393f),
                    new Vector2(184f, 38f), out playLabel, true);
                play.onClick.AddListener(() => ShowLoading(saveData.characters[selectedSlot]));
                Text deleteLabel;
                Button delete = CreateButton(infoPanel, "Karakteri Sil", new Vector2(58f, -437f),
                    new Vector2(124f, 26f), out deleteLabel);
                delete.onClick.AddListener(() => ShowDeleteConfirmation(selectedSlot));
            }
        }

        void BeginCreate(int slot)
        {
            createSlot = Mathf.Clamp(slot, 0, 3);
            draftClass = Metin2CharacterClass.Warrior;
            draftGender = Metin2Gender.Male;
            draftName = string.Empty;
            ShowCharacterCreation();
        }

        void ShowCharacterCreation()
        {
            if (TryShowEditableScreen("Character Creation", out RectTransform editableRoot))
            {
                BindEditableCharacterCreation(editableRoot);
                return;
            }
            RectTransform root = BeginScreen("Character Creation", config.selectionBackground);
            RectTransform classPanel = CreatePanel(root, "Character Class Selection", new Vector2(24f, -72f),
                new Vector2(270f, 632f), new Color(0.02f, 0.018f, 0.016f, 0.95f));
            CreateText(classPanel, "KARAKTER SINIFI", 19, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(16f, -15f), new Vector2(238f, 34f), new Color(0.96f, 0.76f, 0.36f));
            for (int index = 0; index < ClassNames.Length; index++)
            {
                int captured = index;
                Text classLabel;
                Button classButton = CreateButton(classPanel, ClassNames[index], new Vector2(20f, -70f - index * 82f),
                    new Vector2(230f, 66f), out classLabel, index == (int)draftClass);
                classLabel.fontSize = 18;
                classButton.onClick.AddListener(() =>
                {
                    draftClass = (Metin2CharacterClass)captured;
                    ShowCharacterCreation();
                });
            }
            CreateText(classPanel, ClassDescriptions[(int)draftClass], 13, FontStyle.Normal, TextAnchor.UpperCenter,
                new Vector2(24f, -420f), new Vector2(222f, 120f), new Color(0.86f, 0.83f, 0.77f));

            RectTransform previewRect = CreateRect(root, "Creation Character FBX Preview",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(250f, -20f), new Vector2(560f, 670f));
            if (authoringLayout)
                CreatePreviewPlaceholder(previewRect, "OLUŞTURULAN KARAKTER FBX ALANI");
            else
            {
                RawImage rawImage = previewRect.gameObject.AddComponent<RawImage>();
                rawImage.raycastTarget = false;
                Metin2CharacterPreview preview = previewRect.gameObject.AddComponent<Metin2CharacterPreview>();
                preview.Initialize(rawImage);
                preview.Show(config, draftClass, draftGender);
            }
            previewRect.SetAsFirstSibling();

            RectTransform panel = CreatePanel(root, "Character Registration", new Vector2(754f, -116f),
                new Vector2(246f, 500f), new Color(0.02f, 0.018f, 0.016f, 0.95f));
            CreateText(panel, "KARAKTER OLUŞTUR", 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(14f, -16f), new Vector2(218f, 34f), new Color(0.97f, 0.78f, 0.38f));
            CreateText(panel, ClassNames[(int)draftClass].ToUpperInvariant(), 23, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(14f, -56f), new Vector2(218f, 38f), Color.white);

            CreateText(panel, "Cinsiyet", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(20f, -106f), new Vector2(206f, 22f), new Color(0.83f, 0.79f, 0.70f));
            Text maleLabel;
            Button male = CreateButton(panel, "Erkek", new Vector2(24f, -134f), new Vector2(94f, 32f),
                out maleLabel, draftGender == Metin2Gender.Male);
            Text femaleLabel;
            Button female = CreateButton(panel, "Kadın", new Vector2(128f, -134f), new Vector2(94f, 32f),
                out femaleLabel, draftGender == Metin2Gender.Female);
            male.onClick.AddListener(() => { draftGender = Metin2Gender.Male; ShowCharacterCreation(); });
            female.onClick.AddListener(() => { draftGender = Metin2Gender.Female; ShowCharacterCreation(); });

            InputField nameInput = CreateInput(panel, "Karakter adı", false, new Vector2(24f, -183f), new Vector2(198f, 36f));
            nameInput.characterLimit = 12;
            nameInput.text = draftName;
            nameInput.onValueChanged.AddListener(value => draftName = value);
            Text status = CreateText(panel, string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(20f, -224f), new Vector2(206f, 34f), new Color(0.96f, 0.72f, 0.46f));

            CreateCharacterStat(panel, "VIT", StartingStats[(int)draftClass, 0], 270f,
                new Color(0.70f, 0.14f, 0.10f));
            CreateCharacterStat(panel, "INT", StartingStats[(int)draftClass, 1], 306f,
                new Color(0.65f, 0.26f, 0.68f));
            CreateCharacterStat(panel, "STR", StartingStats[(int)draftClass, 2], 342f,
                new Color(0.75f, 0.43f, 0.10f));
            CreateCharacterStat(panel, "DEX", StartingStats[(int)draftClass, 3], 378f,
                new Color(0.12f, 0.42f, 0.78f));

            Text createLabel;
            Button create = CreateButton(panel, "Kaydet", new Vector2(24f, -430f), new Vector2(128f, 38f), out createLabel, true);
            Text backLabel;
            Button back = CreateButton(panel, "Geri", new Vector2(160f, -430f), new Vector2(62f, 38f), out backLabel);
            create.onClick.AddListener(() =>
            {
                string candidate = draftName.Trim();
                if (candidate.Length < 2)
                {
                    status.text = "Karakter adı en az 2 harf olmalı.";
                    return;
                }
                if (saveData.characters.Any(item => item != null && string.Equals(item.characterName, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    status.text = "Bu isimde bir karakter zaten var.";
                    return;
                }

                saveData.characters[createSlot] = new Metin2CharacterData
                {
                    characterName = candidate,
                    characterClass = draftClass,
                    gender = draftGender,
                    level = 1,
                    vitality = StartingStats[(int)draftClass, 0],
                    intelligence = StartingStats[(int)draftClass, 1],
                    strength = StartingStats[(int)draftClass, 2],
                    dexterity = StartingStats[(int)draftClass, 3],
                };
                selectedSlot = createSlot;
                Save();
                ShowCharacterSelection();
            });
            back.onClick.AddListener(ShowCharacterSelection);
        }

        void ShowDeleteConfirmation(int slot)
        {
            Metin2CharacterData character = saveData.characters[slot];
            if (character == null) return;

            RectTransform shade = CreateRect(screenRoot, "Confirmation Shade", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.72f);
            RectTransform dialog = CreatePanel(shade, "Delete Confirmation", new Vector2(337f, -266f), new Vector2(350f, 190f));
            CreateText(dialog, "KARAKTERİ SİL", 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(18f, -16f), new Vector2(314f, 34f), new Color(0.93f, 0.55f, 0.32f));
            CreateText(dialog, character.characterName + " adlı karakter kalıcı olarak silinsin mi?", 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(32f, -62f), new Vector2(286f, 54f), Color.white);
            Text confirmLabel;
            Button confirm = CreateButton(dialog, "Evet, Sil", new Vector2(42f, -133f), new Vector2(126f, 36f), out confirmLabel);
            Text cancelLabel;
            Button cancel = CreateButton(dialog, "Vazgeç", new Vector2(182f, -133f), new Vector2(126f, 36f), out cancelLabel, true);
            confirm.onClick.AddListener(() =>
            {
                saveData.characters[slot] = null;
                Save();
                ShowCharacterSelection();
            });
            cancel.onClick.AddListener(() => Destroy(shade.gameObject));
        }

        void ShowLoading(Metin2CharacterData character)
        {
            if (TryShowEditableScreen("Loading Screen", out RectTransform editableRoot))
            {
                BindEditableLoading(editableRoot, character);
                return;
            }
            int loadingCount = config.loadingBackgrounds != null ? config.loadingBackgrounds.Length : 0;
            int imageIndex = loadingCount > 0 ? Mathf.Clamp((int)character.characterClass, 0, loadingCount - 1) : 0;
            Texture2D background = loadingCount > 0
                ? config.loadingBackgrounds[imageIndex]
                : config.selectionBackground;
            RectTransform root = BeginScreen("Loading Screen", background);

            RectTransform footer = CreatePanel(root, "Loading Footer", new Vector2(112f, 26f), new Vector2(800f, 104f), new Color(0.015f, 0.015f, 0.02f, 0.88f));
            AnchorBottomLeft(footer);
            Text title = CreateText(footer, "OYUN DÜNYASI YÜKLENİYOR", 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(24f, -12f), new Vector2(430f, 30f), new Color(0.96f, 0.78f, 0.40f));
            Text status = CreateText(footer, "Hazırlanıyor...", 13, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(450f, -12f), new Vector2(326f, 30f), new Color(0.84f, 0.82f, 0.77f));
            Image gaugeBack = CreateImage(footer, "Gauge Back", null, new Vector2(24f, -57f), new Vector2(752f, 18f), new Color(0.08f, 0.08f, 0.09f, 0.96f));
            Outline outline = gaugeBack.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.56f, 0.42f, 0.20f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            Image gaugeFill = CreateImage(gaugeBack.rectTransform, "Gauge Fill", null, Vector2.zero, Vector2.zero,
                new Color(0.80f, 0.38f, 0.08f, 1f));
            RectTransform fillRect = gaugeFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            if (authoringLayout)
            {
                status.text = "Harita hazırlanıyor...";
                SetProgress(fillRect, 0.65f);
            }
            else loadingRoutine = StartCoroutine(LoadGame(character, status, fillRect, footer));
        }

        void BindEditableLogin(RectTransform root)
        {
            RectTransform panel = FindRect(root, "Login Panel");
            List<InputField> inputs = root.GetComponentsInChildren<InputField>(true).ToList();
            InputField account = FindNamed<InputField>(root, "Hesap adı") ?? inputs.FirstOrDefault();
            InputField password = FindNamed<InputField>(root, "Şifre") ?? inputs.Skip(1).FirstOrDefault();
            List<Button> buttons = panel != null ? DirectComponents<Button>(panel) : new List<Button>();
            Button server = FindNamed<Button>(root, "Metin3 Button") ?? buttons.ElementAtOrDefault(0);
            Button channel = FindNamed<Button>(root, "CH1 Button") ?? buttons.ElementAtOrDefault(1);
            Button login = FindNamed<Button>(root, "Giriş Button") ?? buttons.ElementAtOrDefault(2);
            Button quit = FindNamed<Button>(root, "Çıkış Button") ?? buttons.ElementAtOrDefault(3);
            Text serverLabel = ButtonLabel(server);
            Text channelLabel = ButtonLabel(channel);
            Text status = panel != null
                ? DirectComponents<Text>(panel).LastOrDefault(item => string.IsNullOrEmpty(item.text))
                : null;

            if (account != null) account.text = saveData.accountId ?? string.Empty;
            string firstServer = serverLabel != null && !string.IsNullOrWhiteSpace(serverLabel.text)
                ? serverLabel.text
                : servers[0];
            string firstChannel = channelLabel != null && !string.IsNullOrWhiteSpace(channelLabel.text)
                ? channelLabel.text
                : channels[0];
            if (server != null)
            {
                server.onClick.RemoveAllListeners();
                server.onClick.AddListener(() =>
                {
                    serverIndex = (serverIndex + 1) % servers.Length;
                    if (serverLabel != null) serverLabel.text = serverIndex == 0 ? firstServer : servers[serverIndex];
                });
            }
            if (channel != null)
            {
                channel.onClick.RemoveAllListeners();
                channel.onClick.AddListener(() =>
                {
                    channelIndex = (channelIndex + 1) % channels.Length;
                    if (channelLabel != null) channelLabel.text = channelIndex == 0 ? firstChannel : channels[channelIndex];
                });
            }
            if (login != null)
            {
                login.onClick.RemoveAllListeners();
                login.onClick.AddListener(() =>
                {
                    string id = account != null ? account.text.Trim() : string.Empty;
                    if (id.Length < 2 || password == null || string.IsNullOrEmpty(password.text))
                    {
                        if (status != null) status.text = "Hesap adı ve şifreyi gir.";
                        return;
                    }
                    LoadAccount(id);
                    Save();
                    if (saveData.empire == Metin2Empire.None) ShowEmpireSelection();
                    else ShowCharacterSelection();
                });
            }
            if (password != null)
            {
                password.onEndEdit.RemoveAllListeners();
                password.onEndEdit.AddListener(_ =>
                {
                    if (!string.IsNullOrEmpty(password.text) && login != null) login.onClick.Invoke();
                });
            }
            if (quit != null)
            {
                quit.onClick.RemoveAllListeners();
                quit.onClick.AddListener(QuitApplication);
            }
        }

        void BindEditableEmpire(RectTransform root)
        {
            draftEmpire = saveData.empire != Metin2Empire.None ? saveData.empire : draftEmpire;
            RectTransform panel = FindRect(root, "Empire Map Window");
            List<Text> texts = panel != null ? DirectComponents<Text>(panel) : new List<Text>();
            if (texts.Count > 1)
            {
                texts[1].text = EmpireNames[(int)draftEmpire].ToUpperInvariant();
                texts[1].color = EmpireColors[(int)draftEmpire];
            }
            if (texts.Count > 2) texts[2].text = EmpireDescription(draftEmpire);

            BindEmpireChoice(root, Metin2Empire.Chunjo);
            BindEmpireChoice(root, Metin2Empire.Jinno);
            BindEmpireChoice(root, Metin2Empire.Shinsoo);

            Button confirm = FindNamed<Button>(root, "Bu Bayrağı Seç Button");
            if (confirm != null)
            {
                confirm.onClick.RemoveAllListeners();
                confirm.onClick.AddListener(() =>
                {
                    saveData.empire = draftEmpire;
                    Save();
                    ShowCharacterSelection();
                });
            }
            Button back = FindNamed<Button>(root, "Geri Button");
            if (back != null)
            {
                back.onClick.RemoveAllListeners();
                back.onClick.AddListener(ShowLogin);
            }
        }

        void BindEmpireChoice(Transform root, Metin2Empire empire)
        {
            Button button = FindNamed<Button>(root, EmpireNames[(int)empire] + " Flag Selection");
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                draftEmpire = empire;
                ShowEmpireSelection();
            });
        }

        void BindEditableCharacterSelection(RectTransform root)
        {
            saveData.EnsureSlots();
            if (selectedSlot < 0 || selectedSlot >= saveData.characters.Length || saveData.characters[selectedSlot] == null)
                selectedSlot = FirstOccupiedSlot();
            Metin2CharacterData selected = selectedSlot >= 0 ? saveData.characters[selectedSlot] : null;

            RectTransform listPanel = FindRect(root, "Saved Characters");
            RectTransform infoPanel = FindRect(root, "Selected Character Information");
            RectTransform previewRect = FindRect(root, "Selected Character FBX Preview");
            List<Text> listTexts = listPanel != null ? DirectComponents<Text>(listPanel) : new List<Text>();
            if (listTexts.Count > 1) listTexts[1].text = saveData.accountId;

            if (listPanel != null)
            {
                foreach (Transform child in listPanel.Cast<Transform>().ToArray())
                    if (child.name.StartsWith("Runtime Character Slot ", StringComparison.Ordinal))
                    {
                        child.gameObject.SetActive(false);
                        Destroy(child.gameObject);
                    }

                Button changeEmpire = FindNamed<Button>(listPanel, "Bayrak Seçimi Button");
                Button exit = FindNamed<Button>(listPanel, "Hesaptan Çık Button");
                Button newCharacter = FindNamed<Button>(listPanel, "+  Yeni Karakter Button");
                Button slotTemplate = DirectComponents<Button>(listPanel).FirstOrDefault(button =>
                    button != changeEmpire && button != exit && button != newCharacter &&
                    !button.name.StartsWith("Runtime Character Slot ", StringComparison.Ordinal));

                if (slotTemplate != null && newCharacter != null && !editableCharacterListCached)
                {
                    editableCharacterSlotBase = slotTemplate.GetComponent<RectTransform>().anchoredPosition;
                    editableNewCharacterBase = newCharacter.GetComponent<RectTransform>().anchoredPosition;
                    editableCharacterListCached = true;
                }

                int visibleRow = 0;
                for (int slot = 0; slot < saveData.characters.Length; slot++)
                {
                    Metin2CharacterData character = saveData.characters[slot];
                    if (character == null || slotTemplate == null) continue;
                    int captured = slot;
                    Button row = visibleRow == 0
                        ? slotTemplate
                        : Instantiate(slotTemplate, listPanel, false);
                    if (visibleRow > 0) row.name = "Runtime Character Slot " + visibleRow;
                    row.gameObject.SetActive(true);
                    RectTransform rowRect = row.GetComponent<RectTransform>();
                    rowRect.anchoredPosition = editableCharacterSlotBase + new Vector2(0f, -94f * visibleRow);
                    Text label = ButtonLabel(row);
                    if (label != null)
                    {
                        label.text = character.characterName + "\n" + ClassNames[(int)character.characterClass] +
                            "  •  Sv. " + character.level;
                    }
                    row.onClick.RemoveAllListeners();
                    row.onClick.AddListener(() =>
                    {
                        selectedSlot = captured;
                        ShowCharacterSelection();
                    });
                    visibleRow++;
                }
                if (slotTemplate != null && visibleRow == 0) slotTemplate.gameObject.SetActive(false);

                int emptySlot = Array.FindIndex(saveData.characters, character => character == null);
                if (newCharacter != null)
                {
                    newCharacter.gameObject.SetActive(emptySlot >= 0);
                    if (emptySlot >= 0)
                    {
                        Vector2 rowStep = editableNewCharacterBase - editableCharacterSlotBase;
                        newCharacter.GetComponent<RectTransform>().anchoredPosition = editableCharacterSlotBase + rowStep * visibleRow;
                        newCharacter.onClick.RemoveAllListeners();
                        newCharacter.onClick.AddListener(() => BeginCreate(emptySlot));
                    }
                }
                if (changeEmpire != null)
                {
                    changeEmpire.onClick.RemoveAllListeners();
                    changeEmpire.onClick.AddListener(ShowEmpireSelection);
                }
                if (exit != null)
                {
                    exit.onClick.RemoveAllListeners();
                    exit.onClick.AddListener(ShowLogin);
                }
            }

            List<Text> infoTexts = infoPanel != null ? DirectComponents<Text>(infoPanel) : new List<Text>();
            Button play = FindNamed<Button>(infoPanel, "Oyuna Başla Button");
            Button delete = FindNamed<Button>(infoPanel, "Karakteri Sil Button");
            if (selected == null)
            {
                SetDirectText(infoTexts, 0, "KARAKTER YOK");
                SetDirectText(infoTexts, 1, "Sol taraftaki Yeni Karakter düğmesiyle ilk karakterini oluştur.");
                for (int index = 2; index < infoTexts.Count; index++) infoTexts[index].gameObject.SetActive(false);
                if (play != null) play.gameObject.SetActive(false);
                if (delete != null) delete.gameObject.SetActive(false);
                ConfigureEditablePreview(previewRect, null);
                return;
            }

            foreach (Text text in infoTexts) text.gameObject.SetActive(true);
            SetDirectText(infoTexts, 0, selected.characterName);
            SetDirectText(infoTexts, 1, EmpireNames[(int)saveData.empire] + " • " +
                (selected.gender == Metin2Gender.Male ? "Erkek" : "Kadın"));
            SetDirectText(infoTexts, 3, ClassNames[(int)selected.characterClass]);
            SetDirectText(infoTexts, 5, selected.level.ToString());
            SetDirectText(infoTexts, 7, FormatPlayTime(selected.playMinutes));
            SetDirectText(infoTexts, 9, selected.vitality.ToString());
            SetDirectText(infoTexts, 11, selected.intelligence.ToString());
            SetDirectText(infoTexts, 13, selected.strength.ToString());
            SetDirectText(infoTexts, 15, selected.dexterity.ToString());
            if (play != null)
            {
                play.gameObject.SetActive(true);
                play.onClick.RemoveAllListeners();
                play.onClick.AddListener(() => ShowLoading(saveData.characters[selectedSlot]));
            }
            if (delete != null)
            {
                delete.gameObject.SetActive(true);
                delete.onClick.RemoveAllListeners();
                delete.onClick.AddListener(() => ShowDeleteConfirmation(selectedSlot));
            }
            ConfigureEditablePreview(previewRect, selected);
        }

        void BindEditableCharacterCreation(RectTransform root)
        {
            RectTransform classPanel = FindRect(root, "Character Class Selection");
            RectTransform panel = FindRect(root, "Character Registration");
            RectTransform previewRect = FindRect(root, "Creation Character FBX Preview");
            List<Button> classButtons = classPanel != null ? DirectComponents<Button>(classPanel) : new List<Button>();
            for (int index = 0; index < classButtons.Count && index < ClassNames.Length; index++)
            {
                int captured = index;
                classButtons[index].onClick.RemoveAllListeners();
                classButtons[index].onClick.AddListener(() =>
                {
                    draftClass = (Metin2CharacterClass)captured;
                    ShowCharacterCreation();
                });
            }
            List<Text> classTexts = classPanel != null ? DirectComponents<Text>(classPanel) : new List<Text>();
            if (classTexts.Count > 1) classTexts[1].text = ClassDescriptions[(int)draftClass];

            List<Text> panelTexts = panel != null ? DirectComponents<Text>(panel) : new List<Text>();
            SetDirectText(panelTexts, 1, ClassNames[(int)draftClass].ToUpperInvariant());
            SetDirectText(panelTexts, 3, string.Empty);
            UpdateEditableStat(panel, "VIT", StartingStats[(int)draftClass, 0]);
            UpdateEditableStat(panel, "INT", StartingStats[(int)draftClass, 1]);
            UpdateEditableStat(panel, "STR", StartingStats[(int)draftClass, 2]);
            UpdateEditableStat(panel, "DEX", StartingStats[(int)draftClass, 3]);

            Button male = FindNamed<Button>(panel, "Erkek Button");
            Button female = FindNamed<Button>(panel, "Kadın Button");
            if (male != null)
            {
                male.onClick.RemoveAllListeners();
                male.onClick.AddListener(() => { draftGender = Metin2Gender.Male; ShowCharacterCreation(); });
            }
            if (female != null)
            {
                female.onClick.RemoveAllListeners();
                female.onClick.AddListener(() => { draftGender = Metin2Gender.Female; ShowCharacterCreation(); });
            }

            InputField nameInput = FindNamed<InputField>(panel, "Karakter adı");
            if (nameInput != null)
            {
                nameInput.characterLimit = 12;
                nameInput.text = draftName;
                nameInput.onValueChanged.RemoveAllListeners();
                nameInput.onValueChanged.AddListener(value => draftName = value);
            }
            Text status = panelTexts.ElementAtOrDefault(3);
            Button create = FindNamed<Button>(panel, "Kaydet Button");
            Button back = FindNamed<Button>(panel, "Geri Button");
            if (create != null)
            {
                create.onClick.RemoveAllListeners();
                create.onClick.AddListener(() =>
                {
                    string candidate = draftName.Trim();
                    if (candidate.Length < 2)
                    {
                        if (status != null) status.text = "Karakter adı en az 2 harf olmalı.";
                        return;
                    }
                    if (saveData.characters.Any(item => item != null &&
                        string.Equals(item.characterName, candidate, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (status != null) status.text = "Bu isimde bir karakter zaten var.";
                        return;
                    }
                    saveData.characters[createSlot] = new Metin2CharacterData
                    {
                        characterName = candidate,
                        characterClass = draftClass,
                        gender = draftGender,
                        level = 1,
                        vitality = StartingStats[(int)draftClass, 0],
                        intelligence = StartingStats[(int)draftClass, 1],
                        strength = StartingStats[(int)draftClass, 2],
                        dexterity = StartingStats[(int)draftClass, 3],
                    };
                    selectedSlot = createSlot;
                    Save();
                    ShowCharacterSelection();
                });
            }
            if (back != null)
            {
                back.onClick.RemoveAllListeners();
                back.onClick.AddListener(ShowCharacterSelection);
            }
            ConfigureEditablePreview(previewRect, new Metin2CharacterData
            {
                characterClass = draftClass,
                gender = draftGender,
            });
        }

        void BindEditableLoading(RectTransform root, Metin2CharacterData character)
        {
            int loadingCount = config.loadingBackgrounds != null ? config.loadingBackgrounds.Length : 0;
            if (loadingCount > 0)
            {
                int imageIndex = Mathf.Clamp((int)character.characterClass, 0, loadingCount - 1);
                RawImage background = root.GetComponent<RawImage>();
                if (background != null) background.texture = config.loadingBackgrounds[imageIndex];
            }
            RectTransform footer = FindRect(root, "Loading Footer");
            List<Text> texts = footer != null ? DirectComponents<Text>(footer) : new List<Text>();
            Text status = texts.ElementAtOrDefault(1);
            RectTransform fill = FindRect(root, "Gauge Fill");
            if (status != null && fill != null)
                loadingRoutine = StartCoroutine(LoadGame(character, status, fill, footer));
        }

        void ConfigureEditablePreview(RectTransform previewRect, Metin2CharacterData character)
        {
            if (previewRect == null) return;
            Image placeholderImage = previewRect.GetComponent<Image>();
            if (placeholderImage != null) placeholderImage.enabled = false;
            foreach (Text text in previewRect.GetComponentsInChildren<Text>(true))
                if (text.text.Contains("FBX ALANI")) text.gameObject.SetActive(false);
            RawImage target = FindNamed<RawImage>(previewRect, "Runtime Character Preview");
            if (target == null)
            {
                GameObject targetObject = new GameObject("Runtime Character Preview", typeof(RectTransform), typeof(RawImage));
                RectTransform targetRect = targetObject.GetComponent<RectTransform>();
                targetRect.SetParent(previewRect, false);
                targetRect.anchorMin = Vector2.zero;
                targetRect.anchorMax = Vector2.one;
                targetRect.pivot = new Vector2(0.5f, 0.5f);
                targetRect.anchoredPosition = Vector2.zero;
                targetRect.sizeDelta = Vector2.zero;
                targetRect.SetAsFirstSibling();
                target = targetObject.GetComponent<RawImage>();
            }
            target.raycastTarget = false;
            Metin2CharacterPreview preview = previewRect.GetComponent<Metin2CharacterPreview>() ??
                previewRect.gameObject.AddComponent<Metin2CharacterPreview>();
            preview.Initialize(target);
            if (character == null) preview.Hide();
            else preview.Show(config, character.characterClass, character.gender);
        }

        void UpdateEditableStat(Transform panel, string label, int value)
        {
            if (panel == null) return;
            List<Text> texts = DirectComponents<Text>(panel);
            int labelIndex = texts.FindIndex(text => text.text == label);
            if (labelIndex >= 0 && labelIndex + 1 < texts.Count) texts[labelIndex + 1].text = value.ToString();
            RectTransform background = FindRect(panel, label + " Background");
            RectTransform fill = background != null ? FindRect(background, "Fill") : null;
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp01(value / 8f), 1f);
        }

        static void SetDirectText(List<Text> texts, int index, string value)
        {
            if (index >= 0 && index < texts.Count) texts[index].text = value;
        }

        static List<T> DirectComponents<T>(Transform parent) where T : Component
        {
            List<T> result = new List<T>();
            if (parent == null) return result;
            foreach (Transform child in parent)
            {
                T component = child.GetComponent<T>();
                if (component != null) result.Add(component);
            }
            return result;
        }

        static T FindNamed<T>(Transform root, string name) where T : Component
        {
            if (root == null) return null;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }

        static RectTransform FindRect(Transform root, string name)
        {
            if (root == null) return null;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        static Text ButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }

        void CreatePreviewPlaceholder(RectTransform previewRect, string label)
        {
            Image background = previewRect.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.16f);
            background.raycastTarget = false;
            Outline outline = previewRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.52f, 0.22f, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
            Text placeholder = CreateText(previewRect, label + "\n(Oyunda gerçek model gösterilir)", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Color(0.90f, 0.78f, 0.52f), false);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            placeholder.rectTransform.anchoredPosition = Vector2.zero;
            placeholder.rectTransform.sizeDelta = Vector2.zero;
        }

        public void BuildEditableHierarchy()
        {
            if (Application.isPlaying || config == null) return;

            Transform existing = transform.Find(EditableLayoutName);
            if (existing != null) DestroyImmediate(existing.gameObject);

            authoringLayout = true;
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            saveData = new Metin2FrontendSaveData();
            saveData.EnsureSlots();
            createSlot = 1;
            draftEmpire = Metin2Empire.Shinsoo;
            draftClass = Metin2CharacterClass.Warrior;
            draftGender = Metin2Gender.Male;
            draftName = string.Empty;

            CreateCanvas(EditableLayoutName);
            List<RectTransform> screens = new List<RectTransform>();
            ShowLogin();
            screens.Add(screenRoot);

            saveData.accountId = "Örnek Hesap";
            saveData.empire = Metin2Empire.Shinsoo;
            ShowEmpireSelection();
            screens.Add(screenRoot);

            saveData.characters[0] = new Metin2CharacterData
            {
                characterName = "Alp",
                characterClass = Metin2CharacterClass.Warrior,
                gender = Metin2Gender.Male,
                level = 1,
                vitality = 4,
                intelligence = 3,
                strength = 6,
                dexterity = 3,
            };
            selectedSlot = 0;
            ShowCharacterSelection();
            screens.Add(screenRoot);

            ShowCharacterCreation();
            screens.Add(screenRoot);
            ShowLoading(saveData.characters[0]);
            screens.Add(screenRoot);

            for (int index = 0; index < screens.Count; index++)
                screens[index].gameObject.SetActive(index == 0);

            canvas = null;
            screenRoot = null;
            authoringLayout = false;
            saveData = null;
            Debug.Log("[Metin2 Frontend] Editable hierarchy generated with " + screens.Count + " screens.", this);
        }

        IEnumerator LoadGame(Metin2CharacterData character, Text status, RectTransform fill, RectTransform footer)
        {
            Metin2Dev.Gameplay.Metin2GameplaySession.Select(character, saveData.empire,
                config.GetRacePrefab(character.characterClass, character.gender),
                config.GetHairPrefab(character.characterClass, character.gender),
                config.GetBodyTexture(character.characterClass, character.gender),
                config.GetFaceTexture(character.characterClass, character.gender),
                config.GetHairTexture(character.characterClass, character.gender));
            string sceneName = config.GetScene(saveData.empire);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                string fallback = new[] { "metin2_map_b1", "metin2_map_c1" }
                    .FirstOrDefault(Application.CanStreamedLevelBeLoaded);
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    Debug.LogWarning("Starting map is unavailable: " + sceneName + ". Loading " + fallback + " instead.");
                    sceneName = fallback;
                }
            }
            float displayed = 0f;
            float warmup = 0f;
            while (warmup < 0.75f)
            {
                warmup += Time.unscaledDeltaTime;
                displayed = Mathf.Lerp(0f, 0.16f, warmup / 0.75f);
                SetProgress(fill, displayed);
                yield return null;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                while (displayed < 1f)
                {
                    displayed = Mathf.MoveTowards(displayed, 1f, Time.unscaledDeltaTime * 0.36f);
                    SetProgress(fill, displayed);
                    status.text = "Harita sahnesi bekleniyor: " + sceneName;
                    yield return null;
                }
                status.text = "Harita Build Settings içinde bulunamadı.";
                Text backLabel;
                Button back = CreateButton(footer, "Karakter Seçimine Dön", new Vector2(288f, -74f), new Vector2(224f, 28f), out backLabel, true);
                back.onClick.AddListener(ShowCharacterSelection);
                loadingRoutine = null;
                yield break;
            }

            status.text = sceneName + " hazırlanıyor...";
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                float target = 0.16f + Mathf.Clamp01(operation.progress / 0.9f) * 0.78f;
                displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * 0.55f);
                SetProgress(fill, displayed);
                status.text = "%" + Mathf.RoundToInt(displayed * 100f);
                yield return null;
            }
            while (displayed < 1f)
            {
                displayed = Mathf.MoveTowards(displayed, 1f, Time.unscaledDeltaTime * 0.55f);
                SetProgress(fill, displayed);
                status.text = "%" + Mathf.RoundToInt(displayed * 100f);
                yield return null;
            }
            character.playMinutes += 1;
            Save();
            yield return new WaitForSecondsRealtime(0.25f);
            operation.allowSceneActivation = true;
            yield return operation;
            Destroy(gameObject);
        }

        static void SetProgress(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        void CreateInfoRow(RectTransform panel, string label, string value, float y)
        {
            CreateText(panel, label, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(26f, -y), new Vector2(92f, 25f), new Color(0.77f, 0.72f, 0.64f));
            CreateText(panel, value, 15, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(116f, -y), new Vector2(148f, 25f), Color.white);
        }

        void CreateCompactInfo(RectTransform panel, string label, string value, float x, float width)
        {
            CreateText(panel, label, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(x, -5f), new Vector2(width, 18f), new Color(0.77f, 0.72f, 0.64f));
            CreateText(panel, value, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(x, -25f), new Vector2(width, 22f), Color.white);
        }

        void CreateStat(RectTransform panel, string label, int value, float y, Color color)
        {
            CreateText(panel, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(25f, -y), new Vector2(42f, 24f), new Color(0.80f, 0.77f, 0.70f));
            Image background = CreateImage(panel, label + " Gauge", null, new Vector2(70f, -y - 4f), new Vector2(170f, 14f), new Color(0.08f, 0.08f, 0.09f, 0.95f));
            Image fill = CreateImage(background.rectTransform, "Fill", null, Vector2.zero, Vector2.zero, color);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / 8f), 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = Vector2.zero;
            CreateText(panel, value.ToString(), 13, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(244f, -y), new Vector2(24f, 24f), Color.white);
        }

        void CreateCharacterStat(RectTransform panel, string label, int value, float y, Color color)
        {
            CreateText(panel, label, 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(22f, -y), new Vector2(38f, 22f), new Color(0.80f, 0.77f, 0.70f));
            Image background = CreateImage(panel, label + " Gauge", null, new Vector2(62f, -y - 4f),
                new Vector2(130f, 12f), new Color(0.08f, 0.08f, 0.09f, 0.95f));
            Image fill = CreateImage(background.rectTransform, "Fill", null, Vector2.zero, Vector2.zero, color);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / 8f), 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = Vector2.zero;
            CreateText(panel, value.ToString(), 12, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(195f, -y), new Vector2(27f, 22f), Color.white);
        }

        void CreateStatsStrip(RectTransform root, int vitality, int intelligence, int strength, int dexterity)
        {
            RectTransform strip = CreatePanel(root, "Character Stats", new Vector2(24f, 10f), new Vector2(976f, 52f),
                new Color(0.025f, 0.025f, 0.03f, 0.94f));
            AnchorBottomLeft(strip);
            CreateHorizontalStat(strip, "HP", vitality, 8f, new Color(0.70f, 0.14f, 0.10f));
            CreateHorizontalStat(strip, "SP", intelligence, 247f, new Color(0.65f, 0.26f, 0.68f));
            CreateHorizontalStat(strip, "STR", strength, 486f, new Color(0.75f, 0.43f, 0.10f));
            CreateHorizontalStat(strip, "DEX", dexterity, 725f, new Color(0.12f, 0.42f, 0.78f));
        }

        void CreateHorizontalStat(RectTransform panel, string label, int value, float x, Color color)
        {
            CreateText(panel, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(x + 8f, -5f), new Vector2(38f, 20f), new Color(0.80f, 0.77f, 0.70f));
            Image background = CreateImage(panel, label + " Gauge", null, new Vector2(x + 8f, -31f), new Vector2(214f, 10f),
                new Color(0.08f, 0.08f, 0.09f, 0.95f));
            Image fill = CreateImage(background.rectTransform, "Fill", null, Vector2.zero, Vector2.zero, color);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value / 8f), 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = Vector2.zero;
            CreateText(panel, value.ToString(), 13, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(x + 198f, -5f), new Vector2(24f, 20f), Color.white);
        }

        RectTransform CreatePanel(Transform parent, string name, Vector2 topLeft, Vector2 size)
        {
            return CreatePanel(parent, name, topLeft, size, new Color(0.025f, 0.025f, 0.03f, 0.86f));
        }

        RectTransform CreatePanel(Transform parent, string name, Vector2 topLeft, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), topLeft, size);

            if (config != null && config.inventoryBoardFrame != null && config.inventoryBoardCenter != null)
            {
                RectTransform centerRect = CreateRect(rect, "Original Board Center", Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Image center = centerRect.gameObject.AddComponent<Image>();
                center.sprite = config.inventoryBoardCenter;
                center.type = Image.Type.Tiled;
                center.color = Color.white;
                center.raycastTarget = false;

                RectTransform frameRect = CreateRect(rect, "Original Inventory Frame", Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Image frame = frameRect.gameObject.AddComponent<Image>();
                frame.sprite = config.inventoryBoardFrame;
                frame.type = Image.Type.Sliced;
                frame.fillCenter = false;
                frame.color = Color.white;
                frame.raycastTarget = false;
            }
            else
            {
                Image image = rect.gameObject.AddComponent<Image>();
                image.color = color;
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.58f, 0.43f, 0.21f, 0.94f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return rect;
        }

        Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 topLeft, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), topLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        Text CreateText(Transform parent, string value, int size, FontStyle style, TextAnchor alignment,
            Vector2 topLeft, Vector2 dimensions, Color color, bool topLeftAnchored = true)
        {
            Vector2 anchor = topLeftAnchored ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            Vector2 pivot = topLeftAnchored ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            RectTransform rect = CreateRect(parent, "Text", anchor, anchor, pivot, topLeft, dimensions);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = uiFont;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.90f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        Button CreateButton(Transform parent, string value, Vector2 topLeft, Vector2 size, out Text label, bool emphasized = false)
        {
            RectTransform rect = CreateRect(parent, value + " Button", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), topLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = emphasized ? new Color(0.50f, 0.19f, 0.055f, 0.98f) : new Color(0.12f, 0.105f, 0.09f, 0.97f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = emphasized ? new Color(0.94f, 0.61f, 0.20f, 0.95f) : new Color(0.57f, 0.43f, 0.24f, 0.90f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.08f, 0.82f, 1f);
            colors.pressedColor = new Color(0.72f, 0.60f, 0.45f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.72f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            label = CreateText(rect, value, 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Color(0.95f, 0.90f, 0.80f));
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = new Vector2(-8f, -4f);
            return button;
        }

        InputField CreateInput(Transform parent, string placeholderValue, bool password, Vector2 topLeft, Vector2 size)
        {
            RectTransform rect = CreateRect(parent, placeholderValue, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), topLeft, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.035f, 0.035f, 0.04f, 0.96f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.39f, 0.27f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.lineType = InputField.LineType.SingleLine;

            Text text = CreateText(rect, string.Empty, 15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(10f, -2f), new Vector2(size.x - 20f, size.y - 4f), Color.white);
            Text placeholder = CreateText(rect, placeholderValue, 14, FontStyle.Italic, TextAnchor.MiddleLeft,
                new Vector2(10f, -2f), new Vector2(size.x - 20f, size.y - 4f), new Color(0.58f, 0.56f, 0.52f));
            input.textComponent = text;
            input.placeholder = placeholder;
            input.caretColor = new Color(0.95f, 0.76f, 0.35f);
            input.selectionColor = new Color(0.58f, 0.31f, 0.10f, 0.75f);
            return input;
        }

        Sprite SpriteFor(Texture2D texture)
        {
            if (texture == null) return null;
            if (spriteCache.TryGetValue(texture, out Sprite cached) && cached != null) return cached;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name + " Runtime Sprite";
            spriteCache[texture] = sprite;
            return sprite;
        }

        RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
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

        static void AnchorBottomLeft(RectTransform rect)
        {
            Vector2 position = rect.anchoredPosition;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
        }

        static void AnchorBottomCenter(RectTransform rect)
        {
            Vector2 position = rect.anchoredPosition;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
        }

        static string FormatPlayTime(int minutes)
        {
            int hours = Mathf.Max(0, minutes) / 60;
            int remainder = Mathf.Max(0, minutes) % 60;
            return hours > 0 ? hours + " sa " + remainder + " dk" : remainder + " dk";
        }

        static string EmpireSymbol(Metin2Empire empire)
        {
            switch (empire)
            {
                case Metin2Empire.Shinsoo: return "朱";
                case Metin2Empire.Chunjo: return "黃";
                case Metin2Empire.Jinno: return "靑";
                default: return "";
            }
        }

        static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        IEnumerator CapturePreviewFromCommandLine()
        {
            string phase = CommandLineValue("-metin2FrontendPhase", "login").ToLowerInvariant();
            string path = CommandLineValue("-metin2FrontendCapturePath", Path.Combine(Application.persistentDataPath, "Metin2Frontend.png"));
            yield return null;
            switch (phase)
            {
                case "empire":
                    ShowEmpireSelection();
                    break;
                case "create":
                    saveData.empire = Metin2Empire.Shinsoo;
                    createSlot = 0;
                    draftClass = Metin2CharacterClass.Warrior;
                    draftGender = Metin2Gender.Male;
                    draftName = "Alp";
                    ShowCharacterCreation();
                    break;
                case "select":
                    saveData.empire = Metin2Empire.Shinsoo;
                    saveData.characters[0] = new Metin2CharacterData
                    {
                        characterName = "Alp",
                        characterClass = Metin2CharacterClass.Warrior,
                        gender = Metin2Gender.Male,
                        level = 1,
                        vitality = 4,
                        intelligence = 3,
                        strength = 6,
                        dexterity = 3,
                    };
                    selectedSlot = 0;
                    ShowCharacterSelection();
                    break;
                case "loading":
                    saveData.empire = Metin2Empire.Shinsoo;
                    ShowLoading(new Metin2CharacterData
                    {
                        characterName = "Alp",
                        characterClass = Metin2CharacterClass.Warrior,
                        gender = Metin2Gender.Male,
                        level = 1,
                        vitality = 4,
                        intelligence = 3,
                        strength = 6,
                        dexterity = 3,
                    });
                    break;
            }
            for (int frame = 0; frame < 20; frame++) yield return new WaitForEndOfFrame();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(path);
            float timeout = Time.realtimeSinceStartup + 5f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < timeout) yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        static string CommandLineValue(string key, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return fallback;
        }
#endif
    }
}

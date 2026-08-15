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
        const string SaveKey = "Metin2.Frontend.Save.v1";
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
        Metin2CharacterClass draftClass = Metin2CharacterClass.Warrior;
        Metin2Gender draftGender = Metin2Gender.Male;
        string draftName = string.Empty;
        Coroutine loadingRoutine;

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

            DontDestroyOnLoad(gameObject);
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadSave();
            CreateEventSystem();
            CreateCanvas();
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

        void LoadSave()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            saveData = string.IsNullOrWhiteSpace(json)
                ? new Metin2FrontendSaveData()
                : JsonUtility.FromJson<Metin2FrontendSaveData>(json) ?? new Metin2FrontendSaveData();
            saveData.EnsureSlots();
        }

        void Save()
        {
            saveData.EnsureSlots();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }

        void CreateEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(transform, false);
            InputSystemUIInputModule inputModule = eventObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        void CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Metin2 Frontend Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        RectTransform BeginScreen(string name, Texture2D background)
        {
            if (loadingRoutine != null)
            {
                StopCoroutine(loadingRoutine);
                loadingRoutine = null;
            }
            if (screenRoot != null)
            {
                screenRoot.gameObject.SetActive(false);
                Destroy(screenRoot.gameObject);
            }

            screenRoot = CreateRect(canvas.transform, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image backdrop = screenRoot.gameObject.AddComponent<Image>();
            backdrop.sprite = SpriteFor(background);
            backdrop.color = Color.white;
            backdrop.raycastTarget = false;
            return screenRoot;
        }

        void ShowLogin()
        {
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
                saveData.accountId = id;
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
            RectTransform root = BeginScreen("Empire Selection", config.serverBackground != null ? config.serverBackground : config.selectionBackground);
            CreateText(root, "İMPARATORLUĞUNU SEÇ", 30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, -58f), new Vector2(700f, 54f), new Color(1f, 0.82f, 0.43f), false);
            CreateText(root, "Karakterlerin bu imparatorluğun başlangıç köyünde doğacak.", 16, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, -107f), new Vector2(720f, 32f), new Color(0.92f, 0.88f, 0.78f), false);

            CreateEmpireCard(root, Metin2Empire.Shinsoo, new Vector2(95f, -210f),
                "Güneydeki kızıl imparatorluk. Ticaret ve dayanışmayla güç kazanır.");
            CreateEmpireCard(root, Metin2Empire.Chunjo, new Vector2(372f, -210f),
                "Batıdaki sarı imparatorluk. Ruhani öğretilere ve disipline bağlıdır.");
            CreateEmpireCard(root, Metin2Empire.Jinno, new Vector2(649f, -210f),
                "Doğudaki mavi imparatorluk. Askerî güç ve mücadeleyi her şeyden üstün tutar.");

            Text backLabel;
            Button back = CreateButton(root, "Geri", new Vector2(40f, 24f), new Vector2(110f, 36f), out backLabel);
            AnchorBottomLeft(back.GetComponent<RectTransform>());
            back.onClick.AddListener(ShowLogin);
        }

        void CreateEmpireCard(RectTransform parent, Metin2Empire empire, Vector2 topLeft, string description)
        {
            Color color = EmpireColors[(int)empire];
            RectTransform card = CreatePanel(parent, EmpireNames[(int)empire], topLeft, new Vector2(250f, 360f), new Color(0.025f, 0.025f, 0.03f, 0.90f));
            Image banner = CreateImage(card, "Empire Color", null, new Vector2(0f, -4f), new Vector2(250f, 88f), color);
            banner.raycastTarget = false;
            CreateText(card, EmpireNames[(int)empire].ToUpperInvariant(), 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(12f, -18f), new Vector2(226f, 48f), Color.white);
            CreateText(card, EmpireSymbol(empire), 62, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(20f, -116f), new Vector2(210f, 92f), new Color(color.r + 0.18f, color.g + 0.18f, color.b + 0.18f, 1f));
            CreateText(card, description, 15, FontStyle.Normal, TextAnchor.UpperCenter,
                new Vector2(24f, -220f), new Vector2(202f, 80f), new Color(0.88f, 0.84f, 0.76f));
            Text chooseLabel;
            Button choose = CreateButton(card, "Seç", new Vector2(48f, -311f), new Vector2(154f, 34f), out chooseLabel, true);
            choose.onClick.AddListener(() =>
            {
                saveData.empire = empire;
                Save();
                ShowCharacterSelection();
            });
        }

        void ShowCharacterSelection()
        {
            RectTransform root = BeginScreen("Character Selection", config.selectionBackground);
            saveData.EnsureSlots();
            selectedSlot = Mathf.Clamp(selectedSlot, 0, 3);
            Metin2CharacterData selected = saveData.characters[selectedSlot];

            RectTransform infoStrip = CreatePanel(root, "Character Information", new Vector2(24f, 126f), new Vector2(976f, 58f),
                new Color(0.025f, 0.025f, 0.03f, 0.94f));
            AnchorBottomLeft(infoStrip);
            Text status = CreateText(infoStrip, string.Empty, 11, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(704f, -35f), new Vector2(254f, 18f), new Color(0.96f, 0.72f, 0.46f));

            Text primaryLabel;
            Button primary = CreateButton(infoStrip, selected != null ? "Oyuna Başla" : "Karakter Oluştur",
                new Vector2(10f, -11f), new Vector2(148f, 36f), out primaryLabel, true);
            primary.onClick.AddListener(() =>
            {
                if (saveData.characters[selectedSlot] == null) BeginCreate(selectedSlot);
                else ShowLoading(saveData.characters[selectedSlot]);
            });

            if (selected != null)
            {
                CreateText(infoStrip, selected.characterName, 18, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(174f, -5f), new Vector2(146f, 25f), Color.white);
                CreateText(infoStrip, EmpireNames[(int)saveData.empire], 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(174f, -30f), new Vector2(146f, 17f), EmpireColors[(int)saveData.empire]);
                CreateCompactInfo(infoStrip, "Sınıf", ClassNames[(int)selected.characterClass], 330f, 120f);
                CreateCompactInfo(infoStrip, "Seviye", selected.level.ToString(), 450f, 86f);
                CreateCompactInfo(infoStrip, "Oynama", FormatPlayTime(selected.playMinutes), 536f, 128f);
            }
            else
            {
                CreateText(infoStrip, "BOŞ KARAKTER YUVASI", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(174f, -6f), new Vector2(210f, 24f), new Color(0.86f, 0.78f, 0.62f));
                CreateText(infoStrip, "Yeni bir karakter oluşturabilirsin.", 11, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(174f, -31f), new Vector2(300f, 17f), new Color(0.82f, 0.80f, 0.75f));
            }

            Text createLabel;
            Button create = CreateButton(infoStrip, "Yeni", new Vector2(704f, -7f), new Vector2(78f, 25f), out createLabel);
            Text deleteLabel;
            Button delete = CreateButton(infoStrip, "Sil", new Vector2(790f, -7f), new Vector2(78f, 25f), out deleteLabel);
            Text exitLabel;
            Button exit = CreateButton(infoStrip, "Geri", new Vector2(876f, -7f), new Vector2(78f, 25f), out exitLabel);
            create.onClick.AddListener(() =>
            {
                int empty = Array.FindIndex(saveData.characters, item => item == null);
                if (empty < 0) status.text = "Dört karakter yuvası da dolu.";
                else BeginCreate(empty);
            });
            delete.interactable = selected != null;
            delete.onClick.AddListener(() => ShowDeleteConfirmation(selectedSlot));
            exit.onClick.AddListener(ShowLogin);

            RectTransform previewRect = CreateRect(root, "Character Preview", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(355f, -35f), new Vector2(610f, 500f));
            RawImage rawImage = previewRect.gameObject.AddComponent<RawImage>();
            rawImage.raycastTarget = false;
            Metin2CharacterPreview preview = previewRect.gameObject.AddComponent<Metin2CharacterPreview>();
            preview.Initialize(rawImage);
            if (selected != null) preview.Show(config, selected.characterClass, selected.gender);

            RectTransform slotStrip = CreatePanel(root, "Character Slots", new Vector2(24f, 68f), new Vector2(976f, 52f),
                new Color(0.025f, 0.025f, 0.03f, 0.92f));
            AnchorBottomLeft(slotStrip);

            for (int slot = 0; slot < 4; slot++)
            {
                int captured = slot;
                Metin2CharacterData data = saveData.characters[slot];
                string text = data != null ? data.characterName + "\nSv. " + data.level : "Boş Yuva";
                Text slotLabel;
                Button slotButton = CreateButton(slotStrip, text, new Vector2(10f + slot * 241f, -6f), new Vector2(232f, 40f), out slotLabel,
                    slot == selectedSlot);
                slotLabel.fontSize = data != null ? 14 : 13;
                slotButton.onClick.AddListener(() => { selectedSlot = captured; ShowCharacterSelection(); });
            }
            CreateStatsStrip(root, selected != null ? selected.vitality : 0, selected != null ? selected.intelligence : 0,
                selected != null ? selected.strength : 0, selected != null ? selected.dexterity : 0);
            previewRect.SetAsFirstSibling();

            Text empireLabel;
            Button changeEmpire = CreateButton(root, "İmparatorluk", new Vector2(30f, -36f), new Vector2(130f, 34f), out empireLabel);
            changeEmpire.onClick.AddListener(ShowEmpireSelection);
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
            RectTransform root = BeginScreen("Character Creation", config.selectionBackground);
            RectTransform panel = CreatePanel(root, "Creation Panel", new Vector2(52f, -126f), new Vector2(330f, 410f));
            CreateText(panel, "KARAKTER OLUŞTUR", 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(16f, -14f), new Vector2(298f, 36f), new Color(0.97f, 0.78f, 0.38f));

            CreateText(panel, ClassNames[(int)draftClass].ToUpperInvariant(), 27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(16f, -60f), new Vector2(298f, 42f), Color.white);
            CreateText(panel, ClassDescriptions[(int)draftClass], 14, FontStyle.Normal, TextAnchor.UpperCenter,
                new Vector2(28f, -107f), new Vector2(274f, 76f), new Color(0.86f, 0.83f, 0.77f));

            CreateText(panel, "Cinsiyet", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(25f, -194f), new Vector2(80f, 28f), new Color(0.83f, 0.79f, 0.70f));
            Text maleLabel;
            Button male = CreateButton(panel, "Erkek", new Vector2(105f, -193f), new Vector2(92f, 30f), out maleLabel, draftGender == Metin2Gender.Male);
            Text femaleLabel;
            Button female = CreateButton(panel, "Kadın", new Vector2(207f, -193f), new Vector2(92f, 30f), out femaleLabel, draftGender == Metin2Gender.Female);
            male.onClick.AddListener(() => { draftGender = Metin2Gender.Male; ShowCharacterCreation(); });
            female.onClick.AddListener(() => { draftGender = Metin2Gender.Female; ShowCharacterCreation(); });

            InputField nameInput = CreateInput(panel, "Karakter adı", false, new Vector2(25f, -240f), new Vector2(274f, 36f));
            nameInput.characterLimit = 12;
            nameInput.text = draftName;
            nameInput.onValueChanged.AddListener(value => draftName = value);
            Text status = CreateText(panel, string.Empty, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(24f, -282f), new Vector2(282f, 28f), new Color(0.96f, 0.72f, 0.46f));

            Text createLabel;
            Button create = CreateButton(panel, "Oluştur", new Vector2(25f, -318f), new Vector2(178f, 38f), out createLabel, true);
            Text backLabel;
            Button back = CreateButton(panel, "Geri", new Vector2(211f, -318f), new Vector2(88f, 38f), out backLabel);
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

            RectTransform previewRect = CreateRect(root, "Creation Preview", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(355f, -30f), new Vector2(610f, 495f));
            RawImage rawImage = previewRect.gameObject.AddComponent<RawImage>();
            rawImage.raycastTarget = false;
            Metin2CharacterPreview preview = previewRect.gameObject.AddComponent<Metin2CharacterPreview>();
            preview.Initialize(rawImage);
            preview.Show(config, draftClass, draftGender);

            RectTransform classStrip = CreatePanel(root, "Character Classes", new Vector2(390f, 94f), new Vector2(610f, 68f),
                new Color(0.025f, 0.025f, 0.03f, 0.92f));
            AnchorBottomLeft(classStrip);
            for (int index = 0; index < ClassNames.Length; index++)
            {
                int captured = index;
                Text classLabel;
                Button classButton = CreateButton(classStrip, ClassNames[index], new Vector2(13f + index * 148f, -10f),
                    new Vector2(140f, 46f), out classLabel, index == (int)draftClass);
                classButton.onClick.AddListener(() => { draftClass = (Metin2CharacterClass)captured; ShowCharacterCreation(); });
            }
            CreateStatsStrip(root, StartingStats[(int)draftClass, 0], StartingStats[(int)draftClass, 1],
                StartingStats[(int)draftClass, 2], StartingStats[(int)draftClass, 3]);
            previewRect.SetAsFirstSibling();
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
            loadingRoutine = StartCoroutine(LoadGame(character, status, fillRect, footer));
        }

        IEnumerator LoadGame(Metin2CharacterData character, Text status, RectTransform fill, RectTransform footer)
        {
            Metin2Dev.Gameplay.Metin2GameplaySession.Select(character, saveData.empire);
            string sceneName = config.GetScene(saveData.empire);
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

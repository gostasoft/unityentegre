using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Metin2Dev.Gameplay;

/// <summary>
/// Metin2 TaskBar runtime davranisi.
/// - TaskBar Character / Inventory / Messenger / System butonlarini calistirir.
/// - I = Inventory
/// - C = Character STATUS
/// - V = Character SKILL
/// - N = Character QUEST
/// - ESC = acik pencereleri kapatir.
///
/// Inventory yerlesimi orijinal inventorywindow.py olculerine gore:
/// 176x565, 5x9 = 45 slot/sayfa, 4 inventory page, equipment bolumu.
/// </summary>
public class Metin2TaskBarRuntime : MonoBehaviour
{
    public static Metin2TaskBarRuntime Instance { get; private set; }

    private const float WINDOW_SCALE = 1.7f;

    private const string RES_PUBLIC =
        "Metin2UI/Windows/game/public/";

    private const string RES_WINDOWS =
        "Metin2UI/Windows/game/windows/";

    // Assets/Metin2Original/UI/pattern altindaki gercek Metin2 pattern sprite'lari
    // Resources altinda degil, bu yuzden runtime icin bunlari Resources'a kopyalaman gerekecek:
    // Assets/Resources/Metin2UI/Pattern/
    private const string RES_PATTERN =
        "Metin2UI/Pattern/";

    private Canvas canvas;
    private Font font;

    private RectTransform inventoryWindow;
    private RectTransform characterWindow;
    private RectTransform messengerWindow;
    private RectTransform systemWindow;

    private int inventoryPage = 0;

    private readonly Button[] inventoryPageButtons =
        new Button[4];
    private readonly RawImage[] inventoryItemIcons = new RawImage[45];
    private readonly Text[] inventoryItemCounts = new Text[45];
    private readonly System.Collections.Generic.Dictionary<Metin2EquipmentSlot, RawImage> equipmentItemIcons =
        new System.Collections.Generic.Dictionary<Metin2EquipmentSlot, RawImage>();
    private CharacterPage characterPage = CharacterPage.Status;

    private Button inventoryTaskButton;
    private Button characterTaskButton;
    private Button messengerTaskButton;
    private Button systemTaskButton;

    private enum CharacterPage
    {
        Status,
        Skill,
        Emoticon,
        Quest
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindFirstObjectByType<Metin2TaskBarRuntime>() != null)
            return;

        GameObject root = GameObject.Find("Metin2_Original_TaskBar");

        if (root == null)
        {
            Debug.LogWarning(
                "[Metin2 UI] Metin2_Original_TaskBar bulunamadi. " +
                "Once Editor TaskBar Builder ile TaskBar'i kur."
            );
            return;
        }

        root.AddComponent<Metin2TaskBarRuntime>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );

        canvas = GetComponent<Canvas>();

        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>();

        if (canvas == null)
        {
            Debug.LogError(
                "[Metin2 UI] Canvas bulunamadi."
            );
            enabled = false;
            return;
        }

        EnsureEventSystem();
        BindTaskBarButtons();

        BuildInventoryWindow();
        BuildCharacterWindow();
        BuildMessengerWindow();
        BuildSystemWindow();

        CloseAllWindows();
    }

    private void Update()
    {
        HandleKeyboard();
    }

    private void OnEnable()
    {
        Metin2InventoryService.Changed += RefreshInventory;
        Metin2InventoryService.EquippedChanged += RefreshInventory;
    }

    private void OnDisable()
    {
        Metin2InventoryService.Changed -= RefreshInventory;
        Metin2InventoryService.EquippedChanged -= RefreshInventory;
    }

    // ============================================================
    // INPUT
    // ============================================================

    private void HandleKeyboard()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.iKey.wasPressedThisFrame)
            ToggleInventory();

        if (Keyboard.current.cKey.wasPressedThisFrame)
            ToggleCharacter(CharacterPage.Status);

        if (Keyboard.current.vKey.wasPressedThisFrame)
            ToggleCharacter(CharacterPage.Skill);

        if (Keyboard.current.nKey.wasPressedThisFrame)
            ToggleCharacter(CharacterPage.Quest);

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseAllWindows();
    }

    // ============================================================
    // TASKBAR BUTTONS
    // ============================================================

    private void BindTaskBarButtons()
    {
        inventoryTaskButton = FindButton("InventoryButton");
        characterTaskButton = FindButton("CharacterButton");
        messengerTaskButton = FindButton("MessengerButton");
        systemTaskButton = FindButton("SystemButton");

        if (inventoryTaskButton != null)
        {
            inventoryTaskButton.onClick.RemoveAllListeners();
            inventoryTaskButton.onClick.AddListener(ToggleInventory);
        }

        if (characterTaskButton != null)
        {
            characterTaskButton.onClick.RemoveAllListeners();
            characterTaskButton.onClick.AddListener(
                () => ToggleCharacter(CharacterPage.Status)
            );
        }

        if (messengerTaskButton != null)
        {
            messengerTaskButton.onClick.RemoveAllListeners();
            messengerTaskButton.onClick.AddListener(ToggleMessenger);
        }

        if (systemTaskButton != null)
        {
            systemTaskButton.onClick.RemoveAllListeners();
            systemTaskButton.onClick.AddListener(ToggleSystem);
        }

        Debug.Log(
            "[Metin2 UI] TaskBar butonlari runtime sisteme baglandi."
        );
    }

    private Button FindButton(string objectName)
    {
        Transform[] all =
            GetComponentsInChildren<Transform>(true);

        foreach (Transform t in all)
        {
            if (!string.Equals(
                t.name,
                objectName,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return t.GetComponent<Button>();
        }

        Debug.LogWarning(
            "[Metin2 UI] TaskBar butonu bulunamadi: " +
            objectName
        );

        return null;
    }

    // ============================================================
    // INVENTORY
    // ============================================================

    private void BuildInventoryWindow()
    {
        /*
         * inventorywindow.py:
         * width 176
         * height 565
         * Equipment_Base x=10 y=33
         * Inventory tabs y=224
         * ItemSlot x=8 y=246, 5x9, 32px step
         */

        inventoryWindow =
            CreateWindow(
                "Metin2_InventoryWindow",
                "Envanter",
                new Vector2(176f, 565f) * WINDOW_SCALE,

                // Envanteri ekranin SOL tarafina sabitle.
                // 18 px sol bosluk, 37 px ust bosluk.
                new Vector2(1037f * WINDOW_SCALE, 30f * WINDOW_SCALE),
                WindowAnchor.BottomLeft
            );

        /*
         * inventorywindow.py burada image kullanmiyor:
         * type = "board".
         * Yani orijinal client bu zemini engine tarafinda ciziyor.
         * Unity'de ayni Metin2 board gorunumunu burada kuruyoruz:
         * koyu ana zemin + ic cerceve + bronz dis cerceve.
         */
        ApplyMetin2BoardSkin(
            inventoryWindow
        );

        // --------------------------------------------------------
        // EQUIPMENT BASE - GERCEK METIN2 SPRITE
        // --------------------------------------------------------

        Sprite equipmentSprite =
            LoadUISprite(
                RES_WINDOWS + "equipment_base"
            );

        RectTransform equipmentBase =
            CreateSpriteTopLeft(
                "Equipment_Base",
                inventoryWindow,
                10f,
                33f,
                equipmentSprite,
                156f,
                188f
            );

        BuildEquipmentSlots(equipmentBase);

        // Equipment Page I / II
        CreateSpriteStateButtonTopLeft(
            "Equipment_Tab_01",
            equipmentBase,
            "I",
            86f,
            161f,
            32f,
            20f,
            RES_WINDOWS + "tab_button_small_01",
            RES_WINDOWS + "tab_button_small_02",
            RES_WINDOWS + "tab_button_small_03",
            () => Debug.Log(
                "[Metin2 Inventory] Equipment Page I"
            )
        );

        CreateSpriteStateButtonTopLeft(
            "Equipment_Tab_02",
            equipmentBase,
            "II",
            118f,
            161f,
            32f,
            20f,
            RES_WINDOWS + "tab_button_small_01",
            RES_WINDOWS + "tab_button_small_02",
            RES_WINDOWS + "tab_button_small_03",
            () => Debug.Log(
                "[Metin2 Inventory] Equipment Page II"
            )
        );

        // --------------------------------------------------------
        // INVENTORY PAGE I / II / III / IV
        // 4 x 45 = 180 inventory slot
        // --------------------------------------------------------

        const float inventoryTabY = 224f;
        const float inventoryTabW = 39f;
        const float inventoryTabH = 22f;

        inventoryPageButtons[0] =
            CreateSpriteStateButtonTopLeft(
            "Inventory_Tab_01",
            inventoryWindow,
            "I",
            10f,
            inventoryTabY,
            inventoryTabW,
            inventoryTabH,
            RES_WINDOWS + "tab_button_small_01",
            RES_WINDOWS + "tab_button_small_02",
            RES_WINDOWS + "tab_button_small_03",
            () => SetInventoryPage(0)
        );

        inventoryPageButtons[1] =
            CreateSpriteStateButtonTopLeft(
            "Inventory_Tab_02",
            inventoryWindow,
            "II",
            49f,
            inventoryTabY,
            inventoryTabW,
            inventoryTabH,
            RES_WINDOWS + "tab_button_small_01",
            RES_WINDOWS + "tab_button_small_02",
            RES_WINDOWS + "tab_button_small_03",
            () => SetInventoryPage(1)
        );

        inventoryPageButtons[2] =
            CreateSpriteStateButtonTopLeft(
            "Inventory_Tab_03",
            inventoryWindow,
            "III",
            88f,
            inventoryTabY,
            inventoryTabW,
            inventoryTabH,
            RES_WINDOWS + "tab_button_small_01",
            RES_WINDOWS + "tab_button_small_02",
            RES_WINDOWS + "tab_button_small_03",
            () => SetInventoryPage(2)
        );

        inventoryPageButtons[3] =
            CreateSpriteStateButtonTopLeft(
            "Inventory_Tab_04",
            inventoryWindow,
            "IV",
            127f,
            inventoryTabY,
            inventoryTabW,
            inventoryTabH,
            RES_WINDOWS + "tab_button_small_01",
            RES_WINDOWS + "tab_button_small_02",
            RES_WINDOWS + "tab_button_small_03",
            () => SetInventoryPage(3)
        );

        // --------------------------------------------------------
        // 5x9 = 45 GERCEK SLOT_BASE
        // --------------------------------------------------------

        RectTransform itemGrid =
            CreateEmptyTopLeft(
                "ItemSlot",
                inventoryWindow,
                8f,
                246f,
                160f,
                288f
            );

        Sprite slotSprite =
            LoadUISprite(
                RES_PUBLIC + "slot_base"
            );

        const int cols = 5;
        const int rows = 9;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int localIndex =
                    y * cols + x;

                RectTransform slot =
                    CreateSpriteTopLeft(
                        "InventorySlot_" + localIndex,
                        itemGrid,
                        x * 32f,
                        y * 32f,
                        slotSprite,
                        32f,
                        32f
                    );

                Image image =
                    slot.GetComponent<Image>();

                if (image != null)
                    image.raycastTarget = true;

                Button button =
                    slot.gameObject.AddComponent<Button>();

                button.targetGraphic = image;
                button.transition =
                    Selectable.Transition.ColorTint;

                int captured =
                    localIndex;

                button.onClick.AddListener(
                    () => OnInventorySlotClick(captured)
                );

                GameObject iconObject = new GameObject(
                    "OriginalItemIcon_" + localIndex,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(slot, false);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(2f, 2f);
                iconRect.offsetMax = new Vector2(-2f, -2f);
                RawImage icon = iconObject.GetComponent<RawImage>();
                icon.raycastTarget = false;
                icon.color = Color.clear;
                inventoryItemIcons[localIndex] = icon;

                Text count = CreateText(
                    "ItemCount_" + localIndex,
                    slot,
                    "",
                    9,
                    TextAnchor.LowerRight
                );
                Stretch(count.rectTransform, 2f);
                count.fontStyle = FontStyle.Bold;
                count.raycastTarget = false;
                inventoryItemCounts[localIndex] = count;
            }
        }

        // --------------------------------------------------------
        // YANG ALANI - GERCEK PARAMETER SLOT + MONEY ICON
        // --------------------------------------------------------

        Sprite moneySlotSprite =
            LoadUISprite(
                RES_PUBLIC + "parameter_slot_05"
            );

        RectTransform moneySlot =
            CreateSpriteBottomCenter(
                "Money_Slot",
                inventoryWindow,
                8f,
                28f,
                moneySlotSprite,
                160f,
                18f
            );

        Sprite moneyIconSprite =
            LoadUISprite(
                RES_WINDOWS + "money_icon"
            );

        CreateSpriteTopLeft(
            "Money_Icon",
            moneySlot,
            -18f,
            20f,
            moneyIconSprite,
            16f,
            16f
        );

        Text money =
            CreateText(
                "Money",
                moneySlot,
                "0",
                10,
                TextAnchor.MiddleRight
            );

        Stretch(
            money.rectTransform,
            3f
        );

        SetInventoryPage(0);

        Debug.Log(
            "[Metin2 Inventory] Gercek UI sprite'lari baglandi. 4 sayfa aktif."
        );
    }

    private void BuildEquipmentSlots(
        RectTransform equipmentBase)
    {
        /*
         * inventorywindow.py:
         * EquipmentSlot root = x:3 y:3 width:150 height:182
         *
         * equipment_base görselinin üzerinde slot çerçeveleri zaten var.
         * Burada ikinci kez slot_base basmıyoruz; yalnızca tıklanabilir,
         * tamamen şeffaf slot alanlarını kuruyoruz.
         */
        RectTransform equipmentSlotRoot =
            CreateEmptyTopLeft(
                "EquipmentSlot",
                equipmentBase,
                3f,
                3f,
                150f,
                182f
            );

        EquipmentSlotDef[] slots =
        {
            new EquipmentSlotDef(90, 39, 37, 32, 64),
            new EquipmentSlotDef(91, 39, 2, 32, 32),
            new EquipmentSlotDef(92, 39, 145, 32, 32),
            new EquipmentSlotDef(93, 75, 67, 32, 32),
            new EquipmentSlotDef(94, 3, 3, 32, 96),
            new EquipmentSlotDef(95, 114, 84, 32, 32),
            new EquipmentSlotDef(96, 114, 52, 32, 32),
            new EquipmentSlotDef(97, 2, 113, 32, 32),
            new EquipmentSlotDef(98, 75, 113, 32, 32),
            new EquipmentSlotDef(99, 114, 1, 32, 32),
            new EquipmentSlotDef(100, 75, 35, 32, 32),
        };

        foreach (EquipmentSlotDef def in slots)
        {
            RectTransform slot =
                CreateEmptyTopLeft(
                    "EquipmentSlot_" + def.index,
                    equipmentSlotRoot,
                    def.x,
                    def.y,
                    def.width,
                    def.height
                );

            Image hit =
                slot.gameObject.AddComponent<Image>();

            hit.color =
                new Color(1f, 1f, 1f, 0f);

            hit.raycastTarget = true;

            Button button =
                slot.gameObject.AddComponent<Button>();

            button.targetGraphic = hit;
            button.transition =
                Selectable.Transition.None;

            int captured =
                def.index;

            button.onClick.AddListener(
                () => Metin2InventoryService.Unequip(EquipmentSlotForIndex(captured))
            );

            Metin2EquipmentSlot equipmentSlot = EquipmentSlotForIndex(captured);
            if (equipmentSlot != Metin2EquipmentSlot.None)
            {
                GameObject iconObject = new GameObject(
                    equipmentSlot + "_EquippedIcon",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(slot, false);
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = new Vector2(2f, 2f);
                iconRect.offsetMax = new Vector2(-2f, -2f);
                RawImage equippedIcon = iconObject.GetComponent<RawImage>();
                equippedIcon.raycastTarget = false;
                equippedIcon.color = Color.clear;
                equipmentItemIcons[equipmentSlot] = equippedIcon;
            }
        }
    }

    private void SetInventoryPage(int page)
    {
        inventoryPage =
            Mathf.Clamp(
                page,
                0,
                3
            );

        for (int i = 0; i < inventoryPageButtons.Length; i++)
        {
            Button b =
                inventoryPageButtons[i];

            if (b == null)
                continue;

            Image img =
                b.targetGraphic as Image;

            if (img == null)
                continue;

            img.color =
                i == inventoryPage
                    ? new Color(1f, 0.88f, 0.55f, 1f)
                    : Color.white;
        }

        Debug.Log(
            "[Metin2 Inventory] Page: " +
            (inventoryPage + 1) +
            " | Global slot araligi: " +
            (inventoryPage * 45) +
            "-" +
            (inventoryPage * 45 + 44)
        );

        RefreshInventory();
    }

    private void RefreshInventory()
    {
        for (int local = 0; local < inventoryItemIcons.Length; local++)
        {
            int global = inventoryPage * 45 + local;
            Metin2InventoryEntry entry = Metin2InventoryService.Get(global);
            Texture2D icon = entry != null ? Metin2ItemDatabase.GetIcon(entry.vnum) : null;
            if (inventoryItemIcons[local] != null)
            {
                inventoryItemIcons[local].texture = icon;
                inventoryItemIcons[local].uvRect = new Rect(0f, 0f, 1f, 1f);
                inventoryItemIcons[local].color = icon != null ? Color.white : Color.clear;
            }
            if (inventoryItemCounts[local] != null)
                inventoryItemCounts[local].text = entry != null && entry.count > 1 ? entry.count.ToString() : string.Empty;
        }
        foreach (System.Collections.Generic.KeyValuePair<Metin2EquipmentSlot, RawImage> pair in equipmentItemIcons)
        {
            Metin2InventoryEntry entry = Metin2InventoryService.GetEquipped(pair.Key);
            Texture2D icon = entry != null ? Metin2ItemDatabase.GetIcon(entry.vnum) : null;
            pair.Value.texture = icon;
            pair.Value.uvRect = new Rect(0f, 0f, 1f, 1f);
            pair.Value.color = icon != null ? Color.white : Color.clear;
        }
    }

    private void OnInventorySlotClick(int localIndex)
    {
        int globalIndex =
            inventoryPage * 45 +
            localIndex;

        Metin2InventoryService.Use(globalIndex);
    }

    private static Metin2EquipmentSlot EquipmentSlotForIndex(int index)
    {
        switch (index)
        {
            case 90: return Metin2EquipmentSlot.Body;
            case 91: return Metin2EquipmentSlot.Head;
            case 92: return Metin2EquipmentSlot.Shoes;
            case 93: return Metin2EquipmentSlot.Wrist;
            case 94: return Metin2EquipmentSlot.Weapon;
            case 95: return Metin2EquipmentSlot.Neck;
            case 96: return Metin2EquipmentSlot.Ear;
            case 99: return Metin2EquipmentSlot.Arrow;
            case 100: return Metin2EquipmentSlot.Shield;
            default: return Metin2EquipmentSlot.None;
        }
    }

    public void ToggleInventory()
    {
        bool open =
            !inventoryWindow.gameObject.activeSelf;

        inventoryWindow.gameObject.SetActive(open);

        if (open)
        {
            inventoryWindow.SetAsLastSibling();
            RefreshInventory();
        }
    }

    // ============================================================
    // CHARACTER
    // ============================================================

    private RectTransform characterStatus;
    private RectTransform characterSkill;
    private RectTransform characterEmotion;
    private RectTransform characterQuest;

    private void BuildCharacterWindow()
    {
        // characterwindow.py:
        // width 253
        // height 361
        // x = 24
        // y = centered above taskbar.
        characterWindow =
            CreateWindow(
                "Metin2_CharacterWindow",
                "KARAKTER",
                new Vector2(253f, 361f) * WINDOW_SCALE,
                new Vector2(24f * WINDOW_SCALE, 37f * WINDOW_SCALE),
                WindowAnchor.BottomLeft
            );

        characterStatus =
            CreateCharacterPage(
                "Character_Page",
                "DURUM\n\n" +
                "Seviye: 1\n" +
                "HP: 3250 / 3250\n" +
                "SP: 1250 / 1250\n\n" +
                "HTH: 10\n" +
                "INT: 10\n" +
                "STR: 10\n" +
                "DEX: 10\n\n" +
                "Saldiri: 120\n" +
                "Savunma: 75"
            );

        characterSkill =
            CreateCharacterPage(
                "Skill_Page",
                "YETENEKLER\n\n" +
                "Aktif Yetenekler\n" +
                "Destek Yetenekleri"
            );

        characterEmotion =
            CreateCharacterPage(
                "Emoticon_Page",
                "DUYGULAR\n\n" +
                "Solo Emotion\n" +
                "Dual Emotion"
            );

        characterQuest =
            CreateCharacterPage(
                "Quest_Page",
                "GOREVLER\n\n" +
                "Aktif gorevler burada listelenecek."
            );

        // characterwindow.py TabControl y=328.
        CreateTabButton(
            characterWindow,
            "Tab_Button_01",
            "Durum",
            6f,
            328f,
            53f,
            27f,
            () => SetCharacterPage(CharacterPage.Status)
        );

        CreateTabButton(
            characterWindow,
            "Tab_Button_02",
            "Yetenek",
            61f,
            328f,
            67f,
            27f,
            () => SetCharacterPage(CharacterPage.Skill)
        );

        CreateTabButton(
            characterWindow,
            "Tab_Button_03",
            "Duygu",
            130f,
            328f,
            61f,
            27f,
            () => SetCharacterPage(CharacterPage.Emoticon)
        );

        CreateTabButton(
            characterWindow,
            "Tab_Button_04",
            "Gorev",
            192f,
            328f,
            55f,
            27f,
            () => SetCharacterPage(CharacterPage.Quest)
        );

        SetCharacterPage(CharacterPage.Status);
    }

    private RectTransform CreateCharacterPage(
        string name,
        string text)
    {
        RectTransform page =
            CreateEmptyTopLeft(
                name,
                characterWindow,
                0f,
                30f,
                250f,
                298f
            );

        Text body =
            CreateText(
                "Body",
                page,
                text,
                11,
                TextAnchor.UpperLeft
            );

        body.rectTransform.anchorMin =
            Vector2.zero;

        body.rectTransform.anchorMax =
            Vector2.one;

        body.rectTransform.offsetMin =
            new Vector2(15f, 10f) * WINDOW_SCALE;

        body.rectTransform.offsetMax =
            new Vector2(-15f, -10f) * WINDOW_SCALE;

        return page;
    }

    private void SetCharacterPage(
        CharacterPage page)
    {
        characterPage = page;

        characterStatus.gameObject.SetActive(
            page == CharacterPage.Status
        );

        characterSkill.gameObject.SetActive(
            page == CharacterPage.Skill
        );

        characterEmotion.gameObject.SetActive(
            page == CharacterPage.Emoticon
        );

        characterQuest.gameObject.SetActive(
            page == CharacterPage.Quest
        );
    }

    private void ToggleCharacter(
        CharacterPage page)
    {
        if (!characterWindow.gameObject.activeSelf)
        {
            characterWindow.gameObject.SetActive(true);
            characterWindow.SetAsLastSibling();
            SetCharacterPage(page);
            return;
        }

        if (characterPage == page)
        {
            characterWindow.gameObject.SetActive(false);
            return;
        }

        SetCharacterPage(page);
    }

    // ============================================================
    // MESSENGER / SYSTEM
    // ============================================================

    private void BuildMessengerWindow()
    {
        messengerWindow =
            CreateWindow(
                "Metin2_MessengerWindow",
                "ARKADASLAR",
                new Vector2(230f, 340f) * WINDOW_SCALE,
                new Vector2(-120f * WINDOW_SCALE, 37f * WINDOW_SCALE),
                WindowAnchor.BottomRight
            );

        Text text =
            CreateText(
                "MessengerText",
                messengerWindow,
                "ARKADASLAR\n\nLonca\nArkadas Listesi\nMesajlar",
                11,
                TextAnchor.UpperLeft
            );

        Stretch(text.rectTransform, 15f);
    }

    private void BuildSystemWindow()
    {
        systemWindow =
            CreateWindow(
                "Metin2_SystemWindow",
                "SISTEM",
                new Vector2(220f, 260f) * WINDOW_SCALE,
                new Vector2(-30f * WINDOW_SCALE, 37f * WINDOW_SCALE),
                WindowAnchor.BottomRight
            );

        Text text =
            CreateText(
                "SystemText",
                systemWindow,
                "Oyun Ayarlari\n\nSistem Ayarlari\n\nYardim\n\nKarakter Degistir\n\nOyundan Cik",
                11,
                TextAnchor.UpperCenter
            );

        Stretch(text.rectTransform, 15f);
    }

    private void ToggleMessenger()
    {
        messengerWindow.gameObject.SetActive(
            !messengerWindow.gameObject.activeSelf
        );

        if (messengerWindow.gameObject.activeSelf)
            messengerWindow.SetAsLastSibling();
    }

    private void ToggleSystem()
    {
        systemWindow.gameObject.SetActive(
            !systemWindow.gameObject.activeSelf
        );

        if (systemWindow.gameObject.activeSelf)
            systemWindow.SetAsLastSibling();
    }

    private void CloseAllWindows()
    {
        if (inventoryWindow != null)
            inventoryWindow.gameObject.SetActive(false);

        if (characterWindow != null)
            characterWindow.gameObject.SetActive(false);

        if (messengerWindow != null)
            messengerWindow.gameObject.SetActive(false);

        if (systemWindow != null)
            systemWindow.gameObject.SetActive(false);
    }

    // ============================================================
    // UI HELPERS
    // ============================================================

    private enum WindowAnchor
    {
        BottomLeft,
        BottomRight
    }

    private RectTransform CreateWindow(
        string name,
        string title,
        Vector2 size,
        Vector2 offset,
        WindowAnchor anchor)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            canvas.transform,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        if (anchor == WindowAnchor.BottomRight)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;

            rect.anchoredPosition =
                new Vector2(
                    offset.x,
                    -offset.y
                );
        }
        else
        {
            rect.anchorMin =
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(0f, 1f);

            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                new Vector2(
                    offset.x,
                    -offset.y
                );
        }

        rect.sizeDelta = size;

        Image background =
            obj.AddComponent<Image>();

        background.color =
            new Color(
                0.055f,
                0.045f,
                0.035f,
                0.98f
            );

        Outline outline =
            obj.AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0.45f,
                0.31f,
                0.14f,
                1f
            );

        outline.effectDistance =
            new Vector2(1f, -1f) *
            WINDOW_SCALE;

        RectTransform titleBar =
            CreatePanelTopLeft(
                "TitleBar",
                rect,
                8f,
                7f,
                size.x / WINDOW_SCALE - 15f,
                20f
            );

        Text titleText =
            CreateText(
                "TitleName",
                titleBar,
                title,
                11,
                TextAnchor.MiddleCenter
            );

        Stretch(
            titleText.rectTransform,
            2f
        );

        Button close =
            CreateButton(
                "CloseButton",
                titleBar,
                "X"
            );

        RectTransform closeRect =
            close.GetComponent<RectTransform>();

        closeRect.anchorMin =
            new Vector2(1f, 0.5f);

        closeRect.anchorMax =
            new Vector2(1f, 0.5f);

        closeRect.pivot =
            new Vector2(1f, 0.5f);

        closeRect.sizeDelta =
            new Vector2(18f, 16f) *
            WINDOW_SCALE;

        closeRect.anchoredPosition =
            new Vector2(-2f, 0f) *
            WINDOW_SCALE;

        close.onClick.AddListener(
            () => obj.SetActive(false)
        );

        return rect;
    }

    private void ApplyMetin2BoardSkin(
        RectTransform window)
    {
        /*
         * ui.py Board.SetSize() birebir:
         *
         * corners: 32x32
         * base:    x=32 y=32, width-64 x height-64
         * left:    x=0 y=32
         * right:   x=width-32 y=32
         * top:     x=32 y=0
         * bottom:  x=32 y=height-32
         *
         * Eski kodda stretch anchor + offsetMin/offsetMax karışık
         * kullanıldığı için özellikle alt kenar ve köşeler kayıyordu.
         * Burada bütün parçalar TOP-LEFT koordinatıyla açıkça kuruluyor.
         */

        const float C = 32f;
        const float TITLE_H = 23f;

        float W =
            window.sizeDelta.x /
            WINDOW_SCALE;

        float H =
            window.sizeDelta.y /
            WINDOW_SCALE;

        Image fallback =
            window.GetComponent<Image>();

        if (fallback != null)
        {
            fallback.sprite = null;
            fallback.color = Color.clear;
        }

        Outline fallbackOutline =
            window.GetComponent<Outline>();

        if (fallbackOutline != null)
            fallbackOutline.enabled = false;

        Transform oldFake =
            window.Find("Metin2_Board_Frame");

        if (oldFake != null)
            Destroy(oldFake.gameObject);

        Transform oldPattern =
            window.Find("Metin2_Board_Pattern");

        if (oldPattern != null)
            Destroy(oldPattern.gameObject);

        RectTransform boardRoot =
            CreateEmptyTopLeft(
                "Metin2_Board_Pattern",
                window,
                0f,
                0f,
                W,
                H
            );

        boardRoot.SetAsFirstSibling();

        Sprite boardBase =
            LoadUISprite(
                RES_PATTERN + "board_base"
            );

        Sprite cornerLT =
            LoadUISprite(
                RES_PATTERN + "board_corner_lefttop"
            );

        Sprite cornerLB =
            LoadUISprite(
                RES_PATTERN + "board_corner_leftbottom"
            );

        Sprite cornerRT =
            LoadUISprite(
                RES_PATTERN + "board_corner_righttop"
            );

        Sprite cornerRB =
            LoadUISprite(
                RES_PATTERN + "board_corner_rightbottom"
            );

        Sprite lineL =
            LoadUISprite(
                RES_PATTERN + "board_line_left"
            );

        Sprite lineR =
            LoadUISprite(
                RES_PATTERN + "board_line_right"
            );

        Sprite lineT =
            LoadUISprite(
                RES_PATTERN + "board_line_top"
            );

        Sprite lineB =
            LoadUISprite(
                RES_PATTERN + "board_line_bottom"
            );

        // BASE
        CreatePatternRectTopLeft(
            "Board_Base",
            boardRoot,
            boardBase,
            C,
            C,
            W - C * 2f,
            H - C * 2f,
            Image.Type.Tiled
        );

        // CORNERS
        CreatePatternRectTopLeft(
            "Board_Corner_LT",
            boardRoot,
            cornerLT,
            0f,
            0f,
            C,
            C,
            Image.Type.Simple
        );

        CreatePatternRectTopLeft(
            "Board_Corner_LB",
            boardRoot,
            cornerLB,
            0f,
            H - C,
            C,
            C,
            Image.Type.Simple
        );

        CreatePatternRectTopLeft(
            "Board_Corner_RT",
            boardRoot,
            cornerRT,
            W - C,
            0f,
            C,
            C,
            Image.Type.Simple
        );

        CreatePatternRectTopLeft(
            "Board_Corner_RB",
            boardRoot,
            cornerRB,
            W - C,
            H - C,
            C,
            C,
            Image.Type.Simple
        );

        // LINES
        CreatePatternRectTopLeft(
            "Board_Line_Left",
            boardRoot,
            lineL,
            0f,
            C,
            C,
            H - C * 2f,
            Image.Type.Tiled
        );

        CreatePatternRectTopLeft(
            "Board_Line_Right",
            boardRoot,
            lineR,
            W - C,
            C,
            C,
            H - C * 2f,
            Image.Type.Tiled
        );

        CreatePatternRectTopLeft(
            "Board_Line_Top",
            boardRoot,
            lineT,
            C,
            0f,
            W - C * 2f,
            C,
            Image.Type.Tiled
        );

        CreatePatternRectTopLeft(
            "Board_Line_Bottom",
            boardRoot,
            lineB,
            C,
            H - C,
            W - C * 2f,
            C,
            Image.Type.Tiled
        );

        // ========================================================
        // TITLE BAR — ui.py / inventorywindow.py birebir
        // x=8 y=7 width=windowWidth-15 height=23
        // ========================================================

        Transform titleTransform =
            window.Find("TitleBar");

        if (titleTransform == null)
            return;

        float titleW =
            W - 15f;

        RectTransform titleRect =
            titleTransform as RectTransform;

        if (titleRect != null)
        {
            titleRect.anchorMin =
                new Vector2(0f, 1f);

            titleRect.anchorMax =
                new Vector2(0f, 1f);

            titleRect.pivot =
                new Vector2(0f, 1f);

            titleRect.anchoredPosition =
                new Vector2(
                    8f,
                    -7f
                ) *
                WINDOW_SCALE;

            titleRect.sizeDelta =
                new Vector2(
                    titleW,
                    TITLE_H
                ) *
                WINDOW_SCALE;
        }

        Image oldTitleBg =
            titleTransform.GetComponent<Image>();

        if (oldTitleBg != null)
        {
            oldTitleBg.sprite = null;
            oldTitleBg.color = Color.clear;
        }

        Outline oldTitleOutline =
            titleTransform.GetComponent<Outline>();

        if (oldTitleOutline != null)
            oldTitleOutline.enabled = false;

        Transform oldHighlight =
            titleTransform.Find("Title_Highlight");

        if (oldHighlight != null)
            Destroy(oldHighlight.gameObject);

        Transform oldTitlePattern =
            titleTransform.Find("TitleBar_Pattern");

        if (oldTitlePattern != null)
            Destroy(oldTitlePattern.gameObject);

        RectTransform titlePattern =
            CreateEmptyTopLeft(
                "TitleBar_Pattern",
                titleRect,
                0f,
                0f,
                titleW,
                TITLE_H
            );

        titlePattern.SetAsFirstSibling();

        Sprite titleLeft =
            LoadUISprite(
                RES_PATTERN + "titlebar_left"
            );

        Sprite titleCenter =
            LoadUISprite(
                RES_PATTERN + "titlebar_center"
            );

        Sprite titleRight =
            LoadUISprite(
                RES_PATTERN + "titlebar_right"
            );

        CreatePatternRectTopLeft(
            "TitleBar_Left",
            titlePattern,
            titleLeft,
            0f,
            0f,
            32f,
            TITLE_H,
            Image.Type.Simple
        );

        CreatePatternRectTopLeft(
            "TitleBar_Center",
            titlePattern,
            titleCenter,
            32f,
            0f,
            titleW - 64f,
            TITLE_H,
            Image.Type.Tiled
        );

        CreatePatternRectTopLeft(
            "TitleBar_Right",
            titlePattern,
            titleRight,
            titleW - 32f,
            0f,
            32f,
            TITLE_H,
            Image.Type.Simple
        );

        // CLOSE
        Transform closeTransform =
            titleTransform.Find(
                "CloseButton"
            );

        if (closeTransform != null)
        {
            Button closeButton =
                closeTransform.GetComponent<Button>();

            Image closeImage =
                closeTransform.GetComponent<Image>();

            Sprite closeNormal =
                LoadUISprite(
                    RES_PUBLIC + "close_button_01"
                );

            Sprite closeHover =
                LoadUISprite(
                    RES_PUBLIC + "close_button_02"
                );

            Sprite closeDown =
                LoadUISprite(
                    RES_PUBLIC + "close_button_03"
                );

            float closeW =
                closeNormal != null
                    ? closeNormal.rect.width
                    : 15f;

            float closeH =
                closeNormal != null
                    ? closeNormal.rect.height
                    : 15f;

            if (closeImage != null)
            {
                closeImage.sprite =
                    closeNormal;

                closeImage.color =
                    Color.white;

                closeImage.preserveAspect =
                    false;
            }

            if (closeButton != null)
            {
                closeButton.transition =
                    Selectable.Transition.SpriteSwap;

                SpriteState state =
                    closeButton.spriteState;

                state.highlightedSprite =
                    closeHover;

                state.selectedSprite =
                    closeHover;

                state.pressedSprite =
                    closeDown;

                state.disabledSprite =
                    closeNormal;

                closeButton.spriteState =
                    state;
            }

            RectTransform closeRect =
                closeTransform as RectTransform;

            if (closeRect != null)
            {
                closeRect.anchorMin =
                    new Vector2(0f, 1f);

                closeRect.anchorMax =
                    new Vector2(0f, 1f);

                closeRect.pivot =
                    new Vector2(0f, 1f);

                closeRect.sizeDelta =
                    new Vector2(
                        closeW,
                        closeH
                    ) *
                    WINDOW_SCALE;

                closeRect.anchoredPosition =
                    new Vector2(
                        titleW - closeW - 3f,
                        -3f
                    ) *
                    WINDOW_SCALE;
            }

            foreach (
                Text t
                in closeTransform
                    .GetComponentsInChildren<Text>(true)
            )
            {
                t.text = "";
            }
        }

        // TITLE TEXT
        Text titleText =
            titleTransform
                .GetComponentInChildren<Text>(true);

        if (titleText != null)
        {
            titleText.text =
                "Envanter";

            titleText.color =
                new Color(
                    0.95f,
                    0.90f,
                    0.78f,
                    1f
                );

            titleText.alignment =
                TextAnchor.UpperCenter;

            titleText.fontStyle =
                FontStyle.Normal;

            RectTransform tr =
                titleText.rectTransform;

            tr.anchorMin =
                new Vector2(0f, 1f);

            tr.anchorMax =
                new Vector2(0f, 1f);

            tr.pivot =
                new Vector2(0.5f, 1f);

            // inventorywindow.py: x=77 y=3
            tr.anchoredPosition =
                new Vector2(
                    77f,
                    -3f
                ) *
                WINDOW_SCALE;

            tr.sizeDelta =
                new Vector2(
                    140f,
                    17f
                ) *
                WINDOW_SCALE;
        }
    }

    private RectTransform CreatePatternRectTopLeft(
        string name,
        RectTransform parent,
        Sprite sprite,
        float x,
        float y,
        float width,
        float height,
        Image.Type type)
    {
        RectTransform rect =
            CreateEmptyTopLeft(
                name,
                parent,
                x,
                y,
                width,
                height
            );

        Image image =
            rect.gameObject.AddComponent<Image>();

        image.sprite =
            sprite;

        image.color =
            sprite != null
                ? Color.white
                : new Color(
                    1f,
                    0f,
                    1f,
                    0.35f
                );

        image.raycastTarget =
            false;

        image.preserveAspect =
            false;

        image.type =
            type;

        return rect;
    }

    private enum PatternAnchor
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Stretch,
        StretchVerticalLeft,
        StretchVerticalRight,
        StretchHorizontalTop,
        StretchHorizontalBottom
    }

    private RectTransform CreateEmptyStretch(
        string name,
        Transform parent)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.localScale =
            Vector3.one;

        return rect;
    }

    private RectTransform CreatePatternImage(
        string name,
        RectTransform parent,
        Sprite sprite,
        Vector2 a,
        Vector2 b,
        PatternAnchor anchor,
        Image.Type type)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        const float CORNER_RUNTIME = 32f;

        float S(float v) =>
            v * WINDOW_SCALE;

        switch (anchor)
        {
            case PatternAnchor.TopLeft:
                rect.anchorMin =
                    new Vector2(0f, 1f);
                rect.anchorMax =
                    new Vector2(0f, 1f);
                rect.pivot =
                    new Vector2(0f, 1f);
                rect.anchoredPosition =
                    new Vector2(
                        S(a.x),
                        -S(a.y)
                    );
                rect.sizeDelta =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.TopRight:
                rect.anchorMin =
                    new Vector2(1f, 1f);
                rect.anchorMax =
                    new Vector2(1f, 1f);
                rect.pivot =
                    new Vector2(1f, 1f);
                rect.anchoredPosition =
                    new Vector2(
                        -S(a.x),
                        -S(a.y)
                    );
                rect.sizeDelta =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.BottomLeft:
                rect.anchorMin =
                    new Vector2(0f, 0f);
                rect.anchorMax =
                    new Vector2(0f, 0f);
                rect.pivot =
                    new Vector2(0f, 0f);
                rect.anchoredPosition =
                    new Vector2(
                        S(a.x),
                        S(a.y)
                    );
                rect.sizeDelta =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.BottomRight:
                rect.anchorMin =
                    new Vector2(1f, 0f);
                rect.anchorMax =
                    new Vector2(1f, 0f);
                rect.pivot =
                    new Vector2(1f, 0f);
                rect.anchoredPosition =
                    new Vector2(
                        -S(a.x),
                        S(a.y)
                    );
                rect.sizeDelta =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.Stretch:
                rect.anchorMin =
                    Vector2.zero;
                rect.anchorMax =
                    Vector2.one;
                rect.offsetMin =
                    new Vector2(
                        S(a.x),
                        S(a.y)
                    );
                rect.offsetMax =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.StretchVerticalLeft:
                rect.anchorMin =
                    new Vector2(0f, 0f);
                rect.anchorMax =
                    new Vector2(0f, 1f);
                rect.pivot =
                    new Vector2(0f, 0.5f);
                rect.offsetMin =
                    new Vector2(
                        S(a.x),
                        S(a.y)
                    );
                rect.offsetMax =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.StretchVerticalRight:
                rect.anchorMin =
                    new Vector2(1f, 0f);
                rect.anchorMax =
                    new Vector2(1f, 1f);
                rect.pivot =
                    new Vector2(1f, 0.5f);
                rect.offsetMin =
                    new Vector2(
                        S(a.x),
                        S(a.y)
                    );
                rect.offsetMax =
                    new Vector2(
                        S(b.x),
                        S(b.y)
                    );
                break;

            case PatternAnchor.StretchHorizontalTop:
                rect.anchorMin =
                    new Vector2(0f, 1f);
                rect.anchorMax =
                    new Vector2(1f, 1f);
                rect.pivot =
                    new Vector2(0.5f, 1f);
                rect.offsetMin =
                    new Vector2(
                        S(a.x),
                        -S(b.y)
                    );
                rect.offsetMax =
                    new Vector2(
                        S(b.x),
                        -S(a.y)
                    );
                break;

            case PatternAnchor.StretchHorizontalBottom:
                rect.anchorMin =
                    new Vector2(0f, 0f);
                rect.anchorMax =
                    new Vector2(1f, 0f);
                rect.pivot =
                    new Vector2(0.5f, 0f);

                // Alt kenar parent'in ALTINDA degil, ICINDE 32px yukseklikte.
                // Sol ve sagda 32px kose payi birak.
                rect.offsetMin =
                    new Vector2(
                        S(CORNER_RUNTIME),
                        0f
                    );
                rect.offsetMax =
                    new Vector2(
                        -S(CORNER_RUNTIME),
                        S(CORNER_RUNTIME)
                    );
                break;
        }

        Image image =
            obj.AddComponent<Image>();

        image.sprite =
            sprite;

        image.color =
            Color.white;

        image.raycastTarget =
            false;

        image.preserveAspect =
            false;

        image.type =
            type;

        if (sprite == null)
        {
            image.color =
                new Color(
                    1f,
                    0f,
                    1f,
                    0.35f
                );
        }

        return rect;
    }

    private Sprite LoadUISprite(
        string resourcePath)
    {
        Sprite sprite =
            Resources.Load<Sprite>(
                resourcePath
            );

        if (sprite == null)
        {
            Debug.LogWarning(
                "[Metin2 UI] Sprite bulunamadi: Resources/" +
                resourcePath
            );
        }

        return sprite;
    }

    private RectTransform CreateSpriteTopLeft(
        string name,
        RectTransform parent,
        float x,
        float y,
        Sprite sprite,
        float fallbackWidth,
        float fallbackHeight)
    {
        float width =
            sprite != null
                ? sprite.rect.width
                : fallbackWidth;

        float height =
            sprite != null
                ? sprite.rect.height
                : fallbackHeight;

        RectTransform rect =
            CreateEmptyTopLeft(
                name,
                parent,
                x,
                y,
                width,
                height
            );

        Image image =
            rect.gameObject.AddComponent<Image>();

        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = false;

        if (sprite == null)
        {
            image.color =
                new Color(
                    1f,
                    0f,
                    1f,
                    0.25f
                );
        }
        else
        {
            image.color = Color.white;
        }

        return rect;
    }

    private RectTransform CreateSpriteBottomCenter(
        string name,
        RectTransform parent,
        float x,
        float y,
        Sprite sprite,
        float fallbackWidth,
        float fallbackHeight)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 0f);

        rect.anchorMax =
            new Vector2(0.5f, 0f);

        // Metin2 vertical_align="bottom":
        // verilen y, objenin UST kenarinin parent altindan uzakligidir.
        rect.pivot =
            new Vector2(0.5f, 1f);

        float width =
            sprite != null
                ? sprite.rect.width
                : fallbackWidth;

        float height =
            sprite != null
                ? sprite.rect.height
                : fallbackHeight;

        rect.sizeDelta =
            new Vector2(
                width,
                height
            ) *
            WINDOW_SCALE;

        rect.anchoredPosition =
            new Vector2(
                x,
                y
            ) *
            WINDOW_SCALE;

        Image image =
            obj.AddComponent<Image>();

        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = false;

        if (sprite == null)
        {
            image.color =
                new Color(
                    1f,
                    0f,
                    1f,
                    0.25f
                );
        }

        return rect;
    }

    private Button CreateSpriteStateButtonTopLeft(
        string name,
        RectTransform parent,
        string label,
        float x,
        float y,
        float fallbackWidth,
        float fallbackHeight,
        string normalResource,
        string hoverResource,
        string downResource,
        Action action)
    {
        Sprite normal =
            LoadUISprite(normalResource);

        Sprite hover =
            LoadUISprite(hoverResource);

        Sprite down =
            LoadUISprite(downResource);

        // Layout dosyasındaki ölçüyü kullan.
        // 4 envanter sekmesi 39x22, equipment sekmeleri 32x20.
        float width =
            fallbackWidth;

        float height =
            fallbackHeight;

        RectTransform rect =
            CreateEmptyTopLeft(
                name,
                parent,
                x,
                y,
                width,
                height
            );

        Image image =
            rect.gameObject.AddComponent<Image>();

        image.sprite = normal;
        image.preserveAspect = false;

        Button button =
            rect.gameObject.AddComponent<Button>();

        button.targetGraphic = image;
        button.transition =
            Selectable.Transition.SpriteSwap;

        SpriteState state =
            button.spriteState;

        state.highlightedSprite = hover;
        state.selectedSprite = hover;
        state.pressedSprite = down;
        state.disabledSprite = normal;

        button.spriteState = state;

        if (action != null)
        {
            button.onClick.AddListener(
                () => action()
            );
        }

        Text text =
            CreateText(
                "Text",
                rect,
                label,
                9,
                TextAnchor.MiddleCenter
            );

        Stretch(
            text.rectTransform,
            0f
        );

        return button;
    }

    private RectTransform CreatePanelTopLeft(
        string name,
        RectTransform parent,
        float x,
        float y,
        float width,
        float height)
    {
        RectTransform rect =
            CreateEmptyTopLeft(
                name,
                parent,
                x,
                y,
                width,
                height
            );

        Image image =
            rect.gameObject.AddComponent<Image>();

        image.color =
            new Color(
                0.09f,
                0.07f,
                0.045f,
                0.98f
            );

        return rect;
    }

    private RectTransform CreatePanelBottomCenter(
        string name,
        RectTransform parent,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.5f, 0f);

        rect.anchorMax =
            new Vector2(0.5f, 0f);

        rect.pivot =
            new Vector2(0.5f, 0f);

        rect.sizeDelta =
            new Vector2(width, height) *
            WINDOW_SCALE;

        rect.anchoredPosition =
            new Vector2(0f, y) *
            WINDOW_SCALE;

        Image image =
            obj.AddComponent<Image>();

        image.color =
            new Color(
                0.08f,
                0.06f,
                0.04f,
                1f
            );

        return rect;
    }

    private RectTransform CreateEmptyTopLeft(
        string name,
        RectTransform parent,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0f, 1f);

        rect.anchorMax =
            new Vector2(0f, 1f);

        rect.pivot =
            new Vector2(0f, 1f);

        rect.sizeDelta =
            new Vector2(width, height) *
            WINDOW_SCALE;

        rect.anchoredPosition =
            new Vector2(x, -y) *
            WINDOW_SCALE;

        return rect;
    }

    private RectTransform CreateSlot(
        string name,
        RectTransform parent,
        float x,
        float y,
        float width,
        float height)
    {
        RectTransform rect =
            CreateEmptyTopLeft(
                name,
                parent,
                x,
                y,
                width,
                height
            );

        Image image =
            rect.gameObject.AddComponent<Image>();

        image.color =
            new Color(
                0.065f,
                0.052f,
                0.038f,
                1f
            );

        Outline outline =
            rect.gameObject.AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0.26f,
                0.18f,
                0.08f,
                1f
            );

        outline.effectDistance =
            new Vector2(1f, -1f);

        return rect;
    }

    private void CreateTabButton(
        RectTransform parent,
        string name,
        string label,
        float x,
        float y,
        float width,
        float height,
        Action action)
    {
        RectTransform rect =
            CreatePanelTopLeft(
                name,
                parent,
                x,
                y,
                width,
                height
            );

        Button button =
            rect.gameObject.AddComponent<Button>();

        if (action != null)
            button.onClick.AddListener(
                () => action()
            );

        Text text =
            CreateText(
                "Text",
                rect,
                label,
                10,
                TextAnchor.MiddleCenter
            );

        Stretch(
            text.rectTransform,
            1f
        );
    }

    private Button CreateButton(
        string name,
        RectTransform parent,
        string label)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            obj.AddComponent<RectTransform>();

        Image image =
            obj.AddComponent<Image>();

        image.color =
            new Color(
                0.16f,
                0.10f,
                0.05f,
                1f
            );

        Button button =
            obj.AddComponent<Button>();

        Text text =
            CreateText(
                "Text",
                rect,
                label,
                10,
                TextAnchor.MiddleCenter
            );

        Stretch(
            text.rectTransform,
            0f
        );

        return button;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject obj =
            new GameObject(name);

        obj.transform.SetParent(
            parent,
            false
        );

        Text text =
            obj.AddComponent<Text>();

        text.font = font;
        text.text = value;

        text.fontSize =
            Mathf.RoundToInt(
                fontSize * WINDOW_SCALE
            );

        text.alignment =
            alignment;

        text.color =
            new Color(
                0.95f,
                0.89f,
                0.76f,
                1f
            );

        text.horizontalOverflow =
            HorizontalWrapMode.Wrap;

        text.verticalOverflow =
            VerticalWrapMode.Overflow;

        Shadow shadow =
            obj.AddComponent<Shadow>();

        shadow.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.95f
            );

        shadow.effectDistance =
            new Vector2(1f, -1f);

        return text;
    }

    private static void Stretch(
        RectTransform rect,
        float padding)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            new Vector2(padding, padding) *
            WINDOW_SCALE;

        rect.offsetMax =
            new Vector2(-padding, -padding) *
            WINDOW_SCALE;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject obj =
            new GameObject("EventSystem");

        obj.AddComponent<EventSystem>();

        try
        {
            obj.AddComponent<
                UnityEngine.InputSystem.UI.InputSystemUIInputModule
            >();
        }
        catch
        {
            // New Input System module yoksa mevcut EventSystem yine kalir.
        }
    }

    private readonly struct EquipmentSlotDef
    {
        public readonly int index;
        public readonly float x;
        public readonly float y;
        public readonly float width;
        public readonly float height;

        public EquipmentSlotDef(
            int index,
            float x,
            float y,
            float width,
            float height)
        {
            this.index = index;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }
}

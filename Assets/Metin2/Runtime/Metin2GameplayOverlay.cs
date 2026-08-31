using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2GameplayOverlay : MonoBehaviour
    {
        public static bool IsTyping { get; private set; }

        readonly Color board = new Color(0.055f, 0.043f, 0.03f, 0.94f);
        readonly Color border = new Color(0.58f, 0.40f, 0.14f, 0.95f);
        Font font;
        Canvas canvas;
        RectTransform targetBoard;
        Text targetName;
        Text targetHp;
        Image targetFill;
        Metin2MobCombatant selectedTarget;
        RectTransform chatPanel;
        Text chatHistory;
        InputField chatInput;
        Text channelLabel;
        Metin2ChatChannel channel = Metin2ChatChannel.Talking;
        RectTransform questTracker;
        Text questTrackerText;
        RectTransform questWindow;
        Text questWindowText;
        RectTransform messengerWindow;
        Text messengerList;
        InputField friendInput;
        RectTransform whisperWindow;
        InputField whisperNameInput;
        InputField whisperMessageInput;
        bool chatDirty = true;
        bool questDirty = true;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            BuildTargetBoard();
            BuildChat();
            BuildQuestUi();
            BuildMessenger();
            BuildWhisper();
            Metin2MobCombatant.SelectedChanged += OnTargetChanged;
            Metin2ChatService.MessageAdded += OnMessage;
            Metin2QuestService.Changed += OnQuestChanged;
            Metin2MessengerService.Changed += RefreshMessenger;
            Metin2QuestService.EnsureInitialized();
            Metin2QuestService.ReportLevel(Metin2PlayerState.Local != null ? Metin2PlayerState.Local.Level : 1);
            Metin2ChatService.Append(Metin2ChatChannel.Info, "Metin 3 dünyasına hoş geldin.");
        }

        void OnDestroy()
        {
            Metin2MobCombatant.SelectedChanged -= OnTargetChanged;
            Metin2ChatService.MessageAdded -= OnMessage;
            Metin2QuestService.Changed -= OnQuestChanged;
            Metin2MessengerService.Changed -= RefreshMessenger;
            IsTyping = false;
        }

        void Update()
        {
            HandleKeys();
            if (selectedTarget == null || selectedTarget.IsDead) targetBoard.gameObject.SetActive(false);
            else
            {
                targetBoard.gameObject.SetActive(true);
                targetName.text = $"Lv.{selectedTarget.Level}  {selectedTarget.DisplayName}";
                targetHp.text = selectedTarget.CurrentHp + " / " + selectedTarget.MaxHp;
                targetFill.fillAmount = selectedTarget.HpRatio;
            }
            if (chatDirty) RefreshChat();
            if (questDirty) RefreshQuests();
        }

        void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (!IsTyping) OpenChatInput();
                else SubmitChat();
            }
            if (keyboard.escapeKey.wasPressedThisFrame && IsTyping) CloseChatInput();
            if (!IsTyping && keyboard.mKey.wasPressedThisFrame) ToggleMessenger();
            if (!IsTyping && keyboard.nKey.wasPressedThisFrame) ToggleQuests();
        }

        void BuildCanvas()
        {
            GameObject root = new GameObject("Metin2 Source Gameplay Overlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40500;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 768f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        void BuildTargetBoard()
        {
            targetBoard = Panel(canvas.transform, "Target Board", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(420f, 64f));
            targetName = Label(targetBoard, "Target Name", 15, TextAnchor.UpperCenter, new Vector2(10f, -6f), new Vector2(-20f, 24f));
            RectTransform gauge = Rect(targetBoard, "HP Gauge", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(330f, 17f));
            Image back = gauge.gameObject.AddComponent<Image>(); back.color = Color.black;
            RectTransform fill = Rect(gauge, "Fill", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-4f, -4f));
            targetFill = fill.gameObject.AddComponent<Image>(); targetFill.color = new Color(0.72f, 0.02f, 0.02f); targetFill.type = Image.Type.Filled; targetFill.fillMethod = Image.FillMethod.Horizontal;
            targetHp = Label(gauge, "HP", 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(targetHp.rectTransform, 2f);
            targetBoard.gameObject.SetActive(false);
        }

        void BuildChat()
        {
            // Original uichat.py keeps the history floating over the world. It is centered above
            // the taskbar and only the active input line receives a dark translucent backing.
            chatPanel = Rect(canvas.transform, "Chat", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 54f), new Vector2(720f, 176f), new Vector2(0.5f, 0f));
            CanvasGroup chatGroup = chatPanel.gameObject.AddComponent<CanvasGroup>();
            chatGroup.alpha = 1f;
            chatHistory = Label(chatPanel, "History", 13, TextAnchor.LowerLeft, new Vector2(8f, 35f), new Vector2(-16f, -43f));
            chatHistory.horizontalOverflow = HorizontalWrapMode.Wrap;
            chatHistory.verticalOverflow = VerticalWrapMode.Truncate;
            chatHistory.supportRichText = true;
            RectTransform modeButton = Rect(chatPanel, "Mode", Vector2.zero, Vector2.zero, new Vector2(8f, 7f), new Vector2(76f, 24f));
            Button mode = Button(modeButton, CycleChannel);
            channelLabel = Label(modeButton, "Label", 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(channelLabel.rectTransform, 1f);
            RectTransform inputRoot = Rect(chatPanel, "Input", Vector2.zero, Vector2.right, new Vector2(90f, 7f), new Vector2(-98f, 24f));
            Image inputBackground = inputRoot.gameObject.AddComponent<Image>(); inputBackground.color = new Color(0f, 0f, 0f, 0.58f);
            chatInput = inputRoot.gameObject.AddComponent<InputField>();
            Text inputText = Label(inputRoot, "Text", 12, TextAnchor.MiddleLeft, new Vector2(6f, 0f), new Vector2(-12f, 0f)); Stretch(inputText.rectTransform, 4f);
            Text placeholder = Label(inputRoot, "Placeholder", 11, TextAnchor.MiddleLeft, new Vector2(6f, 0f), new Vector2(-12f, 0f), new Color(0.65f, 0.6f, 0.5f)); Stretch(placeholder.rectTransform, 4f); placeholder.text = "Enter ile mesaj yaz...";
            chatInput.textComponent = inputText; chatInput.placeholder = placeholder; chatInput.lineType = InputField.LineType.SingleLine;
            chatInput.gameObject.SetActive(false);
            UpdateChannelLabel();
        }

        void BuildQuestUi()
        {
            questTracker = Panel(canvas.transform, "Quest Tracker", Vector2.one, Vector2.one, new Vector2(-18f, -230f), new Vector2(285f, 150f));
            Text title = Label(questTracker, "Title", 13, TextAnchor.UpperLeft, new Vector2(10f, -7f), new Vector2(-20f, 22f)); title.text = "GÖREVLER (N)"; title.color = new Color(1f, 0.8f, 0.3f);
            questTrackerText = Label(questTracker, "Quests", 11, TextAnchor.UpperLeft, new Vector2(10f, -32f), new Vector2(-20f, -40f));
            questWindow = Panel(canvas.transform, "Quest Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 420f));
            Text windowTitle = Label(questWindow, "Title", 17, TextAnchor.UpperCenter, new Vector2(12f, -10f), new Vector2(-24f, 30f)); windowTitle.text = "GÖREVLER";
            questWindowText = Label(questWindow, "Quest List", 13, TextAnchor.UpperLeft, new Vector2(24f, -55f), new Vector2(-48f, -110f));
            RectTransform claim = Rect(questWindow, "Claim", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(170f, 34f));
            Button(claim, ClaimFirstCompleted); Text claimText = Label(claim, "Label", 12, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(claimText.rectTransform, 2f); claimText.text = "ÖDÜLÜ AL";
            questWindow.gameObject.SetActive(false);
        }

        void BuildMessenger()
        {
            // messengerwindow.py: 170x300, five source-image buttons at 30 px steps.
            messengerWindow = Panel(canvas.transform, "Messenger Window", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(170f, 300f));
            Text title = Label(messengerWindow, "Title", 13, TextAnchor.UpperCenter, new Vector2(8f, -7f), new Vector2(-16f, 22f)); title.text = "ARKADAŞLAR (M)";
            messengerList = Label(messengerWindow, "List", 11, TextAnchor.UpperLeft, new Vector2(12f, -72f), new Vector2(-26f, -112f));

            RectTransform input = Rect(messengerWindow, "Friend Name", Vector2.up, Vector2.one, new Vector2(9f, -36f), new Vector2(-18f, 23f), Vector2.up);
            Image inputBg = input.gameObject.AddComponent<Image>(); inputBg.color = new Color(0f, 0f, 0f, 0.72f);
            friendInput = input.gameObject.AddComponent<InputField>();
            Text friendText = Label(input, "Text", 11, TextAnchor.MiddleLeft, new Vector2(5f, 0f), new Vector2(-10f, 0f)); Stretch(friendText.rectTransform, 3f); friendInput.textComponent = friendText;
            Text placeholder = Label(input, "Placeholder", 10, TextAnchor.MiddleLeft, new Vector2(5f, 0f), new Vector2(-10f, 0f), new Color(0.65f, 0.65f, 0.65f)); Stretch(placeholder.rectTransform, 3f); placeholder.text = "Oyuncu adı"; friendInput.placeholder = placeholder;

            const float startX = 12f;
            const float step = 30f;
            SpriteButton(messengerWindow, "Arkadaş Ekle", new Vector2(startX + step * 0f, 9f), "messenger_add_friend", AddFriend);
            SpriteButton(messengerWindow, "Özel Mesaj", new Vector2(startX + step * 1f, 9f), "messenger_whisper", OpenWhisperForFriend);
            SpriteButton(messengerWindow, "Mobil", new Vector2(startX + step * 2f, 9f), "messenger_mobile", ShowMobileContacts);
            SpriteButton(messengerWindow, "Sil", new Vector2(startX + step * 3f, 9f), "messenger_delete", RemoveFriend);
            SpriteButton(messengerWindow, "Lonca", new Vector2(startX + step * 4f, 9f), "messenger_guild", ShowGuildContacts);
            messengerWindow.gameObject.SetActive(false);
            RefreshMessenger();
        }

        void BuildWhisper()
        {
            // whisperdialog.py: exact 280x200 window, 50 px bottom edit bar.
            whisperWindow = Panel(canvas.transform, "Whisper Dialog", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 200f));
            RectTransform nameRoot = Rect(whisperWindow, "Name Slot", Vector2.up, Vector2.one, new Vector2(10f, -10f), new Vector2(-76f, 24f), Vector2.up);
            Image nameBg = nameRoot.gameObject.AddComponent<Image>(); nameBg.color = new Color(0f, 0f, 0f, 0.75f);
            whisperNameInput = nameRoot.gameObject.AddComponent<InputField>();
            Text nameText = Label(nameRoot, "Text", 11, TextAnchor.MiddleLeft, new Vector2(4f, 0f), new Vector2(-8f, 0f)); Stretch(nameText.rectTransform, 2f); whisperNameInput.textComponent = nameText;
            Text namePlaceholder = Label(nameRoot, "Placeholder", 10, TextAnchor.MiddleLeft, new Vector2(4f, 0f), new Vector2(-8f, 0f), Color.gray); Stretch(namePlaceholder.rectTransform, 2f); namePlaceholder.text = "Oyuncu adı"; whisperNameInput.placeholder = namePlaceholder;

            RectTransform close = Rect(whisperWindow, "Close", Vector2.one, Vector2.one, new Vector2(-10f, -12f), new Vector2(16f, 16f), Vector2.one);
            SpriteButton(close, "Metin2UI/Windows/game/public/close_button", () => whisperWindow.gameObject.SetActive(false));
            RectTransform minimize = Rect(whisperWindow, "Minimize", Vector2.one, Vector2.one, new Vector2(-28f, -12f), new Vector2(16f, 16f), Vector2.one);
            SpriteButton(minimize, "Metin2UI/Windows/game/public/minimize_button", () => whisperWindow.gameObject.SetActive(false));

            RectTransform history = Rect(whisperWindow, "Message History", Vector2.up, Vector2.one, new Vector2(10f, -41f), new Vector2(-28f, -105f), Vector2.up);
            Image historyBg = history.gameObject.AddComponent<Image>(); historyBg.color = new Color(0f, 0f, 0f, 0.28f);
            Text historyText = Label(history, "Hint", 11, TextAnchor.UpperLeft, new Vector2(5f, -4f), new Vector2(-10f, -8f), new Color(0.78f, 0.78f, 0.78f)); Stretch(historyText.rectTransform, 4f); historyText.text = "Özel mesaj geçmişi burada görünür.";

            RectTransform editRoot = Rect(whisperWindow, "Edit Bar", Vector2.zero, Vector2.right, new Vector2(10f, 10f), new Vector2(-18f, 50f));
            Image editBg = editRoot.gameObject.AddComponent<Image>(); editBg.color = new Color(0f, 0f, 0f, 0.76f);
            whisperMessageInput = editRoot.gameObject.AddComponent<InputField>();
            Text messageText = Label(editRoot, "Text", 11, TextAnchor.UpperLeft, new Vector2(5f, -5f), new Vector2(-68f, -10f)); Stretch(messageText.rectTransform, 4f); whisperMessageInput.textComponent = messageText; whisperMessageInput.lineType = InputField.LineType.MultiLineNewline;
            RectTransform send = Rect(editRoot, "Send", Vector2.right, Vector2.right, new Vector2(-5f, 10f), new Vector2(55f, 28f), Vector2.right);
            Button(send, SendWhisper); Text sendText = Label(send, "Label", 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(sendText.rectTransform, 2f); sendText.text = "GÖNDER";
            whisperWindow.gameObject.SetActive(false);
        }

        void OpenChatInput()
        {
            chatInput.gameObject.SetActive(true); chatInput.ActivateInputField(); chatInput.Select(); IsTyping = true;
        }

        void CloseChatInput()
        {
            IsTyping = false; chatInput.DeactivateInputField(); chatInput.gameObject.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void SubmitChat()
        {
            string value = chatInput.text; chatInput.text = string.Empty; Metin2ChatService.Submit(value, channel); CloseChatInput();
        }

        void CycleChannel()
        {
            channel = channel == Metin2ChatChannel.Talking ? Metin2ChatChannel.Party : channel == Metin2ChatChannel.Party ? Metin2ChatChannel.Guild : channel == Metin2ChatChannel.Guild ? Metin2ChatChannel.Shout : Metin2ChatChannel.Talking;
            UpdateChannelLabel();
        }

        void UpdateChannelLabel() => channelLabel.text = channel == Metin2ChatChannel.Talking ? "NORMAL" : channel == Metin2ChatChannel.Party ? "GRUP" : channel == Metin2ChatChannel.Guild ? "LONCA" : "DUYURU";
        void OnMessage(Metin2ChatEntry entry) => chatDirty = true;
        void OnQuestChanged() => questDirty = true;

        void RefreshChat()
        {
            chatDirty = false; StringBuilder builder = new StringBuilder();
            foreach (Metin2ChatEntry entry in Metin2ChatService.Entries.Skip(Mathf.Max(0, Metin2ChatService.Entries.Count - 8)))
            {
                string senderColor = SenderColor(entry);
                builder.Append("<color=").Append(senderColor).Append('>')
                    .Append(EscapeRichText(entry.sender)).Append(":</color> ")
                    .Append("<color=#FFFFFFFF>").Append(EscapeRichText(entry.text)).AppendLine("</color>");
            }
            chatHistory.text = builder.ToString();
        }

        static string SenderColor(Metin2ChatEntry entry)
        {
            if (entry.channel == Metin2ChatChannel.Info) return "#FFD36AFF";
            if (entry.channel == Metin2ChatChannel.Party) return "#72D9FFFF";
            if (entry.channel == Metin2ChatChannel.Guild) return "#7EFF8BFF";
            if (entry.channel == Metin2ChatChannel.Shout) return "#FFE75CFF";
            if (entry.channel == Metin2ChatChannel.Whisper) return "#FF7FE7FF";
            int hash = (entry.sender ?? string.Empty).GetHashCode();
            string[] colors = { "#68C5FFFF", "#FF9D6CFF", "#A7E56BFF", "#D898FFFF", "#6FE0C1FF" };
            return colors[Mathf.Abs(hash % colors.Length)];
        }

        static string EscapeRichText(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        void RefreshQuests()
        {
            questDirty = false; StringBuilder compact = new StringBuilder(); StringBuilder full = new StringBuilder();
            foreach (Metin2QuestState quest in Metin2QuestService.Quests)
            {
                string state = quest.rewarded ? "[ALINDI]" : quest.completed ? "[TAMAM]" : $"{quest.progress}/{quest.required}";
                compact.Append("• ").Append(quest.title).Append("  ").AppendLine(state);
                full.Append(quest.title).Append("  ").AppendLine(state).AppendLine(quest.description).Append("Ödül: ").Append(quest.rewardExperience).Append(" EXP, ").Append(quest.rewardGold).AppendLine(" Yang").AppendLine();
            }
            questTrackerText.text = compact.ToString(); questWindowText.text = full.ToString();
        }

        void RefreshMessenger()
        {
            if (messengerList == null) return; StringBuilder builder = new StringBuilder();
            foreach (Metin2MessengerContact contact in Metin2MessengerService.Contacts) builder.Append(contact.online ? "● " : "○ ").AppendLine(contact.name);
            if (builder.Length == 0) builder.Append("Henüz arkadaş eklenmedi."); messengerList.text = builder.ToString();
        }

        void AddFriend() { Metin2MessengerService.Add(friendInput.text); friendInput.text = string.Empty; }
        void RemoveFriend()
        {
            string name = friendInput.text;
            if (string.IsNullOrWhiteSpace(name) && Metin2MessengerService.Contacts.Count > 0) name = Metin2MessengerService.Contacts.Last().name;
            if (Metin2MessengerService.Remove(name)) friendInput.text = string.Empty;
        }
        void OpenWhisperForFriend()
        {
            string name = friendInput.text;
            if (string.IsNullOrWhiteSpace(name) && Metin2MessengerService.Contacts.Count > 0) name = Metin2MessengerService.Contacts[0].name;
            whisperNameInput.text = name ?? string.Empty;
            whisperWindow.gameObject.SetActive(true);
            whisperWindow.SetAsLastSibling();
            whisperMessageInput.ActivateInputField();
        }
        void SendWhisper()
        {
            string name = (whisperNameInput.text ?? string.Empty).Trim();
            string message = (whisperMessageInput.text ?? string.Empty).Trim();
            if (name.Length == 0 || message.Length == 0) return;
            Metin2ChatService.Append(Metin2ChatChannel.Whisper, message, "-> " + name);
            whisperMessageInput.text = string.Empty;
        }
        void ShowMobileContacts() { Metin2ChatService.Append(Metin2ChatChannel.Info, "Mobil arkadaş listesi seçildi."); }
        void ShowGuildContacts() { Metin2ChatService.Append(Metin2ChatChannel.Info, "Lonca üye listesi seçildi."); }
        void ClaimFirstCompleted() { Metin2QuestState quest = Metin2QuestService.Quests.FirstOrDefault(item => item.completed && !item.rewarded); if (quest != null) Metin2QuestService.Claim(quest.id); }
        public void ToggleMessenger() { messengerWindow.gameObject.SetActive(!messengerWindow.gameObject.activeSelf); if (messengerWindow.gameObject.activeSelf) messengerWindow.SetAsLastSibling(); }
        public void ToggleQuests() { questWindow.gameObject.SetActive(!questWindow.gameObject.activeSelf); if (questWindow.gameObject.activeSelf) questWindow.SetAsLastSibling(); }
        void OnTargetChanged(Metin2MobCombatant target) { selectedTarget = target; targetBoard.gameObject.SetActive(target != null && !target.IsDead); }

        RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, anchor, anchor, position, size, pivot); Image image = rect.gameObject.AddComponent<Image>(); image.color = board; Outline outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = border; outline.effectDistance = new Vector2(1f, -1f); return rect;
        }
        RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform)); gameObject.transform.SetParent(parent, false); RectTransform rect = (RectTransform)gameObject.transform; rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot ?? anchorMin; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        Text Label(Transform parent, string name, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions, Color? color = null)
        {
            RectTransform rect = Rect(parent, name, Vector2.up, Vector2.one, position, dimensions, Vector2.up); Text text = rect.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size; text.alignment = alignment; text.color = color ?? Color.white; text.supportRichText = true; return text;
        }
        Button Button(RectTransform rect, UnityEngine.Events.UnityAction action)
        {
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>(); image.color = new Color(0.18f, 0.11f, 0.045f, 1f); Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action); return button;
        }
        void SpriteButton(Transform parent, string name, Vector2 position, string imageName, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero, position, new Vector2(24f, 24f));
            SpriteButton(rect, "Metin2UI/Windows/game/windows/" + imageName, action);
        }
        Button SpriteButton(RectTransform rect, string resourceBase, UnityEngine.Events.UnityAction action)
        {
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(resourceBase + "_01");
            image.preserveAspect = true;
            image.color = Color.white;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            SpriteState state = button.spriteState;
            state.highlightedSprite = Resources.Load<Sprite>(resourceBase + "_02");
            state.pressedSprite = Resources.Load<Sprite>(resourceBase + "_03");
            state.disabledSprite = Resources.Load<Sprite>(resourceBase + "_04");
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
            return button;
        }
        static void Stretch(RectTransform rect, float margin) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * margin; rect.offsetMax = Vector2.one * -margin; }
    }
}

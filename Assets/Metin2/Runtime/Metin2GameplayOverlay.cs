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
            if (selectedTarget == null || selectedTarget.IsDead || selectedTarget.UsesLegacyTargetUi) targetBoard.gameObject.SetActive(false);
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
            chatPanel = Panel(canvas.transform, "Chat", Vector2.zero, Vector2.zero, new Vector2(14f, 58f), new Vector2(470f, 158f));
            chatHistory = Label(chatPanel, "History", 12, TextAnchor.LowerLeft, new Vector2(10f, 34f), new Vector2(-20f, -44f));
            chatHistory.horizontalOverflow = HorizontalWrapMode.Wrap;
            chatHistory.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform modeButton = Rect(chatPanel, "Mode", Vector2.zero, Vector2.zero, new Vector2(8f, 7f), new Vector2(76f, 24f));
            Button mode = Button(modeButton, CycleChannel);
            channelLabel = Label(modeButton, "Label", 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(channelLabel.rectTransform, 1f);
            RectTransform inputRoot = Rect(chatPanel, "Input", Vector2.zero, Vector2.right, new Vector2(90f, 7f), new Vector2(-98f, 24f));
            Image inputBackground = inputRoot.gameObject.AddComponent<Image>(); inputBackground.color = new Color(0f, 0f, 0f, 0.82f);
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
            messengerWindow = Panel(canvas.transform, "Messenger Window", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(300f, 420f));
            Text title = Label(messengerWindow, "Title", 16, TextAnchor.UpperCenter, new Vector2(10f, -10f), new Vector2(-20f, 30f)); title.text = "ARKADAŞLAR (M)";
            messengerList = Label(messengerWindow, "List", 12, TextAnchor.UpperLeft, new Vector2(18f, -56f), new Vector2(-36f, -120f));
            RectTransform input = Rect(messengerWindow, "Friend Input", Vector2.zero, Vector2.right, new Vector2(14f, 62f), new Vector2(-92f, 28f));
            Image inputBg = input.gameObject.AddComponent<Image>(); inputBg.color = Color.black;
            friendInput = input.gameObject.AddComponent<InputField>(); Text friendText = Label(input, "Text", 12, TextAnchor.MiddleLeft, new Vector2(5f, 0f), new Vector2(-10f, 0f)); Stretch(friendText.rectTransform, 3f); friendInput.textComponent = friendText;
            RectTransform add = Rect(messengerWindow, "Add", Vector2.right, Vector2.right, new Vector2(-14f, 62f), new Vector2(68f, 28f));
            Button(add, AddFriend); Text addText = Label(add, "Label", 11, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero); Stretch(addText.rectTransform, 2f); addText.text = "EKLE";
            messengerWindow.gameObject.SetActive(false);
            RefreshMessenger();
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
            foreach (Metin2ChatEntry entry in Metin2ChatService.Entries.Skip(Mathf.Max(0, Metin2ChatService.Entries.Count - 7))) builder.Append('[').Append(entry.channel).Append("] ").Append(entry.sender).Append(": ").AppendLine(entry.text);
            chatHistory.text = builder.ToString();
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
        void ClaimFirstCompleted() { Metin2QuestState quest = Metin2QuestService.Quests.FirstOrDefault(item => item.completed && !item.rewarded); if (quest != null) Metin2QuestService.Claim(quest.id); }
        public void ToggleMessenger() { messengerWindow.gameObject.SetActive(!messengerWindow.gameObject.activeSelf); if (messengerWindow.gameObject.activeSelf) messengerWindow.SetAsLastSibling(); }
        public void ToggleQuests() { questWindow.gameObject.SetActive(!questWindow.gameObject.activeSelf); if (questWindow.gameObject.activeSelf) questWindow.SetAsLastSibling(); }
        void OnTargetChanged(Metin2MobCombatant target) { selectedTarget = target; targetBoard.gameObject.SetActive(target != null && !target.IsDead && !target.UsesLegacyTargetUi); }

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
        static void Stretch(RectTransform rect, float margin) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * margin; rect.offsetMax = Vector2.one * -margin; }
    }
}

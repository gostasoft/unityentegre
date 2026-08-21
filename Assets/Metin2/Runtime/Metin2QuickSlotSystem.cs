using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Metin2Dev.Gameplay
{
    public enum Metin2QuickSlotEntryType { Empty, Skill, Item }

    public sealed class Metin2QuickSlotEntry
    {
        public Metin2QuickSlotEntryType Type { get; }
        public int SourceIndex { get; }
        public string DisplayName { get; }
        public Texture IconTexture { get; }
        public Rect IconUv { get; }
        public Action UseItem { get; }
        public bool IsEmpty => Type == Metin2QuickSlotEntryType.Empty;

        internal Metin2QuickSlotEntry(Metin2QuickSlotEntryType type, int sourceIndex, string displayName,
            Texture iconTexture, Rect iconUv, Action useItem)
        {
            Type = type;
            SourceIndex = sourceIndex;
            DisplayName = displayName ?? string.Empty;
            IconTexture = iconTexture;
            IconUv = iconUv;
            UseItem = useItem;
        }

        internal static Metin2QuickSlotEntry Empty()
        {
            return new Metin2QuickSlotEntry(Metin2QuickSlotEntryType.Empty, -1, string.Empty,
                null, new Rect(0f, 0f, 1f, 1f), null);
        }
    }

    /// <summary>Shared, initially empty assignment state for desktop and mobile quick slots.</summary>
    public static class Metin2QuickSlotSystem
    {
        public const int SlotCount = 8;
        static readonly Metin2QuickSlotEntry[] entries = CreateEmptyEntries();
        public static event Action<int> Changed;
        public static event Action<Metin2QuickSlotEntry> ItemUseRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            for (int index = 0; index < entries.Length; index++)
                entries[index] = Metin2QuickSlotEntry.Empty();
            Changed = null;
            ItemUseRequested = null;
            Metin2QuickSlotDragSource.ResetDragState();
        }

        static Metin2QuickSlotEntry[] CreateEmptyEntries()
        {
            Metin2QuickSlotEntry[] result = new Metin2QuickSlotEntry[SlotCount];
            for (int index = 0; index < result.Length; index++) result[index] = Metin2QuickSlotEntry.Empty();
            return result;
        }

        public static Metin2QuickSlotEntry Get(int slotIndex)
        {
            return IsValid(slotIndex) ? entries[slotIndex] : Metin2QuickSlotEntry.Empty();
        }

        public static void Assign(int slotIndex, Metin2QuickSlotEntry entry)
        {
            if (!IsValid(slotIndex) || entry == null || entry.IsEmpty) return;
            entries[slotIndex] = entry;
            Changed?.Invoke(slotIndex);
        }

        public static void Clear(int slotIndex)
        {
            if (!IsValid(slotIndex)) return;
            entries[slotIndex] = Metin2QuickSlotEntry.Empty();
            Changed?.Invoke(slotIndex);
        }

        public static void ClearAll()
        {
            for (int index = 0; index < entries.Length; index++)
            {
                entries[index] = Metin2QuickSlotEntry.Empty();
                Changed?.Invoke(index);
            }
        }

        public static bool Activate(int slotIndex, Metin2PlayerController player = null)
        {
            if (!IsValid(slotIndex)) return false;
            Metin2QuickSlotEntry entry = entries[slotIndex];
            if (entry == null || entry.IsEmpty) return false;
            if (entry.Type == Metin2QuickSlotEntryType.Skill)
            {
                if (player == null) player = UnityEngine.Object.FindFirstObjectByType<Metin2PlayerController>();
                if (player == null) return false;
                player.ActivateSkill(entry.SourceIndex);
                return true;
            }
            if (entry.UseItem != null) entry.UseItem();
            else ItemUseRequested?.Invoke(entry);
            return true;
        }

        static bool IsValid(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;
    }

    [DisallowMultipleComponent]
    public sealed class Metin2QuickSlotDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static Metin2QuickSlotDragSource activeSource;
        static Metin2QuickSlotEntry activeEntry;
        static RectTransform dragGhost;
        Metin2QuickSlotEntry entry;

        public static Metin2QuickSlotEntry ActiveEntry => activeEntry;
        public bool HasEntry => entry != null && !entry.IsEmpty;

        public void ConfigureSkill(int skillIndex, string displayName, Texture iconTexture, Rect iconUv)
        {
            entry = new Metin2QuickSlotEntry(Metin2QuickSlotEntryType.Skill, skillIndex, displayName,
                iconTexture, iconUv, null);
        }

        public void ConfigureItem(int itemIndex, string displayName, Texture iconTexture, Rect iconUv,
            Action useItem = null)
        {
            entry = new Metin2QuickSlotEntry(Metin2QuickSlotEntryType.Item, itemIndex, displayName,
                iconTexture, iconUv, useItem);
        }

        public void ConfigureItem(int itemIndex, string displayName, Sprite icon, Action useItem = null)
        {
            Texture texture = icon != null ? icon.texture : null;
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (icon != null && icon.texture != null)
            {
                Rect rect = icon.textureRect;
                uv = new Rect(rect.x / icon.texture.width, rect.y / icon.texture.height,
                    rect.width / icon.texture.width, rect.height / icon.texture.height);
            }
            ConfigureItem(itemIndex, displayName, texture, uv, useItem);
        }

        public void Configure(Metin2QuickSlotEntry assignedEntry)
        {
            entry = assignedEntry == null || assignedEntry.IsEmpty ? null : assignedEntry;
        }

        public void Clear() => entry = null;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!HasEntry) return;
            activeSource = this;
            activeEntry = entry;
            CreateDragGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (activeSource == this && dragGhost != null) dragGhost.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (activeSource == this) ResetDragState();
        }

        void OnDisable()
        {
            if (activeSource == this) ResetDragState();
        }

        void CreateDragGhost(PointerEventData eventData)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            GameObject ghost = new GameObject("Quick Slot Drag Icon", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage), typeof(CanvasGroup));
            dragGhost = ghost.GetComponent<RectTransform>();
            dragGhost.SetParent(canvas.rootCanvas.transform, false);
            dragGhost.sizeDelta = new Vector2(48f, 48f);
            dragGhost.position = eventData.position;
            dragGhost.SetAsLastSibling();
            RawImage image = ghost.GetComponent<RawImage>();
            image.texture = entry.IconTexture;
            image.uvRect = entry.IconUv;
            image.color = entry.IconTexture != null ? Color.white : new Color(0.55f, 0.48f, 0.32f, 0.9f);
            image.raycastTarget = false;
            CanvasGroup group = ghost.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.9f;
        }

        internal static void ResetDragState()
        {
            if (dragGhost != null) UnityEngine.Object.Destroy(dragGhost.gameObject);
            dragGhost = null;
            activeEntry = null;
            activeSource = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Metin2QuickSlotView : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField, Range(0, Metin2QuickSlotSystem.SlotCount - 1)] int slotIndex;
        [SerializeField] RawImage assignedIcon;
        [SerializeField] Image emptyCover;
        Metin2QuickSlotDragSource dragSource;
        bool listening;

        public void Configure(int configuredSlot, RawImage icon, Image cover)
        {
            slotIndex = Mathf.Clamp(configuredSlot, 0, Metin2QuickSlotSystem.SlotCount - 1);
            assignedIcon = icon;
            emptyCover = cover;
            EnsureListening();
            Refresh();
        }

        public static Metin2QuickSlotView EnsureMobile(Transform slotRoot, int configuredSlot)
        {
            if (slotRoot == null) return null;
            Metin2QuickSlotView view = slotRoot.GetComponent<Metin2QuickSlotView>();
            if (view == null) view = slotRoot.gameObject.AddComponent<Metin2QuickSlotView>();
            Image cover = FindOrCreateCover(slotRoot);
            RawImage icon = FindOrCreateIcon(slotRoot, cover.transform);
            view.Configure(configuredSlot, icon, cover);
            return view;
        }

        static Image FindOrCreateCover(Transform parent)
        {
            Transform existing = parent.Find("Quick Slot Empty Cover");
            Image cover = existing != null ? existing.GetComponent<Image>() : null;
            if (cover == null)
            {
                GameObject child = new GameObject("Quick Slot Empty Cover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = child.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                cover = child.GetComponent<Image>();
                cover.raycastTarget = false;
            }
            Image authoredFrame = parent.GetComponent<Image>();
            if (authoredFrame != null)
            {
                cover.sprite = authoredFrame.sprite;
                cover.type = authoredFrame.type;
                cover.preserveAspect = authoredFrame.preserveAspect;
            }
            cover.color = Color.white;
            Mask mask = cover.GetComponent<Mask>();
            if (mask == null) mask = cover.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            cover.transform.SetAsLastSibling();
            return cover;
        }

        static RawImage FindOrCreateIcon(Transform slotRoot, Transform maskParent)
        {
            Transform existing = maskParent.Find("Quick Slot Assigned Icon");
            if (existing == null) existing = slotRoot.Find("Quick Slot Assigned Icon");
            RawImage icon = existing != null ? existing.GetComponent<RawImage>() : null;
            if (icon == null)
            {
                GameObject child = new GameObject("Quick Slot Assigned Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                icon = child.GetComponent<RawImage>();
                icon.raycastTarget = false;
            }
            RectTransform rect = icon.rectTransform;
            rect.SetParent(maskParent, false);
            rect.anchorMin = new Vector2(0.14f, 0.14f);
            rect.anchorMax = new Vector2(0.86f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            icon.transform.SetAsLastSibling();
            return icon;
        }

        void OnEnable()
        {
            EnsureListening();
            Refresh();
        }

        void OnDisable()
        {
            if (!listening) return;
            Metin2QuickSlotSystem.Changed -= OnSlotChanged;
            listening = false;
        }

        void EnsureListening()
        {
            if (listening) return;
            Metin2QuickSlotSystem.Changed += OnSlotChanged;
            listening = true;
        }

        void OnSlotChanged(int changedSlot)
        {
            if (changedSlot == slotIndex) Refresh();
        }

        void Refresh()
        {
            Metin2QuickSlotEntry entry = Metin2QuickSlotSystem.Get(slotIndex);
            if (emptyCover != null) emptyCover.enabled = true;
            if (assignedIcon != null)
            {
                assignedIcon.texture = entry.IconTexture;
                assignedIcon.uvRect = entry.IconUv;
                assignedIcon.enabled = !entry.IsEmpty && entry.IconTexture != null;
            }
            if (dragSource == null) dragSource = GetComponent<Metin2QuickSlotDragSource>();
            if (dragSource == null) dragSource = gameObject.AddComponent<Metin2QuickSlotDragSource>();
            dragSource.Configure(entry);
        }

        public void OnDrop(PointerEventData eventData)
        {
            Metin2QuickSlotEntry payload = Metin2QuickSlotDragSource.ActiveEntry;
            if (payload != null && !payload.IsEmpty) Metin2QuickSlotSystem.Assign(slotIndex, payload);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) Metin2QuickSlotSystem.Clear(slotIndex);
        }
    }
}

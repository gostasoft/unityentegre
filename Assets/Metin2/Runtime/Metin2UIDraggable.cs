using UnityEngine;
using UnityEngine.EventSystems;

namespace Metin2Dev.Gameplay
{
    public sealed class Metin2UIDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform target;
        Vector2 pointerOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null) target = transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(target.parent as RectTransform,
                eventData.position, eventData.pressEventCamera, out Vector2 pointer);
            pointerOffset = target.anchoredPosition - pointer;
            target.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null || target.parent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(target.parent as RectTransform,
                    eventData.position, eventData.pressEventCamera, out Vector2 pointer))
                target.anchoredPosition = pointer + pointerOffset;
        }
    }
}

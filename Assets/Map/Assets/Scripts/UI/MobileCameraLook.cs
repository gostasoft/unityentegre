using UnityEngine;
using UnityEngine.EventSystems;

public class MobileCameraLook :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [Header("Kamera Hassasiyeti")]
    [SerializeField] private float sensitivity = 0.12f;

    public Vector2 LookDelta
    {
        get;
        private set;
    }

    private Vector2 lastPointerPosition;
    private bool dragging;

    public void OnPointerDown(
        PointerEventData eventData
    )
    {
        dragging = true;

        lastPointerPosition =
            eventData.position;

        LookDelta =
            Vector2.zero;
    }

    public void OnDrag(
        PointerEventData eventData
    )
    {
        if (!dragging)
            return;

        Vector2 currentPosition =
            eventData.position;

        Vector2 delta =
            currentPosition -
            lastPointerPosition;

        lastPointerPosition =
            currentPosition;

        LookDelta =
            delta *
            sensitivity;
    }

    public void OnPointerUp(
        PointerEventData eventData
    )
    {
        dragging = false;
        LookDelta = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!dragging)
        {
            LookDelta =
                Vector2.zero;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

public class MobileJoystick :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [SerializeField]
    private RectTransform background;

    [SerializeField]
    private RectTransform handle;

    [Range(0.1f, 1f)]
    [SerializeField]
    private float handleRange = 0.55f;

    public Vector2 Direction
    {
        get;
        private set;
    }

    private Canvas canvas;
    private Camera uiCamera;

    public void Configure(
        RectTransform backgroundRect,
        RectTransform handleRect)
    {
        background = backgroundRect;
        handle = handleRect;
        CacheCanvas();
        ResetJoystick();
    }

    private void Awake()
    {
        if (background == null)
            background = transform as RectTransform;

        if (handle == null)
        {
            RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform child in children)
            {
                if (child != background &&
                    (child.name.IndexOf("handle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     child.name.IndexOf("knob", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    handle = child;
                    break;
                }
            }
        }

        CacheCanvas();

        ResetJoystick();
    }

    private void CacheCanvas()
    {
        canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    public void OnPointerDown(
        PointerEventData eventData
    )
    {
        OnDrag(eventData);
    }

    public void OnDrag(
        PointerEventData eventData
    )
    {
        if (background == null ||
            handle == null)
        {
            return;
        }

        Vector2 localPoint;

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    background,
                    eventData.position,
                    uiCamera,
                    out localPoint))
        {
            return;
        }

        Vector2 halfSize =
            background.rect.size * 0.5f;

        Vector2 normalized =
            new Vector2(
                localPoint.x /
                Mathf.Max(halfSize.x, 0.001f),

                localPoint.y /
                Mathf.Max(halfSize.y, 0.001f)
            );

        if (normalized.magnitude > 1f)
        {
            normalized.Normalize();
        }

        Direction =
            normalized;

        Vector2 handlePosition =
            new Vector2(
                Direction.x *
                halfSize.x *
                handleRange,

                Direction.y *
                halfSize.y *
                handleRange
            );

        handle.anchoredPosition =
            handlePosition;
    }

    public void OnPointerUp(
        PointerEventData eventData
    )
    {
        ResetJoystick();
    }

    private void ResetJoystick()
    {
        Direction =
            Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition =
                Vector2.zero;
        }
    }

    private void OnDisable()
    {
        ResetJoystick();
    }
}

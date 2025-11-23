using UnityEngine;
using UnityEngine.EventSystems;

public class WorldSpaceJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Elements")]
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;

    [Header("Settings")]
    [Range(0f, 1f)] public float handleRange = 1f;
    [Range(0f, 1f)] public float deadZone = 0f;

    private Canvas canvas;
    private Camera cam;
    private Vector2 input = Vector2.zero;

    public float Horizontal => input.x;
    public float Vertical => input.y;
    public Vector2 Direction => input;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogError("Joystick must be inside a World Space Canvas!");
        }

        cam = canvas.worldCamera;

        // Handle ortada başlasın
        handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;

        // World Space Canvas → doğru dönüşüm
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            cam,
            out localPoint))
        {
            Vector2 radius = background.sizeDelta / 2f;

            input = localPoint / radius;
            input = Vector2.ClampMagnitude(input, 1f);

            if (input.magnitude < deadZone)
                input = Vector2.zero;

            handle.anchoredPosition = input * radius * handleRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        input = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    // Public helper to explicitly center the handle (useful if layout changes after Start)
    public void CenterHandle()
    {
        input = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }
}

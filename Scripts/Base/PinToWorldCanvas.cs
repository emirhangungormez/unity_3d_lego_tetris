using UnityEngine;

/// <summary>
/// Attach to a UI element (e.g. joystick) that should be parented/pinned to a World-Space Canvas at runtime.
/// If a target Canvas is not assigned, the script will search for the first Canvas with RenderMode = WorldSpace.
/// </summary>
[DisallowMultipleComponent]
public class PinToWorldCanvas : MonoBehaviour
{
    [Tooltip("Optional: explicit target Canvas. If null, first World Space canvas in scene will be used.")]
    public Canvas targetCanvas;

    [Tooltip("If true, when parenting to the canvas the element will snap to the canvas transform local position (0,0,0). Otherwise it preserves its world position.")]
    public bool snapToCanvasLocalZero = false;

    [Tooltip("If true and no World Space canvas is found, this GameObject will remain where it is. Otherwise a warning is logged.")]
    public bool allowNoCanvas = false;

    void Start()
    {
        if (targetCanvas == null)
        {
            var all = FindObjectsOfType<Canvas>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].renderMode == RenderMode.WorldSpace)
                {
                    targetCanvas = all[i];
                    break;
                }
            }
        }

        if (targetCanvas == null)
        {
            if (!allowNoCanvas)
                Debug.LogWarning($"PinToWorldCanvas: no World Space Canvas found to pin '{name}' to.");
            return;
        }

        // Parent to canvas so RectTransform.GetComponentInParent finds the canvas
        var rect = transform as RectTransform;
        if (rect == null)
        {
            // allow non-UI objects too
            transform.SetParent(targetCanvas.transform, true);
        }
        else
        {
            // Preserve world position by default
            transform.SetParent(targetCanvas.transform, true);
            if (snapToCanvasLocalZero)
            {
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }
        }
    }
}

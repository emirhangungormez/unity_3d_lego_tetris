using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kamera için OverlayCanvas/OverlayImage oluşturan yardımcı sınıf.
/// Hem MenuManager hem de CameraController aynı şekilde overlay oluşturmak için bunu kullanır.
/// </summary>
public static class OverlayCreator
{
    /// <summary>
    /// Kameranın child'ı olarak bir OverlayCanvas oluşturur ve içindeki Image komponentini döner.
    /// </summary>
    public static GameObject CreateOverlay(Camera cam, out Image imageComponent, float distance, Vector3 scale, Vector3 rotation, bool maintainAspect)
    {
        imageComponent = null;
        if (cam == null)
        {
            Debug.LogError("OverlayCreator: camera null");
            return null;
        }

        GameObject canvasObj = new GameObject("OverlayCanvas");
        canvasObj.transform.SetParent(cam.transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasRenderer>();

        GameObject imageObj = new GameObject("OverlayImage");
        imageObj.transform.SetParent(canvasObj.transform);
        RectTransform rect = imageObj.AddComponent<RectTransform>();
        rect.pivot = rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(100f, 100f);

        imageComponent = imageObj.AddComponent<Image>();
        imageComponent.preserveAspect = maintainAspect;
        imageComponent.raycastTarget = false;
        imageObj.transform.localRotation = Quaternion.Euler(rotation);

        canvasObj.transform.localScale = scale;
        canvasObj.transform.position = cam.transform.position + cam.transform.forward * distance;
        canvasObj.transform.rotation = Quaternion.LookRotation(cam.transform.forward);

        return canvasObj;
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public GridManager gridManager;
    
    [Header("Rotation")]
    public float rotationSpeed = 2f;
    public float verticalAngleLimit = 80f;
    public float downwardAngleLimit = 10f;
    public float rotationSmoothness = 5f;
    
    [Header("Camera Height")]
    public float heightOffset = 3f; // YENİ: Kamera yükseklik ofseti
    
    [Header("Reset")]
    public float resetSmoothness = 2f;
    public Vector3 defaultPosition = new Vector3(17.45f, 11.9f, -9.57f);
    public Quaternion defaultRotation = Quaternion.Euler(11.5f, -43.9f, 0f);
    
    [Header("Overlay Image")]
    [Tooltip("Kullanılacak background sprite'ları. Menu'deki ile aynı sırada olmalı.")]
    public List<Sprite> backgroundSprites = new List<Sprite>();
    public float imageDistance = 2f;
    public Vector3 imageScale = new Vector3(0.3f, 0.3f, 0.3f);
    public Vector3 imageRotation = new Vector3(0f, 0f, 90f);
    public bool maintainAspectRatio = true;
    
    private float currentX, currentY, targetX, targetY;
    private bool isResetting;
    private float resetProgress;
    private Vector3 defaultEulerAngles;
    private GameObject imageObject;
    private Canvas imageCanvas;
    private Image imageComponent;
    private RectTransform imageRectTransform;
    private Vector3 currentImageScale, currentImageRotation;
    private bool imageInitialized = false;
    private Vector3 gridCenterOffset = Vector3.zero;
    [Header("Debug")]
    public bool debugLogGridCenter = false;
    [Header("Control")]
    [Tooltip("When false, user input for rotating/moving the camera is ignored. Useful to lock camera after win.")]
    public bool allowInput = true;

    void Start()
    {
        defaultEulerAngles = defaultRotation.eulerAngles;
        currentX = targetX = defaultEulerAngles.y;
        currentY = targetY = defaultEulerAngles.x;
        
        FindAndSetupGridManager();
        // Ensure target exists and is positioned based on current grid before updating camera
        UpdateTargetPosition();
        CreateOverlayImage();
        UpdateCameraPosition();
        ForceUpdateImageTransform();
        imageInitialized = true;
    }
    
    void Update()
    {
        UpdateTargetPosition();
        if (!target) return;
        
        if (!isResetting) HandleInput();
        else HandleCameraReset();
        
        UpdateCameraSmoothly();
        if (imageInitialized) UpdateImagePositionAndRotation();
    }
    
    #region Grid Management
    void FindAndSetupGridManager()
    {
        if (!gridManager) gridManager = FindObjectOfType<GridManager>();
        if (gridManager) CalculateGridCenter();
    }
    
    void CalculateGridCenter()
    {
        if (!gridManager) return;
        gridCenterOffset = new Vector3(gridManager.GridWidth * gridManager.cellSize / 2f, 0f, gridManager.GridHeight * gridManager.cellSize / 2f);
    }
    
    void UpdateTargetPosition()
    {
        if (gridManager)
        {
            // Recalculate center in case grid changed at runtime
            CalculateGridCenter();
            if (!target) target = new GameObject("CameraTarget").transform;
            // YENİ: heightOffset eklendi
            target.position = gridManager.transform.position + gridCenterOffset + Vector3.up * heightOffset;
            if (debugLogGridCenter)
            {
                debugLogGridCenter = false;
            }
        }
    }
    #endregion

    #region Overlay Image
    void CreateOverlayImage()
    {
        // overlay yaratımını ortak helper'a devrediyoruz
        Camera cam = Camera.main;
        GameObject canvasObj = OverlayCreator.CreateOverlay(cam, out imageComponent, imageDistance, imageScale, imageRotation, maintainAspectRatio);
        if (canvasObj != null)
        {
            imageCanvas = canvasObj.GetComponent<Canvas>();
            imageObject = imageComponent != null ? imageComponent.gameObject : null;
            imageRectTransform = imageObject != null ? imageObject.GetComponent<RectTransform>() : null;
        }

        // GameSettings'den kayıtlı background'u otomatik uygula
        ApplyBackgroundFromSettings();

        currentImageScale = imageScale;
        currentImageRotation = imageRotation;
    }
    
    /// <summary>
    /// GameSettings'den kayıtlı background'u uygular (otomatik)
    /// </summary>
    void ApplyBackgroundFromSettings()
    {
        Debug.Log($"CameraController: ApplyBackgroundFromSettings çağrıldı. Index: {GameSettings.BackgroundIndex}, Liste sayısı: {backgroundSprites?.Count ?? 0}");
        
        if (backgroundSprites == null || backgroundSprites.Count == 0)
        {
            imageComponent.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            Debug.LogWarning("CameraController: backgroundSprites listesi boş! Inspector'dan sprite ekleyin.");
            return;
        }
        
        int index = GameSettings.BackgroundIndex;
        if (index >= 0 && index < backgroundSprites.Count && backgroundSprites[index] != null)
        {
            imageComponent.sprite = backgroundSprites[index];
            Debug.Log($"CameraController: Background sprite atandı: {backgroundSprites[index].name}");
        }
        else
        {
            // Fallback: ilk sprite
            imageComponent.sprite = backgroundSprites[0];
            Debug.Log($"CameraController: Fallback sprite kullanıldı: {backgroundSprites[0]?.name}");
        }
    }
    
    void ForceUpdateImageTransform()
    {
        if (!imageObject) return;
        imageCanvas.transform.localScale = imageScale;
        currentImageScale = imageScale;
        imageObject.transform.localRotation = Quaternion.Euler(imageRotation);
        currentImageRotation = imageRotation;
        UpdateImagePositionAndRotation();
    }
    
    void UpdateImageTransform()
    {
        if (!imageObject) return;
        
        if (currentImageScale != imageScale)
        {
            imageCanvas.transform.localScale = imageScale;
            currentImageScale = imageScale;
        }
        
        if (currentImageRotation != imageRotation)
        {
            imageObject.transform.localRotation = Quaternion.Euler(imageRotation);
            currentImageRotation = imageRotation;
        }
        
        UpdateImagePositionAndRotation();
    }
    
    void UpdateImagePositionAndRotation()
    {
        if (!imageCanvas) return;
        imageCanvas.transform.position = transform.position + transform.forward * imageDistance;
        imageCanvas.transform.rotation = Quaternion.LookRotation(transform.forward);
    }
    #endregion

    #region Input Handling
    void HandleInput()
    {
        if (!allowInput) return;
        Vector2 input = GetRotationInput();
        if (input != Vector2.zero)
        {
            targetX += input.x * rotationSpeed;
            targetY -= input.y * rotationSpeed;
            targetY = Mathf.Clamp(targetY, -downwardAngleLimit, verticalAngleLimit);
        }
        if (Input.GetKeyDown(KeyCode.C)) StartCameraReset();
    }
    
    Vector2 GetRotationInput()
    {
        // Check if input is in the bottom 25% of the screen (joystick area) - disable camera rotation
        if (IsInputInBottomQuarter()) return Vector2.zero;
        
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved) return touch.deltaPosition * 0.02f;
        }
        return Vector2.zero;
    }
    
    bool IsInputInBottomQuarter()
    {
        // Mouse input check
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            float mouseY = Input.mousePosition.y;
            float screenHeight = Screen.height;
            return mouseY < screenHeight * 0.25f;
        }
        
        // Touch input check
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            float touchY = touch.position.y;
            float screenHeight = Screen.height;
            return touchY < screenHeight * 0.25f;
        }
        
        return false;
    }
    #endregion

    #region Camera Control
    void UpdateCameraSmoothly()
    {
        if (isResetting)
        {
            resetProgress = Mathf.Clamp01(resetProgress + Time.deltaTime * resetSmoothness);
            float smoothProgress = SmoothStep(resetProgress);
            
            currentX = Mathf.LerpAngle(currentX, targetX, smoothProgress);
            currentY = Mathf.LerpAngle(currentY, targetY, smoothProgress);
            
            if (resetProgress >= 1f)
            {
                isResetting = false;
                currentX = targetX;
                currentY = targetY;
            }
        }
        else
        {
            float rotationLerp = 1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime);
            currentX = Mathf.LerpAngle(currentX, targetX, rotationLerp);
            currentY = Mathf.LerpAngle(currentY, targetY, rotationLerp);
        }
        UpdateCameraPosition();
    }
    
    void UpdateCameraPosition()
    {
        if (!target) return;
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        transform.rotation = rotation;
        transform.position = target.position + rotation * Vector3.back * Vector3.Distance(transform.position, target.position);
    }
    
    void HandleCameraReset()
    {
        resetProgress += Time.deltaTime * resetSmoothness;
        if (resetProgress >= 1f)
        {
            resetProgress = 1f;
            isResetting = false;
            currentX = targetX;
            currentY = targetY;
            UpdateCameraPosition();
        }
    }
    #endregion

    #region Public Methods
    public void OnResetCameraButton() => StartCameraReset();
    
    void StartCameraReset()
    {
        isResetting = true;
        resetProgress = 0f;
        targetX = defaultEulerAngles.y;
        targetY = defaultEulerAngles.x;
        imageScale = new Vector3(0.3f, 0.3f, 0.3f);
        imageRotation = new Vector3(0f, 0f, 90f);
    }
    
    public void SetGridManager(GridManager newGridManager)
    {
        gridManager = newGridManager;
        if (gridManager) CalculateGridCenter();
    }
    
    public void RecalculateGridCenter() => CalculateGridCenter();
    
    public void SetImageScale(Vector3 newScale) { imageScale = newScale; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageScaleX(float x) { imageScale.x = x; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageScaleY(float y) { imageScale.y = y; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageScaleZ(float z) { imageScale.z = z; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageRotation(Vector3 newRotation) { imageRotation = newRotation; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageRotationX(float x) { imageRotation.x = x; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageRotationY(float y) { imageRotation.y = y; if (imageInitialized) UpdateImageTransform(); }
    public void SetImageRotationZ(float z) { imageRotation.z = z; if (imageInitialized) UpdateImageTransform(); }
    
    public void SetOverlayImage(Sprite newImage)
    {
        if (imageComponent)
        {
            imageComponent.sprite = newImage;
            imageComponent.preserveAspect = maintainAspectRatio;
        }
    }
    #endregion

    #region Utility
    float SmoothStep(float t) => t * t * t * (t * (6f * t - 15f) + 10f);
    
    void OnValidate() { if (Application.isPlaying && imageInitialized) UpdateImageTransform(); }
    
    void OnDrawGizmos()
    {
        if (target)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, 0.5f);
            Gizmos.DrawLine(target.position, transform.position);
            if (gridManager)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(target.position, 0.3f);
            }
        }
    }
    #endregion
}
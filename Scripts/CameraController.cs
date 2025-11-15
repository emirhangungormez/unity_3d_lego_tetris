using UnityEngine;
using UnityEngine.UI;

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
    public Sprite overlayImage;
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

    void Start()
    {
        defaultEulerAngles = defaultRotation.eulerAngles;
        currentX = targetX = defaultEulerAngles.y;
        currentY = targetY = defaultEulerAngles.x;
        
        FindAndSetupGridManager();
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
            if (!target) target = new GameObject("CameraTarget").transform;
            // YENİ: heightOffset eklendi
            target.position = gridManager.transform.position + gridCenterOffset + Vector3.up * heightOffset;
        }
    }
    #endregion

    #region Overlay Image
    void CreateOverlayImage()
    {
        imageCanvas = new GameObject("OverlayCanvas").AddComponent<Canvas>();
        imageCanvas.transform.SetParent(transform);
        imageCanvas.renderMode = RenderMode.WorldSpace;
        imageCanvas.gameObject.AddComponent<CanvasRenderer>();
        
        imageObject = new GameObject("OverlayImage");
        imageObject.transform.SetParent(imageCanvas.transform);
        imageRectTransform = imageObject.AddComponent<RectTransform>();
        imageRectTransform.pivot = imageRectTransform.anchorMin = imageRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        imageRectTransform.anchoredPosition = Vector2.zero;
        imageRectTransform.sizeDelta = new Vector2(100f, 100f);
        
        imageComponent = imageObject.AddComponent<Image>();
        if (overlayImage)
        {
            imageComponent.sprite = overlayImage;
            imageComponent.preserveAspect = maintainAspectRatio;
        }
        else
        {
            imageComponent.color = new Color(1f, 0.5f, 0.5f, 0.8f);
        }
        
        currentImageScale = imageScale;
        currentImageRotation = imageRotation;
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
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved) return touch.deltaPosition * 0.02f;
        }
        return Vector2.zero;
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
        overlayImage = newImage;
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
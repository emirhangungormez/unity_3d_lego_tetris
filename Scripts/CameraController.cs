using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    
    [Header("Zoom Settings")]
    public float distance = 15f;
    public float minDistance = 3f;
    public float maxDistance = 30f;
    public float zoomSpeed = 8f;
    public float mobileZoomSensitivity = 0.1f;
    public float zoomSmoothness = 5f;
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 2f;
    public float verticalAngleLimit = 80f;
    public float downwardAngleLimit = 10f;
    public float rotationSmoothness = 5f;
    
    [Header("Reset Settings")]
    public float resetSmoothness = 2f;
    public Vector3 defaultPosition = new Vector3(17.45f, 11.9f, -9.57f);
    public Quaternion defaultRotation = new Quaternion(0.199964f, -0.373612f, 0.082827f, 0.90198f);
    
    private float currentX, currentY;
    private float targetX, targetY;
    private float targetDistance;
    private bool isResetting;
    private float resetProgress;
    private Vector3 defaultEulerAngles;

    void Start()
    {
        defaultEulerAngles = defaultRotation.eulerAngles;
        currentX = targetX = defaultEulerAngles.y;
        currentY = targetY = defaultEulerAngles.x;
        targetDistance = distance;
        UpdateCameraPosition();
    }
    
    void Update()
    {
        if (target == null) 
        {
            Debug.LogWarning("Kamera target'ı bulunamadı!");
            return;
        }
        
        if (!isResetting)
        {
            HandleInput();
        }
        else
        {
            HandleCameraReset();
        }
        
        UpdateCameraSmoothly();
    }
    
    void HandleInput()
    {
        HandleCameraRotation();
        HandleCameraZoom();
        
        if (Input.GetKeyDown(KeyCode.C))
        {
            StartCameraReset();
        }
    }
    
    void HandleCameraRotation()
    {
        Vector2 input = GetRotationInput();
        
        if (input != Vector2.zero)
        {
            targetX += input.x * rotationSpeed;
            targetY -= input.y * rotationSpeed;
            targetY = Mathf.Clamp(targetY, -downwardAngleLimit, verticalAngleLimit);
        }
    }
    
    Vector2 GetRotationInput()
    {
        Vector2 input = Vector2.zero;
        
        // Mouse input
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            input = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }
        // Touch input
        else if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                input = touch.deltaPosition * 0.02f;
            }
        }
        
        return input;
    }
    
    void HandleCameraZoom()
    {
        float zoomInput = GetZoomInput();
        
        if (zoomInput != 0)
        {
            targetDistance -= zoomInput;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }
    
    float GetZoomInput()
    {
        // Mouse wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0) return scroll * zoomSpeed;
        
        // Mobile pinch zoom
        if (Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
            Vector2 touch2PrevPos = touch2.position - touch2.deltaPosition;
            
            float prevMagnitude = (touch1PrevPos - touch2PrevPos).magnitude;
            float currentMagnitude = (touch1.position - touch2.position).magnitude;
            
            return (prevMagnitude - currentMagnitude) * mobileZoomSensitivity;
        }
        
        // Keyboard
        if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.Equals)) return zoomSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Minus)) return -zoomSpeed * Time.deltaTime;
        
        return 0;
    }
    
    // UI BUTON FONKSİYONU
    public void OnResetCameraButton()
    {
        StartCameraReset();
    }
    
    void StartCameraReset()
    {
        isResetting = true;
        resetProgress = 0f;
        targetX = defaultEulerAngles.y;
        targetY = defaultEulerAngles.x;
        targetDistance = 15f;
        
        Debug.Log($"🔄 Kamera sıfırlanıyor...");
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
            distance = targetDistance;
            UpdateCameraPosition();
            
            Debug.Log($"✅ Kamera sıfırlandı: distance={distance}");
        }
    }
    
    void UpdateCameraSmoothly()
    {
        if (isResetting)
        {
            float smoothProgress = SmoothStep(resetProgress);
            currentX = Mathf.LerpAngle(currentX, targetX, smoothProgress);
            currentY = Mathf.LerpAngle(currentY, targetY, smoothProgress);
            distance = Mathf.Lerp(distance, targetDistance, smoothProgress);
        }
        else
        {
            currentX = Mathf.LerpAngle(currentX, targetX, rotationSmoothness * Time.deltaTime);
            currentY = Mathf.LerpAngle(currentY, targetY, rotationSmoothness * Time.deltaTime);
            distance = Mathf.Lerp(distance, targetDistance, zoomSmoothness * Time.deltaTime);
        }
        
        UpdateCameraPosition();
    }
    
    void UpdateCameraPosition()
    {
        if (target == null) return;
        
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 direction = rotation * Vector3.back;
        Vector3 desiredPosition = target.position + direction * distance;
        
        transform.rotation = rotation;
        transform.position = desiredPosition;
    }
    
    float SmoothStep(float t) => t * t * t * (t * (6f * t - 15f) + 10f);
    
    void OnValidate()
    {
        if (target != null && Application.isPlaying)
        {
            UpdateCameraPosition();
        }
    }
    
    void OnDrawGizmos()
    {
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, 0.5f);
            Gizmos.DrawLine(target.position, transform.position);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(target.position, distance);
        }
    }
}
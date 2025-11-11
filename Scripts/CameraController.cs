using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Transform target;
    public float distance = 15f;
    public float minDistance = 5f;
    public float maxDistance = 25f;
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 2f;
<<<<<<< HEAD
    public float verticalAngleLimit = 80f;
=======
    public float verticalAngleLimit = 80f; // Yukarı sınır
    public float downwardAngleLimit = 10f;  // Aşağı sınır - YENİ!
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
    
    [Header("Smooth Settings")]
    public float rotationSmoothness = 5f;
    public float zoomSmoothness = 5f;
    public float resetSmoothness = 2f;
    
    [Header("Position Settings")]
    public Vector3 defaultPosition = new Vector3(17.45f, 11.9f, -9.57f);
    public Quaternion defaultRotation = new Quaternion(0.199964f, -0.373612f, 0.082827f, 0.90198f);
    
    private float currentX = 0f;
    private float currentY = 0f;
    private float targetX = 0f;
    private float targetY = 0f;
    private float targetDistance = 15f;
    private bool isResetting = false;
    private float resetProgress = 0f;
    
<<<<<<< HEAD
    // Reset için başlangıç değerleri
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
    private float resetStartX, resetStartY, resetStartDistance;
    private Vector3 defaultEulerAngles;
    
    void Start()
    {
        transform.position = defaultPosition;
        transform.rotation = defaultRotation;
        
        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;
        targetX = currentX;
        targetY = currentY;
        targetDistance = distance;
        
        defaultEulerAngles = defaultRotation.eulerAngles;
    }
    
    void Update()
    {
        if (target == null) return;
        
        if (!isResetting)
        {
            HandleCameraRotation();
            HandleCameraZoom();
            HandleResetCamera();
        }
        else
        {
            HandleCameraReset();
        }
        
        UpdateCameraSmoothly();
    }
    
    void HandleCameraRotation()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            targetX += mouseX * rotationSpeed;
            targetY -= mouseY * rotationSpeed;
<<<<<<< HEAD
            targetY = Mathf.Clamp(targetY, -verticalAngleLimit, verticalAngleLimit);
=======
            
            // Hem yukarı hem aşağı açıyı sınırla
            targetY = Mathf.Clamp(targetY, -downwardAngleLimit, verticalAngleLimit);
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
        }
        
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                targetX += touch.deltaPosition.x * rotationSpeed * 0.02f;
                targetY -= touch.deltaPosition.y * rotationSpeed * 0.02f;
<<<<<<< HEAD
                targetY = Mathf.Clamp(targetY, -verticalAngleLimit, verticalAngleLimit);
=======
                targetY = Mathf.Clamp(targetY, -downwardAngleLimit, verticalAngleLimit);
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
            }
        }
    }
    
    void HandleCameraZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            targetDistance -= scroll * 5f;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
        
        if (Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            
            Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
            Vector2 touch2PrevPos = touch2.position - touch2.deltaPosition;
            
            float prevTouchDeltaMag = (touch1PrevPos - touch2PrevPos).magnitude;
            float touchDeltaMag = (touch1.position - touch2.position).magnitude;
            
            float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;
            
            targetDistance += deltaMagnitudeDiff * 0.1f;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
    }
    
    void HandleResetCamera()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            StartCameraReset();
        }
    }
    
    void StartCameraReset()
    {
        isResetting = true;
        resetProgress = 0f;
        
<<<<<<< HEAD
        // Mevcut değerleri kaydet
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
        resetStartX = currentX;
        resetStartY = currentY;
        resetStartDistance = distance;
        
<<<<<<< HEAD
        // Hedef değerleri ayarla
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
        targetX = defaultEulerAngles.y;
        targetY = defaultEulerAngles.x;
        targetDistance = CalculateDefaultDistance();
    }
    
    float CalculateDefaultDistance()
    {
<<<<<<< HEAD
        // Default pozisyon ile target arasındaki mesafe
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
        if (target != null)
            return Vector3.Distance(defaultPosition, target.position);
        return 15f;
    }
    
    void HandleCameraReset()
    {
        resetProgress += Time.deltaTime * resetSmoothness;
        
        if (resetProgress >= 1f)
        {
            resetProgress = 1f;
            isResetting = false;
            
<<<<<<< HEAD
            // Son değerleri kesin olarak ayarla
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
            currentX = targetX;
            currentY = targetY;
            distance = targetDistance;
            UpdateCameraPosition();
        }
    }
    
    void UpdateCameraSmoothly()
    {
        if (isResetting)
        {
<<<<<<< HEAD
            // Reset sırasında SADECE rotation ve distance'ı smooth geçiş yap
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
            float smoothProgress = SmoothStep(resetProgress);
            
            currentX = Mathf.LerpAngle(resetStartX, targetX, smoothProgress);
            currentY = Mathf.LerpAngle(resetStartY, targetY, smoothProgress);
            distance = Mathf.Lerp(resetStartDistance, targetDistance, smoothProgress);
            
            UpdateCameraPosition();
        }
        else
        {
<<<<<<< HEAD
            // Normal smooth movement
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
            currentX = Mathf.LerpAngle(currentX, targetX, rotationSmoothness * Time.deltaTime);
            currentY = Mathf.LerpAngle(currentY, targetY, rotationSmoothness * Time.deltaTime);
            distance = Mathf.Lerp(distance, targetDistance, zoomSmoothness * Time.deltaTime);
            
            UpdateCameraPosition();
        }
    }
    
    float SmoothStep(float t)
    {
<<<<<<< HEAD
        // Daha smooth bir easing function
=======
>>>>>>> 75acb8f (Layer completion and destruction system implemented; falling adjustments and visual effect updates added)
        return t * t * t * (t * (6f * t - 15f) + 10f);
    }
    
    void UpdateCameraPosition()
    {
        if (target == null) return;
        
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;
        
        transform.rotation = rotation;
        transform.position = position;
    }
    
    void OnValidate()
    {
        if (target != null)
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
        }
    }
}
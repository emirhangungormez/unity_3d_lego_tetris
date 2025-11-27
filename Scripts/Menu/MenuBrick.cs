using UnityEngine;

/// <summary>
/// Menüde kullanılan 3D interaktif brick.
/// Sürüklenebilir, fırlatılabilir, serbestçe hareket eder.
/// Tutulan nokta pivot olur (grabber + FixedJoint).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class MenuBrick : MonoBehaviour
{
    [Header("Brick Info")]
    [SerializeField] private Vector2Int baseSize = new Vector2Int(2, 1);

    [Header("Visual")]
    public MeshRenderer meshRenderer;

    private MenuGridManager gridManager;
    private Rigidbody rb;
    private Collider col;
    private Material brickMaterial;

    private bool isDragging = false;
    private Vector3 lastDragPosition;
    private Vector3 dragVelocity;
    private Vector3 smoothedVelocity;

    // Grabber (pivot) için runtime objeler
    private GameObject grabberObject;
    private Rigidbody grabberRb;
    private FixedJoint grabJoint;

    // Spawn gizleme
    private bool hiddenBySpawn = false;
    private float showYThreshold = 0f;

    // Fizik sabitleri
    private const float MAX_VELOCITY = 15f;
    private const float VELOCITY_SMOOTHING = 0.3f;

    public Vector2Int BaseSize
    {
        get => baseSize;
        set => baseSize = value;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();
    }

    public void Initialize(Vector2Int brickSize, MenuGridManager manager, bool rotated = false)
    {
        baseSize = brickSize;
        gridManager = manager;

        // Model XZ için, menüde XY gösterimi
        float yRotation = rotated ? 90f : 0f;
        transform.rotation = Quaternion.Euler(90f, yRotation, 0f);

        rb.useGravity = false; // manuel gravity
        rb.drag = 1f;
        rb.angularDrag = 3f;
        rb.mass = 1f + (brickSize.x * brickSize.y * 0.2f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Pozisyon Z sıfır
        Vector3 p = transform.position; p.z = 0f; transform.position = p;
    }

    public void ApplyRandomColor(Vector2 tiling, Vector2 offset)
    {
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null) return;

        // Materyali çoğalt ve tiling uygula
        brickMaterial = new Material(meshRenderer.sharedMaterial);
        brickMaterial.mainTextureScale = tiling;
        brickMaterial.mainTextureOffset = offset;
        meshRenderer.material = brickMaterial;
    }

    /// <summary>
    /// Spawn sırasında görünmez yapar; Y değeri altına düştüğünde görünür olur
    /// </summary>
    public void HideUntilBelow(float showY)
    {
        hiddenBySpawn = true;
        showYThreshold = showY;
    }

    void SetRenderersEnabled(bool enabled)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = enabled;
        }
    }

    void Update()
    {
        // Eğer spawn'da gizlendiyse, belirlenen Y'ye gelince görünür yap
        if (hiddenBySpawn)
        {
            if (transform.position.y <= showYThreshold)
            {
                hiddenBySpawn = false;
            }
        }

        HandleInput();
    }

    void FixedUpdate()
    {
        if (!isDragging)
        {
            ApplyGravity();
            // Spawn sırasında ekrana girene kadar clamp yapma (yoksa ekran dışında spawn edilen brick anında 0,0'a taşınır)
            if (!hiddenBySpawn)
            {
                ClampToScreen();
            }
            LimitVelocity();
        }

        // Z pozisyonunu koru
        if (transform.position.z != 0f)
        {
            Vector3 pos = transform.position; pos.z = 0f; transform.position = pos;
        }
    }

    void LimitVelocity()
    {
        if (rb.velocity.magnitude > MAX_VELOCITY)
            rb.velocity = rb.velocity.normalized * MAX_VELOCITY;
    }

    void HandleInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    void HandleMouseInput()
    {
        if (Camera.main == null) return;
        // Brick henüz kamera görüş alanında değilse input işleme
        if (hiddenBySpawn) return;
        
        Vector3 mouseWorld = GetMouseWorldPosition();

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointOverBrick(Input.mousePosition))
                StartDrag(mouseWorld, Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            ContinueDrag(mouseWorld);
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount == 0 || Camera.main == null) return;
        // Brick henüz kamera görüş alanında değilse input işleme
        if (hiddenBySpawn) return;
        
        Touch t = Input.GetTouch(0);
        Vector3 world = GetTouchWorldPosition(t.position);

        switch (t.phase)
        {
            case TouchPhase.Began:
                if (IsPointOverBrick(t.position)) StartDrag(world, t.position);
                break;
            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (isDragging) ContinueDrag(world);
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                if (isDragging) EndDrag();
                break;
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mp);
    }

    Vector3 GetTouchWorldPosition(Vector2 touchPos)
    {
        Vector3 p = new Vector3(touchPos.x, touchPos.y, Camera.main.WorldToScreenPoint(transform.position).z);
        return Camera.main.ScreenToWorldPoint(p);
    }

    bool IsPointOverBrick(Vector2 screenPoint)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPoint);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return hit.collider == col || hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        return false;
    }

    // inputScreenPoint used to compute local anchor
    void StartDrag(Vector3 worldPos, Vector2 inputScreenPoint)
    {
        isDragging = true;
        lastDragPosition = worldPos;
        dragVelocity = Vector3.zero;
        smoothedVelocity = Vector3.zero;

        // Create grabber at worldPos (kinematic)
        grabberObject = new GameObject($"Grabber_{name}");
        grabberObject.transform.position = worldPos;
        grabberRb = grabberObject.AddComponent<Rigidbody>();
        grabberRb.isKinematic = true;

        // Add FixedJoint on brick and connect to grabber
        grabJoint = gameObject.AddComponent<FixedJoint>();
        grabJoint.connectedBody = grabberRb;

        // Anchor: brick'in local space'ine çevir
        Vector3 localAnchor = transform.InverseTransformPoint(worldPos);
        grabJoint.anchor = localAnchor;

        // Slight visual feedback
        SetBrickAlpha(0.8f);
    }

    void ContinueDrag(Vector3 worldPos)
    {
        Vector3 instantVel = (worldPos - lastDragPosition) / Time.deltaTime;
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, instantVel, VELOCITY_SMOOTHING);
        dragVelocity = smoothedVelocity;
        lastDragPosition = worldPos;

        // Move grabber to worldPos
        if (grabberObject != null)
            grabberObject.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
    }

    void EndDrag()
    {
        isDragging = false;

        // Remove joint and grabber, let physics take over
        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }
        if (grabberObject != null)
        {
            Destroy(grabberObject);
            grabberObject = null;
            grabberRb = null;
        }

        // Apply throw velocity
        Vector3 throwVel = dragVelocity;
        if (gridManager != null) throwVel *= gridManager.throwForceMultiplier;
        throwVel.z = 0f;
        if (throwVel.magnitude > MAX_VELOCITY) throwVel = throwVel.normalized * MAX_VELOCITY;
        rb.velocity = throwVel;

        SetBrickAlpha(1f);
    }

    void ApplyGravity()
    {
        if (gridManager == null) return;
        Vector2 g = gridManager.GetGravityVector();
        rb.AddForce(new Vector3(g.x, g.y, 0f) * rb.mass);
    }

    public Vector2 GetWorldSize()
    {
        if (gridManager == null) return new Vector2(1f, 1f);
        return new Vector2(baseSize.x * gridManager.cellSize, baseSize.y * gridManager.cellSize);
    }

    void ClampToScreen()
    {
        if (gridManager == null) return;
        Bounds b = gridManager.GetScreenBounds();
        Vector2 size = GetWorldSize();
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        Vector3 pos = transform.position;
        Vector3 vel = rb.velocity;
        bool bounced = false;

        if (pos.x - halfW < b.min.x) { pos.x = b.min.x + halfW; vel.x = Mathf.Abs(vel.x) * gridManager.bounciness; bounced = true; }
        else if (pos.x + halfW > b.max.x) { pos.x = b.max.x - halfW; vel.x = -Mathf.Abs(vel.x) * gridManager.bounciness; bounced = true; }

        if (pos.y - halfH < b.min.y) { pos.y = b.min.y + halfH; vel.y = Mathf.Abs(vel.y) * gridManager.bounciness; bounced = true; }
        else if (pos.y + halfH > b.max.y) { pos.y = b.max.y - halfH; vel.y = -Mathf.Abs(vel.y) * gridManager.bounciness; bounced = true; }

        pos.z = 0f; vel.z = 0f;
        if (bounced) { vel *= 0.8f; transform.position = pos; rb.velocity = vel; }
    }

    void SetBrickAlpha(float a)
    {
        if (brickMaterial != null)
        {
            Color c = brickMaterial.color; c.a = a; brickMaterial.color = c;
        }
    }

    void OnDestroy()
    {
        if (grabberObject != null) Destroy(grabberObject);
    }
}

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class WinPanelConfetti : MonoBehaviour
{
    [Header("References")]
    public LevelManager levelManager;
    
    [Header("Confetti Settings")]
    public GameObject[] confettiBrickPrefabs;
    public int confettiPerSide = 30;
    public float confettiScale = 1.2f;
    [Header("Pooling")]
    public int confettiPoolSize = 0; // unused when pooling is disabled
    private List<GameObject> activeConfetti = new List<GameObject>();
    
    [Header("Spawn Settings")]
    public float spawnHeight = 5f;
    public float spawnRatePerSecond = 20f;
    public float confettiLifetime = 5f;
    
    [Header("Physics")]
    public float explosionForce = 15f;
    public float explosionUpwardForce = 10f;
    public float torqueForce = 15f;
    public float confettiMass = 0.1f;
    public float confettiDrag = 0.3f;
    public float confettiAngularDrag = 0.2f;
    
    [Header("Spawn Position")]
    public float screenEdgeOffset = 0.05f;
    public float screenHeightPosition = 0.3f;

    private Camera mainCamera;
    private Coroutine leftRoutine, rightRoutine;
    [Header("Anchors")]
    [Tooltip("Optional: assign explicit Transforms for Left/Right spawn anchors. If empty, script will search camera children named 'Left'/'Right'.")]
    public Transform leftAnchor;
    public Transform rightAnchor;

    void Awake()
    {
        if (levelManager == null)
            levelManager = FindObjectOfType<LevelManager>();
    }

#if UNITY_EDITOR
    // Editor-time validation: DO NOT automatically remove entries while editing because
    // that can interfere with dragging prefabs into the array. Instead, report invalid
    // entries and provide a menu tool to clean them explicitly.
    void OnValidate()
    {
        if (confettiBrickPrefabs == null || confettiBrickPrefabs.Length == 0) return;
        for (int i = 0; i < confettiBrickPrefabs.Length; i++)
        {
            var p = confettiBrickPrefabs[i];
            if (p == null)
            {
                Debug.LogWarning($"WinPanelConfetti: confettiBrickPrefabs[{i}] is null. Use the 'Tools/Confetti/Clean Prefabs' menu to remove null/invalid entries safely.");
                continue;
            }

            var comps = p.GetComponents<Component>();
            bool hasMissing = false;
            foreach (var c in comps) if (c == null) { hasMissing = true; break; }
            if (hasMissing)
            {
                Debug.LogError($"WinPanelConfetti: Prefab '{p.name}' contains a missing script reference. Use the 'Tools/Confetti/Clean Prefabs' menu to remove or fix it.");
            }
        }
    }
#endif

    void OnEnable()
    {
        // Initialize camera reference
        mainCamera = Camera.main;
        // find Left/Right anchors (camera children) only if not assigned in Inspector
        if (mainCamera != null)
        {
            if (leftAnchor == null)
                leftAnchor = FindChildRecursive(mainCamera.transform, "Left");
            if (rightAnchor == null)
                rightAnchor = FindChildRecursive(mainCamera.transform, "Right");
            // fallback: try Find in scene by name
            if (leftAnchor == null)
                leftAnchor = GameObject.Find("Left")?.transform;
            if (rightAnchor == null)
                rightAnchor = GameObject.Find("Right")?.transform;
        }
    }

    void Update()
    {
        // Spawning is handled by coroutines started in StartConfetti/StopConfetti.
        // Keep Update minimal to avoid relying on external counters.
    }

    // Call to start confetti effect (e.g. when win panel becomes active)
    public void StartConfetti()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (leftAnchor == null && mainCamera != null) leftAnchor = FindChildRecursive(mainCamera.transform, "Left");
        if (rightAnchor == null && mainCamera != null) rightAnchor = FindChildRecursive(mainCamera.transform, "Right");
        if (leftAnchor == null) leftAnchor = GameObject.Find("Left")?.transform;
        if (rightAnchor == null) rightAnchor = GameObject.Find("Right")?.transform;
        if (leftAnchor == null && rightAnchor == null)
        {
            Debug.LogWarning("WinPanelConfetti: Left and Right anchors not found on camera.");
            return;
        }

        StopConfetti();
        if (leftAnchor != null) leftRoutine = StartCoroutine(SpawnFromAnchor(leftAnchor, true, confettiPerSide));
        if (rightAnchor != null) rightRoutine = StartCoroutine(SpawnFromAnchor(rightAnchor, false, confettiPerSide));
    }

    // Stop and cleanup confetti immediately
    public void StopConfetti()
    {
        if (leftRoutine != null) { StopCoroutine(leftRoutine); leftRoutine = null; }
        if (rightRoutine != null) { StopCoroutine(rightRoutine); rightRoutine = null; }
        // deactivate active confetti and return to pool
        for (int i = activeConfetti.Count - 1; i >= 0; i--)
        {
            var c = activeConfetti[i];
            if (c != null)
            {
                // destroy spawned confetti instances
                Destroy(c);
            }
            activeConfetti.RemoveAt(i);
        }
    }

    // Not used: spawning uses camera child anchors

    private IEnumerator SpawnFromAnchor(Transform anchor, bool isLeftSide, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // spawn slightly above anchor using spawnHeight
            Vector3 spawnPos = anchor.position + Vector3.up * spawnHeight;
            SpawnOne(spawnPos, isLeftSide);
            yield return new WaitForSeconds(1f / spawnRatePerSecond);
        }
    }

    // Recursively search children for a transform with given name (case-sensitive)
    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void SpawnOne(Vector3 spawnPosition, bool isLeftSide)
    {
        if (confettiBrickPrefabs == null || confettiBrickPrefabs.Length == 0) return;
        var prefab = confettiBrickPrefabs[Random.Range(0, confettiBrickPrefabs.Length)];
        GameObject confetti = null;
        if (prefab != null)
        {
            confetti = Instantiate(prefab, this.transform);
        }
        else
        {
            confetti = GameObject.CreatePrimitive(PrimitiveType.Cube);
            confetti.transform.SetParent(this.transform, true);
            confetti.transform.localScale = Vector3.one * confettiScale;
        }
        confetti.transform.position = spawnPosition;
        confetti.transform.rotation = Random.rotation;
        confetti.transform.localScale = Vector3.one * confettiScale;
        confetti.SetActive(true);

        SetupConfettiPhysics(confetti, isLeftSide);
        ApplyRandomColor(confetti);

        // Attach camera-space gravity so confetti always falls toward screen bottom
        if (confetti.GetComponent<Confetti2DGravity>() == null)
        {
            var g = confetti.AddComponent<Confetti2DGravity>();
            // tune defaults if desired
            g.gravity = 9.81f;
            g.gravityScale = 1.0f;
        }

        activeConfetti.Add(confetti);
        Destroy(confetti, confettiLifetime);
    }

    private void SetupConfettiPhysics(GameObject confetti, bool isLeftSide)
    {
        Rigidbody rb = confetti.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = confetti.AddComponent<Rigidbody>();
        }

        if (confetti.GetComponent<Collider>() == null)
        {
            confetti.AddComponent<BoxCollider>();
        }

        rb.mass = confettiMass;
        rb.drag = confettiDrag;
        rb.angularDrag = confettiAngularDrag;
        // We will drive gravity in Confetti2DGravity (camera-space down), so disable built-in gravity.
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Create a gentler, more controlled initial velocity so confetti doesn't launch unrealistically high
        float horizontalDir = isLeftSide ? 1f : -1f;

        // Limit upward velocity so pieces rise at most ~2 units initially (user requested max 2f)
        float upwardVel = Random.Range(0.5f, 2f);

        // Horizontal impulse scaled down to make the burst feel softer
        float horizSpeed = horizontalDir * Random.Range(0.4f, 0.8f) * (explosionForce * 0.2f);
        float lateralSpeed = Random.Range(-0.25f, 0.25f) * (explosionForce * 0.2f);

        Vector3 initialVelocity = new Vector3(horizSpeed, upwardVel, lateralSpeed);

        // Apply as a velocity change for predictable initial speeds (ignores mass)
        rb.AddForce(initialVelocity, ForceMode.VelocityChange);

        // Apply a mild random angular velocity (reduced torque for slower rotation)
        Vector3 randomTorque = Random.insideUnitSphere * (torqueForce * 0.15f);
        rb.AddTorque(randomTorque, ForceMode.VelocityChange);
    }

    // Pooling removed — simplified spawn/destroy behavior

    private void ApplyRandomColor(GameObject confetti)
    {
        if (levelManager == null)
        {
            levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null) return;
        }

        var colors = levelManager.availableColors;
        if (colors == null || colors.Count == 0) return;

        var randomColorSettings = colors[Random.Range(0, colors.Count)];
        
        foreach (var renderer in confetti.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;

            Material mat = new Material(renderer.material);
            mat.mainTextureScale = randomColorSettings.tiling;
            mat.mainTextureOffset = randomColorSettings.offset;
            renderer.material = mat;
        }
    }

    private void CleanupConfetti()
    {
        // Destroy all active confetti instances
        for (int i = activeConfetti.Count - 1; i >= 0; i--)
        {
            var confetti = activeConfetti[i];
            if (confetti != null)
            {
                Destroy(confetti);
            }
            activeConfetti.RemoveAt(i);
        }
    }

    void OnDisable()
    {
        CleanupConfetti();
    }

    void OnDestroy()
    {
        CleanupConfetti();
    }
}
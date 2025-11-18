using UnityEngine;
using System.Collections.Generic;

public class WinPanelConfetti : MonoBehaviour
{
    [Header("Confetti Settings")]
    public GameObject[] confettiBrickPrefabs;
    public int confettiPerSide = 30;
    public float confettiScale = 0.4f;
    
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

    private List<GameObject> activeConfetti = new List<GameObject>();
    private Camera mainCamera;
    private float nextSpawnTime;
    private int leftSpawnCount;
    private int rightSpawnCount;
    private bool isSpawning = false;

    void OnEnable()
    {
        mainCamera = Camera.main;
        leftSpawnCount = confettiPerSide;
        rightSpawnCount = confettiPerSide;
        nextSpawnTime = Time.time;
        isSpawning = true;
        
        CleanupConfetti();
    }

    void Update()
    {
        if (!isSpawning) return;
        if (leftSpawnCount <= 0 && rightSpawnCount <= 0)
        {
            isSpawning = false;
            return;
        }
        
        if (Time.time >= nextSpawnTime)
        {
            if (leftSpawnCount > 0)
            {
                SpawnConfetti(GetScreenEdgePosition(true), true);
                leftSpawnCount--;
            }
            
            if (rightSpawnCount > 0)
            {
                SpawnConfetti(GetScreenEdgePosition(false), false);
                rightSpawnCount--;
            }
            
            nextSpawnTime = Time.time + (1f / spawnRatePerSecond);
        }
    }

    private Vector3 GetScreenEdgePosition(bool isLeft)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return Vector3.zero;
        
        float xPos = isLeft ? screenEdgeOffset : (1f - screenEdgeOffset);
        Vector3 screenPos = new Vector3(
            Screen.width * xPos,
            Screen.height * screenHeightPosition,
            10f
        );
        
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        worldPos.y = spawnHeight;
        
        return worldPos;
    }

    private void SpawnConfetti(Vector3 spawnPosition, bool isLeftSide)
    {
        if (confettiBrickPrefabs == null || confettiBrickPrefabs.Length == 0) return;
        
        GameObject randomPrefab = confettiBrickPrefabs[Random.Range(0, confettiBrickPrefabs.Length)];
        GameObject confetti = Instantiate(randomPrefab, spawnPosition, Random.rotation);
        
        confetti.name = $"Confetti_{activeConfetti.Count}";
        confetti.transform.localScale = Vector3.one * confettiScale;
        confetti.layer = LayerMask.NameToLayer("WinLose");
        
        foreach (Transform child in confetti.GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = LayerMask.NameToLayer("WinLose");
        }
        
        SetupConfettiPhysics(confetti, isLeftSide);
        ApplyRandomColor(confetti);
        
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
        rb.useGravity = true;

        float horizontalDir = isLeftSide ? 1f : -1f;
        Vector3 explosionDirection = new Vector3(
            horizontalDir * Random.Range(0.6f, 1f),
            Random.Range(0.8f, 1.2f),
            Random.Range(-0.3f, 0.3f)
        ).normalized;

        rb.AddForce(explosionDirection * explosionForce + Vector3.up * explosionUpwardForce, ForceMode.Impulse);
        
        Vector3 randomTorque = new Vector3(
            Random.Range(-torqueForce, torqueForce),
            Random.Range(-torqueForce, torqueForce),
            Random.Range(-torqueForce, torqueForce)
        );
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    private void ApplyRandomColor(GameObject confetti)
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null) return;

        var colors = gameManager.availableColors;
        if (colors.Count == 0) return;

        var randomColor = colors[Random.Range(0, colors.Count)];
        
        foreach (var renderer in confetti.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;

            Material mat = new Material(renderer.material);
            mat.mainTextureScale = randomColor.tiling;
            mat.mainTextureOffset = randomColor.offset;
            renderer.material = mat;
        }
    }

    private void CleanupConfetti()
    {
        foreach (GameObject confetti in activeConfetti)
        {
            if (confetti != null)
            {
                Destroy(confetti);
            }
        }
        activeConfetti.Clear();
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
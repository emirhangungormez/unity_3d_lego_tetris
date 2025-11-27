using UnityEngine;
using System.Collections.Generic;

public class MenuGridManager : MonoBehaviour
{
    public static MenuGridManager Instance { get; private set; }

    [Header("Settings")]
    public float cellSize = 1f;

    [Header("Physics")]
    public float gravity = 15f;
    public float accelerometerMultiplier = 2f;
    public float throwForceMultiplier = 2f;
    [Range(0f,1f)] public float bounciness = 0.4f;
    public float maxVelocity = 12f;
    [Header("Spawn Motion")]
    [Tooltip("World Y coordinate to spawn bricks at")]
    public float spawnY = 30f;
    [Tooltip("Delay between sequential spawns (seconds)")]
    public float spawnDelay = 0.15f;
    [Tooltip("If assigned, bricks will spawn at this Transform position instead of sweeping across the screen.")]
    public Transform customSpawnPoint;

    [Header("Prefabs")]
    public List<GameObject> brickPrefabs = new List<GameObject>();

    [Header("Spawn")]
    public int initialBrickCount = 20;

    [Header("Color Tiling")]
    public List<ColorTilingSettings> colorSettings = new List<ColorTilingSettings>();

    private List<MenuBrick> allBricks = new List<MenuBrick>();
    private Bounds screenBounds;
    private Camera mainCamera;
    private float cameraHeight = 0f;
    private int nextColorIndex = 0; // 9 rengin hepsinin sırayla kullanılması için

    [System.Serializable]
    public class ColorTilingSettings { public string name; public Vector2 tiling = Vector2.one; public Vector2 offset = Vector2.zero; }

    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);
    }

    void Start()
    {
        mainCamera = Camera.main;
        if (colorSettings.Count == 0)
        {
            // 9 renk paleti
            colorSettings.Add(new ColorTilingSettings { name = "Orange", tiling = new Vector2(1f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Blue", tiling = new Vector2(1.1f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Green", tiling = new Vector2(1.9f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Red", tiling = new Vector2(0.5f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Yellow", tiling = new Vector2(0.7f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Purple", tiling = new Vector2(1.3f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Cyan", tiling = new Vector2(1.5f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "Pink", tiling = new Vector2(1.7f, -0.5f), offset = new Vector2(0f, 0.5f) });
            colorSettings.Add(new ColorTilingSettings { name = "White", tiling = new Vector2(2.1f, -0.5f), offset = new Vector2(0f, 0.5f) });
        }

        CalculateScreenBounds();
        // start sequential spawn coroutine
        StartCoroutine(SpawnInitialBricksCoroutine());
    }

    void CalculateScreenBounds()
    {
        if (mainCamera == null) return;
        float camH = mainCamera.orthographicSize * 2f;
        float camW = camH * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;
        screenBounds = new Bounds(new Vector3(camPos.x, camPos.y, 0f), new Vector3(camW, camH, 10f));
        cameraHeight = camH;
    }

    System.Collections.IEnumerator SpawnInitialBricksCoroutine()
    {
        if (brickPrefabs == null || brickPrefabs.Count == 0)
        {
            Debug.LogWarning("MenuGridManager: brickPrefabs list empty");
            yield break;
        }

        // spawn parameters
        bool useCustomPoint = customSpawnPoint != null;
        float minX = screenBounds.min.x + 1f;
        float maxX = screenBounds.max.x - 1f;
        float spawnBaseY = spawnY; // fixed spawn Y as requested

        // step for sweeping X across screen (only used when no custom point assigned)
        float sweepWidth = Mathf.Max(1f, maxX - minX);
        float step = sweepWidth / Mathf.Max(1, initialBrickCount - 1);
        float currentX = (minX + maxX) * 0.5f; // start center
        int dir = 1;
        if (useCustomPoint && customSpawnPoint == null) useCustomPoint = false;

        for (int i = 0; i < initialBrickCount; i++)
        {
            GameObject prefab = brickPrefabs[Random.Range(0, brickPrefabs.Count)];
            if (prefab == null) { yield return new WaitForSeconds(spawnDelay); continue; }

            bool rotated = Random.value > 0.5f;
            if (useCustomPoint)
            {
                // spawn at custom transform world position with random X offset (-5 to +5)
                Vector3 spawnPos = customSpawnPoint.position;
                spawnPos.x += Random.Range(-5f, 5f);
                Debug.Log($"MenuGridManager: spawning prefab '{prefab.name}' at customSpawnPoint + random X: {spawnPos}");
                SpawnBrick(prefab, spawnPos, rotated);
            }
            else
            {
                // clamp currentX
                currentX = Mathf.Clamp(currentX, minX, maxX);
                Vector2 spawn = new Vector2(currentX, spawnBaseY + Random.Range(0f, 0.2f));
                Debug.Log($"MenuGridManager: spawning prefab '{prefab.name}' at sweep position {spawn}");
                SpawnBrick(prefab, spawn, rotated);
            }

            // move X for next spawn, bounce at edges (only when not using custom point)
            if (!useCustomPoint)
            {
                currentX += step * dir;
                if (currentX >= maxX) { currentX = maxX; dir = -1; }
                else if (currentX <= minX) { currentX = minX; dir = 1; }
            }

            yield return new WaitForSeconds(spawnDelay);
        }
    }

    Vector2 GetPrefabApproxSize(GameObject prefab)
    {
        if (prefab == null) return new Vector2(cellSize, cellSize);
        Vector3 size = Vector3.zero;
        var rends = prefab.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            size = Vector3.Max(size, r.bounds.size);
        }
        if (size == Vector3.zero)
        {
            var col = prefab.GetComponentInChildren<Collider>();
            if (col != null) size = col.bounds.size;
        }
        if (size == Vector3.zero) size = new Vector3(cellSize, cellSize, cellSize);
        return new Vector2(size.x, size.y);
    }

    Vector2 GetRandomSpawnPosition()
    {
        // Spawn ekranın üst kenarına yakın bir noktada gerçekleşecek
        float x = Random.Range(screenBounds.min.x + 1f, screenBounds.max.x - 1f);
        float spawnOffset = 0.6f; // kameranın üstünden biraz yukarı
        float y = screenBounds.max.y + spawnOffset;
        return new Vector2(x, y);
    }

    ColorTilingSettings GetNextColorSetting()
    {
        if (colorSettings == null || colorSettings.Count == 0) return new ColorTilingSettings { tiling = Vector2.one, offset = Vector2.zero };
        // Sırayla 9 rengin hepsini kullan (round-robin)
        ColorTilingSettings cs = colorSettings[nextColorIndex];
        nextColorIndex = (nextColorIndex + 1) % colorSettings.Count;
        return cs;
    }

    // Spawn using a world position (Vector3). This ensures custom Transform spawn positions are used exactly.
    public MenuBrick SpawnBrick(GameObject prefab, Vector3 worldPosition, bool rotated = false)
    {
        // Prefab'ı doğru pozisyonda instantiate et
        GameObject obj = Instantiate(prefab, worldPosition, Quaternion.identity);
        Debug.Log($"[SPAWN 1] After Instantiate at {worldPosition}: position = {obj.transform.position}");

        Rigidbody rb = obj.GetComponent<Rigidbody>(); if (rb==null) rb = obj.AddComponent<Rigidbody>();
        // Rigidbody pozisyonunu da ayarla (fizik senkronizasyonu için)
        rb.position = worldPosition;
        
        Collider col = obj.GetComponent<Collider>(); if (col==null) obj.AddComponent<BoxCollider>();

        MenuBrick mb = obj.GetComponent<MenuBrick>(); if (mb==null) mb = obj.AddComponent<MenuBrick>();

        Vector2Int size = GetBrickSizeFromName(prefab.name);
        mb.Initialize(size, this, rotated);
        
        // Initialize sonrası pozisyonu tekrar ayarla (Initialize Z'yi 0 yapıyor, Y korunmalı)
        Vector3 correctedPos = new Vector3(worldPosition.x, worldPosition.y, 0f);
        obj.transform.position = correctedPos;
        rb.position = correctedPos;
        Debug.Log($"[SPAWN 2] After Initialize + correction: position = {obj.transform.position}");

        var cs = GetNextColorSetting();
        mb.ApplyRandomColor(cs.tiling, cs.offset);

        // Spawn sırasında görünmez olsun; ekrana girdikçe görünür olacak
        float showY = screenBounds.max.y - 0.05f;
        mb.HideUntilBelow(showY);
        Debug.Log($"[SPAWN 3] Final position = {obj.transform.position}");

        allBricks.Add(mb);
        return mb;
    }

    // Backwards-compatible wrapper: when caller provides 2D position, convert to world (z=0)
    public MenuBrick SpawnBrick(GameObject prefab, Vector2 position, bool rotated = false)
    {
        return SpawnBrick(prefab, new Vector3(position.x, position.y, 0f), rotated);
    }

    Vector2Int GetBrickSizeFromName(string name)
    {
        name = name.ToLower();
        if (name.Contains("1x1")) return new Vector2Int(1,1);
        if (name.Contains("1x2")||name.Contains("1 x 2")) return new Vector2Int(2,1);
        if (name.Contains("1x4")) return new Vector2Int(4,1);
        if (name.Contains("2x2")) return new Vector2Int(2,2);
        if (name.Contains("2x4")) return new Vector2Int(4,2);
        return new Vector2Int(2,1);
    }

    public void RemoveBrick(MenuBrick b) { if (allBricks.Contains(b)) allBricks.Remove(b); }

    public Vector2 GetGravityVector()
    {
        // Telefon hareketine göre yerçekimi yönü değişir
        // Normal tutulduğunda aşağı, yan çevrildiğinde yana düşer
        Vector3 acc = Input.acceleration;
        
        // Accelerometer değerlerini yerçekimi yönüne çevir
        // acc.x = sağa/sola eğim, acc.y = ileri/geri eğim (telefon dikey tutulduğunda aşağı/yukarı)
        Vector2 g = new Vector2(acc.x, acc.y) * gravity;
        
        // Eğer accelerometer verisi yoksa (editörde) varsayılan aşağı yönü kullan
        if (acc.sqrMagnitude < 0.01f)
        {
            g = Vector2.down * gravity;
        }
        
        // Accelerometer çarpanı ile güçlendir (sallama efekti için)
        g *= accelerometerMultiplier;
        
        return g;
    }

    public Vector2 ClampToScreen(Vector2 position, Vector2 objectSize)
    {
        float halfW = objectSize.x*0.5f; float halfH = objectSize.y*0.5f;
        position.x = Mathf.Clamp(position.x, screenBounds.min.x + halfW, screenBounds.max.x - halfW);
        position.y = Mathf.Clamp(position.y, screenBounds.min.y + halfH, screenBounds.max.y - halfH);
        return position;
    }

    public Bounds GetScreenBounds() => screenBounds;
}

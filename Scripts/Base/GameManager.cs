using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class UIReferences
    {
        [Header("Game Info UI")]
        public Text goldText;
        public Text timerText;
        
        [Header("Panels")]
        public GameObject winPanel;
        public GameObject losePanel; 
        public Text loseReasonText;
    }
    
    [System.Serializable]
    public class ScoreSettings
    {
        public int goldPerBrick = 10;
        public int goldPerLayer = 100;
        public int comboMultiplier = 2;
    }
    
    
    [Header("Core References")]
    public GridManager gridManager;
    public EffectManager effectManager;
    public LevelManager levelManager;
    public WinPanelConfetti confettiRunner;
    public List<GameObject> brickPrefabs;
    
    [Header("UI Animation Settings")]
    public float uiScaleDuration = 0.3f;
    public float uiScaleAmount = 1.2f;
    
    [Header("UI References")]
    public UIReferences ui = new UIReferences();
    
    [Header("Score Settings")]
    public ScoreSettings scoreSettings = new ScoreSettings();
    
    [Header("Game UI Controls")]
    [Tooltip("Settings panel to show/hide")]
    public GameObject settingsPanel;
    [Tooltip("Text showing current speed (1X or 2X)")]
    public Text speedToggleText;
    
    // Pause settings removed — pause UI remains but functionality disabled.
    
    [Header("Effect Timing")]
    public float layerClearDelay = 0.3f;
    // Slightly faster brick fall for snappier gameplay
    public float brickFallDuration = 0.45f;
    public float autoFallDelay = 0.25f;

    // Fall speed multiplier (1 = normal, 2 = double). Togglable via UI button.
    private float fallSpeedMultiplier = 1f;
    public bool IsDoubleFallSpeed => fallSpeedMultiplier > 1f;

    [Header("Spawn Settings")]
    [Tooltip("If true, bricks will spawn at the Transform assigned to Custom Spawn Point. If false, they spawn randomly on top of the grid.")]
    public bool useCustomSpawnPoint = false;
    public Transform customSpawnPoint;
    [Tooltip("Spawn Y height when not using a custom spawn point.")]
    public float spawnY = 30f;
    
    [HideInInspector] public List<GameObject> landedBricks = new List<GameObject>();
    
    private GameObject currentBrick;
    private GameObject ghostPreview;
    private List<Renderer> ghostRenderers = new List<Renderer>();
    private BoxCollider currentBrickCollider;
    private Vector2Int currentBrickGridPos;
    private LevelManager.BrickColor currentBrickColor;
    // Increase the in-air fall speed so bricks feel faster
    private float currentBrickFallSpeed = 4f;
    private float nextFallTime;
    
    private bool isGameActive;
    private int currentGold, comboCount;
    
    private static readonly List<GameObject> cachedAvailableBricks = new();
    private static readonly List<GameObject> cachedSuitableBricks = new();
    private static readonly Queue<(int, int, int)> cachedFloodFillQueue = new();
    private static readonly HashSet<(int, int, int)> cachedFloodFillVisited = new();
    
    private Queue<LevelManager.BrickColor> recentBrickColors = new();
    private const int MaxConsecutiveColors = 3;
    
    private Dictionary<Material, Material> ghostMaterialCache = new();
    private float ghostBaseAlpha = 0.35f;

    public bool IsGameActive => isGameActive;
    public GameObject CurrentBrick => currentBrick;

    void Start()
    {
        InitializeGame();
    }

    void InitializeGame()
    {
        isGameActive = true;
        currentGold = 0;
        comboCount = 0;
        currentBrick = null;
        landedBricks.Clear();
        ghostMaterialCache.Clear();
        recentBrickColors.Clear();

        if (ui.winPanel != null)
        {
            ui.winPanel.SetActive(false);
            var conf = ui.winPanel.GetComponent<WinPanelConfetti>();
            if (conf != null) conf.StopConfetti();
        }
        if (ui.losePanel != null) ui.losePanel.SetActive(false);

        if (levelManager != null)
            levelManager.InitializeLevel();

        UpdateGoldUI();

        var camCtrl = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (camCtrl != null) camCtrl.allowInput = true;

        if (confettiRunner != null)
        {
            if (confettiRunner.gameObject.activeInHierarchy && confettiRunner.enabled)
                confettiRunner.StopConfetti();
        }
        else if (ui.winPanel != null)
        {
            var panelConfetti = ui.winPanel.GetComponent<WinPanelConfetti>();
            if (panelConfetti != null)
                panelConfetti.StopConfetti();
        }

        // Ensure speed toggle UI shows correct label at start
        UpdateSpeedToggleUI();

        SpawnNewBrick();
    }

    void UpdateFallLine()
    {
        UpdateGhostPreviewPosition();
    }

    private void SetLayerRecursively(Transform t, int layer)
    {
        if (t == null) return;
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    public IEnumerator ScaleUIElement(Transform element, float targetScale)
    {
        if (element == null) yield break;

        // Capture original scale safely (object might still be destroyed afterwards)
        Vector3 originalScale;
        RectTransform rectTransform = element as RectTransform;
        Vector2 originalPivot = Vector2.one * 0.5f;
        
        try { 
            originalScale = element.localScale;
            // Save original pivot and temporarily center it for proper scaling
            if (rectTransform != null)
            {
                originalPivot = rectTransform.pivot;
                // Calculate position offset to keep visual position same when changing pivot
                Vector2 size = rectTransform.rect.size;
                Vector2 pivotDiff = new Vector2(0.5f, 0.5f) - originalPivot;
                Vector3 posOffset = new Vector3(pivotDiff.x * size.x * element.localScale.x, 
                                                 pivotDiff.y * size.y * element.localScale.y, 0f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.localPosition += posOffset;
            }
        }
        catch { yield break; }
        
        Vector3 scaledUp = originalScale * targetScale;

        float halfDuration = Mathf.Max(0.0001f, uiScaleDuration / 2f);
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (element == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            try { element.localScale = Vector3.Lerp(originalScale, scaledUp, t); }
            catch { yield break; }
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            if (element == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            try { element.localScale = Vector3.Lerp(scaledUp, originalScale, t); }
            catch { yield break; }
            yield return null;
        }

        if (element == null) yield break;
        
        // Restore original pivot and position
        try
        {
            element.localScale = originalScale;
            if (rectTransform != null)
            {
                Vector2 size = rectTransform.rect.size;
                Vector2 pivotDiff = originalPivot - new Vector2(0.5f, 0.5f);
                Vector3 posOffset = new Vector3(pivotDiff.x * size.x * element.localScale.x, 
                                                 pivotDiff.y * size.y * element.localScale.y, 0f);
                rectTransform.pivot = originalPivot;
                rectTransform.localPosition += posOffset;
            }
        }
        catch { }
        try { element.localScale = originalScale; }
        catch { yield break; }
    }
    
    void UpdateGoldUI()
    {
        if (ui.goldText != null)
        {
            ui.goldText.text = $"{currentGold}";
            StartCoroutine(ScaleUIElement(ui.goldText.transform, uiScaleAmount));
        }
    }
    
    void AddGold(int amount)
    {
        currentGold += amount * (comboCount > 0 ? scoreSettings.comboMultiplier : 1);
        UpdateGoldUI();
    }
    
    public void WinLevel()
    {
        isGameActive = false;
        ClearCurrentBrick();
        if (ui.winPanel != null)
        {
            ui.winPanel.SetActive(true);
            var confetti = ui.winPanel.GetComponent<WinPanelConfetti>();
            if (confetti != null) confetti.StartConfetti();
        }

        int winLoseLayer = LayerMask.NameToLayer("WinLose");
        if (winLoseLayer == -1)
        {
            Debug.LogWarning("WinLevel: Layer 'WinLose' not found in project. Assign the layer in Project Settings -> Tags & Layers to enable visual separation.");
        }
        else
        {
            for (int i = 0; i < landedBricks.Count; i++)
            {
                var b = landedBricks[i];
                if (b == null) continue;
                SetLayerRecursively(b.transform, winLoseLayer);
            }
        }

        if (confettiRunner != null)
        {
            if (confettiRunner.gameObject.activeInHierarchy && confettiRunner.enabled)
                confettiRunner.StartConfetti();
        }
        
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var camController = mainCam.GetComponent<CameraController>();
            if (camController != null) camController.allowInput = false;
        }
    }
    
    public void LoseLevel(string reason)
    {
        isGameActive = false;
        ClearCurrentBrick();
        if (ui.losePanel != null) ui.losePanel.SetActive(true);
        if (ui.loseReasonText != null) ui.loseReasonText.text = reason;
    }
    
    // Pause button handling removed. Pause UI should be disabled or repurposed in the scene.
    
    public void SetGameActive(bool active)
    {
        isGameActive = active;
    }

    public void NextLevelFromUI()
    {
        if (levelManager != null)
            levelManager.NextLevel();
    }

    public void RestartLevelFromUI()
    {
        if (levelManager != null)
            levelManager.RestartLevel();
    }

    // Toggle double fall speed (only affects bricks fall speed and fall coroutines)
    public void ToggleDoubleFallSpeed()
    {
        if (fallSpeedMultiplier > 1f)
            fallSpeedMultiplier = 1f;
        else
            fallSpeedMultiplier = 2f;

        UpdateSpeedToggleUI();
        Debug.Log($"ToggleDoubleFallSpeed -> multiplier={fallSpeedMultiplier}");
    }
    
    void UpdateSpeedToggleUI()
    {
        if (speedToggleText != null)
        {
            // Show the action the button will perform: when currently 2x, show "1X" (press to go to 1x),
            // when currently 1x, show "2X" (press to go to 2x).
            speedToggleText.text = fallSpeedMultiplier > 1f ? "1X" : "2X";
        }
    }
    
    #region Settings Panel
    
    /// <summary>
    /// Opens the settings panel. Assign to settings button OnClick.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }
    
    /// <summary>
    /// Closes the settings panel. Assign to close button OnClick.
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
    
    /// <summary>
    /// Toggles settings panel visibility.
    /// </summary>
    public void ToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
    
    #endregion
    
    // Get current fall speed multiplier
    public float FallSpeedMultiplier => fallSpeedMultiplier;
    
    // UpdatePauseUI removed along with pause functionality.
    
    void SpawnNewBrick()
    {
        if (!isGameActive) return;
        
        int currentHighestLayer = gridManager.GetHighestLayer();
        if (currentHighestLayer >= levelManager.CurrentLevel.maxLayers)
        {
            LoseLevel("Maksimum Katman Aşıldı!");
            return;
        }
        
        var availableBricks = GetBricksFromNames(levelManager.CurrentLevel.allowedBrickNames);
        var suitableBricks = GetSuitableBricks(availableBricks);
        
        if (suitableBricks.Count == 0)
        {
            availableBricks = new List<GameObject>(brickPrefabs);
            suitableBricks = GetSuitableBricks(availableBricks);
            if (suitableBricks.Count == 0) return;
        }
        
        var randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        var randomColor = GetRandomBrickColorWithConstraint();
        
        SpawnBrick(randomBrickPrefab, randomColor);
    }
    
    LevelManager.BrickColor GetRandomBrickColorWithConstraint()
    {
        var availableColors = new List<LevelManager.BrickColor>(levelManager.TargetBrickCounts.Keys);
        if (availableColors.Count == 0) return LevelManager.BrickColor.Orange;
        
        if (recentBrickColors.Count >= MaxConsecutiveColors)
        {
            var colors = recentBrickColors.ToArray();
            if (colors[colors.Length - 1] == colors[colors.Length - 2] && 
                colors[colors.Length - 2] == colors[colors.Length - 3])
            {
                var forbiddenColor = colors[colors.Length - 1];
                availableColors.RemoveAll(c => c == forbiddenColor);
                if (availableColors.Count == 0) 
                    availableColors = new List<LevelManager.BrickColor>(levelManager.TargetBrickCounts.Keys);
            }
        }
        
        var selectedColor = availableColors[Random.Range(0, availableColors.Count)];
        recentBrickColors.Enqueue(selectedColor);
        if (recentBrickColors.Count > MaxConsecutiveColors * 2)
            recentBrickColors.Dequeue();
        
        return selectedColor;
    }
    
    void SpawnBrick(GameObject brickPrefab, LevelManager.BrickColor color)
    {
        Vector2Int brickSize = gridManager.GetBrickSize(brickPrefab);
            Vector2Int spawnPos;

            if (useCustomSpawnPoint && customSpawnPoint != null)
            {
                // Derive grid position from custom world point so grid logic remains consistent
                Vector3 cp = customSpawnPoint.position;
                spawnPos = gridManager.WorldToGridPosition(new Vector3(cp.x, 0f, cp.z));
            }
            else
            {
                spawnPos = new Vector2Int(
                    Random.Range(0, gridManager.GridWidth - brickSize.x + 1),
                    Random.Range(0, gridManager.GridHeight - brickSize.y + 1)
                );
            }
        
        currentBrickGridPos = spawnPos;
        currentBrickColor = color;
        
        Vector3 worldPos = gridManager.GetGridPosition(spawnPos, brickSize);
            if (useCustomSpawnPoint && customSpawnPoint != null)
            {
                // Use exact custom spawn transform position (preserve Y from transform)
                worldPos = customSpawnPoint.position;
            }
            else
            {
                worldPos.y = spawnY;
            }

        currentBrick = Instantiate(brickPrefab, worldPos, Quaternion.identity);
        currentBrick.name = brickPrefab.name + "_Current";
        currentBrickCollider = currentBrick.GetComponent<BoxCollider>();

        // Ensure visual effects component exists for animations
        var vfx = currentBrick.GetComponent<VisualBrickEffects>();
        if (vfx == null) vfx = currentBrick.AddComponent<VisualBrickEffects>();

        levelManager.ApplyBrickTexture(currentBrick, color);

        Vector3 spawnCenter = gridManager.GetGridPosition(spawnPos, brickSize);
        float preservedY = currentBrick.transform.position.y;
        AlignBrickToGridXZ(currentBrick, spawnCenter);
        Vector3 sp = currentBrick.transform.position; sp.y = preservedY; currentBrick.transform.position = sp;

        CreateGhostPreview(brickPrefab, spawnPos, brickSize);
        nextFallTime = Time.time;
    }
    
    List<GameObject> GetBricksFromNames(List<string> brickNames)
    {
        cachedAvailableBricks.Clear();
        if (brickNames.Count == 0)
        {
            cachedAvailableBricks.AddRange(brickPrefabs);
            return cachedAvailableBricks;
        }
        
        foreach (var name in brickNames)
        {
            var brick = FindBrickByName(name);
            if (brick != null) cachedAvailableBricks.Add(brick);
        }
        return cachedAvailableBricks;
    }
    
    List<GameObject> GetSuitableBricks(List<GameObject> brickList)
    {
        cachedSuitableBricks.Clear();
        foreach (var brick in brickList)
        {
            var size = gridManager.GetBrickSize(brick);
            if (size.x <= levelManager.CurrentLevel.gridSize.x && size.y <= levelManager.CurrentLevel.gridSize.y)
                cachedSuitableBricks.Add(brick);
        }
        return cachedSuitableBricks;
    }
    
    private GameObject FindBrickByName(string name) => brickPrefabs.Find(b => b.name == name);
    
    public void OnMoveUpButton() => TryMoveBrick(0, 1);
    public void OnMoveDownButton() => TryMoveBrick(0, -1);
    public void OnMoveLeftButton() => TryMoveBrick(-1, 0);
    public void OnMoveRightButton() => TryMoveBrick(1, 0);
    
    void Update()
    {
        if (!isGameActive) return;
        if (currentBrick == null) return;

        UpdateGhostPreviewPosition();

        HandleBrickFalling();
        HandleKeyboardInput();
        HandlePointerInput();
    }
    
    void HandleBrickFalling()
    {
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        float targetHeight = gridManager.GetRequiredHeight(currentBrickGridPos, brickSize);

        if (currentBrick.transform.position.y <= targetHeight + 0.01f)
        {
            OnBrickLanded();
            return;
        }

        // Apply multiplier to only the bricks' fall speed (current falling brick)
        float fallAmount = currentBrickFallSpeed * fallSpeedMultiplier * Time.deltaTime;
        currentBrick.transform.position += Vector3.down * fallAmount;
    }
    
    void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) TryMoveBrick(0, 1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) TryMoveBrick(0, -1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) TryMoveBrick(1, 0);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) TryMoveBrick(-1, 0);
        else if (Input.GetKeyDown(KeyCode.R)) RotateCurrentBrick();
    }

    // Handle mouse click or touch tap to rotate the current brick when tapped/clicked
    void HandlePointerInput()
    {
        if (currentBrick == null) return;

        // Mouse click (editor / standalone)
        if (Input.GetMouseButtonDown(0))
        {
            ProcessPointer(Input.mousePosition);
            return;
        }

        // Touch (mobile)
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                ProcessPointer(touch.position);
            }
        }
    }

    void ProcessPointer(Vector2 screenPos)
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            var hitTransform = hit.collider.transform;

            // Ignore ghost preview
            if (ghostPreview != null && (hit.collider.gameObject == ghostPreview || hitTransform.IsChildOf(ghostPreview.transform)))
                return;

            // If the hit object is the current brick or a child of it, rotate
            if (currentBrick != null && (hit.collider.gameObject == currentBrick || hitTransform.IsChildOf(currentBrick.transform)))
            {
                RotateCurrentBrick();
            }
        }
    }
    
    void TryMoveBrick(int deltaX, int deltaY)
    {
        if (currentBrick == null) return;
        
        Vector2Int newPos = currentBrickGridPos + new Vector2Int(deltaX, deltaY);
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        
        if (gridManager.IsValidPosition(newPos, brickSize))
        {
            currentBrickGridPos = newPos;
            Vector3 worldPos = gridManager.GetGridPosition(newPos, brickSize);
            float preservedY = currentBrick.transform.position.y;
            AlignBrickToGridXZ(currentBrick, worldPos);
            Vector3 tmp = currentBrick.transform.position; tmp.y = preservedY; currentBrick.transform.position = tmp;

            UpdateGhostPreviewPosition();
        }
    }

    void RotateCurrentBrick()
    {
        if (currentBrick == null) return;

        // Mevcut durumu kaydet
        Vector2Int oldSize = gridManager.GetBrickSize(currentBrick);
        // Disable rotation for square bricks of size 1x1 and 2x2
        if (oldSize.x == oldSize.y && (oldSize.x == 1 || oldSize.x == 2))
        {
            Debug.Log("Rotation disabled for 1x1 and 2x2 square bricks.");
            return;
        }
        Vector2Int oldGridPos = currentBrickGridPos;
        float currentY = currentBrick.transform.position.y;

        Debug.Log($"Before rotation - Grid: ({oldGridPos.x},{oldGridPos.y}), Size: {oldSize.x}x{oldSize.y}");

        // Prepare visual to preserve appearance during instant root rotation
        int dir = 1; // clockwise 90
        var vfx = currentBrick.GetComponent<VisualBrickEffects>();
        if (vfx != null) vfx.PrepareForInstantRootRotation(dir);

        // Brick'i döndür (root transform - used for grid logic)
        currentBrick.transform.Rotate(0, 90, 0);
        
        // Yeni boyutu al
        Vector2Int newSize = gridManager.GetBrickSize(currentBrick);

        Debug.Log($"After rotation - New Size: {newSize.x}x{newSize.y}");

        // YENİ YAKLAŞIM: Mevcut dünya pozisyonundan grid pozisyonunu yeniden hesapla
        Vector3 currentWorldPos = currentBrick.transform.position;
        Vector2Int estimatedGridPos = gridManager.WorldToGridPosition(currentWorldPos);

        // Brick boyutuna göre grid pozisyonunu ayarla
        Vector2Int newGridPos = CalculateAdjustedGridPosition(estimatedGridPos, newSize);

        Debug.Log($"Estimated grid: {estimatedGridPos}, Adjusted grid: {newGridPos}");

        // Yeni pozisyon geçerli mi kontrol et
        if (gridManager.IsValidPosition(newGridPos, newSize))
        {
            currentBrickGridPos = newGridPos;
            ForceAlignBrickToGrid(currentY);
            Debug.Log($"Rotation successful - Final position: {currentBrickGridPos}");
            // After root moved to final position, play visual rotation (rotation-only, no positional shift)
            if (vfx != null)
            {
                vfx.PlayRotateVisual();
            }
        }
        else
        {
            // Geçerli değilse, brick'i geri döndür and reset visual
            Debug.LogWarning("Invalid position after rotation, reverting...");
            currentBrick.transform.Rotate(0, -90, 0);
            if (vfx != null)
            {
                // Reset visual immediately (no animation)
                vfx.PrepareForInstantRootRotation(0);
                var visualTf = currentBrick.transform.Find("_Visual");
                if (visualTf != null) visualTf.localRotation = Quaternion.identity;
            }
        }
    }

    Vector2Int CalculateAdjustedGridPosition(Vector2Int estimatedPos, Vector2Int brickSize)
    {
        // Brick'in tamamen grid içinde olmasını sağla
        Vector2Int adjustedPos = estimatedPos;
        
        // Brick grid'in sağ veya üst sınırından taşıyorsa, içeri al
        if (adjustedPos.x + brickSize.x > gridManager.GridWidth)
        {
            adjustedPos.x = gridManager.GridWidth - brickSize.x;
        }
        
        if (adjustedPos.y + brickSize.y > gridManager.GridHeight)
        {
            adjustedPos.y = gridManager.GridHeight - brickSize.y;
        }
        
        // Negatif pozisyonları önle
        adjustedPos.x = Mathf.Max(0, adjustedPos.x);
        adjustedPos.y = Mathf.Max(0, adjustedPos.y);
        
        return adjustedPos;
    }

    void ForceAlignBrickToGrid(float preserveY)
    {
        if (currentBrick == null) return;
        
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        
        // Grid pozisyonundan world pozisyonunu hesapla
        Vector3 targetWorldPos = gridManager.GetGridPosition(currentBrickGridPos, brickSize);
        
        // Brick'i tamamen yeniden konumlandır
        currentBrick.transform.position = new Vector3(targetWorldPos.x, preserveY, targetWorldPos.z);
        
        // Ghost preview'ı güncelle
        UpdateGhostPreviewPosition();
        
        Debug.Log($"Brick forcefully aligned to grid: {currentBrickGridPos}, World: {currentBrick.transform.position}");
    }
    
    void OnBrickLanded()
    {
        if (currentBrick == null) return;
        
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        float targetHeight = gridManager.GetRequiredHeight(currentBrickGridPos, brickSize);
        
        Vector3 finalPos = gridManager.GetGridPosition(currentBrickGridPos, brickSize);
        AlignBrickToGridXZ(currentBrick, finalPos);
        Vector3 landedPos = currentBrick.transform.position; landedPos.y = targetHeight; currentBrick.transform.position = landedPos;
        
        gridManager.PlaceBrick(currentBrickGridPos, brickSize, currentBrick, currentBrickColor);

        // Play snap visual if available (small up + down + vibrate) to emphasize tactile placement
        var landedVfx = currentBrick.GetComponent<VisualBrickEffects>();
        if (landedVfx != null) landedVfx.PlaySnapVisual();

        landedBricks.Add(currentBrick);
        currentBrick.name = currentBrick.name + $"_Landed_{landedBricks.Count}";

        RemoveGhostPreview();
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBrickSnap();
        
        AddGold(scoreSettings.goldPerBrick);
        
        // Clear currentBrick reference so spawn logic can create next one while placed brick animates
        currentBrick = null;
        
        CheckForMatchingLines();
        
        if (isGameActive) Invoke(nameof(SpawnNewBrick), autoFallDelay);
    }
    
    void ClearCurrentBrick()
    {
        if (currentBrick != null)
        {
            Destroy(currentBrick);
            currentBrick = null;
        }
        RemoveGhostPreview();
    }
    
    void CheckForMatchingLines()
    {
        int highest = gridManager.GetHighestLayer();
        
        for (int layer = 0; layer <= highest; layer++)
        {
            if (IsLayerCompleteWithSameColor(layer, out LevelManager.BrickColor layerColor))
            {
                HashSet<GameObject> bricksToDestroy = new HashSet<GameObject>();
                
                for (int x = 0; x < gridManager.GridWidth; x++)
                {
                    for (int y = 0; y < gridManager.GridHeight; y++)
                    {
                        var brick = gridManager.GetBrickAt(x, y, layer);
                        if (brick != null)
                            bricksToDestroy.Add(brick);
                    }
                }
                
                FloodFill3DConnectedBricks(bricksToDestroy, layerColor, layer);
                StartCoroutine(DestroyBricksWithEffects(new List<GameObject>(bricksToDestroy), layer, layerColor));
                AddGold(scoreSettings.goldPerLayer);
                comboCount++;
                break;
            }
        }
    }
    
    bool IsLayerCompleteWithSameColor(int layer, out LevelManager.BrickColor color)
    {
        color = LevelManager.BrickColor.Orange;
        LevelManager.BrickColor? firstColor = null;
        
        for (int x = 0; x < gridManager.GridWidth; x++)
        {
            for (int y = 0; y < gridManager.GridHeight; y++)
            {
                var brick = gridManager.GetBrickAt(x, y, layer);
                
                if (brick == null)
                {
                    return false;
                }
                
                var cellColor = gridManager.GetBrickColorAt(x, y, layer);
                
                if (firstColor == null)
                {
                    firstColor = cellColor;
                    color = cellColor;
                }
                else if (cellColor != firstColor)
                {
                    return false;
                }
            }
        }
        
        return firstColor != null;
    }
    
    void FloodFill3DConnectedBricks(HashSet<GameObject> bricksToDestroy, LevelManager.BrickColor targetColor, int startLayer)
    {
        cachedFloodFillQueue.Clear();
        cachedFloodFillVisited.Clear();
        
        for (int x = 0; x < gridManager.GridWidth; x++)
        {
            for (int y = 0; y < gridManager.GridHeight; y++)
            {
                var brick = gridManager.GetBrickAt(x, y, startLayer);
                if (brick != null && gridManager.GetBrickColorAt(x, y, startLayer) == targetColor)
                {
                    cachedFloodFillQueue.Enqueue((x, y, startLayer));
                    cachedFloodFillVisited.Add((x, y, startLayer));
                }
            }
        }
        
        while (cachedFloodFillQueue.Count > 0)
        {
            var (currentX, currentY, currentLayer) = cachedFloodFillQueue.Dequeue();
            var brick = gridManager.GetBrickAt(currentX, currentY, currentLayer);
            if (brick != null)
                bricksToDestroy.Add(brick);
            
            CheckAndAddNeighbor(currentX - 1, currentY, currentLayer, targetColor, cachedFloodFillQueue, cachedFloodFillVisited);
            CheckAndAddNeighbor(currentX + 1, currentY, currentLayer, targetColor, cachedFloodFillQueue, cachedFloodFillVisited);
            CheckAndAddNeighbor(currentX, currentY - 1, currentLayer, targetColor, cachedFloodFillQueue, cachedFloodFillVisited);
            CheckAndAddNeighbor(currentX, currentY + 1, currentLayer, targetColor, cachedFloodFillQueue, cachedFloodFillVisited);
            CheckAndAddNeighbor(currentX, currentY, currentLayer - 1, targetColor, cachedFloodFillQueue, cachedFloodFillVisited);
            CheckAndAddNeighbor(currentX, currentY, currentLayer + 1, targetColor, cachedFloodFillQueue, cachedFloodFillVisited);
        }
    }
    
    void CheckAndAddNeighbor(int x, int y, int layer, LevelManager.BrickColor targetColor, 
                             Queue<(int, int, int)> queue, HashSet<(int, int, int)> visited)
    {
        if (x < 0 || x >= gridManager.GridWidth || 
            y < 0 || y >= gridManager.GridHeight || 
            layer < 0 || layer > gridManager.GetHighestLayer())
        {
            return;
        }
        
        if (visited.Contains((x, y, layer)))
        {
            return;
        }
        
        var brick = gridManager.GetBrickAt(x, y, layer);
        if (brick != null && gridManager.GetBrickColorAt(x, y, layer) == targetColor)
        {
            queue.Enqueue((x, y, layer));
            visited.Add((x, y, layer));
        }
    }
    
    IEnumerator DestroyBricksWithEffects(List<GameObject> bricksToDestroy, int baseLayer, LevelManager.BrickColor layerColor)
    {
        yield return new WaitForSeconds(layerClearDelay);
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLayerCompleteExplosion();
        
        // Determine the lowest layer being removed so we know from which layer to start collapsing
        int lowestRemovedLayer = int.MaxValue;
        
        foreach (var brick in bricksToDestroy)
        {
            if (brick != null)
            {
                var gridPos = gridManager.GetBrickGridPosition(brick);
                int brickLayer = gridManager.GetLayerAtBrickPosition(brick, gridPos);
                if (brickLayer < lowestRemovedLayer) lowestRemovedLayer = brickLayer;
                
                Vector3 brickPosition = brick.transform.position;
                
                // Spawn UI-directed particles for this destroyed brick (one brick => many UI particles)
                if (levelManager.DestroyedBrickCounts.ContainsKey(layerColor))
                {
                    var uiElement = GetBrickCountUIElement(layerColor);
                    if (uiElement != null)
                    {
                        effectManager.SpawnUIParticlesForBrick(layerColor, brickPosition, uiElement);
                    }
                    else
                    {
                        // If no UI element, immediately notify level manager once
                        levelManager.OnBrickDestroyed(layerColor);
                    }
                }
                
                gridManager.RemoveBrickFromGrid(brick);
                landedBricks.Remove(brick);
                Destroy(brick);
            }
        }
        
        yield return new WaitForSeconds(0.3f);
        
        // Collapse all bricks above the lowest removed layer
        if (lowestRemovedLayer < int.MaxValue)
        {
            // Gather all bricks that need to fall (from layers above the removed one)
            List<GameObject> bricksToFall = gridManager.GetBricksAboveLayer(lowestRemovedLayer - 1);
            
            // Sort by current layer ascending so lower bricks settle first
            bricksToFall.Sort((a, b) => {
                int layerA = gridManager.GetBrickLayer(a);
                int layerB = gridManager.GetBrickLayer(b);
                return layerA.CompareTo(layerB);
            });
            
            // Clear grid data for all falling bricks first so they don't block each other
            foreach (var brick in bricksToFall)
            {
                if (brick != null)
                    gridManager.ClearBrickFromGrid(brick);
            }
            
            // Calculate target heights and re-place bricks in grid BEFORE visual animation
            // This ensures grid data is immediately correct for new brick placement
            Dictionary<GameObject, float> targetHeights = new Dictionary<GameObject, float>();
            foreach (var brick in bricksToFall)
            {
                if (brick == null) continue;
                Vector2Int gridPos = gridManager.GetBrickGridPositionCached(brick);
                Vector2Int brickSize = gridManager.GetBrickSize(brick);
                LevelManager.BrickColor brickColor = gridManager.GetBrickColorCached(brick);
                
                // Calculate height based on current grid state (lower bricks already placed)
                float newHeight = gridManager.GetRequiredHeight(gridPos, brickSize);
                targetHeights[brick] = newHeight;
                
                // Immediately re-place in grid so next brick calculations are correct
                gridManager.PlaceBrick(gridPos, brickSize, brick, brickColor);
            }
            
            // Now animate all bricks falling to their pre-calculated heights in parallel
            foreach (var brick in bricksToFall)
            {
                if (brick != null && targetHeights.ContainsKey(brick))
                {
                    StartCoroutine(AnimateBrickFall(brick, targetHeights[brick]));
                }
            }
            
            // Wait for visual animation to complete
            yield return new WaitForSeconds(Mathf.Max(0.01f, brickFallDuration / fallSpeedMultiplier));
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAllBricksSettled();
        }
    }
    
    // Simple animation coroutine - just moves brick visually, grid data already updated
    IEnumerator AnimateBrickFall(GameObject brick, float targetHeight)
    {
        if (brick == null) yield break;
        
        var startPos = brick.transform.position;
        var endPos = new Vector3(startPos.x, targetHeight, startPos.z);
        
        float elapsed = 0f;
        float effectiveDuration = Mathf.Max(0.01f, brickFallDuration / fallSpeedMultiplier);
        while (elapsed < effectiveDuration && brick != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / effectiveDuration;
            brick.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        if (brick != null)
            brick.transform.position = endPos;
    }
    
    IEnumerator MoveBrickDown(GameObject brick, int removedLayer)
    {
        if (brick == null) yield break;
        
        var startPos = brick.transform.position;
        float newHeight = gridManager.GetRequiredHeightForBrick(brick);
        var endPos = new Vector3(startPos.x, newHeight, startPos.z);
        
        float elapsed = 0f;
        float effectiveDuration = Mathf.Max(0.01f, brickFallDuration / fallSpeedMultiplier);
        while (elapsed < effectiveDuration && brick != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / effectiveDuration;
            brick.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        if (brick != null)
        {
            brick.transform.position = endPos;
            gridManager.UpdateBrickPosition(brick);
        }
    }
    
    // Coroutine that moves a brick down visually then re-registers it in the grid at the correct layer
    IEnumerator MoveBrickDownAndReplace(GameObject brick)
    {
        if (brick == null) yield break;
        
        // Get brick's grid position (X/Y) — this doesn't change, only layer changes
        Vector2Int gridPos = gridManager.GetBrickGridPositionCached(brick);
        Vector2Int brickSize = gridManager.GetBrickSize(brick);
        LevelManager.BrickColor brickColor = gridManager.GetBrickColorCached(brick);
        
        var startPos = brick.transform.position;
        // Calculate target height based on what's currently in the grid (brick was cleared, so it finds correct layer)
        float newHeight = gridManager.GetRequiredHeight(gridPos, brickSize);
        var endPos = new Vector3(startPos.x, newHeight, startPos.z);
        
        float elapsed = 0f;
        float effectiveDuration = Mathf.Max(0.01f, brickFallDuration / fallSpeedMultiplier);
        while (elapsed < effectiveDuration && brick != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / effectiveDuration;
            brick.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        if (brick != null)
        {
            brick.transform.position = endPos;
            // Re-place brick in grid at the new correct layer
            gridManager.PlaceBrick(gridPos, brickSize, brick, brickColor);
        }
    }
    
    GameObject GetBrickCountUIElement(LevelManager.BrickColor color)
    {
        if (levelManager == null) return null;
        var uiElements = levelManager.GetType().GetField("brickCountUIElements", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(levelManager) as Dictionary<LevelManager.BrickColor, GameObject>;
        return uiElements?.ContainsKey(color) == true ? uiElements[color] : null;
    }

    #region Ghost Preview
    private void CreateGhostPreview(GameObject brickPrefab, Vector2Int gridPos, Vector2Int size)
    {
        RemoveGhostPreview();

        Vector3 ghostPos = gridManager.GetGridPosition(gridPos, size);
        float targetHeight = gridManager.GetRequiredHeight(gridPos, size);
        ghostPos.y = targetHeight + 0.001f;

        ghostPreview = Instantiate(brickPrefab, ghostPos, Quaternion.identity);
        ghostPreview.name = brickPrefab.name + "_Ghost";

        foreach (var col in ghostPreview.GetComponentsInChildren<Collider>()) Destroy(col);
        var rb = ghostPreview.GetComponent<Rigidbody>(); if (rb != null) Destroy(rb);

        ghostRenderers.Clear();
        foreach (var renderer in ghostPreview.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;
            
            var origMat = renderer.material;
                if (!ghostMaterialCache.TryGetValue(origMat, out var cachedMat))
                {
                    cachedMat = new Material(origMat);
                    // Make ghost silhouettes white with configured alpha
                    Color c = Color.white;
                    c.a = ghostBaseAlpha;
                    cachedMat.color = c;
                    // Force a plain white main texture so tint becomes pure white silhouette
                    cachedMat.mainTexture = Texture2D.whiteTexture;
                cachedMat.SetFloat("_Mode", 3);
                cachedMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                cachedMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                cachedMat.SetInt("_ZWrite", 0);
                cachedMat.DisableKeyword("_ALPHATEST_ON");
                cachedMat.EnableKeyword("_ALPHABLEND_ON");
                cachedMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                cachedMat.renderQueue = 3000;
                ghostMaterialCache[origMat] = cachedMat;
            }
            
            renderer.material = cachedMat;
            ghostRenderers.Add(renderer);
        }

        ghostPreview.transform.position = ghostPos;
        if (currentBrick != null)
            ghostPreview.transform.rotation = currentBrick.transform.rotation;
    }

    private void UpdateGhostPreviewPosition()
    {
        if (ghostPreview == null || currentBrick == null) return;

        var size = gridManager.GetBrickSize(currentBrick);
        Vector3 pos = gridManager.GetGridPosition(currentBrickGridPos, size);
        float targetHeight = gridManager.GetRequiredHeight(currentBrickGridPos, size);
        pos.y = targetHeight + 0.001f;
        ghostPreview.transform.position = new Vector3(pos.x, pos.y, pos.z);
        ghostPreview.transform.rotation = currentBrick.transform.rotation;

        float distance = currentBrick.transform.position.y - targetHeight;
        float fadeFactor = 1f;
        if (distance <= 5f)
            fadeFactor = Mathf.Clamp01(distance / 5f);

        float alpha = ghostBaseAlpha * fadeFactor;
        foreach (var r in ghostRenderers)
        {
            if (r == null) continue;
            var m = r.material;
            if (m == null) continue;
            Color c = m.color;
            c.a = alpha;
            m.color = c;
        }
    }

    private void RemoveGhostPreview()
    {
        if (ghostPreview != null)
        {
            Destroy(ghostPreview);
            ghostPreview = null;
        }
        ghostRenderers.Clear();
    }
    
    private void AlignBrickToGridXZ(GameObject brick, Vector3 targetCenterXZ)
    {
        if (brick == null) return;
        
        // YENİ VE DAHA ETKİLİ HİZALAMA
        var collider = brick.GetComponent<BoxCollider>();
        if (collider != null)
        {
            // Collider'ın merkezini kullanarak tam hizalama
            Vector3 colliderCenter = brick.transform.TransformPoint(collider.center);
            Vector3 offset = colliderCenter - brick.transform.position;
            
            // Sadece X ve Z offset'ini kullan
            Vector3 offsetXZ = new Vector3(offset.x, 0, offset.z);
            Vector3 newPos = targetCenterXZ - offsetXZ;
            newPos.y = brick.transform.position.y;
            
            brick.transform.position = newPos;
            return;
        }

        // Fallback: Renderer bounds kullan
        var renderers = brick.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) 
                combinedBounds.Encapsulate(renderers[i].bounds);
            
            Vector3 centerDelta = new Vector3(
                targetCenterXZ.x - combinedBounds.center.x,
                0f,
                targetCenterXZ.z - combinedBounds.center.z
            );
            
            brick.transform.position += centerDelta;
        }
    }
    #endregion
}
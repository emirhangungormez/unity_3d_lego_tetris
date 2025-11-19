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
        
        [Header("Pause UI")]
        public Button pauseButton;
        public Text pauseText;
    }
    
    [System.Serializable]
    public class ScoreSettings
    {
        public int goldPerBrick = 10;
        public int goldPerLayer = 100;
        public int comboMultiplier = 2;
    }
    
    [System.Serializable]
    public class PauseSettings
    {
        public Sprite pauseSprite;
        public Sprite continueSprite;
        public int maxPauseChances = 3;
    }
    
    [Header("Core References")]
    public GridManager gridManager;
    public EffectManager effectManager;
    public LevelManager levelManager;
    public List<GameObject> brickPrefabs;
    
    [Header("UI Animation Settings")]
    public float uiScaleDuration = 0.3f;
    public float uiScaleAmount = 1.2f;
    
    [Header("UI References")]
    public UIReferences ui = new UIReferences();
    
    [Header("Score Settings")]
    public ScoreSettings scoreSettings = new ScoreSettings();
    
    [Header("Pause Settings")]
    public PauseSettings pauseSettings = new PauseSettings();
    
    [Header("Effect Timing")]
    public float layerClearDelay = 0.3f;
    public float brickFallDuration = 0.6f;
    public float autoFallDelay = 0.3f;
    
    [HideInInspector] public List<GameObject> landedBricks = new List<GameObject>();
    
    private GameObject currentBrick;
    private GameObject ghostPreview;
    private List<Renderer> ghostRenderers = new List<Renderer>();
    private BoxCollider currentBrickCollider;
    private Vector2Int currentBrickGridPos;
    private LevelManager.BrickColor currentBrickColor;
    private float currentBrickFallSpeed = 3f;
    private float nextFallTime;
    
    private bool isPaused, isGameActive;
    private int currentPauseChances, currentGold, comboCount;
    
    // Cached lists for GC optimization
    private static readonly List<GameObject> cachedAvailableBricks = new();
    private static readonly List<GameObject> cachedSuitableBricks = new();
    private static readonly Queue<(int, int, int)> cachedFloodFillQueue = new();
    private static readonly HashSet<(int, int, int)> cachedFloodFillVisited = new();
    
    // Renk kısıtlaması (üst üste 3 aynı renk engelleme)
    private Queue<LevelManager.BrickColor> recentBrickColors = new();
    private const int MaxConsecutiveColors = 3;
    
    // Material cache for ghost preview (avoid per-frame Material allocations)
    private Dictionary<Material, Material> ghostMaterialCache = new();
    private float ghostBaseAlpha = 0.35f;

    public bool IsGameActive => isGameActive;
    public bool IsPaused => isPaused;
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
        currentPauseChances = pauseSettings.maxPauseChances;
        currentBrick = null;
        landedBricks.Clear();
        ghostMaterialCache.Clear();
        recentBrickColors.Clear();

        if (ui.winPanel != null)
        {
            ui.winPanel.SetActive(false);
            var confetti = ui.winPanel.GetComponent<WinPanelConfetti>();
            if (confetti != null) confetti.StopConfetti();
        }
        if (ui.losePanel != null) ui.losePanel.SetActive(false);

        if (levelManager != null)
            levelManager.InitializeLevel();

        UpdatePauseUI();
        UpdateGoldUI();

        SpawnNewBrick();
    }



    void UpdateFallLine()
    {
        UpdateGhostPreviewPosition();
    }

    public IEnumerator ScaleUIElement(Transform element, float targetScale)
    {
        if (element == null) yield break;

        Vector3 originalScale = element.localScale;
        Vector3 scaledUp = originalScale * targetScale;

        float elapsed = 0f;
        while (elapsed < uiScaleDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (uiScaleDuration / 2f);
            element.localScale = Vector3.Lerp(originalScale, scaledUp, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < uiScaleDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (uiScaleDuration / 2f);
            element.localScale = Vector3.Lerp(scaledUp, originalScale, t);
            yield return null;
        }

        element.localScale = originalScale;
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
    }
    
    public void LoseLevel(string reason)
    {
        isGameActive = false;
        ClearCurrentBrick();
        if (ui.losePanel != null) ui.losePanel.SetActive(true);
        if (ui.loseReasonText != null) ui.loseReasonText.text = reason;
    }
    
    public void OnPauseButtonClicked()
    {
        if (currentPauseChances <= 0 && !isPaused) return;
        
        if (!isPaused)
        {
            isPaused = true;
            if (currentPauseChances > 0) currentPauseChances--;
        }
        else
        {
            isPaused = false;
        }
        
        UpdatePauseUI();
    }
    
    void UpdatePauseUI()
    {
        if (ui.pauseButton != null)
            ui.pauseButton.image.sprite = isPaused ? pauseSettings.continueSprite : pauseSettings.pauseSprite;
        
        if (ui.pauseText != null)
            ui.pauseText.text = currentPauseChances.ToString();
    }
    
    void SpawnNewBrick()
    {
        if (!isGameActive || isPaused) return;
        
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
        
        // Üst üste 3 aynı renk varsa, o rengi hariç tut
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
        Vector2Int spawnPos = new Vector2Int(
            Random.Range(0, gridManager.GridWidth - brickSize.x + 1),
            Random.Range(0, gridManager.GridHeight - brickSize.y + 1)
        );
        
        currentBrickGridPos = spawnPos;
        currentBrickColor = color;
        
        Vector3 worldPos = gridManager.GetGridPosition(spawnPos, brickSize);
        worldPos.y = 20f;

        currentBrick = Instantiate(brickPrefab, worldPos, Quaternion.identity);
        currentBrick.name = brickPrefab.name + "_Current";
        currentBrickCollider = currentBrick.GetComponent<BoxCollider>();

        levelManager.ApplyBrickTexture(currentBrick, color);

        Vector3 spawnCenter = gridManager.GetGridPosition(spawnPos, brickSize);
        float spawnY = currentBrick.transform.position.y;
        AlignBrickToGridXZ(currentBrick, spawnCenter);
        Vector3 sp = currentBrick.transform.position; sp.y = spawnY; currentBrick.transform.position = sp;

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
        if (!isGameActive || isPaused) return;
        if (currentBrick == null) return;
        
        UpdateGhostPreviewPosition();

        HandleBrickFalling();
        HandleKeyboardInput();
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

        float fallAmount = currentBrickFallSpeed * Time.deltaTime;
        currentBrick.transform.position += Vector3.down * fallAmount;
    }
    
    void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) TryMoveBrick(0, 1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) TryMoveBrick(0, -1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) TryMoveBrick(1, 0);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) TryMoveBrick(-1, 0);
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
    
    
    void OnBrickLanded()
    {
        if (currentBrick == null) return;
        
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        float targetHeight = gridManager.GetRequiredHeight(currentBrickGridPos, brickSize);
        
        Vector3 finalPos = gridManager.GetGridPosition(currentBrickGridPos, brickSize);
        AlignBrickToGridXZ(currentBrick, finalPos);
        Vector3 landedPos = currentBrick.transform.position; landedPos.y = targetHeight; currentBrick.transform.position = landedPos;
        
        gridManager.PlaceBrick(currentBrickGridPos, brickSize, currentBrick, currentBrickColor);
        
        landedBricks.Add(currentBrick);
        currentBrick.name = currentBrick.name + $"_Landed_{landedBricks.Count}";

        RemoveGhostPreview();
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBrickSnap();
        
        AddGold(scoreSettings.goldPerBrick);
        
        currentBrick = null;
        
        CheckForMatchingLines();
        
        if (!isPaused) Invoke(nameof(SpawnNewBrick), autoFallDelay);
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
        
        HashSet<int> affectedLayers = new HashSet<int>();
        
        foreach (var brick in bricksToDestroy)
        {
            if (brick != null)
            {
                var gridPos = gridManager.GetBrickGridPosition(brick);
                int brickLayer = gridManager.GetLayerAtBrickPosition(brick, gridPos);
                affectedLayers.Add(brickLayer);
                
                // Katman rengini kullan
                Vector3 brickPosition = brick.transform.position;
                
                effectManager.CreateParticlesForBrick(brick, layerColor, brickPosition);
                
                if (levelManager.DestroyedBrickCounts.ContainsKey(layerColor))
                {
                    var uiElement = GetBrickCountUIElement(layerColor);
                    if (uiElement != null)
                    {
                        StartCoroutine(effectManager.SendParticleToUI(layerColor, brickPosition, uiElement));
                    }
                }
                
                gridManager.RemoveBrickFromGrid(brick);
                landedBricks.Remove(brick);
                Destroy(brick);
            }
        }
        
        yield return new WaitForSeconds(0.3f);
        
        var sortedLayers = new List<int>(affectedLayers);
        sortedLayers.Sort();
        
        foreach (int layer in sortedLayers)
        {
            List<GameObject> bricksToFall = gridManager.GetBricksAboveLayer(layer);
            
            foreach (var brick in bricksToFall)
            {
                if (brick != null)
                {
                    StartCoroutine(MoveBrickDown(brick, layer));
                }
            }
        }
        
        if (sortedLayers.Count > 0)
        {
            yield return new WaitForSeconds(brickFallDuration);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAllBricksSettled();
        }
    }
    
    IEnumerator MoveBrickDown(GameObject brick, int removedLayer)
    {
        if (brick == null) yield break;
        
        var startPos = brick.transform.position;
        float newHeight = gridManager.GetRequiredHeightForBrick(brick);
        var endPos = new Vector3(startPos.x, newHeight, startPos.z);
        
        float elapsed = 0f;
        while (elapsed < brickFallDuration && brick != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / brickFallDuration;
            brick.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        if (brick != null)
        {
            brick.transform.position = endPos;
            gridManager.UpdateBrickPosition(brick);
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
                Color c = cachedMat.color;
                c.a = ghostBaseAlpha;
                cachedMat.color = c;
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
        
        var collider = brick.GetComponent<BoxCollider>();
        if (collider != null)
        {
            Vector3 colliderCenterWorld = brick.transform.TransformPoint(collider.center);
            Vector3 offsetXZ = new Vector3(
                colliderCenterWorld.x - brick.transform.position.x,
                0,
                colliderCenterWorld.z - brick.transform.position.z
            );
            
            Vector3 newPos = targetCenterXZ - offsetXZ;
            newPos.y = brick.transform.position.y;
            brick.transform.position = newPos;
            return;
        }

        var colliders = brick.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            Bounds combined = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) combined.Encapsulate(colliders[i].bounds);
            Vector3 delta = new Vector3(targetCenterXZ.x - combined.center.x, 0f, targetCenterXZ.z - combined.center.z);
            brick.transform.position += delta;
            return;
        }

        var renderers = brick.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combinedR = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) combinedR.Encapsulate(renderers[i].bounds);
            Vector3 deltaR = new Vector3(targetCenterXZ.x - combinedR.center.x, 0f, targetCenterXZ.z - combinedR.center.z);
            brick.transform.position += deltaR;
        }
    }
    #endregion
}
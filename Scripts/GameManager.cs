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
    
    // CurrentBrick sistem değişkenleri
    private GameObject currentBrick;
    private GameObject ghostPreview;
    private List<Renderer> ghostRenderers = new List<Renderer>();
    private float ghostBaseAlpha = 0.35f;
    private Vector2Int currentBrickGridPos;
    private LevelManager.BrickColor currentBrickColor;
    // Increase default fall speed so bricks don't fall too slowly (1.5x applied)
    private float currentBrickFallSpeed = 3f;
    private float nextFallTime;
    
    private bool isPaused, isGameActive;
    private int currentPauseChances, currentGold, comboCount;

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
        
        if (ui.winPanel != null)
        {
            ui.winPanel.SetActive(false);
            var confetti = ui.winPanel.GetComponent<WinPanelConfetti>();
            if (confetti != null) confetti.StopConfetti();
        }
        if (ui.losePanel != null) ui.losePanel.SetActive(false);
        
        levelManager.InitializeLevel();
        
        UpdatePauseUI();
        UpdateGoldUI();
        
        SpawnNewBrick();
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
        var availableColors = new List<LevelManager.BrickColor>(levelManager.TargetBrickCounts.Keys);
        var randomColor = availableColors[Random.Range(0, availableColors.Count)];
        
        SpawnBrick(randomBrickPrefab, randomColor);
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
        float spawnHeight = gridManager.GetRequiredHeight(spawnPos, brickSize);
        // Spawn higher so the brick visibly falls into place
        worldPos.y = 20f;

        currentBrick = Instantiate(brickPrefab, worldPos, Quaternion.identity);
        // Preserve prefab naming (so GetBrickSize can parse dimensions) but mark as current
        currentBrick.name = brickPrefab.name + "_Current";

        levelManager.ApplyBrickTexture(currentBrick, color);

        // Create a static ghost/preview at the target grid position so player sees where it will land
        CreateGhostPreview(brickPrefab, spawnPos, brickSize);

        nextFallTime = Time.time;
    }
    
    List<GameObject> GetBricksFromNames(List<string> brickNames)
    {
        if (brickNames.Count == 0) return new List<GameObject>(brickPrefabs);
        
        var result = new List<GameObject>();
        foreach (var name in brickNames)
        {
            var brick = FindBrickByName(name);
            if (brick != null) result.Add(brick);
        }
        return result;
    }
    
    GameObject FindBrickByName(string brickName)
    {
        foreach (var brick in brickPrefabs)
            if (brick.name == brickName) return brick;
        return null;
    }
    
    List<GameObject> GetSuitableBricks(List<GameObject> brickList)
    {
        var suitable = new List<GameObject>();
        foreach (var brick in brickList)
        {
            var size = gridManager.GetBrickSize(brick);
            if (size.x <= levelManager.CurrentLevel.gridSize.x && size.y <= levelManager.CurrentLevel.gridSize.y)
                suitable.Add(brick);
        }
        return suitable;
    }
    
    public void OnMoveUpButton() => TryMoveBrick(0, 1);
    public void OnMoveDownButton() => TryMoveBrick(0, -1);
    public void OnMoveLeftButton() => TryMoveBrick(-1, 0);
    public void OnMoveRightButton() => TryMoveBrick(1, 0);
    public void OnRotateButton() => TryRotateBrick();
    
    void Update()
    {
        if (!isGameActive || isPaused) return;
        if (currentBrick == null) return;
        // Update ghost preview each frame to handle fade while falling
        UpdateGhostPreviewPosition();

        HandleBrickFalling();
        HandleKeyboardInput();
    }
    
    void HandleBrickFalling()
    {
        // Continuous, frame-based falling for snappier movement
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        float targetHeight = gridManager.GetRequiredHeight(currentBrickGridPos, brickSize);

        if (currentBrick.transform.position.y <= targetHeight + 0.01f)
        {
            OnBrickLanded();
            return;
        }

        // Fall faster; speed tuned by currentBrickFallSpeed
        float fallAmount = currentBrickFallSpeed * Time.deltaTime;
        currentBrick.transform.position += Vector3.down * fallAmount;
    }
    
    void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) TryMoveBrick(0, 1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) TryMoveBrick(0, -1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) TryMoveBrick(1, 0);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) TryMoveBrick(-1, 0);
        else if (Input.GetKeyDown(KeyCode.R)) TryRotateBrick();
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
            worldPos.y = currentBrick.transform.position.y;
            currentBrick.transform.position = worldPos;

            // Update ghost preview position as well
            UpdateGhostPreviewPosition();
        }
    }
    
    void TryRotateBrick()
    {
        if (currentBrick == null) return;
        
        currentBrick.transform.Rotate(0, 90, 0);
        
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        
        if (!gridManager.IsValidPosition(currentBrickGridPos, brickSize))
        {
            currentBrick.transform.Rotate(0, -90, 0);
        }

        // Update ghost preview rotation/position
        UpdateGhostPreviewPosition();
    }
    
    void OnBrickLanded()
    {
        if (currentBrick == null) return;
        
        Vector2Int brickSize = gridManager.GetBrickSize(currentBrick);
        float targetHeight = gridManager.GetRequiredHeight(currentBrickGridPos, brickSize);
        
        Vector3 finalPos = gridManager.GetGridPosition(currentBrickGridPos, brickSize);
        finalPos.y = targetHeight;
        currentBrick.transform.position = finalPos;
        
        gridManager.PlaceBrick(currentBrickGridPos, brickSize, currentBrick, currentBrickColor);
        
        landedBricks.Add(currentBrick);
        // Preserve size-format in name and mark as landed
        currentBrick.name = currentBrick.name + $"_Landed_{landedBricks.Count}";

        // Remove ghost preview since brick has landed
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
                
                // Katmandaki tüm brick'leri topla
                for (int x = 0; x < gridManager.GridWidth; x++)
                {
                    for (int y = 0; y < gridManager.GridHeight; y++)
                    {
                        var brick = gridManager.GetBrickAt(x, y, layer);
                        if (brick != null)
                        {
                            bricksToDestroy.Add(brick);
                        }
                    }
                }
                
                // 3D zincirleme yok etme - temas eden aynı renkteki tüm brick'leri bul
                FloodFill3DConnectedBricks(bricksToDestroy, layerColor, layer);
                
                StartCoroutine(DestroyBricksWithEffects(new List<GameObject>(bricksToDestroy), layer));
                
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
        
        // Katmandaki tüm hücreleri kontrol et
        for (int x = 0; x < gridManager.GridWidth; x++)
        {
            for (int y = 0; y < gridManager.GridHeight; y++)
            {
                var brick = gridManager.GetBrickAt(x, y, layer);
                
                // Eğer herhangi bir hücre boşsa, katman tamamlanmamış
                if (brick == null)
                {
                    return false;
                }
                
                var cellColor = gridManager.GetBrickColorAt(x, y, layer);
                
                // İlk rengi belirle
                if (firstColor == null)
                {
                    firstColor = cellColor;
                    color = cellColor;
                }
                // Farklı bir renk varsa, eşleşme yok
                else if (cellColor != firstColor)
                {
                    return false;
                }
            }
        }
        
        // Tüm hücreler dolu ve aynı renk
        return firstColor != null;
    }
    
    void FloodFill3DConnectedBricks(HashSet<GameObject> bricksToDestroy, LevelManager.BrickColor targetColor, int startLayer)
    {
        Queue<(int x, int y, int layer)> queue = new Queue<(int, int, int)>();
        HashSet<(int x, int y, int layer)> visited = new HashSet<(int, int, int)>();
        
        // Başlangıç katmanındaki tüm hücreleri queue'ye ekle
        for (int x = 0; x < gridManager.GridWidth; x++)
        {
            for (int y = 0; y < gridManager.GridHeight; y++)
            {
                var brick = gridManager.GetBrickAt(x, y, startLayer);
                if (brick != null && gridManager.GetBrickColorAt(x, y, startLayer) == targetColor)
                {
                    queue.Enqueue((x, y, startLayer));
                    visited.Add((x, y, startLayer));
                }
            }
        }
        
        // BFS ile tüm temas eden aynı renkteki brick'leri bul
        while (queue.Count > 0)
        {
            var (currentX, currentY, currentLayer) = queue.Dequeue();
            
            var brick = gridManager.GetBrickAt(currentX, currentY, currentLayer);
            if (brick != null)
            {
                bricksToDestroy.Add(brick);
            }
            
            // 6 yönlü kontrol (yukarı, aşağı, sol, sağ, üst katman, alt katman)
            
            // Yatay komşular (aynı katmanda)
            CheckAndAddNeighbor(currentX - 1, currentY, currentLayer, targetColor, queue, visited);
            CheckAndAddNeighbor(currentX + 1, currentY, currentLayer, targetColor, queue, visited);
            CheckAndAddNeighbor(currentX, currentY - 1, currentLayer, targetColor, queue, visited);
            CheckAndAddNeighbor(currentX, currentY + 1, currentLayer, targetColor, queue, visited);
            
            // Dikey komşular (üst ve alt katmanlar)
            CheckAndAddNeighbor(currentX, currentY, currentLayer - 1, targetColor, queue, visited);
            CheckAndAddNeighbor(currentX, currentY, currentLayer + 1, targetColor, queue, visited);
        }
    }
    
    void CheckAndAddNeighbor(int x, int y, int layer, LevelManager.BrickColor targetColor, 
                             Queue<(int, int, int)> queue, HashSet<(int, int, int)> visited)
    {
        // Sınır kontrolleri
        if (x < 0 || x >= gridManager.GridWidth || 
            y < 0 || y >= gridManager.GridHeight || 
            layer < 0 || layer > gridManager.GetHighestLayer())
        {
            return;
        }
        
        // Zaten ziyaret edildiyse atla
        if (visited.Contains((x, y, layer)))
        {
            return;
        }
        
        // Brick var mı ve aynı renkte mi kontrol et
        var brick = gridManager.GetBrickAt(x, y, layer);
        if (brick != null && gridManager.GetBrickColorAt(x, y, layer) == targetColor)
        {
            queue.Enqueue((x, y, layer));
            visited.Add((x, y, layer));
        }
    }
    
    IEnumerator DestroyBricksWithEffects(List<GameObject> bricksToDestroy, int baseLayer)
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
                
                LevelManager.BrickColor brickColor = gridManager.GetBrickColor(brick);
                Vector3 brickPosition = brick.transform.position;
                
                effectManager.CreateParticlesForBrick(brick, brickColor, brickPosition);
                
                if (levelManager.DestroyedBrickCounts.ContainsKey(brickColor))
                {
                    var uiElement = GetBrickCountUIElement(brickColor);
                    if (uiElement != null)
                    {
                        StartCoroutine(effectManager.SendParticleToUI(brickColor, brickPosition, uiElement));
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
        
        var brickCountUIElements = levelManager.GetType()
            .GetField("brickCountUIElements", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(levelManager) as Dictionary<LevelManager.BrickColor, GameObject>;
        
        if (brickCountUIElements != null && brickCountUIElements.ContainsKey(color))
        {
            return brickCountUIElements[color];
        }
        
        return null;
    }

    #region Ghost Preview
    private void CreateGhostPreview(GameObject brickPrefab, Vector2Int gridPos, Vector2Int size)
    {
        RemoveGhostPreview();

        Vector3 ghostPos = gridManager.GetGridPosition(gridPos, size);
        float targetHeight = gridManager.GetRequiredHeight(gridPos, size);
        ghostPos.y = targetHeight + 0.001f; // slightly above final to avoid z-fighting

        ghostPreview = Instantiate(brickPrefab, ghostPos, Quaternion.identity);
        ghostPreview.name = brickPrefab.name + "_Ghost";

        // Make ghost non-interactive and translucent
        foreach (var col in ghostPreview.GetComponentsInChildren<Collider>()) Destroy(col);
        var rb = ghostPreview.GetComponent<Rigidbody>(); if (rb != null) Destroy(rb);

        ghostRenderers.Clear();
        foreach (var renderer in ghostPreview.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;
            var mat = new Material(renderer.material);
            Color c = mat.color;
            c.a = ghostBaseAlpha;
            mat.color = c;
            // Configure standard material for transparency
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            renderer.material = mat;
            ghostRenderers.Add(renderer);
        }

        // Keep ghost at same rotation as current brick if exists
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
        ghostPreview.transform.position = pos;
        ghostPreview.transform.rotation = currentBrick.transform.rotation;

        // Fade ghost when the falling brick is within 5 units of its target
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
    #endregion
}
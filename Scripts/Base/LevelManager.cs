using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class LevelManager : MonoBehaviour
{
    [System.Serializable]
    public class BrickColorCount
    {
        public BrickColor color;
        public int count;
    }
    
    [System.Serializable]
    public class LevelData
    {
        [Header("Level Info")]
        public int levelNumber = 1;
        public enum Difficulty { Easy, Medium, Hard }
        public Difficulty difficulty = Difficulty.Easy;
        
        [Header("Grid Settings")]
        public Vector2Int gridSize = new Vector2Int(8, 8);
        
        [Header("Level Requirements")]
        public List<BrickColorCount> brickColorCounts = new List<BrickColorCount>();
        public float levelTime = 180f;
        public int maxLayers = 10;
        
        [Header("Brick Configuration")]
        public List<string> allowedBrickNames = new List<string>();
    }
    
    [Header("Core References")]
    public GameManager gameManager;
    
    [Header("Level Configuration")]
    public List<LevelData> levels = new List<LevelData>();
    public int currentLevelIndex = 0;
    
    [Header("Level Number Display")]
    public GameObject levelArea;
    public Sprite[] numberSprites;
    public float numberSpacing = 50f;
    public float doubleDigitOffset = 50f;
    
    [Header("Level State UI")]
    public Image levelStateImage;
    public Sprite easySprite;
    public Sprite mediumSprite;
    public Sprite hardSprite;
    
    [Header("Brick Count UI")]
    public GameObject brickCountUIPrefab;
    public float brickCountUISpacing = 80f;
    
    [Header("UI Animation Settings")]
    public float timerPulseDuration = 1f;
    public float timerPulseScale = 1.05f;
    // Brick count UI animations
    public float uiSlideLeftAmount = 60f;
    public float uiSlideDuration = 0.18f;
    public float uiShiftDuration = 0.18f;
    public float uiPopScale = 1.25f;
    
    public enum BrickColor { Orange, Blue, Yellow, Purple, Green, White, Gray, Brown, Black }
    
    [System.Serializable]
    public class ColorSettings
    {
        public BrickColor colorType;
        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public Sprite brickCountBackground;
    }
    
    [Header("Available Colors")]
    public List<ColorSettings> availableColors = new List<ColorSettings>
    {
        new ColorSettings{ colorType = BrickColor.Orange, tiling = new Vector2(1f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Blue, tiling = new Vector2(1.1f, -0.5f), offset = new Vector2(0f, 0.5f) },
        // Yellow appears as the pink-style tile in some assets — apply requested tiling/offset
        new ColorSettings{ colorType = BrickColor.Yellow, tiling = new Vector2(1f, 1f), offset = new Vector2(0.7f, 0.25f) },
        new ColorSettings{ colorType = BrickColor.Purple, tiling = new Vector2(1.68f, 0f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Green, tiling = new Vector2(1.9f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.White, tiling = new Vector2(1f, 0.5f), offset = Vector2.zero },
        new ColorSettings{ colorType = BrickColor.Gray, tiling = new Vector2(2f, 0.5f), offset = Vector2.zero },
        new ColorSettings{ colorType = BrickColor.Brown, tiling = new Vector2(0f, 0.5f), offset = Vector2.zero },
        new ColorSettings{ colorType = BrickColor.Black, tiling = new Vector2(0f, -0.5f), offset = Vector2.zero }
    };
    
    private LevelData currentLevel;
    private float currentTime;
    private Dictionary<BrickColor, int> targetBrickCounts = new Dictionary<BrickColor, int>();
    private Dictionary<BrickColor, int> destroyedBrickCounts = new Dictionary<BrickColor, int>();
    private Dictionary<BrickColor, GameObject> brickCountUIElements = new Dictionary<BrickColor, GameObject>();
    private List<GameObject> levelNumberObjects = new List<GameObject>();
    // Track pending UI-directed particle arrivals per BRICK so we trigger OnBrickDestroyed
    // when the last particle of EACH brick arrives (not per UI element).
    // Key = unique brick ID, Value = remaining particle count
    private Dictionary<int, int> pendingParticleRemaining = new Dictionary<int, int>();
    private Dictionary<int, BrickColor> pendingParticleColor = new Dictionary<int, BrickColor>();
    private Dictionary<int, GameObject> pendingParticleUIElement = new Dictionary<int, GameObject>();
    private int nextBrickParticleId = 0;
    
    // Renk kısıtlaması (üst üste 3 kere aynı renk engelleme)
    private Queue<BrickColor> recentColors = new Queue<BrickColor>();
    private const int MaxConsecutiveColors = 3;

    public LevelData CurrentLevel => currentLevel;
    public float CurrentTime => currentTime;
    public Dictionary<BrickColor, int> TargetBrickCounts => targetBrickCounts;
    public Dictionary<BrickColor, int> DestroyedBrickCounts => destroyedBrickCounts;

    void Start()
    {
        if (gameManager == null)
            gameManager = GetComponent<GameManager>();
    }

    public void InitializeLevel()
    {
        if (levels.Count == 0 || currentLevelIndex >= levels.Count)
        {
            return;
        }
        
        currentLevel = levels[currentLevelIndex];
        currentTime = currentLevel.levelTime;
        recentColors.Clear();
        
        targetBrickCounts.Clear();
        destroyedBrickCounts.Clear();
        foreach (var brickCount in currentLevel.brickColorCounts)
        {
            targetBrickCounts[brickCount.color] = brickCount.count;
            destroyedBrickCounts[brickCount.color] = 0;
        }
        
        gameManager.gridManager.gridSize = currentLevel.gridSize;
        gameManager.gridManager.InitializeGrid();
        
        // Level state UI güncelle
        UpdateLevelStateUI(currentLevel.difficulty);
        CreateLevelNumberDisplay();
        CreateBrickCountUI();
        
        StartCoroutine(GameTimer());
        StartCoroutine(TimerPulseEffect());
    }
    
    void CreateLevelNumberDisplay()
    {
        foreach (var obj in levelNumberObjects)
            if (obj != null) Destroy(obj);
        levelNumberObjects.Clear();
        
        if (levelArea == null || numberSprites == null || numberSprites.Length < 10) return;
        
        Transform whichLevelTransform = levelArea.transform.Find("WhichLevel");
        if (whichLevelTransform == null) return;
        
        Image whichLevelImage = whichLevelTransform.GetComponent<Image>();
        if (whichLevelImage == null) return;
        
        string levelStr = currentLevel.levelNumber.ToString();
        RectTransform levelAreaRect = levelArea.GetComponent<RectTransform>();
        
        if (levelStr.Length >= 2 && levelAreaRect != null)
        {
            Vector2 currentPos = levelAreaRect.anchoredPosition;
            levelAreaRect.anchoredPosition = new Vector2(currentPos.x + doubleDigitOffset, currentPos.y);
        }
        
        if (levelStr.Length == 1)
        {
            int digit = int.Parse(levelStr);
            whichLevelImage.sprite = numberSprites[digit];
            whichLevelImage.preserveAspect = true;
            levelNumberObjects.Add(whichLevelTransform.gameObject);
        }
        else
        {
            whichLevelTransform.gameObject.SetActive(false);
            
            float totalWidth = (levelStr.Length - 1) * numberSpacing;
            float startX = -totalWidth / 2f;
            
            for (int i = 0; i < levelStr.Length; i++)
            {
                int digit = int.Parse(levelStr[i].ToString());
                GameObject numberObj = Instantiate(whichLevelTransform.gameObject, levelArea.transform);
                numberObj.name = $"Number_{digit}";
                numberObj.SetActive(true);
                
                RectTransform rect = numberObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(
                        whichLevelTransform.GetComponent<RectTransform>().anchoredPosition.x + startX + (i * numberSpacing),
                        whichLevelTransform.GetComponent<RectTransform>().anchoredPosition.y
                    );
                }
                
                Image img = numberObj.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = numberSprites[digit];
                    img.preserveAspect = true;
                }
                
                levelNumberObjects.Add(numberObj);
            }
        }
    }
    
    void CreateBrickCountUI()
    {
        foreach (var ui in brickCountUIElements.Values)
            if (ui != null) Destroy(ui);
        brickCountUIElements.Clear();
        
        if (brickCountUIPrefab == null) return;
        
        int index = 0;
        foreach (var brickCount in currentLevel.brickColorCounts)
        {
            GameObject uiElement = Instantiate(brickCountUIPrefab, brickCountUIPrefab.transform.parent);
            // Ensure new UI element appears at the top (first row) by setting sibling index to 0
            uiElement.transform.SetSiblingIndex(0);
            uiElement.SetActive(true);
            uiElement.name = $"BrickCountUI_{brickCount.color}";
            
            RectTransform rect = uiElement.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    brickCountUIPrefab.GetComponent<RectTransform>().anchoredPosition.x,
                    brickCountUIPrefab.GetComponent<RectTransform>().anchoredPosition.y - (index * brickCountUISpacing)
                );
            }
            
            Image bgImage = uiElement.GetComponent<Image>();
            if (bgImage != null)
            {
                ColorSettings colorSetting = availableColors.Find(c => c.colorType == brickCount.color);
                if (colorSetting != null && colorSetting.brickCountBackground != null)
                    bgImage.sprite = colorSetting.brickCountBackground;
            }
            
            Text countText = uiElement.GetComponentInChildren<Text>();
            if (countText != null)
            {
                countText.name = "CountText";
                countText.text = brickCount.count.ToString();
            }
            
            Transform checkmark = uiElement.transform.Find("Checkmark");
            if (checkmark != null)
            {
                checkmark.gameObject.SetActive(false);
            }
            
            brickCountUIElements[brickCount.color] = uiElement;
            index++;
        }
        
        if (brickCountUIPrefab != null)
            brickCountUIPrefab.SetActive(false);
    }
    
    public void UpdateBrickCountUI(BrickColor color)
    {
        if (!brickCountUIElements.ContainsKey(color)) return;
        
        GameObject uiElement = brickCountUIElements[color];
        Text countText = uiElement.GetComponentInChildren<Text>();
        Transform checkmark = uiElement.transform.Find("Checkmark");
        
        int remaining = targetBrickCounts[color] - destroyedBrickCounts[color];
        
        if (remaining <= 0)
        {
            // Play the completion visual animation for this UI entry (slide left + pop)
            StartCoroutine(PlayCountCompleteVisual(uiElement, color));
            // Also activate tick visuals in-scene/prefabs
            ActivateColorTick(color);
        }
        else
        {
            if (countText != null)
            {
                countText.text = remaining.ToString();
                StartCoroutine(gameManager.ScaleUIElement(countText.transform, gameManager.uiScaleAmount));
            }
        }
    }

    // Register how many particles will arrive for a SPECIFIC BRICK so we only decrement
    // the brick count once when the last particle of that brick reaches the UI.
    // Returns a unique ID to track this brick's particles.
    public int RegisterPendingParticlesForBrick(GameObject uiElement, BrickColor color, int count)
    {
        if (uiElement == null) return -1;
        int brickId = nextBrickParticleId++;
        pendingParticleRemaining[brickId] = Mathf.Max(1, count);
        pendingParticleColor[brickId] = color;
        pendingParticleUIElement[brickId] = uiElement;
        return brickId;
    }

    // Call when an individual particle arrives at the UI. When the remaining count for
    // that BRICK reaches zero, perform the brick-destroy bookkeeping.
    public void NotifyParticleArrival(int brickId)
    {
        if (brickId < 0) return;
        if (!pendingParticleRemaining.ContainsKey(brickId)) return;

        pendingParticleRemaining[brickId]--;
        if (pendingParticleRemaining[brickId] <= 0)
        {
            var color = pendingParticleColor.ContainsKey(brickId) ? pendingParticleColor[brickId] : BrickColor.Orange;
            pendingParticleRemaining.Remove(brickId);
            pendingParticleColor.Remove(brickId);
            pendingParticleUIElement.Remove(brickId);

            // This brick's particles all arrived - decrement count by 1
            OnBrickDestroyed(color);
        }
    }

    IEnumerator PlayCountCompleteVisual(GameObject uiElement, BrickColor color)
    {
        if (uiElement == null) yield break;

        // Find background image "brick_count_background" to slide left
        RectTransform bgRect = null;
        var bgTf = uiElement.transform.Find("brick_count_background");
        if (bgTf != null) bgRect = bgTf as RectTransform;

        // If no background found, just destroy and shift
        if (bgRect == null)
        {
            brickCountUIElements.Remove(color);
            Destroy(uiElement);
            StartCoroutine(ShiftRemainingBrickCountUI());
            yield break;
        }

        Vector3 origBgPos = bgRect.localPosition;
        Vector3 bgSlideTarget = origBgPos + Vector3.left * (uiSlideLeftAmount * 1.5f);

        // Phase 1: Slide background left
        float slideDuration = uiSlideDuration * 0.6f;
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            if (bgRect == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            bgRect.localPosition = Vector3.Lerp(origBgPos, bgSlideTarget, t);
            yield return null;
        }

        // Short delay after slide
        yield return new WaitForSeconds(0.1f);

        // Phase 2: UI Prefab destruction animation (wobble + scale down + fade)
        RectTransform uiRect = uiElement.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = uiElement.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = uiElement.AddComponent<CanvasGroup>();

        Vector3 origScale = uiRect != null ? uiRect.localScale : Vector3.one;
        Vector3 origRotation = uiRect != null ? uiRect.localEulerAngles : Vector3.zero;
        float destroyDuration = 0.35f;
        elapsed = 0f;

        while (elapsed < destroyDuration)
        {
            if (uiElement == null || uiRect == null) break;
            elapsed += Time.deltaTime;
            float t = elapsed / destroyDuration;

            // Wobble effect (slight rotation oscillation)
            float wobbleAngle = Mathf.Sin(t * Mathf.PI * 4f) * 8f * (1f - t);
            uiRect.localEulerAngles = origRotation + new Vector3(0f, 0f, wobbleAngle);

            // Scale down with slight squash
            float scaleT = Mathf.SmoothStep(1f, 0f, t);
            float squashX = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.15f * (1f - t);
            float squashY = 1f - Mathf.Sin(t * Mathf.PI * 2f) * 0.1f * (1f - t);
            uiRect.localScale = new Vector3(origScale.x * scaleT * squashX, origScale.y * scaleT * squashY, origScale.z);

            // Fade out
            canvasGroup.alpha = 1f - t;

            yield return null;
        }

        // Cleanup UI element
        brickCountUIElements.Remove(color);
        Destroy(uiElement);

        // Shift remaining elements to fill the gap
        StartCoroutine(ShiftRemainingBrickCountUI());
    }

    IEnumerator ShiftRemainingBrickCountUI()
    {
        // Build ordered list of remaining UI elements in the same order as currentLevel.brickColorCounts
        List<GameObject> ordered = new List<GameObject>();
        foreach (var bc in currentLevel.brickColorCounts)
        {
            if (brickCountUIElements.ContainsKey(bc.color))
                ordered.Add(brickCountUIElements[bc.color]);
        }

        // Animate each to its new anchored position
        float elapsed = 0f;
        // capture start positions
        List<RectTransform> rects = new List<RectTransform>();
        List<Vector2> starts = new List<Vector2>();
        List<Vector2> targets = new List<Vector2>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var obj = ordered[i];
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect == null) continue;
            rects.Add(rect);
            starts.Add(rect.anchoredPosition);
            Vector2 target = new Vector2(
                brickCountUIPrefab.GetComponent<RectTransform>().anchoredPosition.x,
                brickCountUIPrefab.GetComponent<RectTransform>().anchoredPosition.y - (i * brickCountUISpacing)
            );
            targets.Add(target);
        }

        while (elapsed < uiShiftDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / uiShiftDuration));
            for (int i = 0; i < rects.Count; i++)
            {
                rects[i].anchoredPosition = Vector2.Lerp(starts[i], targets[i], t);
            }
            yield return null;
        }

        // ensure final positions
        for (int i = 0; i < rects.Count; i++)
            rects[i].anchoredPosition = targets[i];

        yield break;
    }

    private void ActivateColorTick(BrickColor color)
    {
        // 1) Try to enable Tick in the UI element (if exists)
        if (brickCountUIElements.ContainsKey(color))
        {
            var uiElement = brickCountUIElements[color];
            var tick = uiElement.transform.Find("Tick");
            if (tick != null) tick.gameObject.SetActive(true);
        }

        // 2) Enable Tick on all instantiated bricks in the grid that match this color
        if (gameManager != null && gameManager.gridManager != null)
        {
            var gm = gameManager;
            var grid = gm.gridManager;
            int highest = grid.GetHighestLayer();
            for (int layer = 0; layer <= highest; layer++)
            {
                var bricks = grid.GetBricksInLayer(layer);
                foreach (var brick in bricks)
                {
                    if (brick == null) continue;
                    var bColor = grid.GetBrickColor(brick);
                    if (bColor == color)
                    {
                        var tickTransform = brick.transform.Find("Tick");
                        if (tickTransform != null)
                            tickTransform.gameObject.SetActive(true);
                    }
                }
            }
        }

        // 3) As a fallback/visual aid, enable Tick child on brick prefabs (if present)
        if (gameManager != null && gameManager.brickPrefabs != null)
        {
            foreach (var prefab in gameManager.brickPrefabs)
            {
                if (prefab == null) continue;
                var tick = prefab.transform.Find("Tick");
                if (tick != null)
                    tick.gameObject.SetActive(true);
            }
        }
    }
    
    IEnumerator TimerPulseEffect()
    {
        if (gameManager.ui.timerText == null) yield break;
        
        Transform timerTransform = gameManager.ui.timerText.transform;
        Vector3 originalScale = timerTransform.localScale;
        
        while (gameManager.IsGameActive)
        {
            float elapsed = 0f;
            while (elapsed < timerPulseDuration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (timerPulseDuration / 2f);
                timerTransform.localScale = Vector3.Lerp(originalScale, originalScale * timerPulseScale, t);
                yield return null;
            }
            
            elapsed = 0f;
            while (elapsed < timerPulseDuration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (timerPulseDuration / 2f);
                timerTransform.localScale = Vector3.Lerp(originalScale * timerPulseScale, originalScale, t);
                yield return null;
            }
        }
    }
    
    IEnumerator GameTimer()
    {
        while (gameManager.IsGameActive && currentTime > 0)
        {
            // Pause removed; timer ticks while game is active
            currentTime -= Time.deltaTime;
            UpdateTimerUI();

            if (currentTime <= 0)
            {
                currentTime = 0;
                gameManager.LoseLevel("Süre Doldu!");
                yield break;
            }

            yield return null;
        }
    }
    
    void UpdateTimerUI()
    {
        if (gameManager.ui.timerText != null)
        {
            int min = Mathf.FloorToInt(currentTime / 60f);
            int sec = Mathf.FloorToInt(currentTime % 60f);
            gameManager.ui.timerText.text = $"{min}:{sec:00}";
        }
    }
    
    public void OnBrickDestroyed(BrickColor color)
    {
        if (!targetBrickCounts.ContainsKey(color)) return;
        
        destroyedBrickCounts[color]++;
        UpdateBrickCountUI(color);
        CheckAllTargetsCompleted();
    }
    
    void CheckAllTargetsCompleted()
    {
        bool allCompleted = true;
        foreach (var kvp in targetBrickCounts)
        {
            if (destroyedBrickCounts[kvp.Key] < kvp.Value)
            {
                allCompleted = false;
                break;
            }
        }
        
        if (allCompleted)
        {
            gameManager.WinLevel();
        }
    }
    
    public List<BrickColor> GetCurrentLevelColors()
    {
        if (currentLevel == null) return new List<BrickColor>();
        return currentLevel.brickColorCounts.Select(bc => bc.color).ToList();
    }
    
    public ColorSettings GetColorSettings(BrickColor color)
    {
        foreach (var cs in availableColors)
            if (cs.colorType == color) return cs;
        return availableColors[0];
    }
    
    public void ApplyBrickTexture(GameObject brick, BrickColor color)
    {
        if (availableColors.Count == 0) return;
        
        var colorSettings = GetColorSettings(color);
        foreach (var renderer in brick.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;
            
            var mat = new Material(renderer.material);
            mat.name = $"BrickMaterial_{color}";
            mat.mainTextureScale = colorSettings.tiling;
            mat.mainTextureOffset = colorSettings.offset;
            renderer.material = mat;
        }
    }
    
    void UpdateLevelStateUI(LevelData.Difficulty difficulty)
    {
        if (levelStateImage == null) return;
        
        levelStateImage.sprite = difficulty switch
        {
            LevelData.Difficulty.Easy => easySprite,
            LevelData.Difficulty.Medium => mediumSprite,
            LevelData.Difficulty.Hard => hardSprite,
            _ => null
        };
    }
    
    void OnDestroy()
    {
        foreach (var obj in levelNumberObjects)
            if (obj != null) Destroy(obj);
        
        foreach (var ui in brickCountUIElements.Values)
            if (ui != null) Destroy(ui);
    }
    
    /// <summary>
    /// Advance to the next level (max 100 levels)
    /// </summary>
    public void NextLevel()
    {
        if (currentLevelIndex < levels.Count - 1 && currentLevelIndex < 99)
        {
            currentLevelIndex++;
            ReloadLevel();
        }
        else
        {
            Debug.LogWarning("No more levels available or reached maximum level (100)");
        }
    }
    
    /// <summary>
    /// Restart the current level - clear scene and reload
    /// </summary>
    public void RestartLevel()
    {
        ReloadLevel();
    }
    
    /// <summary>
    /// Reload the current level - clear all game objects and reinitialize
    /// </summary>
    void ReloadLevel()
    {
        if (gameManager == null) return;
        
        // Disable game to prevent any game logic during reload
        gameManager.SetGameActive(false);
        
        // Clean up current level
        CleanupLevel();
        
        // Wait a frame then reload the scene
        StartCoroutine(ReloadLevelDelayed());
    }
    
    IEnumerator ReloadLevelDelayed()
    {
        yield return null;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Clean up level objects and reset state
    /// </summary>
    void CleanupLevel()
    {
        // Clear level number displays
        foreach (var obj in levelNumberObjects)
            if (obj != null) Destroy(obj);
        levelNumberObjects.Clear();
        
        // Clear brick count UI
        foreach (var ui in brickCountUIElements.Values)
            if (ui != null) Destroy(ui);
        brickCountUIElements.Clear();
        
        // Reset counters
        destroyedBrickCounts.Clear();
        targetBrickCounts.Clear();
        
        // Destroy all bricks currently in the game
        if (gameManager != null && gameManager.gridManager != null)
        {
            var grid = gameManager.gridManager;
            int highest = grid.GetHighestLayer();
            for (int layer = 0; layer <= highest; layer++)
            {
                var bricks = grid.GetBricksInLayer(layer);
                foreach (var brick in bricks)
                {
                    if (brick != null) Destroy(brick);
                }
            }
        }
        
        // Stop all coroutines
        StopAllCoroutines();
    }
}
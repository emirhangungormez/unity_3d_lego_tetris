using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class GameManager : MonoBehaviour
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
    
    [Header("Core References")]
    public GridManager gridManager;
    public EffectManager effectManager;
    public List<GameObject> brickPrefabs;
    
    [Header("Level Number Display")]
    public GameObject levelArea;
    public Sprite[] numberSprites;
    public float numberSpacing = 50f;
    public float doubleDigitOffset = 50f;
    
    [Header("Brick Count UI")]
    public GameObject brickCountUIPrefab;
    public float brickCountUISpacing = 80f;
    
    [Header("Level Configuration")]
    public List<LevelData> levels = new List<LevelData>();
    public int currentLevelIndex = 0;
    
    [Header("UI Animation Settings")]
    public float uiScaleDuration = 0.3f;
    public float uiScaleAmount = 1.2f;
    public float timerPulseDuration = 1f;
    public float timerPulseScale = 1.05f;
    
    [Header("UI References")]
    public UIReferences ui = new UIReferences();
    
    [Header("Score Settings")]
    public ScoreSettings scoreSettings = new ScoreSettings();
    
    [System.Serializable]
    public class GameSettings
    {
        [Header("Fall Physics")]
        public float initialFallSpeed = 3f;
        public float maxFallSpeed = 5f;
        public float accelerationRate = 0.5f;
        public float decelerationDistance = 3f;
        public float snapSpeed = 0.4f;
        public float settleOvershoot = 0.1f;
        public float settleDuration = 0.4f;
        public float autoFallDelay = 0.3f;
        public float movementLockDistance = 0.3f;
        
        [Header("Wobble Effects")]
        public float wobbleAmount = 3f;
        public float wobbleFrequency = 2f;
        public float wobbleDecay = 2f;
        
        [Header("Silhouette Settings")]
        public Color silhouetteColor = new Color(1f, 1f, 1f, 0.5f);
        public Material silhouetteMaterial;
        public float silhouetteFadeDistance = 5f;
        
        [Header("Pause Settings")]
        public Sprite pauseSprite;
        public Sprite continueSprite;
        public int maxPauseChances = 3;
    }
    
    [Header("Game Settings")]
    public GameSettings settings = new GameSettings();
    
    [Header("Effect Timing")]
    public float layerClearDelay = 0.3f;
    public float brickFallDuration = 0.6f;
    public float particleGroundTime = 0.5f;
    
    public enum BrickColor { Orange, Blue, Pink, Purple, Green, White, Gray, Brown, Black }
    
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
        new ColorSettings{ colorType = BrickColor.Pink, tiling = new Vector2(1.48f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Purple, tiling = new Vector2(1.68f, 0f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Green, tiling = new Vector2(1.9f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.White, tiling = new Vector2(1f, 0.5f), offset = Vector2.zero },
        new ColorSettings{ colorType = BrickColor.Gray, tiling = new Vector2(2f, 0.5f), offset = Vector2.zero },
        new ColorSettings{ colorType = BrickColor.Brown, tiling = new Vector2(0f, 0.5f), offset = Vector2.zero },
        new ColorSettings{ colorType = BrickColor.Black, tiling = new Vector2(0f, -0.5f), offset = Vector2.zero }
    };
    
    [HideInInspector] public List<GameObject> landedBricks = new List<GameObject>();
    
    private LevelData currentLevel;
    private GameObject currentBrick, silhouetteBrick;
    private Vector2Int currentGridPosition, brickSize;
    private bool isFalling, hasLanded, isDecelerating, isSnapping, isSettling, isPaused, isGameActive;
    private float currentFallSpeed, settleTimer, currentWobble, wobbleTimer, currentTime;
    private Vector3 originalRotation, targetSettlePosition;
    private BrickColor currentBrickColor;
    private int currentPauseChances, currentGold, comboCount;
    private int maxAllowedLayer = -1;
    private Dictionary<BrickColor, int> targetBrickCounts = new Dictionary<BrickColor, int>();
    private Dictionary<BrickColor, int> destroyedBrickCounts = new Dictionary<BrickColor, int>();
    private Dictionary<BrickColor, GameObject> brickCountUIElements = new Dictionary<BrickColor, GameObject>();
    private List<GameObject> levelNumberObjects = new List<GameObject>();
    private const float START_Y = 20f;

    void Start()
    {
        InitializeLevel();
    }
    
    void InitializeLevel()
    {
        if (levels.Count == 0 || currentLevelIndex >= levels.Count)
        {
            Debug.LogError("Level bulunamadı!");
            return;
        }
        
        currentLevel = levels[currentLevelIndex];
        isGameActive = true;
        currentGold = 0;
        comboCount = 0;
        currentTime = currentLevel.levelTime;
        currentPauseChances = settings.maxPauseChances;
        currentFallSpeed = settings.initialFallSpeed;
        
        targetBrickCounts.Clear();
        destroyedBrickCounts.Clear();
        foreach (var brickCount in currentLevel.brickColorCounts)
        {
            targetBrickCounts[brickCount.color] = brickCount.count;
            destroyedBrickCounts[brickCount.color] = 0;
        }
        
        if (ui.winPanel != null) ui.winPanel.SetActive(false);
        if (ui.losePanel != null) ui.losePanel.SetActive(false);
        
        gridManager.gridSize = currentLevel.gridSize;
        gridManager.InitializeGrid();
        
        CreateLevelNumberDisplay();
        CreateBrickCountUI();
        UpdatePauseUI();
        UpdateGoldUI();
        UpdateTimerUI();
        
        StartCoroutine(GameTimer());
        StartCoroutine(TimerPulseEffect());
        SpawnNewBrick();
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
    
    void UpdateBrickCountUI(BrickColor color)
    {
        if (!brickCountUIElements.ContainsKey(color)) return;
        
        GameObject uiElement = brickCountUIElements[color];
        Text countText = uiElement.GetComponentInChildren<Text>();
        Transform checkmark = uiElement.transform.Find("Checkmark");
        
        int remaining = targetBrickCounts[color] - destroyedBrickCounts[color];
        
        if (remaining <= 0)
        {
            if (countText != null) countText.gameObject.SetActive(false);
            if (checkmark != null)
            {
                checkmark.gameObject.SetActive(true);
                StartCoroutine(ScaleUIElement(checkmark, 1.5f));
            }
        }
        else
        {
            if (countText != null)
            {
                countText.text = remaining.ToString();
                StartCoroutine(ScaleUIElement(countText.transform, uiScaleAmount));
            }
        }
    }
    
    IEnumerator ScaleUIElement(Transform element, float targetScale)
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
    
    IEnumerator TimerPulseEffect()
    {
        if (ui.timerText == null) yield break;
        
        Transform timerTransform = ui.timerText.transform;
        Vector3 originalScale = timerTransform.localScale;
        
        while (isGameActive)
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
    
    public void OnBrickDestroyed(BrickColor color, Vector3 worldPosition)
    {
        if (!targetBrickCounts.ContainsKey(color)) return;
        
        destroyedBrickCounts[color]++;
        
        if (brickCountUIElements.ContainsKey(color))
        {
            StartCoroutine(SendParticleToUI(color, worldPosition));
        }
        
        CheckAllTargetsCompleted();
    }
    
    IEnumerator SendParticleToUI(BrickColor color, Vector3 worldPosition)
    {
        GameObject uiElement = brickCountUIElements[color];
        if (uiElement == null) yield break;
        
        GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        particle.name = "UIParticle";
        particle.transform.localScale = Vector3.one * 0.3f;
        particle.transform.position = worldPosition;
        
        ApplyBrickTexture(particle, color);
        
        Rigidbody rb = particle.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.drag = 0.5f;
        
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
        rb.AddForce(randomDir * 3f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        
        yield return new WaitForSeconds(particleGroundTime);
        
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        Camera cam = Camera.main;
        if (cam == null) yield break;
        
        float duration = 0.8f;
        float elapsed = 0f;
        Vector3 startPos = particle.transform.position;
        
        while (elapsed < duration && particle != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            
            Vector3 screenTargetPos = cam.WorldToScreenPoint(uiElement.transform.position);
            Vector3 worldTargetPos = cam.ScreenToWorldPoint(new Vector3(screenTargetPos.x, screenTargetPos.y, cam.WorldToScreenPoint(startPos).z));
            
            particle.transform.position = Vector3.Lerp(startPos, worldTargetPos, t);
            particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
            
            yield return null;
        }
        
        if (particle != null)
        {
            Destroy(particle);
            UpdateBrickCountUI(color);
        }
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
            WinLevel();
        }
    }
    
    void CreateSilhouette()
    {
        if (silhouetteBrick != null) Destroy(silhouetteBrick);
        if (currentBrick == null) return;

        silhouetteBrick = Instantiate(currentBrick);
        silhouetteBrick.name = "SilhouetteBrick";
        
        foreach (var renderer in silhouetteBrick.GetComponentsInChildren<Renderer>())
        {
            if (settings.silhouetteMaterial != null)
            {
                renderer.material = new Material(settings.silhouetteMaterial);
                renderer.material.color = settings.silhouetteColor;
            }
            else
            {
                var mat = new Material(Shader.Find("Standard"));
                mat.color = settings.silhouetteColor;
                mat.SetFloat("_Mode", 2);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                renderer.material = mat;
            }
        }

        foreach (var collider in silhouetteBrick.GetComponentsInChildren<Collider>())
            collider.enabled = false;
    }

    void UpdateSilhouette()
    {
        if (silhouetteBrick == null || currentBrick == null || hasLanded || isPaused)
        {
            if (silhouetteBrick != null) silhouetteBrick.SetActive(false);
            return;
        }

        float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        float currentY = currentBrick.transform.position.y;
        float distanceToTarget = Mathf.Abs(currentY - targetY);
        float alpha = Mathf.Clamp01(distanceToTarget / settings.silhouetteFadeDistance);
        
        if (alpha <= 0.01f)
        {
            silhouetteBrick.SetActive(false);
        }
        else
        {
            silhouetteBrick.SetActive(true);
            
            foreach (var renderer in silhouetteBrick.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.material.color;
                color.a = alpha * 0.5f;
                renderer.material.color = color;
            }
            
            var targetPosition = gridManager.GetGridPosition(currentGridPosition, brickSize);
            targetPosition.y = targetY;
            silhouetteBrick.transform.position = targetPosition;
            silhouetteBrick.transform.rotation = currentBrick.transform.rotation;
        }
    }

    void ClearSilhouette()
    {
        if (silhouetteBrick != null)
        {
            Destroy(silhouetteBrick);
            silhouetteBrick = null;
        }
    }
    
    IEnumerator GameTimer()
    {
        while (isGameActive && currentTime > 0)
        {
            if (!isPaused)
            {
                currentTime -= Time.deltaTime;
                UpdateTimerUI();
                
                if (currentTime <= 0)
                {
                    currentTime = 0;
                    LoseLevel("Süre Doldu!");
                    yield break;
                }
            }
            yield return null;
        }
    }
    
    void UpdateTimerUI()
    {
        if (ui.timerText != null)
        {
            int min = Mathf.FloorToInt(currentTime / 60f);
            int sec = Mathf.FloorToInt(currentTime % 60f);
            ui.timerText.text = $"{min}:{sec:00}";
        }
    }
    
    void WinLevel()
    {
        isGameActive = false;
        ClearSilhouette();
        if (ui.winPanel != null) ui.winPanel.SetActive(true);
    }
    
    void LoseLevel(string reason)
    {
        isGameActive = false;
        ClearSilhouette();
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
            ui.pauseButton.image.sprite = isPaused ? settings.continueSprite : settings.pauseSprite;
        
        if (ui.pauseText != null)
            ui.pauseText.text = currentPauseChances.ToString();
    }
    
    void SpawnNewBrick()
    {
        if (!isGameActive || isPaused) return;
        
        int currentHighestLayer = gridManager.GetHighestLayer();
        if (currentHighestLayer >= currentLevel.maxLayers)
        {
            LoseLevel("Maksimum Katman Aşıldı!");
            return;
        }
        
        ResetBrickState();
        
        var availableBricks = GetBricksFromNames(currentLevel.allowedBrickNames);
        var suitableBricks = GetSuitableBricks(availableBricks);
        
        if (suitableBricks.Count == 0)
        {
            availableBricks = new List<GameObject>(brickPrefabs);
            suitableBricks = GetSuitableBricks(availableBricks);
            if (suitableBricks.Count == 0) return;
        }
        
        var randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        currentBrick = Instantiate(randomBrickPrefab);
        currentBrick.name = "CurrentBrick";
        originalRotation = currentBrick.transform.eulerAngles;
        
        var availableColors = targetBrickCounts.Keys.ToList();
        currentBrickColor = availableColors[Random.Range(0, availableColors.Count)];
        
        ApplyBrickTexture(currentBrick, currentBrickColor);
        CalculateBrickSize();
        InitializeBrickPosition();
        CreateSilhouette();
        
        maxAllowedLayer = gridManager.GetMaxLayerAtPosition(currentGridPosition, brickSize);
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
            var size = GetBrickSize(brick);
            if (size.x <= currentLevel.gridSize.x && size.y <= currentLevel.gridSize.y)
                suitable.Add(brick);
        }
        return suitable;
    }
    
    void ResetBrickState()
    {
        isFalling = hasLanded = isDecelerating = isSnapping = isSettling = false;
        currentFallSpeed = settings.initialFallSpeed;
        settleTimer = currentWobble = wobbleTimer = 0f;
        maxAllowedLayer = -1;
        ClearSilhouette();
    }
    
    void ApplyBrickTexture(GameObject brick, BrickColor color)
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
    
    ColorSettings GetColorSettings(BrickColor color)
    {
        foreach (var cs in availableColors)
            if (cs.colorType == color) return cs;
        return availableColors[0];
    }
    
    public void OnMoveUpButton() => TryMove(0, 1);
    public void OnMoveDownButton() => TryMove(0, -1);
    public void OnMoveLeftButton() => TryMove(-1, 0);
    public void OnMoveRightButton() => TryMove(1, 0);
    public void OnRotateButton() { if (CanMoveBrick()) RotateBrick(); }

    void TryMove(int deltaX, int deltaY)
    {
        if (!CanMoveBrick()) return;
        var newPos = new Vector2Int(currentGridPosition.x + deltaX, currentGridPosition.y + deltaY);
        
        int newMaxLayer = gridManager.GetMaxLayerAtPosition(newPos, brickSize);
        if (newMaxLayer > maxAllowedLayer) return;
        
        if (gridManager.IsValidPosition(newPos, brickSize))
        {
            currentGridPosition = newPos;
            var gridPos = gridManager.GetGridPosition(currentGridPosition, brickSize);
            currentBrick.transform.position = new Vector3(gridPos.x, currentBrick.transform.position.y, gridPos.z);
            UpdateSilhouette();
        }
    }

    bool CanMoveBrick()
    {
        if (currentBrick == null || isPaused || hasLanded || !isGameActive) return false;
        
        if (isFalling)
        {
            float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
            float distance = Mathf.Abs(currentBrick.transform.position.y - targetY);
            if (distance <= settings.movementLockDistance) return false;
        }
        
        return true;
    }
    
    void RotateBrick()
    {
        currentBrick.transform.Rotate(0, 90, 0);
        originalRotation = currentBrick.transform.eulerAngles;
        
        var newSize = new Vector2Int(brickSize.y, brickSize.x);
        AdjustPositionAfterRotation(newSize);
        brickSize = newSize;
        
        maxAllowedLayer = gridManager.GetMaxLayerAtPosition(currentGridPosition, brickSize);
        UpdateSilhouette();
    }
    
    void AdjustPositionAfterRotation(Vector2Int newSize)
    {
        int maxX = Mathf.Max(0, (int)currentLevel.gridSize.x - newSize.x);
        int maxY = Mathf.Max(0, (int)currentLevel.gridSize.y - newSize.y);
        
        currentGridPosition.x = Mathf.Clamp(currentGridPosition.x, 0, maxX);
        currentGridPosition.y = Mathf.Clamp(currentGridPosition.y, 0, maxY);
        
        var gridPos = gridManager.GetGridPosition(currentGridPosition, newSize);
        currentBrick.transform.position = new Vector3(gridPos.x, currentBrick.transform.position.y, gridPos.z);
    }
    
    Vector2Int GetBrickSize(GameObject brickPrefab)
    {
        var parts = brickPrefab.name.Split('x');
        if (parts.Length == 2)
        {
            return new Vector2Int(
                int.Parse(parts[0].Substring(parts[0].Length - 1)),
                int.Parse(parts[1])
            );
        }
        
        var scale = brickPrefab.transform.localScale;
        return new Vector2Int(Mathf.RoundToInt(scale.x), Mathf.RoundToInt(scale.z));
    }
    
    void CalculateBrickSize() => brickSize = GetBrickSize(currentBrick);
    
    void InitializeBrickPosition()
    {
        int maxX = Mathf.Max(0, (int)currentLevel.gridSize.x - brickSize.x);
        int maxY = Mathf.Max(0, (int)currentLevel.gridSize.y - brickSize.y);
        
        currentGridPosition = new Vector2Int(Random.Range(0, maxX + 1), Random.Range(0, maxY + 1));
        
        var gridPos = gridManager.GetGridPosition(currentGridPosition, brickSize);
        currentBrick.transform.position = new Vector3(gridPos.x, START_Y, gridPos.z);
    }
    
    void Update()
    {
        if (currentBrick == null || !isGameActive || isPaused) return;
        
        if (!hasLanded)
        {
            HandleBrickMovement();
            HandleRotationInput();
            
            if (!isFalling) StartFalling();
            else HandleFalling();
            
            UpdateSilhouette();
        }
    }
    
    void HandleBrickMovement()
    {
        if (!CanMoveBrick()) return;
        
        var newPos = currentGridPosition;
        
        if (Input.GetKeyDown(KeyCode.UpArrow)) newPos.y++;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) newPos.y--;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) newPos.x++;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) newPos.x--;
        
        if (newPos != currentGridPosition)
        {
            int newMaxLayer = gridManager.GetMaxLayerAtPosition(newPos, brickSize);
            if (newMaxLayer <= maxAllowedLayer && gridManager.IsValidPosition(newPos, brickSize))
            {
                currentGridPosition = newPos;
                var gridPos = gridManager.GetGridPosition(currentGridPosition, brickSize);
                currentBrick.transform.position = new Vector3(gridPos.x, currentBrick.transform.position.y, gridPos.z);
                UpdateSilhouette();
            }
        }
    }
    
    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && CanMoveBrick()) RotateBrick();
    }
    
    void StartFalling()
    {
        isFalling = true;
        currentWobble = settings.wobbleAmount;
    }
    
    void HandleFalling()
    {
        if (isPaused) return;
        
        float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        var currentPos = currentBrick.transform.position;
        float distance = Mathf.Abs(currentPos.y - targetY);
        
        ApplyWobbleEffect(distance);
        HandleFallPhases(targetY, currentPos, distance);
    }
    
    void ApplyWobbleEffect(float distance)
    {
        if (isFalling && !isSettling && !isPaused)
        {
            wobbleTimer += Time.deltaTime * settings.wobbleFrequency;
            float progress = distance / settings.decelerationDistance;
            float decay = Mathf.Clamp01(progress);
            float amount = currentWobble * decay;
            
            currentBrick.transform.rotation = Quaternion.Euler(
                originalRotation.x + Mathf.Sin(wobbleTimer) * amount * 0.7f,
                originalRotation.y,
                originalRotation.z + Mathf.Cos(wobbleTimer * 0.8f) * amount * 0.5f
            );
            
            if (isDecelerating)
                currentWobble = Mathf.Lerp(currentWobble, 0f, settings.wobbleDecay * Time.deltaTime);
        }
    }
    
    void HandleFallPhases(float targetY, Vector3 currentPos, float distance)
    {
        if (!isDecelerating && distance <= settings.decelerationDistance)
        {
            isDecelerating = true;
            var gridPos = gridManager.GetGridPosition(currentGridPosition, brickSize);
            targetSettlePosition = new Vector3(gridPos.x, targetY, gridPos.z);
        }
        
        if (!isDecelerating)
        {
            currentFallSpeed = Mathf.Min(currentFallSpeed + settings.accelerationRate * Time.deltaTime, settings.maxFallSpeed);
        }
        else if (!isSnapping && !isSettling)
        {
            float progress = 1f - (distance / settings.decelerationDistance);
            currentFallSpeed = Mathf.Lerp(settings.maxFallSpeed, settings.snapSpeed, progress * progress);
            
            if (distance < 0.2f)
            {
                isSnapping = true;
            }
        }
        
        if (isSnapping && !isSettling) SnapToPosition();
        else if (isSettling) SettleBrick();
        else FallNormally(targetY);
    }
    
    void SnapToPosition()
    {
        float overshootY = targetSettlePosition.y + settings.settleOvershoot;
        currentBrick.transform.position = Vector3.MoveTowards(
            currentBrick.transform.position,
            new Vector3(targetSettlePosition.x, overshootY, targetSettlePosition.z),
            settings.snapSpeed * Time.deltaTime
        );
        
        if (Vector3.Distance(currentBrick.transform.position, new Vector3(targetSettlePosition.x, overshootY, targetSettlePosition.z)) < 0.01f)
        {
            isSettling = true;
            settleTimer = 0f;
        }
    }
    
    void SettleBrick()
    {
        settleTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(settleTimer / settings.settleDuration);
        float smooth = 1f - Mathf.Pow(1f - progress, 3f);
        
        currentBrick.transform.rotation = Quaternion.Euler(originalRotation);
        currentBrick.transform.position = Vector3.Lerp(
            new Vector3(targetSettlePosition.x, targetSettlePosition.y + settings.settleOvershoot, targetSettlePosition.z),
            targetSettlePosition,
            smooth
        );
        
        if (progress >= 1f)
        {
            currentBrick.transform.position = targetSettlePosition;
            OnBrickLanded();
        }
    }
    
    void FallNormally(float targetY)
    {
        float newY = Mathf.MoveTowards(currentBrick.transform.position.y, targetY, currentFallSpeed * Time.deltaTime);
        currentBrick.transform.position = new Vector3(currentBrick.transform.position.x, newY, currentBrick.transform.position.z);
        
        if (Mathf.Approximately(newY, targetY)) OnBrickLanded();
    }
    
    void OnBrickLanded()
    {
        currentBrick.transform.rotation = Quaternion.Euler(originalRotation);
        currentBrick.transform.position = targetSettlePosition;
        
        landedBricks.Add(currentBrick);
        currentBrick.name = $"LandedBrick_{landedBricks.Count}";
        
        gridManager.PlaceBrick(currentGridPosition, brickSize, currentBrick, currentBrickColor);
        
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBrickSnap();
        
        AddGold(scoreSettings.goldPerBrick);
        CheckForMatchingLines();
        
        isFalling = false;
        hasLanded = true;
        isDecelerating = false;
        isSnapping = false;
        isSettling = false;
        
        ClearSilhouette();
        
        if (!isPaused) Invoke("SpawnNewBrick", settings.autoFallDelay);
    }
    
    void CheckForMatchingLines()
    {
        int highest = gridManager.GetHighestLayer();
        
        for (int layer = 0; layer <= highest; layer++)
        {
            List<Vector2Int> matchedPositions = gridManager.FindMatchingLineInLayer(layer);
            
            if (matchedPositions.Count > 0)
            {
                HashSet<GameObject> bricksToDestroy = new HashSet<GameObject>();
                
                foreach (var pos in matchedPositions)
                {
                    var brick = gridManager.GetBrickAt(pos.x, pos.y, layer);
                    if (brick != null)
                    {
                        bricksToDestroy.Add(brick);
                    }
                }
                
                BrickColor matchColor = gridManager.GetBrickColorAt(matchedPositions[0].x, matchedPositions[0].y, layer);
                FloodFillConnectedBricks(bricksToDestroy, matchColor, layer);
                
                StartCoroutine(DestroyBricksWithEffects(new List<GameObject>(bricksToDestroy), layer));
                
                AddGold(scoreSettings.goldPerLayer);
                comboCount++;
                break;
            }
        }
    }
    
    void FloodFillConnectedBricks(HashSet<GameObject> bricksToDestroy, BrickColor targetColor, int startLayer)
    {
        Queue<(GameObject brick, int layer)> queue = new Queue<(GameObject, int)>();
        HashSet<GameObject> visited = new HashSet<GameObject>(bricksToDestroy);
        
        foreach (var brick in bricksToDestroy)
        {
            queue.Enqueue((brick, startLayer));
        }
        
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        
        while (queue.Count > 0)
        {
            var (currentBrick, currentLayer) = queue.Dequeue();
            var currentPos = gridManager.GetBrickGridPosition(currentBrick);
            
            for (int i = 0; i < 4; i++)
            {
                int newX = currentPos.x + dx[i];
                int newY = currentPos.y + dy[i];
                
                if (newX >= 0 && newX < gridManager.GridWidth && newY >= 0 && newY < gridManager.GridHeight)
                {
                    var neighbor = gridManager.GetBrickAt(newX, newY, currentLayer);
                    if (neighbor != null && !visited.Contains(neighbor))
                    {
                        if (gridManager.GetBrickColorAt(newX, newY, currentLayer) == targetColor)
                        {
                            visited.Add(neighbor);
                            bricksToDestroy.Add(neighbor);
                            queue.Enqueue((neighbor, currentLayer));
                        }
                    }
                }
            }
            
            for (int layerOffset = -1; layerOffset <= 1; layerOffset += 2)
            {
                int checkLayer = currentLayer + layerOffset;
                if (checkLayer >= 0 && checkLayer <= gridManager.GetHighestLayer())
                {
                    var neighbor = gridManager.GetBrickAt(currentPos.x, currentPos.y, checkLayer);
                    if (neighbor != null && !visited.Contains(neighbor))
                    {
                        if (gridManager.GetBrickColorAt(currentPos.x, currentPos.y, checkLayer) == targetColor)
                        {
                            visited.Add(neighbor);
                            bricksToDestroy.Add(neighbor);
                            queue.Enqueue((neighbor, checkLayer));
                        }
                    }
                }
            }
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
                
                BrickColor brickColor = gridManager.GetBrickColor(brick);
                Vector3 brickPosition = brick.transform.position;
                
                effectManager.CreateParticlesForBrick(brick, brickColor, brickPosition, this);
                
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
    
    public Vector2Int GetBrickGridPosition(GameObject brick)
    {
        var worldPos = brick.transform.position;
        int gridX = Mathf.Clamp(Mathf.RoundToInt(worldPos.x / gridManager.cellSize), 0, (int)currentLevel.gridSize.x - 1);
        int gridY = Mathf.Clamp(Mathf.RoundToInt(worldPos.z / gridManager.cellSize), 0, (int)currentLevel.gridSize.y - 1);
        return new Vector2Int(gridX, gridY);
    }
    
    void OnDestroy()
    {
        ClearSilhouette();
        
        foreach (var obj in levelNumberObjects)
            if (obj != null) Destroy(obj);
        
        foreach (var ui in brickCountUIElements.Values)
            if (ui != null) Destroy(ui);
    }
    
    public List<BrickColor> GetCurrentLevelColors()
    {
        if (currentLevel == null) return new List<BrickColor>();
        return currentLevel.brickColorCounts.Select(bc => bc.color).ToList();
    }
}
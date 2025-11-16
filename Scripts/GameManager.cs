using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Level Configuration")]
    public List<BrickColor> levelColors = new List<BrickColor>();
    public List<string> levelBrickNames = new List<string>();
    public int maxColorsPerLevel = 3;
    public float levelTime = 180f;
    
    [System.Serializable]
    public class GameSettings
    {
        [Header("Core References")]
        public GridManager gridManager;
        public EffectManager effectManager;
        public List<GameObject> brickPrefabs;
        
        [Header("Fall Physics")]
        public float initialFallSpeed = 3f;    // 3 yap
        public float maxFallSpeed = 5f;        // 5 yap
        public float accelerationRate = 0.5f;
        public float decelerationDistance = 3f;
        public float snapSpeed = 0.2f;
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
        
        [Header("UI References")]
        public Button pauseButton;
        public Sprite pauseSprite;
        public Sprite continueSprite;
        public Text pauseText;
        public Text timerText;
        public int maxPauseChances = 3;
    }
    
    public GameSettings settings = new GameSettings();
    
    public enum BrickColor { Orange, Blue, Pink, Purple, Green, White, Gray, Brown, Black }
    
    [System.Serializable]
    public class ColorSettings
    {
        public BrickColor colorType;
        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
    }
    
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
    
    private GameObject currentBrick;
    private GameObject silhouetteBrick; // YENİ: Silüet brick
    public Vector2Int currentGridPosition, brickSize;
    private bool isFalling, hasLanded, isDecelerating, isSnapping, isSettling, isPaused, isGameActive = true;
    private float currentFallSpeed, settleTimer, targetSettleY, overshootY, currentWobble, wobbleTimer, currentTime;
    private Vector3 originalRotation;
    private BrickColor currentBrickColor;
    private int currentPauseChances;
    private const float START_Y = 20f;

    void Start()
    {
        currentFallSpeed = settings.initialFallSpeed;
        currentPauseChances = settings.maxPauseChances;
        currentTime = levelTime;
        UpdatePauseUI();
        UpdateTimerUI();
        StartCoroutine(GameTimer());
        SpawnNewBrick();
    }
    
    // YENİ: Silüet oluştur
    void CreateSilhouette()
    {
        if (silhouetteBrick != null)
            Destroy(silhouetteBrick);

        if (currentBrick == null) return;

        // Mevcut brick'in kopyasını oluştur
        silhouetteBrick = Instantiate(currentBrick);
        silhouetteBrick.name = "SilhouetteBrick";
        
        // Tüm renderer'ları al ve silüet materyalini uygula
        foreach (var renderer in silhouetteBrick.GetComponentsInChildren<Renderer>())
        {
            if (settings.silhouetteMaterial != null)
            {
                renderer.material = new Material(settings.silhouetteMaterial);
                renderer.material.color = settings.silhouetteColor;
            }
            else
            {
                // Fallback: Basit şeffaf beyaz material
                var mat = new Material(Shader.Find("Standard"));
                mat.color = settings.silhouetteColor;
                mat.SetFloat("_Mode", 2); // Transparent mode
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

        // Collider'ları devre dışı bırak
        foreach (var collider in silhouetteBrick.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
    }

    void UpdateSilhouette()
    {
        if (silhouetteBrick == null || currentBrick == null || hasLanded || isPaused)
        {
            if (silhouetteBrick != null)
                silhouetteBrick.SetActive(false);
            return;
        }

        float targetY = settings.gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        float currentY = currentBrick.transform.position.y;
        float distanceToTarget = Mathf.Abs(currentY - targetY);
        
        // 1.3f'ten 0'a doğru alpha değerini azalt
        float alpha = Mathf.Clamp01(distanceToTarget / 3.9f);
        
        if (alpha <= 0.01f)
        {
            silhouetteBrick.SetActive(false);
        }
        else
        {
            silhouetteBrick.SetActive(true);
            
            // Alpha değerini güncelle
            foreach (var renderer in silhouetteBrick.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.material.color;
                color.a = alpha * 0.5f; // Orijinal alpha (0.5) ile çarp
                renderer.material.color = color;
            }
            
            Vector3 targetPosition = settings.gridManager.GetGridPosition(currentGridPosition, brickSize);
            targetPosition.y = targetY;
            silhouetteBrick.transform.position = targetPosition;
            silhouetteBrick.transform.rotation = currentBrick.transform.rotation;
        }
    }

    // YENİ: Silüeti temizle
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
                    GameOver();
                    yield break;
                }
            }
            yield return null;
        }
    }
    
    void UpdateTimerUI()
    {
        if (settings.timerText != null)
        {
            int min = Mathf.FloorToInt(currentTime / 60f);
            int sec = Mathf.FloorToInt(currentTime % 60f);
            settings.timerText.text = $"{min}:{sec:00}";
        }
    }
    
    void GameOver()
    {
        isGameActive = false;
        ClearSilhouette();
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
        if (settings.pauseButton != null)
            settings.pauseButton.image.sprite = isPaused ? settings.continueSprite : settings.pauseSprite;
        
        if (settings.pauseText != null)
            settings.pauseText.text = currentPauseChances.ToString();
    }
    
    void SpawnNewBrick()
    {
        if (!isGameActive || isPaused) return;
        
        ResetBrickState();
        
        var availableBricks = GetBricksFromNames(levelBrickNames);
        var suitableBricks = GetSuitableBricks(availableBricks);
        
        if (suitableBricks.Count == 0)
        {
            availableBricks = GetBricksFromNames(GetAllBrickNames());
            suitableBricks = GetSuitableBricks(availableBricks);
            if (suitableBricks.Count == 0) return;
        }
        
        var randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        currentBrick = Instantiate(randomBrickPrefab);
        currentBrick.name = "CurrentBrick";
        originalRotation = currentBrick.transform.eulerAngles;
        
        currentBrickColor = levelColors.Count > 0 
            ? levelColors[Random.Range(0, levelColors.Count)]
            : (BrickColor)Random.Range(0, availableColors.Count);
        
        ApplyBrickTexture(currentBrick, currentBrickColor);
        CalculateBrickSize();
        InitializeBrickPosition();
        
        // YENİ: Silüet oluştur
        CreateSilhouette();
    }
    
    List<GameObject> GetBricksFromNames(List<string> brickNames)
    {
        if (brickNames.Count == 0) return new List<GameObject>(settings.brickPrefabs);
        
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
        foreach (var brick in settings.brickPrefabs)
            if (brick.name == brickName) return brick;
        return null;
    }
    
    public List<string> GetAllBrickNames()
    {
        var names = new List<string>();
        foreach (var brick in settings.brickPrefabs) names.Add(brick.name);
        return names;
    }
    
    List<GameObject> GetSuitableBricks(List<GameObject> brickList)
    {
        var suitable = new List<GameObject>();
        foreach (var brick in brickList)
        {
            var size = GetBrickSize(brick);
            if (size.x <= settings.gridManager.gridSize.x && size.y <= settings.gridManager.gridSize.y)
                suitable.Add(brick);
        }
        return suitable;
    }
    
    void ResetBrickState()
    {
        isFalling = hasLanded = isDecelerating = isSnapping = isSettling = false;
        currentFallSpeed = settings.initialFallSpeed;
        settleTimer = currentWobble = wobbleTimer = 0f;
        
        // YENİ: Silüeti temizle
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
        if (settings.gridManager.IsValidPosition(newPos, brickSize))
            MoveBrickToGrid(newPos.x, newPos.y);
    }

    bool CanMoveBrick()
    {
        if (currentBrick == null || isPaused || hasLanded || !isGameActive) return false;
        
        if (isFalling)
        {
            float targetY = settings.gridManager.GetRequiredHeight(currentGridPosition, brickSize);
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
        
        // YENİ: Silüeti güncelle
        UpdateSilhouette();
    }
    
    void AdjustPositionAfterRotation(Vector2Int newSize)
    {
        int maxX = Mathf.Max(0, (int)settings.gridManager.gridSize.x - newSize.x);
        int maxY = Mathf.Max(0, (int)settings.gridManager.gridSize.y - newSize.y);
        
        currentGridPosition.x = Mathf.Clamp(currentGridPosition.x, 0, maxX);
        currentGridPosition.y = Mathf.Clamp(currentGridPosition.y, 0, maxY);
        
        var gridPos = settings.gridManager.GetGridPosition(currentGridPosition, newSize);
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
        int maxX = Mathf.Max(0, (int)settings.gridManager.gridSize.x - brickSize.x);
        int maxY = Mathf.Max(0, (int)settings.gridManager.gridSize.y - brickSize.y);
        
        currentGridPosition = new Vector2Int(Random.Range(0, maxX + 1), Random.Range(0, maxY + 1));
        
        var gridPos = settings.gridManager.GetGridPosition(currentGridPosition, brickSize);
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
            
            // YENİ: Silüeti güncelle (FallLine yerine)
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
        
        if (newPos != currentGridPosition && settings.gridManager.IsValidPosition(newPos, brickSize))
        {
            MoveBrickToGrid(newPos.x, newPos.y);
            // YENİ: Silüeti güncelle
            UpdateSilhouette();
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
        
        float targetY = settings.gridManager.GetRequiredHeight(currentGridPosition, brickSize);
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
            targetSettleY = targetY;
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
                overshootY = targetY + settings.settleOvershoot;
            }
        }
        
        if (isSnapping && !isSettling) SnapToPosition(currentPos, overshootY);
        else if (isSettling) SettleBrick(currentPos);
        else FallNormally(currentPos, targetY);
    }
    
    void SnapToPosition(Vector3 currentPos, float targetY)
    {
        float newY = Mathf.MoveTowards(currentPos.y, targetY, settings.snapSpeed * Time.deltaTime);
        currentBrick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
        
        if (Mathf.Abs(newY - targetY) < 0.01f)
        {
            isSettling = true;
            settleTimer = 0f;
        }
    }
    
    void SettleBrick(Vector3 currentPos)
    {
        settleTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(settleTimer / settings.settleDuration);
        float smooth = 1f - Mathf.Pow(1f - progress, 3f);
        
        currentBrick.transform.rotation = Quaternion.Euler(originalRotation);
        currentBrick.transform.position = new Vector3(currentPos.x, Mathf.Lerp(overshootY, targetSettleY, smooth), currentPos.z);
        
        if (progress >= 1f)
        {
            currentBrick.transform.position = new Vector3(currentPos.x, targetSettleY, currentPos.z);
            OnBrickLanded();
        }
    }
    
    void FallNormally(Vector3 currentPos, float targetY)
    {
        float newY = Mathf.MoveTowards(currentPos.y, targetY, currentFallSpeed * Time.deltaTime);
        currentBrick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
        
        if (Mathf.Approximately(newY, targetY)) OnBrickLanded();
    }
    
    void OnBrickLanded()
    {
        currentBrick.transform.rotation = Quaternion.Euler(originalRotation);
        landedBricks.Add(currentBrick);
        currentBrick.name = $"LandedBrick_{landedBricks.Count}";
        
        settings.gridManager.PlaceBrick(currentGridPosition, brickSize, currentBrick, currentBrickColor);
        CheckForCompletedLayers();
        
        isFalling = false;
        hasLanded = true;
        isDecelerating = false;
        isSnapping = false;
        isSettling = false;
        
        // YENİ: Silüeti temizle
        ClearSilhouette();
        
        if (!isPaused) Invoke("SpawnNewBrick", settings.autoFallDelay);
    }
    
    void CheckForCompletedLayers()
    {
        int highest = settings.gridManager.GetHighestLayer();
        
        for (int layer = highest; layer >= 0; layer--)
        {
            var layerColor = settings.gridManager.CheckCompletedLayerWithColor(layer);
            if (layerColor.HasValue)
            {
                settings.effectManager.ClearLayerWithEffects(layer);
                break;
            }
        }
    }
    
    public Vector2Int GetBrickGridPosition(GameObject brick)
    {
        var worldPos = brick.transform.position;
        int gridX = Mathf.Clamp(Mathf.RoundToInt(worldPos.x / settings.gridManager.cellSize), 0, (int)settings.gridManager.gridSize.x - 1);
        int gridY = Mathf.Clamp(Mathf.RoundToInt(worldPos.z / settings.gridManager.cellSize), 0, (int)settings.gridManager.gridSize.y - 1);
        return new Vector2Int(gridX, gridY);
    }
    
    void MoveBrickToGrid(int x, int y)
    {
        currentGridPosition = new Vector2Int(x, y);
        var gridPos = settings.gridManager.GetGridPosition(currentGridPosition, brickSize);
        currentBrick.transform.position = new Vector3(gridPos.x, currentBrick.transform.position.y, gridPos.z);
    }
    
    public void SetPauseChances(int chances)
    {
        settings.maxPauseChances = currentPauseChances = chances;
        UpdatePauseUI();
    }
    
    public void SetLevelTime(float timeInSeconds)
    {
        levelTime = currentTime = timeInSeconds;
        UpdateTimerUI();
    }
    
    public void SetLevelBrickNames(List<string> brickNames)
    {
        levelBrickNames.Clear();
        levelBrickNames.AddRange(brickNames);
    }
    
    public void UseAllBricks()
    {
        levelBrickNames.Clear();
        levelBrickNames.AddRange(GetAllBrickNames());
    }
    
    void OnDestroy()
    {
        ClearSilhouette();
    }
}
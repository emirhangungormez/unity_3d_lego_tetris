using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public EffectManager effectManager;
    public List<GameObject> brickPrefabs;
    
    [Header("Fall Settings")]
    public float initialFallSpeed = 0.1f;
    public float maxFallSpeed = 1f;
    public float accelerationRate = 0.5f;
    public float decelerationDistance = 3f;
    public float snapSpeed = 0.1f;
    public float settleOvershoot = 0.1f;
    public float settleDuration = 0.4f;
    public float autoFallDelay = 0.3f;
    public float movementLockDistance = 0.3f;
    
    [Header("Wobble Effects")]
    public float wobbleAmount = 3f;
    public float wobbleFrequency = 2f;
    public float wobbleDecay = 2f;
    
    [Header("Level Settings")]
    public List<BrickColor> levelColors = new List<BrickColor>();
    public List<string> levelBrickNames = new List<string>();
    public int maxColorsPerLevel = 3;
    
    [Header("Pause Settings")]
    public int maxPauseChances = 3;
    public int currentPauseChances;
    public Button pauseButton;
    public Sprite pauseSprite;
    public Sprite continueSprite;
    public Text pauseText;
    
    [Header("Timer Settings")]
    public Text timerText;
    public float levelTime = 180f;

    [Header("Fall Line Settings")]
    public Color fallLineStartColor = new Color(1f, 1f, 1f, 0.7f); // Beyaz, %70 şeffaf
    public Color fallLineEndColor = new Color(0.3f, 0.3f, 0.3f, 0.7f); // Koyu gri, %70 şeffaf
    public float fallLineStartWidth = 0.3f; // 3x kalın
    public float fallLineEndWidth = 0.15f; // 3x kalın
    
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
        new ColorSettings{ colorType = BrickColor.Blue,   tiling = new Vector2(1.1f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Pink,   tiling = new Vector2(1.48f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Purple, tiling = new Vector2(1.68f, 0f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.Green,  tiling = new Vector2(1.9f, -0.5f), offset = new Vector2(0f, 0.5f) },
        new ColorSettings{ colorType = BrickColor.White,  tiling = new Vector2(1f, 0.5f), offset = new Vector2(0f, 0f) },
        new ColorSettings{ colorType = BrickColor.Gray,   tiling = new Vector2(2f, 0.5f), offset = new Vector2(0f, 0f) },
        new ColorSettings{ colorType = BrickColor.Brown,  tiling = new Vector2(0f, 0.5f), offset = new Vector2(0f, 0f) },
        new ColorSettings{ colorType = BrickColor.Black,  tiling = new Vector2(0f, -0.5f), offset = new Vector2(0f, 0f) }
    };
    
    public List<GameObject> landedBricks = new List<GameObject>();
    
    private GameObject currentBrick;
    private Vector2Int currentGridPosition;
    private Vector2Int brickSize;
    private bool isFalling, hasLanded, isDecelerating, isSnapping, isSettling;
    private bool isPaused = false;
    private bool isGameActive = true;
    private float currentFallSpeed, settleTimer, targetSettleY, overshootY;
    private float currentWobble, wobbleTimer;
    private Vector3 originalRotation;
    private const float startYPosition = 20f;
    private BrickColor currentBrickColor;
    private float currentTime;
    private LineRenderer fallLine;

    void Start()
    {
        currentFallSpeed = initialFallSpeed;
        currentPauseChances = maxPauseChances;
        currentTime = levelTime;
        
        // Düşüş çizgisini oluştur
        CreateFallLine();
        
        UpdatePauseUI();
        UpdateTimerUI();
        StartCoroutine(GameTimer());
        
        DebugLogAvailableBrickNames();
        
        SpawnNewBrick();
    }
    
    // YENİ: Düşüş çizgisi oluştur
    void CreateFallLine()
    {
        GameObject lineObj = new GameObject("FallLine");
        fallLine = lineObj.AddComponent<LineRenderer>();
        
        // Material ayarla - Transparent shader kullan
        fallLine.material = new Material(Shader.Find("Sprites/Default"));
        fallLine.startColor = fallLineStartColor;
        fallLine.endColor = fallLineEndColor;
        fallLine.startWidth = fallLineStartWidth;
        fallLine.endWidth = fallLineEndWidth;
        fallLine.positionCount = 2;
        
        // Daha iyi görünüm için
        fallLine.useWorldSpace = true;
        fallLine.numCapVertices = 5; // Daha yuvarlak uçlar
        
        // Başlangıçta gizle
        fallLine.enabled = false;
        
        Debug.Log("📏 Düşüş çizgisi oluşturuldu - Beyaz, şeffaf, 3x kalın");
    }
    
    // YENİ: Düşüş çizgisini güncelle
    void UpdateFallLine()
    {
        if (currentBrick == null || !isFalling || hasLanded || isPaused)
        {
            if (fallLine != null)
                fallLine.enabled = false;
            return;
        }
        
        // Çizgiyi göster
        fallLine.enabled = true;
        
        // Başlangıç pozisyonu (brick'in merkezi)
        Vector3 startPos = currentBrick.transform.position;
        
        // Bitiş pozisyonu (hedef yükseklik)
        float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        Vector3 endPos = new Vector3(startPos.x, targetY, startPos.z);
        
        // Çizgiyi güncelle
        fallLine.SetPosition(0, startPos);
        fallLine.SetPosition(1, endPos);
        
        // Çizgi rengini mesafeye göre ayarla
        float distance = Mathf.Abs(startPos.y - targetY);
        UpdateLineColorBasedOnDistance(distance);
    }
    
    // YENİ: Mesafeye göre çizgi rengini güncelle - BEYAZ'dan KOYU GRİ'ye
    void UpdateLineColorBasedOnDistance(float distance)
    {
        float colorLerp = Mathf.Clamp01(1f - (distance / decelerationDistance));
        
        // Beyaz (1,1,1) -> Koyu gri (0.3,0.3,0.3) arasında geçiş
        Color targetColor = Color.Lerp(
            new Color(1f, 1f, 1f, 0.7f), // Beyaz, şeffaf
            new Color(0.3f, 0.3f, 0.3f, 0.7f), // Koyu gri, şeffaf
            colorLerp
        );
        
        fallLine.startColor = targetColor;
        fallLine.endColor = targetColor;
        
        // İsteğe bağlı: Mesafe azaldıkça çizgiyi biraz daha inceltebiliriz
        float widthLerp = Mathf.Clamp01(distance / decelerationDistance);
        fallLine.startWidth = Mathf.Lerp(fallLineEndWidth, fallLineStartWidth, widthLerp);
        fallLine.endWidth = fallLineEndWidth;
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
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60f);
            int seconds = Mathf.FloorToInt(currentTime % 60f);
            timerText.text = string.Format("{0}:{1:00}", minutes, seconds);
        }
    }
    
    void GameOver()
    {
        isGameActive = false;
        
        // Düşüş çizgisini gizle
        if (fallLine != null)
            fallLine.enabled = false;
            
        Debug.Log("⏰ Oyun bitti! Süre doldu.");
    }
    
    public void OnPauseButtonClicked()
    {
        if (currentPauseChances <= 0 && !isPaused)
        {
            Debug.Log("❌ Pause hakkın kalmadı!");
            return;
        }
        
        if (!isPaused)
        {
            isPaused = true;
            if (currentPauseChances > 0)
            {
                currentPauseChances--;
            }
            
            // Düşüş çizgisini gizle
            if (fallLine != null)
                fallLine.enabled = false;
                
            Debug.Log("⏸️ Oyun DURDURULDU - Kalan pause: " + currentPauseChances);
        }
        else
        {
            isPaused = false;
            Debug.Log("▶️ Oyun DEVAM ETTİRİLDİ");
        }
        
        UpdatePauseUI();
    }
    
    void UpdatePauseUI()
    {
        if (pauseButton != null)
        {
            if (isPaused)
            {
                pauseButton.image.sprite = continueSprite;
            }
            else
            {
                pauseButton.image.sprite = pauseSprite;
            }
        }
        
        if (pauseText != null)
        {
            pauseText.text = currentPauseChances.ToString();
        }
    }
    
    void SpawnNewBrick()
    {
        if (!isGameActive || isPaused) return;
        
        ResetBrickState();
        
        List<GameObject> availableBricks = GetBricksFromNames(levelBrickNames);
        List<GameObject> suitableBricks = GetSuitableBricks(availableBricks);
        
        if (suitableBricks.Count == 0)
        {
            Debug.LogError("Grid'e uygun brick bulunamadı!");
            availableBricks = GetBricksFromNames(GetAllBrickNames());
            suitableBricks = GetSuitableBricks(availableBricks);
            
            if (suitableBricks.Count == 0)
            {
                Debug.LogError("Hiç uygun brick yok!");
                return;
            }
        }
        
        GameObject randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        currentBrick = Instantiate(randomBrickPrefab);
        currentBrick.name = "CurrentBrick";
        
        originalRotation = currentBrick.transform.eulerAngles;
        
        if (levelColors.Count > 0)
        {
            currentBrickColor = levelColors[Random.Range(0, levelColors.Count)];
        }
        else
        {
            currentBrickColor = (BrickColor)Random.Range(0, availableColors.Count);
        }
        
        ApplyBrickTexture(currentBrick, currentBrickColor);
        CalculateBrickSize();
        InitializeBrickPosition();
        
        Debug.Log($"🎯 Yeni brick oluşturuldu - Renk: {currentBrickColor}, Tip: {randomBrickPrefab.name}");
    }
    
    void DebugLogAvailableBrickNames()
    {
        List<string> allNames = GetAllBrickNames();
        Debug.Log($"🏗️ Mevcut Brick İsimleri ({allNames.Count} adet):");
        foreach (string name in allNames)
        {
            Debug.Log($"   - {name}");
        }
    }
    
    List<GameObject> GetBricksFromNames(List<string> brickNames)
    {
        List<GameObject> result = new List<GameObject>();
        
        if (brickNames.Count == 0)
        {
            return new List<GameObject>(brickPrefabs);
        }
        
        foreach (string brickName in brickNames)
        {
            GameObject brick = FindBrickByName(brickName);
            if (brick != null)
            {
                result.Add(brick);
            }
            else
            {
                Debug.LogWarning($"⚠️ Brick bulunamadı: {brickName}");
            }
        }
        
        Debug.Log($"🔧 İsimlere göre brick'ler: {result.Count}/{brickNames.Count} bulundu");
        return result;
    }
    
    GameObject FindBrickByName(string brickName)
    {
        foreach (GameObject brickPrefab in brickPrefabs)
        {
            if (brickPrefab.name == brickName)
            {
                return brickPrefab;
            }
        }
        return null;
    }
    
    public List<string> GetAllBrickNames()
    {
        List<string> names = new List<string>();
        foreach (GameObject brickPrefab in brickPrefabs)
        {
            names.Add(brickPrefab.name);
        }
        return names;
    }
    
    List<GameObject> GetSuitableBricks(List<GameObject> brickList)
    {
        List<GameObject> suitableBricks = new List<GameObject>();
        
        foreach (GameObject brickPrefab in brickList)
        {
            Vector2Int size = GetBrickSize(brickPrefab);
            if (size.x <= gridManager.gridSize.x && size.y <= gridManager.gridSize.y)
                suitableBricks.Add(brickPrefab);
        }
        
        Debug.Log($"🔧 Uygun brick sayısı: {suitableBricks.Count}/{brickList.Count}");
        return suitableBricks;
    }
    
    void ResetBrickState()
    {
        isFalling = hasLanded = isDecelerating = isSnapping = isSettling = false;
        currentFallSpeed = initialFallSpeed;
        settleTimer = currentWobble = wobbleTimer = 0f;
        
        // Düşüş çizgisini gizle
        if (fallLine != null)
            fallLine.enabled = false;
    }
    
    void ApplyBrickTexture(GameObject brick, BrickColor color)
    {
        if (availableColors.Count == 0) return;
        
        ColorSettings colorSettings = GetColorSettings(color);
        
        foreach (Renderer renderer in brick.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;
            
            Material newMaterial = new Material(renderer.material);
            newMaterial.name = "BrickMaterial_" + color.ToString();
            newMaterial.mainTextureScale = colorSettings.tiling;
            newMaterial.mainTextureOffset = colorSettings.offset;
            renderer.material = newMaterial;
        }
    }
    
    ColorSettings GetColorSettings(BrickColor color)
    {
        foreach (var colorSetting in availableColors)
            if (colorSetting.colorType == color)
                return colorSetting;
        
        return availableColors[0];
    }
    
    // UI BUTON FONKSİYONLARI
    public void OnMoveUpButton() {
        if (CanMoveBrick()) {
            Vector2Int newPosition = new Vector2Int(currentGridPosition.x, currentGridPosition.y + 1);
            if (gridManager.IsValidPosition(newPosition, brickSize))
                MoveBrickToGrid(newPosition.x, newPosition.y);
        }
    }

    public void OnMoveDownButton() {
        if (CanMoveBrick()) {
            Vector2Int newPosition = new Vector2Int(currentGridPosition.x, currentGridPosition.y - 1);
            if (gridManager.IsValidPosition(newPosition, brickSize))
                MoveBrickToGrid(newPosition.x, newPosition.y);
        }
    }

    public void OnMoveLeftButton() {
        if (CanMoveBrick()) {
            Vector2Int newPosition = new Vector2Int(currentGridPosition.x - 1, currentGridPosition.y);
            if (gridManager.IsValidPosition(newPosition, brickSize))
                MoveBrickToGrid(newPosition.x, newPosition.y);
        }
    }

    public void OnMoveRightButton() {
        if (CanMoveBrick()) {
            Vector2Int newPosition = new Vector2Int(currentGridPosition.x + 1, currentGridPosition.y);
            if (gridManager.IsValidPosition(newPosition, brickSize))
                MoveBrickToGrid(newPosition.x, newPosition.y);
        }
    }

    public void OnRotateButton() {
        if (CanMoveBrick()) {
            RotateBrick();
        }
    }

    bool CanMoveBrick()
    {
        if (currentBrick == null || isPaused || hasLanded || !isGameActive) return false;
        
        if (isFalling)
        {
            float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
            float distanceToTarget = Mathf.Abs(currentBrick.transform.position.y - targetY);
            
            if (distanceToTarget <= movementLockDistance)
            {
                Debug.Log($"🚫 Yerleşmeye çok yakın ({distanceToTarget:F2}) - hareket kilitlendi");
                return false;
            }
        }
        
        return true;
    }
    
    void RotateBrick()
    {
        currentBrick.transform.Rotate(0, 90, 0);
        originalRotation = currentBrick.transform.eulerAngles;
        
        Vector2Int newSize = new Vector2Int(brickSize.y, brickSize.x);
        AdjustPositionAfterRotation(newSize);
        brickSize = newSize;
        
        // Brick döndürüldüğünde çizgiyi güncelle
        UpdateFallLine();
    }
    
    void AdjustPositionAfterRotation(Vector2Int newSize)
    {
        int maxX = Mathf.Max(0, (int)gridManager.gridSize.x - newSize.x);
        int maxY = Mathf.Max(0, (int)gridManager.gridSize.y - newSize.y);
        
        currentGridPosition.x = Mathf.Clamp(currentGridPosition.x, 0, maxX);
        currentGridPosition.y = Mathf.Clamp(currentGridPosition.y, 0, maxY);
        
        Vector3 gridPosition = gridManager.GetGridPosition(currentGridPosition, newSize);
        currentBrick.transform.position = new Vector3(gridPosition.x, currentBrick.transform.position.y, gridPosition.z);
    }
    
    Vector2Int GetBrickSize(GameObject brickPrefab)
    {
        string prefabName = brickPrefab.name;
        string[] sizeParts = prefabName.Split('x');
        
        if (sizeParts.Length == 2)
        {
            return new Vector2Int(
                int.Parse(sizeParts[0].Substring(sizeParts[0].Length - 1)),
                int.Parse(sizeParts[1])
            );
        }
        
        Vector3 localScale = brickPrefab.transform.localScale;
        return new Vector2Int(
            Mathf.RoundToInt(localScale.x),
            Mathf.RoundToInt(localScale.z)
        );
    }
    
    void CalculateBrickSize() => brickSize = GetBrickSize(currentBrick);
    
    void InitializeBrickPosition()
    {
        int maxX = Mathf.Max(0, (int)gridManager.gridSize.x - brickSize.x);
        int maxY = Mathf.Max(0, (int)gridManager.gridSize.y - brickSize.y);
        
        currentGridPosition = new Vector2Int(
            Random.Range(0, maxX + 1),
            Random.Range(0, maxY + 1)
        );
        
        Vector3 gridPosition = gridManager.GetGridPosition(currentGridPosition, brickSize);
        currentBrick.transform.position = new Vector3(gridPosition.x, startYPosition, gridPosition.z);
    }
    
    void Update()
    {
        if (currentBrick == null || !isGameActive || isPaused) return;
        
        if (!hasLanded)
        {
            HandleBrickMovement();
            HandleRotationInput();
            
            if (!isFalling)
            {
                StartFalling();
            }
            else
            {
                HandleFalling();
            }
            
            // YENİ: Düşüş çizgisini sürekli güncelle
            UpdateFallLine();
        }
    }
    
    void HandleBrickMovement()
    {
        if (!CanMoveBrick()) return;
        
        Vector2Int newPosition = currentGridPosition;
        
        if (Input.GetKeyDown(KeyCode.UpArrow)) newPosition.y++;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) newPosition.y--;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) newPosition.x++;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) newPosition.x--;
        
        if (newPosition != currentGridPosition && gridManager.IsValidPosition(newPosition, brickSize))
        {
            MoveBrickToGrid(newPosition.x, newPosition.y);
            // Pozisyon değiştiğinde çizgiyi güncelle
            UpdateFallLine();
        }
    }
    
    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && CanMoveBrick())
        {
            RotateBrick();
        }
    }
    
    void StartFalling()
    {
        isFalling = true;
        currentWobble = wobbleAmount;
        
        // Düşüş başladığında çizgiyi göster
        if (fallLine != null)
            fallLine.enabled = true;
    }
    
    void HandleFalling()
    {
        if (isPaused) return;
        
        float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        Vector3 currentPos = currentBrick.transform.position;
        float distanceToTarget = Mathf.Abs(currentPos.y - targetY);
        
        ApplyWobbleEffect(distanceToTarget);
        HandleFallPhases(targetY, currentPos, distanceToTarget);
    }
    
    void ApplyWobbleEffect(float distanceToTarget)
    {
        if (isFalling && !isSettling && !isPaused)
        {
            wobbleTimer += Time.deltaTime * wobbleFrequency;
            float wobbleProgress = distanceToTarget / decelerationDistance;
            float decayFactor = Mathf.Clamp01(wobbleProgress);
            float currentWobbleAmount = currentWobble * decayFactor;
            
            currentBrick.transform.rotation = Quaternion.Euler(
                originalRotation.x + Mathf.Sin(wobbleTimer) * currentWobbleAmount * 0.7f,
                originalRotation.y,
                originalRotation.z + Mathf.Cos(wobbleTimer * 0.8f) * currentWobbleAmount * 0.5f
            );
            
            if (isDecelerating)
                currentWobble = Mathf.Lerp(currentWobble, 0f, wobbleDecay * Time.deltaTime);
        }
    }
    
    void HandleFallPhases(float targetY, Vector3 currentPos, float distanceToTarget)
    {
        if (!isDecelerating && distanceToTarget <= decelerationDistance)
        {
            isDecelerating = true;
            targetSettleY = targetY;
        }
        
        if (!isDecelerating)
        {
            currentFallSpeed = Mathf.Min(currentFallSpeed + accelerationRate * Time.deltaTime, maxFallSpeed);
        }
        else if (!isSnapping && !isSettling)
        {
            float decelerationProgress = 1f - (distanceToTarget / decelerationDistance);
            currentFallSpeed = Mathf.Lerp(maxFallSpeed, snapSpeed, decelerationProgress * decelerationProgress);
            
            if (distanceToTarget < 0.2f)
            {
                isSnapping = true;
                overshootY = targetY + settleOvershoot;
            }
        }
        
        if (isSnapping && !isSettling)
        {
            SnapToPosition(currentPos, overshootY);
        }
        else if (isSettling)
        {
            SettleBrick(currentPos);
        }
        else
        {
            FallNormally(currentPos, targetY);
        }
    }
    
    void SnapToPosition(Vector3 currentPos, float targetY)
    {
        float newY = Mathf.MoveTowards(currentPos.y, targetY, snapSpeed * Time.deltaTime);
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
        float settleProgress = Mathf.Clamp01(settleTimer / settleDuration);
        float smoothProgress = 1f - Mathf.Pow(1f - settleProgress, 3f);
        
        currentBrick.transform.rotation = Quaternion.Euler(originalRotation);
        currentBrick.transform.position = new Vector3(currentPos.x, Mathf.Lerp(overshootY, targetSettleY, smoothProgress), currentPos.z);
        
        if (settleProgress >= 1f) OnBrickLanded();
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
        
        gridManager.PlaceBrick(currentGridPosition, brickSize, currentBrick, currentBrickColor);
        
        CheckForCompletedLayers();
        
        isFalling = false;
        hasLanded = true;
        isDecelerating = false;
        isSnapping = false;
        isSettling = false;
        
        // Brick yerleştiğinde çizgiyi gizle
        if (fallLine != null)
            fallLine.enabled = false;
        
        if (!isPaused)
        {
            Invoke("SpawnNewBrick", autoFallDelay);
        }
    }
    
    void CheckForCompletedLayers()
{
    int highestLayer = gridManager.GetHighestLayer();
    
    for(int layer = highestLayer; layer >= 0; layer--)
    {
        BrickColor? layerColor = gridManager.CheckCompletedLayerWithColor(layer);
        
        if(layerColor.HasValue)
        {
            // SADECE EffectManager'ı çağır, başka hiçbir şey yapma!
            effectManager.ClearLayerWithEffects(layer);
            break;
        }
    }
}


    
    public Vector2Int GetBrickGridPosition(GameObject brick)
    {
        Vector3 worldPos = brick.transform.position;
        
        int gridX = Mathf.RoundToInt(worldPos.x / gridManager.cellSize);
        int gridY = Mathf.RoundToInt(worldPos.z / gridManager.cellSize);
        
        gridX = Mathf.Clamp(gridX, 0, (int)gridManager.gridSize.x - 1);
        gridY = Mathf.Clamp(gridY, 0, (int)gridManager.gridSize.y - 1);
        
        return new Vector2Int(gridX, gridY);
    }
    
    void MoveBrickToGrid(int x, int y)
    {
        currentGridPosition = new Vector2Int(x, y);
        Vector3 gridPosition = gridManager.GetGridPosition(currentGridPosition, brickSize);
        currentBrick.transform.position = new Vector3(gridPosition.x, currentBrick.transform.position.y, gridPosition.z);
    }
    
    public void SetPauseChances(int chances)
    {
        maxPauseChances = chances;
        currentPauseChances = chances;
        UpdatePauseUI();
    }
    
    public void SetLevelTime(float timeInSeconds)
    {
        levelTime = timeInSeconds;
        currentTime = timeInSeconds;
        UpdateTimerUI();
    }
    
    public void SetLevelBrickNames(List<string> brickNames)
    {
        levelBrickNames.Clear();
        levelBrickNames.AddRange(brickNames);
        Debug.Log($"🔧 Level brick isimleri ayarlandı: {string.Join(", ", brickNames)}");
    }
    
    public void UseAllBricks()
    {
        levelBrickNames.Clear();
        levelBrickNames.AddRange(GetAllBrickNames());
        Debug.Log($"🔧 Tüm brick'ler kullanılacak: {levelBrickNames.Count} brick");
    }
    
    // YENİ: Düşüş çizgisini temizle (sahne değişikliklerinde)
    void OnDestroy()
    {
        if (fallLine != null && fallLine.gameObject != null)
        {
            Destroy(fallLine.gameObject);
        }
    }
}
using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public EffectManager effectManager;
    public List<GameObject> brickPrefabs;
    
    [Header("Fall Settings")]
    public float initialFallSpeed = 2f;
    public float maxFallSpeed = 15f;
    public float accelerationRate = 8f;
    public float decelerationDistance = 3f;
    public float snapSpeed = 1f;
    public float settleOvershoot = 0.1f;
    public float settleDuration = 0.4f;
    
    [Header("Wobble Effects")]
    public float wobbleAmount = 3f;
    public float wobbleFrequency = 2f;
    public float wobbleDecay = 2f;
    
    public enum BrickColor { Orange, Blue, Pink, Purple, Green, White, Gray, Brown, Black }

    [System.Serializable]
    public class ColorSettings
    {
        public BrickColor colorType;
        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
    }

    [Header("Level Settings")]
    public List<BrickColor> levelColors = new List<BrickColor>();
    public int maxColorsPerLevel = 3; // Level başına maksimum renk sayısı
    
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
    private float currentFallSpeed, settleTimer, targetSettleY, overshootY;
    private float currentWobble, wobbleTimer;
    private Vector3 originalRotation;
    private const float startYPosition = 10f;

    void Start()
    {
        currentFallSpeed = initialFallSpeed;
        SpawnNewBrick();
    }
    
    void Update()
    {
        if (currentBrick == null) return;
        
        if (!hasLanded)
        {
            if (!isFalling)
            {
                HandleBrickMovement();
                if (Input.GetKeyDown(KeyCode.Space)) StartFalling();
                if (Input.GetKeyDown(KeyCode.R)) RotateBrick();
            }
            else
            {
                HandleFalling();
            }
        }
    }
    
    void SpawnNewBrick()
    {
        ResetBrickState();
        
        List<GameObject> suitableBricks = GetSuitableBricks();
        if (suitableBricks.Count == 0)
        {
            Debug.LogError("Grid'e uygun brick bulunamadı!");
            return;
        }
        
        GameObject randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        currentBrick = Instantiate(randomBrickPrefab);
        currentBrick.name = "CurrentBrick";
        
        originalRotation = currentBrick.transform.eulerAngles;
        ApplyRandomTexture(currentBrick);
        CalculateBrickSize();
        InitializeBrickPosition();
    }
    
    void ResetBrickState()
    {
        isFalling = hasLanded = isDecelerating = isSnapping = isSettling = false;
        currentFallSpeed = initialFallSpeed;
        settleTimer = currentWobble = wobbleTimer = 0f;
    }
    
    void ApplyRandomTexture(GameObject brick)
    {
        if (availableColors.Count == 0) return;
        
        ColorSettings randomColorSettings = availableColors[Random.Range(0, availableColors.Count)];
        
        foreach (Renderer renderer in brick.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;
            
            Material newMaterial = new Material(renderer.material);
            newMaterial.name = "BrickMaterial_" + randomColorSettings.colorType.ToString();
            newMaterial.mainTextureScale = randomColorSettings.tiling;
            newMaterial.mainTextureOffset = randomColorSettings.offset;
            renderer.material = newMaterial;
        }
    }
    
    // UI BUTON FONKSİYONLARI - BAŞLANGIÇ
    public void OnMoveUpButton() {
        if (currentBrick == null || isFalling || hasLanded) return;
        Vector2Int newPosition = new Vector2Int(currentGridPosition.x, currentGridPosition.y + 1);
        if (gridManager.IsValidPosition(newPosition, brickSize))
            MoveBrickToGrid(newPosition.x, newPosition.y);
    }

    public void OnMoveDownButton() {
        if (currentBrick == null || isFalling || hasLanded) return;
        Vector2Int newPosition = new Vector2Int(currentGridPosition.x, currentGridPosition.y - 1);
        if (gridManager.IsValidPosition(newPosition, brickSize))
            MoveBrickToGrid(newPosition.x, newPosition.y);
    }

    public void OnMoveLeftButton() {
        if (currentBrick == null || isFalling || hasLanded) return;
        Vector2Int newPosition = new Vector2Int(currentGridPosition.x - 1, currentGridPosition.y);
        if (gridManager.IsValidPosition(newPosition, brickSize))
            MoveBrickToGrid(newPosition.x, newPosition.y);
    }

    public void OnMoveRightButton() {
        if (currentBrick == null || isFalling || hasLanded) return;
        Vector2Int newPosition = new Vector2Int(currentGridPosition.x + 1, currentGridPosition.y);
        if (gridManager.IsValidPosition(newPosition, brickSize))
            MoveBrickToGrid(newPosition.x, newPosition.y);
    }

    public void OnRotateButton() {
        if (currentBrick != null && !isFalling && !hasLanded) {
            RotateBrick();
        }
    }

    public void OnDropButton() {
        if (currentBrick != null && !isFalling && !hasLanded) {
            StartFalling();
        }
    }
    // UI BUTON FONKSİYONLARI - BİTİŞ
    
    void RotateBrick()
    {
        currentBrick.transform.Rotate(0, 90, 0);
        originalRotation = currentBrick.transform.eulerAngles;
        
        Vector2Int newSize = new Vector2Int(brickSize.y, brickSize.x);
        AdjustPositionAfterRotation(newSize);
        brickSize = newSize;
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
    
    List<GameObject> GetSuitableBricks()
    {
        List<GameObject> suitableBricks = new List<GameObject>();
        
        foreach (GameObject brickPrefab in brickPrefabs)
        {
            Vector2Int size = GetBrickSize(brickPrefab);
            if (size.x <= gridManager.gridSize.x && size.y <= gridManager.gridSize.y)
                suitableBricks.Add(brickPrefab);
        }
        
        return suitableBricks;
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
    
    void HandleBrickMovement()
    {
        Vector2Int newPosition = currentGridPosition;
        
        if (Input.GetKeyDown(KeyCode.UpArrow)) newPosition.y++;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) newPosition.y--;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) newPosition.x++;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) newPosition.x--;
        
        if (newPosition != currentGridPosition && gridManager.IsValidPosition(newPosition, brickSize))
            MoveBrickToGrid(newPosition.x, newPosition.y);
    }
    
    void StartFalling()
    {
        isFalling = true;
        currentWobble = wobbleAmount;
    }
    
    void HandleFalling()
    {
        float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        Vector3 currentPos = currentBrick.transform.position;
        float distanceToTarget = Mathf.Abs(currentPos.y - targetY);
        
        ApplyWobbleEffect(distanceToTarget);
        HandleFallPhases(targetY, currentPos, distanceToTarget);
    }
    
    void ApplyWobbleEffect(float distanceToTarget)
    {
        if (isFalling && !isSettling)
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
        
        gridManager.PlaceBrick(currentGridPosition, brickSize, currentBrick);
        CheckForCompletedLayers();
        
        ResetBrickState();
        hasLanded = true;
        Invoke("SpawnNewBrick", 0.2f);
    }
    
    void CheckForCompletedLayers()
    {
        int highestLayer = gridManager.GetHighestLayer();
        
        for(int layer = highestLayer; layer >= 0; layer--)
        {
            if(gridManager.CheckCompletedLayer(layer) != null)
            {
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
}
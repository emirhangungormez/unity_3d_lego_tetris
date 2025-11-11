using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GridManager gridManager;
    public EffectManager effectManager;
    public List<GameObject> brickPrefabs;
    
    [Header("Fall Animation Settings")]
    public float initialFallSpeed = 2f;
    public float maxFallSpeed = 15f;
    public float accelerationRate = 8f;
    public float decelerationDistance = 3f;
    public float snapSpeed = 1f;
    public float settleOvershoot = 0.1f;
    public float settleDuration = 0.4f;
    
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
    
    private GameObject currentBrick;
    public List<GameObject> landedBricks = new List<GameObject>(); 
    private Vector2Int currentGridPosition;
    private Vector2Int brickSize;
    private bool isFalling = false;
    private bool hasLanded = false;
    private float startYPosition = 10f;
    
    private float currentFallSpeed;
    private bool isDecelerating = false;
    private bool isSnapping = false;
    private bool isSettling = false;
    private float settleTimer = 0f;
    private float targetSettleY = 0f;
    private float overshootY = 0f;
    
    // Public property for EffectManager access
    public List<GameObject> LandedBricks => landedBricks;
    
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
                HandleFallInput();
                HandleRotationInput();
            }
            else
            {
                HandleFalling();
            }
        }
    }
    
    void SpawnNewBrick()
    {
        isFalling = false;
        hasLanded = false;
        isDecelerating = false;
        isSnapping = false;
        isSettling = false;
        currentFallSpeed = initialFallSpeed;
        settleTimer = 0f;
        
        List<GameObject> suitableBricks = GetSuitableBricks();
        
        if (suitableBricks.Count == 0)
        {
            Debug.LogError("Grid'e uygun brick bulunamadı!");
            return;
        }
        
        GameObject randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        currentBrick = Instantiate(randomBrickPrefab);
        currentBrick.name = "CurrentBrick";
        
        ApplyRandomTexture(currentBrick);
        CalculateBrickSize();
        InitializeBrickPosition();
    }
    
    void ApplyRandomTexture(GameObject brick)
    {
        if (availableColors.Count == 0)
        {
            Debug.LogWarning("Renk listesi boş!");
            return;
        }
        
        Renderer[] allRenderers = brick.GetComponentsInChildren<Renderer>(true);
        
        if (allRenderers.Length == 0)
        {
            Debug.LogError("Brick içinde hiç renderer bulunamadı!");
            return;
        }
        
        ColorSettings randomColorSettings = availableColors[Random.Range(0, availableColors.Count)];
        
        foreach (Renderer renderer in allRenderers)
        {
            if (renderer == null) continue;
            
            Material newMaterial = new Material(renderer.material);
            newMaterial.name = "BrickMaterial_" + randomColorSettings.colorType.ToString();
            
            newMaterial.mainTextureScale = randomColorSettings.tiling;
            newMaterial.mainTextureOffset = randomColorSettings.offset;
            
            renderer.material = newMaterial;
        }
    }
    
    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateBrick();
        }
    }
    
    void RotateBrick()
    {
        currentBrick.transform.Rotate(0, 90, 0);
        
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
            Vector2Int size = GetBrickSizeFromPrefab(brickPrefab);
            
            if (size.x <= gridManager.gridSize.x && size.y <= gridManager.gridSize.y)
            {
                suitableBricks.Add(brickPrefab);
            }
        }
        
        return suitableBricks;
    }
    
    Vector2Int GetBrickSizeFromPrefab(GameObject brickPrefab)
    {
        string prefabName = brickPrefab.name;
        string[] sizeParts = prefabName.Split('x');
        
        if (sizeParts.Length == 2)
        {
            int x = int.Parse(sizeParts[0].Substring(sizeParts[0].Length - 1));
            int y = int.Parse(sizeParts[1]);
            return new Vector2Int(x, y);
        }
        
        Vector3 localScale = brickPrefab.transform.localScale;
        return new Vector2Int(
            Mathf.RoundToInt(localScale.x),
            Mathf.RoundToInt(localScale.z)
        );
    }
    
    void CalculateBrickSize()
    {
        brickSize = GetBrickSizeFromPrefab(currentBrick);
        brickSize.x = Mathf.Max(1, brickSize.x);
        brickSize.y = Mathf.Max(1, brickSize.y);
    }
    
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
        
        if (Input.GetKeyDown(KeyCode.UpArrow)) newPosition.y += 1;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) newPosition.y -= 1;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) newPosition.x += 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) newPosition.x -= 1;
        
        if (newPosition != currentGridPosition && gridManager.IsValidPosition(newPosition, brickSize))
        {
            MoveBrickToGrid(newPosition.x, newPosition.y);
        }
    }
    
    void HandleFallInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartFalling();
        }
    }
    
    void StartFalling()
    {
        isFalling = true;
        isDecelerating = false;
        isSnapping = false;
        isSettling = false;
        currentFallSpeed = initialFallSpeed;
        settleTimer = 0f;
    }
    
    void HandleFalling()
    {
        float targetY = gridManager.GetRequiredHeight(currentGridPosition, brickSize);
        Vector3 currentPos = currentBrick.transform.position;
        float distanceToTarget = Mathf.Abs(currentPos.y - targetY);
        
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
            float newY = Mathf.MoveTowards(currentPos.y, overshootY, snapSpeed * Time.deltaTime);
            currentBrick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
            
            if (Mathf.Abs(newY - overshootY) < 0.01f)
            {
                isSettling = true;
                settleTimer = 0f;
            }
        }
        else if (isSettling)
        {
            settleTimer += Time.deltaTime;
            float settleProgress = Mathf.Clamp01(settleTimer / settleDuration);
            
            float smoothProgress = 1f - Mathf.Pow(1f - settleProgress, 3f);
            float newY = Mathf.Lerp(overshootY, targetSettleY, smoothProgress);
            
            currentBrick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
            
            if (settleProgress >= 1f)
            {
                OnBrickLanded();
            }
            
            return;
        }
        else
        {
            float newY = Mathf.MoveTowards(currentPos.y, targetY, currentFallSpeed * Time.deltaTime);
            currentBrick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
            
            if (Mathf.Approximately(newY, targetY))
            {
                OnBrickLanded();
            }
        }
    }
    
    void OnBrickLanded()
    {
        landedBricks.Add(currentBrick);
        currentBrick.name = $"LandedBrick_{landedBricks.Count}";
        
        gridManager.PlaceBrick(currentGridPosition, brickSize, currentBrick);
        
        CheckForCompletedLayers();
        
        isFalling = false;
        hasLanded = true;
        isDecelerating = false;
        isSnapping = false;
        isSettling = false;
        
        Invoke("SpawnNewBrick", 0.2f);
    }
    
    void CheckForCompletedLayers()
    {
        gridManager.PrintGridStatus();
        
        int highestLayer = gridManager.GetHighestLayer();
        bool foundCompletedLayer = false;
        
        for(int layer = highestLayer; layer >= 0; layer--)
        {
            List<Vector2Int> completedPositions = gridManager.CheckCompletedLayer(layer);
            
            if(completedPositions != null)
            {
                Debug.Log($"🎉 KATMAN {layer} TAMAMLANDI! Efektler başlatılıyor...");
                
                // Tüm işlemleri EffectManager'a devret
                effectManager.ClearLayerWithEffects(layer);
                
                foundCompletedLayer = true;
                break;
            }
        }
        
        if(!foundCompletedLayer)
        {
            Debug.Log("❌ Hiçbir katman tamamlanmamış");
        }
    }
    
    // EffectManager için public metod
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
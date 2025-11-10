using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public GridManager gridManager;
    public List<GameObject> brickPrefabs;
    public float fallSpeed = 5f;
    
    private GameObject currentBrick;
    private List<GameObject> landedBricks = new List<GameObject>();
    private Vector2Int currentGridPosition;
    private Vector2Int brickSize;
    private bool isFalling = false;
    private bool hasLanded = false;
    private float startYPosition = 8f;
    private float targetYPosition = 0f;
    
    void Start()
    {
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
            }
            else
            {
                HandleFalling();
            }
        }
    }
    
    void SpawnNewBrick()
    {
        // Reset durumları
        isFalling = false;
        hasLanded = false;
        
        // Grid'e uygun brick'leri filtrele
        List<GameObject> suitableBricks = GetSuitableBricks();
        
        if (suitableBricks.Count == 0)
        {
            Debug.LogError("Grid'e uygun brick bulunamadı!");
            return;
        }
        
        // Rastgele bir brick seç
        GameObject randomBrickPrefab = suitableBricks[Random.Range(0, suitableBricks.Count)];
        
        // Brick'i spawn et
        currentBrick = Instantiate(randomBrickPrefab);
        currentBrick.name = "CurrentBrick";
        
        CalculateBrickSize();
        InitializeBrickPosition();
        
        Debug.Log($"Yeni brick spawn edildi: {brickSize.x}x{brickSize.y}");
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
        // Rastgele başlangıç pozisyonu
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
        Debug.Log("Brick düşmeye başladı!");
    }
    
    void HandleFalling()
    {
        Vector3 currentPos = currentBrick.transform.position;
        float newY = Mathf.MoveTowards(currentPos.y, targetYPosition, fallSpeed * Time.deltaTime);
        
        currentBrick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
        
        if (Mathf.Approximately(newY, targetYPosition))
        {
            OnBrickLanded();
        }
    }
    
    void OnBrickLanded()
    {
        isFalling = false;
        hasLanded = true;
        
        // Brick'i landed listesine ekle
        landedBricks.Add(currentBrick);
        currentBrick.name = $"LandedBrick_{landedBricks.Count}";
        
        Debug.Log($"Brick düştü! Toplam landed brick: {landedBricks.Count}");
        
        // Yeni brick spawn et
        SpawnNewBrick();
    }
    
    void MoveBrickToGrid(int x, int y)
    {
        currentGridPosition = new Vector2Int(x, y);
        Vector3 gridPosition = gridManager.GetGridPosition(currentGridPosition, brickSize);
        
        currentBrick.transform.position = new Vector3(gridPosition.x, currentBrick.transform.position.y, gridPosition.z);
    }
}
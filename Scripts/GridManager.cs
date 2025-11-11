using UnityEngine;

public class GridManager : MonoBehaviour
{
    public Vector2 gridSize = new Vector2(8, 8);
    public float cellSize = 1f;
    public float layerHeight = 1.3f;
    
    private int[,] heightMap;
    
    void Start()
    {
        heightMap = new int[(int)gridSize.x, (int)gridSize.y];
        // CenterGridAtOrigin()'i KALDIRDIK - Grid (0,0,0)'da başlayacak
    }
    
    public bool IsValidPosition(Vector2Int position, Vector2Int size)
    {
        return position.x >= 0 && 
               position.y >= 0 && 
               position.x + size.x <= gridSize.x && 
               position.y + size.y <= gridSize.y;
    }
    
    public Vector3 GetGridPosition(Vector2Int gridPos, Vector2Int size)
    {
        // Doğrudan world position hesapla - transform kullanma
        Vector3 worldPos = new Vector3(
            gridPos.x * cellSize + (size.x * cellSize * 0.5f),
            0,
            gridPos.y * cellSize + (size.y * cellSize * 0.5f)
        );
        return worldPos;
    }
    
    public float GetRequiredHeight(Vector2Int gridPos, Vector2Int size)
    {
        int maxHeight = 0;
        for(int x = gridPos.x; x < gridPos.x + size.x; x++)
        {
            for(int y = gridPos.y; y < gridPos.y + size.y; y++)
            {
                if(x < gridSize.x && y < gridSize.y)
                {
                    maxHeight = Mathf.Max(maxHeight, heightMap[x, y]);
                }
            }
        }
        return maxHeight * layerHeight;
    }
    
    public void UpdateHeightMap(Vector2Int gridPos, Vector2Int size)
    {
        int currentMaxHeight = 0;
        
        for(int x = gridPos.x; x < gridPos.x + size.x; x++)
        {
            for(int y = gridPos.y; y < gridPos.y + size.y; y++)
            {
                if(x < gridSize.x && y < gridSize.y)
                {
                    currentMaxHeight = Mathf.Max(currentMaxHeight, heightMap[x, y]);
                }
            }
        }
        
        int newHeight = currentMaxHeight + 1;
        
        for(int x = gridPos.x; x < gridPos.x + size.x; x++)
        {
            for(int y = gridPos.y; y < gridPos.y + size.y; y++)
            {
                if(x < gridSize.x && y < gridSize.y)
                {
                    heightMap[x, y] = newHeight;
                }
            }
        }
    }
    
    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        
        for (int x = 0; x <= gridSize.x; x++)
        {
            Vector3 start = new Vector3(x * cellSize, 0, 0);
            Vector3 end = new Vector3(x * cellSize, 0, gridSize.y * cellSize);
            Gizmos.DrawLine(start, end);
        }
        
        for (int y = 0; y <= gridSize.y; y++)
        {
            Vector3 start = new Vector3(0, 0, y * cellSize);
            Vector3 end = new Vector3(gridSize.x * cellSize, 0, y * cellSize);
            Gizmos.DrawLine(start, end);
        }
    }
}
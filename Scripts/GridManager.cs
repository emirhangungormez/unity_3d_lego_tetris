using UnityEngine;

public class GridManager : MonoBehaviour
{
    public Vector2 gridSize = new Vector2(2, 2);
    public float cellSize = 1f;
    
    public bool IsValidPosition(Vector2Int position, Vector2Int size)
    {
        return position.x >= 0 && 
               position.y >= 0 && 
               position.x + size.x <= gridSize.x && 
               position.y + size.y <= gridSize.y;
    }
    
    public Vector3 GetGridPosition(Vector2Int gridPos, Vector2Int size)
    {
        // Basit köşe pozisyonu + yarısı kadar offset
        Vector3 worldPos = new Vector3(
            gridPos.x * cellSize + (size.x * cellSize * 0.5f),
            0,
            gridPos.y * cellSize + (size.y * cellSize * 0.5f)
        );
        return worldPos;
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
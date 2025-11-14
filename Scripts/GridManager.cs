using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public Vector2 gridSize = new Vector2(8, 8);
    public float cellSize = 1f;
    public float layerHeight = 1.3f;
    
    private GameObject[,,] gridCells;
    private int currentHighestLayer = 0;
    private const int maxLayers = 100;

    void Start()
    {
        gridCells = new GameObject[(int)gridSize.x, (int)gridSize.y, maxLayers];
    }
    
    public bool IsValidPosition(Vector2Int position, Vector2Int size)
    {
        return position.x >= 0 && position.y >= 0 && 
               position.x + size.x <= gridSize.x && 
               position.y + size.y <= gridSize.y;
    }
    
    public Vector3 GetGridPosition(Vector2Int gridPos, Vector2Int size)
    {
        return new Vector3(
            gridPos.x * cellSize + (size.x * cellSize * 0.5f),
            0,
            gridPos.y * cellSize + (size.y * cellSize * 0.5f)
        );
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
                    for(int layer = currentHighestLayer; layer >= 0; layer--)
                    {
                        if(gridCells[x, y, layer] != null)
                        {
                            maxHeight = Mathf.Max(maxHeight, layer + 1);
                            break;
                        }
                    }
                }
            }
        }
        
        return maxHeight * layerHeight;
    }
    
    public void PlaceBrick(Vector2Int gridPos, Vector2Int size, GameObject brick)
    {
        int targetLayer = Mathf.RoundToInt(GetRequiredHeight(gridPos, size) / layerHeight);
        
        for(int x = gridPos.x; x < gridPos.x + size.x; x++)
        {
            for(int y = gridPos.y; y < gridPos.y + size.y; y++)
            {
                if(x < gridSize.x && y < gridSize.y)
                {
                    gridCells[x, y, targetLayer] = brick;
                }
            }
        }
        
        currentHighestLayer = Mathf.Max(currentHighestLayer, targetLayer);
    }
    
    public List<Vector2Int> CheckCompletedLayer(int layer)
    {
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                if(gridCells[x, y, layer] == null)
                {
                    return null;
                }
            }
        }
        
        List<Vector2Int> completedPositions = new List<Vector2Int>();
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                completedPositions.Add(new Vector2Int(x, y));
            }
        }
        
        return completedPositions;
    }
    
    public void RemoveLayer(int layer)
    {
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                gridCells[x, y, layer] = null;
            }
        }
        
        for(int l = layer + 1; l <= currentHighestLayer; l++)
        {
            for(int x = 0; x < gridSize.x; x++)
            {
                for(int y = 0; y < gridSize.y; y++)
                {
                    gridCells[x, y, l - 1] = gridCells[x, y, l];
                    gridCells[x, y, l] = null;
                }
            }
        }
        
        currentHighestLayer = Mathf.Max(0, currentHighestLayer - 1);
    }
    
    public int GetHighestLayer() => currentHighestLayer;
    
    public int GetLayerAtPosition(Vector2Int gridPos, GameObject brick)
    {
        for(int layer = 0; layer <= currentHighestLayer; layer++)
        {
            if(gridCells[gridPos.x, gridPos.y, layer] == brick)
            {
                return layer;
            }
        }
        return -1;
    }
    
    public void PrintGridStatus()
    {
        string status = "=== GRID DURUMU ===\n";
        
        for(int layer = 0; layer <= currentHighestLayer; layer++)
        {
            int filledCells = 0;
            for(int x = 0; x < gridSize.x; x++)
            {
                for(int y = 0; y < gridSize.y; y++)
                {
                    if(gridCells[x, y, layer] != null) filledCells++;
                }
            }
            status += $"Layer {layer}: {filledCells}/64 dolu\n";
        }
        
        Debug.Log(status);
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
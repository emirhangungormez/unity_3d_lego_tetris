using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public Vector2 gridSize = new Vector2(8, 8);
    public float cellSize = 1f;
    public float layerHeight = 1.3f;
    
    [Header("Plate Brick Settings")]
    public List<GameObject> plateBrickPrefabs;
    public bool isColorPlate = false;
    public float plateYPosition = -0.4f;

    private List<GameObject> gridPlateBricks = new List<GameObject>();
    private GameManager.BrickColor[,] gridPlateColors;
    private GameObject[,,] gridCells;
    private GameManager.BrickColor[,,] gridCellColors;
    private Dictionary<GameObject, Vector3Int> brickPositions = new Dictionary<GameObject, Vector3Int>();
    private int currentHighestLayer = 0;

    public int GridWidth => (int)gridSize.x;
    public int GridHeight => (int)gridSize.y;
    public int TotalCells => GridWidth * GridHeight;

    void Start()
    {
        InitializeGrid();
        CreateGridPlates();
    }

    public void InitializeGrid()
    {
        gridCells = new GameObject[GridWidth, GridHeight, 100];
        gridCellColors = new GameManager.BrickColor[GridWidth, GridHeight, 100];
        gridPlateColors = new GameManager.BrickColor[GridWidth, GridHeight];
        brickPositions.Clear();
        currentHighestLayer = 0;
        
        ClearGridPlates();
        CreateGridPlates();
    }

    private void CreateGridPlates()
    {
        ClearGridPlates();
        CalculateOptimalPlatePlacement().ForEach(CreatePlateBrick);
        ApplyPlateColors();
    }

    private List<PlatePlacement> CalculateOptimalPlatePlacement()
    {
        var covered = new bool[GridWidth, GridHeight];
        var placements = new List<PlatePlacement>();
        var plateSizes = new[,] { {4,8}, {8,4}, {4,4}, {2,4}, {4,2}, {2,2}, {1,1} };

        for (int i = 0; i < plateSizes.GetLength(0); i++)
        {
            int w = plateSizes[i,0], h = plateSizes[i,1];
            var prefab = GetPlateBrick(w, h);
            if (!prefab) continue;

            for (int x = 0; x <= GridWidth - w; x++)
            for (int y = 0; y <= GridHeight - h; y++)
                if (IsAreaEmpty(covered, x, y, w, h))
                {
                    placements.Add(new PlatePlacement(prefab, x, y, w, h));
                    MarkArea(covered, x, y, w, h, true);
                }
        }

        for (int x = 0; x < GridWidth; x++)
        for (int y = 0; y < GridHeight; y++)
            if (!covered[x,y] && GetPlateBrick(1,1))
                placements.Add(new PlatePlacement(GetPlateBrick(1,1), x, y, 1, 1));

        return placements;
    }

    private void CreatePlateBrick(PlatePlacement placement)
    {
        var plate = Instantiate(placement.prefab);
        plate.name = $"GridPlate_{placement.gridX}_{placement.gridY}_{placement.width}x{placement.height}";
        
        plate.transform.position = new Vector3(
            placement.gridX * cellSize + (placement.width * cellSize * 0.5f),
            plateYPosition,
            placement.gridY * cellSize + (placement.height * cellSize * 0.5f)
        );

        for (int x = placement.gridX; x < placement.gridX + placement.width; x++)
        for (int y = placement.gridY; y < placement.gridY + placement.height; y++)
            if (x < GridWidth && y < GridHeight)
                gridPlateColors[x, y] = GetRandomPlateColor();

        gridPlateBricks.Add(plate);
    }

    private void ApplyPlateColors()
    {
        var gameManager = FindObjectOfType<GameManager>();
        if (!gameManager) return;

        gridPlateBricks.ForEach(plate => 
            plate.GetComponentsInChildren<Renderer>().ToList().ForEach(renderer => 
            {
                if (!renderer) return;
                var material = new Material(renderer.material);
                
                if (isColorPlate)
                {
                    var color = GetRandomPlateColor();
                    var settings = gameManager.availableColors.Find(c => c.colorType == color) ?? gameManager.availableColors[0];
                    material.mainTextureScale = settings.tiling;
                    material.mainTextureOffset = settings.offset;
                }
                else
                {
                    material.color = Color.gray;
                }
                renderer.material = material;
            })
        );
    }

    public bool IsValidPosition(Vector2Int pos, Vector2Int size) =>
        pos.x >= 0 && pos.y >= 0 && pos.x + size.x <= GridWidth && pos.y + size.y <= GridHeight;
    
    public Vector3 GetGridPosition(Vector2Int gridPos, Vector2Int size) => new Vector3(
        gridPos.x * cellSize + (size.x * cellSize * 0.5f), 0,
        gridPos.y * cellSize + (size.y * cellSize * 0.5f)
    );
    
    public float GetRequiredHeight(Vector2Int gridPos, Vector2Int size)
    {
        int maxLayer = -1;
        
        for (int x = gridPos.x; x < gridPos.x + size.x && x < GridWidth; x++)
        {
            for (int y = gridPos.y; y < gridPos.y + size.y && y < GridHeight; y++)
            {
                int topLayer = GetTopLayerAt(x, y);
                if (topLayer > maxLayer)
                    maxLayer = topLayer;
            }
        }
        
        return (maxLayer + 1) * layerHeight;
    }
    
    public int GetMaxLayerAtPosition(Vector2Int gridPos, Vector2Int size)
    {
        int maxLayer = -1;
        
        for (int x = gridPos.x; x < gridPos.x + size.x && x < GridWidth; x++)
        {
            for (int y = gridPos.y; y < gridPos.y + size.y && y < GridHeight; y++)
            {
                int topLayer = GetTopLayerAt(x, y);
                if (topLayer > maxLayer)
                    maxLayer = topLayer;
            }
        }
        
        return maxLayer + 1;
    }

    public void PlaceBrick(Vector2Int gridPos, Vector2Int size, GameObject brick, GameManager.BrickColor color)
    {
        int maxLayer = -1;
        
        for (int x = gridPos.x; x < gridPos.x + size.x && x < GridWidth; x++)
        {
            for (int y = gridPos.y; y < gridPos.y + size.y && y < GridHeight; y++)
            {
                int topLayer = GetTopLayerAt(x, y);
                if (topLayer > maxLayer)
                    maxLayer = topLayer;
            }
        }
        
        int targetLayer = maxLayer + 1;
        
        for (int x = gridPos.x; x < gridPos.x + size.x && x < GridWidth; x++)
        {
            for (int y = gridPos.y; y < gridPos.y + size.y && y < GridHeight; y++)
            {
                gridCells[x, y, targetLayer] = brick;
                gridCellColors[x, y, targetLayer] = color;
            }
        }
        
        brickPositions[brick] = new Vector3Int(gridPos.x, gridPos.y, targetLayer);
        currentHighestLayer = Mathf.Max(currentHighestLayer, targetLayer);
    }
    
    public List<Vector2Int> FindMatchingLineInLayer(int layer)
    {
        for (int y = 0; y < GridHeight; y++)
        {
            GameManager.BrickColor? firstColor = null;
            List<Vector2Int> rowPositions = new List<Vector2Int>();
            bool isComplete = true;
            
            for (int x = 0; x < GridWidth; x++)
            {
                if (gridCells[x, y, layer] == null)
                {
                    isComplete = false;
                    break;
                }
                
                var cellColor = gridCellColors[x, y, layer];
                
                if (firstColor == null)
                {
                    firstColor = cellColor;
                }
                else if (cellColor != firstColor)
                {
                    isComplete = false;
                    break;
                }
                
                rowPositions.Add(new Vector2Int(x, y));
            }
            
            if (isComplete && rowPositions.Count == GridWidth)
            {
                return rowPositions;
            }
        }
        
        for (int x = 0; x < GridWidth; x++)
        {
            GameManager.BrickColor? firstColor = null;
            List<Vector2Int> colPositions = new List<Vector2Int>();
            bool isComplete = true;
            
            for (int y = 0; y < GridHeight; y++)
            {
                if (gridCells[x, y, layer] == null)
                {
                    isComplete = false;
                    break;
                }
                
                var cellColor = gridCellColors[x, y, layer];
                
                if (firstColor == null)
                {
                    firstColor = cellColor;
                }
                else if (cellColor != firstColor)
                {
                    isComplete = false;
                    break;
                }
                
                colPositions.Add(new Vector2Int(x, y));
            }
            
            if (isComplete && colPositions.Count == GridHeight)
            {
                return colPositions;
            }
        }
        
        return new List<Vector2Int>();
    }
    
    public GameObject GetBrickAt(int x, int y, int layer)
    {
        if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight && layer >= 0 && layer < 100)
        {
            return gridCells[x, y, layer];
        }
        return null;
    }
    
    public GameManager.BrickColor GetBrickColorAt(int x, int y, int layer)
    {
        if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight && layer >= 0 && layer < 100)
        {
            return gridCellColors[x, y, layer];
        }
        return GameManager.BrickColor.Orange;
    }
    
    public void RemoveBrickFromGrid(GameObject brick)
    {
        if (!brickPositions.ContainsKey(brick)) return;
        
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                for (int layer = 0; layer <= currentHighestLayer; layer++)
                {
                    if (gridCells[x, y, layer] == brick)
                    {
                        gridCells[x, y, layer] = null;
                        gridCellColors[x, y, layer] = default;
                    }
                }
            }
        }
        
        brickPositions.Remove(brick);
        RecalculateHighestLayer();
    }
    
    public List<GameObject> GetBricksAboveLayer(int layer)
    {
        HashSet<GameObject> bricks = new HashSet<GameObject>();
        
        for (int l = layer + 1; l <= currentHighestLayer; l++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    var brick = gridCells[x, y, l];
                    if (brick != null)
                    {
                        bricks.Add(brick);
                    }
                }
            }
        }
        
        return new List<GameObject>(bricks);
    }
    
    public float GetRequiredHeightForBrick(GameObject brick)
    {
        if (!brickPositions.ContainsKey(brick)) return 0f;
        
        Vector3Int pos = brickPositions[brick];
        Vector2Int brickSize = GetBrickSize(brick);
        
        int maxLayer = -1;
        for (int x = pos.x; x < pos.x + brickSize.x && x < GridWidth; x++)
        {
            for (int y = pos.y; y < pos.y + brickSize.y && y < GridHeight; y++)
            {
                int topLayer = GetTopLayerAt(x, y);
                if (topLayer > maxLayer)
                    maxLayer = topLayer;
            }
        }
        
        return (maxLayer + 1) * layerHeight;
    }
    
    public void UpdateBrickPosition(GameObject brick)
    {
        if (!brickPositions.ContainsKey(brick)) return;
        
        Vector3Int oldPos = brickPositions[brick];
        Vector2Int brickSize = GetBrickSize(brick);
        
        for (int x = oldPos.x; x < oldPos.x + brickSize.x && x < GridWidth; x++)
        {
            for (int y = oldPos.y; y < oldPos.y + brickSize.y && y < GridHeight; y++)
            {
                if (gridCells[x, y, oldPos.z] == brick)
                {
                    gridCells[x, y, oldPos.z] = null;
                    gridCellColors[x, y, oldPos.z] = default;
                }
            }
        }
        
        int newLayer = -1;
        for (int x = oldPos.x; x < oldPos.x + brickSize.x && x < GridWidth; x++)
        {
            for (int y = oldPos.y; y < oldPos.y + brickSize.y && y < GridHeight; y++)
            {
                int topLayer = GetTopLayerAt(x, y);
                if (topLayer > newLayer)
                    newLayer = topLayer;
            }
        }
        newLayer++;
        
        var color = GetBrickColor(brick);
        for (int x = oldPos.x; x < oldPos.x + brickSize.x && x < GridWidth; x++)
        {
            for (int y = oldPos.y; y < oldPos.y + brickSize.y && y < GridHeight; y++)
            {
                gridCells[x, y, newLayer] = brick;
                gridCellColors[x, y, newLayer] = color;
            }
        }
        
        brickPositions[brick] = new Vector3Int(oldPos.x, oldPos.y, newLayer);
        RecalculateHighestLayer();
    }
    
    Vector2Int GetBrickSize(GameObject brick)
    {
        var parts = brick.name.Split('_');
        if (parts.Length > 0)
        {
            var sizeParts = parts[0].Replace("Brick", "").Split('x');
            if (sizeParts.Length == 2)
            {
                int w, h;
                if (int.TryParse(sizeParts[0], out w) && int.TryParse(sizeParts[1], out h))
                {
                    return new Vector2Int(w, h);
                }
            }
        }
        
        return new Vector2Int(1, 1);
    }
    
    void RecalculateHighestLayer()
    {
        currentHighestLayer = 0;
        for (int layer = 99; layer >= 0; layer--)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    if (gridCells[x, y, layer] != null)
                    {
                        currentHighestLayer = layer;
                        return;
                    }
                }
            }
        }
    }

    private GameObject GetPlateBrick(int w, int h) => 
        plateBrickPrefabs.Find(p => p.name.Contains($"{w}x{h}"));

    private GameManager.BrickColor GetRandomPlateColor()
    {
        var gameManager = FindObjectOfType<GameManager>();
        if (!gameManager) return GameManager.BrickColor.Gray;
        
        var colors = gameManager.GetCurrentLevelColors();
        return colors.Count > 0 
            ? colors[Random.Range(0, colors.Count)]
            : GameManager.BrickColor.Gray;
    }

    private bool IsAreaEmpty(bool[,] covered, int x, int y, int w, int h)
    {
        for (int i = x; i < x + w; i++)
        for (int j = y; j < y + h; j++)
            if (i >= covered.GetLength(0) || j >= covered.GetLength(1) || covered[i, j])
                return false;
        return true;
    }

    private void MarkArea(bool[,] covered, int x, int y, int w, int h, bool value)
    {
        for (int i = x; i < x + w; i++)
        for (int j = y; j < y + h; j++)
            if (i < covered.GetLength(0) && j < covered.GetLength(1))
                covered[i, j] = value;
    }

    private int GetTopLayerAt(int x, int y)
    {
        for (int layer = currentHighestLayer; layer >= 0; layer--)
            if (gridCells[x, y, layer] != null)
                return layer;
        return -1;
    }

    public List<GameObject> GetBricksInLayer(int layer)
    {
        var bricks = new List<GameObject>();
        
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                GameObject brick = gridCells[x, y, layer];
                if (brick != null && !bricks.Contains(brick))
                    bricks.Add(brick);
            }
        }
        
        return bricks;
    }

    private void ClearGridPlates()
    {
        gridPlateBricks.ForEach(Destroy);
        gridPlateBricks.Clear();
    }

    public int GetHighestLayer() => currentHighestLayer;
    
    public int GetLayerAtPosition(Vector2Int gridPos, GameObject brick)
    {
        for (int layer = 0; layer <= currentHighestLayer; layer++)
            if (gridCells[gridPos.x, gridPos.y, layer] == brick)
                return layer;
        return -1;
    }
    
    public int GetLayerAtBrickPosition(GameObject brick, Vector2Int gridPos)
    {
        if (brickPositions.ContainsKey(brick))
        {
            return brickPositions[brick].z;
        }
        
        for (int layer = 0; layer <= currentHighestLayer; layer++)
        {
            if (gridCells[gridPos.x, gridPos.y, layer] == brick)
                return layer;
        }
        return -1;
    }
    
    public Vector2Int GetBrickGridPosition(GameObject brick)
    {
        if (brickPositions.ContainsKey(brick))
        {
            var pos = brickPositions[brick];
            return new Vector2Int(pos.x, pos.y);
        }
        
        for (int layer = 0; layer <= currentHighestLayer; layer++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    if (gridCells[x, y, layer] == brick)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }
        
        return Vector2Int.zero;
    }
    
    public GameManager.BrickColor GetBrickColor(GameObject brick)
    {
        for (int x = 0; x < GridWidth; x++)
        for (int y = 0; y < GridHeight; y++)
        for (int layer = 0; layer <= currentHighestLayer; layer++)
            if (gridCells[x, y, layer] == brick)
                return gridCellColors[x, y, layer];
        return GameManager.BrickColor.Orange;
    }

    public void SetGridSize(int width, int height)
    {
        gridSize = new Vector2(width, height);
        InitializeGrid();
    }

    public void SetColorPlateMode(bool colorMode)
    {
        isColorPlate = colorMode;
        ApplyPlateColors();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        for (int x = 0; x <= GridWidth; x++)
            Gizmos.DrawLine(new Vector3(x * cellSize, 0, 0), new Vector3(x * cellSize, 0, GridHeight * cellSize));
        for (int y = 0; y <= GridHeight; y++)
            Gizmos.DrawLine(new Vector3(0, 0, y * cellSize), new Vector3(GridWidth * cellSize, 0, y * cellSize));
    }
}

[System.Serializable]
public class PlatePlacement
{
    public GameObject prefab;
    public int gridX, gridY, width, height;
    
    public PlatePlacement(GameObject prefab, int x, int y, int w, int h)
    {
        this.prefab = prefab;
        gridX = x; gridY = y; width = w; height = h;
    }
}
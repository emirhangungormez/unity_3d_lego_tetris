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
    private int currentHighestLayer = 0;

    public int GridWidth => (int)gridSize.x;
    public int GridHeight => (int)gridSize.y;
    public int TotalCells => GridWidth * GridHeight;

    void Start()
    {
        InitializeGrid();
        CreateGridPlates();
    }

    #region Initialization
    private void InitializeGrid()
    {
        gridCells = new GameObject[GridWidth, GridHeight, 100];
        gridCellColors = new GameManager.BrickColor[GridWidth, GridHeight, 100];
        gridPlateColors = new GameManager.BrickColor[GridWidth, GridHeight];
    }
    #endregion

    #region Grid Plate Management
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

        // Fill gaps with 1x1 plates
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

        // Set colors for covered area
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
    #endregion

    #region Core Grid Operations
    public bool IsValidPosition(Vector2Int pos, Vector2Int size) =>
        pos.x >= 0 && pos.y >= 0 && pos.x + size.x <= GridWidth && pos.y + size.y <= GridHeight;
    
    public Vector3 GetGridPosition(Vector2Int gridPos, Vector2Int size) => new Vector3(
        gridPos.x * cellSize + (size.x * cellSize * 0.5f), 0,
        gridPos.y * cellSize + (size.y * cellSize * 0.5f)
    );
    
    public float GetRequiredHeight(Vector2Int gridPos, Vector2Int size)
    {
        int maxHeight = 0;
        ForEachCellInArea(gridPos, size, (x, y) => 
            maxHeight = Mathf.Max(maxHeight, GetTopLayerAt(x, y) + 1)
        );
        return maxHeight * layerHeight;
    }

    public void PlaceBrick(Vector2Int gridPos, Vector2Int size, GameObject brick, GameManager.BrickColor color)
    {
        int targetLayer = Mathf.RoundToInt(GetRequiredHeight(gridPos, size) / layerHeight);
        ForEachCellInArea(gridPos, size, (x, y) => 
        {
            gridCells[x, y, targetLayer] = brick;
            gridCellColors[x, y, targetLayer] = color;
        });
        currentHighestLayer = Mathf.Max(currentHighestLayer, targetLayer);
    }

    public GameManager.BrickColor? CheckCompletedLayerWithColor(int layer)
    {
        if (!IsLayerComplete(layer)) return null;
        
        var firstColor = GetColorAt(0, 0, layer);
        for (int x = 0; x < GridWidth; x++)
        for (int y = 0; y < GridHeight; y++)
            if (GetColorAt(x, y, layer) != firstColor)
                return null;
        
        return firstColor;
    }

    public void RemoveLayer(int layer)
    {
        ClearLayer(layer);
        ShiftLayersDown(layer);
        currentHighestLayer = Mathf.Max(0, currentHighestLayer - 1);
    }
    #endregion

    #region Utility Methods
    private GameObject GetPlateBrick(int w, int h) => 
        plateBrickPrefabs.Find(p => p.name.Contains($"{w}x{h}"));

    private GameManager.BrickColor GetRandomPlateColor()
    {
        var gameManager = FindObjectOfType<GameManager>();
        return gameManager?.levelColors.Count > 0 
            ? gameManager.levelColors[Random.Range(0, gameManager.levelColors.Count)]
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

    private void ForEachCellInArea(Vector2Int start, Vector2Int size, System.Action<int, int> action)
    {
        for (int x = start.x; x < start.x + size.x; x++)
        for (int y = start.y; y < start.y + size.y; y++)
            if (x < GridWidth && y < GridHeight)
                action(x, y);
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
            {
                bricks.Add(brick);
            }
        }
    }
    
    return bricks;
}

    private bool IsLayerComplete(int layer)
    {
        for (int x = 0; x < GridWidth; x++)
        for (int y = 0; y < GridHeight; y++)
            if (gridCells[x, y, layer] == null)
                return false;
        return true;
    }

    private void ClearLayer(int layer) => 
        ForEachCell((x, y) => { gridCells[x, y, layer] = null; gridCellColors[x, y, layer] = default; });

    private void ShiftLayersDown(int startLayer)
    {
        for (int layer = startLayer + 1; layer <= currentHighestLayer; layer++)
            ForEachCell((x, y) => 
            {
                gridCells[x, y, layer - 1] = gridCells[x, y, layer];
                gridCellColors[x, y, layer - 1] = gridCellColors[x, y, layer];
                gridCells[x, y, layer] = null;
                gridCellColors[x, y, layer] = default;
            });
    }

    private void ForEachCell(System.Action<int, int> action)
    {
        for (int x = 0; x < GridWidth; x++)
        for (int y = 0; y < GridHeight; y++)
            action(x, y);
    }

    private GameManager.BrickColor GetColorAt(int x, int y, int layer) => gridCellColors[x, y, layer];

    private void ClearGridPlates()
    {
        gridPlateBricks.ForEach(Destroy);
        gridPlateBricks.Clear();
    }
    #endregion

    #region Public API
    public int GetHighestLayer() => currentHighestLayer;
    
    public int GetLayerAtPosition(Vector2Int gridPos, GameObject brick)
    {
        for (int layer = 0; layer <= currentHighestLayer; layer++)
            if (gridCells[gridPos.x, gridPos.y, layer] == brick)
                return layer;
        return -1;
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
        gridPlateColors = new GameManager.BrickColor[width, height];
        CreateGridPlates();
    }

    public void SetColorPlateMode(bool colorMode)
    {
        isColorPlate = colorMode;
        ApplyPlateColors();
    }
    #endregion

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
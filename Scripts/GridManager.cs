using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public Vector2 gridSize = new Vector2(8, 8);
    public float cellSize = 1f;
    public float layerHeight = 1.3f;
    
    // YENİ: Her hücrenin hangi brick tarafından doldurulduğunu ve rengini tut
    private GameObject[,,] gridCells; // [x, y, layer]
    private GameManager.BrickColor[,,] gridCellColors; // [x, y, layer] - YENİ: Renk bilgisi
    private int currentHighestLayer = 0;
    
    void Start()
    {
        gridCells = new GameObject[(int)gridSize.x, (int)gridSize.y, 100]; // Max 100 layer
        gridCellColors = new GameManager.BrickColor[(int)gridSize.x, (int)gridSize.y, 100]; // YENİ: Renk array'i
    }
    
    public bool IsValidPosition(Vector2Int position, Vector2Int size)
    {
        if (position.x < 0 || position.y < 0) return false;
        if (position.x + size.x > gridSize.x || position.y + size.y > gridSize.y) return false;
        return true;
    }
    
    public Vector3 GetGridPosition(Vector2Int gridPos, Vector2Int size)
    {
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
        
        // Brick'in yerleşeceği alandaki en yüksek layer'ı bul
        for(int x = gridPos.x; x < gridPos.x + size.x; x++)
        {
            for(int y = gridPos.y; y < gridPos.y + size.y; y++)
            {
                if(x < gridSize.x && y < gridSize.y)
                {
                    // Bu hücredeki en üst brick'i bul
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
    
    // YENİ: Renk bilgisi ile brick yerleştirme
    public void PlaceBrick(Vector2Int gridPos, Vector2Int size, GameObject brick, GameManager.BrickColor color)
    {
        int targetLayer = Mathf.RoundToInt(GetRequiredHeight(gridPos, size) / layerHeight);
        
        // Brick'i grid'e yerleştir ve rengini kaydet
        for(int x = gridPos.x; x < gridPos.x + size.x; x++)
        {
            for(int y = gridPos.y; y < gridPos.y + size.y; y++)
            {
                if(x < gridSize.x && y < gridSize.y)
                {
                    gridCells[x, y, targetLayer] = brick;
                    gridCellColors[x, y, targetLayer] = color; // YENİ: Renk kaydı
                }
            }
        }
        
        // En yüksek layer'ı güncelle
        currentHighestLayer = Mathf.Max(currentHighestLayer, targetLayer);
        
        Debug.Log($"Brick {gridPos} pozisyonuna layer {targetLayer}'a yerleştirildi - Renk: {color}");
    }
    
    public List<Vector2Int> CheckCompletedLayer(int layer)
    {
        // Bu layer'daki TÜM hücreler dolu mu?
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                if(gridCells[x, y, layer] == null)
                {
                    // Boş hücre bulundu, katman tamamlanmamış
                    return null;
                }
            }
        }
        
        // Tüm hücreler dolu, pozisyon listesini oluştur
        List<Vector2Int> completedPositions = new List<Vector2Int>();
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                completedPositions.Add(new Vector2Int(x, y));
            }
        }
        
        Debug.Log($"✅ Katman {layer} TAMAMEN DOLU! {completedPositions.Count} hücre");
        return completedPositions;
    }
    
    // YENİ: Hem doluluk hem renk kontrolü
    public GameManager.BrickColor? CheckCompletedLayerWithColor(int layer)
    {
        // Önce layer'ın tamamen dolu olup olmadığını kontrol et
        List<Vector2Int> completedPositions = CheckCompletedLayer(layer);
        if (completedPositions == null)
        {
            // Debug.Log($"❌ Katman {layer} tam dolu değil");
            return null;
        }
        
        // Layer doluysa, tüm brick'lerin aynı renkte olup olmadığını kontrol et
        GameManager.BrickColor? firstColor = null;
        bool allSameColor = true;
        
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                if(gridCells[x, y, layer] != null)
                {
                    GameManager.BrickColor currentColor = gridCellColors[x, y, layer];
                    
                    if(firstColor == null)
                    {
                        firstColor = currentColor;
                        // Debug.Log($"🎨 Katman {layer} ilk renk belirlendi: {firstColor}");
                    }
                    else if(firstColor.Value != currentColor)
                    {
                        // Farklı renk bulundu
                        // Debug.Log($"🎨 Katman {layer} farklı renk bulundu: {firstColor} != {currentColor} (X:{x}, Y:{y})");
                        allSameColor = false;
                        break;
                    }
                }
            }
            if (!allSameColor) break;
        }
        
        if (allSameColor && firstColor.HasValue)
        {
            Debug.Log($"🎉 Katman {layer} hem dolu hem aynı renk: {firstColor.Value}");
            return firstColor.Value;
        }
        else
        {
            Debug.Log($"❌ Katman {layer} dolu ama farklı renkler var");
            return null;
        }
    }
    
    public void RemoveLayer(int layer)
    {
        Debug.Log($"🗑️ Katman {layer} siliniyor...");
        
        // Bu layer'daki tüm brick'leri temizle
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                gridCells[x, y, layer] = null;
                gridCellColors[x, y, layer] = default(GameManager.BrickColor); // YENİ: Renk bilgisini de temizle
            }
        }
        
        // Üstteki layer'ları aşağı kaydır
        for(int l = layer + 1; l <= currentHighestLayer; l++)
        {
            for(int x = 0; x < gridSize.x; x++)
            {
                for(int y = 0; y < gridSize.y; y++)
                {
                    gridCells[x, y, l - 1] = gridCells[x, y, l];
                    gridCellColors[x, y, l - 1] = gridCellColors[x, y, l]; // YENİ: Renk bilgisini de kaydır
                    gridCells[x, y, l] = null;
                    gridCellColors[x, y, l] = default(GameManager.BrickColor);
                }
            }
        }
        
        currentHighestLayer = Mathf.Max(0, currentHighestLayer - 1);
    }
    
    public int GetHighestLayer()
    {
        return currentHighestLayer;
    }
    
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
    
    // YENİ: Brick'in rengini al (EffectManager için)
    public GameManager.BrickColor GetBrickColor(GameObject brick)
    {
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                for(int layer = 0; layer <= currentHighestLayer; layer++)
                {
                    if(gridCells[x, y, layer] == brick)
                    {
                        return gridCellColors[x, y, layer];
                    }
                }
            }
        }
        return GameManager.BrickColor.Orange; // Fallback
    }
    
    // YENİ: Belirli bir pozisyondaki brick rengini al
    public GameManager.BrickColor GetColorAtPosition(Vector2Int gridPos, int layer)
    {
        if (gridPos.x >= 0 && gridPos.x < gridSize.x && 
            gridPos.y >= 0 && gridPos.y < gridSize.y && 
            layer >= 0 && layer <= currentHighestLayer)
        {
            return gridCellColors[gridPos.x, gridPos.y, layer];
        }
        return default(GameManager.BrickColor);
    }
    
    public void PrintGridStatus()
    {
        string status = "=== GRID DURUMU ===\n";
        
        for(int layer = 0; layer <= currentHighestLayer; layer++)
        {
            int filledCells = 0;
            Dictionary<GameManager.BrickColor, int> colorDistribution = new Dictionary<GameManager.BrickColor, int>();
            
            for(int x = 0; x < gridSize.x; x++)
            {
                for(int y = 0; y < gridSize.y; y++)
                {
                    if(gridCells[x, y, layer] != null)
                    {
                        filledCells++;
                        
                        // Renk dağılımını hesapla
                        GameManager.BrickColor color = gridCellColors[x, y, layer];
                        if (colorDistribution.ContainsKey(color))
                            colorDistribution[color]++;
                        else
                            colorDistribution[color] = 1;
                    }
                }
            }
            
            status += $"Layer {layer}: {filledCells}/64 dolu | ";
            
            // Renk dağılımını ekle
            foreach (var kvp in colorDistribution)
            {
                status += $"{kvp.Key}:{kvp.Value} ";
            }
            status += "\n";
        }
        
        Debug.Log(status);
    }
    
    // YENİ: Debug için renk bilgilerini göster
    public void PrintColorInfo(int layer)
    {
        if (layer < 0 || layer > currentHighestLayer)
        {
            Debug.Log($"❌ Layer {layer} geçersiz");
            return;
        }
        
        string colorInfo = $"🎨 Layer {layer} Renk Bilgisi:\n";
        Dictionary<GameManager.BrickColor, int> colorCount = new Dictionary<GameManager.BrickColor, int>();
        
        for(int x = 0; x < gridSize.x; x++)
        {
            for(int y = 0; y < gridSize.y; y++)
            {
                if(gridCells[x, y, layer] != null)
                {
                    GameManager.BrickColor color = gridCellColors[x, y, layer];
                    if (colorCount.ContainsKey(color))
                        colorCount[color]++;
                    else
                        colorCount[color] = 1;
                }
            }
        }
        
        foreach (var kvp in colorCount)
        {
            colorInfo += $"   {kvp.Key}: {kvp.Value} brick\n";
        }
        
        Debug.Log(colorInfo);
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
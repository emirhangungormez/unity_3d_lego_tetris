using UnityEngine;
using System.Collections.Generic;

public class EffectManager : MonoBehaviour
{
    [Header("Layer Clear Effects")]
    public float layerClearDelay = 0.3f;
    public float brickFallDuration = 0.6f;
    public float brickRemoveDuration = 0.3f;
    
    [Header("Visual Effects")]
    public ParticleSystem clearEffectPrefab;
    public AudioClip clearSound;
    
    private GameManager gameManager;
    private GridManager gridManager;
    
    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        gridManager = FindObjectOfType<GridManager>();
    }
    
    public void ClearLayerWithEffects(int layer)
    {
        Debug.Log($"🎬 Layer {layer} temizleme efektleri başlatılıyor...");
        
        // 1. Önce üst katmanları aşağı kaydır
        StartCoroutine(MoveBricksDownCoroutine(layer));
        
        // 2. Sonra bu layer'daki brick'leri efektle sil
        StartCoroutine(RemoveLayerWithEffectsCoroutine(layer, brickFallDuration + layerClearDelay));
    }
    
    private System.Collections.IEnumerator MoveBricksDownCoroutine(int clearedLayer)
    {
        yield return new WaitForSeconds(layerClearDelay);
        
        List<GameObject> bricksToMove = new List<GameObject>();
        
        // Üstteki brick'leri bul
        foreach(GameObject brick in gameManager.landedBricks)
        {
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            
            if(brickLayer > clearedLayer)
            {
                bricksToMove.Add(brick);
            }
        }
        
        // Brick'leri aşağı kaydır
        foreach(GameObject brick in bricksToMove)
        {
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            float newY = (brickLayer - 1) * gridManager.layerHeight;
            
            StartCoroutine(MoveBrickSmooth(brick, newY, brickFallDuration));
        }
        
        Debug.Log($"⬇️ {bricksToMove.Count} brick aşağı kaydırılıyor...");
    }
    
    private System.Collections.IEnumerator RemoveLayerWithEffectsCoroutine(int layer, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Bu layer'daki brick'leri bul
        List<GameObject> bricksToRemove = new List<GameObject>();
        
        foreach(GameObject brick in gameManager.landedBricks.ToArray())
        {
            if (brick == null) continue;
            
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            
            if(brickLayer == layer)
            {
                bricksToRemove.Add(brick);
            }
        }
        
        // Brick'leri efektle sil
        foreach(GameObject brick in bricksToRemove)
        {
            StartCoroutine(RemoveBrickWithEffects(brick));
            gameManager.landedBricks.Remove(brick);
        }
        
        // Grid'den layer'ı temizle
        gridManager.RemoveLayer(layer);
        
        Debug.Log($"✅ Layer {layer} tamamen temizlendi! ({bricksToRemove.Count} brick)");
    }
    
    private System.Collections.IEnumerator MoveBrickSmooth(GameObject brick, float targetY, float duration)
    {
        Vector3 startPos = brick.transform.position;
        Vector3 endPos = new Vector3(startPos.x, targetY, startPos.z);
        float elapsed = 0f;
        
        while(elapsed < duration)
        {
            float progress = elapsed / duration;
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            
            brick.transform.position = Vector3.Lerp(startPos, endPos, easedProgress);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        brick.transform.position = endPos;
    }
    
    private System.Collections.IEnumerator RemoveBrickWithEffects(GameObject brick)
    {
        // 1. Parlama efekti (partikül)
        if (clearEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(clearEffectPrefab, brick.transform.position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 2f);
        }
        
        // 2. Ses efekti
        if (clearSound != null)
        {
            AudioSource.PlayClipAtPoint(clearSound, brick.transform.position);
        }
        
        // 3. Scale down animasyonu
        Vector3 originalScale = brick.transform.localScale;
        float elapsed = 0f;
        
        while(elapsed < brickRemoveDuration)
        {
            float progress = elapsed / brickRemoveDuration;
            brick.transform.localScale = originalScale * (1f - progress);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Destroy(brick);
    }
    
    // Hızlı efekt testi için
    public void TestEffects()
    {
        ClearLayerWithEffects(1);
    }
}
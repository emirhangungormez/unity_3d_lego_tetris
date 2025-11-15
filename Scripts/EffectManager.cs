using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public GridManager gridManager;
    
    [Header("Timing Settings")]
    public float layerClearDelay = 0.3f;
    public float brickFallDuration = 0.6f;
    
    [Header("Particle Settings")]
    public GameObject brickParticlePrefab;
    public int minParticles = 3;
    public int maxParticles = 8;
    public float particleLifetime = 2f;

    // GameManager animasyon ayarları
    private float fallSpeed = 1f;
    private float snapSpeed = 0.1f;
    private float settleOvershoot = 0.1f;
    private float settleDuration = 0.4f;

    void Start()
    {
        gameManager ??= FindObjectOfType<GameManager>();
        gridManager ??= FindObjectOfType<GridManager>();
    }

    public void ClearLayerWithEffects(int layer) => StartCoroutine(ClearLayerRoutine(layer));

    private IEnumerator ClearLayerRoutine(int layer)
    {
        yield return new WaitForSeconds(layerClearDelay);

        var bricksToRemove = gridManager.GetBricksInLayer(layer);
        var bricksToMove = GetBricksAboveLayer(layer);

        // 1. ÖNCE GRID'DEN TEMİZLE
        gridManager.RemoveLayer(layer);

        // 2. SONRA BRICK'LERİ YOK ET
        foreach (var brick in bricksToRemove)
        {
            if (brick != null)
            {
                CreateParticlesForBrick(brick);
                gameManager.landedBricks.Remove(brick);
                Destroy(brick);
            }
        }

        // 3. EN SON ÜST BRICK'LERİ KAYDIR (GAMEMANAGER ANİMASYONU İLE)
        foreach (var brick in bricksToMove)
        {
            if (brick != null)
                StartCoroutine(MoveBrickDown(brick));
        }
    }

    private List<GameObject> GetBricksAboveLayer(int layer) 
    {
        var bricks = new List<GameObject>();
        
        foreach (var brick in gameManager.landedBricks)
        {
            if (brick == null) continue;
            
            var gridPos = gameManager.GetBrickGridPosition(brick);
            if (gridManager.GetLayerAtPosition(gridPos, brick) > layer)
                bricks.Add(brick);
        }
        
        return bricks;
    }

    private IEnumerator MoveBrickDown(GameObject brick)
    {
        if (brick == null) yield break;

        var startPos = brick.transform.position;
        var gridPos = gameManager.GetBrickGridPosition(brick);
        
        // Layer hesaplama
        int currentLayer = gridManager.GetLayerAtPosition(gridPos, brick);
        int targetLayer = Mathf.Max(0, currentLayer - 1);
        float targetY = targetLayer * gridManager.layerHeight;
        
        var endPos = new Vector3(startPos.x, targetY, startPos.z);
        float distanceToTarget = Mathf.Abs(startPos.y - targetY);
        
        // GAMEMANAGER ANİMASYON SİSTEMİ
        if (distanceToTarget > 0.2f)
        {
            // FALL NORMALLY
            yield return StartCoroutine(FallBrickNormally(brick, startPos, targetY));
        }
        else
        {
            // SNAP AND SETTLE
            float overshootY = targetY + settleOvershoot;
            yield return StartCoroutine(SnapBrickToPosition(brick, startPos, overshootY));
            yield return StartCoroutine(SettleBrick(brick, overshootY, targetY));
        }

        if (brick != null)
            brick.transform.position = endPos;
    }

    // GAMEMANAGER'DAKİ FALLNORMALLY METODU
    private IEnumerator FallBrickNormally(GameObject brick, Vector3 startPos, float targetY)
    {
        Vector3 currentPos = startPos;
        float newY = currentPos.y;
        
        while (brick != null && Mathf.Abs(newY - targetY) > 0.01f)
        {
            newY = Mathf.MoveTowards(currentPos.y, targetY, fallSpeed * Time.deltaTime);
            brick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
            currentPos = brick.transform.position;
            yield return null;
        }
    }

    // GAMEMANAGER'DAKİ SNAPTOPOSITION METODU  
    private IEnumerator SnapBrickToPosition(GameObject brick, Vector3 startPos, float targetY)
    {
        Vector3 currentPos = startPos;
        float newY = currentPos.y;
        
        while (brick != null && Mathf.Abs(newY - targetY) > 0.01f)
        {
            newY = Mathf.MoveTowards(currentPos.y, targetY, snapSpeed * Time.deltaTime);
            brick.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
            currentPos = brick.transform.position;
            yield return null;
        }
    }

    // GAMEMANAGER'DAKİ SETTLEBRICK METODU
    private IEnumerator SettleBrick(GameObject brick, float overshootY, float targetY)
    {
        if (brick == null) yield break;
        
        float settleTimer = 0f;
        Vector3 currentPos = brick.transform.position;
        
        while (settleTimer < settleDuration && brick != null)
        {
            settleTimer += Time.deltaTime;
            float settleProgress = Mathf.Clamp01(settleTimer / settleDuration);
            float smoothProgress = 1f - Mathf.Pow(1f - settleProgress, 3f);
            
            float currentY = Mathf.Lerp(overshootY, targetY, smoothProgress);
            brick.transform.position = new Vector3(currentPos.x, currentY, currentPos.z);
            yield return null;
        }
    }

    private Vector2Int GetBrickSize(GameObject brick)
    {
        if (brick == null) return Vector2Int.one;
        
        // Brick'in orijinal prefab ismini bul
        string originalName = brick.name.Replace("(Clone)", "").Replace("LandedBrick_", "");
        
        var sizeParts = originalName.Split('x');
        if (sizeParts.Length == 2)
        {
            if (int.TryParse(sizeParts[0], out int x) && int.TryParse(sizeParts[1], out int z))
                return new Vector2Int(x, z);
        }
        
        var scale = brick.transform.localScale;
        return new Vector2Int(Mathf.RoundToInt(scale.x), Mathf.RoundToInt(scale.z));
    }

    private void CreateParticlesForBrick(GameObject brick)
    {
        if (brickParticlePrefab == null || brick == null) return;

        var color = GetBrickColor(brick);
        int count = Random.Range(minParticles, maxParticles + 1);

        for (int i = 0; i < count; i++)
            CreateParticle(brick.transform.position, color);
    }

    private void CreateParticle(Vector3 position, GameManager.BrickColor color)
    {
        var particle = Instantiate(brickParticlePrefab, position + Random.insideUnitSphere * 0.3f, Quaternion.identity);
        particle.transform.localScale = Vector3.one * 0.5f;

        var rb = particle.GetComponent<Rigidbody>() ?? particle.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.drag = 0.5f;
        rb.AddForce(new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 3f), Random.Range(-2f, 2f)), ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);

        ApplyParticleTexture(particle, color);
        Destroy(particle, particleLifetime);
    }

    private GameManager.BrickColor GetBrickColor(GameObject brick)
    {
        if (brick == null) return GameManager.BrickColor.Orange;

        var renderer = brick.GetComponentInChildren<Renderer>();
        if (renderer?.material == null) return GameManager.BrickColor.Orange;

        var name = renderer.material.name.ToLower();

        if (name.Contains("orange")) return GameManager.BrickColor.Orange;
        if (name.Contains("blue")) return GameManager.BrickColor.Blue;
        if (name.Contains("pink")) return GameManager.BrickColor.Pink;
        if (name.Contains("purple")) return GameManager.BrickColor.Purple;
        if (name.Contains("green")) return GameManager.BrickColor.Green;
        if (name.Contains("white")) return GameManager.BrickColor.White;
        if (name.Contains("gray")) return GameManager.BrickColor.Gray;
        if (name.Contains("brown")) return GameManager.BrickColor.Brown;
        if (name.Contains("black")) return GameManager.BrickColor.Black;

        return GameManager.BrickColor.Orange;
    }

    private void ApplyParticleTexture(GameObject particle, GameManager.BrickColor color)
    {
        if (particle == null || gameManager == null) return;

        var colorSettings = gameManager.availableColors.Find(c => c.colorType == color);
        if (colorSettings == null) return;

        foreach (var renderer in particle.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;

            var mat = new Material(renderer.material);
            mat.mainTextureScale = colorSettings.tiling;
            mat.mainTextureOffset = colorSettings.offset;
            renderer.material = mat;
        }
    }
}
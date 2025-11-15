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

    void Start()
    {
        // Sadece inspector'dan atanmamışsa bul
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
    }

    public void ClearLayerWithEffects(int layer)
    {
        StartCoroutine(ClearLayerRoutine(layer));
    }

    private IEnumerator ClearLayerRoutine(int layer)
    {
        yield return new WaitForSeconds(layerClearDelay);

        // Brick'leri topla (orijinal metod)
        var bricksToRemove = gridManager.GetBricksInLayer(layer);
        var bricksToMove = GetBricksAboveLayer(layer);

        // Grid'den temizle
        gridManager.RemoveLayer(layer);

        // Brick'leri yok et ve partikül oluştur
        foreach (var brick in bricksToRemove)
        {
            if (brick != null)
            {
                CreateParticlesForBrick(brick);
                gameManager.landedBricks.Remove(brick);
                Destroy(brick);
            }
        }

        // Üst brick'leri kaydır
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
            var brickLayer = gridManager.GetLayerAtPosition(gridPos, brick);
            
            if (brickLayer > layer)
                bricks.Add(brick);
        }
        
        return bricks;
    }

    private IEnumerator MoveBrickDown(GameObject brick)
    {
        if (brick == null) yield break;

        var startPos = brick.transform.position;
        var gridPos = gameManager.GetBrickGridPosition(brick);
        var brickLayer = gridManager.GetLayerAtPosition(gridPos, brick);
        var endPos = new Vector3(startPos.x, (brickLayer - 1) * gridManager.layerHeight, startPos.z);

        var elapsed = 0f;
        while (elapsed < brickFallDuration && brick != null)
        {
            brick.transform.position = Vector3.Lerp(startPos, endPos, elapsed / brickFallDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (brick != null)
            brick.transform.position = endPos;
    }

    private void CreateParticlesForBrick(GameObject brick)
    {
        if (brickParticlePrefab == null || brick == null) return;

        var particleCount = Random.Range(minParticles, maxParticles + 1);
        var brickColor = GetBrickColor(brick);

        for (int i = 0; i < particleCount; i++)
        {
            CreateParticle(brick.transform.position, brickColor);
        }
    }

    private void CreateParticle(Vector3 position, GameManager.BrickColor color)
    {
        var particle = Instantiate(brickParticlePrefab);
        particle.transform.position = position + Random.insideUnitSphere * 0.3f;
        particle.transform.localScale = Vector3.one * 0.5f;

        var rb = particle.GetComponent<Rigidbody>();
        if (rb == null)
            rb = particle.AddComponent<Rigidbody>();

        rb.mass = 0.5f;
        rb.drag = 0.5f;

        var force = new Vector3(
            Random.Range(-2f, 2f),
            Random.Range(1f, 3f),
            Random.Range(-2f, 2f)
        );
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);

        ApplyParticleTexture(particle, color);
        Destroy(particle, particleLifetime);
    }

    private GameManager.BrickColor GetBrickColor(GameObject brick)
    {
        if (brick == null) return GameManager.BrickColor.Orange;

        var renderer = brick.GetComponentInChildren<Renderer>();
        if (renderer == null || renderer.material == null)
            return GameManager.BrickColor.Orange;

        var materialName = renderer.material.name.ToLower();

        if (materialName.Contains("orange")) return GameManager.BrickColor.Orange;
        if (materialName.Contains("blue")) return GameManager.BrickColor.Blue;
        if (materialName.Contains("pink")) return GameManager.BrickColor.Pink;
        if (materialName.Contains("purple")) return GameManager.BrickColor.Purple;
        if (materialName.Contains("green")) return GameManager.BrickColor.Green;
        if (materialName.Contains("white")) return GameManager.BrickColor.White;
        if (materialName.Contains("gray")) return GameManager.BrickColor.Gray;
        if (materialName.Contains("brown")) return GameManager.BrickColor.Brown;
        if (materialName.Contains("black")) return GameManager.BrickColor.Black;

        return GameManager.BrickColor.Orange;
    }

    private void ApplyParticleTexture(GameObject particle, GameManager.BrickColor color)
    {
        if (particle == null || gameManager == null) return;

        var colorSettings = gameManager.availableColors.Find(c => c.colorType == color);
        if (colorSettings == null) return;

        var renderers = particle.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var material = new Material(renderer.material);
            material.mainTextureScale = colorSettings.tiling;
            material.mainTextureOffset = colorSettings.offset;
            renderer.material = material;
        }
    }
}
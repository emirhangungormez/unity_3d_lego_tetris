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
        gameManager ??= FindObjectOfType<GameManager>();
        gridManager ??= FindObjectOfType<GridManager>();
    }

    public void ClearLayerWithEffects(int layer) => StartCoroutine(ClearLayerRoutine(layer));

    private IEnumerator ClearLayerRoutine(int layer)
    {
        yield return new WaitForSeconds(layerClearDelay);

        var bricksToRemove = gridManager.GetBricksInLayer(layer);
        var bricksToMove = new List<GameObject>();
        
        foreach (var brick in gameManager.landedBricks)
        {
            if (brick == null) continue;
            
            var gridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(gridPos, brick);
            
            if (brickLayer > layer)
                bricksToMove.Add(brick);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayLayerCompleteExplosion();

        foreach (var brick in bricksToRemove)
        {
            if (brick != null)
            {
                CreateParticlesForBrick(brick);
                gameManager.landedBricks.Remove(brick);
                Destroy(brick);
            }
        }

        gridManager.RemoveLayer(layer);

        foreach (var brick in bricksToMove)
        {
            if (brick != null)
                StartCoroutine(MoveBrickDown(brick));
        }
        
        if (bricksToMove.Count > 0)
        {
            yield return new WaitForSeconds(brickFallDuration);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAllBricksSettled();
        }
    }

    private IEnumerator MoveBrickDown(GameObject brick)
    {
        if (brick == null) yield break;

        var startPos = brick.transform.position;
        float targetY = startPos.y - gridManager.layerHeight;
        var endPos = new Vector3(startPos.x, targetY, startPos.z);

        float elapsed = 0f;
        while (elapsed < brickFallDuration && brick != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / brickFallDuration;
            brick.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        if (brick != null)
            brick.transform.position = endPos;
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
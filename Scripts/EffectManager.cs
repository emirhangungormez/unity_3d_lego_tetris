using UnityEngine;
using System.Collections.Generic;

public class EffectManager : MonoBehaviour
{
    [Header("Particle Settings")]
    public GameObject brickParticlePrefab;
    public int minParticles = 3;
    public int maxParticles = 8;
    
    [Header("Particle Physics")]
    public float particleMass = 0.5f;
    public float particleDrag = 0.5f;
    public float minForceX = -2f;
    public float maxForceX = 2f;
    public float minForceY = 1f;
    public float maxForceY = 3f;
    public float minForceZ = -2f;
    public float maxForceZ = 2f;
    public float torqueForce = 2f;

    public void CreateParticlesForBrick(GameObject brick, GameManager.BrickColor color, Vector3 position, GameManager gameManager)
    {
        if (brickParticlePrefab == null || brick == null) return;

        int count = Random.Range(minParticles, maxParticles + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 particlePos = position + Random.insideUnitSphere * 0.3f;
            CreateParticle(particlePos, color, gameManager);
        }
    }

    private void CreateParticle(Vector3 position, GameManager.BrickColor color, GameManager gameManager)
    {
        var particle = Instantiate(brickParticlePrefab, position, Quaternion.identity);
        particle.transform.localScale = Vector3.one * 0.5f;

        var rb = particle.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = particle.AddComponent<Rigidbody>();
        }
        
        rb.mass = particleMass;
        rb.drag = particleDrag;
        
        Vector3 force = new Vector3(
            Random.Range(minForceX, maxForceX),
            Random.Range(minForceY, maxForceY),
            Random.Range(minForceZ, maxForceZ)
        );
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

        ApplyParticleTexture(particle, color, gameManager);
        
        if (gameManager != null)
        {
            gameManager.OnBrickDestroyed(color, position);
        }
    }

    private void ApplyParticleTexture(GameObject particle, GameManager.BrickColor color, GameManager gameManager)
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
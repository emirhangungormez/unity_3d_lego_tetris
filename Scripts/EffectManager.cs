using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EffectManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public LevelManager levelManager;
    
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
    
    [Header("UI Particle Settings")]
    public float particleGroundTime = 0.5f;
    public float uiParticleDuration = 0.8f;
    public float uiParticleScale = 0.3f;
    
    private Queue<GameObject> particlePool = new();
    private const int PoolSize = 50;

    void Start()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (levelManager == null) levelManager = GetComponent<LevelManager>();
        
        // Pre-populate particle pool
        for (int i = 0; i < PoolSize; i++)
        {
            var p = Instantiate(brickParticlePrefab);
            p.SetActive(false);
            particlePool.Enqueue(p);
        }
    }
    
    GameObject GetPooledParticle()
    {
        return particlePool.Count > 0 ? particlePool.Dequeue() : Instantiate(brickParticlePrefab);
    }
    
    void ReturnParticleToPool(GameObject particle)
    {
        particle.SetActive(false);
        if (particlePool.Count < PoolSize) particlePool.Enqueue(particle);
        else Destroy(particle);
    }

    public void CreateParticlesForBrick(GameObject brick, LevelManager.BrickColor color, Vector3 position)
    {
        if (brickParticlePrefab == null || brick == null) return;

        int count = Random.Range(minParticles, maxParticles + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 particlePos = position + Random.insideUnitSphere * 0.3f;
            CreateParticle(particlePos, color);
        }
        
        levelManager.OnBrickDestroyed(color);
    }

    private void CreateParticle(Vector3 position, LevelManager.BrickColor color)
    {
        var particle = GetPooledParticle();
        particle.SetActive(true);
        particle.transform.position = position;
        particle.transform.localScale = Vector3.one * 0.5f;

        var rb = particle.GetComponent<Rigidbody>();
        if (rb == null) rb = particle.AddComponent<Rigidbody>();
        
        rb.mass = particleMass;
        rb.drag = particleDrag;
        
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(new Vector3(Random.Range(minForceX, maxForceX), Random.Range(minForceY, maxForceY), Random.Range(minForceZ, maxForceZ)), ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

        ApplyParticleTexture(particle, color);
        StartCoroutine(ReturnParticleCoroutine(particle, 3f));
    }
    
    private IEnumerator ReturnParticleCoroutine(GameObject particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnParticleToPool(particle);
    }

    private void ApplyParticleTexture(GameObject particle, LevelManager.BrickColor color)
    {
        if (particle == null || levelManager == null) return;
        levelManager.ApplyBrickTexture(particle, color);
    }
    
    public IEnumerator SendParticleToUI(LevelManager.BrickColor color, Vector3 worldPosition, GameObject uiElement)
    {
        if (uiElement == null) yield break;
        
        GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        particle.name = "UIParticle";
        particle.transform.localScale = Vector3.one * uiParticleScale;
        particle.transform.position = worldPosition;
        
        levelManager.ApplyBrickTexture(particle, color);
        
        Rigidbody rb = particle.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.drag = 0.5f;
        
        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
        rb.AddForce(randomDir * 3f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        
        yield return new WaitForSeconds(particleGroundTime);
        
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        Camera cam = Camera.main;
        if (cam == null) yield break;
        
        float elapsed = 0f;
        Vector3 startPos = particle.transform.position;
        
        while (elapsed < uiParticleDuration && particle != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / uiParticleDuration;
            t = t * t * (3f - 2f * t);
            
            Vector3 screenTargetPos = cam.WorldToScreenPoint(uiElement.transform.position);
            Vector3 worldTargetPos = cam.ScreenToWorldPoint(new Vector3(screenTargetPos.x, screenTargetPos.y, cam.WorldToScreenPoint(startPos).z));
            
            particle.transform.position = Vector3.Lerp(startPos, worldTargetPos, t);
            particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
            
            yield return null;
        }
        
        if (particle != null)
        {
            Destroy(particle);
        }
    }
}
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
    public int minParticles = 6;
    public int maxParticles = 16;
    
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
    public float uiParticleScale = 1.4f;
    
    [Tooltip("Multiplier to slow down UI-flight (values >1 slow the travel)")]
    public float uiParticleSpeedMultiplier = 1.3f;

    [Tooltip("Initial spawn height offset so particles start above then drop")]
    public float spawnDropHeight = 0.6f;
    
    [Header("Explosion Particle Settings")]
    [Tooltip("Scale applied to explosion particles spawned at brick position")]
    public float explosionParticleScale = 0.8f;
    
    private Queue<GameObject> particlePool = new();
    private const int PoolSize = 100;

    void Start()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (levelManager == null) levelManager = GetComponent<LevelManager>();
        
        // Pre-populate particle pool
        for (int i = 0; i < PoolSize; i++)
        {
            var p = Instantiate(brickParticlePrefab);
            // Ensure pooled particle has required physics components for realistic scattering
            if (p.GetComponent<Rigidbody>() == null)
            {
                var rb = p.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.drag = particleDrag;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            if (p.GetComponent<Collider>() == null)
            {
                // Add a small sphere collider if prefab lacks collider
                var col = p.AddComponent<SphereCollider>();
                col.radius = 0.25f;
                col.material = null;
            }
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

    // Replaced explosion particles with UI-only particles. Use SpawnUIParticlesForBrick
    // to spawn N UI-bound particles (pooled) when a brick is destroyed.
    public void SpawnUIParticlesForBrick(LevelManager.BrickColor color, Vector3 worldPosition, GameObject uiElement)
    {
        if (brickParticlePrefab == null) return;
        int count = Random.Range(minParticles, maxParticles + 1);

        // Ensure at least one particle will notify the LevelManager on arrival
        bool notified = false;

        for (int i = 0; i < count; i++)
        {
            var particle = GetPooledParticle();
            particle.SetActive(true);
            // Spawn slightly above the brick so the particle falls first and make it larger for visibility
            particle.transform.position = worldPosition + Vector3.up * spawnDropHeight + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));
            particle.transform.localScale = Vector3.one * (uiParticleScale * 1.15f);
            ApplyParticleTexture(particle, color);

            // initial physics hop
            var rb = particle.GetComponent<Rigidbody>();
            if (rb == null) rb = particle.AddComponent<Rigidbody>();
            rb.mass = particleMass;
            rb.drag = particleDrag;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.useGravity = true;
            rb.isKinematic = false;
            // Give a slight horizontal kick so particles scatter and can collide
            rb.AddForce(new Vector3(Random.Range(minForceX, maxForceX), Random.Range(minForceY * 0.3f, minForceY), Random.Range(minForceZ, maxForceZ)), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

            // Only one particle should notify the LevelManager on arrival
            bool notify = !notified;
            if (notify) notified = true;

            StartCoroutine(SendPooledParticleToUI(particle, color, worldPosition, uiElement, notify));
        }
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

        // Use pooled brick particle prefab so visual matches and we don't create small white primitives
        GameObject particle = GetPooledParticle();
        particle.name = "UIParticle";
        particle.SetActive(true);
        particle.transform.position = worldPosition;
        particle.transform.localScale = Vector3.one * (uiParticleScale * 1.15f); // scale for visibility

        // Make physics controlled for initial hop, then disable for smooth lerp
        var rb = particle.GetComponent<Rigidbody>();
        if (rb == null) rb = particle.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.drag = 0.5f;

        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), Random.Range(1f, 2f), Random.Range(-1f, 1f));
        rb.AddForce(randomDir * 3f, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);

        ApplyParticleTexture(particle, color);

        yield return new WaitForSeconds(particleGroundTime);

        // Prepare for UI fly; stop physics and lerp to ui element world position
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        float elapsed = 0f;
        float duration = uiParticleDuration * Mathf.Max(0.01f, uiParticleSpeedMultiplier);
        while (elapsed < duration && particle != null)
        {
            elapsed += Time.deltaTime;
            float remaining = Mathf.Max(0.0001f, duration - elapsed);

            // Recalculate target each frame so particle homes to moving UI elements
            Vector3 worldTargetPos = uiElement.transform.position;

            // Move toward the current target such that if target is stable, particle arrives roughly by the end of duration
            float step = (Vector3.Distance(particle.transform.position, worldTargetPos) / remaining) * Time.deltaTime;
            step /= Mathf.Max(0.1f, uiParticleSpeedMultiplier);
            particle.transform.position = Vector3.MoveTowards(particle.transform.position, worldTargetPos, step);
            particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);

            if ((particle.transform.position - worldTargetPos).sqrMagnitude < 0.0004f)
                break;

            yield return null;
        }

        // Arrival: notify level manager to decrement the UI counter immediately
        if (levelManager != null)
            levelManager.OnBrickDestroyed(color);

        // Return to pool instead of destroying
        if (particle != null)
        {
            ReturnParticleToPool(particle);
        }
    }

    // Coroutine used by SpawnUIParticlesForBrick to drive pooled particles to UI
    private IEnumerator SendPooledParticleToUI(GameObject particle, LevelManager.BrickColor color, Vector3 startWorldPos, GameObject uiElement, bool notifyOnArrival)
    {
        if (particle == null) yield break;

        yield return new WaitForSeconds(particleGroundTime);

        var rb = particle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        float elapsed = 0f;
        float duration = uiParticleDuration * Mathf.Max(0.01f, uiParticleSpeedMultiplier);
        while (elapsed < duration && particle != null)
        {
            elapsed += Time.deltaTime;
            float remaining = Mathf.Max(0.0001f, duration - elapsed);

            Vector3 worldTargetPos = uiElement != null ? uiElement.transform.position : particle.transform.position;
            float step = (Vector3.Distance(particle.transform.position, worldTargetPos) / remaining) * Time.deltaTime;
            step /= Mathf.Max(0.1f, uiParticleSpeedMultiplier);
            particle.transform.position = Vector3.MoveTowards(particle.transform.position, worldTargetPos, step);
            particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
            if ((particle.transform.position - worldTargetPos).sqrMagnitude < 0.0004f) break;
            yield return null;
        }

        if (notifyOnArrival && levelManager != null)
            levelManager.OnBrickDestroyed(color);

        if (particle != null) ReturnParticleToPool(particle);
    }
}
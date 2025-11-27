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
    public float minForceX = -1f;
    public float maxForceX = 1f;
    public float minForceY = 0.4f;
    public float maxForceY = 1.2f;
    public float minForceZ = -1f;
    public float maxForceZ = 1f;
    public float torqueForce = 1.2f;
    
    [Header("UI Particle Settings")]
    public float particleGroundTime = 0.9f;
    public float uiParticleDuration = 0.9f;
    // Base visual scale for UI-bound particles. We'll multiply this when spawning to produce larger pieces.
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

    // Base collider radius used when creating or resizing sphere colliders for particles
    private const float baseColliderRadius = 0.25f;

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
                rb.mass = particleMass;
                rb.drag = particleDrag;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            if (p.GetComponent<Collider>() == null)
            {
                // Add a small sphere collider if prefab lacks collider
                var col = p.AddComponent<SphereCollider>();
                col.radius = baseColliderRadius;
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
        if (particle == null) return;
        // reset transform state so next spawn is clean
        particle.transform.localScale = Vector3.one;
        particle.transform.rotation = Quaternion.identity;
        var rb = particle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Ensure non-kinematic before setting velocities (setting velocity on kinematic bodies throws)
            if (rb.isKinematic) rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
        }
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

        // Register expected particle arrivals with LevelManager for THIS SPECIFIC BRICK
        // Returns a unique ID to track this brick's particles
        int brickId = -1;
        if (levelManager != null && uiElement != null)
        {
            brickId = levelManager.RegisterPendingParticlesForBrick(uiElement, color, count);
        }

        for (int i = 0; i < count; i++)
        {
            var particle = GetPooledParticle();
            particle.SetActive(true);
            // Spawn slightly above the brick so the particle falls first and make it larger for visibility
            particle.transform.position = worldPosition + Vector3.up * spawnDropHeight + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
            // Make particle visibly larger (2x) per request, scaled by configured uiParticleScale
            particle.transform.localScale = Vector3.one * (uiParticleScale * 2f);
            ApplyParticleTexture(particle, color);

            // initial physics hop
            var rb = particle.GetComponent<Rigidbody>();
            if (rb == null) rb = particle.AddComponent<Rigidbody>();
            rb.mass = particleMass;
            rb.drag = particleDrag;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.useGravity = true;
            rb.isKinematic = false;
            // Give a gentle horizontal kick so particles scatter slowly and can collide with grid
            rb.AddForce(new Vector3(Random.Range(minForceX, maxForceX), Random.Range(minForceY * 0.6f, Random.Range(minForceY, maxForceY)), Random.Range(minForceZ, maxForceZ)), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);

            // Ensure collider radius scales with transform so larger pieces collide properly with grid
            var sc = particle.GetComponent<SphereCollider>();
            if (sc != null) sc.radius = baseColliderRadius * particle.transform.localScale.x;

            // All particles will notify arrival with brick ID
            StartCoroutine(SendPooledParticleToUI(particle, color, worldPosition, uiElement, brickId));
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

        // Randomize ground hang a bit so particles don't all start flying at the same time
        yield return new WaitForSeconds(particleGroundTime * Random.Range(0.9f, 1.6f));

        // Prepare for UI fly; stop physics and lerp to ui element world position
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        float elapsed = 0f;
        // Give each particle a different travel duration so they arrive at different times (wider variance)
        float duration = uiParticleDuration * Random.Range(0.5f, 1.8f) * Mathf.Max(0.01f, uiParticleSpeedMultiplier);
        Vector3 startScale = particle.transform.localScale;
        Vector3 endScale = startScale * 0.35f;
        while (elapsed < duration && particle != null)
        {
            elapsed += Time.deltaTime;
            float remaining = Mathf.Max(0.0001f, duration - elapsed);

            // Recalculate target each frame so particle homes to moving UI elements
            Vector3 worldTargetPos = uiElement.transform.position;

            // Move toward the current target such that if target is stable, particle arrives roughly by the end of duration
            float step = (Vector3.Distance(particle.transform.position, worldTargetPos) / remaining) * Time.deltaTime;
            step /= Mathf.Max(0.1f, uiParticleSpeedMultiplier);
            // Add per-particle larger random variation to speed so arrivals vary more
            step *= Random.Range(0.7f, 1.6f);
            particle.transform.position = Vector3.MoveTowards(particle.transform.position, worldTargetPos, step);
            // shrink while flying so they visually converge to the small UI icon
            particle.transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Clamp01(elapsed / duration));
            particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);

            if ((particle.transform.position - worldTargetPos).sqrMagnitude < 0.0004f)
                break;

            yield return null;
        }

        // Arrival: notify the LevelManager directly (this method doesn't use brick tracking)
        if (levelManager != null)
        {
            levelManager.OnBrickDestroyed(color);
        }

        // Return to pool instead of destroying
        if (particle != null)
        {
            ReturnParticleToPool(particle);
        }
    }

    // Coroutine used by SpawnUIParticlesForBrick to drive pooled particles to UI
    private IEnumerator SendPooledParticleToUI(GameObject particle, LevelManager.BrickColor color, Vector3 startWorldPos, GameObject uiElement, int brickId)
    {
        if (particle == null) yield break;

        // small randomized ground delay so particles launch at different times
        yield return new WaitForSeconds(particleGroundTime * Random.Range(0.8f, 1.8f));

        var rb = particle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        float elapsed = 0f;
        // per-particle travel duration variety (wider spread so some are much faster/slower)
        float duration = uiParticleDuration * Random.Range(0.5f, 2.0f) * Mathf.Max(0.01f, uiParticleSpeedMultiplier);
        Vector3 startScale = particle.transform.localScale;
        Vector3 endScale = startScale * 0.35f;
        while (elapsed < duration && particle != null)
        {
            elapsed += Time.deltaTime;
            float remaining = Mathf.Max(0.0001f, duration - elapsed);

            Vector3 worldTargetPos = uiElement != null ? uiElement.transform.position : particle.transform.position;
            float step = (Vector3.Distance(particle.transform.position, worldTargetPos) / remaining) * Time.deltaTime;
            step /= Mathf.Max(0.1f, uiParticleSpeedMultiplier);
            // per-particle larger speed variation
            step *= Random.Range(0.7f, 1.6f);
            particle.transform.position = Vector3.MoveTowards(particle.transform.position, worldTargetPos, step);
            // shrink while flying
            particle.transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.Clamp01(elapsed / duration));
            particle.transform.Rotate(Vector3.up * Time.deltaTime * 720f);
            if ((particle.transform.position - worldTargetPos).sqrMagnitude < 0.0004f) break;
            yield return null;
        }

        // Notify LevelManager with the brick ID
        if (brickId >= 0 && levelManager != null)
        {
            levelManager.NotifyParticleArrival(brickId);
        }
        else if (levelManager != null)
        {
            levelManager.OnBrickDestroyed(color);
        }

        if (particle != null) ReturnParticleToPool(particle);
    }
}
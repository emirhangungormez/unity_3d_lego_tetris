using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    [Header("Layer Clear Effects")]
    public float layerClearDelay = 0.3f;
    public float brickFallDuration = 0.6f;
    public float brickRemoveDuration = 0.3f;
    
    [Header("Particle Brick Settings")]
    public GameObject brickParticlePrefab; // 1x1 küçük brick prefab'ı
    public int minParticles = 3;
    public int maxParticles = 8;
    public int poolSize = 50; // Object Pool boyutu
    public float particleLifetime = 4f; // Biraz daha uzun süre
    public float flowSpeed = 3f; // Akış hızını artırdım
    public float randomForce = 0.3f; // ÇOK AZALTILDI (0.3f)
    public float gravityForce = 0.5f; // Yerçekimi ekledim
    public Transform collectionPoint; // Parçaların toplandığı nokta
    
    [Header("Visual Effects")]
    public ParticleSystem clearEffectPrefab;
    public AudioClip clearSound;
    
    private GameManager gameManager;
    private GridManager gridManager;
    
    // OBJECT POOLING Sistemi
    private Queue<GameObject> particlePool = new Queue<GameObject>();
    private List<GameObject> activeParticles = new List<GameObject>();
    
    // Renk dağılımını takip etmek için (GameManager.ColorSettings kullanarak)
    private Dictionary<GameManager.BrickColor, int> layerColorDistribution = new Dictionary<GameManager.BrickColor, int>();
    private int totalBricksInLayer = 0;
    
    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        gridManager = FindObjectOfType<GridManager>();
        
        // Object Pool'u başlat
        InitializeParticlePool();
        
        // Collection point yoksa oluştur
        if (collectionPoint == null)
        {
            CreateCollectionPoint();
        }
    }
    
    // OBJECT POOLING BAŞLANGICI
    void InitializeParticlePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject particle = Instantiate(brickParticlePrefab);
            particle.transform.SetParent(transform);
            particle.SetActive(false);
            particlePool.Enqueue(particle);
        }
        Debug.Log($"🔄 Object Pool başlatıldı: {poolSize} parçacık");
    }
    
    // Pool'dan parçacık al
    GameObject GetParticleFromPool()
    {
        if (particlePool.Count > 0)
        {
            GameObject particle = particlePool.Dequeue();
            particle.SetActive(true);
            return particle;
        }
        
        // Pool boşsa yeni oluştur (acil durum)
        Debug.LogWarning("⚠️ Particle pool boş, yeni parçacık oluşturuluyor");
        GameObject newParticle = Instantiate(brickParticlePrefab);
        return newParticle;
    }
    
    // Parçacığı pool'a geri ver
    void ReturnParticleToPool(GameObject particle)
    {
        if (particle == null) return;
        
        particle.SetActive(false);
        particle.transform.SetParent(transform);
        
        // Fizik bileşenlerini sıfırla
        Rigidbody rb = particle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Transform'u sıfırla
        particle.transform.localPosition = Vector3.zero;
        particle.transform.localRotation = Quaternion.identity;
        particle.transform.localScale = Vector3.one;
        
        particlePool.Enqueue(particle);
    }
    
    void CreateCollectionPoint()
    {
        GameObject point = new GameObject("ParticleCollectionPoint");
        collectionPoint = point.transform;
        collectionPoint.position = new Vector3(4f, -8f, 4f); // Daha aşağıda
    }
    
    public void ClearLayerWithEffects(int layer)
    {
        Debug.Log($"🎬 Layer {layer} temizleme efektleri başlatılıyor...");
        
        // Önce renk dağılımını hesapla
        CalculateColorDistribution(layer);
        
        // 1. Önce üst katmanları aşağı kaydır
        StartCoroutine(MoveBricksDownCoroutine(layer));
        
        // 2. Sonra bu layer'daki brick'leri parçacıklara dönüştür
        StartCoroutine(RemoveLayerWithParticleEffectCoroutine(layer, brickFallDuration + layerClearDelay));
    }
    
    private void CalculateColorDistribution(int layer)
    {
        layerColorDistribution.Clear();
        totalBricksInLayer = 0;
        
        // Layer'daki tüm brick'leri tara ve renk dağılımını hesapla
        foreach(GameObject brick in gameManager.landedBricks)
        {
            if (brick == null) continue;
            
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            
            if(brickLayer == layer)
            {
                // Brick'in rengini material'dan değil, GameManager'ın color sisteminden bul
                GameManager.BrickColor brickColor = GetBrickColorFromName(brick);
                
                if (layerColorDistribution.ContainsKey(brickColor))
                {
                    layerColorDistribution[brickColor]++;
                }
                else
                {
                    layerColorDistribution[brickColor] = 1;
                }
                
                totalBricksInLayer++;
            }
        }
        
        // Renk dağılımını logla (debug için)
        Debug.Log($"🎨 Layer {layer} Renk Dağılımı:");
        foreach (var kvp in layerColorDistribution)
        {
            float percentage = (float)kvp.Value / totalBricksInLayer * 100f;
            Debug.Log($"   - {kvp.Key}: {kvp.Value} brick (%{percentage:F1})");
        }
    }
    
    private GameManager.BrickColor GetBrickColorFromName(GameObject brick)
    {
        // Brick'in adından veya material adından rengi bul
        Renderer renderer = brick.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            string materialName = renderer.material.name.ToLower();
            
            if (materialName.Contains("orange")) return GameManager.BrickColor.Orange;
            if (materialName.Contains("blue")) return GameManager.BrickColor.Blue;
            if (materialName.Contains("pink")) return GameManager.BrickColor.Pink;
            if (materialName.Contains("purple")) return GameManager.BrickColor.Purple;
            if (materialName.Contains("green")) return GameManager.BrickColor.Green;
            if (materialName.Contains("white")) return GameManager.BrickColor.White;
            if (materialName.Contains("gray")) return GameManager.BrickColor.Gray;
            if (materialName.Contains("brown")) return GameManager.BrickColor.Brown;
            if (materialName.Contains("black")) return GameManager.BrickColor.Black;
        }
        
        // Eğer bulamazsak rastgele bir renk döndür
        return (GameManager.BrickColor)Random.Range(0, 9);
    }
    
    private GameManager.ColorSettings GetColorSettings(GameManager.BrickColor colorType)
    {
        // GameManager'daki color settings'i bul
        foreach (var colorSetting in gameManager.availableColors)
        {
            if (colorSetting.colorType == colorType)
            {
                return colorSetting;
            }
        }
        return gameManager.availableColors[0]; // Fallback
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
    
    private System.Collections.IEnumerator RemoveLayerWithParticleEffectCoroutine(int layer, float delay)
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
        
        // Toplam parçacık sayısını hesapla
        int totalParticles = 0;
        Dictionary<GameManager.BrickColor, int> particlesPerColor = new Dictionary<GameManager.BrickColor, int>();
        
        // Her renk için parçacık sayısını hesapla (dağılıma göre)
        foreach (var kvp in layerColorDistribution)
        {
            int particlesForThisColor = Mathf.RoundToInt((float)kvp.Value / totalBricksInLayer * (maxParticles * bricksToRemove.Count));
            particlesPerColor[kvp.Key] = particlesForThisColor;
            totalParticles += particlesForThisColor;
        }
        
        Debug.Log($"🎯 Toplam {totalParticles} parçacık oluşturulacak");
        
        // Brick'leri parçacıklara dönüştür
        foreach(GameObject brick in bricksToRemove)
        {
            CreateBrickParticlesWithDistribution(brick, particlesPerColor);
            gameManager.landedBricks.Remove(brick);
            Destroy(brick);
        }
        
        // Grid'den layer'ı temizle
        gridManager.RemoveLayer(layer);
        
        Debug.Log($"✅ Layer {layer} parçacıklara dönüştürüldü! ({bricksToRemove.Count} brick → {totalParticles} parçacık)");
        
        // Skor/istatistik bilgisini GameManager'a ilet (ileride kullanılacak)
        SendColorStatsToGameManager(layer);
    }
    
    private void CreateBrickParticlesWithDistribution(GameObject originalBrick, Dictionary<GameManager.BrickColor, int> particlesPerColor)
    {
        if (brickParticlePrefab == null)
        {
            Debug.LogError("Brick particle prefab'ı atanmamış!");
            return;
        }
        
        // Bu brick için kaç parçacık oluşturulacağını belirle
        int particlesForThisBrick = Random.Range(minParticles, maxParticles + 1);
        
        for (int i = 0; i < particlesForThisBrick; i++)
        {
            // Renk dağılımına göre renk seç
            GameManager.BrickColor particleColor = GetRandomColorByDistribution(particlesPerColor);
            CreateSingleParticle(originalBrick.transform.position, particleColor);
        }
        
        // Efekt ve ses
        PlayClearEffects(originalBrick.transform.position);
    }
    
    private GameManager.BrickColor GetRandomColorByDistribution(Dictionary<GameManager.BrickColor, int> particlesPerColor)
    {
        // Toplam parçacık sayısını hesapla
        int totalParticles = 0;
        foreach (var kvp in particlesPerColor)
        {
            totalParticles += kvp.Value;
        }
        
        if (totalParticles == 0) 
            return (GameManager.BrickColor)Random.Range(0, 9);
        
        // Rastgele seçim yap (dağılıma göre)
        int randomValue = Random.Range(0, totalParticles);
        int currentSum = 0;
        
        foreach (var kvp in particlesPerColor)
        {
            currentSum += kvp.Value;
            if (randomValue < currentSum)
            {
                return kvp.Key;
            }
        }
        
        return (GameManager.BrickColor)Random.Range(0, 9);
    }
    
    private void CreateSingleParticle(Vector3 position, GameManager.BrickColor color)
    {
        // POOL'dan parçacık al (Instantiate yerine)
        GameObject particle = GetParticleFromPool();
        
        particle.transform.position = position + new Vector3(
            Random.Range(-0.2f, 0.2f), // ÇOK AZ rastgele offset
            Random.Range(-0.1f, 0.1f),
            Random.Range(-0.2f, 0.2f)
        );
        
        // GameManager'ın renk sistemini kullanarak texture uygula
        ApplyParticleTexture(particle, color);
        
        // Fizik ayarla
        Rigidbody rb = particle.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = particle.AddComponent<Rigidbody>();
        }
        
        // ÇOK AZ rastgele başlangıç kuvveti
        Vector3 randomDirection = new Vector3(
            Random.Range(-0.5f, 0.5f),
            Random.Range(0.1f, 0.5f),   // ÇOK AZ yukarı
            Random.Range(-0.5f, 0.5f)
        );
        rb.AddForce(randomDirection * randomForce, ForceMode.Impulse);
        
        // ÇOK AZ rastgele rotation
        rb.AddTorque(Random.insideUnitSphere * randomForce * 0.5f, ForceMode.Impulse);
        
        // Akış coroutine'ini başlat
        StartCoroutine(ParticleFlowCoroutine(particle, rb));
        
        activeParticles.Add(particle);
    }
    
    private void ApplyParticleTexture(GameObject particle, GameManager.BrickColor color)
    {
        Renderer[] allRenderers = particle.GetComponentsInChildren<Renderer>(true);
        
        if (allRenderers.Length == 0)
        {
            Debug.LogError("Particle brick içinde hiç renderer bulunamadı!");
            return;
        }
        
        GameManager.ColorSettings colorSettings = GetColorSettings(color);
        
        foreach (Renderer renderer in allRenderers)
        {
            if (renderer == null) continue;
            
            Material newMaterial = new Material(renderer.material);
            newMaterial.name = "ParticleMaterial_" + color.ToString();
            
            newMaterial.mainTextureScale = colorSettings.tiling;
            newMaterial.mainTextureOffset = colorSettings.offset;
            
            renderer.material = newMaterial;
        }
    }
    
    private System.Collections.IEnumerator ParticleFlowCoroutine(GameObject particle, Rigidbody rb)
    {
        float timer = 0f;
        Vector3 startPosition = particle.transform.position;
        Vector3 startScale = particle.transform.localScale;
        
        while (timer < particleLifetime && particle != null)
        {
            timer += Time.deltaTime;
            
            if (collectionPoint != null && rb != null)
            {
                // Akışın ilk yarısında daha yavaş, ikinci yarısında daha hızlı
                float flowPhase = timer / particleLifetime;
                float currentFlowSpeed = flowSpeed * (0.5f + flowPhase * 1.5f);
                
                // Hedefe doğru akış kuvveti (daha güçlü)
                Vector3 direction = (collectionPoint.position - particle.transform.position).normalized;
                rb.AddForce(direction * currentFlowSpeed * Time.deltaTime, ForceMode.VelocityChange);
                
                // Hafif yerçekimi (aşağı doğru)
                rb.AddForce(Vector3.down * gravityForce * Time.deltaTime, ForceMode.VelocityChange);
                
                // Hız sınırlaması (çok hızlı gitmesin)
                if (rb.velocity.magnitude > 5f)
                {
                    rb.velocity = rb.velocity.normalized * 5f;
                }
                
                // Yavaş yavaş scale küçült (yok olma efekti) - sadece son %20'sinde
                if (flowPhase > 0.8f)
                {
                    float scaleProgress = (flowPhase - 0.8f) / 0.2f;
                    particle.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.1f, scaleProgress);
                }
            }
            
            yield return null;
        }
        
        // Parçacığı POOL'a geri ver (Destroy yerine)
        if (particle != null)
        {
            activeParticles.Remove(particle);
            ReturnParticleToPool(particle);
        }
    }
    
    private void PlayClearEffects(Vector3 position)
    {
        // Parlama efekti
        if (clearEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(clearEffectPrefab, position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 2f);
        }
        
        // Ses efekti
        if (clearSound != null)
        {
            AudioSource.PlayClipAtPoint(clearSound, position);
        }
    }
    
    private void SendColorStatsToGameManager(int layer)
    {
        // Bu bilgiyi GameManager'a ilet (skor sistemi için)
        Debug.Log($"📊 Layer {layer} renk istatistikleri GameManager'a iletildi");
        
        // Örnek: GameManager.instance.OnLayerCleared(layerColorDistribution);
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
    
    // Debug için pool durumunu göster
    public void DebugPoolStatus()
    {
        Debug.Log($"=== OBJECT POOL DURUMU ===");
        Debug.Log($"🔷 Aktif Parçacık: {activeParticles.Count}");
        Debug.Log($"💠 Pool'da Bekleyen: {particlePool.Count}");
        Debug.Log($"📊 Toplam: {activeParticles.Count + particlePool.Count}");
    }
    
    // Temizlik için
    void OnDestroy()
    {
        foreach (GameObject particle in activeParticles)
        {
            if (particle != null)
            {
                ReturnParticleToPool(particle);
            }
        }
        activeParticles.Clear();
        
        // Pool'daki tüm parçacıkları da temizle
        foreach (GameObject particle in particlePool)
        {
            if (particle != null)
            {
                Destroy(particle);
            }
        }
        particlePool.Clear();
    }
    
    // Hızlı efekt testi için
    public void TestEffects()
    {
        ClearLayerWithEffects(1);
    }
}
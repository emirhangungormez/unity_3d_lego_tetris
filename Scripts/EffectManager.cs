using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    [System.Serializable]
    public class EffectSettings
    {
        public float layerClearDelay = 0.3f;
        public float brickFallDuration = 0.6f;
        public float particleLifetime = 10f;
        public float flowSpeed = 20f;
        public float randomForce = 2f;
        public float gravityForce = 0.1f;
    }
    
    [System.Serializable]
    public class ParticleSettings
    {
        public GameObject brickParticlePrefab;
        public int minParticles = 3;
        public int maxParticles = 8;
        public ParticleSystem clearEffectPrefab;
        public AudioClip clearSound;
    }

    [System.Serializable]
    public class CollectionBoxSettings
    {
        public GameObject boxPrefab;
        public float spacing = 2f;
        public float yPosition = -5f;
        public int maxColumns = 3;
    }

    [Header("Settings")]
    public EffectSettings effectSettings;
    public ParticleSettings particleSettings;
    public CollectionBoxSettings boxSettings;
    
    private GameManager gameManager;
    private GridManager gridManager;
    private List<GameObject> activeParticles = new List<GameObject>();
    private Dictionary<GameManager.BrickColor, int> layerColorDistribution = new Dictionary<GameManager.BrickColor, int>();
    private Dictionary<GameManager.BrickColor, Transform> collectionBoxes = new Dictionary<GameManager.BrickColor, Transform>();
    private int totalBricksInLayer = 0;

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();
        gridManager = FindObjectOfType<GridManager>();
        
        CreateCollectionBoxes();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            DebugParticleStatus();
        }
    }
    
    void CreateCollectionBoxes()
    {
        // Önce eski kutuları temizle
        foreach (var box in collectionBoxes.Values)
        {
            if (box != null) Destroy(box.gameObject);
        }
        collectionBoxes.Clear();

        // Aktif renkleri al
        List<GameManager.BrickColor> activeColors = GetActiveColors();
        
        if (activeColors.Count == 0) return;

        // Grid merkezini hesapla
        float gridCenterX = gridManager.gridSize.x * gridManager.cellSize * 0.5f;
        float gridCenterZ = gridManager.gridSize.y * gridManager.cellSize * 0.5f;
        Vector3 gridCenter = new Vector3(gridCenterX, boxSettings.yPosition, gridCenterZ);

        // Matris düzeni için ayarlar
        int maxColumns = boxSettings.maxColumns;
        float spacing = boxSettings.spacing;
        Vector3 boxScale = Vector3.one;

        // Satır ve sütun sayılarını hesapla
        int totalBoxes = activeColors.Count;
        int rows = Mathf.CeilToInt((float)totalBoxes / maxColumns);
        int columns = Mathf.Min(totalBoxes, maxColumns);

        // Toplam genişlik ve derinlik hesapla
        float totalWidth = (columns - 1) * spacing;
        float totalDepth = (rows - 1) * spacing;

        // Başlangıç pozisyonunu hesapla (merkezden)
        float startX = gridCenter.x - totalWidth * 0.5f;
        float startZ = gridCenter.z - totalDepth * 0.5f;

        for (int i = 0; i < activeColors.Count; i++)
        {
            GameManager.BrickColor color = activeColors[i];
            
            // Matris pozisyonunu hesapla
            int row = i / maxColumns;
            int column = i % maxColumns;
            
            Vector3 boxPosition = new Vector3(
                startX + column * spacing,
                boxSettings.yPosition,
                startZ + row * spacing
            );

            CreateCollectionBox(color, boxPosition, boxScale);
        }

        Debug.Log($"📦 {activeColors.Count} adet toplama kutusu oluşturuldu ({rows}x{columns} matris)");
    }
    
    List<GameManager.BrickColor> GetActiveColors()
    {
        List<GameManager.BrickColor> activeColors = new List<GameManager.BrickColor>();
        HashSet<GameManager.BrickColor> usedColors = new HashSet<GameManager.BrickColor>();

        // Landed brick'lerde kullanılan renkleri bul
        foreach (GameObject brick in gameManager.landedBricks)
        {
            if (brick == null) continue;
            
            GameManager.BrickColor brickColor = GetBrickColor(brick);
            if (!usedColors.Contains(brickColor))
            {
                usedColors.Add(brickColor);
                activeColors.Add(brickColor);
            }
        }

        // Eğer landed brick yoksa, available colors'dan al
        if (activeColors.Count == 0)
        {
            foreach (var colorSetting in gameManager.availableColors)
            {
                activeColors.Add(colorSetting.colorType);
            }
        }

        return activeColors;
    }
    
    void CreateCollectionBox(GameManager.BrickColor color, Vector3 position, Vector3 scale)
    {
        if (boxSettings.boxPrefab == null)
        {
            Debug.LogError("Kutu prefab'ı atanmamış!");
            return;
        }

        GameObject box = Instantiate(boxSettings.boxPrefab);
        box.name = $"CollectionBox_{color}";
        box.transform.position = position;
        box.transform.localScale = scale;

        // Kutuya renk uygula
        ApplyBoxColor(box, color);

        collectionBoxes[color] = box.transform;

        Debug.Log($"🎯 {color} rengi için toplama kutusu oluşturuldu: {position}");
    }
    
    void ApplyBoxColor(GameObject box, GameManager.BrickColor color)
    {
        GameManager.ColorSettings colorSettings = GetColorSettings(color);
        
        foreach (Renderer renderer in box.GetComponentsInChildren<Renderer>())
        {
            if (renderer == null) continue;
            
            Material newMaterial = new Material(renderer.material);
            newMaterial.name = $"BoxMaterial_{color}";
            
            newMaterial.mainTextureScale = colorSettings.tiling;
            newMaterial.mainTextureOffset = colorSettings.offset;
            
            renderer.material = newMaterial;
        }
    }
    
    public void ClearLayerWithEffects(int layer)
    {
        Debug.Log($"🎬 Layer {layer} temizleme efektleri başlatılıyor...");
        
        CalculateColorDistribution(layer);
        StartCoroutine(MoveBricksDownCoroutine(layer));
        StartCoroutine(RemoveLayerWithParticleEffectCoroutine(layer, effectSettings.brickFallDuration + effectSettings.layerClearDelay));
    }
    
    private void CalculateColorDistribution(int layer)
    {
        layerColorDistribution.Clear();
        totalBricksInLayer = 0;
        
        foreach(GameObject brick in gameManager.landedBricks)
        {
            if (brick == null) continue;
            
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            
            if(brickLayer == layer)
            {
                GameManager.BrickColor brickColor = GetBrickColor(brick);
                layerColorDistribution[brickColor] = layerColorDistribution.GetValueOrDefault(brickColor) + 1;
                totalBricksInLayer++;
            }
        }
        
        // Debug log
        Debug.Log($"🎨 Layer {layer} Renk Dağılımı:");
        foreach (var kvp in layerColorDistribution)
        {
            float percentage = (float)kvp.Value / totalBricksInLayer * 100f;
            Debug.Log($"   - {kvp.Key}: {kvp.Value} brick (%{percentage:F1})");
        }
    }
    
    private GameManager.BrickColor GetBrickColor(GameObject brick)
    {
        Renderer renderer = brick.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            string materialName = renderer.material.name.ToLower();
            return materialName switch
            {
                string s when s.Contains("orange") => GameManager.BrickColor.Orange,
                string s when s.Contains("blue") => GameManager.BrickColor.Blue,
                string s when s.Contains("pink") => GameManager.BrickColor.Pink,
                string s when s.Contains("purple") => GameManager.BrickColor.Purple,
                string s when s.Contains("green") => GameManager.BrickColor.Green,
                string s when s.Contains("white") => GameManager.BrickColor.White,
                string s when s.Contains("gray") => GameManager.BrickColor.Gray,
                string s when s.Contains("brown") => GameManager.BrickColor.Brown,
                string s when s.Contains("black") => GameManager.BrickColor.Black,
                _ => (GameManager.BrickColor)Random.Range(0, 9)
            };
        }
        return (GameManager.BrickColor)Random.Range(0, 9);
    }
    
    private IEnumerator MoveBricksDownCoroutine(int clearedLayer)
    {
        yield return new WaitForSeconds(effectSettings.layerClearDelay);
        
        List<GameObject> bricksToMove = new List<GameObject>();
        
        foreach(GameObject brick in gameManager.landedBricks)
        {
            if (brick == null) continue;
            
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            
            if(brickLayer > clearedLayer)
            {
                bricksToMove.Add(brick);
            }
        }
        
        foreach(GameObject brick in bricksToMove)
        {
            Vector2Int brickGridPos = gameManager.GetBrickGridPosition(brick);
            int brickLayer = gridManager.GetLayerAtPosition(brickGridPos, brick);
            float newY = (brickLayer - 1) * gridManager.layerHeight;
            StartCoroutine(MoveBrickSmooth(brick, newY, effectSettings.brickFallDuration));
        }
        
        Debug.Log($"⬇️ {bricksToMove.Count} brick aşağı kaydırılıyor...");
    }
    
    private IEnumerator RemoveLayerWithParticleEffectCoroutine(int layer, float delay)
    {
        yield return new WaitForSeconds(delay);
        
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
        
        // Toplam parçacık sayısını hesapla ve logla
        int totalParticles = 0;
        Dictionary<GameManager.BrickColor, int> particlesPerColor = new Dictionary<GameManager.BrickColor, int>();
        
        foreach (var kvp in layerColorDistribution)
        {
            int particlesForThisColor = Mathf.RoundToInt((float)kvp.Value / totalBricksInLayer * (particleSettings.maxParticles * bricksToRemove.Count));
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
        
        gridManager.RemoveLayer(layer);
        
        Debug.Log($"✅ Layer {layer} parçacıklara dönüştürüldü! ({bricksToRemove.Count} brick → {totalParticles} parçacık)");
    }
    
    private void CreateBrickParticlesWithDistribution(GameObject originalBrick, Dictionary<GameManager.BrickColor, int> particlesPerColor)
    {
        if (particleSettings.brickParticlePrefab == null)
        {
            Debug.LogError("Brick particle prefab'ı atanmamış!");
            return;
        }
        
        int particlesForThisBrick = Random.Range(particleSettings.minParticles, particleSettings.maxParticles + 1);
        
        for (int i = 0; i < particlesForThisBrick; i++)
        {
            GameManager.BrickColor particleColor = GetRandomColorByDistribution(particlesPerColor);
            CreateSingleParticle(originalBrick.transform.position, particleColor);
        }
        
        PlayClearEffects(originalBrick.transform.position);
    }
    
    private GameManager.BrickColor GetRandomColorByDistribution(Dictionary<GameManager.BrickColor, int> particlesPerColor)
    {
        int totalParticles = 0;
        foreach (var kvp in particlesPerColor)
            totalParticles += kvp.Value;
        
        if (totalParticles == 0) 
            return (GameManager.BrickColor)Random.Range(0, 9);
        
        int randomValue = Random.Range(0, totalParticles);
        int currentSum = 0;
        
        foreach (var kvp in particlesPerColor)
        {
            currentSum += kvp.Value;
            if (randomValue < currentSum) return kvp.Key;
        }
        
        return (GameManager.BrickColor)Random.Range(0, 9);
    }
    
    private void CreateSingleParticle(Vector3 position, GameManager.BrickColor color)
    {
        GameObject particle = Instantiate(particleSettings.brickParticlePrefab);
        
        // BRICK'LERİ 3'TE 1 ORANINDA KÜÇÜLT
        particle.transform.localScale = Vector3.one * 0.33f;
        
        // Rastgele pozisyon offset'i
        particle.transform.position = position + new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0.5f, 2f),
            Random.Range(-1f, 1f)
        );
        
        ApplyParticleTexture(particle, color);
        
        Rigidbody rb = particle.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = particle.AddComponent<Rigidbody>();
        }
        
        // DAHA AZ DRAG - daha serbest hareket
        rb.drag = 0.05f;
        rb.angularDrag = 0.02f;
        rb.mass = 0.1f;
        
        // Yerçekimini etkinleştir
        rb.useGravity = true;
        
        // Daha güçlü başlangıç kuvveti
        Vector3 randomDirection = new Vector3(
            Random.Range(-2f, 2f),
            Random.Range(1f, 3f),
            Random.Range(-2f, 2f)
        );
        rb.AddForce(randomDirection * effectSettings.randomForce * 3f, ForceMode.Impulse);
        
        // Rastgele rotation
        rb.AddTorque(Random.insideUnitSphere * effectSettings.randomForce, ForceMode.Impulse);
        
        StartCoroutine(ParticleFlowCoroutine(particle, rb, color));
        activeParticles.Add(particle);
        
        Debug.Log($"🎯 {color} renginde KÜÇÜK parçacık oluşturuldu, hedef: {collectionBoxes.ContainsKey(color)}");
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
    
    private GameManager.ColorSettings GetColorSettings(GameManager.BrickColor colorType)
    {
        foreach (var colorSetting in gameManager.availableColors)
            if (colorSetting.colorType == colorType)
                return colorSetting;
        
        return gameManager.availableColors[0];
    }
    
    private IEnumerator ParticleFlowCoroutine(GameObject particle, Rigidbody rb, GameManager.BrickColor color)
    {
        float timer = 0f;
        Vector3 startScale = particle.transform.localScale;
        float lastDebugTime = 0f;
        bool isAbovePlate = true;
        
        // Hedef kutusunu bulmak için bekle (kutular oluşana kadar)
        Transform targetBox = null;
        float waitTime = 0f;
        while (targetBox == null && waitTime < 2f)
        {
            targetBox = collectionBoxes.ContainsKey(color) ? collectionBoxes[color] : null;
            if (targetBox == null)
            {
                waitTime += Time.deltaTime;
                yield return null;
            }
        }

        if (targetBox == null)
        {
            Debug.LogWarning($"Hedef kutu bulunamadı: {color}, parçacık yok ediliyor");
            activeParticles.Remove(particle);
            Destroy(particle);
            yield break;
        }

        Debug.Log($"🎯 Parçacık {color} hedef kutusuna yönlendiriliyor: {targetBox.position}");

        // Başlangıçta biraz bekle (dağılma efekti için)
        yield return new WaitForSeconds(0.5f);

        // Fizik ayarlarını daha agresif yap
        rb.drag = 0.1f;
        rb.angularDrag = 0.05f;

        while (timer < effectSettings.particleLifetime && particle != null && targetBox != null)
        {
            timer += Time.deltaTime;
            
            if (rb != null)
            {
                // Hedef kutuya olan mesafeyi hesapla
                float distanceToTarget = Vector3.Distance(particle.transform.position, targetBox.position);
                
                // Plate üzerinde mi kontrol et (Y pozisyonu > 0 ise plate üzerinde)
                bool currentlyAbovePlate = particle.transform.position.y > 0.1f;
                
                Vector3 direction;
                
                if (currentlyAbovePlate && isAbovePlate)
                {
                    // PLATE ÜZERİNDEYKEN: Önce yana doğru it, sonra aşağı
                    Vector3 horizontalDirection = new Vector3(
                        (targetBox.position - particle.transform.position).normalized.x,
                        0f,
                        (targetBox.position - particle.transform.position).normalized.z
                    ).normalized;
                    
                    // Yana doğru güçlü kuvvet + hafif aşağı itiş
                    direction = horizontalDirection + Vector3.down * 0.3f;
                    isAbovePlate = true;
                }
                else
                {
                    // PLATE'İN ALTINDAYKEN: Doğrudan hedefe git
                    direction = (targetBox.position - particle.transform.position).normalized;
                    isAbovePlate = false;
                }
                
                // Mesafe azaldıkça hızı azalt (yavaşlama efekti)
                float targetSpeed = Mathf.Clamp(distanceToTarget * 3f, 2f, 15f);
                
                // Plate üzerindeyken daha hızlı, aşağıdayken normal hız
                if (isAbovePlate) targetSpeed *= 1.5f;
                
                // Mevcut hızı hedef hıza doğru yönlendir
                Vector3 targetVelocity = direction * targetSpeed;
                rb.velocity = Vector3.Lerp(rb.velocity, targetVelocity, 3f * Time.deltaTime);

                // Debug: Her saniye bir kere log göster
                if (timer - lastDebugTime > 1f)
                {
                    lastDebugTime = timer;
                    string positionStatus = isAbovePlate ? "PLATE ÜZERİNDE" : "PLATE ALTINDA";
                    Debug.Log($"🎯 Parçacık {color} -> {positionStatus}, Mesafe: {distanceToTarget:F1}, Hız: {rb.velocity.magnitude:F1}");
                }
                
                // Hedefe çok yakınsa yok ol
                if (distanceToTarget < 1f)
                {
                    // Küçülme efekti
                    float disappearProgress = 1f - (distanceToTarget / 1f);
                    particle.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.01f, disappearProgress);
                    
                    if (distanceToTarget < 0.3f)
                    {
                        Debug.Log($"✅ Parçacık {color} kutusuna ulaştı!");
                        break;
                    }
                }
            }
            
            yield return null;
        }
        
        // Parçacığı yok et
        if (particle != null)
        {
            activeParticles.Remove(particle);
            Destroy(particle);
        }
    }
    
    private void PlayClearEffects(Vector3 position)
    {
        // Parlama efekti
        if (particleSettings.clearEffectPrefab != null)
        {
            ParticleSystem effect = Instantiate(particleSettings.clearEffectPrefab, position, Quaternion.identity);
            effect.Play();
            Destroy(effect.gameObject, 2f);
        }
        
        // Ses efekti
        if (particleSettings.clearSound != null)
        {
            AudioSource.PlayClipAtPoint(particleSettings.clearSound, position);
        }
    }
    
    private IEnumerator MoveBrickSmooth(GameObject brick, float targetY, float duration)
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

    // Debug için: Tüm kutuların pozisyonlarını ve parçacık sayılarını göster
    public void DebugParticleStatus()
    {
        Debug.Log("=== PARÇACIK DURUMU ===");
        Debug.Log($"Aktif Parçacık Sayısı: {activeParticles.Count}");
        
        foreach (var kvp in collectionBoxes)
        {
            int particlesForThisColor = 0;
            foreach (var particle in activeParticles)
            {
                if (particle != null)
                {
                    var particleColor = GetBrickColor(particle);
                    if (particleColor == kvp.Key)
                        particlesForThisColor++;
                }
            }
            Debug.Log($"Kutu {kvp.Key}: {particlesForThisColor} parçacık, Pozisyon: {kvp.Value.position}");
        }
    }

    // Oyun başladığında veya renkler değiştiğinde kutuları yeniden oluşturmak için
    public void RefreshCollectionBoxes()
    {
        CreateCollectionBoxes();
    }
    
    void OnDestroy()
    {
        foreach (GameObject particle in activeParticles)
            if (particle != null)
                Destroy(particle);
        
        foreach (var box in collectionBoxes.Values)
            if (box != null)
                Destroy(box.gameObject);
        
        activeParticles.Clear();
        collectionBoxes.Clear();
    }
    
    // Hızlı efekt testi için
    public void TestEffects()
    {
        ClearLayerWithEffects(1);
    }
}
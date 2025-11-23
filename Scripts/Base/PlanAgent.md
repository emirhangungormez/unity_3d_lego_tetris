# Plan Agent — Kısa Kılavuz

Bu dosya, proje genelinde Copilot veya Copilot Chat'e doğrudan yapıştırıp kullanabileceğin, minimal ve performans odaklı kod yazma yönergelerini içerir. Türkçe ve kopyala‑yapıştır için uygun formatta hazırlanmıştır.

---

## KOPYALA‑YAPIŞTIR Copilot prompt'u

```
Unity C# component oluştur: minimal, açık, performans odaklı, null-safe ve test edilebilir. Yap:
- [SerializeField] kullanarak private alanları expose et, public alanları azalt.
- Awake/Start/OnEnable/OnDisable doğru ayır. Component içindeki GetComponent tek seferde cache'le.
- Update/FixedUpdate/OnTrigger vb. içinde alokasyon/yeni obje yok; string.Format, LINQ veya boxing-e neden olmayacak.
- Instantiate/Destroy sık kullanılıyorsa pooling uygula (basit reusable pool sınıfı oluştur).
- Coroutine'leri iptal edilebilir yap (OnDisable/OnDestroy'da StopCoroutine).
- Hata/eksik referans için guard-check ve Debug.Assert/LogWarning ekle (sadece editorda verbose).
- Performans kritik kısımlarda LINQ ve GetComponent çağrılarını kaldır, for döngüsü/array kullan.
- Event-driven (Action/UnityEvent) kullanarak tight coupling azalt.
- ScriptableObject ile konfigürasyon ayır, Magic number yok.
- Single-responsibility: bir sınıf tek sorumluluk.
- Threading yok (Unity API main thread); async gerekiyorsa UnityMainThreadDispatcher veya Task + main-thread callback.
Üret: kısa bir component şablonu ve basit ObjectPool sınıfı.
```

---

## Kısa Kurallar (Her dosya / PR için kontrol listesi)
- **Tek sorumluluk:** bir class = 1 amaç.
- **Serialized alanlar private:** Inspector için `[SerializeField] private Type name;` kullan; public hanya API için.
- **GetComponent cache'le:** Awake veya OnEnable'da çağır ve sakla.
- **Update içinde GC yasak:** new, LINQ, boxing veya string birleştirme yapma.
- **Pool kullan:** Instantiate/Destroy sık ise ObjectPool uygula.
- **Null-check + early-return:** `if (ref == null) { Debug.LogWarning(...); enabled = false; return; }`.
- **Coroutine yönetimi:** coroutine referansını tut, OnDisable/OnDestroy'da iptal et.
- **Physics:** FixedUpdate ve Time.fixedDeltaTime kullan.
- **Ayarlar:** ScriptableObject ile konfigürasyon, magic number yok.
- **Test edilebilirlik:** bağımlılıkları setter/constructor ile dışa ver.

---

## Minimal Component Şablonu (Düz metin — kopyala/yapıştır için)

// Minimal, güvenli Unity component şablonu
using UnityEngine;
using System.Collections;

public class MinimalComponent : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int updateIntervalFrames = 1;
    [SerializeField] private float someValue = 1f;

    // cached refs
    private Rigidbody cachedRb;
    private Coroutine runningCoroutine;

    void Awake()
    {
        cachedRb = GetComponent<Rigidbody>();
        if (cachedRb == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"{name}: Rigidbody bulunamadı, fizik tabanlı işlevler devre dışı.");
#endif
            enabled = false;
            return;
        }
        ValidateConfig();
    }

    void OnEnable()
    {
        runningCoroutine = StartCoroutine(MainLoop());
    }

    void OnDisable()
    {
        if (runningCoroutine != null) StopCoroutine(runningCoroutine);
        runningCoroutine = null;
    }

    void ValidateConfig()
    {
        updateIntervalFrames = Mathf.Max(1, updateIntervalFrames);
        someValue = Mathf.Max(0f, someValue);
    }

    IEnumerator MainLoop()
    {
        int frame = 0;
        while (enabled)
        {
            if (++frame >= updateIntervalFrames)
            {
                frame = 0;
                DoWork();
            }
            yield return null;
        }
    }

    void DoWork()
    {
        // GC-safe, no allocations, no LINQ
        var v = cachedRb.position + Vector3.up * someValue * Time.deltaTime;
        cachedRb.MovePosition(v);
    }
}

---

## Basit ObjectPool (Düz metin örnek)

// Amaç: Instantiate/Destroy maliyetini azaltmak.
using UnityEngine;
using System.Collections.Generic;

public class SimplePool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    private readonly Queue<GameObject> q = new Queue<GameObject>();

    public GameObject Get(Vector3 pos, Quaternion rot)
    {
        GameObject go = q.Count > 0 ? q.Dequeue() : Instantiate(prefab);
        go.transform.SetPositionAndRotation(pos, rot);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        go.SetActive(false);
        q.Enqueue(go);
    }
}

Kullanım: Spawn yerine `pool.Get(...)` çağır; nesne tamamlandığında `pool.Release(obj)` çağır.

---

## Hızlı İpuçları — Sık Hatalar ve Çözümleri
- **NullReference:** Awake/OnValidate'da serialized referansları kontrol et; eksikse component'i disable et.
- **Instantiate/Destroy:** partiküller ve konfeti için pool kullan.
- **GetComponent Update içinde:** cache'le.
- **LINQ/Enumerator GC:** performans kritik yerlerde for/array kullan.
- **Coroutine leak:** referans tut, OnDisable'da StopCoroutine.
- **Audio:** çoklu çalma gerekiyorsa pool edilmiş AudioSource kullan veya PlayOneShot dikkatli kullan.

---

## Kullanım
1. `Assets/Scripts/PlanAgent.md` dosyasını repo'ya ekle.
2. Copilot Chat aç, üstteki "KOPYALA‑YAPIŞTIR Copilot prompt'u" bölümünü kopyala ve yapıştır.
3. Copilot'un önerilerini al, gerekirse component'i veya pool'u örnek `.cs` dosyası olarak oluştur.

**Önerilen commit mesajı:** `Add PlanAgent guide (Assets/Scripts/PlanAgent.md)`
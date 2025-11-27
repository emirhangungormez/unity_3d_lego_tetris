using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Loading ekranını yönetir.
/// LoadingPanel prefab'ı aktif olduğunda yükleme başlar.
/// 
/// SETUP:
/// 1. LoadingPanel prefab oluştur:
///    - Scrollbar (loadingScrollbar'a ata)
///    - Text (loadingPercentText'e ata)
/// 2. StartLoading() çağırarak yüklemeyi başlat
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Loading UI")]
    [Tooltip("Scrollbar - size değeri 0'dan 1'e yükselir")]
    public Scrollbar loadingScrollbar;
    [Tooltip("Yüzde text'i (%0, %35, %100)")]
    public Text loadingPercentText;
    [Tooltip("Loading Panel prefab")]
    public GameObject loadingPanel;
    
    [Header("Timing")]
    [Tooltip("Minimum yükleme süresi (saniye)")]
    public float minimumDisplayTime = 2f;
    [Tooltip("Fade out süresi")]
    public float fadeOutDuration = 0.3f;
    
    private CanvasGroup canvasGroup;
    private float loadingProgress = 0f;
    private bool isLoading = false;
    private bool isLoadingComplete = false;
    
    // Yükleme bittiğinde çağrılacak callback
    public System.Action OnLoadingComplete;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Panel aktifse yüklemeyi başlat
        if (loadingPanel != null && loadingPanel.activeSelf)
        {
            StartLoading();
        }
    }

    /// <summary>
    /// Yükleme işlemini başlatır. LoadingPanel aktif olmalı.
    /// </summary>
    public void StartLoading()
    {
        if (loadingPanel == null)
        {
            Debug.LogWarning("LoadingScreenManager: loadingPanel atanmamış!");
            return;
        }
        
        if (!loadingPanel.activeSelf)
        {
            Debug.LogWarning("LoadingScreenManager: loadingPanel aktif değil, önce aktifleştirin.");
            return;
        }
        
        if (isLoading) return;
        
        isLoading = true;
        isLoadingComplete = false;
        loadingProgress = 0f;
        
        // CanvasGroup ayarla
        canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        
        // UI'ı sıfırla
        if (loadingScrollbar != null)
            loadingScrollbar.size = 0f;
        if (loadingPercentText != null)
            loadingPercentText.text = "%0";
        
        StartCoroutine(LoadingSequence());
    }

    /// <summary>
    /// LoadingPanel'i aktifleştirir ve yüklemeyi başlatır.
    /// </summary>
    public void ShowAndStartLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            StartLoading();
        }
    }

    IEnumerator LoadingSequence()
    {
        float elapsed = 0f;
        
        while (elapsed < minimumDisplayTime)
        {
            elapsed += Time.deltaTime;
            loadingProgress = Mathf.Clamp01(elapsed / minimumDisplayTime);
            UpdateLoadingUI();
            yield return null;
        }
        
        // %100 göster
        loadingProgress = 1f;
        UpdateLoadingUI();
        isLoadingComplete = true;
        
        yield return new WaitForSeconds(0.2f);
        
        // Fade out
        yield return StartCoroutine(FadeOut());
        
        // Panel'i gizle
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        
        isLoading = false;
        
        // Callback çağır
        OnLoadingComplete?.Invoke();
    }

    void UpdateLoadingUI()
    {
        if (loadingScrollbar != null)
            loadingScrollbar.size = loadingProgress;
        
        if (loadingPercentText != null)
        {
            int percentage = Mathf.RoundToInt(loadingProgress * 100f);
            loadingPercentText.text = $"%{percentage}";
        }
    }

    IEnumerator FadeOut()
    {
        if (canvasGroup == null) yield break;
        
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Yükleme tamamlandı mı?
    /// </summary>
    public bool IsLoadingComplete => isLoadingComplete;
    
    /// <summary>
    /// Yükleme devam ediyor mu?
    /// </summary>
    public bool IsLoading => isLoading;
}

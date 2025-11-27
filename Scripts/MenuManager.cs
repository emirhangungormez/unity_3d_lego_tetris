using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Menu sahnesinde kullanılan yönetici.
/// Level seçimi, arkaplan ayarları ve sahne geçişlerini yönetir.
/// Kameranın child'ı olarak background overlay oluşturur.
/// </summary>
public class MenuManager : MonoBehaviour
{
    public enum BackgroundType { White, Black }
    
    public static MenuManager Instance { get; private set; }

    [Header("Scene Names")]
    public string menuSceneName = "Menu";
    public string gameSceneName = "Game";

    [Header("Background Selection")]
    [Tooltip("Inspector'dan background seçin")]
    public BackgroundType selectedBackground = BackgroundType.White;
    
    [Header("Background Images")]
    [Tooltip("0: White, 1: Black sırasında olmalı")]
    public List<Sprite> backgroundImages = new List<Sprite>();
    
    [Header("Overlay Settings")]
    public float imageDistance = 80f;
    public Vector3 imageScale = new Vector3(0.3f, 0.3f, 0.3f);
    public Vector3 imageRotation = new Vector3(0f, 0f, 90f);
    
    [Header("Background Music")]
    [Tooltip("Background music clips matching background themes")]
    public List<AudioClip> backgroundMusicList = new List<AudioClip>();
    
    // Overlay objeleri
    private Camera targetCamera;
    private Canvas overlayCanvas;
    private GameObject imageObject;
    private Image imageComponent;
    private BackgroundType lastSelectedBackground;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Debug.LogError("MenuManager: Main Camera bulunamadı!");
            return;
        }
        
        CreateOverlayImage();
        ApplySelectedBackground();
        lastSelectedBackground = selectedBackground;
    }
    
    void LateUpdate()
    {
        // Overlay'i kameraya göre güncelle
        UpdateOverlayPosition();
        
        // Inspector'dan değişiklik yapıldığında otomatik uygula
        if (selectedBackground != lastSelectedBackground)
        {
            ApplySelectedBackground();
            lastSelectedBackground = selectedBackground;
        }
    }
    
    /// <summary>
    /// Kameranın child'ı olarak OverlayCanvas/OverlayImage oluşturur
    /// </summary>
    void CreateOverlayImage()
    {
        // CameraController ile aynı yöntemi kullanmak için OverlayCreator kullan
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        GameObject canvasObj = OverlayCreator.CreateOverlay(cam, out imageComponent, imageDistance, imageScale, imageRotation, true);
        if (canvasObj != null)
        {
            overlayCanvas = canvasObj.GetComponent<Canvas>();
            imageObject = imageComponent != null ? imageComponent.gameObject : null;
        }

        UpdateOverlayPosition();
    }
    
    void UpdateOverlayPosition()
    {
        if (overlayCanvas == null || targetCamera == null) return;
        overlayCanvas.transform.position = targetCamera.transform.position + targetCamera.transform.forward * imageDistance;
        overlayCanvas.transform.rotation = Quaternion.LookRotation(targetCamera.transform.forward);
    }
    
    void ApplySelectedBackground()
    {
        int index = (int)selectedBackground;
        
        // Sprite'ı overlay'e uygula
        if (imageComponent != null && index >= 0 && index < backgroundImages.Count)
        {
            imageComponent.sprite = backgroundImages[index];
        }
        
        // GameSettings'e kaydet (Game sahnesi için)
        SetBackground(index);
        Debug.Log($"MenuManager: Background seçildi. Index={index}, Name={(index>=0 && index<backgroundImages.Count? backgroundImages[index]?.name : "<invalid>")}");
    }

    #region Scene Management

    /// <summary>
    /// Goes to game scene. Assign to Play button.
    /// </summary>
    public void GoToGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Loads a scene by name.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    #endregion

    #region Background Settings

    /// <summary>
    /// Index ile background seçer ve GameSettings'e kaydeder.
    /// Game sahnesi açıldığında CameraController otomatik uygular.
    /// </summary>
    public void SetBackground(int index)
    {
        if (index >= 0 && index < backgroundImages.Count)
        {
            Sprite sprite = backgroundImages[index];
            if (sprite != null)
            {
                GameSettings.SetBackgroundByIndex(index, sprite.name);
            }
        }
    }

    /// <summary>
    /// Sonraki background'a geçer
    /// </summary>
    public void NextBackground()
    {
        if (backgroundImages.Count > 0)
        {
            int nextIndex = (GameSettings.BackgroundIndex + 1) % backgroundImages.Count;
            SetBackground(nextIndex);
        }
    }

    /// <summary>
    /// Önceki background'a geçer
    /// </summary>
    public void PreviousBackground()
    {
        if (backgroundImages.Count > 0)
        {
            int prevIndex = (GameSettings.BackgroundIndex - 1 + backgroundImages.Count) % backgroundImages.Count;
            SetBackground(prevIndex);
        }
    }

    /// <summary>
    /// Mevcut background sprite'ını döndürür
    /// </summary>
    public Sprite GetCurrentBackgroundSprite()
    {
        if (backgroundImages.Count == 0) return null;
        int index = Mathf.Clamp(GameSettings.BackgroundIndex, 0, backgroundImages.Count - 1);
        return backgroundImages[index];
    }

    public int CurrentBackgroundIndex => GameSettings.BackgroundIndex;

    #endregion

    #region Background Music

    public AudioClip GetCurrentBackgroundMusic()
    {
        if (backgroundMusicList.Count == 0) return null;
        int index = Mathf.Clamp(GameSettings.BackgroundIndex, 0, backgroundMusicList.Count - 1);
        return backgroundMusicList[index];
    }

    #endregion

    #region Level Selection

    /// <summary>
    /// Select and play a specific level
    /// </summary>
    public void SelectLevel(int levelNumber)
    {
        GameSettings.SelectedLevel = levelNumber;
        GameSettings.LastPlayedLevel = levelNumber;
        GoToGame();
    }

    /// <summary>
    /// Play level 1
    /// </summary>
    public void PlayFirstLevel()
    {
        SelectLevel(1);
    }

    /// <summary>
    /// Get highest unlocked level
    /// </summary>
    public int GetHighestUnlockedLevel()
    {
        return GameSettings.HighestUnlockedLevel;
    }

    /// <summary>
    /// Unlock next level
    /// </summary>
    public static void UnlockNextLevel(int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        if (nextLevel > GameSettings.HighestUnlockedLevel)
        {
            GameSettings.HighestUnlockedLevel = nextLevel;
        }
    }

    /// <summary>
    /// Get selected level
    /// </summary>
    public int GetSelectedLevel()
    {
        return GameSettings.SelectedLevel;
    }

    /// <summary>
    /// Check if first time opening
    /// </summary>
    public static bool IsFirstTimeOpening()
    {
        return !GameSettings.HasOpenedBefore;
    }

    /// <summary>
    /// Mark as opened
    /// </summary>
    public static void MarkAsOpened()
    {
        GameSettings.HasOpenedBefore = true;
    }

    #endregion
}

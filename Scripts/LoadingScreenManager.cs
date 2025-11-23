using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages the loading screen scene.
/// Handles loading visual display and transitions to level selection screen.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    [Header("Loading UI")]
    public Image loadingImage;
    public Text loadingText;
    public float minimumDisplayTime = 3f;
    
    [Header("Scene Transition")]
    public string nextSceneName = "LevelSelection";
    public float fadeOutDuration = 0.5f;
    
    private CanvasGroup canvasGroup;
    private float loadingStartTime;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        loadingStartTime = Time.time;
        
        // Start the loading process
        StartCoroutine(LoadingSequence());
    }

    IEnumerator LoadingSequence()
    {
        // Simulate loading by waiting minimum display time
        // In a real game, you would preload assets/resources here
        yield return new WaitForSeconds(minimumDisplayTime);
        
        // Fade out and transition to next scene
        yield return FadeOutAndTransition();
    }

    IEnumerator FadeOutAndTransition()
    {
        float elapsed = 0f;
        
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            canvasGroup.alpha = alpha;
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        
        // Load the next scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }

    public void SetLoadingText(string text)
    {
        if (loadingText != null)
            loadingText.text = text;
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the level selection screen.
/// Displays available levels to the player and handles level selection.
/// </summary>
public class LevelSelectionManager : MonoBehaviour
{
    [Header("Level Selection UI")]
    public Transform levelButtonContainer;
    public GameObject levelButtonPrefab;
    public int maxLevels = 100;
    
    [Header("Display Settings")]
    public int levelsPerRow = 5;
    public float buttonSpacing = 10f;
    public float rowSpacing = 10f;
    
    [Header("Player Progress")]
    public Text currentLevelText;
    public Text highestUnlockedText;
    
    [Header("Scene Transition")]
    public string gameSceneName = "Game";
    public float transitionDelay = 0.3f;
    
    private int lastPlayedLevel = 1;
    private int highestUnlockedLevel = 1;
    private List<Button> levelButtons = new List<Button>();

    void Start()
    {
        LoadPlayerProgress();
        CreateLevelButtons();
        UpdateProgressDisplay();
    }

    void LoadPlayerProgress()
    {
        lastPlayedLevel = PlayerPrefs.GetInt("LastPlayedLevel", 1);
        highestUnlockedLevel = PlayerPrefs.GetInt("HighestUnlockedLevel", 1);
        
        lastPlayedLevel = Mathf.Clamp(lastPlayedLevel, 1, maxLevels);
        highestUnlockedLevel = Mathf.Clamp(highestUnlockedLevel, 1, maxLevels);
    }

    void CreateLevelButtons()
    {
        if (levelButtonContainer == null || levelButtonPrefab == null)
        {
            Debug.LogError("LevelSelectionManager: levelButtonContainer or levelButtonPrefab not assigned");
            return;
        }
        
        foreach (Transform child in levelButtonContainer)
            Destroy(child.gameObject);
        levelButtons.Clear();
        
        for (int i = 1; i <= maxLevels; i++)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelButtonContainer);
            buttonObj.name = $"Level_{i}";
            
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
                button = buttonObj.AddComponent<Button>();
            
            Text buttonText = buttonObj.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = i.ToString();
            
            int levelIndex = i;
            button.onClick.AddListener(() => SelectLevel(levelIndex));
            
            bool isUnlocked = (i <= highestUnlockedLevel);
            button.interactable = isUnlocked;
            
            if (i == lastPlayedLevel)
            {
                Image buttonImage = button.GetComponent<Image>();
                if (buttonImage != null)
                    buttonImage.color = new Color(0.7f, 1f, 0.7f);
            }
            
            levelButtons.Add(button);
        }
    }

    void UpdateProgressDisplay()
    {
        if (currentLevelText != null)
            currentLevelText.text = $"Son Oyunanan: Level {lastPlayedLevel}";
        
        if (highestUnlockedText != null)
            highestUnlockedText.text = $"Açık Level: {highestUnlockedLevel}";
    }

    public void SelectLevel(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > maxLevels)
        {
            Debug.LogWarning($"Geçersiz level: {levelNumber}");
            return;
        }
        
        PlayerPrefs.SetInt("SelectedLevel", levelNumber);
        PlayerPrefs.SetInt("LastPlayedLevel", levelNumber);
        PlayerPrefs.Save();
        
        StartCoroutine(TransitionToGame());
    }

    public void PlayLastLevel()
    {
        SelectLevel(lastPlayedLevel);
    }

    IEnumerator TransitionToGame()
    {
        yield return new WaitForSeconds(transitionDelay);
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    public static void UnlockNextLevel(int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        int highestUnlocked = PlayerPrefs.GetInt("HighestUnlockedLevel", 1);
        
        if (nextLevel > highestUnlocked)
        {
            PlayerPrefs.SetInt("HighestUnlockedLevel", nextLevel);
            PlayerPrefs.Save();
        }
    }

    public static void SaveLastPlayedLevel(int levelNumber)
    {
        PlayerPrefs.SetInt("LastPlayedLevel", levelNumber);
        PlayerPrefs.Save();
    }
}

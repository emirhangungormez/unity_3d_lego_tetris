using UnityEngine;

/// <summary>
/// Oyun genelinde kalıcı ayarları yönetir.
/// Tüm sahnelerde erişilebilir statik sınıf.
/// PlayerPrefs üzerinden veri saklar.
/// </summary>
public static class GameSettings
{
    // PlayerPrefs Keys
    private const string KEY_BACKGROUND_NAME = "BackgroundName";
    private const string KEY_BACKGROUND_INDEX = "BackgroundIndex";
    private const string KEY_MUSIC_VOLUME = "MusicVolume";
    private const string KEY_SFX_VOLUME = "SFXVolume";
    private const string KEY_HIGHEST_UNLOCKED_LEVEL = "HighestUnlockedLevel";
    private const string KEY_LAST_PLAYED_LEVEL = "LastPlayedLevel";
    private const string KEY_SELECTED_LEVEL = "SelectedLevel";
    private const string KEY_HAS_OPENED_BEFORE = "HasOpenedBefore";

    #region Background Settings

    /// <summary>
    /// Mevcut background adını döndürür (örn: "White", "Black")
    /// </summary>
    public static string BackgroundName
    {
        get => PlayerPrefs.GetString(KEY_BACKGROUND_NAME, "White");
        set
        {
            PlayerPrefs.SetString(KEY_BACKGROUND_NAME, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Mevcut background index'ini döndürür
    /// </summary>
    public static int BackgroundIndex
    {
        get => PlayerPrefs.GetInt(KEY_BACKGROUND_INDEX, 0);
        set
        {
            PlayerPrefs.SetInt(KEY_BACKGROUND_INDEX, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Background'u index ile değiştirir ve ismini de günceller
    /// </summary>
    public static void SetBackgroundByIndex(int index, string name)
    {
        BackgroundIndex = index;
        BackgroundName = name;
    }

    #endregion

    #region Audio Settings

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, 0.5f);
        set
        {
            PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    public static float SFXVolume
    {
        get => PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1f);
        set
        {
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    #endregion

    #region Level Progress

    public static int HighestUnlockedLevel
    {
        get => PlayerPrefs.GetInt(KEY_HIGHEST_UNLOCKED_LEVEL, 1);
        set
        {
            if (value > HighestUnlockedLevel)
            {
                PlayerPrefs.SetInt(KEY_HIGHEST_UNLOCKED_LEVEL, value);
                PlayerPrefs.Save();
            }
        }
    }

    public static int LastPlayedLevel
    {
        get => PlayerPrefs.GetInt(KEY_LAST_PLAYED_LEVEL, 1);
        set
        {
            PlayerPrefs.SetInt(KEY_LAST_PLAYED_LEVEL, value);
            PlayerPrefs.Save();
        }
    }

    public static int SelectedLevel
    {
        get => PlayerPrefs.GetInt(KEY_SELECTED_LEVEL, 1);
        set
        {
            PlayerPrefs.SetInt(KEY_SELECTED_LEVEL, value);
            PlayerPrefs.Save();
        }
    }

    #endregion

    #region First Time Check

    public static bool HasOpenedBefore
    {
        get => PlayerPrefs.GetInt(KEY_HAS_OPENED_BEFORE, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(KEY_HAS_OPENED_BEFORE, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Tüm ayarları sıfırlar (debug/test için)
    /// </summary>
    public static void ResetAllSettings()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    #endregion
}

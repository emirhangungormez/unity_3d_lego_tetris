using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [Header("Audio Clips")]
    public AudioClip brickSnapSound;
    public AudioClip[] layerBreakSounds;
    public AudioClip allBricksSettledSound;
    
    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource musicSource;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    
    [Header("Randomization")]
    [Range(0.8f, 1.2f)] public float minPitch = 0.9f;
    [Range(0.8f, 1.2f)] public float maxPitch = 1.1f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }
        
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
        }
        
        UpdateVolumes();
    }
    
    public void UpdateVolumes()
    {
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (musicSource != null) musicSource.volume = musicVolume;
    }
    
    public void PlayBrickSnap()
    {
        if (brickSnapSound != null)
            PlayRandomizedSound(brickSnapSound, sfxVolume);
    }
    
    public void PlayLayerCompleteExplosion()
    {
        if (layerBreakSounds != null && layerBreakSounds.Length > 0)
        {
            AudioClip randomSound = layerBreakSounds[Random.Range(0, layerBreakSounds.Length)];
            PlayRandomizedSound(randomSound, sfxVolume);
        }
    }
    
    public void PlayAllBricksSettled()
    {
        if (allBricksSettledSound != null)
            PlayRandomizedSound(allBricksSettledSound, sfxVolume * 0.8f);
    }
    
    private void PlayRandomizedSound(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        
        float randomPitch = Random.Range(minPitch, maxPitch);
        sfxSource.pitch = randomPitch;
        sfxSource.PlayOneShot(clip, volume);
        sfxSource.pitch = 1f;
    }
    
    public void PlayMusic(AudioClip musicClip)
    {
        if (musicClip != null && musicSource != null)
        {
            musicSource.clip = musicClip;
            musicSource.Play();
        }
    }
    
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }
    
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
    
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }
}
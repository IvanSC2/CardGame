using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    [Tooltip("AudioSource dedicado solo a la música (con Loop).")]
    public AudioSource musicSource;
    [Tooltip("AudioSource dedicado a los efectos de sonido.")]
    public AudioSource sfxSource;

    [Header("Música")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;

    [Header("SFX UI")]
    public AudioClip buttonGenericClick;
    public AudioClip buttonActionClick; // Play, Join, Leave
    public AudioClip shopSuccess;

    [Header("SFX Gameplay")]
    public AudioClip cardDrop;
    public AudioClip loseLife;

    [Header("Configuración Interna")]
    [Tooltip("Volumen original de la música (0 a 1).")]
    [Range(0f, 1f)] public float defaultMusicVolume = 0.5f;
    [Tooltip("Volumen reducido cuando hay un menú superpuesto.")]
    [Range(0f, 1f)] public float lowMusicVolume = 0.15f;

    // Preferencias de usuario
    private bool isMusicMuted = false;
    private bool isSfxMuted = false;

    private Coroutine musicFadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPreferences();
            SetupSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupSources()
    {
        // Si no se asignaron en el inspector, los creamos al vuelo
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        AplicarMuteoAMotores();
    }

    private void LoadPreferences()
    {
        isMusicMuted = PlayerPrefs.GetInt("MuteMusic", 0) == 1;
        isSfxMuted = PlayerPrefs.GetInt("MuteSFX", 0) == 1;
    }

    private void AplicarMuteoAMotores()
    {
        if (musicSource != null) musicSource.mute = isMusicMuted;
        if (sfxSource != null) sfxSource.mute = isSfxMuted;
    }

    // ==========================================
    // CONTROLES DE MÚSICA
    // ==========================================

    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }

    public void PlayGameMusic()
    {
        PlayMusic(gameMusic);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;

        // Si ya está sonando esta canción, no la reiniciamos
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
        musicSource.clip = clip;
        musicSource.volume = defaultMusicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    public void FadeOutMusic(float duration = 1f)
    {
        if (musicSource == null || !musicSource.isPlaying) return;
        if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
        musicFadeCoroutine = StartCoroutine(FadeRoutine(0f, duration, true));
    }

    /// <summary>
    /// Baja el volumen temporalmente (Audio Ducking)
    /// Útil para menús de pausa o game over.
    /// </summary>
    public void SetMusicLowVolume(bool isLow)
    {
        if (musicSource == null || !musicSource.isPlaying) return;
        
        if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
        
        float targetVol = isLow ? lowMusicVolume : defaultMusicVolume;
        musicFadeCoroutine = StartCoroutine(FadeRoutine(targetVol, 0.5f, false));
    }

    private IEnumerator FadeRoutine(float targetVolume, float duration, bool stopAtEnd)
    {
        float startVolume = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled para que funcione aunque el juego esté en pausa
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        musicSource.volume = targetVolume;
        if (stopAtEnd) musicSource.Stop();
    }

    // ==========================================
    // CONTROLES DE SFX
    // ==========================================

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null || isSfxMuted) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    public void PlayButtonGeneric()
    {
        PlaySFX(buttonGenericClick);
    }

    public void PlayButtonAction()
    {
        PlaySFX(buttonActionClick);
    }

    public void PlayShopSuccess()
    {
        PlaySFX(shopSuccess);
    }

    // ==========================================
    // PREFERENCIAS (Para el menú de ajustes)
    // ==========================================

    public bool IsMusicMuted => isMusicMuted;
    public bool IsSfxMuted => isSfxMuted;

    public void ToggleMusic(bool isMuted)
    {
        isMusicMuted = isMuted;
        PlayerPrefs.SetInt("MuteMusic", isMuted ? 1 : 0);
        PlayerPrefs.Save();
        AplicarMuteoAMotores();
    }

    public void ToggleSFX(bool isMuted)
    {
        isSfxMuted = isMuted;
        PlayerPrefs.SetInt("MuteSFX", isMuted ? 1 : 0);
        PlayerPrefs.Save();
        AplicarMuteoAMotores();
    }
}

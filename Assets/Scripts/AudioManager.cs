using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    [Header("Music")]
    public AudioClip mainMusic;
    [Range(0f, 1f)] public float musicVolumeScale = 0.9f;
    [Header("SFX")]
    public AudioClip sfxThruster;
    public AudioClip sfxPerfectLanding;
    public AudioClip sfxCrash;
    public AudioClip sfxCountdown;
    public AudioClip sfxCountdownStart;
    [Min(1)] public int maxSfxSources = 8;
    public AudioSource sourcePrefab;
    public AudioSource musicSource;
    [Header("Refill")]
    public AudioSource refillSource;
    [Range(0f, 1f)] public float refillVolume = 0.28f;
    [Min(0.01f)] public float refillFadeDuration = 0.15f;

    float sfxVolume;
    float musicVolume;
    float refillBlend;
    readonly List<AudioSource> sfxPool = new();
    readonly Dictionary<AudioSource, float> volumeScales = new();

    void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void Init()
    {
        var data = SaveLoadManager.Instance.Data ?? new SaveGame();
        sfxVolume = data.volSfx;
        musicVolume = data.volMusic;
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume * musicVolumeScale;
        refillSource.volume = 0f;
        refillBlend = 0f;
        refillSource.Stop();
    }

    void Update()
    {
        if (!refillSource) return;
        bool refilling = GameController.Instance && GameController.Instance.IsRefilling
            && GameController.Instance.CanRefill && Time.timeScale > 0f;
        refillBlend = Mathf.MoveTowards(refillBlend, refilling ? 1f : 0f,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, refillFadeDuration));
        refillSource.volume = refillBlend * sfxVolume * refillVolume;
        if (refilling && refillSource.volume > 0f && !refillSource.isPlaying) refillSource.Play();
        else if (refillSource.volume <= 0.001f) { refillSource.volume = 0f; refillSource.Stop(); }
    }

    AudioSource CreateSource(string sourceName)
    {
        var source = Instantiate(sourcePrefab, transform);
        source.name = sourceName;
        volumeScales[source] = 1f;
        return source;
    }

    public AudioSource CreateThrusterSound()
    {
        var source = CreateSource("ThrusterSound");
        source.clip = sfxThruster;
        source.loop = true;
        source.volume = sfxVolume;
        return source;
    }

    public void ReleaseSound(AudioSource source)
    {
        if (!source) return;
        volumeScales.Remove(source);
        sfxPool.Remove(source);
        source.Stop();
        Destroy(source.gameObject);
    }

    public AudioSource PlaySound(AudioClip audioClip) => PlaySound(audioClip, 1f);

    public AudioSource PlaySound(AudioClip clip, float volumeScale = 1f, float pitch = 1f, bool loop = false)
    {
        if (!clip) return null;
        var source = GetOrCreateSfxSource();
        if (!source) return null;
        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.pitch = pitch;
        volumeScales[source] = Mathf.Clamp01(volumeScale);
        source.volume = volumeScales[source] * sfxVolume;
        source.Play();
        return source;
    }

    public void PlayMusic(AudioClip clip, float pitch = 1f, bool loop = true)
    {
        if (!musicSource || !clip) return;
        musicSource.pitch = pitch;
        musicSource.loop = loop;
        musicSource.volume = musicVolume * musicVolumeScale;
        if (musicSource.clip != clip) musicSource.clip = clip;
        if (!musicSource.isPlaying) musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource) musicSource.Stop();
    }

    public void SetSfxVolume(float value, bool save = true)
    {
        sfxVolume = Mathf.Clamp01(value);
        foreach (var entry in volumeScales)
            if (entry.Key) entry.Key.volume = entry.Value * sfxVolume;
        if (!save) return;
        SaveLoadManager.Instance.Data.volSfx = sfxVolume;
        SaveLoadManager.Instance.Save();
    }

    public void SetMusicVolume(float value, bool save = true)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource) musicSource.volume = musicVolume * musicVolumeScale;
        if (!save) return;
        SaveLoadManager.Instance.Data.volMusic = musicVolume;
        SaveLoadManager.Instance.Save();
    }

    public float GetSfxVolume() => sfxVolume;
    public float GetMusicVolume() => musicVolume;

    AudioSource GetOrCreateSfxSource()
    {
        sfxPool.RemoveAll(source => !source);
        foreach (var source in sfxPool)
            if (!source.isPlaying) return source;
        if (sfxPool.Count >= Mathf.Max(1, maxSfxSources)) return sfxPool[0];
        var created = CreateSource("SFX_" + sfxPool.Count);
        sfxPool.Add(created);
        return created;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

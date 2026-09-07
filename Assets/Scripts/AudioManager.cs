using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Music")]
    public AudioClip mainMusic;
    public AudioClip crashedMusic;
    public AudioClip landedMusic;

    [Header("SFX")]
    public AudioClip sfxThruster;
    public AudioClip sfxPerfectLanding;
    public AudioClip sfxCrash;
    public AudioClip sfxCountdown;
    public AudioClip sfxCountdownStart;
    public int maxSfxSources = 8;

    [Header("Defaults")]
    [Range(0f, 1f)] public float defaultSfxVolume = 0.8f;
    [Range(0f, 1f)] public float defaultMusicVolume = 0.6f;

    readonly List<AudioSource> sfxPool = new();
    readonly List<AudioSource> persistentSfxSources = new();

    AudioSource musicSource;
    float sfxVolume;
    float musicVolume;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Init()
    {
        SaveGame data = SaveLoadManager.Instance.Data;
        sfxVolume = Mathf.Clamp01(data?.volSfx ?? defaultSfxVolume);
        musicVolume = Mathf.Clamp01(data?.volMusic ?? defaultMusicVolume);

        if (!musicSource)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
        }

        musicSource.loop = true;
        musicSource.volume = musicVolume;
    }

    public AudioSource CreateThrusterSound()
    {
        GameObject go = new("ThrusterSound");
        go.transform.SetParent(transform, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = sfxThruster;
        source.loop = true;
        source.volume = sfxVolume;

        persistentSfxSources.Add(source);
        return source;
    }

    public AudioSource PlaySound(AudioClip clip)
        => PlaySound(clip, 1f, 1f, false);

    public AudioSource PlaySound(AudioClip clip, float volumeScale = 1f, float pitch = 1f, bool loop = false)
    {
        if (!clip) return null;

        AudioSource source = GetOrCreateSfxSource();
        if (!source) return null;

        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(volumeScale) * sfxVolume;
        source.Play();

        return source;
    }

    public void PlayMusic(AudioClip clip, float pitch = 1f, bool loop = true)
    {
        if (!musicSource) Init();
        if (!musicSource || !clip) return;

        musicSource.pitch = pitch;
        musicSource.volume = musicVolume;
        musicSource.loop = loop;

        if (musicSource.clip != clip)
            musicSource.clip = clip;

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource && musicSource.isPlaying)
            musicSource.Stop();
    }

    public void SetSfxVolume(float value, bool save = true)
    {
        float old = sfxVolume;
        sfxVolume = Mathf.Clamp01(value);
        float ratio = old <= 0.0001f ? 0f : sfxVolume / old;

        UpdateActivePoolVolumes(ratio);

        for (int i = persistentSfxSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = persistentSfxSources[i];
            if (!source)
            {
                persistentSfxSources.RemoveAt(i);
                continue;
            }

            source.volume = sfxVolume;
        }

        if (save)
        {
            SaveLoadManager.Instance.Data.volSfx = sfxVolume;
            SaveLoadManager.Instance.Save();
        }
    }

    public void SetMusicVolume(float value, bool save = true)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource) musicSource.volume = musicVolume;

        if (save)
        {
            SaveLoadManager.Instance.Data.volMusic = musicVolume;
            SaveLoadManager.Instance.Save();
        }
    }

    public float GetSfxVolume() => sfxVolume;
    public float GetMusicVolume() => musicVolume;

    void UpdateActivePoolVolumes(float ratio)
    {
        foreach (AudioSource source in sfxPool)
        {
            if (!source || !source.isPlaying) continue;
            source.volume = ratio <= 0f ? 0f : Mathf.Clamp01(source.volume * ratio);
        }
    }

    AudioSource GetOrCreateSfxSource()
    {
        foreach (AudioSource source in sfxPool)
            if (source && !source.isPlaying)
                return source;

        if (sfxPool.Count < maxSfxSources)
        {
            GameObject go = new($"SFX_{sfxPool.Count:00}");
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            sfxPool.Add(source);
            return source;
        }

        return sfxPool.Count > 0 ? sfxPool[0] : null;
    }
}

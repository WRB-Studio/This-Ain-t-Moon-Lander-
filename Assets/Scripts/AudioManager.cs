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

    float sfxVolume;
    float musicVolume;
    AudioSource musicSource;
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
        var data = SaveLoadManager.Instance.Data;
        sfxVolume = data != null ? data.volSfx : defaultSfxVolume;
        musicVolume = data != null ? data.volMusic : defaultMusicVolume;
        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
    }

    AudioSource CreateSource(string sourceName)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
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
        musicSource.volume = musicVolume;
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
        if (musicSource) musicSource.volume = musicVolume;
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

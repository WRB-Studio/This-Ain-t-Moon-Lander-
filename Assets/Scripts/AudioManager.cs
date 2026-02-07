using System;
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
    readonly List<AudioSource> sfxPool = new List<AudioSource>();

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Init()
    {
        sfxVolume = SaveLoadManager.Instance.Data.volSfx;
        musicVolume = SaveLoadManager.Instance.Data.volMusic;

        // Music source
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = musicVolume;
    }

    // -------------------- Public API --------------------

    public AudioSource CreateThrusterSound()
    {
        var go = new GameObject("ThrusterSound");
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;

        src.Stop();
        src.clip = sfxThruster;
        src.loop = true;
        src.volume = sfxVolume;

        return src;
    }

    public AudioSource PlaySound(AudioClip audioClip)
    {
        return PlaySound(audioClip, sfxVolume, 1f, false);
    }

    public AudioSource PlaySound(AudioClip clip, float volumeScale = 1f, float pitch = 1f, bool loop = false)
    {
        if (!clip) return null;

        AudioSource src = GetOrCreateSfxSource();
        if (!src) return null;

        src.Stop();
        src.clip = clip;
        src.loop = loop;
        src.pitch = pitch;
        src.volume = Mathf.Clamp01(volumeScale) * sfxVolume;
        src.Play();

        return src;
    }

    public void PlayMusic(AudioClip clip, float pitch = 1f, bool loop = true)
    {
        musicSource.pitch = pitch;
        musicSource.volume = musicVolume;

        if (musicSource.clip == clip) return;

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource.isPlaying) musicSource.Stop();
    }

    public void SetSfxVolume(float v, bool save = true)
    {
        float old = sfxVolume;
        sfxVolume = Mathf.Clamp01(v);

        float ratio = (old <= 0.0001f) ? 0f : (sfxVolume / old);

        // laufende SFX nachziehen (best-effort)
        for (int i = 0; i < sfxPool.Count; i++)
        {
            var src = sfxPool[i];
            if (!src) continue;
            if (!src.isPlaying) continue;

            src.volume = Mathf.Clamp01(src.volume * ratio);
        }

        if (save)
        {
            SaveLoadManager.Instance.Data.volSfx = sfxVolume;
            SaveLoadManager.Instance.Save();
        }
    }

    public void SetMusicVolume(float v, bool save = true)
    {
        musicVolume = Mathf.Clamp01(v);
        musicSource.volume = musicVolume;

        if (save)
        {
            SaveLoadManager.Instance.Data.volMusic = musicVolume;
            SaveLoadManager.Instance.Save();
        }
    }

    public float GetSfxVolume() => sfxVolume;
    public float GetMusicVolume() => musicVolume;

    // -------------------- Internals --------------------

    AudioSource GetOrCreateSfxSource()
    {
        // 1) freie Quelle suchen
        for (int i = 0; i < sfxPool.Count; i++)
        {
            var src = sfxPool[i];
            if (src && !src.isPlaying) return src;
        }

        // 2) wenn noch Platz: neue erstellen
        if (sfxPool.Count < maxSfxSources)
        {
            var go = new GameObject($"SFX_{sfxPool.Count:00}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;

            sfxPool.Add(src);
            return src;
        }

        // 3) sonst: fallback (überschreibt ggf. laufenden Sound)
        return sfxPool.Count > 0 ? sfxPool[0] : null;
    }
}


using UnityEngine;

public class StoryLineAudio : MonoBehaviour
{
    AudioSource voice, sound;
    public void Play(StoryNode node)
    {
        Stop(); if (node == null) return;
        if (!voice) voice = gameObject.AddComponent<AudioSource>();
        if (!sound) sound = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = sound.playOnAwake = false;
        voice.clip = Localization.CurrentLanguage == "de" ? node.voiceDE : node.voiceEN;
        if (voice.clip) voice.Play();
        if (node.sound) sound.PlayOneShot(node.sound);
    }
    public void Stop() { if (voice) voice.Stop(); if (sound) sound.Stop(); }
    void Update()
    {
        float volume = SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null ? SaveLoadManager.Instance.Data.volSfx : 1f;
        if (voice) voice.volume = volume;
        if (sound) sound.volume = volume;
    }
    void OnDisable() => Stop();
}

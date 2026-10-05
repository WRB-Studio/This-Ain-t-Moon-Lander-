using System;
using UnityEngine;

[Serializable]
public class SaveGame
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public float volMusic = 0.8f;
    public float volSfx = 1f;
    public int level = 1;
    public int BestScore;
    public int CollectedScore;
    public int selectedLanderIndex;
    public int selectedLanderId = -1;
    public SerializableDictionary<string, bool> flags = new();
    public WorldSave world;

    public bool GetFlag(string key, bool def = false)
        => flags != null && flags.TryGetValue(key, out var value) ? value : def;

    public void SetFlag(string key, bool value)
    {
        flags ??= new();
        flags[key] = value;
    }

    public void Normalize()
    {
        if (version > CurrentVersion) return;
        if (version < 2) selectedLanderId = -1;
        version = CurrentVersion;
        level = Mathf.Max(1, level);
        BestScore = Mathf.Max(0, BestScore);
        CollectedScore = Mathf.Max(0, CollectedScore);
        selectedLanderIndex = Mathf.Max(0, selectedLanderIndex);
        volMusic = float.IsNaN(volMusic) || float.IsInfinity(volMusic) ? 0.8f : Mathf.Clamp01(volMusic);
        volSfx = float.IsNaN(volSfx) || float.IsInfinity(volSfx) ? 1f : Mathf.Clamp01(volSfx);
        flags ??= new();
        if (world != null && !world.IsValid()) world = null;
    }
}

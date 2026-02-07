using System;
using UnityEngine;

[Serializable]
public class SaveGame
{
    // Versioning (falls du später Felder änderst)
    public int version = 1;

    // --- Settings ---
    public float volMusic = 0.8f;
    public float volSfx = 1f;

    public int level = 1;

    public int BestScore = 0;
    public int CollectedScore = 0;

    // --- Meta / Progress ---
    public int selectedLanderIndex = 0;

    // Flexible Flags (Seen/Unlocked/etc.)
    // Key: "LANDER_SEEN_3" -> true
    public SerializableDictionary<string, bool> flags = new();

    public bool GetFlag(string key, bool def = false)
        => flags.TryGetValue(key, out var v) ? v : def;

    public void SetFlag(string key, bool value)
        => flags[key] = value;
}

using System;

[Serializable]
public class SaveGame
{
    public int version = 2;

    public float volMusic = 0.8f;
    public float volSfx = 1f;

    public int level = 1;
    public int BestScore;
    public int CollectedScore;

    // Keep the old index for backwards compatibility. New code prefers the stable id.
    public int selectedLanderIndex;
    public string selectedLanderId = "";

    public SerializableDictionary<string, bool> flags = new();

    public bool GetFlag(string key, bool def = false)
        => flags != null && flags.TryGetValue(key, out bool value) ? value : def;

    public void SetFlag(string key, bool value)
    {
        flags ??= new SerializableDictionary<string, bool>();
        flags[key] = value;
    }
}

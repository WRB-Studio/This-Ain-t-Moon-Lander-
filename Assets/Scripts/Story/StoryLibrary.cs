using UnityEngine;

public static class StoryLibrary
{
    public static StoryProject Project => Resources.Load<StoryProject>("Story/StoryProject");
    public static bool Flag(string key) => SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null && SaveLoadManager.Instance.Data.GetFlag(key);
    public static void SetFlag(string key, bool value)
    {
        if (SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null) SaveLoadManager.Instance.Data.SetFlag(key, value);
    }
    public static StorySession Session() => new(Project, Flag, SetFlag);
    public static void Save() { if (SaveLoadManager.Instance) SaveLoadManager.Instance.Save(); }
}

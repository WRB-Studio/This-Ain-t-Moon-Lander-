using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance;
    public SaveGame Data { get; private set; }
    const string FILE_NAME = "savegame.json";
    internal string StorageDirectory { get; set; }
    string DirectoryPath => StorageDirectory ?? Application.persistentDataPath;
    string PathFile => Path.Combine(DirectoryPath, FILE_NAME);
    bool preserveBackup;
    bool loaded;

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
        if (!loaded) Load();
    }

    public void NewGame()
    {
        Data = new SaveGame();
        loaded = true;
        Save();
    }

    public void Save()
    {
        if (!loaded || Data == null) return;
        if (Data.version > SaveGame.CurrentVersion)
        {
            Debug.LogWarning("Savegame belongs to a newer game version; it will not be overwritten.", this);
            return;
        }
        string temporary = PathFile + ".tmp";
        string backup = PathFile + ".bak";
        try
        {
            Data.Normalize();
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(temporary, JsonUtility.ToJson(Data, true));
            if (preserveBackup && File.Exists(PathFile))
            {
                File.Move(PathFile, PathFile + ".corrupt-" + DateTime.UtcNow.Ticks);
            }
            if (File.Exists(PathFile))
            {
                try { File.Replace(temporary, PathFile, backup); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(PathFile, backup, true);
                    File.Delete(PathFile);
                    File.Move(temporary, PathFile);
                }
            }
            else File.Move(temporary, PathFile);
            preserveBackup = false;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Could not save progress: " + exception.Message, this);
        }
    }

    public void Load()
    {
        bool hasPrimary = File.Exists(PathFile);
        if (TryLoad(PathFile, out var data))
        {
            Data = data;
            preserveBackup = false;
        }
        else if (TryLoad(PathFile + ".bak", out data))
        {
            Data = data;
            preserveBackup = hasPrimary;
            Debug.LogWarning("Recovered savegame from backup.", this);
        }
        else
        {
            Data = new SaveGame();
            preserveBackup = hasPrimary;
        }
        Data.Normalize();
        loaded = true;
        if (!hasPrimary) Save();
    }

    bool TryLoad(string path, out SaveGame data)
    {
        data = null;
        if (!File.Exists(path)) return false;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) return false;
            data = JsonUtility.FromJson<SaveGame>(json);
            return data != null;
        }
        catch (Exception exception) when (exception is IOException
            || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            Debug.LogWarning("Could not load savegame: " + exception.Message, this);
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(PathFile + suffix)) File.Delete(PathFile + suffix);
            Data = new SaveGame();
            preserveBackup = false;
            loaded = true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Could not delete savegame: " + exception.Message, this);
        }
    }

    void OnApplicationPause(bool pause) { if (pause) Save(); }
    void OnApplicationQuit() => Save();
    void OnDestroy() { if (Instance == this) Instance = null; }
}
#if UNITY_EDITOR

[CustomEditor(typeof(SaveLoadManager))]
public class SaveLoadManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var mgr = (SaveLoadManager)target;

        GUILayout.Space(10);

        if (GUILayout.Button("DELETE SAVEGAME"))
        {
            if (EditorUtility.DisplayDialog(
                "Delete Savegame",
                "Savegame wirklich l�schen?",
                "Ja, l�schen",
                "Abbrechen"))
            {
                mgr.Delete();
                Debug.Log("Savegame deleted");
            }
        }
    }
}
#endif

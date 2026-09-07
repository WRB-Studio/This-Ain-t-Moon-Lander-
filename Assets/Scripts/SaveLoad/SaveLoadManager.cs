using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance;

    public SaveGame Data { get; private set; }

    const string FILE_NAME = "savegame.json";
    const int CURRENT_VERSION = 2;

    string PathFile => Path.Combine(Application.persistentDataPath, FILE_NAME);

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Init() => Load();

    public void NewGame()
    {
        Data = new SaveGame();
        Save();
    }

    public void Save()
    {
        Data ??= new SaveGame();
        Data.version = CURRENT_VERSION;

        try
        {
            string json = JsonUtility.ToJson(Data, true);
            File.WriteAllText(PathFile, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveLoadManager: Could not save game. {ex.Message}");
        }
    }

    public void Load()
    {
        if (!File.Exists(PathFile))
        {
            NewGame();
            return;
        }

        try
        {
            string json = File.ReadAllText(PathFile);
            Data = JsonUtility.FromJson<SaveGame>(json) ?? new SaveGame();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SaveLoadManager: Savegame could not be read. Starting with defaults. {ex.Message}");
            Data = new SaveGame();
        }

        if (Migrate())
            Save();
    }

    bool Migrate()
    {
        bool changed = false;

        if (Data.version < 2)
        {
            Data.selectedLanderId ??= "";
            Data.version = 2;
            changed = true;
        }

        if (Data.flags == null)
        {
            Data.flags = new SerializableDictionary<string, bool>();
            changed = true;
        }

        if (Data.selectedLanderId == null)
        {
            Data.selectedLanderId = "";
            changed = true;
        }

        return changed;
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(PathFile)) File.Delete(PathFile);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SaveLoadManager: Could not delete savegame. {ex.Message}");
        }

        Data = new SaveGame();
    }

    void OnApplicationPause(bool pause)
    {
        if (pause) Save();
    }

    void OnApplicationQuit() => Save();
}

#if UNITY_EDITOR
[CustomEditor(typeof(SaveLoadManager))]
public class SaveLoadManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SaveLoadManager manager = (SaveLoadManager)target;
        GUILayout.Space(10f);

        if (!GUILayout.Button("DELETE SAVEGAME")) return;

        if (EditorUtility.DisplayDialog(
                "Delete Savegame",
                "Savegame wirklich löschen?",
                "Ja, löschen",
                "Abbrechen"))
        {
            manager.Delete();
            Debug.Log("Savegame deleted");
        }
    }
}
#endif

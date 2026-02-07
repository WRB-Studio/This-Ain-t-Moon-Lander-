using System.IO;
using UnityEditor;
using UnityEngine;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance;

    public SaveGame Data { get; private set; }

    const string FILE_NAME = "savegame.json";
    string PathFile => Path.Combine(Application.persistentDataPath, FILE_NAME);

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Init()
    {
        Load();
    }

    public void NewGame()
    {
        Data = new SaveGame();
        Save();
    }

    public void Save()
    {
        if (Data == null) Data = new SaveGame();
        var json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(PathFile, json);
    }

    public void Load()
    {
        if (!File.Exists(PathFile))
        {
            Data = new SaveGame();
            Save(); // initial anlegen
            return;
        }

        var json = File.ReadAllText(PathFile);
        Data = JsonUtility.FromJson<SaveGame>(json) ?? new SaveGame();
    }

    public void Delete()
    {
        if (File.Exists(PathFile)) File.Delete(PathFile);
        Data = new SaveGame();
    }

    void OnApplicationPause(bool pause) { if (pause) Save(); }
    void OnApplicationQuit() => Save();
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
                "Savegame wirklich löschen?",
                "Ja, löschen",
                "Abbrechen"))
            {
                mgr.Delete();
                Debug.Log("Savegame deleted");
            }
        }
    }
}
#endif
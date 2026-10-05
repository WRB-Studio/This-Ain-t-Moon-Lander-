using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RandomLandscape))]
public class RandomLandscapeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var terrain = (RandomLandscape)target;
        LandingPadPlacer pad = null;
        foreach (var candidate in Object.FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != terrain.gameObject.scene) continue;
            pad = candidate;
            break;
        }

        EditorGUILayout.Space();
        if (!pad)
            EditorGUILayout.HelpBox("In derselben Szene muss ein LandingPadPlacer vorhanden sein.", MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode
            || EditorUtility.IsPersistent(terrain) || !pad))
        {
            if (GUILayout.Button("Gelände + Landeplatz erzeugen", GUILayout.Height(30f)))
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Generate terrain and landing pad");
                Undo.RegisterFullObjectHierarchyUndo(terrain.gameObject, "Generate terrain");
                Undo.RegisterFullObjectHierarchyUndo(pad.gameObject, "Place landing pad");
                terrain.GenerateNewLevel();
                pad.SetRandomPlaceForPad();
                EditorUtility.SetDirty(terrain);
                EditorUtility.SetDirty(pad);
                EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                Undo.CollapseUndoOperations(group);
                EditorGUIUtility.PingObject(pad.gameObject);
                SceneView.lastActiveSceneView?.LookAt(pad.transform.position);
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}

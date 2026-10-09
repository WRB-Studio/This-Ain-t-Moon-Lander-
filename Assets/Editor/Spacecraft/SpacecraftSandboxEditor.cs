using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpacecraftSandbox))]
public class SpacecraftSandboxEditor : Editor
{
    Editor shipEditor;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var sandbox = (SpacecraftSandbox)target;
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Steuerung des aktiven Schiffs", EditorStyles.boldLabel);
        if (!Application.isPlaying || !sandbox.SelectedShip)
        {
            EditorGUILayout.HelpBox("Im Play Mode erscheinen hier die Werte des ausgewählten Schiffs. Außerhalb des Play Mode das Schiff oder sein Prefab auswählen.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField(sandbox.SelectedShip.displayName, EditorStyles.boldLabel);
        if (GUILayout.Button("Aktives Schiff in der Hierarchy auswählen")) Selection.activeGameObject = sandbox.SelectedShip.gameObject;
        CreateCachedEditor(sandbox.SelectedShip, typeof(AssistedSpacecraftEditor), ref shipEditor);
        shipEditor.OnInspectorGUI();
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
    void OnDisable() { if (shipEditor) DestroyImmediate(shipEditor); }
}

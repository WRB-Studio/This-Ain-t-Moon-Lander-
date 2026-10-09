using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StationResident)), CanEditMultipleObjects]
public class StationResidentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("residentId"));
        var mode = serializedObject.FindProperty("patrolMode");
        EditorGUILayout.PropertyField(mode);
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)StationResident.PatrolMode.MinMaxX)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxX"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("floorY"), new GUIContent("Initial Floor Y", "Bodenhöhe bei der ersten Platzierung; danach übernimmt die Physik."));
        }
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)StationResident.PatrolMode.Waypoints)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("waypoints"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("waypointTraversal"));
            EditorGUILayout.HelpBox("Leere GameObjects auf Bodenhöhe setzen und in Laufreihenfolge zuweisen. Punkte vor und nach Treppen setzen; keine automatische Wegfindung oder Sprünge.", MessageType.Info);
        }
        DrawPropertiesExcluding(serializedObject, "m_Script", "residentId", "patrolMode", "minX", "maxX", "floorY", "waypoints", "waypointTraversal");
        serializedObject.ApplyModifiedProperties();
    }
}

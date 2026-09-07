#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

public class TestingManager : MonoBehaviour
{
    [Header("Editor Behavior")]
    public bool autoFocus = true;
    public bool autoSelect;

    [Header("Highlight Gizmo")]
    public float highlightSeconds = 2f;
    public float highlightRadius = 2f;

    [Header("References")]
    public LandingPadPlacer landingPadPlacer;
    public LanderController lander;

#if UNITY_EDITOR
    [HideInInspector] public GameObject highlightObject;
    [HideInInspector] public float highlightUntil;

    void OnValidate()
    {
        if (!landingPadPlacer)
            landingPadPlacer = FindFirstObjectByType<LandingPadPlacer>();

        if (!lander)
            lander = LanderController.Active ? LanderController.Active : FindFirstObjectByType<LanderController>();
    }

    void OnDrawGizmos()
    {
        if (!highlightObject || Time.realtimeSinceStartup > highlightUntil) return;

        float pulse = 1f + Mathf.Sin(Time.realtimeSinceStartup * 10f) * 0.15f;
        float radius = highlightRadius * pulse;

        Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
        Gizmos.DrawWireSphere(highlightObject.transform.position, radius);
        Gizmos.DrawWireSphere(highlightObject.transform.position, radius * 0.6f);
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(TestingManager))]
public class TestingManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TestingManager manager = (TestingManager)target;

        GUILayout.Space(10f);

        GUI.enabled = manager.landingPadPlacer;
        if (GUILayout.Button("New Random LandingPad Position"))
        {
            manager.landingPadPlacer.SetRandomPlaceForPad();
            MarkAndFocus(manager, manager.landingPadPlacer.gameObject);
        }

        GUI.enabled = manager.lander;
        if (GUILayout.Button("Set Random Lander Position"))
        {
            manager.lander.SetRandomPosition();
            MarkAndFocus(manager, manager.lander.gameObject);
        }

        GUI.enabled = true;
    }

    static void MarkAndFocus(TestingManager manager, GameObject go)
    {
        manager.highlightObject = go;
        manager.highlightUntil = Time.realtimeSinceStartup + manager.highlightSeconds;

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        SceneView.RepaintAll();

        if (manager.autoFocus)
            SceneView.lastActiveSceneView?.LookAt(go.transform.position);

        if (!manager.autoSelect) return;

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
#endif

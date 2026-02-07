#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;


public class TestingManager : MonoBehaviour
{
    [Header("Editor Behavior")]
    public bool autoFocus = true;
    public bool autoSelect = false;

    [Header("Highlight Gizmo")]
    public float highlightSeconds = 2f;
    public float highlightRadius = 2f;

    [Header("References")]
    public LandingPadPlacer landingPadPlacer;
    public LanderController lander;

#if UNITY_EDITOR
    [HideInInspector] public GameObject highlightObject;
    [HideInInspector] public float highlightUntil;

    private void OnValidate()
    {
        if (!landingPadPlacer)
            landingPadPlacer = FindFirstObjectByType<LandingPadPlacer>();

        if (!lander)
            lander = FindFirstObjectByType<LanderController>();
    }

    void OnDrawGizmos()
    {
        if (!highlightObject) return;
        if (Time.realtimeSinceStartup > highlightUntil) return;

        // Puls (optional, aber mega sichtbar)
        float pulse = 1f + Mathf.Sin(Time.realtimeSinceStartup * 10f) * 0.15f;
        float r = highlightRadius * pulse;

        Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
        Gizmos.DrawWireSphere(highlightObject.transform.position, r);
        Gizmos.DrawWireSphere(highlightObject.transform.position, r * 0.6f);
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
        var tm = (TestingManager)target;

        GUILayout.Space(10);

        GUI.enabled = tm.landingPadPlacer;
        if (GUILayout.Button("New Random LandingPad Position"))
        {
            tm.landingPadPlacer.SetRandomPlaceForPad();
            tm.landingPadPlacer.PlacePad();

            MarkDirty(tm.landingPadPlacer.gameObject, tm);
            Highlight(tm, tm.landingPadPlacer.gameObject);
            FocusAndSelect(tm, tm.landingPadPlacer.gameObject);
        }

        GUI.enabled = tm.lander;
        if (GUILayout.Button("Set Random Lander Position"))
        {
            tm.lander.SetRandomPosition();

            MarkDirty(tm.lander.gameObject, tm);
            Highlight(tm, tm.lander.gameObject);
            FocusAndSelect(tm, tm.lander.gameObject);
        }

        GUI.enabled = true;
    }

    static void Highlight(TestingManager tm, GameObject go)
    {
        tm.highlightObject = go;
        tm.highlightUntil = Time.realtimeSinceStartup + tm.highlightSeconds;

        // SceneView neu zeichnen, damit man den Highlight sofort sieht
        SceneView.RepaintAll();
    }

    static void FocusAndSelect(TestingManager tm, GameObject go)
    {
        if (tm.autoFocus)
            SceneView.lastActiveSceneView?.LookAt(go.transform.position);

        if (tm.autoSelect)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }

    static void MarkDirty(GameObject go, TestingManager tm)
    {
        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(tm.gameObject.scene);
    }
}
#endif
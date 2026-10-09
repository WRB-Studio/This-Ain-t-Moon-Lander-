using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class StoryCharacterPreview : IDisposable
{
    PreviewRenderUtility renderer;
    GameObject source, instance;
    Animator[] animators = Array.Empty<Animator>();
    AnimationClip[] clips = Array.Empty<AnimationClip>();
    string[] clipNames = Array.Empty<string>();
    int selected;
    float time;
    double previous;
    public bool Playing { get; private set; }
    Bounds bounds;
    public void Dispose()
    {
        if (renderer != null) renderer.Cleanup(); renderer = null;
        source = instance = null; Playing = false;
    }
    void Load(GameObject prefab)
    {
        Dispose(); source = prefab;
        if (!prefab) return;
        renderer = new PreviewRenderUtility();
        instance = UnityEngine.Object.Instantiate(prefab); instance.hideFlags = HideFlags.HideAndDontSave;
        renderer.AddSingleGO(instance);
        animators = instance.GetComponentsInChildren<Animator>(true);
        clips = animators.Where(a => a.runtimeAnimatorController).SelectMany(a => a.runtimeAnimatorController.animationClips).Distinct().OrderBy(c => c.name).ToArray();
        clipNames = clips.Select(c => c.name).ToArray();
        var visuals = instance.GetComponentsInChildren<Renderer>(true);
        bounds = visuals.Length == 0 ? new Bounds(Vector3.zero, Vector3.one * 2) : visuals[0].bounds;
        foreach (var visual in visuals) bounds.Encapsulate(visual.bounds);
        selected = 0; time = 0; previous = EditorApplication.timeSinceStartup;
        renderer.camera.orthographic = true; renderer.camera.clearFlags = CameraClearFlags.SolidColor; renderer.camera.backgroundColor = new Color(.08f, .08f, .08f);
    }
    public void Draw(GameObject prefab)
    {
        if (source != prefab || prefab && renderer == null) Load(prefab);
        GUILayout.Label("Charakteransicht & Animationen", EditorStyles.boldLabel);
        if (!prefab) { EditorGUILayout.HelpBox("Charakter-Prefab zuweisen, um Bild und Animationen zu sehen.", MessageType.Info); return; }
        if (clips.Length > 0)
        {
            GUILayout.Space(8);
            GUILayout.Label("Animation", EditorStyles.miniBoldLabel);
            int next = EditorGUILayout.Popup(selected, clipNames, GUILayout.Height(24));
            if (next != selected) { selected = next; time = 0; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Playing ? "Pause" : "Abspielen", GUILayout.Height(30))) { Playing = !Playing; previous = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("Anfang", GUILayout.Height(30))) { time = 0; Playing = false; }
            }
            GUILayout.Space(8);
            time = EditorGUILayout.Slider("Zeit", time, 0, clips[selected].length);
        }
        else EditorGUILayout.HelpBox("Dieses Prefab enthält keine Animator-Clips.", MessageType.Info);
        Rect rect = GUILayoutUtility.GetRect(180, 360, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;
        if (clips.Length > 0)
        {
            if (Playing) { time += (float)(EditorApplication.timeSinceStartup - previous); time = Mathf.Repeat(time, Mathf.Max(.01f, clips[selected].length)); }
            previous = EditorApplication.timeSinceStartup;
            foreach (var animator in animators)
                if (animator.runtimeAnimatorController && animator.runtimeAnimatorController.animationClips.Contains(clips[selected])) clips[selected].SampleAnimation(animator.gameObject, time);
        }
        renderer.BeginPreview(rect, GUIStyle.none);
        renderer.camera.transform.position = bounds.center + Vector3.back * 10;
        renderer.camera.transform.rotation = Quaternion.identity;
        renderer.camera.orthographicSize = Mathf.Max(.5f, Mathf.Max(bounds.size.y, bounds.size.x / Mathf.Max(.1f, rect.width / rect.height)) * .65f);
        renderer.camera.nearClipPlane = .01f; renderer.camera.farClipPlane = 100;
        renderer.Render(true);
        GUI.DrawTexture(rect, renderer.EndPreview(), ScaleMode.StretchToFill, false);
    }
}

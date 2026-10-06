using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoryTextController))]
public class StoryTextControllerEditor : Editor
{
    bool showDebug = true;
    int earthLevel = 1;
    static readonly string[] discoveryLabels = { "ZeroG", "Mondanflug", "Mondlandung", "EVA / Aussteigen", "Mondlander gefunden", "UFO-Kontakt", "Stationsanflug", "Stationslandung" };

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        var story = (StoryTextController)target;
        showDebug = EditorGUILayout.Foldout(showDebug, "Story Debug (Play Mode)", true, EditorStyles.foldoutHeader);
        if (showDebug)
        {
            bool ready = Application.isPlaying && story == StoryTextController.Instance
                && SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null
                && GameController.Instance && GameController.Instance.ControlledTarget;
            EditorGUILayout.HelpBox(ready
                ? "Aktiv erlaubt die Auslösung. Entdeckt/Gelesen ändern den laufenden Fortschritt. Sprünge setzen passende Vorstufen und füllen den Tank. Änderungen am Fortschritt werden vom Autosave gespeichert."
                : "Play Mode starten und den StoryTextController in der Szene auswählen. Dann sind die Schalter und Sprünge verfügbar.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (ready) DrawProgress(story);
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Direkt zu einer Story-Phase", EditorStyles.boldLabel);
                earthLevel = Mathf.Max(1, EditorGUILayout.IntField("Erde: Level", earthLevel));
                if (GUILayout.Button("Erde / Level starten")) story.DebugStartEarth(earthLevel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("ZeroG")) story.DebugJumpTo(StoryTextController.Discovery.ZeroG);
                    if (GUILayout.Button("Mondanflug")) story.DebugJumpTo(StoryTextController.Discovery.Moon);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Mondlandung")) story.DebugJumpTo(StoryTextController.Discovery.MoonLanding);
                    if (GUILayout.Button("EVA")) story.DebugJumpTo(StoryTextController.Discovery.EVA);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Mondlander")) story.DebugJumpTo(StoryTextController.Discovery.AbandonedShip);
                    if (GUILayout.Button("Erdrückkehr")) story.DebugReturnToEarth();
                }
                if (GUILayout.Button("UFO-Anflug")) story.DebugJumpTo(StoryTextController.Discovery.UFO);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Stationsanflug")) story.DebugJumpTo(StoryTextController.Discovery.Station);
                    if (GUILayout.Button("Stationslandung")) story.DebugJumpTo(StoryTextController.Discovery.StationLanding);
                }
                if (GUILayout.Button("Aktuelle Nachricht schließen"))
                {
                    if (StationConversation.Instance && StationConversation.Instance.IsShowing) StationConversation.Instance.Close();
                    else story.ContinueDiscovery();
                }
                using (new EditorGUI.DisabledScope(!StationInterior.Instance))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Hauptgang")) StationInterior.Instance.DebugJump(StationInterior.Area.Hall);
                    if (GUILayout.Button("Registrierung")) StationInterior.Instance.DebugJump(StationInterior.Area.Registration);
                }
                using (new EditorGUI.DisabledScope(!StationConversation.Instance))
                    if (GUILayout.Button("Stationsgespräch und Bewohner zurücksetzen")) StationConversation.Instance.DebugReset();
            }
            EditorGUILayout.Space(10);
        }
        DrawDefaultInspector();
    }

    void DrawProgress(StoryTextController story)
    {
        var data = SaveLoadManager.Instance.Data;
        EditorGUILayout.LabelField("Aktuell", $"Level {GameController.Instance.level} · {GameController.Instance.Phase}");
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Story-Phase", GUILayout.MinWidth(100));
            GUILayout.Label("Aktiv", GUILayout.Width(45));
            GUILayout.Label("Entdeckt", GUILayout.Width(60));
            GUILayout.Label("Gelesen", GUILayout.Width(55));
        }
        foreach (StoryTextController.Discovery discovery in Enum.GetValues(typeof(StoryTextController.Discovery)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(discoveryLabels[(int)discovery], GUILayout.MinWidth(100));
                bool enabled = story.DebugIsEnabled(discovery);
                bool found = story.HasDiscovered(discovery);
                bool read = data.GetFlag("story.ack." + discovery);
                EditorGUI.BeginChangeCheck();
                enabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(45));
                found = EditorGUILayout.Toggle(found, GUILayout.Width(60));
                if (!found) read = false;
                read = EditorGUILayout.Toggle(read, GUILayout.Width(55));
                if (EditorGUI.EndChangeCheck()) story.DebugSetDiscovery(discovery, enabled, found || read, read);
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Erdrückkehr / Sperre", GUILayout.MinWidth(100));
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(story.DebugReturnEnabled, GUILayout.Width(45));
            bool returned = EditorGUILayout.Toggle(data.GetFlag("story.companyLanderReturned"), GUILayout.Width(60));
            bool read = EditorGUILayout.Toggle(returned && data.GetFlag("story.ack.CompanyReturn"), GUILayout.Width(55));
            if (EditorGUI.EndChangeCheck()) story.DebugSetReturn(enabled, returned || read, read);
        }
        using (new EditorGUI.DisabledScope(!StationConversation.Instance))
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Registrierungsgespräch", GUILayout.MinWidth(100));
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(StationConversation.Instance && StationConversation.Instance.DebugEnabled, GUILayout.Width(45));
            GUILayout.Space(64);
            bool completed = EditorGUILayout.Toggle(data.GetFlag("story.registrationComplete"), GUILayout.Width(55));
            if (EditorGUI.EndChangeCheck()) StationConversation.Instance.DebugSetProgress(enabled, completed);
        }
        EditorGUILayout.Space(4);
        foreach (int level in new[] { 3, 6, 9 })
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Erde: Ansage Level " + level, GUILayout.MinWidth(100));
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle(story.DebugTransmissionEnabled(level), GUILayout.Width(45));
                GUILayout.Space(64);
                bool read = EditorGUILayout.Toggle(data.GetFlag("story.earth." + level), GUILayout.Width(55));
                if (EditorGUI.EndChangeCheck()) story.DebugSetTransmission(level, enabled, read);
            }
        }
    }
}

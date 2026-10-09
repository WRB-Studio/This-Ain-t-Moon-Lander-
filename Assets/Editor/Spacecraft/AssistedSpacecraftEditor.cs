using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AssistedSpacecraft)), CanEditMultipleObjects]
public class AssistedSpacecraftEditor : Editor
{
    bool showGeometry;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Section("Schiff");
        Field("displayName", "Schiffsname"); Field("homePad", "Startpad");
        var legacy = ((AssistedSpacecraft)target).ClassicFlight;
        if (legacy)
        {
            Section("Klassische Lander-Steuerung");
            var settings = new SerializedObject(legacy); settings.Update();
            string[] names = { "thrustForce", "rotationSpeed", "rotationSmooth", "steeringDeadzone", "steeringRange", "maxSteer", "steerResponse", "maxFallSpeed", "gravityScale", "safeVerticalSpeed", "spaceTuning" };
            string[] labels = { "Haupttriebwerkskraft", "Drehgeschwindigkeit (°/s)", "Drehglättung", "Steuer-Totzone", "Mausabstand für volle Drehung", "Max. Steuerintensität", "Steuerreaktion", "Max. Falltempo", "Gravitationsfaktor", "Max. vertikales Aufsetztempo", "Originale Weltraum-Faktoren" };
            for (int i=0;i<names.Length;i++)
            {
                var property=settings.FindProperty(names[i]);
                EditorGUILayout.PropertyField(property,new GUIContent(labels[i],property.tooltip));
            }
            settings.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Schub nur entlang der Nase. Loslassen schaltet das Triebwerk ab. Keine Brems-, Schwebe-, Start- oder Landeassistenz.",MessageType.Info);
        }
        else
        {
        Field("flightAssist", "Flugassistenz");
        Section("Fliegen und Drehen");
        Field("maxSpeed", "Max. Flugtempo"); Field("acceleration", "Beschleunigung");
        Field("braking", "Bremskraft"); Field("maneuverAcceleration", "Seitlicher Schub");
        Field("turnSpeed", "Drehgeschwindigkeit (°/s)");
        Section("Starten");
        Field("takeoffSpeed", "Starttempo"); Field("takeoffClearance", "Sicherheitsabstand vor Drehung");
        }
        Section("Landen");
        if (!legacy) { Field("approachSpeed", "Seitliches Anflugtempo"); Field("approachDescentSpeed", "Sinktempo"); }
        Field("safeLandingSpeed", "Max. Aufsetztempo"); Field("safeLandingAngle", "Erlaubter Landewinkel (°)");
        if (!legacy) Field("damagingImpactSpeed", "Schadensgrenze beim Aufprall");
        Section("Treibstoff");
        Field("fuelCapacity", "Tankgröße"); Field("fuelBurnPerSecond", "Verbrauch bei vollem Schub / s");
        if (!legacy) Field("gravityFuelPerSecond", "Verbrauch fürs Schweben / s");
        showGeometry = EditorGUILayout.Foldout(showGeometry, "Geometrie und Triebwerksanzeigen", true);
        if (showGeometry) { Field("footOffset", "Fußhöhe relativ zum Zentrum"); Field("forwardAngle", "Nasenrichtung der Grafik (°)"); Field("thrustIndicators", "Triebwerksanzeigen"); }
        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying && targets.Length == 1)
        {
            var ship = (AssistedSpacecraft)target;
            EditorGUILayout.HelpBox($"Tempo: {ship.Speed:0.0}   Tank: {ship.Fuel:0.0}\nÄnderungen wirken direkt. Play-Mode-Werte anschließend im Prefab speichern.", MessageType.Info);
        }
    }
    void Field(string name, string label)
    {
        var property = serializedObject.FindProperty(name);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
    }
    static void Section(string name)
    {
        EditorGUILayout.Space(8f); EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
    }
}

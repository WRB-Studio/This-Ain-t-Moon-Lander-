using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class StoryEditorWindow
{
    string selectedDecoration, draggingDecoration, resizingDecoration;
    bool resizeLineStart;
    System.Collections.Generic.IEnumerable<StoryBoardDecoration> BoardDecorations() => project.decorations.Where(d => d.storyView == StoryView && d.chapter == Sections.ElementAtOrDefault(chapter));
    Rect DecorationBounds(StoryBoardDecoration d) => d.kind == StoryDecorationKind.Label ? new Rect(d.position, d.size)
        : Rect.MinMaxRect(Mathf.Min(d.position.x, d.end.x), Mathf.Min(d.position.y, d.end.y), Mathf.Max(d.position.x, d.end.x) + 1, Mathf.Max(d.position.y, d.end.y) + 1);
    bool DecorationOverlaps(StoryBoardDecoration d, Rect area)
    {
        if (d.kind == StoryDecorationKind.Label) return area.Overlaps(DecorationBounds(d));
        float enter = 0, exit = 1; Vector2 delta = d.end - d.position;
        bool Clip(float direction, float distance)
        {
            if (Mathf.Abs(direction) < .00001f) return distance >= 0;
            float ratio = distance / direction;
            if (direction < 0) enter = Mathf.Max(enter, ratio); else exit = Mathf.Min(exit, ratio);
            return enter <= exit;
        }
        return Clip(-delta.x, d.position.x - area.xMin) && Clip(delta.x, area.xMax - d.position.x)
            && Clip(-delta.y, d.position.y - area.yMin) && Clip(delta.y, area.yMax - d.position.y);
    }
    Vector2 ScreenPoint(Vector2 world) => (world - boardScroll) * boardZoom;
    StoryBoardDecoration HitDecoration(Vector2 mouse)
    {
        foreach (var d in BoardDecorations().Reverse())
            if (d.kind == StoryDecorationKind.Label ? new Rect(ScreenPoint(d.position), d.size * boardZoom).Contains(mouse)
                : HandleUtility.DistancePointLine(mouse, ScreenPoint(d.position), ScreenPoint(d.end)) <= Mathf.Max(5, d.lineWidth * boardZoom)) return d;
        return null;
    }
    bool DecorationResizeHit(Vector2 mouse)
    {
        var d = project.decorations.Find(d => d.id == selectedDecoration); if (d == null) return false;
        resizeLineStart = d.kind == StoryDecorationKind.Line && Vector2.Distance(mouse, ScreenPoint(d.position)) < 8;
        return resizeLineStart || Vector2.Distance(mouse, ScreenPoint(d.kind == StoryDecorationKind.Line ? d.end : d.position + d.size)) < 8;
    }
    void AddDecoration(StoryDecorationKind kind)
    {
        Undo.RegisterCompleteObjectUndo(project, "Boardelement hinzufügen");
        var position = SnapPoint(boardScroll + new Vector2(50, 50));
        var d = new StoryBoardDecoration { id = "decoration." + Guid.NewGuid().ToString("N"), chapter = Sections.ElementAtOrDefault(chapter), storyView = StoryView, kind = kind, position = position, end = SnapPoint(position + new Vector2(250, 0)) };
        project.decorations.Add(d); selectedDecoration = d.id; selected = -1;
        markedCards.Clear(); markedCards.Add((d.id, -3)); Dirty();
    }
    void DrawDecorations()
    {
        Color previousColor = Handles.color;
        foreach (var d in BoardDecorations())
        {
            if (d.kind == StoryDecorationKind.Label)
            {
                var rect = new Rect(ScreenPoint(d.position), d.size * boardZoom);
                var style = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = Mathf.Max(3, Mathf.RoundToInt(d.fontSize * boardZoom)), fontStyle = FontStyle.Bold };
                style.normal.textColor = d.color; GUI.Label(rect, d.text, style);
                if (markedCards.Contains((d.id, -3)))
                {
                    Handles.BeginGUI(); Handles.color = new Color(.3f, .72f, 1);
                    Handles.DrawAAPolyLine(1, new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y), new Vector3(rect.xMax, rect.yMax), new Vector3(rect.x, rect.yMax), new Vector3(rect.x, rect.y)); Handles.EndGUI();
                }
            }
            else
            {
                Vector2 a = ScreenPoint(d.position), b = ScreenPoint(d.end);
                Handles.BeginGUI(); Handles.color = markedCards.Contains((d.id, -3)) ? new Color(.3f, .72f, 1) : d.color;
                if (!d.dashed) Handles.DrawAAPolyLine(Mathf.Max(1, d.lineWidth * boardZoom), a, b);
                else
                {
                    float length = Vector2.Distance(a, b), dash = Mathf.Max(5, 12 * boardZoom, length / 1500);
                    Vector2 direction = length > 0 ? (b - a) / length : Vector2.zero;
                    for (float x = 0; x < length; x += dash * 1.7f) Handles.DrawAAPolyLine(Mathf.Max(1, d.lineWidth * boardZoom), a + direction * x, a + direction * Mathf.Min(length, x + dash));
                }
                Handles.EndGUI();
            }
            if (d.id == selectedDecoration)
            {
                var endpoint = ScreenPoint(d.kind == StoryDecorationKind.Line ? d.end : d.position + d.size);
                EditorGUI.DrawRect(new Rect(endpoint - Vector2.one * 4, Vector2.one * 8), new Color(.3f, .72f, 1));
                EditorGUIUtility.AddCursorRect(new Rect(endpoint - Vector2.one * 8, Vector2.one * 16), MouseCursor.ResizeUpLeft);
                if (d.kind == StoryDecorationKind.Line) EditorGUI.DrawRect(new Rect(ScreenPoint(d.position) - Vector2.one * 4, Vector2.one * 8), new Color(.3f, .72f, 1));
            }
        }
        Handles.color = previousColor;
    }
    void DecorationDetails(StoryBoardDecoration d)
    {
        if (markedCards.Count > 1) EditorGUILayout.HelpBox(markedCards.Count + " Elemente markiert. Auswahl entfernen löscht alle; die Eigenschaften unten gelten für dieses Element.", MessageType.None);
        GUILayout.Label(d.kind == StoryDecorationKind.Label ? "Titel / Label" : "Linie", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        string text = d.text; int font = d.fontSize; Vector2 size = d.size, end = d.end;
        bool dashed = d.dashed; float width = d.lineWidth;
        if (d.kind == StoryDecorationKind.Label)
        {
            text = EditorGUILayout.TextArea(text, GUILayout.MinHeight(70));
            font = EditorGUILayout.IntSlider("Schriftgröße", font, 8, 64); size = EditorGUILayout.Vector2Field("Textfläche", size);
        }
        else { dashed = EditorGUILayout.Popup("Linienstil", dashed ? 1 : 0, new[] { "Vollstrich", "Gestrichelt" }) == 1; width = EditorGUILayout.Slider("Linienstärke", width, .5f, 8); end = EditorGUILayout.Vector2Field("Endpunkt", end); }
        var position = EditorGUILayout.Vector2Field("Position", d.position); var color = EditorGUILayout.ColorField("Farbe", d.color);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RegisterCompleteObjectUndo(project, "Boardelement bearbeiten");
            d.text = text; d.fontSize = font; d.size = Vector2.Max(Vector2.one * (project.snapToGrid ? Mathf.Max(20, GridStep) : 20), SnapPoint(size)); d.end = SnapPoint(end); d.position = SnapPoint(position); d.color = color; d.dashed = dashed; d.lineWidth = width; Dirty();
        }
        EditorGUILayout.HelpBox("Eckpunkt ziehen verändert die Textfläche; bei Linien lassen sich die Endpunkte ziehen. Diese Elemente dienen nur der Entwicklungsübersicht.", MessageType.None);
        if (GUILayout.Button(markedCards.Count > 1 ? "Auswahl entfernen" : "Element entfernen")) { RemoveMarked(); GUIUtility.ExitGUI(); }
    }
    void RemoveMarked()
    {
        if (markedCards.Count == 0) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.RegisterCompleteObjectUndo(project, "Auswahl entfernen");
        var removed = markedCards.Where(c => c.answer == -1 || c.answer == -2).Select(c => c.id).ToHashSet();
        foreach (var ownerGroup in markedCards.Where(c => c.answer >= 0 && !removed.Contains(c.id)).GroupBy(c => c.id))
        {
            var owner = project.Find(ownerGroup.Key); if (owner == null) continue;
            foreach (int index in ownerGroup.Select(c => c.answer).Distinct().OrderByDescending(a => a))
                if (index < owner.choices.Count) owner.choices.RemoveAt(index);
        }
        foreach (var node in project.nodes.Where(n => !removed.Contains(n.id)))
        {
            foreach (var choice in node.choices) if (removed.Contains(choice.next)) choice.next = "";
            if (removed.Contains(node.storyStepId)) node.storyStepId = "";
            if (removed.Contains(node.eventTriggerId)) { node.eventTriggerId = ""; node.eventInfo.enabled = false; node.eventBindingEdited = true; }
        }
        project.nodes.RemoveAll(n => removed.Contains(n.id));
        var decorationIds = markedCards.Where(c => c.answer == -3).Select(c => c.id).ToHashSet();
        project.decorations.RemoveAll(d => decorationIds.Contains(d.id));
        Undo.CollapseUndoOperations(group);
        markedCards.Clear(); selectionBeforeMarquee.Clear(); selected = selectedAnswer = -1;
        selectedDecoration = draggingDecoration = resizingDecoration = null;
        connectionOwner = drag = -1; marquee = panning = false;
        preview = null; StopAudio(); Dirty();
    }
    void AddTrainingTrigger()
    {
        Undo.RegisterCompleteObjectUndo(project, "Auslöser hinzufügen");
        string id = "trigger.custom." + Guid.NewGuid().ToString("N");
        var node = new StoryNode { id = id, title = "Trainingsstart", triggerOnly = true, chapter = Sections.ElementAtOrDefault(chapter), position = SnapPoint(boardScroll + new Vector2(40, 40)), answerBoardLayout = true,
            eventInfo = new StoryEventInfo { enabled = true, kind = StoryEventKind.LevelStart, eventId = "level-start", pool = id, fromLevel = 1, toLevel = 1, random = true, description = "Eigener Trainingsauslöser für Level 1–9." } };
        project.nodes.Add(node); selected = project.nodes.Count - 1; selectedAnswer = -2; selectedDecoration = null;
        markedCards.Clear(); markedCards.Add((id, -2)); Dirty();
    }
    void TriggerDetails(SerializedProperty property, StoryNode trigger)
    {
        GUILayout.Label("Gemeinsamer Auslöser", EditorStyles.boldLabel);
        InputField(property.FindPropertyRelative("title"));
        EditorGUILayout.HelpBox(EventSummary(trigger.eventInfo) + "\n" + trigger.eventInfo.description, MessageType.Info);
        if (string.IsNullOrEmpty(trigger.sourcePath))
        {
            var info = property.FindPropertyRelative("eventInfo");
            int from = EditorGUILayout.IntSlider("Ab Level", info.FindPropertyRelative("fromLevel").intValue, 1, 9);
            int to = EditorGUILayout.IntSlider("Bis Level", info.FindPropertyRelative("toLevel").intValue, from, 9);
            info.FindPropertyRelative("fromLevel").intValue = from; info.FindPropertyRelative("toLevel").intValue = to;
            foreach (var field in new[] { "random", "requiredFlag", "blockedFlag" }) EditorGUILayout.PropertyField(info.FindPropertyRelative(field));
        }
        property.serializedObject.ApplyModifiedProperties();
        GUILayout.Space(12); GUILayout.Label("Verbundene Texte", EditorStyles.boldLabel);
        foreach (var text in project.nodes.Where(n => !n.triggerOnly && n.eventTriggerId == trigger.id).ToList())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(NodeTitle(text))) { OpenNode(text); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Lösen", GUILayout.Width(55)))
                { Undo.RegisterCompleteObjectUndo(project, "Text vom Auslöser lösen"); text.eventTriggerId = ""; text.eventInfo.enabled = false; text.eventBindingEdited = true; Dirty(); }
            }
        }
        GUILayout.Label("Vom Verbindungspunkt auf eine Textkarte ziehen. Bei Zufallsauswahl ist jeder verbundene Text eine mögliche Variante.", EditorStyles.wordWrappedLabel);
    }
}

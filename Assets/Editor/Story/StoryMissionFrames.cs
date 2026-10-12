using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class StoryEditorWindow
{
    Dictionary<string, Rect> missionDropZones = new();
    bool missionCardsMoved;
    string PlanningChapter => CurrentMission != null ? CurrentMission.chapter ?? "" : Sections.ElementAtOrDefault(chapter) ?? "";

    string PlanningMissionOf(StoryNode node)
    {
        if (node.planningScopeAssigned) return node.planningMissionId ?? "";
        string id = project.conversations.Find(c => c.id == node.conversationId)?.missionId ?? node.storyStepId;
        return project.Find(id)?.overviewOnly == true ? id : "";
    }
    string PlanningChapterOf(StoryNode node)
    {
        var mission = project.Find(PlanningMissionOf(node));
        return mission != null ? mission.chapter ?? "" : node.planningScopeAssigned ? node.planningChapter ?? "" : node.chapter ?? "";
    }
    string DecorationMissionOf(StoryBoardDecoration decoration) => !string.IsNullOrEmpty(decoration.missionId)
        ? decoration.missionId : project.conversations.Find(c => c.id == decoration.conversationId)?.missionId ?? "";
    IEnumerable<StoryNode> ChapterMissions() => project.nodes.Where(n => n.overviewOnly && n.chapter == PlanningChapter);
    bool MissionHasContents(string id) => project.nodes.Any(n => !n.overviewOnly && PlanningMissionOf(n) == id)
        || project.decorations.Any(d => DecorationMissionOf(d) == id);
    bool ChapterHasContents(string name) => project.nodes.Any(n => n.overviewOnly ? n.chapter == name : PlanningChapterOf(n) == name)
        || project.decorations.Any(d => (project.Find(DecorationMissionOf(d))?.chapter ?? d.chapter) == name);

    void UnassignedMissionDropZone()
    {
        Rect rect = GUILayoutUtility.GetRect(new GUIContent("Nicht zugeordnete Missionen"), EditorStyles.boldLabel, GUILayout.Height(24), GUILayout.ExpandWidth(true));
        GUI.Label(rect, "Nicht zugeordnete Missionen", EditorStyles.boldLabel);
        var payload = DragAndDrop.GetGenericData(HierarchyDragKey) as WritingHierarchyDrag;
        var e = Event.current;
        if (payload == null || payload.project != project || payload.chapter || payload.palette || !rect.Contains(e.mousePosition)) return;
        if (e.type == EventType.Repaint) EditorGUI.DrawRect(rect, new Color(.3f, .75f, 1, .25f));
        if (e.type == EventType.DragUpdated) { DragAndDrop.visualMode = DragAndDropVisualMode.Move; e.Use(); Repaint(); }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag(); DragAndDrop.SetGenericData(HierarchyDragKey, null);
            DropWritingHierarchy(payload, "", true, false); e.Use(); GUIUtility.ExitGUI();
        }
    }

    Rect MissionWorldFrame(StoryNode mission)
    {
        var bounds = new List<Rect>();
        foreach (var node in project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly && PlanningMissionOf(n) == mission.id))
        {
            if (!node.planningAnswer) bounds.Add(new Rect(node.position, new Vector2(230, 150)));
            for (int i = 0; i < node.choices.Count; i++) bounds.Add(AnswerWorldRect(node, i));
        }
        bounds.AddRange(project.decorations.Where(d => !d.storyView && DecorationMissionOf(d) == mission.id).Select(DecorationBounds));
        if (bounds.Count == 0) return new Rect(mission.position, new Vector2(480, 260));
        Vector2 min = bounds.Select(r => r.min).Aggregate(Vector2.Min) - new Vector2(28, 28);
        Vector2 max = bounds.Select(r => r.max).Aggregate(Vector2.Max) + new Vector2(28, 28);
        max = Vector2.Max(max, min + new Vector2(360, 200));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    Dictionary<string, Rect> MissionZones() => ChapterMissions().ToDictionary(m => m.id, MissionWorldFrame);
    string MissionAt(Vector2 point, Dictionary<string, Rect> zones)
    {
        if (CurrentMission != null) return CurrentMission.id;
        return zones.Where(pair => pair.Value.Contains(point)).OrderBy(pair => pair.Value.width * pair.Value.height)
            .ThenBy(pair => (pair.Value.center - point).sqrMagnitude).Select(pair => pair.Key).FirstOrDefault() ?? "";
    }
    void SetPlanningScope(StoryNode node, string missionId)
    {
        node.planningScopeAssigned = true; node.planningMissionId = missionId;
        node.planningChapter = project.Find(missionId)?.chapter ?? PlanningChapter;
    }
    void AssignCreatedCard(StoryNode node, Vector2 point) => SetPlanningScope(node, MissionAt(point, MissionZones()));

    void CaptureMissionDropZones() { missionDropZones = MissionZones(); missionCardsMoved = false; }
    void AssignMovedCards(Vector2 dropPoint)
    {
        string destination = MissionAt(dropPoint, missionDropZones);
        foreach (var card in markedCards)
        {
            var node = project.Find(card.id);
            if (node != null && (card.answer == -1 || node.planningAnswer))
            {
                SetPlanningScope(node, destination);
            }
            else if (node == null)
            {
                var decoration = project.decorations.Find(d => d.id == card.id);
                if (decoration == null) continue;
                decoration.missionId = destination;
                decoration.chapter = project.Find(decoration.missionId)?.chapter ?? PlanningChapter;
                decoration.conversationId = "";
            }
        }
    }

    void DrawMissionFrames()
    {
        if (CurrentMission != null) return;
        var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = Mathf.Max(7, Mathf.RoundToInt(13 * boardZoom)) };
        style.normal.textColor = new Color(.7f, .82f, .94f);
        var palette = DragAndDrop.GetGenericData(HierarchyDragKey) as WritingHierarchyDrag;
        bool paletteDrag = palette != null && palette.project == project && palette.palette;
        Vector2 pointer = boardScroll + Event.current.mousePosition / boardZoom;
        string target = dragRecorded ? MissionAt(pointer, missionDropZones) : paletteDrag ? MissionAt(SnapPoint(pointer), MissionZones()) : "";
        foreach (var mission in ChapterMissions())
        {
            Rect world = MissionWorldFrame(mission);
            Rect box = new((world.position - boardScroll) * boardZoom, world.size * boardZoom);
            Color border = target == mission.id ? new Color(.3f, .8f, 1) : new Color(.35f, .53f, .7f, .7f);
            EditorGUI.DrawRect(box, new Color(.2f, .35f, .5f, .06f));
            EditorGUI.DrawRect(new Rect(box.x, box.y, box.width, 1), border);
            EditorGUI.DrawRect(new Rect(box.x, box.yMax - 1, box.width, 1), border);
            EditorGUI.DrawRect(new Rect(box.x, box.y, 1, box.height), border);
            EditorGUI.DrawRect(new Rect(box.xMax - 1, box.y, 1, box.height), border);
            float titleHeight = Mathf.Max(14, 22 * boardZoom);
            GUI.Label(new Rect(box.x + 6, box.y - titleHeight - 3, Mathf.Max(120, box.width), titleHeight), NodeTitle(mission), style);
        }
    }
}

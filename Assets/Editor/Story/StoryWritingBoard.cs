using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public partial class StoryEditorWindow
{
    [SerializeField] string writingMissionId = "", writingConversationId = "";
    [SerializeField] List<string> expandedWritingChapters = new();
    bool writingAdvanced;
    const string HierarchyDragKey = "MoonLander.StoryHierarchy";
    sealed class WritingHierarchyDrag
    {
        public StoryProject project;
        public string id;
        public bool chapter;
        public bool palette;
    }
    WritingHierarchyDrag hierarchyPointer;
    Vector2 hierarchyPointerStart;
    [SerializeField] float planningTextHeight = 170;
    Vector2 planningTextScroll;
    string planningTextScrollKey = "";
    StoryConversation CurrentConversation => project.conversations.Find(c => c.id == writingConversationId);
    StoryNode CurrentMission => project.nodes.Find(n => n.overviewOnly && n.id == writingMissionId);
    static string KindLabel(StoryConversationKind kind) => kind == StoryConversationKind.Personal ? "Persönlich"
        : kind == StoryConversationKind.Radio ? "Funk" : "Ereignismeldung";
    string WritingBreadcrumb() => string.Join("  /  ", new[] { Sections.ElementAtOrDefault(chapter), CurrentMission == null ? null : NodeTitle(CurrentMission) }.Where(s => !string.IsNullOrEmpty(s)));

    IEnumerable<StoryNode> WritingNodes() => project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly
        && (!n.planningAnswer || n.choices.Count > 0)
        && (CurrentMission != null ? PlanningMissionOf(n) == writingMissionId : PlanningChapterOf(n) == PlanningChapter));
    bool WritingMatch(StoryConversation conversation)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        string content = conversation.title + " " + conversation.location + " " + KindLabel(conversation.kind);
        foreach (var node in project.nodes.Where(n => n.conversationId == conversation.id))
            content += " " + node.title + " " + node.purpose + " " + StoryPlanningCatalog.Text(node.textKey, "de") + " " + StoryPlanningCatalog.Text(node.textKey, "en")
                + " " + (node.speaker ? StoryPlanningCatalog.Text(node.speaker.nameKey, language) : "")
                + " " + string.Join(" ", node.choices.Select(c => StoryPlanningCatalog.Text(c.textKey, "de") + " " + StoryPlanningCatalog.Text(c.textKey, "en")));
        return content.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void WritingCreateButtons()
    {
        if (GUILayout.Button("Kapitel hinzufügen"))
        {
            Edit("Kapitel hinzufügen"); string title = "Neues Kapitel"; int number = 2;
            while (Sections.Contains(title)) title = "Neues Kapitel " + number++;
            Sections.Add(title); chapter = Sections.Count - 1; writingMissionId = writingConversationId = ""; ClearWritingSelection(); Dirty();
        }
        GUILayout.Space(5);
        if (GUILayout.Button("Mission hinzufügen"))
            {
                Edit("Mission hinzufügen"); var mission = new StoryNode { id = "mission." + Guid.NewGuid().ToString("N"),
                    title = "Neue Mission", chapter = "", overviewOnly = true, status = StoryDevelopmentStatus.Planned, position = SnapPoint(boardScroll + new Vector2(40, 40)) };
                InitializePlanningTitle(mission);
                project.nodes.Add(mission); writingMissionId = mission.id; writingConversationId = ""; ClearWritingSelection(); Dirty();
            }
    }

    bool WritingHierarchyButton(string id, string title, bool isChapter, bool selected, float height, bool palette = false)
    {
        var content = new GUIContent(title, palette ? id == "answer" ? "Frei ins Board ziehen. Anschließend über Verbindungspunkte mit einer Storykarte verbinden." : "Ins Storyboard ziehen oder zum Hinzufügen klicken." : isChapter ? "Klicken: Kapitel wählen und auf-/zuklappen. Ziehen: Reihenfolge ändern."
            : "Klicken: Missionsboard zeigen. Ziehen: Reihenfolge ändern oder in ein anderes Kapitel verschieben.");
        var buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.padding.right = Mathf.Max(buttonStyle.padding.right, 34);
        Rect rect = GUILayoutUtility.GetRect(content, buttonStyle, GUILayout.Height(height), GUILayout.ExpandWidth(true));
        int control = GUIUtility.GetControlID((isChapter ? "chapter." : "mission.").GetHashCode() ^ id.GetHashCode(), FocusType.Passive, rect);
        var e = Event.current;
        bool clicked = false;
        if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            hierarchyPointer = new WritingHierarchyDrag { project = project, id = id, chapter = isChapter, palette = palette };
            hierarchyPointerStart = e.mousePosition; GUIUtility.hotControl = control; e.Use();
        }
        if (GUIUtility.hotControl == control && hierarchyPointer != null)
        {
            if (e.type == EventType.MouseDrag && Vector2.Distance(e.mousePosition, hierarchyPointerStart) >= 6)
            {
                DragAndDrop.PrepareStartDrag(); DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                DragAndDrop.SetGenericData(HierarchyDragKey, hierarchyPointer); DragAndDrop.StartDrag(title);
                hierarchyPointer = null; GUIUtility.hotControl = 0; e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            { clicked = rect.Contains(e.mousePosition); hierarchyPointer = null; GUIUtility.hotControl = 0; e.Use(); }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            { hierarchyPointer = null; GUIUtility.hotControl = 0; e.Use(); }
        }
        GUI.Toggle(rect, selected, content, buttonStyle);
        Rect grip = new Rect(rect.xMax - 29, rect.y + 3, 24, rect.height - 6);
        if (e.type == EventType.Repaint)
            for (int line = 0; line < 3; line++)
                EditorGUI.DrawRect(new Rect(grip.center.x - 6, grip.center.y - 5 + line * 4, 12, 1),
                    new Color(.75f, .75f, .75f));
        GUI.Label(grip, new GUIContent("", palette ? "Ins Storyboard ziehen" : "Zum Verschieben ziehen"));
        var payload = DragAndDrop.GetGenericData(HierarchyDragKey) as WritingHierarchyDrag;
        if (payload == null || payload.project != project) return clicked;
        if (e.type == EventType.DragExited)
        { DragAndDrop.SetGenericData(HierarchyDragKey, null); Repaint(); return false; }
        if (payload.palette) return clicked;
        if (!rect.Contains(e.mousePosition) || payload.chapter && !isChapter) return clicked;
        bool after = e.mousePosition.y >= rect.center.y;
        if (e.type == EventType.Repaint)
        {
            if (!payload.chapter && isChapter) EditorGUI.DrawRect(rect, new Color(.2f, .65f, 1, .25f));
            else EditorGUI.DrawRect(new Rect(rect.x, after ? rect.yMax - 2 : rect.y, rect.width, 2), new Color(.3f, .75f, 1));
        }
        if (e.type == EventType.DragUpdated)
        { DragAndDrop.visualMode = DragAndDropVisualMode.Move; e.Use(); Repaint(); }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag(); DragAndDrop.SetGenericData(HierarchyDragKey, null);
            DropWritingHierarchy(payload, id, isChapter, after); e.Use(); GUIUtility.ExitGUI();
        }
        return clicked;
    }

    void DropWritingHierarchy(WritingHierarchyDrag payload, string targetId, bool targetChapter, bool after)
    {
        if (payload.chapter)
        {
            int source = Sections.IndexOf(payload.id), target = Sections.IndexOf(targetId);
            if (source < 0 || target < 0 || source == target) return;
            Edit("Kapitel per Ziehen verschieben");
            int destination = target + (after ? 1 : 0); Sections.RemoveAt(source);
            if (source < destination) destination--;
            Sections.Insert(destination, payload.id); chapter = destination;
            writingMissionId = writingConversationId = "";
        }
        else
        {
            var mission = project.nodes.Find(n => n.overviewOnly && n.id == payload.id);
            var target = targetChapter ? null : project.nodes.Find(n => n.overviewOnly && n.id == targetId);
            string destinationChapter = targetChapter ? targetId : target?.chapter;
            if (mission == null || destinationChapter == null || destinationChapter != "" && !Sections.Contains(destinationChapter) || mission == target) return;
            bool hasMissionContents = MissionHasContents(mission.id);
            Edit("Mission per Ziehen verschieben"); project.nodes.Remove(mission);
            int destination;
            if (target != null) destination = project.nodes.IndexOf(target) + (after ? 1 : 0);
            else
            {
                int last = project.nodes.FindLastIndex(n => n.overviewOnly && n.chapter == destinationChapter);
                destination = last >= 0 ? last + 1 : project.nodes.Count;
            }
            if (mission.chapter != destinationChapter && !hasMissionContents)
            {
                var frames = project.nodes.Where(n => n.overviewOnly && n.chapter == destinationChapter).Select(MissionWorldFrame).ToList();
                mission.position = SnapPoint(frames.Count == 0 ? boardScroll + new Vector2(40, 40)
                    : new Vector2(frames.Max(r => r.xMax) + 80, frames.Min(r => r.y)));
            }
            mission.chapter = destinationChapter; project.nodes.Insert(destination, mission);
            chapter = Mathf.Max(0, Sections.IndexOf(destinationChapter)); writingMissionId = mission.id; writingConversationId = "";
            if (!string.IsNullOrEmpty(destinationChapter) && !expandedWritingChapters.Contains(destinationChapter)) expandedWritingChapters.Add(destinationChapter);
        }
        ClearWritingSelection(); Dirty();
    }

    bool WritingPaletteDrop(Rect view)
    {
        var payload = DragAndDrop.GetGenericData(HierarchyDragKey) as WritingHierarchyDrag;
        if (payload == null || !payload.palette || payload.project != project) return false;
        var e = Event.current;
        if (e.type == EventType.DragExited)
        { DragAndDrop.SetGenericData(HierarchyDragKey, null); Repaint(); return false; }
        if (!view.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated) { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; e.Use(); }
            return false;
        }
        bool allowed = project && AssetDatabase.GetAssetPath(project) == StoryPlanningCatalog.ProjectPath
            && (Sections.Count > 0 || CurrentMission != null);
        Vector2 world = SnapPoint(boardScroll + e.mousePosition / boardZoom);
        if (e.type == EventType.Repaint && allowed)
        {
            Vector2 screen = (world - boardScroll) * boardZoom;
            Color color = new(.3f, .75f, 1);
            EditorGUI.DrawRect(new Rect(0, 0, view.width, 2), color);
            EditorGUI.DrawRect(new Rect(0, view.height - 2, view.width, 2), color);
            EditorGUI.DrawRect(new Rect(0, 0, 2, view.height), color);
            EditorGUI.DrawRect(new Rect(view.width - 2, 0, 2, view.height), color);
            EditorGUI.DrawRect(new Rect(screen.x - 6, screen.y, 13, 1), color);
            EditorGUI.DrawRect(new Rect(screen.x, screen.y - 6, 1, 13), color);
        }
        if (e.type == EventType.DragUpdated)
        { DragAndDrop.visualMode = allowed ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected; e.Use(); Repaint(); }
        else if (e.type == EventType.DragPerform)
        {
            if (allowed)
            {
                DragAndDrop.AcceptDrag(); DragAndDrop.SetGenericData(HierarchyDragKey, null);
                if (payload.id == "note") AddPlanningNote(world);
                else if (payload.id == "answer") AddFreePlanningAnswer(world);
                else AddDecoration(payload.id == "label" ? StoryDecorationKind.Label : StoryDecorationKind.Line, world);
                focusBoard = false;
            }
            e.Use(); return true;
        }
        return false;
    }

    void WritingSidebar()
    {
        sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
        WritingCreateButtons();
        GUILayout.Space(12);
        GUILayout.Label("Kapitel · Missionen", EditorStyles.boldLabel);
        search = EditorGUILayout.TextField(new GUIContent("Suchen", "Sucht Titel, Kontext, Sprecher und DE/EN-Texte in allen Gesprächen."), search);
        GUILayout.Space(8);
        for (int i = 0; i < Sections.Count; i++)
        {
            string section = Sections[i];
            var missions = project.nodes.Where(n => n.overviewOnly && n.chapter == section).ToList();
            bool sectionMatch = !string.IsNullOrWhiteSpace(search) && section.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            bool chapterOpen = expandedWritingChapters.Contains(section);
            bool chapterSelected = chapter == i && CurrentMission == null;
            if (WritingHierarchyButton(section, section, true, chapterSelected, 28))
            {
                if (!chapterOpen) expandedWritingChapters.Add(section); else expandedWritingChapters.Remove(section);
                chapter = i; writingMissionId = writingConversationId = ""; ClearWritingSelection(); GUIUtility.ExitGUI();
            }
            GUILayout.Space(4);
            if (!chapterOpen && string.IsNullOrWhiteSpace(search)) continue;
            foreach (var mission in missions)
            {
                bool missionMatch = sectionMatch || !string.IsNullOrWhiteSpace(search) && (NodeTitle(mission) + " " + mission.purpose).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
                var conversations = project.conversations.Where(c => c.missionId == mission.id && (missionMatch || WritingMatch(c))).ToList();
                if (!string.IsNullOrWhiteSpace(search) && !missionMatch && conversations.Count == 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12);
                    bool missionSelected = writingMissionId == mission.id;
                    if (WritingHierarchyButton(mission.id, NodeTitle(mission), false, missionSelected, 26))
                    {
                        chapter = i; writingMissionId = mission.id; writingConversationId = ""; ClearWritingSelection(); GUIUtility.ExitGUI();
                    }
                }
                GUILayout.Space(4);
            }
            GUILayout.Space(4);
        }
        GUILayout.Space(16);
        UnassignedMissionDropZone();
        foreach (var mission in project.nodes.Where(n => n.overviewOnly && string.IsNullOrEmpty(n.chapter)).ToList())
        {
            if (WritingHierarchyButton(mission.id, NodeTitle(mission), false, writingMissionId == mission.id, 26))
            { writingMissionId = mission.id; writingConversationId = ""; ClearWritingSelection(); GUIUtility.ExitGUI(); }
            GUILayout.Space(4);
        }
        GUILayout.Space(12);
        GUILayout.Label("Werkzeuge", EditorStyles.boldLabel);
        GUILayout.Space(6);
        GUILayout.Label("Storyelemente", EditorStyles.miniBoldLabel);
        foreach (var decoration in project.decorations.Where(d => d.storyView && d.chapter == Sections.ElementAtOrDefault(chapter)).ToList())
            if (GUILayout.Button("Planungsnotiz: " + (decoration.kind == StoryDecorationKind.Label ? decoration.text : "Linie")))
            { ClearWritingSelection(); selectedDecoration = decoration.id; markedCards.Add((decoration.id, -3)); }
        using (new EditorGUI.DisabledScope(CurrentMission == null && Sections.Count == 0))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(CurrentMission == null && Sections.Count == 0))
                    if (WritingHierarchyButton("note", "Storykarte", false, false, 26, true)) AddPlanningNote();
            using (new EditorGUI.DisabledScope(Sections.Count == 0 && CurrentMission == null))
                    if (WritingHierarchyButton("answer", "Antwortkarte", false, false, 26, true)) AddFreePlanningAnswer();
            }
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (WritingHierarchyButton("label", "Label", false, false, 26, true)) AddDecoration(StoryDecorationKind.Label);
                if (WritingHierarchyButton("line", "Linie", false, false, 26, true)) AddDecoration(StoryDecorationKind.Line);
            }
            GUILayout.Space(6);
            GridControls();
        }
        EditorGUILayout.EndScrollView();
    }

    void ClearWritingSelection()
    {
        selected = selectedAnswer = -1; selectedDecoration = null; markedCards.Clear(); connectionOwner = drag = -1;
        GUIUtility.keyboardControl = 0; focusBoard = true;
    }
    void SelectConversation(StoryConversation conversation)
    {
        writingConversationId = conversation.id; writingMissionId = conversation.missionId;
        chapter = Mathf.Max(0, Sections.IndexOf(CurrentMission?.chapter)); ClearWritingSelection();
    }
    void OpenNode(StoryNode node)
    {
        tab = 0;
        if (node.overviewOnly)
        {
            writingMissionId = node.id; writingConversationId = ""; chapter = Mathf.Max(0, Sections.IndexOf(node.chapter)); ClearWritingSelection(); return;
        }
        writingMissionId = PlanningMissionOf(node); writingConversationId = "";
        chapter = Mathf.Max(0, Sections.IndexOf(PlanningChapterOf(node)));
        selected = project.nodes.IndexOf(node); selectedAnswer = node.triggerOnly ? -2 : -1;
        markedCards.Clear(); markedCards.Add((node.id, selectedAnswer)); focusBoard = true; Repaint();
    }

    void WritingDetails()
    {
        var decoration = project.decorations.Find(d => d.id == selectedDecoration);
        if (decoration != null) { DecorationDetails(decoration); return; }
        if (selected >= 0 && selected < project.nodes.Count)
        {
            var current = project.nodes[selected]; serialized.Update();
            var node = serialized.FindProperty("nodes").GetArrayElementAtIndex(selected);
            if (!current.overviewOnly)
            {
                if (selectedAnswer >= 0 && selectedAnswer < current.choices.Count) WritingAnswer(node, current);
                else WritingText(node, current);
                return;
            }
        }
        if (CurrentMission != null) MissionDetails();
        else ChapterDetails();
    }

    void ChapterDetails()
    {
        if (Sections.Count == 0) { GUILayout.Label("Mit einem Kapitel beginnen."); return; }
        GUILayout.Label("Kapitel", EditorStyles.boldLabel);
        string old = Sections[chapter]; string title = EditorGUILayout.DelayedTextField("Name", old);
        if (!string.IsNullOrWhiteSpace(title) && title != old && !Sections.Contains(title.Trim()))
        {
            Edit("Kapitel umbenennen"); Sections[chapter] = title.Trim();
            foreach (var card in project.nodes.Where(n => !n.overviewOnly && PlanningMissionOf(n) == "" && PlanningChapterOf(n) == old))
            { card.planningScopeAssigned = true; card.planningChapter = title.Trim(); }
            foreach (var decoration in project.decorations.Where(d => DecorationMissionOf(d) == "" && d.chapter == old)) decoration.chapter = title.Trim();
            if (expandedWritingChapters.Remove(old)) expandedWritingChapters.Add(title.Trim());
            foreach (var mission in project.nodes.Where(n => n.overviewOnly && n.chapter == old)) mission.chapter = title.Trim();
            Dirty();
        }
    }

    void RemovePlanningChapter()
    {
        if (Sections.Count == 0 || ChapterHasContents(Sections[chapter])) return;
        Edit("Kapitel entfernen"); Sections.RemoveAt(chapter); chapter = Mathf.Max(0, chapter - 1);
        writingMissionId = writingConversationId = ""; ClearWritingSelection(); Dirty(); GUIUtility.ExitGUI();
    }

    void MissionDetails()
    {
        var mission = CurrentMission;
        EditorGUILayout.LabelField("Kapitel", string.IsNullOrEmpty(mission.chapter) ? "Nicht zugeordnet" : mission.chapter);
        GUILayout.Space(10);
        string draftKey = language + "/" + mission.planningTitleKey;
        if (!textDrafts.ContainsKey(draftKey)) textDrafts[draftKey] = StoryPlanningCatalog.Text(mission.planningTitleKey, language);
        textDrafts[draftKey] = EditorGUILayout.TextField("Missionsname", textDrafts[draftKey]);
    }

    void RemovePlanningMission()
    {
        var mission = CurrentMission;
        if (mission == null || MissionHasContents(mission.id)) return;
        Edit("Mission entfernen"); project.nodes.Remove(mission);
        foreach (var conversation in project.conversations.Where(c => c.missionId == mission.id)) conversation.missionId = "";
        foreach (var other in project.nodes)
        {
            if (other.storyStepId == mission.id) other.storyStepId = "";
            foreach (var choice in other.choices) if (choice.next == mission.id) choice.next = "";
        }
        writingMissionId = ""; ClearWritingSelection(); Dirty(); GUIUtility.ExitGUI();
    }
    void ConversationDetails()
    {
        var conversation = CurrentConversation; serialized.Update();
        var data = serialized.FindProperty("conversations").GetArrayElementAtIndex(project.conversations.IndexOf(conversation));
        GUILayout.Label("Gespräch", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(data.FindPropertyRelative("title"), new GUIContent("Name"));
        var missions = project.nodes.Where(n => n.overviewOnly).ToList();
        int old = missions.FindIndex(n => n.id == conversation.missionId);
        int pick = EditorGUILayout.Popup("Mission", Mathf.Max(0, old), missions.Select(n => n.chapter + " / " + NodeTitle(n)).ToArray());
        if (pick != old && pick >= 0 && pick < missions.Count) data.FindPropertyRelative("missionId").stringValue = missions[pick].id;
        var kind = data.FindPropertyRelative("kind"); kind.enumValueIndex = EditorGUILayout.Popup("Gesprächsart", kind.enumValueIndex, new[] { "Persönlich", "Funk", "Ereignismeldung" });
        EditorGUILayout.PropertyField(data.FindPropertyRelative("location"), new GUIContent("Ort / Kontext (optional)"));
        var texts = project.nodes.Where(n => n.conversationId == conversation.id && !n.triggerOnly && !n.overviewOnly).ToList();
        int start = texts.FindIndex(n => n.id == conversation.entryNodeId);
        int nextStart = EditorGUILayout.Popup("Starttext", Mathf.Max(0, start), texts.Select(NodeTitle).ToArray());
        if (nextStart >= 0 && nextStart < texts.Count && nextStart != start) data.FindPropertyRelative("entryNodeId").stringValue = texts[nextStart].id;
        if (serialized.ApplyModifiedProperties())
        { writingMissionId = conversation.missionId; chapter = Mathf.Max(0, Sections.IndexOf(CurrentMission?.chapter)); Dirty(); }
        GUILayout.Label("Art, Texte und Verbindungen beschreiben nur die Planung; sie werden nicht vom Spiel ausgeführt.", EditorStyles.wordWrappedMiniLabel);
        var siblings = project.conversations.Where(c => c.missionId == conversation.missionId).ToList();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(siblings.IndexOf(conversation) <= 0)) if (GUILayout.Button("Nach oben")) MoveConversation(-1);
            using (new EditorGUI.DisabledScope(siblings.IndexOf(conversation) >= siblings.Count - 1)) if (GUILayout.Button("Nach unten")) MoveConversation(1);
        }
        if (GUILayout.Button("Starttext bearbeiten") && texts.Count > 0) OpenNode(texts.Find(n => n.id == conversation.entryNodeId) ?? texts[0]);
        if (GUILayout.Button("Text hinzufügen")) AddWritingText();
        if (GUILayout.Button("Gespräch entfernen") && EditorUtility.DisplayDialog("Gespräch entfernen", conversation.title + " und " + texts.Count + " Texte samt Antworten entfernen? Übersetzungen bleiben im Sprachkatalog. Rückgängig mit Strg+Z.", "Entfernen", "Behalten"))
        {
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Edit("Gespräch entfernen");
            markedCards.Clear(); foreach (var text in texts) markedCards.Add((text.id, -1)); RemoveMarked();
            project.conversations.Remove(conversation); writingConversationId = ""; Dirty(); Undo.CollapseUndoOperations(group); GUIUtility.ExitGUI();
        }
    }

    void AddWritingText()
    {
        if (CurrentConversation == null) return;
        Edit("Text hinzufügen"); var conversation = CurrentConversation;
        var nodes = project.nodes.Where(n => n.conversationId == conversation.id).ToList();
        string id = "story." + Guid.NewGuid().ToString("N");
        string legacyGroup = nodes.FirstOrDefault()?.chapter ?? "Eigene Gespräche";
        if (!project.dialogueGroups.Contains(legacyGroup)) project.dialogueGroups.Add(legacyGroup);
        var node = new StoryNode { id = id, textKey = id, title = "Neuer Text", chapter = legacyGroup, storyStepId = conversation.missionId,
            conversationId = conversation.id, location = conversation.location, channel = conversation.kind == StoryConversationKind.Radio ? StoryChannel.Radio : StoryChannel.Personal,
            speaker = nodes.FirstOrDefault()?.speaker, answerBoardLayout = true,
            position = SnapPoint(nodes.Count == 0 ? Vector2.zero : nodes.Last().position + new Vector2(760, 0)) };
        InitializePlanningTitle(node);
        project.nodes.Add(node); if (string.IsNullOrEmpty(conversation.entryNodeId)) conversation.entryNodeId = node.id;
        OpenNode(node); Dirty();
    }
    void AddPlanningNote(Vector2? worldPosition = null)
    {
        if (CurrentMission == null && Sections.Count == 0) return;
        Edit("Storykarte hinzufügen");
        string id = "note." + Guid.NewGuid().ToString("N");
        var note = new StoryNode { id = id, textKey = id + ".text", title = "Neue Storykarte", chapter = PlanningChapter,
            answerBoardLayout = true, position = SnapPoint(worldPosition ?? boardScroll + new Vector2(40, 40)) };
        AssignCreatedCard(note, note.position); InitializePlanningTitle(note);
        project.nodes.Add(note); selected = project.nodes.Count - 1; selectedAnswer = -1; selectedDecoration = null;
        markedCards.Clear(); markedCards.Add((note.id, -1)); focusBoard = false; Dirty();
    }
    void InitializePlanningTitle(StoryNode node)
    {
        node.planningTitleKey = node.id + ".title";
        project.planningTexts.Add(new StoryPlanningText { key = node.planningTitleKey, de = node.title ?? "", en = "" });
    }
    void AddFreePlanningAnswer(Vector2? worldPosition = null)
    {
        if (Sections.Count == 0 && CurrentMission == null) return;
        Edit("Freie Antwortkarte hinzufügen");
        string id = "answer." + Guid.NewGuid().ToString("N");
        var card = new StoryNode { id = id, title = "Antwortkarte", planningAnswer = true,
            chapter = PlanningChapter,
            position = SnapPoint(worldPosition ?? boardScroll + new Vector2(40, 40)), answerBoardLayout = true,
            choices = new List<StoryChoice> { new StoryChoice { textKey = id + ".text", positioned = true, position = Vector2.zero } } };
        AssignCreatedCard(card, card.position);
        project.nodes.Add(card); selected = project.nodes.Count - 1; selectedAnswer = 0; selectedDecoration = null;
        markedCards.Clear(); markedCards.Add((id, 0)); focusBoard = false; Dirty();
    }
    void AddPlanningAnswer(StoryNode owner, Vector2? worldPosition = null)
    {
        if (owner == null || !project.nodes.Contains(owner) || owner.overviewOnly || owner.triggerOnly || owner.planningNote || owner.choices.Count >= 6) return;
        Edit("Antwortkarte hinzufügen");
        var choice = new StoryChoice { textKey = owner.id + ".answer." + Guid.NewGuid().ToString("N"), positioned = true,
            position = SnapPoint(worldPosition ?? owner.position + AnswerOffset(owner.choices.Count)) - owner.position };
        owner.choices.Add(choice); selected = project.nodes.IndexOf(owner); selectedAnswer = owner.choices.Count - 1; selectedDecoration = null;
        markedCards.Clear(); markedCards.Add((owner.id, selectedAnswer)); focusBoard = false; Dirty();
    }
    void MoveMission(int direction)
    {
        var mission = CurrentMission;
        var siblings = project.nodes.Where(n => n.overviewOnly && n.chapter == mission.chapter).ToList();
        var target = siblings[siblings.IndexOf(mission) + direction];
        Edit("Mission verschieben"); int sourceIndex = project.nodes.IndexOf(mission), targetIndex = project.nodes.IndexOf(target);
        project.nodes[sourceIndex] = target; project.nodes[targetIndex] = mission; ClearWritingSelection(); Dirty(); GUIUtility.ExitGUI();
    }
    void MoveConversation(int direction)
    {
        var conversation = CurrentConversation;
        var siblings = project.conversations.Where(c => c.missionId == conversation.missionId).ToList();
        var target = siblings[siblings.IndexOf(conversation) + direction];
        Edit("Gespräch verschieben"); int sourceIndex = project.conversations.IndexOf(conversation), targetIndex = project.conversations.IndexOf(target);
        project.conversations[sourceIndex] = target; project.conversations[targetIndex] = conversation; Dirty(); GUIUtility.ExitGUI();
    }

    void WritingText(SerializedProperty node, StoryNode current)
    {
        string titleDraft = language + "/" + current.planningTitleKey;
        if (!textDrafts.ContainsKey(titleDraft)) textDrafts[titleDraft] = StoryPlanningCatalog.Text(current.planningTitleKey, language);
        textDrafts[titleDraft] = EditorGUILayout.TextField("Kartentitel", textDrafts[titleDraft]);
        GUILayout.Space(10);
        PlanningCharacterPicker(node.FindPropertyRelative("speaker"));
        if (serialized.ApplyModifiedProperties()) Dirty();
        GUILayout.Space(12);
        GUILayout.Label("Storytext", EditorStyles.miniBoldLabel);
        GUILayout.Space(4);
        PlanningTextArea(current.textKey);
        GUILayout.Space(10);
        serialized.Update();
        EditorGUILayout.PropertyField(node.FindPropertyRelative(language == "de" ? "voiceDE" : "voiceEN"), new GUIContent("Audio"));
        if (serialized.ApplyModifiedProperties()) Dirty();
        GUILayout.Space(12);
        using (new EditorGUI.DisabledScope(current.choices.Count >= 6))
            if (GUILayout.Button("Antwort hinzufügen")) { AddPlanningAnswer(current); GUIUtility.ExitGUI(); }
    }

    void WritingCardFooter()
    {
        if (selectedDecoration == null && selected < 0 && CurrentConversation == null && CurrentMission == null && Sections.Count > 0)
        {
            bool hasMissions = ChapterHasContents(Sections[chapter]);
            using (new EditorGUI.DisabledScope(hasMissions))
                RedRemovalButton(new GUIContent("Kapitel entfernen", hasMissions ? "Zuerst zugehörige Inhalte verschieben oder entfernen." : "Kapitel entfernen"), RemovePlanningChapter);
            return;
        }
        if (selectedDecoration == null && selected < 0 && CurrentConversation == null && CurrentMission != null)
        {
            var mission = CurrentMission;
            bool hasContents = MissionHasContents(mission.id);
            using (new EditorGUI.DisabledScope(hasContents))
                RedRemovalButton(new GUIContent("Mission entfernen", hasContents ? "Zuerst zugehörige Inhalte verschieben oder entfernen." : "Mission entfernen"), RemovePlanningMission);
            return;
        }
        if (selectedDecoration != null || selectedAnswer >= 0 || selected < 0 || selected >= project.nodes.Count
            || project.nodes[selected].overviewOnly || project.nodes[selected].triggerOnly) return;
        RedRemovalButton(new GUIContent("Storykarte entfernen"), () =>
        {
            markedCards.Clear(); markedCards.Add((project.nodes[selected].id, -1));
            RemoveMarked(); GUIUtility.ExitGUI();
        });
    }

    void RedRemovalButton(GUIContent label, Action remove)
    {
        GUILayout.Space(8);
        var style = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        style.normal.textColor = style.hover.textColor = style.active.textColor = Color.white;
        Color previous = GUI.backgroundColor;
        try
        {
            GUI.backgroundColor = new Color(.85f, .2f, .2f);
            if (GUILayout.Button(label, style, GUILayout.Height(38))) remove();
        }
        finally { GUI.backgroundColor = previous; }
    }

    void PlanningCharacterPicker(SerializedProperty speaker)
    {
        Rect rect = EditorGUILayout.GetControlRect();
        Rect field = EditorGUI.PrefixLabel(rect, new GUIContent("Charakter"));
        var current = speaker.objectReferenceValue as StoryCharacter;
        if (!EditorGUI.DropdownButton(field, new GUIContent(PlanningCharacterDropdown.Name(current, language), "Öffnen und direkt nach einem Charakter suchen"), FocusType.Keyboard)) return;
        var owner = project;
        string nodeId = project.nodes[selected].id;
        PopupWindow.Show(field, new PlanningCharacterDropdown(field.width, language, current, character =>
        {
            if (!this || !owner || AssetDatabase.GetAssetPath(owner) != StoryPlanningCatalog.ProjectPath) return;
            var node = owner.Find(nodeId); if (node == null || node.speaker == character) return;
            Undo.RecordObject(owner, "Planungscharakter wählen"); node.speaker = character;
            EditorUtility.SetDirty(owner); serialized.Update(); Repaint();
        }));
    }
    void PlanningTextArea(string key, string hint = "Hier den Storytext dieser Karte schreiben. Beschreibe, was passiert oder was der Charakter sagt.")
    {
        if (string.IsNullOrEmpty(key)) return;
        string draftKey = language + "/" + key;
        if (!textDrafts.ContainsKey(draftKey)) textDrafts[draftKey] = StoryPlanningCatalog.Text(key, language);
        if (planningTextScrollKey != draftKey) { planningTextScrollKey = draftKey; planningTextScroll = Vector2.zero; }
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true, padding = new RectOffset(8, 8, 8, 8) };
        float width = Mathf.Max(100, detailsWidth - 58);
        float contentHeight = Mathf.Max(planningTextHeight - 8, style.CalcHeight(new GUIContent(textDrafts[draftKey] + "\n"), width));
        planningTextScroll = EditorGUILayout.BeginScrollView(planningTextScroll, GUILayout.Height(planningTextHeight));
        textDrafts[draftKey] = EditorGUILayout.TextArea(textDrafts[draftKey], style, GUILayout.Height(contentHeight), GUILayout.ExpandWidth(true));
        if (string.IsNullOrEmpty(textDrafts[draftKey]))
        {
            var hintStyle = new GUIStyle(style); hintStyle.normal.textColor = new Color(.55f, .55f, .55f);
            GUI.Label(GUILayoutUtility.GetLastRect(), hint, hintStyle);
        }
        EditorGUILayout.EndScrollView();
        Rect grip = GUILayoutUtility.GetRect(0, 9, GUILayout.ExpandWidth(true));
        int control = GUIUtility.GetControlID("PlanningTextResize".GetHashCode(), FocusType.Passive, grip);
        EditorGUIUtility.AddCursorRect(grip, MouseCursor.ResizeVertical);
        EditorGUI.DrawRect(new Rect(grip.center.x - 18, grip.center.y, 36, 1), new Color(.55f, .55f, .55f));
        GUI.Label(grip, new GUIContent("", "Ziehen, um die Textfläche zu vergrößern oder zu verkleinern"));
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition)) { GUIUtility.hotControl = control; e.Use(); }
        if (GUIUtility.hotControl != control) return;
        if (e.type == EventType.MouseDrag) { planningTextHeight = Mathf.Clamp(planningTextHeight + e.delta.y, 90, 650); e.Use(); Repaint(); }
        else if (e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
    }

    void WritingAnswer(SerializedProperty node, StoryNode owner)
    {
        GUILayout.Label("Antwort " + (selectedAnswer + 1), EditorStyles.boldLabel);
        var choice = owner.choices[selectedAnswer];
        GUILayout.Space(8);
        GUILayout.Label("Antworttext", EditorStyles.miniBoldLabel);
        PlanningTextArea(choice.textKey, "Hier die Antwort schreiben, die zu dieser Storykarte gehört.");
        GUILayout.Space(10);
        serialized.Update();
        var data = node.FindPropertyRelative("choices").GetArrayElementAtIndex(selectedAnswer);
        var targets = project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly && !n.planningNote && !n.planningAnswer).ToList();
        var local = targets.Where(n => n.conversationId == owner.conversationId).ToList();
        var existing = targets.Find(n => n.id == choice.next);
        if (existing != null && !local.Contains(existing)) local.Add(existing);
        int old = local.FindIndex(n => n.id == choice.next) + 1;
        int pick = EditorGUILayout.Popup("Danach", old, new[] { "Gespräch beenden" }.Concat(local.Select(NodeTitle)).ToArray());
        if (pick != old) data.FindPropertyRelative("next").stringValue = pick == 0 ? "" : local[pick - 1].id;
        if (serialized.ApplyModifiedProperties()) Dirty();
        if (GUILayout.Button("Zur Storykarte")) { selectedAnswer = -1; markedCards.Clear(); markedCards.Add((owner.id, -1)); GUIUtility.ExitGUI(); }
        if (GUILayout.Button("Antwortkarte entfernen")) { RemoveMarked(); GUIUtility.ExitGUI(); }
    }

    void AdvancedNode(SerializedProperty node, StoryNode current)
    {
        writingAdvanced = EditorGUILayout.Foldout(writingAdvanced, "Weitere Planungsdetails", true);
        if (!writingAdvanced) return;
        serialized.Update();
        foreach (string field in current.overviewOnly ? new[] { "choices", "location", "relatedAsset" }
            : new[] { "status", "purpose", "questions", "location", "relatedAsset" })
            EditorGUILayout.PropertyField(node.FindPropertyRelative(field), true);
        if (serialized.ApplyModifiedProperties()) Dirty();
        using (new EditorGUI.DisabledScope(true))
            foreach (string field in new[] { "id", "sourceReference", "sourcePath", "sourceProperty" })
                EditorGUILayout.PropertyField(node.FindPropertyRelative(field), true);
    }
    void RepairWritingEntry()
    {
        foreach (var conversation in project.conversations)
            if (!project.nodes.Any(n => n.id == conversation.entryNodeId && n.conversationId == conversation.id))
                conversation.entryNodeId = project.nodes.FirstOrDefault(n => n.conversationId == conversation.id)?.id ?? "";
    }
    void PreviewConversation()
    {
        SaveWritingDrafts(); var entry = project.Find(CurrentConversation?.entryNodeId);
        if (entry == null) { ShowNotification(new GUIContent("Zuerst einen Starttext hinzufügen.")); return; }
        previewMode = 0; StartPreview(entry); tab = 2;
    }
    void SaveWritingDrafts()
    {
        if (!project || AssetDatabase.GetAssetPath(project) != StoryPlanningCatalog.ProjectPath) return;
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        foreach (var draft in textDrafts.ToArray())
        {
            int separator = draft.Key.IndexOf('/'); if (separator < 0) continue;
            string lang = draft.Key.Substring(0, separator), key = draft.Key.Substring(separator + 1);
            if (StoryPlanningCatalog.Text(key, lang) != draft.Value) StoryPlanningCatalog.Write(key, lang, draft.Value);
            if (lang == "de")
            {
                var node = project.nodes.Find(n => n.planningTitleKey == key);
                if (node != null) node.title = draft.Value;
            }
        }
        Undo.CollapseUndoOperations(group); AssetDatabase.SaveAssetIfDirty(project);
        if (character && AssetDatabase.GetAssetPath(character).StartsWith(StoryPlanningCatalog.CharactersPath + "/", StringComparison.Ordinal)) AssetDatabase.SaveAssetIfDirty(character);
    }
    void OnLostFocus() => SaveWritingDrafts();
}

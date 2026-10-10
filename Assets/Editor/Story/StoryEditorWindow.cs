using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public partial class StoryEditorWindow : EditorWindow
{
    StoryProject project;
    SerializedObject serialized;
    int tab, chapter, selected = -1;
    Vector2 boardScroll, detailScroll, characterScroll;
    string search = "", language = "de", newCharacter = "";
    StoryCharacter character;
    StorySession preview;
    readonly Dictionary<string, bool> flags = new();
    readonly List<string> trace = new();
    string flagKey = "";
    readonly Dictionary<string, string> textDrafts = new();
    bool autoAudio = true;
    int drag = -1;
    Vector2 lastMouse;
    bool panning, dragRecorded;
    int dragUndoGroup;
    string chapterName = "";
    int chapterNameIndex = -1;
    [SerializeField] float sidebarWidth = 270, detailsWidth = 420;
    Vector2 sidebarScroll;
    bool showNavigationHelp;
    [SerializeField] float boardZoom = 1;
    bool focusBoard;
    int selectedAnswer = -1, previewStart;
    readonly HashSet<(string id, int answer)> markedCards = new();
    readonly HashSet<(string id, int answer)> selectionBeforeMarquee = new();
    bool marquee, additiveMarquee;
    Vector2 marqueeStart, marqueeEnd;
    int connectionOwner = -1, connectionAnswer = -1, connectionSide;
    Vector2 previewScroll, characterDetailsScroll;
    int messageFilter, previewMode, previewLevel = 1, previewSignal;
    StoryEventSimulator eventSimulator;
    string eventPreviewReason = "";
    readonly StoryCharacterPreview characterPreview = new();
    bool StoryView => tab == 0;
    List<string> Sections => StoryView ? project.chapters : project.dialogueGroups;
    int previousBoardTab = -1;
    public static void Open() { var window = GetWindow<StoryEditorWindow>("Story & Charaktere"); window.minSize = new Vector2(1050, 650); }
    double nextCharacterFrame;
    void OnEnable() { language = SessionState.GetString("StoryEditor.ContentLanguage", Localization.CurrentLanguage); eventSimulator = new StoryEventSimulator(flags); Undo.undoRedoPerformed += Refresh; EditorApplication.update += CharacterFrame; Refresh(); }
    void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorApplication.update -= CharacterFrame; StopAudio(); characterPreview.Dispose(); }
    void CharacterFrame()
    {
        if (tab != 2 || !characterPreview.Playing || EditorApplication.timeSinceStartup < nextCharacterFrame) return;
        nextCharacterFrame = EditorApplication.timeSinceStartup + 1d / 60d; Repaint();
    }
    void Refresh()
    {
        project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath); serialized = project ? new SerializedObject(project) : null;
        if (project && (project.dialogueGroups.Count == 0 || project.Find("pilot-trail") != null || project.nodes.Any(n => !n.overviewOnly && !n.answerBoardLayout)))
        { StoryImport.SeparateExisting(project); AssetDatabase.SaveAssets(); }
        if (project && !project.nodes.Any(n => n.triggerOnly) && project.nodes.Any(n => n.eventInfo.enabled))
        { StoryTriggerAuthoring.Sync(project); AssetDatabase.SaveAssets(); }
        if (project && project.nodes.Any(n => n.triggerOnly) && !project.eventTriggersManaged)
        { project.eventTriggersManaged = true; EditorUtility.SetDirty(project); }
        textDrafts.Clear(); chapter = Mathf.Clamp(chapter, 0, project ? Mathf.Max(0, Sections.Count - 1) : 0);
        if (!project || selected >= project.nodes.Count) selected = -1;
        chapterNameIndex = -1; drag = connectionOwner = -1; selectedAnswer = -1; panning = marquee = false; markedCards.Clear(); preview = null; StopAudio(); Repaint();
        selectedDecoration = draggingDecoration = resizingDecoration = null;
    }
    void OnGUI()
    {
        if (project && tab <= 1 && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete
            && GUIUtility.keyboardControl == 0 && GUIUtility.hotControl == 0 && markedCards.Count > 0)
        { Event.current.Use(); RemoveMarked(); GUIUtility.ExitGUI(); }
        if (Event.current.type == EventType.KeyDown && Event.current.control && GUIUtility.keyboardControl == 0)
        {
            bool undo = Event.current.keyCode == KeyCode.Z && !Event.current.shift;
            bool redo = Event.current.keyCode == KeyCode.Y || Event.current.keyCode == KeyCode.Z && Event.current.shift;
            if (undo || redo) { Event.current.Use(); UndoOrRedo(redo); GUIUtility.ExitGUI(); }
        }
        bool undoClicked, redoClicked;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            tab = GUILayout.Toolbar(tab, new[] { "Storyübersicht", "Gespräche", "Charaktere", "Vorschau", "Prüfung" }, EditorStyles.toolbarButton);
            undoClicked = GUILayout.Button(new GUIContent("Rückgängig", "Ctrl+Z"), EditorStyles.toolbarButton);
            redoClicked = GUILayout.Button(new GUIContent("Wiederholen", "Ctrl+Y / Ctrl+Shift+Z"), EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Inhalte:", EditorStyles.miniLabel, GUILayout.Width(45));
            int choice = GUILayout.Toolbar(language == "de" ? 0 : 1, new[] { "Deutsch", "English" }, EditorStyles.toolbarButton);
            if (choice != (language == "de" ? 0 : 1))
            { language = choice == 0 ? "de" : "en"; SessionState.SetString("StoryEditor.ContentLanguage", language); if (preview?.Node != null && autoAudio) PlayNode(preview.Node); Repaint(); }
            if (GUILayout.Button(new GUIContent("Speichern", "Übernimmt offene DE/EN-Textentwürfe und speichert Story- und Charakter-Assets."), EditorStyles.toolbarButton)) SaveAll();
        }
        if (undoClicked || redoClicked) { UndoOrRedo(redoClicked); GUIUtility.ExitGUI(); }
        if (!project)
        {
            EditorGUILayout.HelpBox("Storyprojekt fehlt. Die Übernahme verknüpft bestehende Texte und Abläufe; sie ändert keine Spielstände.", MessageType.Info);
            if (GUILayout.Button("Bestehende Story übernehmen")) { StoryImport.Build(); Refresh(); }
            return;
        }
        if (tab <= 1) Board(); else if (tab == 2) Characters(); else if (tab == 3) Preview(); else Validation();
    }
    void Board()
    {
        if (previousBoardTab != tab)
        { previousBoardTab = tab; chapter = 0; selected = -1; selectedDecoration = null; markedCards.Clear(); chapterNameIndex = -1; search = ""; focusBoard = true; }
        sidebarWidth = Mathf.Clamp(sidebarWidth, 230, Mathf.Max(230, position.width - detailsWidth - 320));
        detailsWidth = Mathf.Clamp(detailsWidth, 340, Mathf.Max(340, position.width - sidebarWidth - 320));
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(sidebarWidth)))
            {
                sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
                using (new EditorGUILayout.VerticalScope(PanelStyle()))
                {
                GUILayout.Label(StoryView ? "Storykapitel" : "Gesprächsgruppen", EditorStyles.boldLabel);
                if (!StoryView) messageFilter = GUILayout.Toolbar(messageFilter, new[] { "Alle", "Dialoge", "Ereignisse" });
                GUILayout.Space(8);
                for (int i = 0; i < Sections.Count; i++)
                {
                    if (GUILayout.Toggle(chapter == i, Sections[i], "Button", GUILayout.Height(28)) && chapter != i)
                    { chapter = i; selected = -1; selectedDecoration = null; markedCards.Clear(); chapterNameIndex = -1; focusBoard = true; }
                    GUILayout.Space(3);
                }
                GUILayout.Space(10);
                GUILayout.Label("Suche", EditorStyles.miniBoldLabel);
                search = EditorGUILayout.TextField(search, GUILayout.Height(26));
                GUILayout.Space(8);
                if (GUILayout.Button(StoryView ? "Kapitel hinzufügen" : "Gesprächsgruppe hinzufügen", GUILayout.Height(28))) { Edit("Kapitel"); Sections.Add("Kapitel " + (Sections.Count + 1)); chapter = Sections.Count - 1; Dirty(); }
                }
                GUILayout.Space(8);
                if (Sections.Count > 0)
                {
                    using (new EditorGUILayout.VerticalScope(PanelStyle()))
                    {
                    GUILayout.Label(StoryView ? "Ausgewähltes Kapitel" : "Ausgewählte Gesprächsgruppe", EditorStyles.boldLabel);
                    GUILayout.Space(8);
                    if (chapterNameIndex != chapter) { chapterName = Sections[chapter]; chapterNameIndex = chapter; }
                    GUILayout.Label("Name", EditorStyles.miniBoldLabel);
                    chapterName = EditorGUILayout.TextField(chapterName, GUILayout.Height(26));
                    GUILayout.Space(8);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(chapterName) || chapterName.Trim() == Sections[chapter]))
                        if (GUILayout.Button(StoryView ? "Kapitel umbenennen" : "Gruppe umbenennen", GUILayout.Height(28))) RenameChapter();
                    }
                }
                GUILayout.Space(8);
                using (new EditorGUILayout.VerticalScope(PanelStyle()))
                {
                GUILayout.Label("Story bearbeiten", EditorStyles.boldLabel);
                GUILayout.Space(8);
                if (GUILayout.Button(StoryView ? "Neuer Storyschritt" : "Neue Dialogzeile", GUILayout.Height(30)))
                {
                    Edit("Storyschritt"); var id = "story." + Guid.NewGuid().ToString("N");
                    project.nodes.Add(new StoryNode { id = id, title = "Neuer Schritt", chapter = Sections.ElementAtOrDefault(chapter), textKey = StoryView ? "" : id, overviewOnly = StoryView, position = SnapPoint(boardScroll + new Vector2(30, 30)),
                        answerBoardLayout = true, choices = new() { new StoryChoice { textKey = StoryView ? "" : "game.continue" } } });
                    selected = project.nodes.Count - 1; selectedAnswer = -1;
                    markedCards.Clear(); markedCards.Add((id, -1)); SnapLayout(markedCards); Dirty();
                }
                GUILayout.Space(8);
                if (GUILayout.Button(new GUIContent("Quellen ergänzen", "Ergänzt neue Inhalte aus dem Spiel. Vorhandene Storyschritte und Texte bleiben erhalten."), GUILayout.Height(28)))
                { SaveAll(); StoryImport.Build(); Refresh(); }
                }
                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Titel hinzufügen", GUILayout.Height(28))) AddDecoration(StoryDecorationKind.Label);
                    if (GUILayout.Button("Linie hinzufügen", GUILayout.Height(28))) AddDecoration(StoryDecorationKind.Line);
                }
                if (!StoryView && GUILayout.Button("Auslöser hinzufügen", GUILayout.Height(28))) AddTrainingTrigger();
                GridControls();
                using (new EditorGUI.DisabledScope(markedCards.Count == 0))
                    if (GUILayout.Button(new GUIContent("Auswahl entfernen", "Löscht alle markierten Karten, Labels und Linien. Rückgängig mit Strg+Z."), GUILayout.Height(28))) { RemoveMarked(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button(new GUIContent("Inhalte fokussieren", "Zeigt alle Nodes des aktuellen Kapitels und setzt die Suche zurück."), GUILayout.Height(28)))
                { search = ""; focusBoard = true; Repaint(); }
                GUILayout.Space(8);
                showNavigationHelp = EditorGUILayout.Foldout(showNavigationHelp, "Navigation", true);
                if (showNavigationHelp) EditorGUILayout.HelpBox("Verbindungspunkt ziehen: Folgeschritt verbinden.\nRechts oder Mausrad gedrückt ziehen: Board bewegen.\nLinks auf leerer Fläche ziehen: Auswahlrechteck.\nStrg/Shift: Auswahl ergänzen.\nMarkierte Karte ziehen: Auswahl verschieben.\nMausrad: Zoom am Mauszeiger.", MessageType.None);
                EditorGUILayout.EndScrollView();
            }
            Splitter(ref sidebarWidth, false);
            Rect board = GUILayoutUtility.GetRect(280, 400, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Graph(board);
            Splitter(ref detailsWidth, true);
            using (new EditorGUILayout.VerticalScope(PanelStyle(), GUILayout.Width(detailsWidth)))
            {
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                Details(); EditorGUILayout.EndScrollView();
            }
        }
    }
    static GUIStyle PanelStyle() => new(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 12, 12) };
    static void InputField(SerializedProperty property)
    {
        if (property.propertyType != SerializedPropertyType.String) { EditorGUILayout.PropertyField(property, true); return; }
        GUILayout.Label(new GUIContent(property.displayName, property.tooltip), EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(property, GUIContent.none, true);
        GUILayout.Space(8);
    }
    void Splitter(ref float width, bool right)
    {
        Rect rect = GUILayoutUtility.GetRect(6, 6, GUILayout.Width(6), GUILayout.ExpandHeight(true));
        int control = GUIUtility.GetControlID(right ? 7122 : 7121, FocusType.Passive, rect);
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
        EditorGUI.DrawRect(new Rect(rect.center.x - 1, rect.y, 2, rect.height), new Color(.25f, .25f, .25f));
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        { GUIUtility.hotControl = control; e.Use(); }
        if (GUIUtility.hotControl != control) return;
        if (e.type == EventType.MouseDrag) { width += e.delta.x * (right ? -1 : 1); e.Use(); Repaint(); }
        else if (e.type == EventType.MouseUp) { GUIUtility.hotControl = 0; e.Use(); }
    }
    IEnumerable<int> Visible() => Enumerable.Range(0, project.nodes.Count).Where(i => project.nodes[i].overviewOnly == StoryView && project.nodes[i].chapter == Sections.ElementAtOrDefault(chapter)
        && (StoryView || messageFilter == 0 || messageFilter == 1 && !project.EventInfo(project.nodes[i]).enabled || messageFilter == 2 && project.EventInfo(project.nodes[i]).enabled)
        && (string.IsNullOrEmpty(search) || (project.nodes[i].title + project.nodes[i].id + project.nodes[i].purpose).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
    void Graph(Rect rect)
    {
        var indices = Visible().ToList();
        if (focusBoard && Event.current.type == EventType.Repaint)
        {
            var bounds = ContentBounds(0);
            boardZoom = Mathf.Clamp(Mathf.Min(Mathf.Max(1, rect.width - 64) / bounds.width, Mathf.Max(1, rect.height - 64) / bounds.height), .05f, 1f);
            boardScroll = bounds.center - rect.size / (2 * boardZoom);
            focusBoard = false;
        }
        int control = GUIUtility.GetControlID("StoryBoard".GetHashCode(), FocusType.Passive, rect);
        GUI.BeginGroup(rect);
        var view = new Rect(Vector2.zero, rect.size);
        EditorGUI.DrawRect(view, new Color(.09f, .09f, .09f));
        Handles.BeginGUI(); Handles.color = new Color(.16f, .16f, .16f);
        float grid = GridStep * boardZoom; while (grid < 8) grid *= 2;
        if (showGrid) for (float x = -Mathf.Repeat(boardScroll.x * boardZoom, grid); x < rect.width; x += grid) Handles.DrawLine(new Vector2(x, 0), new Vector2(x, rect.height));
        if (showGrid) for (float y = -Mathf.Repeat(boardScroll.y * boardZoom, grid); y < rect.height; y += grid) Handles.DrawLine(new Vector2(0, y), new Vector2(rect.width, y));
        Handles.color = Color.white; Handles.EndGUI();
        DrawDecorations();
        var worldBounds = ContentBounds();
        var e = Event.current;
        if (e.type == EventType.MouseDown && view.Contains(e.mousePosition))
        {
            dragRemainder = Vector2.zero;
            int hit = indices.Where(i => NodeRect(project.nodes[i]).Contains(e.mousePosition)).DefaultIfEmpty(-1).Last();
            int answerHit = -1;
            if (!StoryView) foreach (int owner in indices) for (int a = 0; a < project.nodes[owner].choices.Count; a++)
                if (AnswerRect(project.nodes[owner], a).Contains(e.mousePosition)) { hit = owner; answerHit = a; }
            if (!StoryView) foreach (int owner in indices) if (project.nodes[owner].triggerOnly && EventRect(project.nodes[owner]).Contains(e.mousePosition)) { hit = owner; answerHit = -2; }
            var port = HitPort(indices, e.mousePosition);
            if (e.button == 0 && port.owner >= 0)
            {
                connectionOwner = port.owner; connectionAnswer = port.answer; connectionSide = port.side;
                selectedDecoration = null;
                selected = port.owner; selectedAnswer = port.answer; markedCards.Clear(); markedCards.Add((project.nodes[port.owner].id, port.answer));
                panning = marquee = false; drag = -1;
                lastMouse = e.mousePosition; GUIUtility.hotControl = control; GUIUtility.keyboardControl = 0; e.Use(); Repaint();
            }
            else if (e.button == 1 || e.button == 2)
            { connectionOwner = -1; panning = true; marquee = false; drag = -1; GUIUtility.hotControl = control; lastMouse = e.mousePosition; GUIUtility.keyboardControl = 0; e.Use(); }
            else if (e.button == 0 && !e.control && !e.shift && hit < 0 && DecorationResizeHit(e.mousePosition))
            {
                resizingDecoration = selectedDecoration; draggingDecoration = null; panning = marquee = false; drag = -1; dragRecorded = false;
                GUIUtility.hotControl = control; GUIUtility.keyboardControl = 0; lastMouse = e.mousePosition; e.Use();
            }
            else if (e.button == 0 && hit < 0 && HitDecoration(e.mousePosition) is StoryBoardDecoration decoration)
            {
                var card = (decoration.id, -3);
                if (e.control && markedCards.Contains(card)) markedCards.Remove(card);
                else { if (!e.control && !e.shift && !markedCards.Contains(card)) markedCards.Clear(); markedCards.Add(card); }
                selectedDecoration = decoration.id; selected = -1; draggingDecoration = markedCards.Contains(card) ? decoration.id : null;
                if (draggingDecoration == null) PrimaryMarkedCard();
                panning = marquee = false; drag = -1; dragRecorded = false; GUIUtility.hotControl = control; GUIUtility.keyboardControl = 0; lastMouse = e.mousePosition; e.Use(); Repaint();
            }
            else if (e.button == 0 && hit < 0)
            {
                marquee = true; panning = false; drag = -1; additiveMarquee = e.control || e.shift;
                selectionBeforeMarquee.Clear(); selectionBeforeMarquee.UnionWith(markedCards);
                if (!additiveMarquee) { markedCards.Clear(); selected = selectedAnswer = -1; selectedDecoration = null; }
                marqueeStart = marqueeEnd = boardScroll + e.mousePosition / boardZoom;
                GUIUtility.hotControl = control; GUIUtility.keyboardControl = 0; e.Use(); Repaint();
            }
            else if (e.button == 0)
            {
                var card = (project.nodes[hit].id, answerHit);
                selectedDecoration = null; draggingDecoration = resizingDecoration = null;
                if (e.control && markedCards.Contains(card)) markedCards.Remove(card);
                else { if (!e.control && !e.shift && !markedCards.Contains(card)) markedCards.Clear(); markedCards.Add(card); }
                selected = hit; selectedAnswer = answerHit;
                drag = markedCards.Contains(card) ? hit : -1;
                if (drag < 0) PrimaryMarkedCard();
                panning = marquee = false; dragRecorded = false; GUIUtility.hotControl = control;
                lastMouse = e.mousePosition; GUIUtility.keyboardControl = 0; e.Use(); Repaint();
            }
        }
        if (GUIUtility.hotControl == control && e.type == EventType.MouseDrag)
        {
            Vector2 delta = (e.mousePosition - lastMouse) / boardZoom; lastMouse = e.mousePosition;
            if (panning) boardScroll -= delta;
            else if (!string.IsNullOrEmpty(resizingDecoration))
            {
                if (!dragRecorded) { Undo.IncrementCurrentGroup(); dragUndoGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(project, "Boardelement Größe ändern"); dragRecorded = true; }
                var d = project.decorations.Find(d => d.id == resizingDecoration); var world = SnapPoint(boardScroll + e.mousePosition / boardZoom);
                if (d.kind == StoryDecorationKind.Label) d.size = Vector2.Max(Vector2.one * 20, world - d.position);
                else if (resizeLineStart) d.position = world; else d.end = world;
                Dirty();
            }
            else if (marquee)
            {
                marqueeEnd = boardScroll + e.mousePosition / boardZoom;
                markedCards.Clear(); if (additiveMarquee) markedCards.UnionWith(selectionBeforeMarquee);
                var area = MarqueeBounds();
                foreach (int i in indices)
                {
                    var node = project.nodes[i];
                    if (area.Overlaps(node.triggerOnly ? EventWorldRect(node) : new Rect(node.position, new Vector2(230, 95)))) markedCards.Add((node.id, node.triggerOnly ? -2 : -1));
                    if (!StoryView) for (int a = 0; a < node.choices.Count; a++)
                        if (area.Overlaps(AnswerWorldRect(node, a))) markedCards.Add((node.id, a));
                }
                foreach (var d in BoardDecorations()) if (DecorationOverlaps(d, area)) markedCards.Add((d.id, -3));
                PrimaryMarkedCard();
            }
            else if (drag >= 0 && drag < project.nodes.Count || !string.IsNullOrEmpty(draggingDecoration))
            {
                if (project.snapToGrid) { dragRemainder += delta; delta = SnapPoint(dragRemainder); dragRemainder -= delta; }
                if (!dragRecorded) { Undo.IncrementCurrentGroup(); dragUndoGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(project, "Auswahl verschieben"); dragRecorded = true; }
                foreach (var card in markedCards)
                {
                    var owner = project.Find(card.id);
                    if (owner == null)
                    { var d = project.decorations.Find(d => d.id == card.id); if (d != null) { d.position += delta; d.end += delta; } continue; }
                    if (card.answer == -2) owner.position += delta;
                    else if (card.answer == -1) owner.position += delta;
                    else if (!markedCards.Contains((card.id, -1)) && card.answer < owner.choices.Count)
                    {
                        var choice = owner.choices[card.answer];
                        if (!choice.positioned) choice.position = AnswerOffset(card.answer);
                        choice.positioned = true; choice.position += delta;
                    }
                }
                Dirty();
            }
            e.Use(); Repaint();
        }
        if (GUIUtility.hotControl == control && e.type == EventType.MouseUp)
        {
            if (connectionOwner >= 0)
            {
                var destination = HitPort(indices, e.mousePosition);
                if (destination.owner < 0) destination = HitCard(indices, e.mousePosition);
                if (destination.owner >= 0) LinkCards(connectionOwner, connectionAnswer, destination.owner, destination.answer);
                connectionOwner = -1;
            }
            if (dragRecorded) { SnapLayout(markedCards); Undo.CollapseUndoOperations(dragUndoGroup); }
            dragRecorded = false; drag = -1; panning = marquee = false; GUIUtility.hotControl = 0; e.Use();
            draggingDecoration = resizingDecoration = null;
        }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && GUIUtility.hotControl == control && marquee)
        { markedCards.Clear(); markedCards.UnionWith(selectionBeforeMarquee); PrimaryMarkedCard(); marquee = false; GUIUtility.hotControl = 0; e.Use(); Repaint(); }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && GUIUtility.hotControl == control && connectionOwner >= 0)
        { connectionOwner = -1; GUIUtility.hotControl = 0; e.Use(); Repaint(); }
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition) && GUIUtility.hotControl == 0)
        {
            Vector2 anchor = boardScroll + e.mousePosition / boardZoom;
            boardZoom = Mathf.Clamp(boardZoom * Mathf.Exp(-e.delta.y * .08f), .05f, 2.5f);
            boardScroll = anchor - e.mousePosition / boardZoom;
            e.Use(); Repaint();
        }
        Handles.BeginGUI();
        if (!StoryView) foreach (int i in indices)
        {
            var text = project.nodes[i]; var trigger = project.Find(text.eventTriggerId);
            if (!text.triggerOnly && trigger != null && indices.Contains(project.nodes.IndexOf(trigger))) ConnectCards(EventRect(trigger), NodeRect(text), new Color(.9f, .65f, .25f));
        }
        foreach (int i in indices)
            for (int answerIndex = 0; answerIndex < project.nodes[i].choices.Count; answerIndex++)
            {
                var choice = project.nodes[i].choices[answerIndex];
                int target = project.nodes.FindIndex(n => n.id == choice.next);
                Rect from = NodeRect(project.nodes[i]);
                if (!StoryView)
                {
                    Rect answer = AnswerRect(project.nodes[i], answerIndex);
                    ConnectCards(from, answer, new Color(.5f, .8f, .6f));
                    from = answer;
                }
                if (indices.Contains(target))
                    ConnectCards(from, NodeRect(project.nodes[target]), Color.cyan);
            }
        Handles.EndGUI();
        var titleStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { fontStyle = FontStyle.Bold, fontSize = Mathf.Max(3, Mathf.RoundToInt(12 * boardZoom)), padding = new RectOffset() };
        titleStyle.normal.textColor = Color.white;
        var infoStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = Mathf.Max(3, Mathf.RoundToInt(10 * boardZoom)), padding = new RectOffset() };
        infoStyle.normal.textColor = new Color(.83f, .85f, .88f);
        foreach (int i in indices)
        {
            var node = project.nodes[i]; if (node.triggerOnly) continue; Rect box = NodeRect(node);
            EditorGUI.DrawRect(new Rect(box.x + 3 * boardZoom, box.y + 4 * boardZoom, box.width, box.height), new Color(0, 0, 0, .45f));
            bool marked = markedCards.Contains((node.id, -1));
            Color border = marked ? new Color(.3f, .72f, 1) : new Color(.48f, .5f, .53f);
            float edge = Mathf.Max(1, (marked ? 2 : 1) * boardZoom);
            EditorGUI.DrawRect(box, border);
            EditorGUI.DrawRect(new Rect(box.x + edge, box.y + edge, box.width - edge * 2, box.height - edge * 2), new Color(.25f, .26f, .28f));
            EditorGUI.DrawRect(new Rect(box.x + edge, box.y + edge, box.width - edge * 2, 47 * boardZoom), marked ? new Color(.16f, .28f, .38f) : new Color(.2f, .21f, .23f));
            GUI.Label(new Rect(box.x + 10 * boardZoom, box.y + 6 * boardZoom, 210 * boardZoom, 40 * boardZoom), NodeTitle(node), titleStyle);
            string info = node.overviewOnly ? node.status + " · Storyschritt\n" + project.nodes.Count(n => !n.overviewOnly && n.storyStepId == node.id) + " Gesprächszeilen"
                : (project.EventInfo(node).enabled ? "Ereignismeldung · " : "Text · ") + node.channel + "\n" + (node.speaker ? StoryCatalog.Text(node.speaker.nameKey, language) : "Kein Sprecher");
            GUI.Label(new Rect(box.x + 10 * boardZoom, box.y + 54 * boardZoom, 210 * boardZoom, 34 * boardZoom), info, infoStyle);
        }
        if (!StoryView) foreach (int i in indices) for (int a = 0; a < project.nodes[i].choices.Count; a++)
        {
            Rect box = AnswerRect(project.nodes[i], a);
            EditorGUI.DrawRect(box, markedCards.Contains((project.nodes[i].id, a)) ? new Color(.3f, .72f, 1) : new Color(.35f, .58f, .42f));
            EditorGUI.DrawRect(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), new Color(.15f, .24f, .18f));
            GUI.Label(new Rect(box.x + 8 * boardZoom, box.y + 5 * boardZoom, 220 * boardZoom, 18 * boardZoom), "Antwort " + (a + 1), titleStyle);
            string text = StoryCatalog.Text(project.nodes[i].choices[a].textKey, language);
            GUI.Label(new Rect(box.x + 8 * boardZoom, box.y + 25 * boardZoom, 220 * boardZoom, 40 * boardZoom), string.IsNullOrEmpty(text) ? "Antworttext fehlt" : text, titleStyle);
        }
        foreach (int i in indices)
        {
            if (!StoryView && project.nodes[i].triggerOnly)
            {
                var node = project.nodes[i]; var box = EventRect(node);
                EditorGUI.DrawRect(box, markedCards.Contains((node.id, -2)) ? new Color(.3f, .72f, 1) : new Color(.72f, .5f, .24f));
                EditorGUI.DrawRect(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), new Color(.26f, .2f, .13f));
                GUI.Label(new Rect(box.x + 8 * boardZoom, box.y + 6 * boardZoom, 220 * boardZoom, 22 * boardZoom), string.IsNullOrEmpty(node.title) ? "Auslöser" : node.title, titleStyle);
                GUI.Label(new Rect(box.x + 8 * boardZoom, box.y + 30 * boardZoom, 220 * boardZoom, 72 * boardZoom), EventSummary(project.EventInfo(node)) + "\n" + project.TriggerTexts(node.id).Length + " Texte", new GUIStyle(titleStyle) { fontStyle = FontStyle.Normal });
                DrawPorts(box);
            }
            if (!project.nodes[i].triggerOnly) DrawPorts(NodeRect(project.nodes[i]));
            if (!StoryView) for (int a = 0; a < project.nodes[i].choices.Count; a++) DrawPorts(AnswerRect(project.nodes[i], a));
        }
        if (connectionOwner >= 0)
        {
            var owner = project.nodes[connectionOwner];
            Rect card = connectionAnswer == -2 ? EventRect(owner) : connectionAnswer < 0 ? NodeRect(owner) : AnswerRect(owner, connectionAnswer);
            Vector2 start = CardPorts(card)[connectionSide];
            var normals = new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up };
            var hover = HitPort(indices, lastMouse); if (hover.owner < 0) hover = HitCard(indices, lastMouse);
            Color color = hover.owner >= 0 && CanLink(connectionOwner, connectionAnswer, hover.owner, hover.answer) ? Color.green : Color.cyan;
            Handles.BeginGUI();
            Handles.DrawBezier(start, lastMouse, start + normals[connectionSide] * (60 * boardZoom), lastMouse, color, null, 2);
            Handles.EndGUI();
        }
        if (marquee)
        {
            var world = MarqueeBounds(); var box = new Rect((world.position - boardScroll) * boardZoom, world.size * boardZoom);
            EditorGUI.DrawRect(box, new Color(.3f, .65f, 1, .15f));
            var color = new Color(.3f, .72f, 1);
            EditorGUI.DrawRect(new Rect(box.x, box.y, box.width, 1), color); EditorGUI.DrawRect(new Rect(box.x, box.yMax - 1, box.width, 1), color);
            EditorGUI.DrawRect(new Rect(box.x, box.y, 1, box.height), color); EditorGUI.DrawRect(new Rect(box.xMax - 1, box.y, 1, box.height), color);
        }
        GUI.Label(new Rect(8, rect.height - 22, rect.width - 16, 20), $"Zoom: {boardZoom:P0} · {markedCards.Count} markiert · Board: {worldBounds.width:0} × {worldBounds.height:0}", EditorStyles.miniLabel);
        GUI.EndGroup();
    }
    Rect NodeRect(StoryNode node) => node.triggerOnly ? EventRect(node) : new((node.position - boardScroll) * boardZoom, new Vector2(230, 95) * boardZoom);
    Rect EventWorldRect(StoryNode node) => new(node.triggerOnly ? node.position : node.eventPositioned ? node.eventPosition : node.position + new Vector2(-310, 0), new Vector2(240, 110));
    Rect EventRect(StoryNode node) { var rect = EventWorldRect(node); return new Rect((rect.position - boardScroll) * boardZoom, rect.size * boardZoom); }
    static string EventSummary(StoryEventInfo info)
    {
        string range = info.kind == StoryEventKind.LevelStart || info.kind == StoryEventKind.LevelTransmission || info.kind == StoryEventKind.LateTraining
            ? "\nLevel " + info.fromLevel + (info.toLevel == 0 ? "+" : info.toLevel != info.fromLevel ? "–" + info.toLevel : "") : "";
        return StoryEventSimulator.Signal(info) + range + "\n" + (info.once ? "Einmalig" : "Wiederholbar") + (info.random ? " · Zufallsauswahl" : " · Fester Text") + (info.probability < 1 ? "\nChance: " + info.probability.ToString("P0") : "");
    }
    static Vector2[] CardPorts(Rect r) => new[] { new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y), new Vector2(r.center.x, r.yMin), new Vector2(r.center.x, r.yMax) };
    void DrawPorts(Rect card)
    {
        float size = Mathf.Clamp(9 * boardZoom, 6, 14);
        foreach (var point in CardPorts(card))
        {
            Rect port = new(point - Vector2.one * (size * .5f), Vector2.one * size);
            EditorGUI.DrawRect(port, new Color(.07f, .08f, .1f));
            EditorGUI.DrawRect(new Rect(port.x + 1, port.y + 1, size - 2, size - 2), new Color(.55f, .8f, 1));
            EditorGUIUtility.AddCursorRect(port, MouseCursor.Link);
        }
    }
    (int owner, int answer, int side) HitPort(List<int> indices, Vector2 mouse)
    {
        float radius = Mathf.Clamp(7 * boardZoom, 5, 12);
        foreach (int i in indices.AsEnumerable().Reverse())
            for (int a = project.nodes[i].triggerOnly ? -2 : StoryView ? -1 : project.nodes[i].choices.Count - 1; a >= (project.nodes[i].triggerOnly ? -2 : -1); a--)
            {
                var ports = CardPorts(a == -2 ? EventRect(project.nodes[i]) : a < 0 ? NodeRect(project.nodes[i]) : AnswerRect(project.nodes[i], a));
                for (int side = 0; side < 4; side++) if ((mouse - ports[side]).sqrMagnitude <= radius * radius) return (i, a, side);
            }
        return (-1, -1, -1);
    }
    (int owner, int answer, int side) HitCard(List<int> indices, Vector2 mouse)
    {
        foreach (int i in indices.AsEnumerable().Reverse())
        {
            if (!StoryView) for (int a = project.nodes[i].choices.Count - 1; a >= 0; a--) if (AnswerRect(project.nodes[i], a).Contains(mouse)) return (i, a, -1);
            if (!StoryView && project.nodes[i].triggerOnly && EventRect(project.nodes[i]).Contains(mouse)) return (i, -2, -1);
            if (NodeRect(project.nodes[i]).Contains(mouse)) return (i, -1, -1);
        }
        return (-1, -1, -1);
    }
    bool CanLink(int from, int fromAnswer, int to, int toAnswer)
    {
        if (project.nodes[from].triggerOnly) return !project.nodes[to].triggerOnly && !project.nodes[to].overviewOnly && toAnswer == -1;
        if (project.nodes[to].triggerOnly) return false;
        if (fromAnswer == -2 || toAnswer == -2) return from == to && fromAnswer == -2 && toAnswer == -1;
        if (project.EventInfo(project.nodes[from]).enabled || project.EventInfo(project.nodes[to]).enabled) return false;
        if (from == to && fromAnswer == toAnswer) return false;
        if (project.nodes[from].overviewOnly != project.nodes[to].overviewOnly) return false;
        return fromAnswer < 0 || toAnswer < 0;
    }
    void LinkCards(int from, int fromAnswer, int to, int toAnswer)
    {
        if (project.nodes[from].triggerOnly)
        {
            if (!CanLink(from, fromAnswer, to, toAnswer)) return;
            var trigger = project.nodes[from]; var text = project.nodes[to];
            Undo.RegisterCompleteObjectUndo(project, "Auslöser mit Text verbinden");
            if (!trigger.eventInfo.random)
                foreach (var prior in project.nodes.Where(n => n.eventTriggerId == trigger.id && n != text))
                { prior.eventTriggerId = ""; prior.eventInfo.enabled = false; prior.eventBindingEdited = true; }
            text.eventTriggerId = trigger.id; text.eventInfo = JsonUtility.FromJson<StoryEventInfo>(JsonUtility.ToJson(trigger.eventInfo)); text.eventBindingEdited = true;
            markedCards.Clear(); markedCards.Add((trigger.id, -2)); selected = from; selectedAnswer = -2;
            Dirty(); return;
        }
        if (fromAnswer == -2 && from == to && toAnswer == -1) return;
        if (project.EventInfo(project.nodes[from]).enabled || project.EventInfo(project.nodes[to]).enabled)
        { ShowNotification(new GUIContent("Ereignisse sind an ihre Spielauslöser gebunden und bilden keine Dialogfolge.")); return; }
        if (!CanLink(from, fromAnswer, to, toAnswer))
        { ShowNotification(new GUIContent("Bitte eine andere, passende Karte wählen; Antworten führen zu Texten.")); return; }
        var source = project.nodes[from]; var target = project.nodes[to];
        if (toAnswer >= 0)
        {
            if (from == to) return;
            if (source.choices.Count >= 6) { ShowNotification(new GUIContent("Maximal sechs Antworten pro Text.")); return; }
            Vector2 world = AnswerWorldRect(target, toAnswer).position;
            Undo.RegisterCompleteObjectUndo(project, "Antwort verbinden");
            var choice = target.choices[toAnswer]; target.choices.RemoveAt(toAnswer); source.choices.Add(choice);
            choice.positioned = true; choice.position = world - source.position; choice.continueLegacy = false;
            choice.legacyAnswer = Mathf.Clamp(choice.legacyAnswer, 0, Mathf.Max(0, source.legacyAnswerCount - 1));
            selected = from; selectedAnswer = source.choices.Count - 1;
        }
        else
        {
            StoryChoice choice;
            bool create = fromAnswer < 0 && source.choices.Count != 1;
            if (create && !source.overviewOnly && source.choices.Count >= 6) { ShowNotification(new GUIContent("Maximal sechs Antworten pro Text.")); return; }
            Undo.RegisterCompleteObjectUndo(project, "Folgeschritt verbinden");
            if (fromAnswer >= 0) choice = source.choices[fromAnswer];
            else if (!create) choice = source.choices[0];
            else { choice = new StoryChoice { textKey = source.overviewOnly ? "" : "game.continue" }; source.choices.Add(choice); }
            if (choice.next != target.id) choice.continueLegacy = false;
            choice.next = target.id;
            selected = from; selectedAnswer = source.overviewOnly ? -1 : source.choices.IndexOf(choice);
        }
        markedCards.Clear(); markedCards.Add((source.id, selectedAnswer));
        Dirty(); ShowNotification(new GUIContent("Verbindung übernommen"));
    }
    Rect MarqueeBounds() => Rect.MinMaxRect(Mathf.Min(marqueeStart.x, marqueeEnd.x), Mathf.Min(marqueeStart.y, marqueeEnd.y), Mathf.Max(marqueeStart.x, marqueeEnd.x), Mathf.Max(marqueeStart.y, marqueeEnd.y));
    void PrimaryMarkedCard()
    {
        selected = selectedAnswer = -1;
        selectedDecoration = null;
        foreach (var card in markedCards)
        { int index = project.nodes.FindIndex(n => n.id == card.id); if (index >= 0) { selected = index; selectedAnswer = card.answer; selectedDecoration = null; }
            else if (project.decorations.Any(d => d.id == card.id)) { selected = -1; selectedDecoration = card.id; } }
    }
    void ConnectCards(Rect from, Rect to, Color color)
    {
        var starts = CardPorts(from); var ends = CardPorts(to);
        var normals = new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up };
        int source = 0, target = 0; float shortest = float.PositiveInfinity;
        for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++)
        {
            float distance = (starts[a] - ends[b]).sqrMagnitude;
            if (distance < shortest) { shortest = distance; source = a; target = b; }
        }
        float tangent = Mathf.Min(80 * boardZoom, Mathf.Sqrt(shortest) * .45f);
        Handles.DrawBezier(starts[source], ends[target], starts[source] + normals[source] * tangent,
            ends[target] + normals[target] * tangent, color, null, Mathf.Max(1, 2 * boardZoom));
    }
    static Vector2 AnswerOffset(int index) => new(330, index * 100);
    Rect AnswerWorldRect(StoryNode node, int index) => new(node.position + (node.choices[index].positioned ? node.choices[index].position : AnswerOffset(index)), new Vector2(240, 70));
    Rect AnswerRect(StoryNode node, int index) { var world = AnswerWorldRect(node, index); return new Rect((world.position - boardScroll) * boardZoom, world.size * boardZoom); }
    Rect ContentBounds(float padding = 200)
    {
        var nodes = project.nodes.Where(n => n.overviewOnly == StoryView && n.chapter == Sections.ElementAtOrDefault(chapter)).ToList();
        Vector2 min = nodes.Count == 0 ? Vector2.zero : nodes.Select(n => n.position).Aggregate(Vector2.Min) - Vector2.one * padding;
        Vector2 max = nodes.Count == 0 ? new Vector2(600, 400) : nodes.Select(n => n.position + new Vector2(230, 95)).Aggregate(Vector2.Max) + Vector2.one * padding;
        if (!StoryView) foreach (var node in nodes.Where(n => n.triggerOnly))
        { var trigger = EventWorldRect(node); min = Vector2.Min(min, trigger.min - Vector2.one * padding); max = Vector2.Max(max, trigger.max + Vector2.one * padding); }
        if (!StoryView) foreach (var node in nodes) for (int a = 0; a < node.choices.Count; a++)
        { var answer = AnswerWorldRect(node, a); min = Vector2.Min(min, answer.min - Vector2.one * padding); max = Vector2.Max(max, answer.max + Vector2.one * padding); }
        foreach (var d in BoardDecorations()) { var bounds = DecorationBounds(d); min = Vector2.Min(min, bounds.min - Vector2.one * padding); max = Vector2.Max(max, bounds.max + Vector2.one * padding); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    void RenameChapter()
    {
        string name = chapterName.Trim(), old = Sections[chapter];
        if (Sections.Contains(name)) { ShowNotification(new GUIContent("Dieser Kapitelname existiert bereits.")); return; }
        Edit("Kapitel umbenennen"); Sections[chapter] = name;
        foreach (var alias in project.chapterAliases.Where(alias => alias.name == old)) alias.name = name;
        if (!project.chapterAliases.Any(alias => alias.source == old)) project.chapterAliases.Add(new StoryChapterAlias { source = old, name = name });
        foreach (var node in project.nodes.Where(n => n.overviewOnly == StoryView && n.chapter == old)) node.chapter = name;
        Dirty(); GUIUtility.keyboardControl = 0;
    }
    void SaveAll()
    {
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        foreach (var draft in textDrafts.ToArray())
        {
            int separator = draft.Key.IndexOf('/'); if (separator < 0) continue;
            string lang = draft.Key.Substring(0, separator), key = draft.Key.Substring(separator + 1);
            if (StoryCatalog.Text(key, lang) != draft.Value) StoryCatalog.Write(key, lang, draft.Value);
        }
        Undo.CollapseUndoOperations(group); AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent("Gespeichert"));
    }
    void UndoOrRedo(bool redo)
    {
        if (!redo && textDrafts.Any(d =>
        {
            int separator = d.Key.IndexOf('/');
            return separator >= 0 && StoryCatalog.Text(d.Key.Substring(separator + 1), d.Key.Substring(0, separator)) != d.Value;
        })) SaveAll();
        if (redo) Undo.PerformRedo(); else Undo.PerformUndo();
    }
    string NodeTitle(StoryNode node)
    {
        string german = StoryCatalog.Text(node.textKey, "de").Split('\n')[0];
        if (german.Length > 65) german = german.Substring(0, 62) + "…";
        if (node.title != german || string.IsNullOrEmpty(node.textKey)) return node.title;
        string localized = StoryCatalog.Text(node.textKey, language).Split('\n')[0];
        return localized.Length > 65 ? localized.Substring(0, 62) + "…" : localized;
    }
    void Details()
    {
        var decoration = project.decorations.Find(d => d.id == selectedDecoration);
        if (decoration != null) { DecorationDetails(decoration); return; }
        if (selected < 0 || selected >= project.nodes.Count) { GUILayout.Label("Schritt im Board auswählen."); return; }
        if (markedCards.Count > 1) EditorGUILayout.HelpBox(markedCards.Count + " Karten markiert. Ziehen verschiebt die Auswahl; unten stehen die Eigenschaften der aktiven Karte.", MessageType.None);
        serialized.Update(); var node = serialized.FindProperty("nodes").GetArrayElementAtIndex(selected);
        var selectedNode = project.nodes[selected];
        if (selectedNode.triggerOnly) { TriggerDetails(node, selectedNode); return; }
        if (project.EventInfo(selectedNode).enabled)
        {
            GUILayout.Label("Ereignismeldung", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(EventSummary(project.EventInfo(selectedNode)) + "\n\n" + project.EventInfo(selectedNode).description, MessageType.Info);
            var commonTrigger = project.Find(selectedNode.eventTriggerId);
            if (commonTrigger != null)
            {
                if (GUILayout.Button("Gemeinsamen Auslöser öffnen")) { OpenNode(commonTrigger); GUIUtility.ExitGUI(); }
            }
            else using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(node.FindPropertyRelative("eventInfo"), new GUIContent("Auslöser aus dem Spielsystem"), true);
            EditorGUILayout.HelpBox("Die Auslöserdaten werden aus dem vorhandenen Storysystem gelesen. Texte und Audio sind hier bearbeitbar; der Auslöser wird nicht durch eine künstliche Dialogverbindung ersetzt.", MessageType.None);
        }
        if (!selectedNode.overviewOnly && selectedAnswer >= 0 && selectedAnswer < selectedNode.choices.Count)
        { AnswerDetails(node, selectedNode); return; }
        GUILayout.Label("Storyschritt", EditorStyles.boldLabel);
        foreach (string field in new[] { "id", "title", "chapter", "status", "purpose", "questions", "location", "relatedAsset" })
            InputField(node.FindPropertyRelative(field));
        var current = project.nodes[selected];
        if (current.overviewOnly)
        {
            var links = node.FindPropertyRelative("choices");
            GUILayout.Label("Nächste Storyschritte", EditorStyles.boldLabel);
            var storySteps = project.nodes.Where(n => n.overviewOnly).ToList();
            for (int i = 0; i < links.arraySize; i++)
            {
                var link = links.GetArrayElementAtIndex(i).FindPropertyRelative("next");
                int original = storySteps.FindIndex(n => n.id == link.stringValue) + 1;
                int picked = EditorGUILayout.Popup("Folgeschritt", original, new[] { "Ende" }.Concat(storySteps.Select(n => n.title)).ToArray());
                if (picked != original) link.stringValue = picked == 0 ? "" : storySteps[picked - 1].id;
                if (GUILayout.Button("Verbindung entfernen")) { links.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("Verbindung hinzufügen"))
            { serialized.ApplyModifiedProperties(); Edit("Storyverbindung"); current.choices.Add(new StoryChoice()); Dirty(); serialized.Update(); }
            serialized.ApplyModifiedProperties();
            GUILayout.Space(12); GUILayout.Label("Zugehörige Gespräche", EditorStyles.boldLabel);
            var associated = project.nodes.Where(n => !n.overviewOnly && n.storyStepId == current.id).ToList();
            foreach (var dialogue in associated)
                if (GUILayout.Button(NodeTitle(dialogue))) { OpenNode(dialogue); GUIUtility.ExitGUI(); }
            if (associated.Count == 0) GUILayout.Label("Noch keine Gespräche zugewiesen.");
            if (GUILayout.Button(markedCards.Count > 1 ? "Auswahl entfernen" : "Storyschritt entfernen")) { RemoveMarked(); GUIUtility.ExitGUI(); }
            return;
        }
        var steps = project.nodes.Where(n => n.overviewOnly).ToList();
        int stepIndex = steps.FindIndex(n => n.id == current.storyStepId) + 1;
        int pickedStep = EditorGUILayout.Popup("Storyschritt", stepIndex, new[] { "Nicht zugeordnet" }.Concat(steps.Select(n => n.title)).ToArray());
        if (pickedStep != stepIndex) node.FindPropertyRelative("storyStepId").stringValue = pickedStep == 0 ? "" : steps[pickedStep - 1].id;
        if (stepIndex > 0 && GUILayout.Button("Zugehörigen Storyschritt öffnen"))
        { serialized.ApplyModifiedProperties(); OpenNode(steps[stepIndex - 1]); GUIUtility.ExitGUI(); }
        foreach (string field in new[] { "channel", "speaker" }) InputField(node.FindPropertyRelative(field));
        EditorGUILayout.HelpBox("IDs bleiben stabil. Quellenverweise erhalten bestehende Missionsaktionen; neue Zweige können davor eingefügt werden.", MessageType.Info);
        InputField(node.FindPropertyRelative("textKey"));
        if (serialized.ApplyModifiedProperties()) Dirty();
        TextPair(current.textKey);
        serialized.Update(); node = serialized.FindProperty("nodes").GetArrayElementAtIndex(selected);
        foreach (string field in new[] { "voiceDE", "voiceEN", "sound", "conditions", "onEnter" }) EditorGUILayout.PropertyField(node.FindPropertyRelative(field), true);
        if (GUILayout.Button("Audio dieser Zeile hören")) PlayNode(current);
        if (project.EventInfo(current).enabled)
        {
            if (GUILayout.Button("Diese Meldung einzeln ansehen")) { previewMode = 0; StartPreview(current); tab = 3; }
            if (GUILayout.Button("Auslöser in der Vorschau öffnen"))
            { previewMode = 1; var signals = EventSignals(); previewSignal = Mathf.Max(0, Array.IndexOf(signals, StoryEventSimulator.Signal(project.EventInfo(current)))); tab = 3; }
            GUILayout.Label("Keine Antwortzweige: Diese Meldung wird durch ihr Ereignis ausgelöst.", EditorStyles.wordWrappedLabel);
            return;
        }
        var choices = node.FindPropertyRelative("choices");
        GUILayout.Label("Antworten / Verzweigungen", EditorStyles.boldLabel);
        for (int i = 0; i < choices.arraySize; i++)
        {
            var choice = choices.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                InputField(choice.FindPropertyRelative("textKey"));
                InputField(choice.FindPropertyRelative("reactionKey"));
                var ids = new[] { "" }.Concat(project.nodes.Select(n => n.id)).ToArray();
                var labels = new[] { "Ende" }.Concat(project.nodes.Select(n => NodeTitle(n) + " [" + n.id + "]")).ToArray();
                var next = choice.FindPropertyRelative("next"); int target = Array.IndexOf(ids, next.stringValue);
                int picked = EditorGUILayout.Popup("Nächster Schritt", Mathf.Max(0, target), labels);
                if (picked != Mathf.Max(0, target)) next.stringValue = ids[picked];
                foreach (string field in new[] { "conditions", "effects", "continueLegacy", "legacyAnswer" }) EditorGUILayout.PropertyField(choice.FindPropertyRelative(field), true);
                if (GUILayout.Button("Antwort löschen")) { choices.DeleteArrayElementAtIndex(i); break; }
            }
        }
        if (GUILayout.Button("Antwort hinzufügen"))
        {
            serialized.ApplyModifiedProperties(); Edit("Antwort");
            current.choices.Add(new StoryChoice { textKey = current.id + ".answer." + Guid.NewGuid().ToString("N"), legacyAnswer = 0 }); Dirty(); serialized.Update();
        }
        if (serialized.ApplyModifiedProperties()) Dirty();
        foreach (var choice in current.choices)
        { GUILayout.Label("Antworttext", EditorStyles.miniBoldLabel); TextPair(choice.textKey); if (!string.IsNullOrEmpty(choice.reactionKey)) TextPair(choice.reactionKey); }
        GUILayout.Label("Bestehende Quelle", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(current.sourcePath + "\n" + current.sourceProperty, GUILayout.Height(40));
        if (GUILayout.Button("Quelle auswählen")) Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(current.sourcePath);
        if (GUILayout.Button("Ab hier durchspielen")) { StartPreview(current); tab = 3; }
        if (GUILayout.Button(markedCards.Count > 1 ? "Auswahl entfernen" : "Text entfernen")) { RemoveMarked(); GUIUtility.ExitGUI(); }
    }
    void OpenNode(StoryNode node)
    {
        selectedDecoration = null;
        selectedAnswer = node.triggerOnly ? -2 : -1;
        markedCards.Clear(); markedCards.Add((node.id, selectedAnswer));
        tab = node.overviewOnly ? 0 : 1; previousBoardTab = tab;
        chapter = Mathf.Max(0, Sections.IndexOf(node.chapter)); chapterNameIndex = -1;
        selected = project.nodes.IndexOf(node); search = ""; focusBoard = true; Repaint();
        messageFilter = project.EventInfo(node).enabled ? 2 : 0;
    }
    void AnswerDetails(SerializedProperty node, StoryNode owner)
    {
        GUILayout.Label("Antwort " + (selectedAnswer + 1), EditorStyles.boldLabel);
        GUILayout.Label("Auf Text: " + NodeTitle(owner), EditorStyles.wordWrappedLabel);
        var choice = node.FindPropertyRelative("choices").GetArrayElementAtIndex(selectedAnswer);
        InputField(choice.FindPropertyRelative("textKey")); InputField(choice.FindPropertyRelative("reactionKey"));
        var targets = project.nodes.Where(n => !n.overviewOnly).ToList();
        var next = choice.FindPropertyRelative("next"); int old = targets.FindIndex(n => n.id == next.stringValue) + 1;
        int picked = EditorGUILayout.Popup("Folgetext", old, new[] { "Gespräch beenden" }.Concat(targets.Select(NodeTitle)).ToArray());
        if (picked != old)
        {
            next.stringValue = picked == 0 ? "" : targets[picked - 1].id;
            if (picked > 0 && string.IsNullOrEmpty(targets[picked - 1].sourceReference)) choice.FindPropertyRelative("continueLegacy").boolValue = false;
        }
        foreach (string field in new[] { "conditions", "effects", "continueLegacy", "legacyAnswer" }) EditorGUILayout.PropertyField(choice.FindPropertyRelative(field), true);
        serialized.ApplyModifiedProperties();
        TextPair(owner.choices[selectedAnswer].textKey);
        if (!string.IsNullOrEmpty(owner.choices[selectedAnswer].reactionKey)) TextPair(owner.choices[selectedAnswer].reactionKey);
        if (GUILayout.Button("Textkarte auswählen")) { selectedAnswer = -1; markedCards.Clear(); markedCards.Add((owner.id, -1)); }
        if (GUILayout.Button(markedCards.Count > 1 ? "Auswahl entfernen" : "Antwort entfernen")) { RemoveMarked(); GUIUtility.ExitGUI(); }
    }
    void TextPair(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        GUILayout.Label(key, EditorStyles.wordWrappedMiniLabel);
        bool changed = false;
        using (new EditorGUILayout.HorizontalScope())
        foreach (string lang in new[] { "de", "en" })
        {
            using (new EditorGUILayout.VerticalScope())
            {
            string text = StoryCatalog.Text(key, lang);
            string draftKey = lang + "/" + key;
            if (!textDrafts.ContainsKey(draftKey)) textDrafts[draftKey] = text;
            GUILayout.Label(lang.ToUpperInvariant(), EditorStyles.miniBoldLabel);
            var textStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, padding = new RectOffset(8, 8, 8, 8) };
            textDrafts[draftKey] = EditorGUILayout.TextArea(textDrafts[draftKey], textStyle, GUILayout.MinHeight(110));
            changed |= textDrafts[draftKey] != text;
            if (string.IsNullOrEmpty(text)) EditorGUILayout.HelpBox("Übersetzung fehlt.", MessageType.Warning);
            else if (StoryCatalog.TooLong(text)) EditorGUILayout.HelpBox("Kürzen: möglichst 1–2 kurze Sätze, eine Aussage.", MessageType.Warning);
            }
        }
        if (changed && GUILayout.Button("DE/EN-Text übernehmen"))
        {
            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Storytext DE/EN");
            foreach (string lang in new[] { "de", "en" }) StoryCatalog.Write(key, lang, textDrafts[lang + "/" + key]);
            Undo.CollapseUndoOperations(group);
        }
    }
    void Characters()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(PanelStyle(), GUILayout.Width(260)))
            {
                GUILayout.Label("Charaktere", EditorStyles.boldLabel); GUILayout.Space(10);
                characterScroll = EditorGUILayout.BeginScrollView(characterScroll);
                foreach (string guid in AssetDatabase.FindAssets("t:StoryCharacter"))
                {
                    var item = AssetDatabase.LoadAssetAtPath<StoryCharacter>(AssetDatabase.GUIDToAssetPath(guid));
                    string name = StoryCatalog.Text(item.nameKey, language);
                    if (GUILayout.Toggle(character == item, new GUIContent(string.IsNullOrEmpty(name) ? item.id : name, item.id), "Button", GUILayout.Height(30))) character = item;
                    GUILayout.Space(4);
                }
                EditorGUILayout.EndScrollView();
                GUILayout.Space(8);
                GUILayout.Label("Neuen Charakter anlegen", EditorStyles.boldLabel); GUILayout.Space(8);
                GUILayout.Label("Neue ID", EditorStyles.miniBoldLabel);
                newCharacter = EditorGUILayout.TextField(newCharacter, GUILayout.Height(26));
                GUILayout.Space(8);
                if (GUILayout.Button("Charakter erstellen", GUILayout.Height(30)) && System.Text.RegularExpressions.Regex.IsMatch(newCharacter, @"^[a-zA-Z0-9_.-]+$"))
                {
                    string path = "Assets/Resources/Story/Characters/" + newCharacter + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<StoryCharacter>(path)) { EditorUtility.DisplayDialog("ID vergeben", "Diese Charakter-ID existiert bereits.", "OK"); return; }
                    character = CreateInstance<StoryCharacter>(); character.id = newCharacter; character.nameKey = "story.character." + newCharacter;
                    AssetDatabase.CreateAsset(character, path); Undo.RegisterCreatedObjectUndo(character, "Charakter erstellen"); newCharacter = "";
                }
            }
            using (new EditorGUILayout.VerticalScope(PanelStyle(), GUILayout.Width(Mathf.Clamp(position.width - 300, 640, 980) * .6f)))
            {
                if (!character) { GUILayout.Label("Charakter links auswählen."); return; }
                characterDetailsScroll = EditorGUILayout.BeginScrollView(characterDetailsScroll);
                var so = new SerializedObject(character); so.Update();
                GUILayout.Label("Identität", EditorStyles.boldLabel); GUILayout.Space(8);
                foreach (string field in new[] { "id", "nameKey" }) InputField(so.FindProperty(field));
                GUILayout.Space(8); GUILayout.Label("Darstellung", EditorStyles.boldLabel); GUILayout.Space(8);
                foreach (string field in new[] { "prefab", "portrait" }) InputField(so.FindProperty(field));
                GUILayout.Space(12); GUILayout.Label("Name im Spiel", EditorStyles.boldLabel); GUILayout.Space(8);
                so.ApplyModifiedProperties(); TextPair(character.nameKey);

                GUILayout.Space(16); GUILayout.Label("Gespräche dieses Charakters", EditorStyles.boldLabel); GUILayout.Space(8);
                foreach (var node in project.nodes.Where(n => n.speaker == character))
                {
                    if (GUILayout.Button(NodeTitle(node), new GUIStyle(GUI.skin.button) { wordWrap = true }, GUILayout.MinHeight(30))) { selected = project.nodes.IndexOf(node); OpenNode(node); }
                    GUILayout.Space(4);
                }
                EditorGUILayout.HelpBox("Prefab und Porträt zentral zuweisen. Für importierte Figuren fehlen teilweise noch individuelle Porträts und bestätigte Rollen; die Prüfung zeigt diese an.", MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(PanelStyle(), GUILayout.Width(Mathf.Clamp(position.width - 300, 640, 980) * .4f)))
            {
                characterPreview.Draw(character.prefab);
                GUILayout.Space(12); GUILayout.Label("Gesprächsporträt", EditorStyles.boldLabel);
                Portrait(character.portrait);
                if (!character.portrait) GUILayout.Label("Noch kein Porträt zugewiesen.");
            }
            GUILayout.FlexibleSpace();
        }
    }
    void StartPreview(StoryNode node)
    {
        StopAudio(); trace.Clear();
        preview = new StorySession(project, key => flags.TryGetValue(key, out bool value) && value, (key, value) => flags[key] = value);
        selected = project.nodes.IndexOf(node);
        previewStart = Mathf.Max(0, project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly).ToList().IndexOf(node));
        if (!preview.Begin(node)) { preview = null; trace.Add("Voraussetzungen nicht erfüllt. Simulierte Flags rechts setzen."); return; }
        trace.Add(node.title); if (autoAudio) PlayNode(node);
    }
    void Preview()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(PanelStyle(), GUILayout.Width(Mathf.Clamp(position.width - 350, 450, 760))))
            {
                previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
                GUILayout.Label("Gesprächsvorschau · " + language.ToUpperInvariant(), EditorStyles.boldLabel);
                previewMode = GUILayout.Toolbar(previewMode, new[] { "Einzeltext / Gespräch", "Ereignis auslösen" });
                var starts = project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly).ToList();
                if (starts.Count == 0) { GUILayout.Label("Noch keine Dialogtexte vorhanden."); EditorGUILayout.EndScrollView(); return; }
                previewStart = Mathf.Clamp(previewStart, 0, starts.Count - 1);
                if (previewMode == 0)
                {
                    previewStart = EditorGUILayout.Popup("Starttext", previewStart, starts.Select(n => n.chapter + " · " + NodeTitle(n)).ToArray());
                    if (GUILayout.Button("Text / Gespräch starten", GUILayout.Height(30))) StartPreview(starts[previewStart]);
                }
                else DrawEventPreview();
                autoAudio = EditorGUILayout.Toggle("Audio automatisch", autoAudio);
                if (selected >= 0 && selected < project.nodes.Count && GUILayout.Button("Ausgewählten Schritt neu starten")) StartPreview(project.nodes[selected]);
                if (preview?.Node != null)
                {
                    var node = preview.Node;
                    GUILayout.Label(node.channel.ToString(), EditorStyles.miniLabel);
                    if (node.channel != StoryChannel.Radio && node.speaker) Portrait(node.speaker.portrait);
                    GUILayout.Label(node.speaker ? StoryCatalog.Text(node.speaker.nameKey, language) : "", EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(preview.Reaction)) GUILayout.Label(StoryCatalog.Text(preview.Reaction, language), EditorStyles.wordWrappedLabel);
                    GUILayout.Label(StoryCatalog.Text(node.textKey, language), EditorStyles.wordWrappedLabel);
                    GUILayout.Space(15);
                    var options = preview.Choices;
                    for (int i = 0; i < options.Count; i++)
                    {
                        int index = i;
                        if (GUILayout.Button(StoryCatalog.Text(options[i].textKey, language), GUILayout.MinHeight(34)))
                        {
                            var previousNode = preview.Node;
                            if (!preview.Choose(index, false)) trace.Add("Ziel fehlt oder seine Bedingungen sind nicht erfüllt.");
                            else { if (previousNode.eventInfo.enabled) eventSimulator.Confirm(project, previousNode); StopAudio(); trace.Add(preview.Node == null ? "Gespräch beendet." : preview.Node.title); if (autoAudio && preview.Node != null) PlayNode(preview.Node); }
                            break;
                        }
                    }
                    if (options.Count == 0) EditorGUILayout.HelpBox("Keine Antwort erfüllt ihre Bedingungen. Simulierte Flags rechts prüfen oder die Bedingungen an der Antwort anpassen.", MessageType.Warning);
                    if (GUILayout.Button("Audio wiederholen")) PlayNode(node);
                    if (!(language == "de" ? node.voiceDE : node.voiceEN) && !node.sound)
                        EditorGUILayout.HelpBox("Für diese Zeile ist noch keine Sprachaufnahme oder Sounddatei zugewiesen.", MessageType.None);
                }
                else
                {
                    GUILayout.Label("Vorschau beendet oder noch nicht gestartet.");
                    if (!string.IsNullOrEmpty(preview?.Reaction)) GUILayout.Label(StoryCatalog.Text(preview.Reaction, language), EditorStyles.wordWrappedLabel);
                }
                using (new EditorGUI.DisabledScope(preview?.Node == null || !(language == "de" ? preview.Node.voiceDE : preview.Node.voiceEN) && !preview.Node.sound))
                    if (GUILayout.Button("Audio stoppen")) StopAudio();
                GUILayout.Space(20); GUILayout.Label("Verlauf", EditorStyles.boldLabel);
                foreach (string item in trace.TakeLast(12)) GUILayout.Label(item, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(PanelStyle(), GUILayout.Width(300)))
            {
                EditorGUILayout.HelpBox("Nur simulierte Flags. Kein Zugriff auf Spielstand oder echte Missionsaktionen. Bedingungen und Flagänderungen entsprechen dem Spiel; bestehende Missionsaktionen werden hier nicht ausgeführt.", MessageType.Info);
                foreach (string key in flags.Keys.ToArray()) flags[key] = EditorGUILayout.Toggle(key, flags[key]);
                GUILayout.Label("Simulierter Zustand (Flag-ID)", EditorStyles.miniBoldLabel);
                flagKey = EditorGUILayout.TextField(flagKey, GUILayout.Height(26));
                GUILayout.Label("Beispiel: story.registrationComplete. Nur für Bedingungen dieser Vorschau.", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Flag hinzufügen") && !string.IsNullOrWhiteSpace(flagKey)) { flags[flagKey] = true; flagKey = ""; }
                if (GUILayout.Button("Simulation zurücksetzen")) { flags.Clear(); preview = null; trace.Clear(); eventSimulator.Reset(); eventPreviewReason = ""; StopAudio(); }
                GUILayout.Label("Löscht simulierte Flags und Verlauf. Spielstände bleiben unverändert.", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
    string[] EventSignals() => new[] { "Trainingsstart" }.Concat(project.nodes.Where(n => n.triggerOnly).Select(n => StoryEventSimulator.Signal(project.EventInfo(n)))).Distinct().ToArray();
    void DrawEventPreview()
    {
        var signals = EventSignals(); previewSignal = Mathf.Clamp(previewSignal, 0, signals.Length - 1);
        previewSignal = EditorGUILayout.Popup("Ereignis", previewSignal, signals);
        previewLevel = Mathf.Max(1, EditorGUILayout.IntField("Trainingslevel", previewLevel));
        var candidates = eventSimulator.Candidates(project, signals[previewSignal], previewLevel);
        if (GUILayout.Button("Ereignis auslösen", GUILayout.Height(30)))
        {
            var result = eventSimulator.Fire(project, signals[previewSignal], previewLevel);
            StopAudio(); preview = null; eventPreviewReason = result.reason;
            if (result.node != null) StartPreview(result.node);
            else trace.Add(result.reason);
        }
        if (!string.IsNullOrEmpty(eventPreviewReason)) EditorGUILayout.HelpBox(eventPreviewReason, MessageType.Info);
        GUILayout.Label("Mögliche Texte für dieses Ereignis: " + candidates.Count, EditorStyles.boldLabel);
        foreach (var node in candidates)
            if (GUILayout.Button(NodeTitle(node), new GUIStyle(GUI.skin.button) { wordWrap = true }, GUILayout.MinHeight(28))) StartPreview(node);
        EditorGUILayout.HelpBox("Die Liste zeigt passende Varianten; Auslösen berücksichtigt auch Zufallschance und Einmaligkeit. Physische Flugbedingungen und echtes Timing werden hier nicht simuliert.", MessageType.None);
    }
    void Validation()
    {
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        var messages = Issues(project).ToList();
        GUILayout.Label(messages.Count + " Hinweise", EditorStyles.boldLabel);
        foreach (var issue in messages)
            if (GUILayout.Button(issue, EditorStyles.wordWrappedLabel)) { selected = project.nodes.FindIndex(n => issue.StartsWith(n.id + ":")); if (selected >= 0) { OpenNode(project.nodes[selected]); } }
        EditorGUILayout.EndScrollView();
    }
    public static IEnumerable<string> Issues(StoryProject project)
    {
        foreach (var trigger in project.nodes.Where(n => n.triggerOnly))
            if (project.TriggerTexts(trigger.id).Length == 0) yield return trigger.id + ": Auslöser hat keinen verbundenen Text.";
        var de = StoryCatalog.Load("de").entries.GroupBy(e => e.key).ToDictionary(g => g.Key, g => g.First().text);
        var en = StoryCatalog.Load("en").entries.GroupBy(e => e.key).ToDictionary(g => g.Key, g => g.First().text);
        foreach (var group in project.nodes.GroupBy(n => n.id).Where(g => string.IsNullOrEmpty(g.Key) || g.Count() > 1)) yield return group.Key + ": ID fehlt oder doppelt.";
        foreach (var node in project.nodes)
        {
            if (node.overviewOnly || node.triggerOnly) continue;
            if (!node.speaker) yield return node.id + ": Sprecher fehlt.";
            else
            {
                if (!node.speaker.prefab) yield return node.id + ": Charakter-Prefab fehlt (z. B. noch unbekannte Figur).";
                if (node.channel != StoryChannel.Radio && !node.speaker.portrait) yield return node.id + ": Porträt fehlt.";
            }
            foreach (string key in new[] { node.textKey, node.speaker ? node.speaker.nameKey : "" }.Concat(node.choices.SelectMany(c => new[] { c.textKey, c.reactionKey })).Where(k => !string.IsNullOrEmpty(k)).Distinct())
                foreach (var pair in new[] { ("DE", de), ("EN", en) })
                    if (!pair.Item2.TryGetValue(key, out var text) || string.IsNullOrEmpty(text)) yield return node.id + ": " + pair.Item1 + " fehlt: " + key;
                    else if (StoryCatalog.TooLong(text)) yield return node.id + ": " + pair.Item1 + " Text kürzen: " + key;
            foreach (var choice in node.choices)
            {
                if (!string.IsNullOrEmpty(choice.next) && project.Find(choice.next) == null) yield return node.id + ": Ziel fehlt: " + choice.next;
                if (node.legacyAnswerCount > 0 && (choice.legacyAnswer < 0 || choice.legacyAnswer >= node.legacyAnswerCount)) yield return node.id + ": Ungültiger Index der ursprünglichen Antwort.";
            }
            foreach (var flag in node.conditions.Concat(node.onEnter).Concat(node.choices.SelectMany(c => c.conditions.Concat(c.effects))))
                if (string.IsNullOrWhiteSpace(flag.key)) yield return node.id + ": Flag-ID fehlt.";
            if (node.choices.Count > 6) yield return node.id + ": Mehr als sechs Antworten; Laufzeit-UI erweitern.";
            if (!project.dialogueGroups.Contains(node.chapter)) yield return node.id + ": Kapitel fehlt.";
            if (!string.IsNullOrEmpty(node.storyStepId) && !project.nodes.Any(n => n.overviewOnly && n.id == node.storyStepId)) yield return node.id + ": Zugeordneter Storyschritt fehlt.";
        }
    }
    static void Portrait(Sprite sprite)
    {
        if (!sprite) return; var rect = GUILayoutUtility.GetRect(120, 120, GUILayout.Width(120));
        var uv = sprite.textureRect; uv.x /= sprite.texture.width; uv.width /= sprite.texture.width; uv.y /= sprite.texture.height; uv.height /= sprite.texture.height;
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv);
    }
    static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    public static void StopAudio() => AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
    static void Play(AudioClip clip)
    {
        if (!clip) return;
        var method = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        if (method == null) { Debug.LogError("Unity Audio-Vorschau ist in dieser Editorversion nicht verfügbar."); return; }
        method.Invoke(null, new object[] { clip, 0, false });
    }
    static void PlayNode(StoryNode node) { StopAudio(); Play(node.sound); Play(EditorWindow.GetWindow<StoryEditorWindow>().language == "de" ? node.voiceDE : node.voiceEN); }
    void Edit(string name) => Undo.RecordObject(project, name);
    void Dirty() { EditorUtility.SetDirty(project); Repaint(); }
}

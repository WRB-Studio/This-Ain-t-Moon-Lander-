using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class StoryEditorWindow : EditorWindow
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
    int selectedAnswer = -1, draggedAnswer = -1, previewStart;
    Vector2 previewScroll, characterDetailsScroll;
    readonly StoryCharacterPreview characterPreview = new();
    bool StoryView => tab == 0;
    List<string> Sections => StoryView ? project.chapters : project.dialogueGroups;
    int previousBoardTab = -1;
    public static void Open() { var window = GetWindow<StoryEditorWindow>("Story & Charaktere"); window.minSize = new Vector2(1050, 650); }
    double nextCharacterFrame;
    void OnEnable() { language = SessionState.GetString("StoryEditor.ContentLanguage", Localization.CurrentLanguage); Undo.undoRedoPerformed += Refresh; EditorApplication.update += CharacterFrame; Refresh(); }
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
        textDrafts.Clear(); chapter = Mathf.Clamp(chapter, 0, project ? Mathf.Max(0, Sections.Count - 1) : 0);
        if (!project || selected >= project.nodes.Count) selected = -1;
        chapterNameIndex = -1; drag = -1; selectedAnswer = draggedAnswer = -1; panning = false; preview = null; StopAudio(); Repaint();
    }
    void OnGUI()
    {
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
        { previousBoardTab = tab; chapter = 0; selected = -1; chapterNameIndex = -1; search = ""; focusBoard = true; }
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
                GUILayout.Space(8);
                for (int i = 0; i < Sections.Count; i++)
                {
                    if (GUILayout.Toggle(chapter == i, Sections[i], "Button", GUILayout.Height(28)) && chapter != i)
                    { chapter = i; selected = -1; chapterNameIndex = -1; focusBoard = true; }
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
                    project.nodes.Add(new StoryNode { id = id, title = "Neuer Schritt", chapter = Sections.ElementAtOrDefault(chapter), textKey = StoryView ? "" : id, overviewOnly = StoryView, position = boardScroll + new Vector2(30, 30),
                        answerBoardLayout = true, choices = new() { new StoryChoice { textKey = StoryView ? "" : "game.continue" } } });
                    selected = project.nodes.Count - 1; selectedAnswer = -1; Dirty();
                }
                GUILayout.Space(8);
                if (GUILayout.Button(new GUIContent("Quellen ergänzen", "Ergänzt neue Inhalte aus dem Spiel. Vorhandene Storyschritte und Texte bleiben erhalten."), GUILayout.Height(28)))
                { SaveAll(); StoryImport.Build(); Refresh(); }
                }
                GUILayout.Space(8);
                if (GUILayout.Button(new GUIContent("Inhalte fokussieren", "Zeigt alle Nodes des aktuellen Kapitels und setzt die Suche zurück."), GUILayout.Height(28)))
                { search = ""; focusBoard = true; Repaint(); }
                GUILayout.Space(8);
                showNavigationHelp = EditorGUILayout.Foldout(showNavigationHelp, "Navigation", true);
                if (showNavigationHelp) EditorGUILayout.HelpBox("Mausrad: Zoom am Mauszeiger.\nLeere Fläche ziehen: Board bewegen.\nKarte ziehen: Schritt verschieben.\nMittlere/rechte Maustaste: überall navigieren.", MessageType.None);
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
        float grid = 40 * boardZoom; while (grid < 24) grid *= 2;
        for (float x = -Mathf.Repeat(boardScroll.x * boardZoom, grid); x < rect.width; x += grid) Handles.DrawLine(new Vector2(x, 0), new Vector2(x, rect.height));
        for (float y = -Mathf.Repeat(boardScroll.y * boardZoom, grid); y < rect.height; y += grid) Handles.DrawLine(new Vector2(0, y), new Vector2(rect.width, y));
        Handles.color = Color.white; Handles.EndGUI();
        var worldBounds = ContentBounds();
        var e = Event.current;
        if (e.type == EventType.MouseDown && view.Contains(e.mousePosition))
        {
            int hit = indices.Where(i => NodeRect(project.nodes[i]).Contains(e.mousePosition)).DefaultIfEmpty(-1).Last();
            int answerHit = -1;
            if (!StoryView) foreach (int owner in indices) for (int a = 0; a < project.nodes[owner].choices.Count; a++)
                if (AnswerRect(project.nodes[owner], a).Contains(e.mousePosition)) { hit = owner; answerHit = a; }
            if (e.button == 1 || e.button == 2 || e.button == 0 && hit < 0)
            { panning = true; drag = -1; GUIUtility.hotControl = control; lastMouse = e.mousePosition; GUIUtility.keyboardControl = 0; e.Use(); }
            else if (e.button == 0)
            { selected = drag = hit; selectedAnswer = draggedAnswer = answerHit; panning = false; dragRecorded = false; GUIUtility.hotControl = control; lastMouse = e.mousePosition; GUIUtility.keyboardControl = 0; e.Use(); Repaint(); }
        }
        if (GUIUtility.hotControl == control && e.type == EventType.MouseDrag)
        {
            Vector2 delta = (e.mousePosition - lastMouse) / boardZoom; lastMouse = e.mousePosition;
            if (panning) boardScroll -= delta;
            else if (drag >= 0 && drag < project.nodes.Count)
            {
                if (!dragRecorded) { Undo.IncrementCurrentGroup(); dragUndoGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(project, "Storyschritt verschieben"); dragRecorded = true; }
                if (draggedAnswer < 0) project.nodes[drag].position += delta;
                else
                {
                    var choice = project.nodes[drag].choices[draggedAnswer];
                    if (!choice.positioned) choice.position = AnswerOffset(draggedAnswer);
                    choice.positioned = true; choice.position += delta;
                }
                Dirty();
            }
            e.Use(); Repaint();
        }
        if (GUIUtility.hotControl == control && e.type == EventType.MouseUp)
        {
            if (dragRecorded) Undo.CollapseUndoOperations(dragUndoGroup);
            dragRecorded = false; drag = draggedAnswer = -1; panning = false; GUIUtility.hotControl = 0; e.Use();
        }
        if (e.type == EventType.ScrollWheel && view.Contains(e.mousePosition))
        {
            Vector2 anchor = boardScroll + e.mousePosition / boardZoom;
            boardZoom = Mathf.Clamp(boardZoom * Mathf.Exp(-e.delta.y * .08f), .05f, 2.5f);
            boardScroll = anchor - e.mousePosition / boardZoom;
            e.Use(); Repaint();
        }
        Handles.BeginGUI();
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
            var node = project.nodes[i]; Rect box = NodeRect(node);
            EditorGUI.DrawRect(new Rect(box.x + 3 * boardZoom, box.y + 4 * boardZoom, box.width, box.height), new Color(0, 0, 0, .45f));
            Color border = selected == i ? new Color(.3f, .72f, 1) : new Color(.48f, .5f, .53f);
            float edge = Mathf.Max(1, (selected == i ? 2 : 1) * boardZoom);
            EditorGUI.DrawRect(box, border);
            EditorGUI.DrawRect(new Rect(box.x + edge, box.y + edge, box.width - edge * 2, box.height - edge * 2), new Color(.25f, .26f, .28f));
            EditorGUI.DrawRect(new Rect(box.x + edge, box.y + edge, box.width - edge * 2, 47 * boardZoom), selected == i ? new Color(.16f, .28f, .38f) : new Color(.2f, .21f, .23f));
            GUI.Label(new Rect(box.x + 10 * boardZoom, box.y + 6 * boardZoom, 210 * boardZoom, 40 * boardZoom), NodeTitle(node), titleStyle);
            string info = node.overviewOnly ? node.status + " · Storyschritt\n" + project.nodes.Count(n => !n.overviewOnly && n.storyStepId == node.id) + " Gesprächszeilen"
                : "Text · " + node.channel + "\n" + (node.speaker ? StoryCatalog.Text(node.speaker.nameKey, language) : "Kein Sprecher");
            GUI.Label(new Rect(box.x + 10 * boardZoom, box.y + 54 * boardZoom, 210 * boardZoom, 34 * boardZoom), info, infoStyle);
        }
        if (!StoryView) foreach (int i in indices) for (int a = 0; a < project.nodes[i].choices.Count; a++)
        {
            Rect box = AnswerRect(project.nodes[i], a);
            EditorGUI.DrawRect(box, selected == i && selectedAnswer == a ? new Color(.3f, .72f, 1) : new Color(.35f, .58f, .42f));
            EditorGUI.DrawRect(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), new Color(.15f, .24f, .18f));
            GUI.Label(new Rect(box.x + 8 * boardZoom, box.y + 5 * boardZoom, 220 * boardZoom, 18 * boardZoom), "Antwort " + (a + 1), titleStyle);
            string text = StoryCatalog.Text(project.nodes[i].choices[a].textKey, language);
            GUI.Label(new Rect(box.x + 8 * boardZoom, box.y + 25 * boardZoom, 220 * boardZoom, 40 * boardZoom), string.IsNullOrEmpty(text) ? "Antworttext fehlt" : text, titleStyle);
        }
        GUI.Label(new Rect(8, rect.height - 22, rect.width - 16, 20), $"Zoom: {boardZoom:P0} · Board: {worldBounds.width:0} × {worldBounds.height:0}", EditorStyles.miniLabel);
        GUI.EndGroup();
    }
    Rect NodeRect(StoryNode node) => new((node.position - boardScroll) * boardZoom, new Vector2(230, 95) * boardZoom);
    void ConnectCards(Rect from, Rect to, Color color)
    {
        Vector2[] Ports(Rect r) => new[] { new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y), new Vector2(r.center.x, r.yMin), new Vector2(r.center.x, r.yMax) };
        var starts = Ports(from); var ends = Ports(to);
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
        if (nodes.Count == 0) return new Rect(0, 0, 600, 400);
        Vector2 min = nodes.Select(n => n.position).Aggregate(Vector2.Min) - Vector2.one * padding;
        Vector2 max = nodes.Select(n => n.position + new Vector2(230, 95)).Aggregate(Vector2.Max) + Vector2.one * padding;
        if (!StoryView) foreach (var node in nodes) for (int a = 0; a < node.choices.Count; a++)
        { var answer = AnswerWorldRect(node, a); min = Vector2.Min(min, answer.min - Vector2.one * padding); max = Vector2.Max(max, answer.max + Vector2.one * padding); }
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
        if (selected < 0 || selected >= project.nodes.Count) { GUILayout.Label("Schritt im Board auswählen."); return; }
        serialized.Update(); var node = serialized.FindProperty("nodes").GetArrayElementAtIndex(selected);
        var selectedNode = project.nodes[selected];
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
            if (GUILayout.Button("Storyschritt löschen") && EditorUtility.DisplayDialog("Storyschritt löschen", "Gespräche bleiben erhalten; ihre Zuordnung muss danach angepasst werden.", "Löschen", "Abbrechen"))
            { Edit("Storyschritt löschen"); project.nodes.RemoveAt(selected); selected = -1; Dirty(); }
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
        if (GUILayout.Button("Schritt löschen") && EditorUtility.DisplayDialog("Storyschritt löschen", "Verweise auf diesen Schritt müssen anschließend angepasst werden.", "Löschen", "Abbrechen"))
        { Edit("Schritt löschen"); project.nodes.RemoveAt(selected); selected = -1; Dirty(); }
    }
    void OpenNode(StoryNode node)
    {
        selectedAnswer = -1;
        tab = node.overviewOnly ? 0 : 1; previousBoardTab = tab;
        chapter = Mathf.Max(0, Sections.IndexOf(node.chapter)); chapterNameIndex = -1;
        selected = project.nodes.IndexOf(node); search = ""; focusBoard = true; Repaint();
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
        if (GUILayout.Button("Textkarte auswählen")) selectedAnswer = -1;
        if (GUILayout.Button("Antwort entfernen"))
        { Edit("Antwort entfernen"); owner.choices.RemoveAt(selectedAnswer); selectedAnswer = -1; Dirty(); }
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
        previewStart = Mathf.Max(0, project.nodes.Where(n => !n.overviewOnly).ToList().IndexOf(node));
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
                var starts = project.nodes.Where(n => !n.overviewOnly).ToList();
                if (starts.Count == 0) { GUILayout.Label("Noch keine Dialogtexte vorhanden."); EditorGUILayout.EndScrollView(); return; }
                previewStart = Mathf.Clamp(previewStart, 0, starts.Count - 1);
                previewStart = EditorGUILayout.Popup("Starttext", previewStart, starts.Select(n => n.chapter + " · " + NodeTitle(n)).ToArray());
                if (GUILayout.Button("Gespräch starten", GUILayout.Height(30))) StartPreview(starts[previewStart]);
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
                            if (!preview.Choose(index, false)) trace.Add("Ziel fehlt oder seine Bedingungen sind nicht erfüllt.");
                            else { StopAudio(); trace.Add(preview.Node == null ? "Gespräch beendet." : preview.Node.title); if (autoAudio && preview.Node != null) PlayNode(preview.Node); }
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
                if (GUILayout.Button("Simulation zurücksetzen")) { flags.Clear(); preview = null; trace.Clear(); StopAudio(); }
                GUILayout.Label("Löscht simulierte Flags und Verlauf. Spielstände bleiben unverändert.", EditorStyles.wordWrappedMiniLabel);
            }
        }
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
        var de = StoryCatalog.Load("de").entries.GroupBy(e => e.key).ToDictionary(g => g.Key, g => g.First().text);
        var en = StoryCatalog.Load("en").entries.GroupBy(e => e.key).ToDictionary(g => g.Key, g => g.First().text);
        foreach (var group in project.nodes.GroupBy(n => n.id).Where(g => string.IsNullOrEmpty(g.Key) || g.Count() > 1)) yield return group.Key + ": ID fehlt oder doppelt.";
        foreach (var node in project.nodes)
        {
            if (node.overviewOnly) continue;
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

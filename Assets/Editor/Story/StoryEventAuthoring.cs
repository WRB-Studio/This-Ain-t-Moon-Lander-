using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StoryEventAuthoring
{
    public static void Read(StoryProject project, StoryTextController source, string path)
    {
        var so = new SerializedObject(source);
        var groups = so.FindProperty("earthMessages"); var transmissions = so.FindProperty("earthTransmissions");
        for (int i = 0; i < groups.arraySize; i++)
        {
            var group = groups.GetArrayElementAtIndex(i); int level = group.FindPropertyRelative("fromLevel").intValue;
            int end = Enumerable.Range(0, groups.arraySize).Select(n => groups.GetArrayElementAtIndex(n).FindPropertyRelative("fromLevel").intValue).Where(n => n > level).DefaultIfEmpty(10).Min() - 1;
            var fixedLevels = Enumerable.Range(0, transmissions.arraySize).Select(n => transmissions.GetArrayElementAtIndex(n).FindPropertyRelative("level").intValue).ToArray();
            while (end >= level && fixedLevels.Contains(end)) end--;
            var messages = group.FindPropertyRelative("messages");
            for (int m = 0; m < messages.arraySize; m++)
                Set(project, messages.GetArrayElementAtIndex(m).stringValue, path, messages.GetArrayElementAtIndex(m).propertyPath,
                    new StoryEventInfo { kind = StoryEventKind.LevelStart, eventId = "level-start", pool = "earth-level-" + level, fromLevel = level, toLevel = Mathf.Min(9, end), random = true,
                        blockedFlag = "discovery.Moon", presentation = "Kurzer Hinweis",
                        description = "Zufallskommentar beim Trainingsstart; höchste passende Levelgruppe. Übertragungen in Level 3/6/9 haben Vorrang, bei bekanntem Mond wird dieser Kommentar ersetzt." });
        }
        for (int i = 0; i < transmissions.arraySize; i++)
        {
            var item = transmissions.GetArrayElementAtIndex(i); int level = item.FindPropertyRelative("level").intValue;
            Set(project, item.FindPropertyRelative("text").stringValue, path, item.propertyPath,
                new StoryEventInfo { kind = StoryEventKind.LevelTransmission, eventId = "training-level-" + level, pool = "transmission-" + level, fromLevel = level, toLevel = level, once = true,
                    blockedFlag = "story.earth." + level, presentation = "Bestätigbare Übertragung / Funk",
                    description = "Beim Erreichen dieses Trainingslevels, nicht erst nach dessen Abschluss; nur solange Schwerelosigkeit noch nicht entdeckt wurde. Nach Bestätigung wird story.earth." + level + " gespeichert." });
        }
        var discoveries = so.FindProperty("discoveryMessages");
        for (int i = 0; i < discoveries.arraySize; i++)
        {
            var item = discoveries.GetArrayElementAtIndex(i); string discovery = item.FindPropertyRelative("discovery").enumNames[item.FindPropertyRelative("discovery").enumValueIndex];
            var presentation = item.FindPropertyRelative("presentation");
            Set(project, item.FindPropertyRelative("text").stringValue, path, item.propertyPath,
                new StoryEventInfo { kind = StoryEventKind.Discovery, eventId = discovery, pool = "discovery-" + discovery, once = true, blockedFlag = "story.ack." + discovery,
                    presentation = presentation.enumNames[presentation.enumValueIndex],
                    description = "Erste Entdeckung: " + discovery + ". Unbestätigte Meldungen können nach einer Unterbrechung erneut erscheinen; tatsächliche Nähe, Flugphase und Anzeigetiming prüft das Spiel." });
        }
        var asides = so.FindProperty("earthAsides");
        for (int i = 0; i < asides.arraySize; i++)
            Set(project, asides.GetArrayElementAtIndex(i).stringValue, path, asides.GetArrayElementAtIndex(i).propertyPath,
                new StoryEventInfo { kind = StoryEventKind.LateTraining, eventId = "late-training", pool = "earth-asides", fromLevel = 10, toLevel = 0, random = true,
                    probability = so.FindProperty("earthAsideChance").floatValue, presentation = "Kurzer Hinweis",
                    description = "Ab Level 10 maximal ein Zufallsversuch je neuem Level; keine direkte Wiederholung des letzten Kommentars." });
        var states = typeof(StoryTextController).GetField("stateMessages", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(source)
            as Dictionary<StoryTextController.eStoryTextType, string[]>;
        if (states != null) foreach (var state in states)
            foreach (string reference in state.Value)
                Set(project, reference, "Assets/Scripts/StoryTextController.cs", "stateMessages." + state.Key,
                    new StoryEventInfo { kind = StoryEventKind.FlightState, eventId = state.Key.ToString(), pool = "flight-" + state.Key, random = true, fromLevel = state.Key == StoryTextController.eStoryTextType.BackToPlanet ? 3 : 1, toLevel = 0,
                        probability = so.FindProperty("flightCommentChance").floatValue, presentation = "Kurzer Hinweis",
                        blockedFlag = "story.earth.9",
                        requiredFlag = state.Key == StoryTextController.eStoryTextType.NearToMoon ? "story.ack.Moon" : state.Key == StoryTextController.eStoryTextType.AtmosphereExit ? "story.ack.ZeroG" : "",
                        description = "Flugzustand " + state.Key + "; ein Kommentar pro Flugversuch, mit Startverzögerung und Cooldown. Nach der ersten Entdeckung, nicht als Dialogfolge." });
        Set(project, "[[game.back.on.earth.you.can.refuel.on.a.free.landing.pad]]", "Assets/Scripts/StoryTextController.cs", "Show(BackToPlanet)",
            new StoryEventInfo { kind = StoryEventKind.FlightState, eventId = "BackToPlanet", pool = "early-earth-return", fromLevel = 1, toLevel = 2, probability = so.FindProperty("flightCommentChance").floatValue,
                blockedFlag = "story.earth.9", presentation = "Kurzer Hinweis", description = "Rückkehr zur Erde in Level 1/2: ersetzt den Zufallstext durch den Auftankhinweis, falls die Kommentar-Chance greift." });
        Special(project, so, path, "moonKnownEarthMessage", StoryEventKind.KnownMoon, "known-moon", "discovery.Moon", "", "Ersetzt normale Trainingsstart-Kommentare, wenn der Mond bereits bekannt ist; Levelübertragungen und Level 10+ haben Vorrang.");
        Special(project, so, path, "earthReturnMessage", StoryEventKind.EarthReturn, "earth-return", "story.companyLanderReturned", "story.ack.CompanyReturn", "Nach Rückkehr des gefundenen Firmenschiffs zur Erde; Anzeige bei den Landeergebnissen beziehungsweise als bestätigbares Gespräch.");
        Special(project, so, path, "zeroGAfterDepartureMessage", StoryEventKind.ZeroGAfterTraining, "ZeroG", "story.earth.9", "story.ack.ZeroG", "Alternative erste Schwerelosigkeitsmeldung nach bestätigter Level-9-Übertragung.");
    }
    public static void ArrangeTraining()
    {
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath);
        if (!project) return;
        var nodes = project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly && n.chapter == project.ResolveChapter("Training")).ToList();
        var fixedLevels = nodes.Where(n => n.eventInfo.enabled && n.eventInfo.kind == StoryEventKind.LevelTransmission).Select(n => n.eventInfo.fromLevel).ToArray();
        foreach (var node in nodes.Where(n => n.eventInfo.enabled && n.eventInfo.kind == StoryEventKind.LevelStart))
            while (node.eventInfo.toLevel >= node.eventInfo.fromLevel && fixedLevels.Contains(node.eventInfo.toLevel)) node.eventInfo.toLevel--;
        Undo.RegisterCompleteObjectUndo(project, "Trainingsmeldungen anordnen");
        float y = 0;
        var levelNodes = nodes.Where(n => n.eventInfo.enabled && (n.eventInfo.kind == StoryEventKind.LevelStart || n.eventInfo.kind == StoryEventKind.LevelTransmission || n.eventInfo.kind == StoryEventKind.LateTraining)).ToList();
        foreach (var group in levelNodes.GroupBy(n => n.eventInfo.pool).OrderBy(g => g.First().eventInfo.fromLevel).ThenBy(g => g.First().eventInfo.kind))
        {
            int i = 0;
            foreach (var node in group.OrderBy(n => n.sourceProperty, System.StringComparer.Ordinal))
            {
                node.position = new Vector2(330 + i % 3 * 700, y + i / 3 * 160);
                node.eventPosition = node.position + Vector2.left * 310; node.eventPositioned = true; i++;
            }
            y += Mathf.Ceil(i / 3f) * 160 + 100;
        }
        y += 240;
        foreach (var group in nodes.Except(levelNodes).GroupBy(n => n.eventInfo.enabled ? n.eventInfo.pool : "other-texts")
            .OrderBy(g => g.First().eventInfo.enabled ? (int)g.First().eventInfo.kind : 100).ThenBy(g => g.Key))
        {
            int i = 0;
            foreach (var node in group.OrderBy(n => n.sourceProperty, System.StringComparer.Ordinal))
            {
                node.position = new Vector2(330 + i % 3 * 700, y + i / 3 * 160);
                if (node.eventInfo.enabled) { node.eventPosition = node.position + Vector2.left * 310; node.eventPositioned = true; }
                i++;
            }
            y += Mathf.Ceil(i / 3f) * 160 + 100;
        }
        EditorUtility.SetDirty(project); AssetDatabase.SaveAssets();
        Debug.Log("Training arranged by level; fixed messages take priority. Independent events separated below. No gameplay triggers or links changed.");
    }
    static void Special(StoryProject project, SerializedObject so, string path, string field, StoryEventKind kind, string id, string required, string blocked, string description)
    {
        Set(project, so.FindProperty(field).stringValue, path, field, new StoryEventInfo { kind = kind, eventId = id, pool = field, requiredFlag = required, blockedFlag = blocked, once = kind != StoryEventKind.KnownMoon, description = description, presentation = "Spielabhängige Meldung" });
    }
    static void Set(StoryProject project, string reference, string path, string property, StoryEventInfo info)
    {
        string key = StoryCatalog.Key(reference);
        if (reference == "...")
        {
            key = "story.event.training.silence";
            foreach (string language in new[] { "de", "en" }) if (string.IsNullOrEmpty(StoryCatalog.Text(key, language))) StoryCatalog.Write(key, language, "...");
        }
        var node = project.Find(key);
        if (node == null)
        {
            node = new StoryNode { id = key, textKey = key, title = reference, chapter = project.ResolveChapter("Training"),
                sourceReference = reference, sourcePath = path, sourceProperty = property, storyStepId = "overview.0", status = StoryDevelopmentStatus.Implemented, answerBoardLayout = true };
            node.position = new Vector2(0, project.nodes.Where(n => n.chapter == node.chapter).Select(n => n.position.y).DefaultIfEmpty(0).Max() + 300);
            node.speaker = project.nodes.FirstOrDefault(n => n.speaker && n.speaker.id == "training-pilot")?.speaker;
            project.nodes.Add(node);
        }
        info.enabled = true; if (!node.eventBindingEdited) node.eventInfo = info;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StoryImport
{
    public const string ProjectPath = "Assets/Resources/Story/StoryProject.asset";
    [MenuItem("Tools/Story/Story-Editor")]
    public static void Open() => StoryEditorWindow.Open();
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources/Story/Characters"); AssetDatabase.Refresh();
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(ProjectPath);
        bool firstImport = !project;
        if (!project) { project = ScriptableObject.CreateInstance<StoryProject>(); AssetDatabase.CreateAsset(project, ProjectPath); }
        Undo.RecordObject(project, "Story übernehmen");
        if (project.chapters.Count == 0) project.chapters.AddRange(new[] { "Training und Entdeckungen", "Registrierung", "Rhekk", "Funk", "Stationsbewohner", "Spätere Geschichte" });
        var pilot = Character("training-pilot", "story.character.training-pilot", null);
        var registration = Character("registration", "game.registration", "Assets/Prefabs/Characters/Station/StationOperatorVisual.prefab");
        var rhekk = Character("rhekk", "cargo.passenger.name", "Assets/Prefabs/Characters/Rhekk.prefab");
        var unknown = Character("unknown-sender", "game.unknown.sender", null);
        if (string.IsNullOrEmpty(StoryCatalog.Text(pilot.nameKey, "de"))) StoryCatalog.Write(pilot.nameKey, "de", "Trainingspilot");
        if (string.IsNullOrEmpty(StoryCatalog.Text(pilot.nameKey, "en"))) StoryCatalog.Write(pilot.nameKey, "en", "Training pilot");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Station01/StationInterior.prefab");
        var station = prefab.GetComponentInChildren<StationConversation>(true);
        if (station)
        {
            var so = new SerializedObject(station);
            Pages(project, so.FindProperty("steps"), "registration", "Registrierung", registration, "Assets/Prefabs/Station01/StationInterior.prefab", true);
            var repeat = Single(project, so.FindProperty("repeatMessage").stringValue, "Registrierung", registration, StoryChannel.Personal, "Assets/Prefabs/Station01/StationInterior.prefab", "repeatMessage");
            repeat.legacyAnswerCount = 1;
            if (repeat.choices.Count == 0) repeat.choices.Add(new StoryChoice { textKey = StoryCatalog.Key(so.FindProperty("goodbyeAnswer").stringValue), continueLegacy = true });
        }
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        const string main = "Assets/Scenes/MainScene.unity";
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(main);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(main, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var cargo in root.GetComponentsInChildren<CargoMission>(true))
                {
                    var so = new SerializedObject(cargo);
                    Pages(project, so.FindProperty("discoveryConversation"), "cargo.discovery", "Rhekk", rhekk, main, false);
                    Pages(project, so.FindProperty("releaseConversation"), "cargo.release", "Rhekk", rhekk, main, false);
                }
                foreach (var resident in root.GetComponentsInChildren<StationResident>(true))
                {
                    var so = new SerializedObject(resident); var comments = so.FindProperty("comments");
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(resident.gameObject);
                    var character = Character("resident-" + so.FindProperty("residentId").intValue,
                        "story.character.resident-" + so.FindProperty("residentId").intValue, source ? AssetDatabase.GetAssetPath(source) : null);
                    foreach (var lang in new[] { "de", "en" }) if (string.IsNullOrEmpty(StoryCatalog.Text(character.nameKey, lang))) StoryCatalog.Write(character.nameKey, lang, resident.name);
                    for (int i = 0; i < comments.arraySize; i++) Single(project, comments.GetArrayElementAtIndex(i).stringValue, "Stationsbewohner", character, StoryChannel.Personal, main, "comments.Array.data[" + i + "]");
                }
                foreach (var radio in root.GetComponentsInChildren<RadioController>(true))
                {
                    var so = new SerializedObject(radio); var message = so.FindProperty("pickupMessage");
                    if (message != null)
                    {
                        var node = Single(project, message.FindPropertyRelative("body").stringValue, "Funk", registration, StoryChannel.RadioPortrait, main, "pickupMessage");
                        Replies(node, message.FindPropertyRelative("replies"));
                    }
                }
                foreach (var text in root.GetComponentsInChildren<StoryTextController>(true))
                {
                    var so = new SerializedObject(text); var property = so.GetIterator();
                    while (property.Next(true)) if (property.propertyType == SerializedPropertyType.String && property.stringValue.Contains("[["))
                    {
                        var speaker = property.propertyPath.StartsWith("discoveryMessages.Array.data[5]") ? unknown
                            : property.propertyPath.StartsWith("discoveryMessages.Array.data[6]") || property.propertyPath.StartsWith("discoveryMessages.Array.data[7]") ? registration : pilot;
                        var node = Single(project, property.stringValue, "Training und Entdeckungen", speaker, StoryChannel.Radio, main, property.propertyPath);
                        if (speaker != pilot && node.speaker == pilot) node.speaker = speaker;
                    }
                }
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (originalScene.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene); }
        // Includes code-authored transmissions and repeat text, without copying localized content.
        foreach (string file in new[] { "StoryTextController", "CargoMission", "RadioController", "StationConversation" })
            foreach (Match match in Regex.Matches(File.ReadAllText("Assets/Scripts/" + file + ".cs"), @"\[\[[a-zA-Z0-9_.-]+\]\]"))
            {
                string reference = match.Value;
                if (project.nodes.Any(n => n.sourceReference == reference)) continue;
                if (reference.StartsWith("[[cargo.radio.") && reference.EndsWith(".body]]")) Single(project, reference, "Funk", registration, StoryChannel.Radio, "Assets/Scripts/" + file + ".cs", "");
                else if (file == "StoryTextController") Single(project, reference, "Training und Entdeckungen", pilot, StoryChannel.Radio, "Assets/Scripts/" + file + ".cs", "");
                else if (reference == "[[cargo.dialog.repeat]]")
                {
                    var repeat = Single(project, reference, "Rhekk", rhekk, StoryChannel.Personal, "Assets/Scripts/CargoMission.cs", ""); repeat.legacyAnswerCount = 1;
                    if (repeat.choices.Count == 0) repeat.choices.Add(new StoryChoice { textKey = "cargo.answer.back", continueLegacy = true });
                }
            }
        AddFuture(project, "pilot-trail", "Pilotenspur", "Nach Rhekks Rettung: noch gemeinsam zu planen.");
        AddFuture(project, "spacecraft-reveal", "Erstes richtiges Raumschiff", "Mobile dreht auf Landscape; PC öffnet den Hochkantbereich auf den ganzen Bildschirm. Neue Flug- und Werkzeugsteuerung.");
        AddFuture(project, "home", "Eigenes Zuhause", "Kleine eigene Station beziehungsweise Außenposten; Storyweg noch offen.");
        Milestones(project);
        SeparateExisting(project);
        EditorUtility.SetDirty(project); AssetDatabase.SaveAssets();
        if (firstImport)
        {
            AuthorPortrait("Assets/Prefabs/UI/StoryDialog.prefab", "storyPortrait");
            AuthorPortrait("Assets/Prefabs/UI/Radio.prefab", "storyPortrait");
            AuthorAnswers("Assets/Prefabs/UI/StoryDialog.prefab", "answerButtons", -300, 100, 900, 85);
            AuthorAnswers("Assets/Prefabs/UI/Radio.prefab", "replyButtons", -180, 80, 760, 70);
        }
        Debug.Log("Story import: " + project.nodes.Count + " nodes. Existing text keys and mission logic retained. No Play Mode tests.");
    }
    static StoryCharacter Character(string id, string key, string prefabPath)
    {
        string path = "Assets/Resources/Story/Characters/" + id + ".asset";
        var character = AssetDatabase.LoadAssetAtPath<StoryCharacter>(path);
        if (character) return character;
        character = ScriptableObject.CreateInstance<StoryCharacter>(); character.id = id; character.nameKey = key;
        character.prefab = string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        AssetDatabase.CreateAsset(character, path); return character;
    }
    static StoryNode Single(StoryProject project, string reference, string chapter, StoryCharacter speaker, StoryChannel channel, string source, string property)
    {
        chapter = project.ResolveChapter(chapter);
        if (!project.dialogueGroups.Contains(chapter)) project.dialogueGroups.Add(chapter);
        var key = StoryCatalog.Key(reference);
        var existing = project.nodes.Find(n => n.id == key); if (existing != null) return existing;
        int index = project.nodes.Count(n => n.chapter == chapter);
        string title = StoryCatalog.Text(key, "de").Split('\n')[0]; if (title.Length > 65) title = title.Substring(0, 62) + "…";
        var node = new StoryNode { id = key, title = string.IsNullOrEmpty(title) ? key : title, textKey = key, sourceReference = reference, chapter = chapter,
            speaker = speaker, channel = channel, sourcePath = source, sourceProperty = property,
            status = StoryDevelopmentStatus.Implemented, position = new Vector2((index % 4) * 280, (index / 4) * 130) };
        project.nodes.Add(node); return node;
    }
    static void Pages(StoryProject project, SerializedProperty pages, string prefix, string chapter, StoryCharacter speaker, string source, bool reactions)
    {
        for (int i = 0; i < pages.arraySize; i++)
        {
            var page = pages.GetArrayElementAtIndex(i);
            var node = Single(project, page.FindPropertyRelative("message").stringValue, chapter, speaker, StoryChannel.Personal, source, page.propertyPath);
            node.legacyAnswerCount = page.FindPropertyRelative("answers").arraySize;
            if (node.choices.Count != 0) continue;
            var answers = page.FindPropertyRelative("answers");
            for (int a = 0; a < answers.arraySize; a++)
            {
                var answer = answers.GetArrayElementAtIndex(a);
                node.choices.Add(new StoryChoice { textKey = StoryCatalog.Key(reactions ? answer.FindPropertyRelative("text").stringValue : answer.stringValue),
                    reactionKey = reactions ? StoryCatalog.Key(answer.FindPropertyRelative("reaction").stringValue) : "",
                    next = i + 1 < pages.arraySize ? StoryCatalog.Key(pages.GetArrayElementAtIndex(i + 1).FindPropertyRelative("message").stringValue) : "",
                    continueLegacy = true, legacyAnswer = a });
            }
            node.purpose = reactions && page.FindPropertyRelative("givesRadio").boolValue ? "Bestehender Ablauf: Funkgerät ausgeben, unabhängig von der Antwort." : "Bestehender Missionsablauf bleibt beim Abschluss dieses Schritts zuständig.";
        }
    }
    static void Replies(StoryNode node, SerializedProperty replies)
    {
        node.legacyAnswerCount = replies.arraySize;
        if (node.choices.Count > 0) return;
        for (int i = 0; i < replies.arraySize; i++)
        {
            var reply = replies.GetArrayElementAtIndex(i);
            node.choices.Add(new StoryChoice { textKey = StoryCatalog.Key(reply.FindPropertyRelative("text").stringValue), reactionKey = StoryCatalog.Key(reply.FindPropertyRelative("reaction").stringValue), continueLegacy = true, legacyAnswer = i });
        }
    }
    static void AddFuture(StoryProject project, string id, string title, string purpose)
    {
        string mainId = id switch { "pilot-trail" => "overview.11", "spacecraft-reveal" => "overview.12", "home" => "overview.13", _ => null };
        if (mainId != null && project.Find(mainId) != null) return;
        if (project.Find(id) != null) return;
        string chapter = project.ResolveChapter("Spätere Geschichte");
        if (!project.chapters.Contains(chapter)) project.chapters.Add(chapter);
        project.nodes.Add(new StoryNode { id = id, chapter = chapter, title = title, purpose = purpose, overviewOnly = true, position = new Vector2(project.nodes.Count(n => n.chapter == chapter) * 280, 0) });
    }
    static void Milestones(StoryProject project)
    {
        string chapter = project.ResolveChapter("Ablauf");
        if (!project.chapters.Contains(chapter)) project.chapters.Insert(0, chapter);
        string[] titles = { "Landing-Pad-Training", "Ins All aufbrechen", "Mond entdecken und landen", "Aussteigen", "Verlassenes Schiff finden", "Station erreichen", "Registrierung und Funk", "Rhekk am Außenposten finden", "Kapsel verbinden und abschleppen", "Sanft absetzen und Rhekk befreien", "Rhekk geht zur Registrierung", "Pilotenspur", "Neues Raumschiff / Bildschirm-Enthüllung", "Eigenes Zuhause" };
        for (int i = 0; i < titles.Length; i++)
        {
            string id = "overview." + i;
            if (project.Find(id) != null) continue;
            var node = new StoryNode { id = id, chapter = chapter, title = titles[i], overviewOnly = true,
                status = i < 11 ? StoryDevelopmentStatus.Implemented : StoryDevelopmentStatus.Planned,
                sourcePath = i < 11 ? "Assets/Scenes/MainScene.unity" : "GAME_CONCEPT.md",
                purpose = i < 11 ? "Bestehender Spielablauf. Zugehörige Gespräche stehen in den jeweiligen Kapiteln. Gameplay-Abnahme separat." : "Grobe Richtung vereinbart; Storydetails noch offen.",
                position = new Vector2((i % 4) * 280, (i / 4) * 150) };
            if (i + 1 < titles.Length) node.choices.Add(new StoryChoice { next = "overview." + (i + 1) });
            project.nodes.Add(node);
        }
    }
    public static void SeparateExisting(StoryProject project)
    {
        bool initial = project.dialogueGroups.Count == 0;
        project.SeparateSections();
        foreach (var mapping in new[] { ("pilot-trail", "overview.11"), ("spacecraft-reveal", "overview.12"), ("home", "overview.13") })
        {
            var old = project.Find(mapping.Item1); var target = project.Find(mapping.Item2);
            if (old == null || target == null) continue;
            if (!string.IsNullOrEmpty(old.purpose) && !(target.purpose ?? "").Contains(old.purpose)) target.purpose += "\n\n" + old.purpose;
            if (!string.IsNullOrEmpty(old.questions)) target.questions += "\n" + old.questions;
            foreach (var n in project.nodes)
            {
                if (n.storyStepId == old.id) n.storyStepId = target.id;
                foreach (var choice in n.choices) if (choice.next == old.id) choice.next = target.id;
            }
            project.nodes.Remove(old);
        }
        string future = project.ResolveChapter("Spätere Geschichte");
        string mainChapter = project.Find("overview.0")?.chapter ?? project.chapters.FirstOrDefault();
        if (!string.IsNullOrEmpty(mainChapter) && mainChapter != future)
        {
            foreach (var n in project.nodes.Where(n => n.overviewOnly && n.chapter == future)) n.chapter = mainChapter;
            if (!project.nodes.Any(n => n.overviewOnly && n.chapter == future)) project.chapters.Remove(future);
        }
        foreach (var n in project.nodes.Where(n => !n.overviewOnly && !n.answerBoardLayout))
        { n.position = new Vector2(n.position.x * 2.6f, n.position.y * 2.25f); n.answerBoardLayout = true; }
        foreach (var node in project.nodes.Where(n => !n.overviewOnly && (initial || string.IsNullOrEmpty(n.storyStepId))))
        {
            string key = node.textKey ?? "";
            if (key.StartsWith("cargo.dialog.release")) node.storyStepId = "overview.9";
            else if (key.StartsWith("cargo.dialog.discovery.4") || key.StartsWith("cargo.dialog.discovery.5") || key.StartsWith("cargo.radio.tow") || key.StartsWith("cargo.radio.failed")) node.storyStepId = "overview.8";
            else if (key.StartsWith("cargo.radio.delivered")) node.storyStepId = "overview.9";
            else if (key.StartsWith("cargo.radio.complete")) node.storyStepId = "overview.10";
            else if (key.StartsWith("cargo.") || node.sourceProperty == "pickupMessage") node.storyStepId = "overview.7";
            else if (node.chapter == project.ResolveChapter("Registrierung")) node.storyStepId = "overview.6";
            else if (node.chapter == project.ResolveChapter("Stationsbewohner")) node.storyStepId = "overview.5";
            else if (node.chapter == project.ResolveChapter("Training und Entdeckungen"))
            {
                var match = Regex.Match(node.sourceProperty ?? "", @"discoveryMessages.Array.data\[(\d+)\]");
                int index = match.Success ? int.Parse(match.Groups[1].Value) : -1;
                int step = index switch { 0 => 1, 1 or 2 => 2, 3 => 3, 4 => 4, 5 or 6 or 7 => 5, _ => 0 };
                node.storyStepId = "overview." + step;
            }
        }
        EditorUtility.SetDirty(project);
    }
    public static void MigrateSections()
    {
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(ProjectPath);
        if (project) { SeparateExisting(project); AssetDatabase.SaveAssets(); }
    }
    static void AuthorPortrait(string path, string field)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var component = root.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c => c && new SerializedObject(c).FindProperty(field) != null);
            if (!component) return;
            var so = new SerializedObject(component);
            var previous = so.FindProperty(field).objectReferenceValue as UnityEngine.UI.Image;
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var image = new GameObject("Story character portrait", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            var device = so.FindProperty("device")?.objectReferenceValue as GameObject;
            image.transform.SetParent(device ? device.transform : component.transform, false); image.raycastTarget = false; image.preserveAspect = true;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition = device ? new Vector2(-290, 475) : new Vector2(-370, 650);
            image.rectTransform.sizeDelta = new Vector2(110, 110);
            if (device && so.FindProperty("voiceOnly").objectReferenceValue is GameObject voiceOnly)
            {
                var slot = (RectTransform)voiceOnly.transform;
                image.transform.SetParent(slot.parent, false); image.rectTransform.anchorMin = slot.anchorMin; image.rectTransform.anchorMax = slot.anchorMax;
                image.rectTransform.pivot = slot.pivot; image.rectTransform.anchoredPosition = slot.anchoredPosition; image.rectTransform.sizeDelta = slot.sizeDelta;
            }
            image.gameObject.SetActive(false); so.FindProperty(field).objectReferenceValue = image; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void AuthorAnswers(string path, string field, float y, float spacing, float width, float height)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var component = root.GetComponentsInChildren<MonoBehaviour>(true).First(c => c && new SerializedObject(c).FindProperty(field) != null);
            var so = new SerializedObject(component); var buttons = so.FindProperty(field);
            var first = (UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(0).objectReferenceValue;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(first.gameObject);
            int previousCount = buttons.arraySize; buttons.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var button = i < previousCount ? (UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue
                    : ((GameObject)PrefabUtility.InstantiatePrefab(source, first.transform.parent)).GetComponent<UnityEngine.UI.Button>();
                button.name = "Story answer " + (i + 1);
                var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(0, y - i * spacing); rect.sizeDelta = new Vector2(width, height);
                if (field == "replyButtons")
                { rect.anchoredPosition = new Vector2(i % 2 == 0 ? -195 : 195, y - (i / 2) * spacing); rect.sizeDelta = new Vector2(370, height); }
                button.gameObject.SetActive(false); buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
                var label = button.GetComponentInChildren<TMPro.TMP_Text>(true); label.enableAutoSizing = true; label.fontSizeMin = 28; label.fontSizeMax = 34;
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect); PrefabUtility.RecordPrefabInstancePropertyModifications(button.gameObject);
            }
            if (field == "answerButtons")
            {
                var text = ((TMPro.TMP_Text)so.FindProperty("conversationText").objectReferenceValue).rectTransform;
                text.anchoredPosition = new Vector2(0, 170); text.sizeDelta = new Vector2(900, 650);
                var speaker = ((TMPro.TMP_Text)so.FindProperty("speakerText").objectReferenceValue).rectTransform;
                speaker.anchoredPosition = new Vector2(100, 650); speaker.sizeDelta = new Vector2(650, 85);
            }
            else
            {
                var track = ((UnityEngine.UI.Button)so.FindProperty("trackButton").objectReferenceValue).GetComponent<RectTransform>();
                track.anchoredPosition = new Vector2(0, -450); PrefabUtility.RecordPrefabInstancePropertyModifications(track);
                var signal = ((TMPro.TMP_Text)so.FindProperty("signalText").objectReferenceValue).rectTransform;
                signal.anchoredPosition = new Vector2(0, -395); signal.sizeDelta = new Vector2(760, 35);
            }
            so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}

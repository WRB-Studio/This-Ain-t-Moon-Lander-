using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class CommunicationPanelGalleryAuthoring
{
    const string ScenePath = "Assets/Scenes/CommunicationPanelGallery.unity";
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
    static readonly List<CommunicationPanelGallery.PanelCase> cases = new();
    static List<CommunicationPanelGallery.SampleText> texts;
    static RectTransform preview;
    static Camera camera;

    public static void Build()
    {
        cases.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 10;
        camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        BuildWorld();
        var root = Rect(null, "Communication panel gallery", Vector2.zero, Vector2.zero).gameObject;
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();
        var gallery = root.AddComponent<CommunicationPanelGallery>();
        preview = Rect(root.transform, "Panel variants", Vector2.zero, Vector2.zero);
        preview.anchorMin = Vector2.zero; preview.anchorMax = Vector2.one; preview.offsetMin = Vector2.zero; preview.offsetMax = Vector2.zero;
        BuildTraining(); BuildDialog(true); BuildRadio(true); BuildRadio(false);
        BuildAside(); BuildBubble(); BuildResult();
        var serialized = new SerializedObject(gallery);
        var entries = serialized.FindProperty("cases"); entries.arraySize = cases.Count;
        for (int i = 0; i < cases.Count; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i); var sample = cases[i];
            entry.FindPropertyRelative("title").stringValue = sample.title; entry.FindPropertyRelative("purpose").stringValue = sample.purpose;
            entry.FindPropertyRelative("sourcePrefab").stringValue = sample.sourcePrefab; entry.FindPropertyRelative("comparison").stringValue = sample.comparison;
            entry.FindPropertyRelative("root").objectReferenceValue = sample.root;
            var labels = entry.FindPropertyRelative("texts"); labels.arraySize = sample.texts.Length;
            for (int j = 0; j < sample.texts.Length; j++)
            {
                var label = labels.GetArrayElementAtIndex(j); label.FindPropertyRelative("label").objectReferenceValue = sample.texts[j].label;
                label.FindPropertyRelative("german").stringValue = sample.texts[j].german; label.FindPropertyRelative("english").stringValue = sample.texts[j].english;
            }
            var buttons = entry.FindPropertyRelative("demoButtons"); buttons.arraySize = sample.demoButtons.Length;
            for (int j = 0; j < sample.demoButtons.Length; j++) buttons.GetArrayElementAtIndex(j).objectReferenceValue = sample.demoButtons[j];
            sample.root.SetActive(i == 0);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo(); ArrangeSideBySide(gallery); gallery.ShowCase(0);
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        Debug.Log("Communication panel gallery authored: " + cases.Count + " cases, existing prefab instances, no gameplay/save managers.");
    }
    public static void ArrangeExistingSideBySide()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        ArrangeSideBySide(Object.FindFirstObjectByType<CommunicationPanelGallery>());
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Communication variants arranged in four groups with shared-basis headings.");
    }
    static void ArrangeSideBySide(CommunicationPanelGallery gallery)
    {
        var data = new SerializedObject(gallery);
        var entries = data.FindProperty("cases");
        var parent = gallery.transform.Find("Panel variants");
        var viewport = Rect(parent, "Grouped comparison", Vector2.zero, Vector2.zero);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
        viewport.gameObject.AddComponent<RectMask2D>().enabled = false;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 80; scroll.viewport = viewport;
        var content = Rect(viewport, "Four panel groups", new Vector2(4860, 5200), Vector2.zero);
        content.anchorMin = content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1);
        scroll.content = content;
        string[] groups = { "Gespräch\ngemeinsames DialogueContent-Prefab", "Funkgerät", "Kurztext", "Ergebnis" };
        var columns = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            columns[i] = Rect(content, (i + 1) + " " + groups[i], new Vector2(1080, 5200), new Vector2(i * 1260, 0));
            columns[i].anchorMin = columns[i].anchorMax = new Vector2(0, 1); columns[i].pivot = new Vector2(0, 1);
            var heading = Text(columns[i], "Group and shared basis", new Vector2(1000, 110), new Vector2(540, -65), 34);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(0, 1);
            heading.text = groups[i];
        }
        for (int i = 0; i < entries.arraySize; i++)
        {
            int group = i < 2 ? 0 : i < 4 ? 1 : i < 6 ? 2 : 3;
            int row = i - (group == 0 ? 0 : group == 1 ? 2 : group == 2 ? 4 : 6);
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("group").stringValue = groups[group];
            entry.FindPropertyRelative("focus").vector2Value = new Vector2(group / 3f, 1f - (160f + row * 2200) / 3280);
            var root = (GameObject)entry.FindPropertyRelative("root").objectReferenceValue;
            var card = Rect(columns[group], entry.FindPropertyRelative("title").stringValue, new Vector2(1080, 1920), new Vector2(540, -160 - row * 2200));
            card.anchorMin = card.anchorMax = new Vector2(0, 1); card.pivot = new Vector2(.5f, 1);
            var title = Text(card, "Variant title", new Vector2(1040, 44), new Vector2(0, 925), 25);
            title.text = "Variante " + (row + 1) + " · " + entry.FindPropertyRelative("title").stringValue;
            root.transform.SetParent(card, false);
            var rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchoredPosition = Vector2.zero;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = false; }
            if (root.GetComponent<Canvas>() || root.name.Contains("Story message") || root.name.Contains("Conversation") || root.name.Contains("Landing") || root.name.Contains("Game over"))
            { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; }
            else { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); }
            if (root.name.Contains("NPC bubble")) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(500, 130); }
            root.SetActive(true);
            AddFrameEdge(card, "Top frame", new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -2), new Vector2(0, 4));
            AddFrameEdge(card, "Bottom frame", new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 2), new Vector2(0, 4));
            AddFrameEdge(card, "Left frame", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), new Vector2(2, 0), new Vector2(4, 0));
            AddFrameEdge(card, "Right frame", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, .5f), new Vector2(-2, 0), new Vector2(4, 0));
            title.transform.SetAsLastSibling();
        }
        data.FindProperty("comparisonScroll").objectReferenceValue = scroll;
        data.ApplyModifiedPropertiesWithoutUndo();
        Canvas.ForceUpdateCanvases(); scroll.normalizedPosition = new Vector2(0, 1);
    }
    static void Assign(SerializedObject data, string property, Object value) => data.FindProperty(property).objectReferenceValue = value;
    static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static TMP_Text Text(Transform parent, string name, Vector2 size, Vector2 position, float font)
    {
        var label = Rect(parent, name, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Font; label.fontSize = font; label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return label;
    }
    static void AddFrameEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, size, position);
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(1, 1, 1, .55f); image.raycastTarget = false;
    }
    static Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ActionButton.prefab"), parent);
        root.name = name; var rect = root.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        foreach (var local in root.GetComponentsInChildren<LocalizedText>(true)) Object.DestroyImmediate(local);
        var label = root.GetComponentInChildren<TMP_Text>(); label.text = text; label.fontSize = 30;
        return root.GetComponent<Button>();
    }
    static GameObject Begin(string path, string name)
    {
        texts = new();
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), preview);
        root.name = name;
        foreach (var localized in root.GetComponentsInChildren<LocalizedText>(true))
        {
            string source = localized.Source;
            Sample(localized.GetComponent<TMP_Text>(), Translate(source, "de"), Translate(source, "en"));
            Object.DestroyImmediate(localized);
        }
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) { canvas.overrideSorting = false; canvas.renderMode = RenderMode.ScreenSpaceOverlay; }
        foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true)) Object.DestroyImmediate(scaler);
        var rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one;
        if (root.GetComponent<Canvas>() || root.GetComponent<StoryDialog>() || root.GetComponent<ResultPanel>())
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
        else { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; }
        root.SetActive(true); return root;
    }
    static void End(GameObject root, string title, string purpose, string source, string comparison)
    {
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is TrainingBriefingPanel || component is StoryDialog || component is RadioController || component is SpaceFlightNavigation || component is DirectionMarkerSystem
                || component is StartPanel || component is ResultPanel || component is StoryLineAudio)
                Object.DestroyImmediate(component);
        foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
        cases.Add(new CommunicationPanelGallery.PanelCase { root = root, title = title, purpose = purpose, sourcePrefab = source,
            comparison = comparison, texts = texts.ToArray(), demoButtons = root.GetComponentsInChildren<Button>(true) });
    }
    static T Field<T>(SerializedObject data, string field) where T : Object => data.FindProperty(field).objectReferenceValue as T;
    static void Sample(TMP_Text label, string de, string en)
    {
        if (!label) return; label.text = de;
        var existing = texts.Find(t => t.label == label);
        if (existing != null) { existing.german = de; existing.english = en; }
        else texts.Add(new CommunicationPanelGallery.SampleText { label = label, german = de, english = en });
    }
    static string Translate(string source, string language)
    {
        var catalog = JsonUtility.FromJson<Localization.Language>(File.ReadAllText("Assets/Resources/Localization/" + language + ".json"));
        return Regex.Replace(source ?? "", @"\[\[([a-zA-Z0-9_.-]+)\]\]", match => catalog.entries.FirstOrDefault(e => e.key == match.Groups[1].Value)?.text ?? match.Value);
    }
    static void ButtonSample(Button button, string de, string en) => Sample(button.GetComponentInChildren<TMP_Text>(true), de, en);
    static void BuildTraining()
    {
        const string path = "Assets/Prefabs/UI/TrainingBriefing.prefab"; var root = Begin(path, "01 Tutorial");
        var panel = root.GetComponentInChildren<TrainingBriefingPanel>(true); panel.gameObject.SetActive(true); var data = new SerializedObject(panel);
        Sample(Field<TMP_Text>(data, "speakerText"), "Flugtrainer · Testpilot a. D.", "Flight instructor · former test pilot");
        Sample(Field<TMP_Text>(data, "messageText"), "Ziel: sicher auf dem Pad landen.\n\nGedrückt halten: Schub.\nZur Seite ziehen: drehen.", "Goal: land safely on the pad.\n\nHold: thrust.\nDrag sideways: turn.");
        ButtonSample(Field<Button>(data, "continueButton"), "Weiter", "Continue");
        End(root, "Weiter-Ansicht: TrainingBriefing + StoryDialog", "Tutorial und kurze Storymeldungen mit Bestätigung.", path + " + UI/StoryDialog.prefab", "Gemeinsames DialogueContent-Prefab; TrainingBriefing und StoryDialog steuern den Ablauf.");
    }
    static void BuildDialog(bool choices)
    {
        const string path = "Assets/Prefabs/UI/StoryDialog.prefab"; var root = Begin(path, "02 Conversation");
        var data = new SerializedObject(root.GetComponent<StoryDialog>());
        Field<GameObject>(data, "conversationContent").SetActive(choices); Field<TMP_Text>(data, "messageText").gameObject.SetActive(!choices);
        Field<Button>(data, "continueButton").gameObject.SetActive(!choices);
        var answers = data.FindProperty("answerButtons");
        string[] de = { "Was ist passiert?", "Wie bekomme ich dich hier weg?", "Ich komme gleich zurück." };
        string[] en = { "What happened?", "How do I get you out of here?", "I'll be right back." };
        for (int i = 0; i < answers.arraySize; i++) { var button = (Button)answers.GetArrayElementAtIndex(i).objectReferenceValue; button.gameObject.SetActive(choices && i < 3); if (i < 3) ButtonSample(button, de[i], en[i]); }
        Sample(Field<TMP_Text>(data, "speakerText"), choices ? "Rhekk" : "Flugtrainer", choices ? "Rhekk" : "Flight instructor");
        Sample(Field<TMP_Text>(data, "conversationText"), "Endlich. Ich war schon kurz davor, Inventar zu werden.", "Finally. I was about to become inventory.");
        Sample(Field<TMP_Text>(data, "messageText"), "Da ist er: der Mond.\nBrems rechtzeitig.", "There it is: the Moon.\nBrake in time.");
        ButtonSample(Field<Button>(data, "continueButton"), "Weiter", "Continue");
        var portrait = Field<Image>(data, "storyPortrait"); if (portrait) portrait.gameObject.SetActive(false);
        End(root, "Antwort-Ansicht: StoryDialog", "Registrierung und Rhekk: kurzer Text mit Entscheidungen.", path,
            "Dieselbe DialogueContent-Basis mit eigenem Antwortbereich; Porträt optional.");
    }
    static void BuildRadio(bool portrait)
    {
        const string path = "Assets/Prefabs/UI/Radio.prefab"; var root = Begin(path, portrait ? "04 Radio portrait" : "05 Radio voice only");
        var data = new SerializedObject(root.GetComponent<RadioController>());
        Field<GameObject>(data, "device").SetActive(true); Field<Button>(data, "openButton").gameObject.SetActive(false);
        Sample(Field<TMP_Text>(data, "senderText"), "Registrierung", "Registration"); Sample(Field<TMP_Text>(data, "titleText"), "Letzte bestätigte Abholung", "Last confirmed pickup");
        Sample(Field<TMP_Text>(data, "bodyText"), "Folge dem Tracker zum Außenposten.\nDie Fracht wartet draußen.", "Follow the tracker to the outpost.\nThe cargo is waiting outside.");
        Sample(Field<TMP_Text>(data, "pageText"), "1 / 1", "1 / 1"); Sample(Field<TMP_Text>(data, "signalNameText"), "Missionsziel", "Mission target"); Sample(Field<TMP_Text>(data, "signalText"), "Aktiv · 7,1 km", "Active · 7.1 km");
        ButtonSample(Field<Button>(data, "previousSignalButton"), "<", "<"); ButtonSample(Field<Button>(data, "nextSignalButton"), ">", ">");
        ButtonSample(Field<Button>(data, "trackButton"), "Ziel verfolgen", "Track target"); ButtonSample(Field<Button>(data, "closeButton"), "Schließen", "Close");
        var portraits = data.FindProperty("portraits");
        for (int i = 0; i < portraits.arraySize; i++) { var p = portraits.GetArrayElementAtIndex(i); p.FindPropertyRelative("visual").objectReferenceValue.AsGameObject().SetActive(portrait && p.FindPropertyRelative("expression").enumValueIndex == (int)StoryDialog.Expression.StationCrew); }
        Field<GameObject>(data, "voiceOnly").SetActive(!portrait); var picture = Field<Image>(data, "storyPortrait"); if (picture) picture.gameObject.SetActive(false);
        var replies = data.FindProperty("replyButtons"); for (int i = 0; i < replies.arraySize; i++) { var button = replies.GetArrayElementAtIndex(i).objectReferenceValue as Button; button.gameObject.SetActive(i == 0); if (i == 0) ButtonSample(button, "Verstanden.", "Understood."); }
        End(root, portrait ? "Funkgerät / Porträt" : "Funkgerät / nur Stimme", "Nachrichtenverlauf, Antworten und anwählbare Missionsziele.", path, "Eigenes Funkgerät; Antworten und Zielauswahl haben getrennte Bereiche.");
    }
    static void BuildAside()
    {
        const string path = "Assets/Prefabs/UI/SpaceFlightNavigation.prefab"; var root = Begin(path, "06 Peripheral text");
        var data = new SerializedObject(root.GetComponent<SpaceFlightNavigation>()); var text = Field<TMP_Text>(data, "asideText"); text.gameObject.SetActive(true);
        Sample(text, "Hier war wirklich schon jemand.\nHoffentlich mit Rückflugticket.", "Someone really was here before.\nHopefully with a return ticket.");
        End(root, "Kurzer Nebentext", "Kommentare bei etwa einem Drittel von oben; unterbrechen das Spiel nicht.", path, "Gemeinsame ShortCaption-Gestaltung; bildschirmfester Nebentext.");
    }
    static void BuildBubble()
    {
        const string path = "Assets/Prefabs/UI/StationWorldText.prefab"; var root = Begin(path, "07 NPC bubble");
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        root.transform.SetParent(null, false); root.transform.position = new Vector3(1.4f, -1.8f, 0); root.transform.localScale = Vector3.one * .01f;
        var caption = root.transform.Find("ShortCaption");
        var text = caption ? caption.GetComponentInChildren<TMP_Text>(true) : root.GetComponentInChildren<TMP_Text>(true); text.gameObject.SetActive(true); text.rectTransform.sizeDelta = new Vector2(500, 130); text.fontSize = 30;
        Sample(text, "Alles ruhig. Verdächtig ruhig.", "All quiet. Suspiciously quiet.");
        End(root, "NPC-Sprechblase", "Kurzer Kommentar direkt über einer Person, ohne Spielpause.", path, "Gemeinsame ShortCaption-Gestaltung; im Spiel über dem Kopf in der Welt.");
    }
    static void BuildResult()
    {
        string path = "Assets/Prefabs/UI/Panels/ResultPanel.prefab"; var root = Begin(path, "08 Ergebnis / Landung & Absturz");
        var panel = root.GetComponent<ResultPanel>();
        Sample(panel.title, "Sicher gelandet", "Landed safely"); Sample(panel.message, "Außenposten erreicht.\nZeit, nach der Fracht zu sehen.", "Outpost reached.\nTime to find the cargo.");
        Sample(panel.totalScore, "XP-PUNKTE 1250", "TOTAL SCORE 1250"); panel.score.gameObject.SetActive(false);
        panel.nextLevelButton.gameObject.SetActive(false); panel.chooseShipButton.gameObject.SetActive(false); panel.refillButton.gameObject.SetActive(false); panel.continueButton.gameObject.SetActive(false); panel.retryButton.gameObject.SetActive(false); ButtonSample(panel.exitLanderButton, "Aussteigen", "Exit ship");
        End(root, "Ergebnis / Landung & Absturz", "Ein gemeinsames Panel für Landungs- und Absturzergebnisse.", path, "Ergebniszustand und passende Aktionen kommen vom ResultPanel-Controller.");
    }
    static void BuildWorld()
    {
        var world = new GameObject("Static world reference");
        Line(world.transform, "Ground", new[] { new Vector3(-12, -4), new Vector3(12, -4) });
        Line(world.transform, "Pad", new[] { new Vector3(-3, -3.6f), new Vector3(0, -3.6f) });
        Line(world.transform, "Building", new[] { new Vector3(3, -4), new Vector3(3, 1), new Vector3(6, 1), new Vector3(6, -4) });
        Line(world.transform, "Door", new[] { new Vector3(3.8f, -4), new Vector3(3.8f, -2), new Vector3(4.8f, -2), new Vector3(4.8f, -4) });
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lander/Lander01.prefab").GetComponent<SpriteRenderer>();
        if (source && source.sprite) { var ship = new GameObject("Static ship visual").AddComponent<SpriteRenderer>(); ship.sprite = source.sprite; ship.transform.SetParent(world.transform); ship.transform.position = new Vector3(-1.5f, -2.7f, 0); ship.transform.localScale = Vector3.one * (1.8f / source.sprite.bounds.size.y); }
        var personAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Station/StationOperatorVisual.prefab");
        if (personAsset)
        {
            var person = (GameObject)PrefabUtility.InstantiatePrefab(personAsset, world.transform);
            person.name = "Static person reference";
            foreach (var controller in person.GetComponentsInChildren<CharacterVisual>(true)) Object.DestroyImmediate(controller);
            foreach (var animator in person.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var renderers = person.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                person.transform.localScale *= 1.7f / Mathf.Max(.01f, bounds.size.y);
                bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                person.transform.position += new Vector3(1.4f - bounds.center.x, -4f - bounds.min.y, 0);
            }
        }
        var random = new System.Random(144); for (int i = 0; i < 35; i++) { float x = (float)random.NextDouble() * 22 - 11; float y = (float)random.NextDouble() * 14 - 4; Line(world.transform, "Star " + i, new[] { new Vector3(x, y), new Vector3(x + .025f, y) }); }
    }
    static void Line(Transform parent, string name, Vector3[] points)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent); line.useWorldSpace = false; line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = .025f;
        line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/OutlineWhite.mat");
        if (!line.sharedMaterial) line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
    }
    static GameObject AsGameObject(this Object obj) => obj as GameObject;
    public static void Preview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var gallery = Object.FindFirstObjectByType<CommunicationPanelGallery>(); gallery.ShowCase(2);
        var camera = Camera.main; var canvas = gallery.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        foreach (int index in new[] { 0, 1, 2, 3, 6, 7 })
        {
            gallery.ShowCase(index);
            var target = new RenderTexture(1080, 1920, 24); camera.targetTexture = target;
            gallery.GetComponent<CanvasScaler>().SendMessage("Handle"); Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); pixels.Apply();
            Directory.CreateDirectory("Documentation/SpaceExperience"); File.WriteAllBytes("Documentation/SpaceExperience/CommunicationPanelGallery" + (index == 0 ? "Tutorial" : index == 1 ? "Conversation" : index == 2 ? "RadioPortrait" : index == 3 ? "Radio" : index == 6 ? "Landing" : "GameOver") + ".png", pixels.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
        }
        Debug.Log("Communication panel gallery statically rendered; no Play Mode executed.");
    }
}

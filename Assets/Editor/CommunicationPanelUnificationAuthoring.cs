using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CommunicationPanelUnificationAuthoring
{
    const string Shared = "Assets/Prefabs/UI/Shared/";
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
    public static void Build()
    {
        Directory.CreateDirectory(Shared); AssetDatabase.Refresh();
        var dialogue = AssetDatabase.LoadAssetAtPath<GameObject>(Shared + "DialogueContent.prefab") ?? BuildDialogue();
        var result = AssetDatabase.LoadAssetAtPath<GameObject>(Shared + "ResultContent.prefab") ?? BuildResult();
        var caption = AssetDatabase.LoadAssetAtPath<GameObject>(Shared + "ShortCaption.prefab") ?? BuildCaption();
        Edit("Assets/Prefabs/UI/StoryDialog.prefab", root => ReplaceDialogue(root, dialogue));
        Edit("Assets/Prefabs/UI/TrainingBriefing.prefab", root => ReplaceTraining(root, dialogue));
        Edit("Assets/Prefabs/UI/Panels/ResultPanel.prefab", root => ReplaceResult(root, result));
        Edit("Assets/Prefabs/UI/Radio.prefab", PolishRadio);
        Edit("Assets/Prefabs/UI/SpaceFlightNavigation.prefab", root => ReplaceAside(root, caption));
        Edit("Assets/Prefabs/UI/StationWorldText.prefab", root => ReplaceBubble(root, caption));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Station01", "Assets/Prefabs/Characters/Expansion/Residents" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<StationResident>(true)) continue;
            Edit(path, RepairResidents);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        foreach (var dialog in Object.FindObjectsByType<StoryDialog>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BindDialogue(dialog.gameObject);
        foreach (var training in Object.FindObjectsByType<TrainingBriefingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BindTraining(training.gameObject);
        foreach (var panel in Object.FindObjectsByType<ResultPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) BindResult(panel.gameObject);
        foreach (var radio in Object.FindObjectsByType<RadioController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) PolishRadio(radio.gameObject);
        foreach (var root in scene.GetRootGameObjects()) RepairResidents(root);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Communication panels unified with nested DialogueContent, ResultContent and ShortCaption prefabs; existing gameplay controllers retained.");
    }
    public static void FinishPolish()
    {
        Edit(Shared + "DialogueContent.prefab", root => root.transform.Find("Speaker").GetComponent<RectTransform>().anchoredPosition = new Vector2(-70, 0));
        Edit("Assets/Prefabs/UI/Radio.prefab", PolishRadio);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Station01", "Assets/Prefabs/Characters/Expansion/Residents" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<StationResident>(true)) Edit(path, RepairResidents);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        foreach (var root in scene.GetRootGameObjects()) RepairResidents(root);
        foreach (var radio in Object.FindObjectsByType<RadioController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) PolishRadio(radio.gameObject);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        InspectBindings(); CommunicationPanelGalleryAuthoring.Build();
    }
    public static void FinalizeGallery()
    {
        InspectBindings();
        CommunicationPanelGalleryAuthoring.Build();
    }
    public static void InspectBindings()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        foreach (var dialog in Object.FindObjectsByType<StoryDialog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = new SerializedObject(dialog);
            foreach (string field in new[] { "speakerText", "messageText", "conversationText", "continueButton", "conversationContent", "storyPortrait" }) Debug.Log("UI binding " + field + ": " + data.FindProperty(field).objectReferenceValue);
        }
        var ui = Object.FindFirstObjectByType<LanderUI>();
        Debug.Log("Result bindings: " + ui.resultPanel.title + ", " + ui.resultPanel.exitLanderButton + ", " + ui.resultPanel.refillProgressFill + ", " + ui.resultPanel.retryButton);
        var navigation = Object.FindFirstObjectByType<SpaceFlightNavigation>();
        Debug.Log("Caption binding: " + new SerializedObject(navigation).FindProperty("asideText").objectReferenceValue);
        foreach (var resident in Object.FindObjectsByType<StationResident>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Debug.Log("NPC caption binding: " + resident.name + " / " + new SerializedObject(resident).FindProperty("bubbleText").objectReferenceValue);
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (text.LocalizationSource().Contains(".sign")) Debug.Log("Door/world sign retained: " + text.LocalizationSource());
    }
    static void Edit(string path, System.Action<GameObject> change)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); Debug.Log("Unified prefab: " + path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static TMP_Text Text(Transform parent, string name, float size, Vector2 dimensions, Vector2 position)
    {
        var text = Rect(parent, name, dimensions, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font; text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; text.text = "";
        text.gameObject.AddComponent<LocalizedText>(); return text;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
    static void Plate(TMP_Text text)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PanelBackdrop.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, text.transform.parent);
        root.name = "Backdrop_" + text.name; Stretch(root.GetComponent<RectTransform>()); root.transform.SetAsFirstSibling();
        var data = new SerializedObject(root.GetComponent<TextBackdropGraphic>()); data.FindProperty("source").objectReferenceValue = text; data.ApplyModifiedPropertiesWithoutUndo();
    }
    static Button Button(Transform parent, string name, string label, float height = 80, bool refill = false)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(refill ? "Assets/Prefabs/UI/RefillButton.prefab" : "Assets/Prefabs/UI/ActionButton.prefab"), parent);
        root.name = name; root.GetComponent<RectTransform>().sizeDelta = new Vector2(900, height);
        var layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>(); layout.preferredHeight = height; layout.flexibleHeight = 0;
        var text = root.GetComponentInChildren<TMP_Text>(true); text.fontSize = 30; text.enableAutoSizing = true; text.fontSizeMin = 24; text.fontSizeMax = 30; text.SetLocalizedText(label);
        var button = root.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent(); return button;
    }
    static GameObject Save(GameObject root, string name)
    {
        var asset = PrefabUtility.SaveAsPrefabAsset(root, Shared + name + ".prefab"); Object.DestroyImmediate(root); return asset;
    }
    static GameObject BuildDialogue()
    {
        var root = Rect(null, "DialogueContent", Vector2.zero, Vector2.zero); Stretch(root);
        root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .08f);
        var speaker = Text(root, "Speaker", 30, new Vector2(760, 72), new Vector2(-70, 0)); speaker.rectTransform.anchorMin = speaker.rectTransform.anchorMax = new Vector2(.5f, .83f); speaker.rectTransform.pivot = new Vector2(.5f, 1); Plate(speaker);
        var portrait = Rect(root, "Portrait", new Vector2(72, 84), new Vector2(414, 0)); portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .83f); portrait.pivot = new Vector2(.5f, 1);
        var image = portrait.gameObject.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false; portrait.gameObject.SetActive(false);
        var message = Text(root, "Message", 34, new Vector2(900, 420), Vector2.zero); message.rectTransform.anchorMin = message.rectTransform.anchorMax = new Vector2(.5f, .75f); message.rectTransform.pivot = new Vector2(.5f, 1); Plate(message);
        var conversation = Rect(root, "Conversation", Vector2.zero, Vector2.zero); Stretch(conversation);
        var body = Text(conversation, "Text", 34, new Vector2(900, 420), Vector2.zero); body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(.5f, .75f); body.rectTransform.pivot = new Vector2(.5f, 1); Plate(body);
        conversation.gameObject.SetActive(false);
        var actions = Rect(root, "Actions", new Vector2(900, 0), Vector2.zero); actions.anchorMin = actions.anchorMax = new Vector2(.5f, .14f); actions.pivot = new Vector2(.5f, 0);
        var layout = actions.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 14; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        actions.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Button(actions, "Continue", "[[game.continue]]");
        for (int i = 0; i < 6; i++) Button(actions, "Answer" + i, "").gameObject.SetActive(false);
        return Save(root.gameObject, "DialogueContent");
    }
    static GameObject BuildResult()
    {
        var root = Rect(null, "ResultContent", new Vector2(900, 0), Vector2.zero); root.anchorMin = root.anchorMax = new Vector2(.5f, .8f); root.pivot = new Vector2(.5f, 1);
        var background = root.gameObject.AddComponent<Image>(); background.color = new Color(.198f, .198f, .198f, 1f); background.raycastTarget = false;
        var outline = root.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(1f, 1f, 1f, .5f); outline.effectDistance = new Vector2(2f, 2f);
        var layout = root.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(24, 24, 24, 24); layout.spacing = 20; layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var information = Rect(root, "Information", Vector2.zero, Vector2.zero);
        information.anchorMin = new Vector2(0, 1); information.anchorMax = new Vector2(1, 1); information.pivot = new Vector2(.5f, 1); information.sizeDelta = Vector2.zero;
        var informationLayout = information.gameObject.AddComponent<VerticalLayoutGroup>(); informationLayout.spacing = 12; informationLayout.childControlHeight = informationLayout.childControlWidth = true; informationLayout.childForceExpandWidth = true; informationLayout.childForceExpandHeight = false;
        information.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var specification in new[] { ("Title", 48f, 66f), ("Message", 32f, 64f), ("Score", 27f, 40f), ("TotalScore", 28f, 40f) })
        {
            var text = Text(information, specification.Item1, specification.Item2, new Vector2(900, specification.Item3), Vector2.zero);
            text.gameObject.AddComponent<LayoutElement>().minHeight = specification.Item3;
            text.SetLocalizedText(specification.Item1 switch
            {
                "Title" => "[[game.landed.title]]",
                "Message" => "[[game.landing.confirmed]]",
                "Score" => "[[score.success]] +120\n[[score.speed]]   +80\n[[score.angle]]   +100\n[[score.center]]  +150\n[[score.fuel]]    +60\n[[score.time]]    +40\n────────────\n[[score.score]]   550\n\n[[score.best]]    1250\n",
                "TotalScore" => "[[score.xp]] 1250",
                _ => ""
            });
        }
        var actions = Rect(root, "Actions", Vector2.zero, Vector2.zero);
        actions.anchorMin = new Vector2(0, 1); actions.anchorMax = new Vector2(1, 1); actions.pivot = new Vector2(.5f, 1); actions.sizeDelta = Vector2.zero;
        var actionsLayout = actions.gameObject.AddComponent<VerticalLayoutGroup>(); actionsLayout.spacing = 12; actionsLayout.childControlHeight = actionsLayout.childControlWidth = true; actionsLayout.childForceExpandWidth = true; actionsLayout.childForceExpandHeight = false;
        actions.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var button in new[]
        {
            ("Next", "[[game.next.level]]"), ("Continue", "[[game.continue.flight]]"), ("Refill", "[[game.refill.tank]]"),
            ("Choose", "[[game.lander.bay]]"), ("Exit", "[[game.exit.lander]]"), ("Retry", "[[game.try.again]]")
        }) Button(actions, button.Item1, button.Item2, 80, button.Item1 == "Refill").gameObject.SetActive(false);
        return Save(root.gameObject, "ResultContent");
    }
    static GameObject BuildCaption()
    {
        var root = Rect(null, "ShortCaption", new Vector2(900, 140), Vector2.zero);
        var text = Text(root, "Text", 30, Vector2.zero, Vector2.zero); Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center; Plate(text);
        return Save(root.gameObject, "ShortCaption");
    }
    static void ClearChildren(Transform root, string keep = null)
    {
        foreach (Transform child in root.Cast<Transform>().ToArray()) if (child.name != keep) Object.DestroyImmediate(child.gameObject);
    }
    static GameObject Nest(GameObject asset, Transform parent) => (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
    static void CleanWrapper(GameObject root)
    {
        foreach (var component in root.GetComponents<LayoutGroup>()) Object.DestroyImmediate(component);
        foreach (var component in root.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(component);
        foreach (var outline in root.GetComponents<Outline>()) Object.DestroyImmediate(outline);
        if (root.TryGetComponent<Image>(out var image)) image.color = Color.clear;
        Stretch(root.GetComponent<RectTransform>());
    }
    static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
    static T Find<T>(Transform parent, string path) where T : Component => parent.Find(path).GetComponent<T>();
    static void ReplaceDialogue(GameObject root, GameObject asset) { ClearChildren(root.transform); CleanWrapper(root); Nest(asset, root.transform); BindDialogue(root); }
    static void BindDialogue(GameObject root)
    {
        var module = root.transform.Find("DialogueContent"); if (!module) return;
        ClearChildren(root.transform, "DialogueContent"); CleanWrapper(root);
        var data = new SerializedObject(root.GetComponent<StoryDialog>());
        Set(data, "messageText", Find<TMP_Text>(module, "Message")); Set(data, "speakerText", Find<TMP_Text>(module, "Speaker")); Set(data, "conversationText", Find<TMP_Text>(module, "Conversation/Text"));
        Set(data, "conversationContent", module.Find("Conversation").gameObject); Set(data, "continueButton", Find<Button>(module, "Actions/Continue")); Set(data, "storyPortrait", Find<Image>(module, "Portrait"));
        var answers = data.FindProperty("answerButtons"); answers.arraySize = 6;
        for (int i = 0; i < 6; i++) answers.GetArrayElementAtIndex(i).objectReferenceValue = Find<Button>(module, "Actions/Answer" + i);
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void ReplaceTraining(GameObject root, GameObject asset)
    {
        var panel = root.GetComponentInChildren<TrainingBriefingPanel>(true).gameObject; ClearChildren(panel.transform); CleanWrapper(panel); Nest(asset, panel.transform); BindTraining(panel);
    }
    static void BindTraining(GameObject root)
    {
        var module = root.transform.Find("DialogueContent"); if (!module) return;
        ClearChildren(root.transform, "DialogueContent"); CleanWrapper(root);
        var data = new SerializedObject(root.GetComponent<TrainingBriefingPanel>());
        Set(data, "speakerText", Find<TMP_Text>(module, "Speaker")); Set(data, "messageText", Find<TMP_Text>(module, "Message")); Set(data, "continueButton", Find<Button>(module, "Actions/Continue")); Set(data, "storyPortrait", Find<Image>(module, "Portrait"));
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void ReplaceResult(GameObject root, GameObject asset)
    {
        var controller = root.GetComponent<ResultPanel>(); var old = new SerializedObject(controller);
        string[] fields = { "nextLevelButton", "continueButton", "refillButton", "chooseShipButton", "exitLanderButton", "retryButton" };
        var labels = fields.Select(field =>
        {
            var button = old.FindProperty(field).objectReferenceValue as Button;
            string name = field == "nextLevelButton" ? "Next" : field == "continueButton" ? "Continue" : field == "refillButton" ? "Refill" : field == "chooseShipButton" ? "Choose" : field == "exitLanderButton" ? "Exit" : "Retry";
            if (!button) button = root.transform.Find("ResultContent/" + name)?.GetComponent<Button>();
            return button ? button.GetComponentInChildren<TMP_Text>(true).LocalizationSource() : "";
        }).ToArray();
        ClearChildren(root.transform); CleanWrapper(root); var module = Nest(asset, root.transform); BindResult(root);
        for (int i = 0; i < fields.Length; i++) ((Button)new SerializedObject(controller).FindProperty(fields[i]).objectReferenceValue).GetComponentInChildren<TMP_Text>(true).SetLocalizedText(labels[i]);
        Find<TMP_Text>(module.transform, "Information/Score").gameObject.SetActive(true);
    }
    static void BindResult(GameObject root)
    {
        var module = root.transform.Find("ResultContent"); if (!module) return;
        ClearChildren(root.transform, "ResultContent"); CleanWrapper(root);
        var data = new SerializedObject(root.GetComponent<ResultPanel>());
        Set(data, "title", Find<TMP_Text>(module, "Information/Title")); Set(data, "message", Find<TMP_Text>(module, "Information/Message")); Set(data, "totalScore", Find<TMP_Text>(module, "Information/TotalScore"));
        Set(data, "score", Find<TMP_Text>(module, "Information/Score"));
        Set(data, "nextLevelButton", Find<Button>(module, "Actions/Next")); Set(data, "continueButton", Find<Button>(module, "Actions/Continue")); Set(data, "refillButton", Find<Button>(module, "Actions/Refill")); Set(data, "chooseShipButton", Find<Button>(module, "Actions/Choose")); Set(data, "exitLanderButton", Find<Button>(module, "Actions/Exit")); Set(data, "retryButton", Find<Button>(module, "Actions/Retry"));
        Set(data, "refillProgressFill", module.Find("Actions/Refill").GetComponentsInChildren<Image>(true).First(i => i.name == "RefillFill"));
        foreach (string name in new[] { "Next", "Continue", "Refill", "Choose", "Exit", "Retry" }) module.Find("Actions/" + name).gameObject.SetActive(true);
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Position(Component component, Vector2 position, Vector2 size)
    {
        var rect = component.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    static void PolishRadio(GameObject root)
    {
        var controller = root.GetComponent<RadioController>(); if (!controller) return; var data = new SerializedObject(controller);
        T Field<T>(string field) where T : Object => data.FindProperty(field).objectReferenceValue as T;
        Field<GameObject>("device").GetComponent<Image>().color = new Color(0, 0, 0, .12f);
        var sender = Field<TMP_Text>("senderText"); Position(sender, new Vector2(-110, 460), new Vector2(520, 72)); sender.fontSize = 30;
        var title = Field<TMP_Text>("titleText"); Position(title, new Vector2(-80, 350), new Vector2(600, 86)); title.fontSize = 27;
        var scroll = Field<ScrollRect>("messageScroll"); Position(scroll, new Vector2(0, 215), new Vector2(760, 180));
        var body = Field<TMP_Text>("bodyText"); body.fontSize = 32;
        body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.pivot = new Vector2(.5f, 1); body.rectTransform.anchoredPosition = Vector2.zero;
        body.rectTransform.sizeDelta = new Vector2(0, 180); scroll.content = body.rectTransform; scroll.viewport = scroll.GetComponent<RectTransform>();
        var replies = data.FindProperty("replyButtons");
        for (int i = 0; i < replies.arraySize; i++)
        {
            var button = (Button)replies.GetArrayElementAtIndex(i).objectReferenceValue;
            Position(button, new Vector2(i % 2 == 0 ? -195 : 195, 70 - i / 2 * 82), new Vector2(370, 70));
            var label = button.GetComponentInChildren<TMP_Text>(true); label.fontSize = 27; label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = 27;
        }
        var signal = Field<TMP_Text>("signalNameText"); Position(signal, new Vector2(0, -210), new Vector2(570, 64));
        foreach (string field in new[] { "previousSignalButton", "nextSignalButton" })
        {
            var button = Field<Button>(field); Position(button, new Vector2(field.StartsWith("previous") ? -340 : 340, -210), new Vector2(80, 64)); button.GetComponentInChildren<TMP_Text>(true).SetLocalizedText(field.StartsWith("previous") ? "<" : ">");
        }
        Position(Field<TMP_Text>("signalText"), new Vector2(0, -285), new Vector2(760, 68)); Position(Field<Button>("trackButton"), new Vector2(0, -365), new Vector2(760, 74));
        var parent = sender.transform.parent;
        var divider = parent.Find("SignalDivider"); if (divider) Position(divider, new Vector2(0, -165), new Vector2(760, 2));
        var hint = parent.Find("ScrollHint"); if (hint) Position(hint, new Vector2(220, 112), new Vector2(300, 26));
    }
    static void ReplaceAside(GameObject root, GameObject asset)
    {
        var data = new SerializedObject(root.GetComponent<SpaceFlightNavigation>()); var previous = (TMP_Text)data.FindProperty("asideText").objectReferenceValue;
        var parent = previous.transform.parent; Object.DestroyImmediate(previous.gameObject); var caption = Nest(asset, parent);
        var rect = caption.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 2f / 3); rect.anchoredPosition = Vector2.zero;
        Set(data, "asideText", caption.GetComponentInChildren<TMP_Text>(true)); data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void ReplaceBubble(GameObject root, GameObject asset)
    {
        // Keep the legacy text object: door-sign instances override its stable prefab ID.
        var existing = root.transform.Find("ShortCaption");
        var caption = existing ? existing.gameObject : Nest(asset, root.transform);
        Stretch(caption.GetComponent<RectTransform>());
    }
    static void RepairResidents(GameObject root)
    {
        foreach (var resident in root.GetComponentsInChildren<StationResident>(true))
        {
            var bubble = resident.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.renderMode == RenderMode.WorldSpace); if (!bubble) continue;
            bubble.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 130);
            var caption = bubble.transform.Find("ShortCaption");
            var text = caption ? caption.GetComponentInChildren<TMP_Text>(true) : bubble.GetComponentInChildren<TMP_Text>(true); if (!text) continue;
            foreach (var legacy in bubble.GetComponentsInChildren<TMP_Text>(true)) if (legacy != text) legacy.gameObject.SetActive(false);
            text.SetLocalizedText(""); text.gameObject.SetActive(false);
            var data = new SerializedObject(resident); Set(data, "bubbleText", text); data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

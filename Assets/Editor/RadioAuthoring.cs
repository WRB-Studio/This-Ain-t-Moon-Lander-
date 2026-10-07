using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class RadioAuthoring
{
    const string RadioPath = "Assets/Prefabs/UI/Radio.prefab";
    const string DialogPath = "Assets/Prefabs/UI/StoryDialog.prefab";
    const string InteriorPath = "Assets/Prefabs/Station01/StationInterior.prefab";
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");

    [MenuItem("Tools/Radio/Build Radio and Registration")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var dialog = PrefabUtility.LoadPrefabContents(DialogPath);
        try
        {
            var asset = MakeRadio(dialog);
            foreach (var portrait in dialog.GetComponentsInChildren<RectTransform>(true)
                .Where(t => t.name.StartsWith("Operator") || t.name == "StationOperator").ToArray())
                if (portrait) UnityEngine.Object.DestroyImmediate(portrait.gameObject);
            var conversation = dialog.transform.Find("ConversationContent");
            if (conversation)
            {
                var text = conversation.Find("ConversationText").GetComponent<RectTransform>();
                text.sizeDelta = new Vector2(900f, 480f);
                text.anchoredPosition = Vector2.zero;
            }
            PrefabUtility.SaveAsPrefabAsset(dialog, DialogPath);
            UpdateRegistration();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
            var existing = UnityEngine.Object.FindFirstObjectByType<RadioController>(FindObjectsInactive.Include);
            if (!existing) PrefabUtility.InstantiatePrefab(asset, scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ExportPreview(asset);
            Debug.Log("Radio authored: prefab, registration handover and MainScene integration saved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(dialog); }
    }

    static GameObject MakeRadio(GameObject oldDialog)
    {
        var root = new GameObject("Radio", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RadioController));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var controller = new SerializedObject(root.GetComponent<RadioController>());
        var open = Button(root.transform, "OpenRadio", "RADIO", new Vector2(-150f, -140f), new Vector2(240f, 90f));
        var openRect = open.GetComponent<RectTransform>();
        openRect.anchorMin = openRect.anchorMax = Vector2.one;
        Set("openButton", open);
        Set("openLabel", open.GetComponentInChildren<TMP_Text>());
        Set("unreadLight", Image(open.transform, "UnreadLight", new Vector2(95f, 26f), new Vector2(10f, 10f), Color.white).gameObject);
        var icon = Rect(root.transform, "TrackedSignal", Vector2.zero, new Vector2(40f, 40f));
        var graphic = icon.gameObject.AddComponent<RadioSignalGraphic>();
        graphic.raycastTarget = false;
        var distance = Text(icon, "Distance", "SIGNAL", new Vector2(0f, -50f), new Vector2(420f, 90f), 26f);
        distance.alignment = TextAlignmentOptions.Center;
        Set("navigationIcon", icon);
        Set("navigationGraphic", graphic);
        Set("navigationDistance", distance);

        var device = Image(root.transform, "Device", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.85f));
        Stretch(device.rectTransform);
        device.raycastTarget = true;
        Set("device", device.gameObject);
        Image(device.transform, "Antenna", new Vector2(340f, 722f), new Vector2(12f, 176f), Color.white);
        Image(device.transform, "AntennaTip", new Vector2(340f, 810f), new Vector2(24f, 12f), Color.white);
        var caseBorder = Image(device.transform, "Case", Vector2.zero, new Vector2(860f, 1320f), Color.white);
        var panel = Image(caseBorder.transform, "Display", Vector2.zero, new Vector2(852f, 1312f), Color.black);
        var title = Text(panel.transform, "DeviceTitle", "OUTPOST / RADIO", new Vector2(-100f, 560f), new Vector2(550f, 80f), 38f);
        title.fontStyle = FontStyles.Bold;
        Set("closeButton", Button(panel.transform, "Close", "CLOSE", new Vector2(310f, 560f), new Vector2(140f, 70f)));
        Image(panel.transform, "HeaderLine", new Vector2(0f, 505f), new Vector2(760f, 2f), Color.white);
        Set("senderText", Text(panel.transform, "Sender", "Registration", new Vector2(-120f, 430f), new Vector2(520f, 100f), 40f));
        Set("titleText", Text(panel.transform, "Subject", "Last confirmed pickup", new Vector2(-120f, 345f), new Vector2(520f, 70f), 27f));

        var portraitFrame = Image(panel.transform, "PortraitFrame", new Vector2(295f, 405f), new Vector2(170f, 210f), Color.white);
        var portraitPanel = Image(portraitFrame.transform, "PortraitDisplay", Vector2.zero, new Vector2(166f, 206f), Color.black);
        portraitPanel.gameObject.AddComponent<RectMask2D>();
        var voiceOnly = Text(portraitPanel.transform, "VoiceOnly", "AUDIO\nONLY", Vector2.zero, new Vector2(160f, 180f), 25f);
        voiceOnly.alignment = TextAlignmentOptions.Center;
        Set("voiceOnly", voiceOnly.gameObject);
        var portraitArray = controller.FindProperty("portraits");
        portraitArray.arraySize = 5;
        var names = new[] { "OperatorNeutral", "OperatorAnnoyed", "OperatorAngry", "OperatorSurprised", "StationOperator" };
        for (int i = 0; i < names.Length; i++)
        {
            var original = oldDialog.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == names[i]);
            if (!original)
            {
                var previousRadio = AssetDatabase.LoadAssetAtPath<GameObject>(RadioPath);
                if (previousRadio) original = previousRadio.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == names[i]);
            }
            GameObject portrait;
            if (original)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(original.gameObject);
                portrait = source ? (GameObject)PrefabUtility.InstantiatePrefab(source, portraitPanel.transform)
                    : UnityEngine.Object.Instantiate(original.gameObject, portraitPanel.transform);
                if (source) PrefabUtility.SetPropertyModifications(portrait, PrefabUtility.GetPropertyModifications(original.gameObject));
            }
            else portrait = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                i == 4 ? "Assets/Prefabs/UI/StationOperator.prefab" : "Assets/Prefabs/UI/MissionOperator.prefab"), portraitPanel.transform);
            portrait.name = names[i];
            var rect = portrait.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(180f, 210f);
            rect.localScale = Vector3.one * 0.8f;
            rect.localRotation = Quaternion.identity;
            foreach (string bodyPart in new[] { "Body", "Arms" })
                if (portrait.transform.Find(bodyPart)) portrait.transform.Find(bodyPart).gameObject.SetActive(false);
            portrait.SetActive(false);
            foreach (var image in portrait.GetComponentsInChildren<Graphic>(true)) image.raycastTarget = false;
            var item = portraitArray.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("expression").enumValueIndex = i + 1;
            item.FindPropertyRelative("visual").objectReferenceValue = portrait;
        }

        var viewport = Image(panel.transform, "MessageViewport", new Vector2(0f, 80f), new Vector2(760f, 400f), Color.black);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.raycastTarget = true;
        var body = Text(viewport.transform, "MessageBody", "Your radio is registered.\n\nLast confirmed pickup coordinates attached.", Vector2.zero, new Vector2(820f, 400f), 32f);
        body.rectTransform.anchorMin = new Vector2(0f, 1f);
        body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.sizeDelta = new Vector2(-20f, 400f);
        body.rectTransform.anchoredPosition = Vector2.zero;
        body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = body.rectTransform;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        Set("bodyText", body);
        Set("messageScroll", scroll);
        var scrollHint = Text(panel.transform, "ScrollHint", "SCROLL / SWIPE", new Vector2(260f, -122f), new Vector2(240f, 28f), 18f);
        scrollHint.alignment = TextAlignmentOptions.Right;
        Image(panel.transform, "SignalDivider", new Vector2(0f, -140f), new Vector2(760f, 2f), Color.white);
        Set("previousSignalButton", Button(panel.transform, "PreviousSignal", "<", new Vector2(-340f, -190f), new Vector2(80f, 60f)));
        Set("nextSignalButton", Button(panel.transform, "NextSignal", ">", new Vector2(340f, -190f), new Vector2(80f, 60f)));
        var signalName = Text(panel.transform, "SignalName", "Station (1/2)", new Vector2(0f, -190f), new Vector2(550f, 60f), 26f);
        signalName.alignment = TextAlignmentOptions.Center;
        signalName.enableAutoSizing = true;
        signalName.fontSizeMin = 20f; signalName.fontSizeMax = 26f;
        Set("signalNameText", signalName);
        Set("signalText", Text(panel.transform, "SignalCoordinates", "SELECTED  STATION", new Vector2(0f, -265f), new Vector2(760f, 80f), 24f));
        var track = Button(panel.transform, "TrackSignal", "Track signal", new Vector2(0f, -355f), new Vector2(760f, 70f));
        Set("trackButton", track);
        Set("trackLabel", track.GetComponentInChildren<TMP_Text>());
        var replies = controller.FindProperty("replyButtons");
        replies.arraySize = 2;
        for (int i = 0; i < 2; i++)
            replies.GetArrayElementAtIndex(i).objectReferenceValue = Button(panel.transform, "Reply" + i,
                i == 0 ? "Received. I'll take a look." : "I'm not your courier.", new Vector2(i == 0 ? -200f : 200f, -455f), new Vector2(360f, 100f));
        Set("previousButton", Button(panel.transform, "PreviousMessage", "PREVIOUS", new Vector2(-270f, -565f), new Vector2(220f, 80f)));
        Set("nextButton", Button(panel.transform, "NextMessage", "NEXT", new Vector2(270f, -565f), new Vector2(220f, 80f)));
        var page = Text(panel.transform, "MessageNumber", "1 / 1", new Vector2(0f, -565f), new Vector2(260f, 70f), 30f);
        page.alignment = TextAlignmentOptions.Center;
        Set("pageText", page);
        controller.ApplyModifiedPropertiesWithoutUndo();
        device.gameObject.SetActive(false);
        icon.gameObject.SetActive(false);
        open.gameObject.SetActive(false);
        var asset = PrefabUtility.SaveAsPrefabAsset(root, RadioPath);
        UnityEngine.Object.DestroyImmediate(root);
        return asset;

        void Set(string field, UnityEngine.Object value) => controller.FindProperty(field).objectReferenceValue = value;
    }

    static void UpdateRegistration()
    {
        var root = PrefabUtility.LoadPrefabContents(InteriorPath);
        try
        {
            var conversation = new SerializedObject(root.GetComponent<StationConversation>());
            var steps = conversation.FindProperty("steps");
            if (!Enumerable.Range(0, steps.arraySize).Any(i => steps.GetArrayElementAtIndex(i).FindPropertyRelative("givesRadio").boolValue))
            {
                steps.InsertArrayElementAtIndex(3);
                var step = steps.GetArrayElementAtIndex(3);
                step.FindPropertyRelative("message").stringValue = "Before we get to the coordinates: take this radio.\n\nMessages, optional video, signal tracking. It even keeps the old messages.\n\nPlease take it. Our last pilot was difficult to reach.";
                step.FindPropertyRelative("givesRadio").boolValue = true;
                var answers = step.FindPropertyRelative("answers");
                answers.arraySize = 2;
                answers.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue = "All right. I'll take it.";
                answers.GetArrayElementAtIndex(0).FindPropertyRelative("reaction").stringValue = "Excellent. One radio issued. Try to return with the same number of radios.";
                answers.GetArrayElementAtIndex(1).FindPropertyRelative("text").stringValue = "No thanks. I don't need a radio.";
                answers.GetArrayElementAtIndex(1).FindPropertyRelative("reaction").stringValue = "Of course. I'll record 'accepted reluctantly'. Here you go. Refusing equipment requires a radio to submit the refusal.";
                steps.GetArrayElementAtIndex(5).FindPropertyRelative("message").stringValue = "The coordinates are in a message on your new radio.\n\nOpen RADIO and select 'Track signal' when you're ready.\n\nMaintenance has topped up your tank.\n\nFor now I'll write: 'Ship returned. Rest unclear.'\n\nThat holds up surprisingly well.";
                conversation.FindProperty("repeatMessage").stringValue = "The pickup coordinates are still on your radio.\n\nOpen RADIO to read the message or track the signal.\n\nInvestigate, go home, or enjoy the corridor. I have plenty of forms to keep me company.";
            }
            conversation.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, InteriorPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    static TMP_Text Text(Transform parent, string name, string content, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }
    static Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ActionButton.prefab"), parent);
        instance.name = name;
        var rect = instance.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        var button = instance.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        var text = button.GetComponentInChildren<TMP_Text>(true);
        text.font = Font;
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 22f; text.fontSizeMax = 30f;
        text.raycastTarget = false;
        return button;
    }

    static void ExportPreview(GameObject prefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var controller = root.GetComponent<RadioController>();
        controller.enabled = false;
        var data = new SerializedObject(controller);
        ((GameObject)data.FindProperty("device").objectReferenceValue).SetActive(true);
        var camera = new GameObject("Radio Preview Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        var body = (TMP_Text)data.FindProperty("bodyText").objectReferenceValue;
        body.text = data.FindProperty("pickupMessage").FindPropertyRelative("body").stringValue;
        var portraits = data.FindProperty("portraits");
        ((GameObject)portraits.GetArrayElementAtIndex(4).FindPropertyRelative("visual").objectReferenceValue).SetActive(true);
        ((GameObject)data.FindProperty("voiceOnly").objectReferenceValue).SetActive(false);
        string folder = Environment.GetEnvironmentVariable("RADIO_PREVIEW_OUTPUT") ?? "Temp/RadioPreview";
        Directory.CreateDirectory(folder);
        foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400), new Vector2Int(1920, 1080) })
        {
            var target = new RenderTexture(size.x, size.y, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            root.GetComponent<CanvasScaler>().SendMessage("Handle");
            Canvas.ForceUpdateCanvases();
            var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            pixels.Apply();
            string name = size.y == 1920 ? "Radio.png" : $"Radio-{size.x}x{size.y}.png";
            File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
        Debug.Log("Radio UI preview exported: " + folder);
    }
}

using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System;
using System.Linq;

public static class PauseMenuAuthoring
{
    public static void BuildAndLocalize()
    {
        Build();
        LocalizationAuthoring.Apply();
        ExportPreview();
        ExportConversationPreview();
    }
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
    [MenuItem("Tools/UI/Build Pause Menu")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseMenu));
        root.GetComponent<Canvas>().sortingOrder = 100;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var data = new SerializedObject(root.GetComponent<PauseMenu>());
        var open = Button(root.transform, "Pause", "[[menu.pause]]", new Vector2(-150, -65), new Vector2(240, 80));
        open.GetComponent<RectTransform>().anchorMin = open.GetComponent<RectTransform>().anchorMax = Vector2.one;
        Set("openButton", open);
        var overlay = Image(root.transform, "Overlay", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .85f));
        overlay.rectTransform.anchorMin = Vector2.zero; overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        overlay.raycastTarget = true;
        var border = Image(overlay.transform, "Frame", Vector2.zero, new Vector2(850, 1120), Color.white);
        var panel = Image(border.transform, "Menu", Vector2.zero, new Vector2(842, 1112), Color.black);
        Text(panel.transform, "Title", "[[menu.paused]]", new Vector2(0, 390), new Vector2(760, 100), 52);
        Set("resumeButton", Button(panel.transform, "Resume", "[[menu.resume]]", new Vector2(0, 150), new Vector2(700, 100)));
        Set("settingsButton", Button(panel.transform, "Settings", "[[menu.settings]]", new Vector2(0, 10), new Vector2(700, 100)));
        Set("exitButton", Button(panel.transform, "Exit", "[[menu.exit]]", new Vector2(0, -130), new Vector2(700, 100)));
        Text(panel.transform, "Hint", "[[menu.hint]]", new Vector2(0, -360), new Vector2(700, 140), 26);
        var settings = Image(panel.transform, "SettingsPanel", Vector2.zero, new Vector2(842, 1112), Color.black);
        settings.raycastTarget = true;
        Text(settings.transform, "Title", "[[menu.settings]]", new Vector2(0, 410), new Vector2(760, 90), 46);
        Text(settings.transform, "LanguageLabel", "[[menu.language]]", new Vector2(0, 290), new Vector2(700, 65), 30);
        var dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        dropdownObject.name = "Language";
        Place(dropdownObject.GetComponent<RectTransform>(), settings.transform, new Vector2(0, 205), new Vector2(700, 85));
        var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        foreach (var item in Localization.Languages) dropdown.options.Add(new TMP_Dropdown.OptionData(item.nativeName));
        dropdown.RefreshShownValue();
        dropdown.transform.Find("Arrow").GetComponent<Image>().enabled = false;
        var arrow = Text(dropdown.transform, "ArrowLabel", "▼", new Vector2(315, 0), new Vector2(45, 60), 30);
        arrow.color = Color.black;
        foreach (var text in dropdownObject.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = Font; text.fontSize = 30; text.color = Color.black;
        }
        dropdown.template.sizeDelta = new Vector2(0, 230);
        dropdown.itemText.rectTransform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 75);
        Set("language", dropdown);
        Text(settings.transform, "MusicLabel", "[[menu.music]]", new Vector2(0, 65), new Vector2(700, 65), 30);
        Set("music", Slider(settings.transform, "Music", new Vector2(0, -10)));
        Text(settings.transform, "EffectsLabel", "[[menu.effects]]", new Vector2(0, -145), new Vector2(700, 65), 30);
        Set("effects", Slider(settings.transform, "Effects", new Vector2(0, -220)));
        Set("backButton", Button(settings.transform, "Back", "[[menu.back]]", new Vector2(0, -405), new Vector2(700, 100)));
        Set("panel", overlay.gameObject); Set("settings", settings.gameObject);
        data.ApplyModifiedPropertiesWithoutUndo();
        settings.gameObject.SetActive(false); overlay.gameObject.SetActive(false);
        var asset = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/PauseMenu.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        if (!UnityEngine.Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include)) PrefabUtility.InstantiatePrefab(asset, scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Pause menu authored and integrated into MainScene.");
        void Set(string property, UnityEngine.Object value) => data.FindProperty(property).objectReferenceValue = value;
    }
    public static void AddExitButton()
    {
        const string path = "Assets/Prefabs/UI/PauseMenu.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var menu = root.transform.Find("Overlay/Frame/Menu");
            var existing = menu.Find("Exit");
            var button = existing ? existing.GetComponent<Button>()
                : Button(menu, "Exit", "[[menu.exit]]", new Vector2(0, -130), new Vector2(700, 100));
            menu.Find("SettingsPanel").SetAsLastSibling();
            var data = new SerializedObject(root.GetComponent<PauseMenu>());
            data.FindProperty("exitButton").objectReferenceValue = button;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ExportPreview();
        Debug.Log("Exit button added to pause prefab.");
    }
    static void Place(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
    {
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }
    static Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Place(image.rectTransform, parent, position, size); image.color = color; image.raycastTarget = false;
        return image;
    }
    static TMP_Text Text(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LocalizedText)).GetComponent<TMP_Text>();
        Place(label.rectTransform, parent, position, size); label.font = Font; label.text = content;
        label.fontSize = fontSize; label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.SetLocalizedText(content);
        return label;
    }
    static Button Button(Transform parent, string name, string content, Vector2 position, Vector2 size)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ActionButton.prefab"), parent);
        root.name = name; Place(root.GetComponent<RectTransform>(), parent, position, size);
        var button = root.GetComponent<Button>(); button.onClick.RemoveAllListeners();
        var label = button.GetComponentInChildren<TMP_Text>(true); label.font = Font; label.text = content;
        label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = 34;
        if (!label.GetComponent<LocalizedText>()) label.gameObject.AddComponent<LocalizedText>();
        label.SetLocalizedText(content);
        return button;
    }
    static Slider Slider(Transform parent, string name, Vector2 position)
    {
        var root = DefaultControls.CreateSlider(new DefaultControls.Resources()); root.name = name;
        Place(root.GetComponent<RectTransform>(), parent, position, new Vector2(700, 65));
        var slider = root.GetComponent<Slider>(); slider.value = .8f;
        foreach (var image in root.GetComponentsInChildren<Image>()) image.color = image.name == "Background" ? new Color(.2f, .2f, .2f) : Color.white;
        slider.handleRect.sizeDelta = new Vector2(40, 0);
        return slider;
    }

    static void ExportPreview()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PauseMenu.prefab"), scene);
        root.GetComponent<PauseMenu>().enabled = false;
        root.transform.Find("Overlay").gameObject.SetActive(true);
        var settings = root.transform.Find("Overlay/Frame/Menu/SettingsPanel");
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 1f;
        var labels = root.GetComponentsInChildren<TMP_Text>(true).ToDictionary(text => text, text => text.LocalizationSource());
        string output = Environment.GetEnvironmentVariable("PAUSE_PREVIEW_OUTPUT") ?? "Documentation";
        Directory.CreateDirectory(output);
        var previousLanguage = Localization.CurrentLanguage;
        foreach (var code in new[] { "de", "en" })
        {
            Localization.SetLanguage(code);
            foreach (var label in labels) label.Key.text = Localization.Resolve(label.Value);
            foreach (bool showSettings in new[] { false, true })
            {
                settings.gameObject.SetActive(showSettings);
                foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400), new Vector2Int(1920, 1080) })
                {
                    var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                    Canvas.ForceUpdateCanvases(); root.GetComponent<CanvasScaler>().SendMessage("Handle"); Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, $"{(showSettings ? "Settings" : "Pause")}-{code}-{size.x}x{size.y}.png"), pixels.EncodeToPNG());
                    RenderTexture.active = null; camera.targetTexture = null; target.Release();
                    UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
        }
        Localization.SetLanguage(previousLanguage);
        Debug.Log("Pause menu and settings previews exported.");
    }

    static void ExportConversationPreview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvasRoot = new GameObject("DialogPreview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasRoot.GetComponent<Canvas>();
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StoryDialog.prefab"), canvasRoot.transform);
        var dialog = new SerializedObject(root.GetComponent<StoryDialog>());
        ((TMP_Text)dialog.FindProperty("messageText").objectReferenceValue).gameObject.SetActive(false);
        ((Button)dialog.FindProperty("continueButton").objectReferenceValue).gameObject.SetActive(false);
        ((GameObject)dialog.FindProperty("conversationContent").objectReferenceValue).SetActive(true);
        var registration = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Station01/StationInterior.prefab").GetComponent<StationConversation>());
        var previousLanguage = Localization.CurrentLanguage;
        string output = Environment.GetEnvironmentVariable("PAUSE_PREVIEW_OUTPUT") ?? "Documentation";
        foreach (string code in new[] { "de", "en" })
        {
            Localization.SetLanguage(code);
            foreach (int page in new[] { 3, 5 })
            {
                var step = registration.FindProperty("steps").GetArrayElementAtIndex(page);
                string reaction = registration.FindProperty("steps").GetArrayElementAtIndex(page - 1).FindPropertyRelative("answers").GetArrayElementAtIndex(1).FindPropertyRelative("reaction").stringValue;
                ((TMP_Text)dialog.FindProperty("speakerText").objectReferenceValue).SetLocalizedText("[[game.registration]]:");
                var body = (TMP_Text)dialog.FindProperty("conversationText").objectReferenceValue;
                body.SetLocalizedText(reaction + "\n\n" + step.FindPropertyRelative("message").stringValue);
                for (int i = 0; i < 2; i++)
                {
                    var button = (Button)dialog.FindProperty("answerButtons").GetArrayElementAtIndex(i).objectReferenceValue;
                    button.gameObject.SetActive(true);
                    button.GetComponentInChildren<TMP_Text>(true).SetLocalizedText(step.FindPropertyRelative("answers").GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue);
                }
                var target = new RenderTexture(1080, 1920, 24); camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); scaler.SendMessage("Handle"); Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, $"Registration-{code}-{page}.png"), pixels.EncodeToPNG());
                Debug.Log($"Registration layout: {code}, page {page}, font {body.fontSize}, overflow {body.isTextOverflowing}");
                RenderTexture.active = null; camera.targetTexture = null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        Localization.SetLanguage(previousLanguage);
    }
}

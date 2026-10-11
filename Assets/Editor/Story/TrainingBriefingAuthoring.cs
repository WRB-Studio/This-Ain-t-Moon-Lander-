using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TrainingBriefingAuthoring
{
    public static void Build()
    {
        const string path = "Assets/Prefabs/UI/TrainingBriefing.prefab";
        var root = new GameObject("TrainingBriefing", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 45;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var panel = new GameObject("PilotPanel", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(TrainingBriefingPanel), typeof(StoryLineAudio));
        panel.transform.SetParent(root.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(920, 600);
        panel.GetComponent<Image>().color = new Color(.025f, .025f, .025f, 1);
        var outline = panel.GetComponent<Outline>(); outline.effectColor = Color.white; outline.effectDistance = new Vector2(2, -2);
        var speaker = Text(panel.transform, "Speaker", new Vector2(0, 224), new Vector2(816, 88), 32);
        speaker.SetLocalizedText("[[story.character.training-pilot]]");
        var message = Text(panel.transform, "Message", new Vector2(0, 20), new Vector2(816, 300), 38);
        message.SetLocalizedText("[[story.training.introduction]]");
        var buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ActionButton.prefab"), panel.transform);
        buttonObject.name = "Continue";
        var buttonRect = buttonObject.GetComponent<RectTransform>(); buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
        buttonRect.pivot = new Vector2(.5f, .5f); buttonRect.anchoredPosition = new Vector2(0, -226); buttonRect.sizeDelta = new Vector2(816, 92);
        buttonObject.GetComponentInChildren<TMP_Text>(true).SetLocalizedText("[[game.continue]]");
        var component = new SerializedObject(panel.GetComponent<TrainingBriefingPanel>());
        component.FindProperty("speakerText").objectReferenceValue = speaker;
        component.FindProperty("messageText").objectReferenceValue = message;
        component.FindProperty("continueButton").objectReferenceValue = buttonObject.GetComponent<Button>();
        component.FindProperty("pilot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StoryCharacter>("Assets/Resources/Story/Characters/training-pilot.asset");
        component.FindProperty("audioPlayer").objectReferenceValue = panel.GetComponent<StoryLineAudio>(); component.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(false);
        var asset = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var source = Object.FindFirstObjectByType<StoryTextController>(FindObjectsInactive.Include);
        var existing = Object.FindFirstObjectByType<TrainingBriefingPanel>(FindObjectsInactive.Include);
        if (existing) Object.DestroyImmediate(existing.transform.parent.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        var so = new SerializedObject(source); so.FindProperty("briefingPanel").objectReferenceValue = instance.GetComponentInChildren<TrainingBriefingPanel>(true);
        so.ApplyModifiedPropertiesWithoutUndo();
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath);
        StoryEventAuthoring.Read(project, source, scene.path); StoryTriggerAuthoring.Sync(project);
        var introduction = project.Find("story.training.introduction"); introduction.chapter = project.ResolveChapter("Training");
        introduction.channel = StoryChannel.Radio;
        introduction.title = "Flugtrainer: Einführung und Steuerung";
        introduction.position = new Vector2(340, -360);
        project.Find(introduction.eventTriggerId).position = new Vector2(0, -360);
        EditorUtility.SetDirty(project); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Training briefing prefab, main scene and story introduction authored.");
    }

    public static void RefreshStory()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var source = Object.FindFirstObjectByType<StoryTextController>(FindObjectsInactive.Include);
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath);
        StoryEventAuthoring.Read(project, source, scene.path); StoryTriggerAuthoring.Sync(project);
        var navigation = project.Find("story.training.navigation");
        navigation.title = "Flugtrainer: Navigation und Trainingsstart";
        navigation.position = new Vector2(340, -200);
        project.Find(navigation.eventTriggerId).position = new Vector2(0, -200);
        EditorUtility.SetDirty(project); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Training navigation briefing integrated into story and main scene.");
    }

    static TMP_Text Text(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
        text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    public static void FormatPanelAndMoon()
    {
        const string panelPath = "Assets/Prefabs/UI/TrainingBriefing.prefab";
        var root = PrefabUtility.LoadPrefabContents(panelPath);
        try
        {
            var panel = root.transform.Find("PilotPanel");
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(920, 700);
            var speaker = panel.Find("Speaker").GetComponent<RectTransform>(); speaker.anchoredPosition = new Vector2(0, 270);
            var message = panel.Find("Message").GetComponent<TMP_Text>();
            message.rectTransform.anchoredPosition = Vector2.zero; message.rectTransform.sizeDelta = new Vector2(816, 390);
            message.fontSize = 34;
            message.SetLocalizedText("[[story.training.introduction]]");
            panel.Find("Continue").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -276);
            PrefabUtility.SaveAsPrefabAsset(root, panelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = Object.FindFirstObjectByType<LanderUI>();
        var indicator = ui.indicatorMoon;
        if (!indicator.GetComponent<MoonNavigationGraphic>())
        {
            foreach (Transform child in indicator.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            Object.DestroyImmediate(indicator.GetComponent<Image>());
            var graphic = indicator.gameObject.AddComponent<MoonNavigationGraphic>();
            graphic.color = Color.white; graphic.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAssetAndConnect(indicator.gameObject, "Assets/Prefabs/UI/MoonNavigationIcon.prefab", InteractionMode.AutomatedAction);
        }
        ui.navigationGrp.GetComponentInChildren<Image>(true).enabled = true;
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Formatted training panel and outlined vector moon navigation icon authored.");
    }
}

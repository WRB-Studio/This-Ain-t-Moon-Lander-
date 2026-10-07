using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UIAuthoringChecks
{
    public static void RunAll()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run authoring checks in a separate Unity batch process.");
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = UnityEngine.Object.FindFirstObjectByType<LanderUI>(FindObjectsInactive.Include);
        var story = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<StoryTextController>(FindObjectsInactive.Include));
        var dialogComponent = (StoryDialog)story.FindProperty("dialog").objectReferenceValue;
        Require(dialogComponent && PrefabUtility.IsPartOfPrefabInstance(dialogComponent),
            "Discovery dialogue must use the authored dialog prefab.");
        var dialog = new SerializedObject(dialogComponent);
        Require(dialog.FindProperty("messageText").objectReferenceValue
            && dialog.FindProperty("continueButton").objectReferenceValue, "The dialog prefab must have text and a Continue button.");
        Require(story.FindProperty("gameplayPanelVisibility").objectReferenceValue,
            "The HUD visibility group must be authored in the scene.");
        var dialogueText = (TMPro.TMP_Text)dialog.FindProperty("messageText").objectReferenceValue;
        var transmissions = story.FindProperty("earthTransmissions");
        for (int i = 0; i < transmissions.arraySize; i++)
            Require(dialogueText.GetPreferredValues(transmissions.GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue,
                dialogueText.rectTransform.rect.width, 0f).y <= dialogueText.rectTransform.rect.height,
                "The full transmission must fit above the Continue button.");
        Require(ui.startPanel && ui.landingPanel && ui.crashPanel
            && PrefabUtility.IsPartOfPrefabInstance(ui.startPanel)
            && PrefabUtility.IsPartOfPrefabInstance(ui.landingPanel)
            && PrefabUtility.IsPartOfPrefabInstance(ui.crashPanel), "Start, landing and crash must use separate panel prefabs.");
        Require(ui.landingPanel.continueButton && ui.btnRefill && ui.crashPanel.retryButton,
            "Panel actions must be authored in their prefabs.");
        Require(ui.refillProgressFill && ui.refillProgressFill.transform.IsChildOf(ui.btnRefill.transform)
            && ui.refillProgressFill.type == Image.Type.Simple && !ui.refillProgressFill.sprite,
            "Refill progress must be authored inside the button prefab.");
        var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
            Require(PrefabUtility.IsPartOfPrefabInstance(button), "Button must use a prefab: " + button.name);
        var chooser = UnityEngine.Object.FindFirstObjectByType<LanderChooserManager>(FindObjectsInactive.Include);
        Require(chooser.optionButtons.Length == chooser.landerPrefabs.Length, "Every ship must have an authored chooser button.");
        foreach (var button in chooser.optionButtons) Require(button, "Missing chooser button reference.");
        var game = UnityEngine.Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        Require(game.landingPadPrefab && PrefabUtility.IsPartOfPrefabAsset(game.landingPadPrefab), "Landing pads must use a prefab.");
        var audio = UnityEngine.Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        Require(audio.sourcePrefab && audio.musicSource && audio.refillSource && audio.refillSource.clip && audio.refillSource.loop,
            "Audio sources and the refill loop must be authored as prefab and scene references.");
        SessionState.SetInt("AuthoredButtonCount", buttons.Length);
        Debug.Log("UI authoring checks passed: " + buttons.Length + " prefab buttons and all scene references.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

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
        foreach (string portrait in new[] { "neutralPortrait", "annoyedPortrait", "angryPortrait", "surprisedPortrait" })
            Require(dialog.FindProperty(portrait).objectReferenceValue
                && PrefabUtility.IsPartOfPrefabInstance(dialog.FindProperty(portrait).objectReferenceValue),
                "Operator portraits must use the shared prefab.");
        var dialogueText = (TMPro.TMP_Text)dialog.FindProperty("messageText").objectReferenceValue;
        var transmissions = story.FindProperty("earthTransmissions");
        for (int i = 0; i < transmissions.arraySize; i++)
            Require(dialogueText.GetPreferredValues(transmissions.GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue,
                dialogueText.rectTransform.rect.width, 0f).y <= dialogueText.rectTransform.rect.height,
                "The full transmission must fit above the Continue button.");
        Require(ui.btnContinue && ui.btnRefill && ui.flightActions, "Flight actions must be authored in the scene.");
        Require(ui.refillProgressFill && ui.refillProgressFill.transform.IsChildOf(ui.btnRefill.transform)
            && ui.refillProgressFill.type == Image.Type.Simple && !ui.refillProgressFill.sprite,
            "Refill progress must be authored inside the button prefab.");
        Require(ui.panelTopPosition && ui.panelCenterPosition && ui.panelBottomPosition, "Panel positions must be editable in the scene.");
        foreach (var position in new[] { ui.panelTopPosition, ui.panelCenterPosition, ui.panelBottomPosition })
        {
            if (position == ui.panelTopPosition) ui.SetPanelTopCenter();
            else if (position == ui.panelCenterPosition) ui.SetPanelCenter();
            else ui.SetPanelBottomCenter();
            Canvas.ForceUpdateCanvases();
            Require(Vector3.Distance(ui.panelGrp.position, position.position) < 0.01f,
                "Panel position must match its editor marker: " + position.name);
        }
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

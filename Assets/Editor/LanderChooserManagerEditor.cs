using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(LanderChooserManager))]
public class LanderChooserManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var chooser = (LanderChooserManager)target;
        using (new EditorGUI.DisabledScope(Application.isPlaying || !chooser.btnOptionPrefab || !chooser.optionsParent))
            if (GUILayout.Button("Rebuild Chooser Buttons")) RebuildButtons(chooser);
    }

    public static void RebuildButtons(LanderChooserManager chooser)
    {
        Undo.RecordObject(chooser, "Rebuild chooser buttons");
        if (chooser.optionButtons != null)
            foreach (var button in chooser.optionButtons)
                if (button) Undo.DestroyObjectImmediate(button.gameObject);
        chooser.optionButtons = new Button[chooser.landerPrefabs.Length];
        for (int i = 0; i < chooser.landerPrefabs.Length; i++)
        {
            var config = chooser.landerPrefabs[i].GetComponent<LanderController>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(chooser.btnOptionPrefab.gameObject, chooser.optionsParent);
            Undo.RegisterCreatedObjectUndo(instance, "Create chooser button");
            instance.name = "LanderOption " + config.landerIndex;
            instance.transform.Find("ImgLander").GetComponent<Image>().sprite = chooser.landerPrefabs[i].GetComponent<SpriteRenderer>().sprite;
            instance.transform.Find("imgSecret").gameObject.SetActive(config.isSecretLander);
            instance.GetComponentInChildren<TMP_Text>(true).text = config.unlockCost.ToString();
            chooser.optionButtons[i] = instance.GetComponent<Button>();
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        EditorUtility.SetDirty(chooser);
        EditorSceneManager.MarkSceneDirty(chooser.gameObject.scene);
    }
}

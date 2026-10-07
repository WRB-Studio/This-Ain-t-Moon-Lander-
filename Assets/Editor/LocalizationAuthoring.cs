using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LocalizationAuthoring
{
    [Serializable] public class SourceList { public string[] texts; }
    static readonly HashSet<string> fields = new()
    {
        "m_text", "text", "message", "reaction", "speaker", "repeatMessage", "goodbyeAnswer",
        "buttonLabel", "sender", "title", "body", "zeroGAfterDepartureMessage", "earthReturnMessage",
        "moonKnownEarthMessage", "starterBlockedMessage"
    };

    public static void ExportSources()
    {
        var texts = new HashSet<string>();
        VisitAssets(component =>
        {
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (IsText(property) && !string.IsNullOrWhiteSpace(property.stringValue)) texts.Add(property.stringValue);
        }, false);
        File.WriteAllText(Environment.GetEnvironmentVariable("LOCALIZATION_EXPORT") ?? "LocalizationSources.json", JsonUtility.ToJson(new SourceList { texts = texts.OrderBy(t => t).ToArray() }, true));
        Debug.Log("Localization source export: " + texts.Count);
    }

    static bool IsText(SerializedProperty property) => property.propertyType == SerializedPropertyType.String
        && (fields.Contains(property.name) || property.propertyPath.Contains("comments.Array.data[")
            || property.propertyPath.Contains("messages.Array.data[") || property.propertyPath.Contains("earthAsides.Array.data["));

    [MenuItem("Tools/Localization/Apply Catalog to Game")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        VisitAssets(component =>
        {
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (IsText(property)) property.stringValue = Localization.Reference(property.stringValue);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (component is TMP_Text text && !text.GetComponentInParent<TMP_Dropdown>(true))
            {
                var presenters = text.GetComponents<LocalizedText>();
                var localized = presenters.FirstOrDefault(item => !PrefabUtility.IsPartOfPrefabInstance(item)
                    || !PrefabUtility.IsAddedComponentOverride(item)) ?? presenters.FirstOrDefault();
                if (!localized) localized = text.gameObject.AddComponent<LocalizedText>();
                string source = presenters.Select(item => item.Source).FirstOrDefault(value => value.Contains("[[")
                    && Localization.Resolve(value) == text.text) ?? (text.text.Contains("[[") || !localized.Source.Contains("[[") ? text.text : localized.Source);
                foreach (var duplicate in presenters.Where(item => item != localized)) UnityEngine.Object.DestroyImmediate(duplicate, true);
                localized.SetSource(Localization.Reference(source));
                EditorUtility.SetDirty(localized);
                if (text.name == "ConversationText") text.fontSizeMin = 22f;
                if (text.name == "Speaker" && text.GetComponentInParent<StoryDialog>())
                {
                    text.rectTransform.anchoredPosition = new Vector2(0, 285);
                    text.rectTransform.sizeDelta = new Vector2(900, 65);
                }
            }
        }, true);
        AssetDatabase.SaveAssets();
        Debug.Log("Localization applied to game prefabs and scenes.");
    }

    static void VisitAssets(Action<Component> visit, bool save)
    {
        // Author shared prefabs before their instances so variants inherit one presenter per label.
        foreach (var path in Directory.GetFiles("Assets/Prefabs", "*.prefab", SearchOption.AllDirectories)
            .OrderBy(path => AssetDatabase.GetDependencies(path.Replace('\\', '/'), true).Length))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool visited = false;
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component is TMP_Text || component is StationConversation || component is StationResident
                        || component is StationInteraction || component is RadioController)
                    {
                        visit(component);
                        visited = true;
                    }
                if (save && visited) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component is TMP_Text || component is StoryTextController || component is LanderChooserManager
                        || component is StationResident || component is StationInteraction || component is StationConversation) visit(component);
            if (save) EditorSceneManager.SaveScene(scene);
        }
    }
}

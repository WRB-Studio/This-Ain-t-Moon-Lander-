using System.Linq;
using UnityEditor;

// Planning text is stored in the planning asset, never in the game's localisation files.
public static class StoryPlanningCatalog
{
    public const string ProjectPath = "Assets/StoryPlanning/StoryPlan.asset";
    public const string CharactersPath = "Assets/StoryPlanning/Characters";
    static StoryProject Project => AssetDatabase.LoadAssetAtPath<StoryProject>(ProjectPath);
    public static string Text(string key, string language)
    {
        var entry = Project ? Project.planningTexts.Find(t => t.key == key) : null;
        return entry == null ? "" : language == "de" ? entry.de : entry.en;
    }
    public static void Write(string key, string language, string text)
    {
        var project = Project;
        if (!project || string.IsNullOrWhiteSpace(key)) return;
        Undo.RecordObject(project, "Planungstext " + language);
        var entry = project.planningTexts.Find(t => t.key == key);
        if (entry == null) { entry = new StoryPlanningText { key = key }; project.planningTexts.Add(entry); }
        if (language == "de") entry.de = text; else entry.en = text;
        EditorUtility.SetDirty(project);
    }
    public static Localization.Language Load(string language) => new Localization.Language
    {
        code = language,
        entries = Project ? Project.planningTexts.Select(t => new Localization.Entry { key = t.key, text = language == "de" ? t.de : t.en }).ToArray() : new Localization.Entry[0]
    };
    public static bool TooLong(string text) => StoryCatalog.TooLong(text);
}

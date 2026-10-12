using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class PlanningCharacterDropdown : PopupWindowContent
{
    readonly List<(StoryCharacter character, string name)> characters;
    readonly Action<StoryCharacter> choose;
    readonly float width;
    readonly StoryCharacter current;
    string search = "";
    Vector2 scroll;
    int highlighted;
    bool focusSearch = true;

    public PlanningCharacterDropdown(float width, string language, StoryCharacter current, Action<StoryCharacter> choose)
    {
        this.width = Mathf.Max(260, width); this.choose = choose; this.current = current;
        characters = AssetDatabase.FindAssets("t:StoryCharacter", new[] { StoryPlanningCatalog.CharactersPath })
            .Select(g => AssetDatabase.LoadAssetAtPath<StoryCharacter>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c).Select(c => (character: c, name: Name(c, language))).OrderBy(c => c.name).ToList();
        highlighted = Mathf.Max(0, characters.FindIndex(c => c.character == current) + 1);
        scroll.y = Mathf.Max(0, (highlighted - 7) * 24);
    }

    public static string Name(StoryCharacter character, string language)
    {
        if (!character) return "Kein Charakter";
        string name = StoryPlanningCatalog.Text(character.nameKey, language);
        if (string.IsNullOrWhiteSpace(name)) name = StoryPlanningCatalog.Text(character.nameKey, language == "de" ? "en" : "de");
        return string.IsNullOrWhiteSpace(name) ? "Unbenannter Charakter" : name;
    }

    public override Vector2 GetWindowSize() => new(width, 40 + Mathf.Min(240, (characters.Count + 1) * 24));

    public override void OnGUI(Rect rect)
    {
        var options = characters.Where(c => string.IsNullOrWhiteSpace(search)
            || c.name.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        options.Insert(0, (null, "Kein Charakter"));
        highlighted = Mathf.Clamp(highlighted, 0, options.Count - 1);
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape) { editorWindow.Close(); e.Use(); return; }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            { Select(options[highlighted].character); e.Use(); return; }
            if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.UpArrow)
            {
                highlighted = Mathf.Clamp(highlighted + (e.keyCode == KeyCode.DownArrow ? 1 : -1), 0, options.Count - 1);
                scroll.y = Mathf.Max(0, (highlighted - 7) * 24); e.Use(); editorWindow.Repaint();
            }
        }
        using (new GUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(6, 6, 6, 6) }))
        {
            GUI.SetNextControlName("PlanningCharacterSearch");
            string next = EditorGUILayout.TextField(search);
            if (focusSearch && e.type == EventType.Repaint) { EditorGUI.FocusTextInControl("PlanningCharacterSearch"); focusSearch = false; }
            if (next != search)
            {
                search = next; highlighted = 1; scroll = Vector2.zero; editorWindow.Repaint();
                // Render the new result list on the next layout pass.
                GUIUtility.ExitGUI();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 0, 0) };
            for (int i = 0; i < options.Count; i++)
            {
                Rect row = GUILayoutUtility.GetRect(new GUIContent(options[i].name), style, GUILayout.Height(24), GUILayout.ExpandWidth(true));
                if (i == highlighted) EditorGUI.DrawRect(row, new Color(.2f, .4f, .6f, .65f));
                string label = options[i].name + (options[i].character == current ? "  ✓" : "");
                if (GUI.Button(row, label, style)) { Select(options[i].character); GUIUtility.ExitGUI(); }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    void Select(StoryCharacter character) { choose(character); editorWindow.Close(); }
}

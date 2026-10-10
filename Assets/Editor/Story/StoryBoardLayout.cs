using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class StoryBoardLayout
{
    public static void ArrangeConversations()
    {
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath); if (!project) return;
        Undo.RegisterCompleteObjectUndo(project, "Gespräche anordnen");
        foreach (string chapter in project.dialogueGroups) Arrange(project, chapter);
        EditorUtility.SetDirty(project); AssetDatabase.SaveAssets();
        Debug.Log("Conversation boards arranged: shared triggers left, alternatives right, dialogue answers between texts. No dialogue links changed.");
    }
    public static void Arrange(StoryProject project, string chapter)
    {
        var nodes = project.nodes.Where(n => !n.overviewOnly && n.chapter == chapter).ToList();
        var placed = new HashSet<string>(); float y = 0;
        foreach (var trigger in nodes.Where(n => n.triggerOnly).OrderBy(n => n.eventInfo.kind == StoryEventKind.LevelStart || n.eventInfo.kind == StoryEventKind.LevelTransmission || n.eventInfo.kind == StoryEventKind.LateTraining ? 0 : 1)
            .ThenBy(n => n.eventInfo.kind == StoryEventKind.LevelStart || n.eventInfo.kind == StoryEventKind.LevelTransmission || n.eventInfo.kind == StoryEventKind.LateTraining ? n.eventInfo.fromLevel : (int)n.eventInfo.kind)
            .ThenBy(n => n.sourceProperty))
        {
            var texts = nodes.Where(n => !n.triggerOnly && n.eventTriggerId == trigger.id).OrderBy(n => Natural(n.sourceProperty)).ToList();
            int columns = texts.Count > 4 ? 3 : 1, rows = Mathf.Max(1, Mathf.CeilToInt(texts.Count / (float)columns));
            trigger.position = new Vector2(0, y + (rows - 1) * 80); placed.Add(trigger.id);
            for (int i = 0; i < texts.Count; i++) { texts[i].position = new Vector2(400 + i % columns * 300, y + i / columns * 160); placed.Add(texts[i].id); }
            y += rows * 160 + 120;
        }
        if (placed.Count > 0) y += 160;
        var dialogue = nodes.Where(n => !placed.Contains(n.id)).ToList();
        var incoming = dialogue.SelectMany(n => n.choices).Select(c => c.next).ToHashSet();
        var queue = new Queue<(StoryNode node, int depth)>();
        var seen = new HashSet<string>(); var columnY = new Dictionary<int, float>();
        foreach (var root in dialogue.Where(n => !incoming.Contains(n.id))) queue.Enqueue((root, 0));
        foreach (var remaining in dialogue)
        {
            if (queue.Count == 0 && !seen.Contains(remaining.id)) queue.Enqueue((remaining, 0));
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue(); if (!seen.Add(node.id)) continue;
                float row = columnY.TryGetValue(depth, out float value) ? value : y;
                node.position = new Vector2(depth * 760, row);
                columnY[depth] = row + Mathf.Max(180, node.choices.Count * 120) + 100;
                for (int a = 0; a < node.choices.Count; a++) { node.choices[a].positioned = true; node.choices[a].position = new Vector2(330, a * 120); }
                foreach (var choice in node.choices)
                { var next = dialogue.Find(n => n.id == choice.next); if (next != null && !seen.Contains(next.id)) queue.Enqueue((next, depth + 1)); }
            }
        }
    }
    static string Natural(string value) => System.Text.RegularExpressions.Regex.Replace(value ?? "", @"\d+", m => int.Parse(m.Value).ToString("D6"));
}

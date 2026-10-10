using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoryTriggerAuthoring
{
    public static void Sync(StoryProject project)
    {
        project.eventTriggersManaged = true;
        foreach (var group in project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly && n.eventInfo.enabled && !n.eventBindingEdited).ToList().GroupBy(n => ((int)n.eventInfo.kind) + "." + n.eventInfo.pool))
        {
            var first = group.First(); string id = "trigger." + group.Key;
            var trigger = project.Find(id);
            if (trigger == null)
            {
                trigger = new StoryNode { id = id, title = "Auslöser", chapter = first.chapter, triggerOnly = true, answerBoardLayout = true,
                    position = first.eventPositioned ? first.eventPosition : first.position + Vector2.left * 310,
                    status = StoryDevelopmentStatus.Implemented, sourcePath = first.sourcePath, sourceProperty = first.sourceProperty };
                project.nodes.Add(trigger);
            }
            trigger.eventInfo = JsonUtility.FromJson<StoryEventInfo>(JsonUtility.ToJson(first.eventInfo));
            foreach (var node in group) node.eventTriggerId = id;
        }
        EditorUtility.SetDirty(project);
    }
    public static void Upgrade()
    {
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath);
        if (project) { Sync(project); AssetDatabase.SaveAssets(); }
    }
}

using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Editor entry point and migration of already stored organisation; no source import.
public static class StoryEditorData
{
    public const string ProjectPath = "Assets/Resources/Story/StoryProject.asset";
    [MenuItem("Tools/Story/Story-Editor")]
    public static void Open() => StoryEditorWindow.Open();

    public static void MigrateLegacyOrganisation(StoryProject project)
    {
        bool initial = project.dialogueGroups.Count == 0;
        project.SeparateSections();
        foreach (var mapping in new[] { ("pilot-trail", "overview.11"), ("spacecraft-reveal", "overview.12"), ("home", "overview.13") })
        {
            var old = project.Find(mapping.Item1); var target = project.Find(mapping.Item2);
            if (old == null || target == null) continue;
            if (!string.IsNullOrEmpty(old.purpose) && !(target.purpose ?? "").Contains(old.purpose)) target.purpose += "\n\n" + old.purpose;
            if (!string.IsNullOrEmpty(old.questions)) target.questions += "\n" + old.questions;
            foreach (var n in project.nodes)
            {
                if (n.storyStepId == old.id) n.storyStepId = target.id;
                foreach (var choice in n.choices) if (choice.next == old.id) choice.next = target.id;
            }
            project.nodes.Remove(old);
        }
        string future = project.ResolveChapter("Spätere Geschichte");
        string mainChapter = project.Find("overview.5")?.chapter ?? project.chapters.FirstOrDefault();
        if (!string.IsNullOrEmpty(mainChapter) && mainChapter != future)
        {
            foreach (var n in project.nodes.Where(n => n.overviewOnly && n.chapter == future)) n.chapter = mainChapter;
            if (!project.nodes.Any(n => n.overviewOnly && n.chapter == future)) project.chapters.Remove(future);
        }
        foreach (var n in project.nodes.Where(n => !n.overviewOnly && !n.answerBoardLayout))
        { n.position = new Vector2(n.position.x * 2.6f, n.position.y * 2.25f); n.answerBoardLayout = true; }
        foreach (var node in project.nodes.Where(n => !n.overviewOnly && (initial || string.IsNullOrEmpty(n.storyStepId))))
        {
            string key = node.textKey ?? "";
            if (key.StartsWith("cargo.dialog.release")) node.storyStepId = "overview.9";
            else if (key.StartsWith("cargo.dialog.discovery.4") || key.StartsWith("cargo.dialog.discovery.5") || key.StartsWith("cargo.radio.tow") || key.StartsWith("cargo.radio.failed")) node.storyStepId = "overview.8";
            else if (key.StartsWith("cargo.radio.delivered")) node.storyStepId = "overview.9";
            else if (key.StartsWith("cargo.radio.complete")) node.storyStepId = "overview.10";
            else if (key.StartsWith("cargo.") || node.sourceProperty == "pickupMessage") node.storyStepId = "overview.7";
            else if (node.chapter == project.ResolveChapter("Registrierung")) node.storyStepId = "overview.6";
            else if (node.chapter == project.ResolveChapter("Stationsbewohner")) node.storyStepId = "overview.5";
            else if (node.chapter == project.ResolveChapter("Training und Entdeckungen"))
            {
                var match = Regex.Match(node.sourceProperty ?? "", @"discoveryMessages.Array.data\[(\d+)\]");
                int index = match.Success ? int.Parse(match.Groups[1].Value) : -1;
                int step = index switch { 0 => 1, 1 or 2 => 2, 3 => 3, 4 => 4, 5 or 6 or 7 => 5, _ => 0 };
                node.storyStepId = "overview." + step;
            }
        }
        SplitDiscoveryGroups(project);
        StoryTriggerAuthoring.Sync(project);
        EditorUtility.SetDirty(project);
    }
    static void SplitDiscoveryGroups(StoryProject project)
    {
        string old = project.ResolveChapter("Training und Entdeckungen");
        foreach (var node in project.nodes.Where(n => !n.overviewOnly && n.chapter == old))
        {
            if (node.textKey == "game.back.to.the.moon.you.know.more.about.it.than.i.do"
                || node.textKey == "game.i.wonder.what.else.is.up.there"
                || node.textKey == "game.still.there.this.wasn.t.in.my.little.landing.game") node.storyStepId = "overview.2";
            string group = node.storyStepId == "overview.5" ? "Funk"
                : node.storyStepId == "overview.2" || node.storyStepId == "overview.3" || node.storyStepId == "overview.4" ? "Mond" : "Training";
            node.chapter = project.ResolveChapter(group);
            if (!project.dialogueGroups.Contains(node.chapter)) project.dialogueGroups.Add(node.chapter);
        }
        if (!project.nodes.Any(n => !n.overviewOnly && n.chapter == old)) project.dialogueGroups.Remove(old);
    }
}

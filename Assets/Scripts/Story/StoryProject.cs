using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum StoryChannel { Personal, Radio, RadioPortrait }
public enum StoryDevelopmentStatus { Idea, Planned, Implemented, Verified }
[Serializable] public class StoryChapterAlias
{
    public string source, name;
}
[Serializable] public class StoryFlag
{
    public string key;
    public bool value = true;
}
[Serializable] public class StoryChoice
{
    public string textKey;
    public string reactionKey;
    public string next;
    public List<StoryFlag> conditions = new();
    public List<StoryFlag> effects = new();
    [Tooltip("Bestehenden Missionsablauf mit der ursprünglichen Antwort fortsetzen. Ziel bleibt für die Übersicht/Vorschau sichtbar.")]
    public bool continueLegacy;
    public int legacyAnswer;
    public bool positioned;
    public Vector2 position;
}
[Serializable] public class StoryNode
{
    public string id;
    public string chapter;
    public string storyStepId;
    public string title;
    public Vector2 position;
    public StoryDevelopmentStatus status;
    [TextArea] public string purpose;
    [TextArea] public string questions;
    public StoryChannel channel;
    public StoryCharacter speaker;
    public string textKey;
    public string sourceReference;
    public string sourcePath;
    public string sourceProperty;
    public int legacyAnswerCount;
    public string location;
    public UnityEngine.Object relatedAsset;
    public AudioClip voiceDE, voiceEN, sound;
    public List<StoryFlag> conditions = new();
    public List<StoryFlag> onEnter = new();
    public List<StoryChoice> choices = new();
    public bool overviewOnly;
    public bool answerBoardLayout;
    public StoryEventInfo eventInfo = new();
    public bool eventPositioned;
    public Vector2 eventPosition;
    public bool triggerOnly, eventBindingEdited;
    public string eventTriggerId;
}
[CreateAssetMenu(menuName = "Story/Projekt")]
public class StoryProject : ScriptableObject
{
    public List<string> chapters = new();
    public List<string> dialogueGroups = new();
    public List<StoryChapterAlias> chapterAliases = new();
    public List<StoryNode> nodes = new();
    public List<StoryBoardDecoration> decorations = new();
    public bool eventTriggersManaged;
    public bool snapToGrid;
    public float gridSize = 20;
    public StoryEventInfo EventInfo(StoryNode node) => Find(node.eventTriggerId)?.eventInfo ?? node.eventInfo;
    public string[] EventTexts(string pool, string[] fallback)
    {
        var trigger = nodes.Find(n => n.triggerOnly && n.eventInfo.pool == pool);
        return trigger == null ? eventTriggersManaged ? Array.Empty<string>() : fallback : TriggerTexts(trigger.id, StoryLibrary.Flag);
    }
    public string[] TriggerTexts(string triggerId, Func<string, bool> flags = null) => nodes.Where(n => !n.triggerOnly && n.eventTriggerId == triggerId && !string.IsNullOrEmpty(n.textKey)
        && (flags == null || StorySession.All(n.conditions, flags))).Select(n => "[[" + n.textKey + "]]").ToArray();
    public string[] TrainingTexts(int level, string[] fallback, Func<string, bool> flags)
    {
        var triggers = nodes.Where(n => n.triggerOnly && n.eventInfo.kind == StoryEventKind.LevelStart).ToList();
        if (triggers.Count == 0) return eventTriggersManaged ? Array.Empty<string>() : fallback;
        var chosen = triggers.Where(n => n.eventInfo.fromLevel <= level && (n.eventInfo.toLevel == 0 || n.eventInfo.toLevel >= level)
            && (string.IsNullOrEmpty(n.eventInfo.requiredFlag) || flags(n.eventInfo.requiredFlag))
            && (string.IsNullOrEmpty(n.eventInfo.blockedFlag) || !flags(n.eventInfo.blockedFlag))).OrderBy(n => n.eventInfo.fromLevel).LastOrDefault();
        if (chosen == null) return Array.Empty<string>();
        var texts = TriggerTexts(chosen.id, flags); return chosen.eventInfo.random ? texts : texts.Take(1).ToArray();
    }
    public string ResolveChapter(string source) => chapterAliases.Find(alias => alias.source == source)?.name ?? source;
    public void SeparateSections()
    {
        foreach (string group in chapters.ToArray())
        {
            bool hasDialogue = nodes.Any(n => !n.overviewOnly && n.chapter == group);
            bool hasStory = nodes.Any(n => n.overviewOnly && n.chapter == group);
            if (hasDialogue && !dialogueGroups.Contains(group)) dialogueGroups.Add(group);
            if (hasDialogue && !hasStory) chapters.Remove(group);
        }
    }
    public StoryNode Find(string id) => nodes.Find(n => n.id == id);
    public StoryNode Match(string message, Func<string, bool> flags)
    {
        if (string.IsNullOrEmpty(message)) return null;
        return nodes.LastOrDefault(n => !n.overviewOnly && !n.triggerOnly
            && ((!string.IsNullOrEmpty(n.sourceReference) && message.EndsWith(n.sourceReference, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(n.textKey) && message.EndsWith("[[" + n.textKey + "]]", StringComparison.Ordinal))) && StorySession.All(n.conditions, flags));
    }
}

// The preview and game use the same branching rules; only their flag stores differ.
public class StorySession
{
    readonly StoryProject project;
    readonly Func<string, bool> read;
    readonly Action<string, bool> write;
    public StoryNode Node { get; private set; }
    public int OriginalAnswer { get; private set; } = -1;
    public string Reaction { get; private set; }
    public StorySession(StoryProject project, Func<string, bool> read, Action<string, bool> write)
    { this.project = project; this.read = read; this.write = write; }
    public static bool All(IEnumerable<StoryFlag> flags, Func<string, bool> read) => flags.All(f => !string.IsNullOrEmpty(f.key) && read(f.key) == f.value);
    public List<StoryChoice> Choices => Node == null ? new() : Node.choices.Count == 0
        ? new() { new StoryChoice { textKey = "game.continue" } }
        : Node.choices.Where(c => All(c.conditions, read)).ToList();
    void Apply(IEnumerable<StoryFlag> effects) { foreach (var f in effects) if (!string.IsNullOrEmpty(f.key)) write(f.key, f.value); }
    public bool Begin(StoryNode node)
    {
        if (node == null || !All(node.conditions, read)) return false;
        Node = node; Reaction = null; OriginalAnswer = -1; Apply(node.onEnter); return true;
    }
    public bool Choose(int index, bool legacy)
    {
        var options = Choices;
        if (index < 0 || index >= options.Count) return false;
        var choice = options[index];
        var next = string.IsNullOrEmpty(choice.next) ? null : project.Find(choice.next);
        if (!(legacy && choice.continueLegacy) && !string.IsNullOrEmpty(choice.next)
            && (next == null || !All(next.conditions, read))) return false;
        if (OriginalAnswer < 0) OriginalAnswer = choice.legacyAnswer;
        Apply(choice.effects); Reaction = choice.reactionKey;
        Node = legacy && choice.continueLegacy ? null : next;
        if (Node != null) Apply(Node.onEnter);
        return true;
    }
}

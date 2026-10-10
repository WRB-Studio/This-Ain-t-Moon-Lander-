using System;
using System.Collections.Generic;
using System.Linq;

public sealed class StoryEventSimulator
{
    readonly Dictionary<string, bool> flags;
    readonly Random random = new();
    readonly HashSet<int> lateLevels = new();
    bool flightComment;
    string lastAside;
    public StoryEventSimulator(Dictionary<string, bool> flags) => this.flags = flags;
    bool Flag(string key) => !string.IsNullOrEmpty(key) && flags.TryGetValue(key, out bool value) && value;
    public static string Signal(StoryEventInfo info) => info.kind == StoryEventKind.LevelStart || info.kind == StoryEventKind.LevelTransmission || info.kind == StoryEventKind.KnownMoon || info.kind == StoryEventKind.LateTraining
        ? "Trainingsstart" : info.kind == StoryEventKind.Discovery || info.kind == StoryEventKind.ZeroGAfterTraining ? "Entdeckung: " + info.eventId
        : info.kind == StoryEventKind.FlightState ? "Flugzustand: " + info.eventId : "Rückkehr: " + info.eventId;
    public List<StoryNode> Candidates(StoryProject project, string signal, int level)
    {
        var nodes = project.nodes.Where(n => !n.triggerOnly && project.EventInfo(n).enabled && Signal(project.EventInfo(n)) == signal).ToList();
        if (signal == "Trainingsstart")
        {
            StoryEventKind kind = level >= 10 ? StoryEventKind.LateTraining
                : project.nodes.Any(n => n.triggerOnly && n.eventInfo.kind == StoryEventKind.LevelTransmission && n.eventInfo.fromLevel == level) ? StoryEventKind.LevelTransmission
                : Flag("discovery.Moon") ? StoryEventKind.KnownMoon : StoryEventKind.LevelStart;
            nodes = nodes.Where(n => project.EventInfo(n).kind == kind).ToList();
            if (kind == StoryEventKind.LevelStart)
            {
                var trigger = project.nodes.Where(n => n.triggerOnly && n.eventInfo.kind == kind && n.eventInfo.fromLevel <= level
                    && (n.eventInfo.toLevel == 0 || n.eventInfo.toLevel >= level)
                    && (string.IsNullOrEmpty(n.eventInfo.requiredFlag) || Flag(n.eventInfo.requiredFlag))
                    && !Flag(n.eventInfo.blockedFlag)).OrderBy(n => n.eventInfo.fromLevel).LastOrDefault();
                if (trigger != null) nodes = nodes.Where(n => n.eventTriggerId == trigger.id).ToList();
            }
        }
        if (signal == "Entdeckung: ZeroG") nodes = nodes.Where(n => project.EventInfo(n).kind == (Flag("story.earth.9") ? StoryEventKind.ZeroGAfterTraining : StoryEventKind.Discovery)).ToList();
        return nodes.Where(n => StorySession.All(n.conditions, key => Flag(key))
            && (!string.IsNullOrEmpty(project.EventInfo(n).requiredFlag) ? Flag(project.EventInfo(n).requiredFlag) : true)
            && !Flag(project.EventInfo(n).blockedFlag)
            && (project.EventInfo(n).kind != StoryEventKind.LevelTransmission || !Flag("discovery.ZeroG"))
            && (project.EventInfo(n).kind != StoryEventKind.LevelStart && project.EventInfo(n).kind != StoryEventKind.LevelTransmission && project.EventInfo(n).kind != StoryEventKind.LateTraining && project.EventInfo(n).kind != StoryEventKind.FlightState
                || level >= project.EventInfo(n).fromLevel && (project.EventInfo(n).toLevel == 0 || level <= project.EventInfo(n).toLevel))).ToList();
    }
    public (StoryNode node, string reason) Fire(StoryProject project, string signal, int level)
    {
        if (signal == "Trainingsstart") flightComment = false;
        if (signal == "Rückkehr: earth-return") flags["story.companyLanderReturned"] = true;
        var nodes = Candidates(project, signal, level);
        if (nodes.Count == 0) return (null, "Keine Meldung: Voraussetzungen fehlen oder bereits bestätigt.");
        var info = project.EventInfo(nodes[0]);
        if (info.kind == StoryEventKind.LateTraining && !lateLevels.Add(level)) return (null, "Für dieses Level wurde bereits ein Zufallsversuch ausgeführt.");
        if (info.kind == StoryEventKind.FlightState && flightComment) return (null, "In diesem Flugversuch wurde bereits ein Flugkommentar angezeigt.");
        if (random.NextDouble() >= info.probability) return (null, "Keine Meldung: Zufallschance " + info.probability.ToString("P0") + " hat nicht gegriffen.");
        if (info.kind == StoryEventKind.LateTraining && nodes.Count > 1) nodes.RemoveAll(n => n.id == lastAside);
        var chosen = nodes[info.random ? random.Next(nodes.Count) : 0];
        if (project.EventInfo(chosen).kind == StoryEventKind.Discovery || project.EventInfo(chosen).kind == StoryEventKind.ZeroGAfterTraining) flags["discovery." + project.EventInfo(chosen).eventId] = true;
        if (info.kind == StoryEventKind.LateTraining) lastAside = chosen.id;
        if (info.kind == StoryEventKind.FlightState) flightComment = true;
        return (chosen, "Meldung ausgewählt: " + chosen.title);
    }
    public void Confirm(StoryProject project, StoryNode node)
    {
        if (project.EventInfo(node).once && !string.IsNullOrEmpty(project.EventInfo(node).blockedFlag)) flags[project.EventInfo(node).blockedFlag] = true;
    }
    public void Reset() { lateLevels.Clear(); lastAside = null; flightComment = false; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

// Adds writing-only organisation. Existing node IDs, branches and runtime metadata are untouched.
public static class StoryWritingMigration
{
    public static bool Sync(StoryProject project)
    {
        bool changed = UpgradePlanningCards(project);
        if (AssetDatabase.GetAssetPath(project) == StoryPlanningCatalog.ProjectPath)
            foreach (var node in project.nodes.Where(n => !n.triggerOnly && !n.planningAnswer))
            {
                if (string.IsNullOrEmpty(node.planningTitleKey)) { node.planningTitleKey = node.id + ".title"; changed = true; }
                if (!project.planningTexts.Any(t => t.key == node.planningTitleKey))
                { project.planningTexts.Add(new StoryPlanningText { key = node.planningTitleKey, de = node.title ?? "", en = "" }); changed = true; }
            }
        var texts = project.nodes.Where(n => !n.overviewOnly && !n.triggerOnly && !n.planningAnswer).ToList();
        var pending = texts.Where(n => !n.planningScopeAssigned && !project.conversations.Any(c => c.id == n.conversationId)).ToList();
        while (pending.Count > 0)
        {
            var first = pending[0];
            var members = new List<StoryNode> { first };
            for (int i = 0; i < members.Count; i++)
                foreach (var candidate in pending.Where(n => !members.Contains(n)).ToList())
                    if (members[i].choices.Any(c => c.next == candidate.id) || candidate.choices.Any(c => c.next == members[i].id)
                        || (!string.IsNullOrEmpty(first.eventTriggerId) && candidate.eventTriggerId == first.eventTriggerId)) members.Add(candidate);
            var entry = members.FirstOrDefault(n => !members.Any(other => other.choices.Any(c => c.next == n.id))) ?? first;
            var existing = project.conversations.FirstOrDefault(c => texts.Any(n => n.conversationId == c.id
                && (members.Any(m => m.choices.Any(a => a.next == n.id) || n.choices.Any(a => a.next == m.id))
                    || (!string.IsNullOrEmpty(first.eventTriggerId) && n.eventTriggerId == first.eventTriggerId))));
            if (existing == null)
            {
                var mission = project.nodes.Find(n => n.overviewOnly && n.id == entry.storyStepId)
                    ?? members.Select(n => project.Find(n.storyStepId)).FirstOrDefault(n => n != null && n.overviewOnly);
                if (mission == null)
                {
                    string chapter = string.IsNullOrEmpty(first.chapter) ? "Weitere Geschichte" : first.chapter;
                    if (!project.chapters.Contains(chapter)) project.chapters.Add(chapter);
                    mission = project.nodes.Find(n => n.overviewOnly && n.id == "writing.imported." + chapter);
                    if (mission == null)
                    {
                        mission = new StoryNode { id = "writing.imported." + chapter, chapter = chapter, title = "Weitere Gespräche",
                            overviewOnly = true, purpose = "Aus der bisherigen Gesprächsgruppe übernommen; noch keiner geplanten Mission zugeordnet." };
                        project.nodes.Add(mission);
                    }
                }
                var kind = project.EventInfo(first).enabled || (first.sourceProperty ?? "").StartsWith("comments")
                    ? StoryConversationKind.EventMessage : first.channel == StoryChannel.Personal ? StoryConversationKind.Personal : StoryConversationKind.Radio;
                existing = new StoryConversation { id = "conversation." + entry.id, missionId = mission.id, entryNodeId = entry.id,
                    title = ConversationTitle(project, entry), location = entry.location, kind = kind };
                project.conversations.Add(existing);
            }
            foreach (var node in members) { node.conversationId = existing.id; pending.Remove(node); }
            changed = true;
        }
        foreach (var mission in project.nodes.Where(n => n.overviewOnly))
            if (!string.IsNullOrEmpty(mission.chapter) && !project.chapters.Contains(mission.chapter)) { project.chapters.Add(mission.chapter); changed = true; }
        foreach (var conversation in project.conversations.Where(c => c.title == "Auslöser" && c.id == "conversation." + c.entryNodeId))
        {
            var entry = project.Find(conversation.entryNodeId);
            if (entry == null || string.IsNullOrEmpty(entry.eventTriggerId)) continue;
            conversation.title = ConversationTitle(project, entry); changed = true;
        }
        if (changed) EditorUtility.SetDirty(project);
        return changed;
    }

    static bool UpgradePlanningCards(StoryProject project)
    {
        if (AssetDatabase.GetAssetPath(project) != StoryPlanningCatalog.ProjectPath) return false;
        bool changed = false;
        foreach (var node in project.nodes.Where(n => n.planningNote))
        {
            if (string.IsNullOrEmpty(node.textKey)) node.textKey = node.id + ".text";
            var text = project.planningTexts.Find(t => t.key == node.textKey);
            if (text == null) { text = new StoryPlanningText { key = node.textKey, de = node.purpose ?? "", en = "" }; project.planningTexts.Add(text); }
            else if (string.IsNullOrEmpty(text.de)) text.de = node.purpose ?? "";
            node.planningNote = false; changed = true;
        }
        return changed;
    }

    public static string ConversationTitle(StoryProject project, StoryNode entry)
    {
        var trigger = project.Find(entry.eventTriggerId);
        if (trigger == null) return entry.title;
        if (!string.IsNullOrWhiteSpace(trigger.title) && trigger.title != "Auslöser") return trigger.title;
        var info = project.EventInfo(entry);
        string level = "Level " + info.fromLevel + (info.toLevel == 0 ? "+" : info.toLevel != info.fromLevel ? "–" + info.toLevel : "");
        switch (info.kind)
        {
            case StoryEventKind.LevelStart: return "Landetraining · " + level;
            case StoryEventKind.LevelTransmission: return "Trainer-Funk · " + level;
            case StoryEventKind.LateTraining: return "Trainingskommentare · " + level;
            case StoryEventKind.KnownMoon: return "Trainingsstart · Mond bereits bekannt";
            case StoryEventKind.EarthReturn: return "Firmenschiff zur Erde zurückbringen";
            case StoryEventKind.ZeroGAfterTraining: return "Schwerelosigkeit nach Trainer-Abschied";
            case StoryEventKind.TrainingIntroduction: return info.pool == "training-navigation" ? "Einweisung · Navigation" : "Einweisung · Flugtraining";
            case StoryEventKind.Discovery:
                return info.eventId switch
                {
                    "ZeroG" => "Schwerelosigkeit entdecken", "Moon" => "Mond entdecken", "MoonLanding" => "Erste Mondlandung",
                    "EVA" => "Zum ersten Mal aussteigen", "AbandonedShip" => "Verlassenes Schiff entdecken",
                    "UFO" => "Unbekannter Kontakt", "Station" => "Station entdecken", "StationLanding" => "Erste Stationslandung", _ => entry.title
                };
            case StoryEventKind.FlightState:
                string flight = info.eventId switch { "AtmosphereExit" => "Atmosphäre verlassen", "BackToPlanet" => "Zur Erde zurückkehren", "NearToMoon" => "Mondanflug", _ => entry.title };
                return "Flugkommentar · " + flight + " · " + level;
            default: return entry.title;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveGame
{
    public const int CurrentVersion = 7;
    public int version = CurrentVersion;
    public float volMusic = 0.8f;
    public float volSfx = 1f;
    public int level = 1;
    public int BestScore;
    public int CollectedScore;
    public int selectedLanderIndex;
    public int selectedLanderId = -1;
    public int earthAsideLevel;
    public string lastEarthAside;
    public Vector3 ufoContactPosition;
    public float stationSignalStartDistance;
    public float nextSignalStartDistance;
    public int stationConversationPage;
    public string stationConversationReaction;
    public List<RadioMessage> radioMessages = new();
    public string radioTrackedMessageId;
    public SerializableDictionary<string, bool> flags = new();
    public WorldSave world;

    public bool GetFlag(string key, bool def = false)
        => flags != null && flags.TryGetValue(key, out var value) ? value : def;

    public void SetFlag(string key, bool value)
    {
        flags ??= new();
        flags[key] = value;
    }

    public void Normalize()
    {
        if (version > CurrentVersion) return;
        if (version < 2) selectedLanderId = -1;
        if (version < 4 && world != null)
        {
            if (world.ships != null)
                foreach (var ship in world.ships) if (ship != null) ship.stationPad = -1;
            if (world.scoring != null) world.scoring.scoredStationPad = -1;
        }
        if (version < 5 && GetFlag("story.registrationComplete")) SetFlag("story.radioOwned", true);
        radioMessages ??= new();
        radioMessages.RemoveAll(message => message == null || string.IsNullOrEmpty(message.id));
        stationConversationReaction = Localization.Reference(stationConversationReaction);
        lastEarthAside = Localization.Reference(lastEarthAside);
        if (world != null) world.resultStoryMessage = Localization.Reference(world.resultStoryMessage);
        foreach (var message in radioMessages)
        {
            message.sender = Localization.Reference(message.sender);
            message.title = Localization.Reference(message.title);
            message.body = Localization.Reference(message.body);
            if (message.replies == null) continue;
            foreach (var reply in message.replies)
                if (reply != null)
                {
                    reply.text = Localization.Reference(reply.text);
                    reply.reaction = Localization.Reference(reply.reaction);
                }
        }
        version = CurrentVersion;
        level = Mathf.Max(1, level);
        BestScore = Mathf.Max(0, BestScore);
        CollectedScore = Mathf.Max(0, CollectedScore);
        selectedLanderIndex = Mathf.Max(0, selectedLanderIndex);
        volMusic = float.IsNaN(volMusic) || float.IsInfinity(volMusic) ? 0.8f : Mathf.Clamp01(volMusic);
        volSfx = float.IsNaN(volSfx) || float.IsInfinity(volSfx) ? 1f : Mathf.Clamp01(volSfx);
        flags ??= new();
        if (world != null && !world.IsValid()) world = null;
    }
}

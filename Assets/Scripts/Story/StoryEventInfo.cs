using System;
using UnityEngine;

public enum StoryEventKind { LevelStart, LevelTransmission, Discovery, FlightState, LateTraining, KnownMoon, EarthReturn, ZeroGAfterTraining }
[Serializable] public class StoryEventInfo
{
    public bool enabled;
    public StoryEventKind kind;
    public string eventId;
    public string pool;
    public int fromLevel = 1, toLevel = 9;
    public bool random, once;
    public float probability = 1;
    public string requiredFlag, blockedFlag;
    public string presentation;
    [TextArea] public string description;
}

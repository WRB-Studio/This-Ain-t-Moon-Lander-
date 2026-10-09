using System;
using UnityEngine;

[Serializable]
public class WorldSave
{
    public int seed, terrainLevel;
    public RandomLandscape.TerrainType terrainType;
    public Vector3[] terrain;
    public Vector2 landingZone;
    public float landingHalfWidth;
    public Vector3 moonPosition, moonScale;
    public bool hasStationLayout;
    public int stationLayoutVersion;
    public int stationArea;
    public ResidentSave[] stationResidents;
    public Vector2 stationOffset;
    public float moonRotation;
    public Vector3[] pads;
    public int activePad, activeShip;
    public ShipSave[] ships;
    public GameController.GamePhase phase;
    public bool exploring, hasResults, showScore, refilling;
    public float refillStartFuel;
    public bool atmosphereExit, backToPlanet, nearMoon;
    public float storyElapsed;
    public string resultStoryMessage;
    public ActorSave astronaut;
    public ScoreSave scoring;
    public CargoMissionSave cargoMission;

    public bool IsValid()
    {
        if (terrain == null || terrain.Length < 4 || terrain.Length > 10000
            || pads == null || pads.Length == 0 || ships == null || ships.Length == 0
            || activePad < 0 || activePad >= pads.Length || activeShip < 0 || activeShip >= ships.Length
            || !Enum.IsDefined(typeof(GameController.GamePhase), phase) || scoring == null
            || !Finite(landingZone) || !Finite(landingHalfWidth) || !Finite(moonPosition) || !Finite(moonScale)
            || !Finite(moonRotation) || !Finite(scoring.elapsed) || !Finite(scoring.lastTime) || !Finite(storyElapsed) || !Finite(refillStartFuel)
            || scoring.scoredPad < -1 || scoring.scoredPad >= pads.Length
            || scoring.scoredStationPad < -1 || scoring.scoredStationPad > 2
            || stationArea < 0 || stationArea > 2
            || (hasStationLayout && !Finite(stationOffset))) return false;
        if (cargoMission != null && !cargoMission.IsValid(ships.Length)) return false;
        if (stationResidents != null)
            foreach (var resident in stationResidents)
                if (resident == null || resident.id < 1 || !Finite(resident.x) || !Finite(resident.pause)
                    || (resident.hasPhysicsState && (!Finite(resident.position) || !Finite(resident.velocity)
                        || resident.waypointIndex < 0 || (resident.waypointDirection != 1 && resident.waypointDirection != -1)))) return false;
        for (int i = 0; i < terrain.Length; i++)
            if (!Finite(terrain[i]) || (i > 0 && terrain[i].x <= terrain[i - 1].x)) return false;
        foreach (var pad in pads) if (!Finite(pad)) return false;
        foreach (var ship in ships)
            if (ship == null || !ship.IsValid() || !Finite(ship.fuel) || !Finite(ship.fuelMax)
                || !Finite(ship.targetRotation) || !Finite(ship.gravity) || !Finite(ship.fuelEmptyTimer) || !Finite(ship.deadZoneTimer)
                || ship.stationPad < -1 || ship.stationPad > 2
                || !Enum.IsDefined(typeof(LanderController.eLanderState), ship.state)
                || !Enum.IsDefined(typeof(RigidbodyType2D), ship.bodyType)) return false;
        return phase != GameController.GamePhase.EVA || (astronaut != null && astronaut.IsValid());
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
    public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
}

[Serializable]
public class ActorSave
{
    public Vector3 position, scale;
    public float rotation, angularVelocity;
    public Vector2 velocity;
    public bool IsValid() => WorldSave.Finite(position) && WorldSave.Finite(scale)
        && WorldSave.Finite(rotation) && WorldSave.Finite(angularVelocity) && WorldSave.Finite(velocity);

    public void Capture(Transform actor, Rigidbody2D body)
    {
        position = actor.position;
        scale = actor.lossyScale;
        rotation = body.rotation;
        velocity = body.linearVelocity;
        angularVelocity = body.angularVelocity;
    }

    public void Restore(Transform actor, Rigidbody2D body)
    {
        var parentScale = actor.parent ? actor.parent.lossyScale : Vector3.one;
        actor.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
        actor.position = position;
        body.rotation = rotation;
        if (body.bodyType != RigidbodyType2D.Static)
        {
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
        }
    }
}

[Serializable]
public class ShipSave : ActorSave
{
    public int definitionId;
    public int stationPad = -1;
    public float fuel, fuelMax, targetRotation, fuelEmptyTimer, deadZoneTimer;
    public Vector2 gravity;
    public bool controlsEnabled, deadZoneTriggered, fuelInitialized;
    public RigidbodyType2D bodyType;
    public LanderController.eLanderState state;
}

[Serializable]
public class ScoreSave
{
    public bool padAwarded, moonAwarded, lastWasMoon;
    public int scoredPad = -1;
    public int scoredStationPad = -1;
    public float elapsed, lastTime;
    public int lastScore, baseScore, speedScore, angleScore, centerScore, fuelScore, timeScore, moonScore, centerPct;
}

[Serializable]
public class ResidentSave
{
    public int id;
    public float x, pause;
    public bool movingRight, walkingToEntrance, entered;
    public bool hasPhysicsState;
    public Vector3 position;
    public Vector2 velocity;
    public int waypointIndex, waypointDirection;
}

[Serializable]
public class CargoMissionSave
{
    public CargoMission.Progress stage;
    public ActorSave capsule;
    public int towShip = -1;
    public int walkArea;
    public float cableLength;
    public Vector3 passengerPosition;
    public bool IsValid(int shipCount) => Enum.IsDefined(typeof(CargoMission.Progress), stage)
        && capsule != null && capsule.IsValid() && WorldSave.Finite(passengerPosition) && WorldSave.Finite(cableLength) && cableLength >= 0f
        && walkArea >= 0 && walkArea <= 3 && towShip >= -1 && towShip < shipCount
        && (stage != CargoMission.Progress.Towing || towShip >= 0);
}

using System.Collections.Generic;
using UnityEngine;

public class SpaceNavigationTarget : MonoBehaviour
{
    public enum ObjectKind { Earth, Moon, Station, Ship, Outpost }
    public ObjectKind kind;
    public string titleKey;
    public string signalId;
    [Tooltip("Maximale Entfernung, in der ein Flugmarker sichtbar wird.")]
    [Min(1)] public float detectionDistance = 300;
    [Tooltip("Unter dieser Entfernung verschwindet der Flugmarker.")]
    [Min(0)] public float closeDistance = 8;
    public static readonly List<SpaceNavigationTarget> Active = DirectionMarkerSystem.Targets;
    public bool Available => isActiveAndEnabled && gameObject.activeInHierarchy
        && (kind != ObjectKind.Station || SpaceStation.Instance && SpaceStation.Instance.IsAvailable)
        && (kind != ObjectKind.Outpost || AsteroidOutpost.Instance && AsteroidOutpost.Instance.IsAvailable);
    public bool IsDiscovered
    {
        get
        {
            var data = SaveLoadManager.Instance ? SaveLoadManager.Instance.Data : null;
            if (data == null) return false;
            if (kind == ObjectKind.Earth || kind == ObjectKind.Ship) return true;
            if (data.GetFlag("navigation.landed." + signalId)) return true;
            // Preserve places already visited in saves from before navigation discovery existed.
            if (kind == ObjectKind.Moon) return data.GetFlag("discovery.MoonLanding");
            if (kind == ObjectKind.Station) return data.GetFlag("discovery.StationLanding") || data.GetFlag("story.registrationComplete");
            if (kind == ObjectKind.Outpost)
                return data.world?.cargoMission != null && data.world.cargoMission.stage != CargoMission.Progress.Waiting
                    || data.world?.ships != null && System.Array.Exists(data.world.ships, ship => ship.stationPad == 2);
            return false;
        }
    }
    public void DiscoverOnLanding()
    {
        var data = SaveLoadManager.Instance ? SaveLoadManager.Instance.Data : null;
        if (data != null && !string.IsNullOrEmpty(signalId)) data.SetFlag("navigation.landed." + signalId, true);
    }
    void OnEnable() => DirectionMarkerSystem.RegisterTarget(this);
    void OnDisable() => DirectionMarkerSystem.UnregisterTarget(this);
}

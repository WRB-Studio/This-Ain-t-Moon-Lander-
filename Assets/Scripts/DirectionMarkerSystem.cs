using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class DirectionMarkerSystem : MonoBehaviour
{
    public static DirectionMarkerSystem Instance { get; private set; }
    public static readonly List<SpaceNavigationTarget> Targets = new();

    [SerializeField] SpaceNavigationMarker markerPrefab;
    [SerializeField] RectTransform markerRoot;
    [SerializeField, Range(1, 10)] int maximumMarkers = 3;
    [SerializeField, Min(1)] float orbitRadius = 260;
    [SerializeField, Min(.01f)] float metresPerUnit = 10;
    [SerializeField, Range(1, 40)] float groupingAngle = 18;
    [SerializeField, Min(1)] float groupingDistance = 35;
    [SerializeField] Color missionColor = new(1, .72f, .2f);

    readonly List<SpaceNavigationMarker> markers = new();
    readonly List<Candidate> candidates = new();
    readonly List<Candidate> visibleTargets = new();
    readonly List<Vector2> markerPositions = new();
    Canvas canvas;
    bool trackedSignal;
    Vector3 trackedPosition;
    SpaceNavigationTarget trackedTarget;

    struct Candidate
    {
        public Vector3 position;
        public float distance;
        public Vector2 direction;
        public bool mission;
        public bool unknown;
    }

    public static void RegisterTarget(SpaceNavigationTarget target)
    {
        if (target && !Targets.Contains(target)) Targets.Add(target);
    }

    public static void UnregisterTarget(SpaceNavigationTarget target) => Targets.Remove(target);

    public static string FormatDistance(float distance)
    {
        float metres = distance * Mathf.Max(.01f, Instance ? Instance.metresPerUnit : 10f);
        var culture = CultureInfo.GetCultureInfo(Localization.CurrentLanguage == "de" ? "de-DE" : "en-US");
        return metres < 1000 ? metres.ToString("0", culture) + " m"
            : (metres / 1000).ToString(metres < 10000 ? "0.0" : "0", culture) + " km";
    }

    public void SetTrackedSignal(bool active, Vector3 position, SpaceNavigationTarget target)
    {
        trackedSignal = active;
        trackedPosition = position;
        trackedTarget = target;
    }

    void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();
        for (int i = 0; i < maximumMarkers; i++)
        {
            var marker = Instantiate(markerPrefab, markerRoot);
            marker.gameObject.SetActive(false);
            markers.Add(marker);
        }
    }

    void LateUpdate()
    {
        var game = GameController.Instance;
        var camera = Camera.main;
        var radio = RadioController.Instance;
        bool playing = game && game.IsPlaying && !PauseMenu.IsPaused && !(radio && radio.IsOpen)
            && !(StoryTextController.Instance && StoryTextController.Instance.IsShowingDialogue);
        foreach (var marker in markers) marker.gameObject.SetActive(false);
        if (!(radio && radio.IsOwned) || !playing || !camera || !game.ControlledTarget
            || StationInterior.Instance && StationInterior.Instance.IsInside) return;

        bool flight = game.Phase == GameController.GamePhase.Flight;
        Vector3 source = game.ControlledTarget.position;
        candidates.Clear();
        visibleTargets.Clear();
        if (flight)
        {
            foreach (var target in Targets)
            {
                if (!target || !target.Available || target.transform == game.ControlledTarget) continue;
                if ((target.kind == SpaceNavigationTarget.ObjectKind.Earth || target.kind == SpaceNavigationTarget.ObjectKind.Moon)
                    && GravityManager2D.Instance && source.y < GravityManager2D.Instance.zeroGFullY) continue;
                Add(target.transform.position, source, target.detectionDistance, target.closeDistance, false, !target.IsDiscovered);
            }
        }
        if (trackedSignal)
            Add(trackedPosition, source, float.PositiveInfinity, 1, true, trackedTarget && !trackedTarget.IsDiscovered);

        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
        foreach (var candidate in candidates)
        {
            int cluster = visibleTargets.FindIndex(other => Vector2.Angle(other.direction, candidate.direction) < groupingAngle
                && Vector3.Distance(other.position, candidate.position) < groupingDistance);
            if (cluster < 0) visibleTargets.Add(candidate);
            else if (candidate.mission) visibleTargets[cluster] = candidate;
        }
        visibleTargets.Sort((a, b) => a.mission == b.mission ? a.distance.CompareTo(b.distance) : a.mission ? -1 : 1);

        Vector2 sourceScreen = camera.WorldToScreenPoint(source);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRoot, sourceScreen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var center);
        markerPositions.Clear();
        for (int i = 0; i < Mathf.Min(markers.Count, visibleTargets.Count); i++)
        {
            var candidate = visibleTargets[i];
            Vector2 position = center + candidate.direction * orbitRadius;
            for (int ring = 1; ring <= 3 && markerPositions.Exists(p => Vector2.Distance(p, position) < 110); ring++)
                position = center + candidate.direction * (orbitRadius + ring * 110);
            var bounds = markerRoot.rect;
            position.x = Mathf.Clamp(position.x, bounds.xMin + 80, bounds.xMax - 80);
            position.y = Mathf.Clamp(position.y, bounds.yMin + 90, bounds.yMax - 90);
            markerPositions.Add(position);
            markers[i].Draw(position, candidate.direction, FormatDistance(candidate.distance),
                candidate.mission ? missionColor : Color.white, candidate.unknown);
        }
    }

    void Add(Vector3 position, Vector3 source, float range, float near, bool mission, bool unknown)
    {
        float distance = Vector2.Distance(position, source);
        if (distance > range || distance <= near) return;
        candidates.Add(new Candidate
        {
            position = position,
            distance = distance,
            direction = ((Vector2)(position - source)).normalized,
            mission = mission,
            unknown = unknown
        });
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

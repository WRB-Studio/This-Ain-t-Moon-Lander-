using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("Follow")]
    [SerializeField] float followSmooth = 5f;
    [SerializeField] Vector3 followOffset = new Vector3(0f, 2f, -10f);
    [HideInInspector] public Vector3 shakeOffset;

    [Header("Landing Zoom")]
    [SerializeField] float minZoom = 4f;
    [SerializeField] float maxZoom = 7f;
    [SerializeField] float zoomSmooth = 5f;
    [SerializeField] float zoomInDistance = 2f;
    [SerializeField] float zoomOutDistance = 12f;

    [Tooltip("Unter diesem Flugtempo ist der volle Landungszoom erlaubt.")]
    [SerializeField, Min(0f)] float landingZoomSlowSpeed = 2f;
    [Tooltip("Ab diesem Flugtempo bleibt die Kamera beim Landen in der Übersicht.")]
    [SerializeField, Min(.1f)] float landingZoomFastSpeed = 12f;

    [Header("ZeroG Zoom")]
    [SerializeField] float zeroGMaxZoom = 10f;   // wie weit raus im All
    [SerializeField] float zeroGSmooth = 3f;
    [Header("Station Zoom")]
    [SerializeField, Min(0.05f)] float stationZoomSmooth = 0.6f;
    [SerializeField, HideInInspector] float stationLandingZoom = 26f;
    [SerializeField, Min(1f)] float stationAstronautZoom = 18f;
    [UnityEngine.Serialization.FormerlySerializedAs("stationFocusWeight")]
    [Tooltip("Anteil des Landepunkts am Kamerafokus beim nahen Anflug.")]
    [SerializeField, Range(0f, .5f)] float landingFocusWeight = .4f;

    [Header("Landing Moon Zoom")]
    [SerializeField] float moonEnterMaxZoom = 12f;
    [SerializeField] float moonSurfaceZoom = 7f;
    [SerializeField] float surfaceNearDist = 2f;
    [SerializeField] float surfaceFarDist = 18f;

    [Header("Astronaut Zoom")]
    [SerializeField] float astronautZoom = 9f;
    [SerializeField] Vector3 astronautOffset = new Vector3(0f, 1.2f, -10f);
    bool isAstronaut;

    Transform target;
    Camera cam;
    Vector3 landerOffset;
    Vector3 followPosition;
    readonly System.Collections.Generic.List<RaycastHit2D> surfaceHits = new();
    readonly System.Collections.Generic.List<Collider2D> padHits = new();

    void Awake() => Instance = this;

    public void Init()
    {
        cam = GetComponent<Camera>();
        landerOffset = followOffset;
        followPosition = transform.position;
    }

    void LateUpdate()
    {
        if (!target) return;
        UpdateComposition();
        FollowTarget();
        DistanceBasedZoom();
    }

    public void SetInstantFocus()
    {
        if (!target) return;

        UpdateComposition();
        cam.orthographicSize = CalcTargetZoom();
        followPosition = GetFollowPosition();
        transform.position = followPosition + shakeOffset;
    }

    void FollowTarget()
    {
        Vector3 desired = GetFollowPosition();
        followPosition = Vector3.Lerp(followPosition, desired, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
        transform.position = followPosition + shakeOffset;
    }

    Vector3 GetFollowPosition()
    {
        Vector2 up = Vector2.up;
        if (hasLandingSurface && landingDistance < SurfaceFarDistance)
            up = landingNormal;
        else if (isAstronaut && GravityManager2D.Instance)
        {
            var gravity = GravityManager2D.Instance.GetGravity(target.position);
            if (gravity.sqrMagnitude > .01f) up = -gravity.normalized;
        }
        Vector3 desired = target.position + new Vector3(up.x * followOffset.y + followOffset.x, up.y * followOffset.y, followOffset.z);
        if (!isAstronaut && hasLandingSurface)
        {
            float blend = LandingNearBlend;
            var focus = Vector2.Lerp(target.position, landingPoint, landingFocusWeight * Mathf.SmoothStep(0, 1, blend));
            desired.x = focus.x + up.x * followOffset.y * (1f - blend);
            desired.y = focus.y + up.y * followOffset.y * (1f - blend);
        }
        return desired;
    }

    void DistanceBasedZoom()
    {
        float targetZoom = CalcTargetZoom();
        var gravity = GravityManager2D.Instance;
        bool inSpace = gravity && gravity.GetZeroBlend(target.position) > 0f && gravity.GetMoonBlend(target.position) == 0f;
        float smooth = inSpace ? zeroGSmooth : zoomSmooth;
        if (!isAstronaut && SpaceStation.Instance && SpaceStation.Instance.IsAvailable
            && Vector2.Distance(target.position, SpaceStation.Instance.transform.position) <= SpaceStation.Instance.ApproachDistance)
            smooth = stationZoomSmooth;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    float CalcTargetZoom()
    {
        if (isAstronaut)
        {
            bool station = SpaceStation.Instance && SpaceStation.Instance.IsAvailable
                && Vector2.Distance(target.position, SpaceStation.Instance.transform.position) <= SpaceStation.Instance.GravityRange
                && !(StationInterior.Instance && StationInterior.Instance.IsInside);
            return station ? stationAstronautZoom : astronautZoom;
        }
        var gm = GravityManager2D.Instance;
        float baseline = gm ? Mathf.Lerp(maxZoom, zeroGMaxZoom, gm.GetZeroBlend(target.position)) : maxZoom;
        if (hasLandingSurface)
        {
            float near = LandingNearBlend;
            float landingZoom = moonSurface ? moonSurfaceZoom : minZoom;
            // Portrait framing must leave room for both the ship and its approach point.
            landingZoom = Mathf.Max(landingZoom, 4f / Mathf.Max(.3f, cam.aspect));
            baseline = Mathf.Lerp(Mathf.Max(baseline, approachZoom), landingZoom, Mathf.SmoothStep(0, 1, near));
        }
        if (CargoMission.Instance && CargoMission.Instance.Stage == CargoMission.Progress.Towing)
            baseline = Mathf.Max(baseline, 13f / Mathf.Min(1, cam.aspect));
        return baseline;
    }

    [Header("Object Approach")]
    [Tooltip("Oberflächen in diesem Radius lösen zunächst einen Überblick aus, auch Asteroiden vor dem Außenposten.")]
    [SerializeField, Min(20)] float approachScanDistance = 120;
    [SerializeField, Min(1)] float obstacleApproachZoom = 26;
    bool hasLandingSurface, moonSurface;
    float landingDistance, approachZoom;
    Vector2 landingPoint, landingNormal;
    float LandingNearBlend
    {
        get
        {
            float near = 1f - Mathf.InverseLerp(SurfaceNearDistance, SurfaceFarDistance, landingDistance);
            var body = GameController.Instance ? GameController.Instance.ControlledBody : null;
            float speed = body ? body.linearVelocity.magnitude : 0f;
            float fast = Mathf.InverseLerp(landingZoomSlowSpeed, Mathf.Max(landingZoomSlowSpeed + .01f, landingZoomFastSpeed), speed);
            return near * (1f - Mathf.SmoothStep(0f, 1f, fast));
        }
    }
    float SurfaceNearDistance => moonSurface ? surfaceNearDist : zoomInDistance;
    float SurfaceFarDistance => moonSurface ? surfaceFarDist : zoomOutDistance;

    bool IsSurface(Collider2D collider)
    {
        if (!collider || collider.isTrigger || collider.transform.IsChildOf(target)) return false;
        if (collider.GetComponentInParent<LanderController>() || collider.GetComponentInParent<NPCMotor2D>()
            || collider.GetComponentInParent<AstronautMoonController>()) return false;
        return collider.CompareTag("Moon") || collider.CompareTag("LandingPad") || collider.CompareTag("Landscape")
            || collider.GetComponentInParent<AsteroidOutpost>() || collider.GetComponentInParent<SpaceStation>();
    }
    void UpdateComposition()
    {
        hasLandingSurface = false; landingDistance = float.PositiveInfinity; approachZoom = maxZoom; moonSurface = false;
        if (!target || isAstronaut) return;
        Vector2 origin = target.position;
        var filter = new ContactFilter2D { useTriggers = false };
        Physics2D.OverlapCircle(origin, approachScanDistance, filter, padHits);
        Collider2D nearest = null;
        foreach (var collider in padHits)
        {
            if (!IsSurface(collider)) continue;
            Vector2 point = collider.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, point);
            if (distance < landingDistance) { nearest = collider; landingPoint = point; landingDistance = distance; }
        }
        if (!nearest) return;
        hasLandingSurface = true;
        landingNormal = (origin - landingPoint).normalized;
        moonSurface = nearest.CompareTag("Moon");
        if (moonSurface)
            approachZoom = Mathf.Max(moonEnterMaxZoom, nearest.bounds.extents.x / Mathf.Max(.3f, cam.aspect) + 7);
        else if (nearest.GetComponentInParent<AsteroidOutpost>())
            approachZoom = Mathf.Max(obstacleApproachZoom, 20 / Mathf.Max(.3f, cam.aspect));
        else if (nearest.GetComponentInParent<SpaceStation>())
            approachZoom = Mathf.Max(obstacleApproachZoom, SpaceStation.Instance.GetApproachZoom(origin, zeroGMaxZoom));
        float approach = 1 - Mathf.InverseLerp(surfaceFarDist, approachScanDistance, landingDistance);
        approachZoom = Mathf.Lerp(zeroGMaxZoom, approachZoom, Mathf.SmoothStep(0, 1, approach));
        if (nearest.CompareTag("Landscape") || nearest.CompareTag("LandingPad") && !nearest.GetComponentInParent<SpaceStation>() && !nearest.GetComponentInParent<AsteroidOutpost>()) approachZoom = maxZoom;
        // The actual approach follows local gravity, then velocity in zero-G; never a fixed world-down assumption.
        Vector2 gravity = LanderController.Instance ? LanderController.Instance.Gravity : Vector2.zero;
        Vector2 direction = gravity.sqrMagnitude > .01f ? gravity.normalized
            : GameController.Instance.ControlledBody && GameController.Instance.ControlledBody.linearVelocity.sqrMagnitude > .1f
                ? GameController.Instance.ControlledBody.linearVelocity.normalized : -landingNormal;
        Physics2D.Raycast(origin, direction, filter, surfaceHits, surfaceFarDist);
        foreach (var hit in surfaceHits)
            if (IsSurface(hit.collider)) { landingPoint = hit.point; landingNormal = hit.normal; landingDistance = hit.distance; break; }
    }

    public void SetTarget(Transform newTarget, bool instantFocus = false)
    {
        target = newTarget;
        isAstronaut = target && target.GetComponent<AstronautMoonController>();
        followOffset = isAstronaut ? astronautOffset : landerOffset;

        if (instantFocus) SetInstantFocus();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}

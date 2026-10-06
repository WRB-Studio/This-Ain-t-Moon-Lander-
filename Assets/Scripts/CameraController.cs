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

    [Header("ZeroG Zoom")]
    [SerializeField] float zeroGMaxZoom = 10f;   // wie weit raus im All
    [SerializeField] float zeroGSmooth = 3f;
    [Header("Station Zoom")]
    [SerializeField, Min(0.05f)] float stationZoomSmooth = 0.6f;
    [SerializeField, Min(1f)] float stationLandingZoom = 26f;
    [SerializeField, Min(1f)] float stationAstronautZoom = 18f;
    [SerializeField, Range(0f, 1f)] float stationFocusWeight = 0.35f;

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
        FollowTarget();
        DistanceBasedZoom();
    }

    public void SetInstantFocus()
    {
        if (!target) return;

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
        Vector3 desired = target.position + followOffset;
        var station = SpaceStation.Instance;
        if (!station || !station.IsAvailable || (StationInterior.Instance && StationInterior.Instance.IsInside)) return desired;
        float distance = Vector2.Distance(target.position, station.transform.position);
        float proximity = 1f - Mathf.InverseLerp(station.GravityRange, station.ApproachDistance, distance);
        float stationShift = (station.transform.position.x - desired.x)
            * stationFocusWeight * Mathf.SmoothStep(0f, 1f, proximity);
        // Reserve screen space around the player, including on narrow portrait displays.
        float maximumShift = cam.orthographicSize * cam.aspect * 0.35f;
        desired.x += Mathf.Clamp(stationShift, -maximumShift, maximumShift);
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
            var evaStation = SpaceStation.Instance;
            bool onStationDeck = evaStation && evaStation.IsAvailable
                && (!StationInterior.Instance || !StationInterior.Instance.IsInside)
                && Vector2.Distance(target.position, evaStation.transform.position) <= evaStation.GravityRange;
            return onStationDeck ? stationAstronautZoom : astronautZoom;
        }

        var gm = GravityManager2D.Instance;

        var station = SpaceStation.Instance;
        if (station && station.IsAvailable && target
            && Vector2.Distance(target.position, station.transform.position) <= station.ApproachDistance)
        {
            float closest = float.PositiveInfinity;
            StationLandingPad nearest = null;
            foreach (var pad in station.pads)
            {
                float distance = Vector2.Distance(target.position, pad.Surface.bounds.center);
                if (distance < closest) { closest = distance; nearest = pad; }
            }
            float wideZoom = station.GetApproachZoom(target.position, zeroGMaxZoom);
            if (!nearest) return wideZoom;
            float padZoom = Mathf.Max(stationLandingZoom, nearest.Surface.bounds.extents.x / cam.aspect + 1f);
            float landingBlend = 1f - Mathf.InverseLerp(zoomInDistance, station.GravityRange, closest);
            return Mathf.Lerp(wideZoom, padZoom, Mathf.SmoothStep(0f, 1f, landingBlend));
        }

        // 1) PAD: wenn in Reichweite -> ran zoomen
        if (target)
        {
            float d = float.PositiveInfinity;
            Physics2D.OverlapCircle(target.position, zoomOutDistance, new ContactFilter2D { useTriggers = false }, padHits);
            foreach (var hit in padHits)
                if (hit && hit.CompareTag("LandingPad"))
                    d = Mathf.Min(d, Vector2.Distance(target.position, hit.transform.position));
            if (d <= zoomOutDistance)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(zoomInDistance, zoomOutDistance, d));
                return Mathf.Lerp(minZoom, maxZoom, t);
            }
        }

        // 2) MOON SURFACE: nur wenn im Mondbereich + Oberfl�che in Range -> ran zoomen
        if (gm && target)
        {
            float moonEnterT = gm.GetMoonBlend(target.position);

            if (moonEnterT > 0.001f)
            {
                float surfaceDist = GetSurfaceDistance(gm.transform.position);
                if (surfaceDist <= surfaceFarDist)
                {
                    float enterZoom = Mathf.Lerp(maxZoom, moonEnterMaxZoom, moonEnterT);

                    float nearT = 1f - Mathf.Clamp01(
                        Mathf.InverseLerp(surfaceNearDist, surfaceFarDist, surfaceDist)
                    );

                    return Mathf.Lerp(enterZoom, moonSurfaceZoom, nearT);
                }
            }
        }

        // 3) ZERO-G: wenn zeroG -> raus zoomen auf zeroGMaxZoom
        if (gm && target)
        {
            float zeroT = Mathf.Clamp01(Mathf.InverseLerp(gm.zeroGStartY, gm.zeroGFullY, target.position.y));
            if (zeroT > 0.001f)
                return Mathf.Lerp(maxZoom, zeroGMaxZoom, zeroT);
        }

        // 4) DEFAULT
        return maxZoom;
    }

    float GetSurfaceDistance(Vector3 moonCenter)
    {
        Vector2 origin = target.position;
        Vector2 dir = ((Vector2)moonCenter - origin).normalized;

        Physics2D.Raycast(origin, dir, new ContactFilter2D { useTriggers = false }, surfaceHits, 1000f);

        foreach (var hit in surfaceHits)
        {
            if (hit.collider.CompareTag("Moon"))
                return hit.distance;
        }

        return surfaceFarDist; // fallback
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

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

    [Header("Landing Moon Zoom")]
    [SerializeField] float moonEnterMaxZoom = 12f;
    [SerializeField] float moonSurfaceZoom = 7f;
    [SerializeField] float surfaceNearDist = 2f;
    [SerializeField] float surfaceFarDist = 18f;

    [Header("Astronaut Zoom")]
    [SerializeField] float astronautZoom = 5.5f;
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

        followPosition = target.position + followOffset;
        transform.position = followPosition + shakeOffset;
        cam.orthographicSize = CalcTargetZoom();
    }

    void FollowTarget()
    {
        Vector3 desired = target.position + followOffset;
        followPosition = Vector3.Lerp(followPosition, desired, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));
        transform.position = followPosition + shakeOffset;
    }

    void DistanceBasedZoom()
    {
        float targetZoom = CalcTargetZoom();
        var gravity = GravityManager2D.Instance;
        bool inSpace = gravity && gravity.GetZeroBlend(target.position) > 0f && gravity.GetMoonBlend(target.position) == 0f;
        float smooth = inSpace ? zeroGSmooth : zoomSmooth;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, 1f - Mathf.Exp(-smooth * Time.deltaTime));
    }

    float CalcTargetZoom()
    {
        if (isAstronaut) return astronautZoom;

        var gm = GravityManager2D.Instance;

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

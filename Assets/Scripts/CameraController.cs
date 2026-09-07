using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance;

    [Header("Follow")]
    [SerializeField] float followSmooth = 5f;
    [SerializeField] Vector3 followOffset = new Vector3(0f, 2f, -10f);
    public Vector3 shakeOffset;

    [Header("Landing Zoom")]
    [SerializeField] float minZoom = 4f;
    [SerializeField] float maxZoom = 7f;
    [SerializeField] float zoomSmooth = 5f;
    [SerializeField] float zoomInDistance = 2f;
    [SerializeField] float zoomOutDistance = 12f;

    [Header("ZeroG Zoom")]
    [SerializeField] float zeroGMaxZoom = 10f;
    [SerializeField] float zeroGSmooth = 3f;

    [Header("Landing Moon Zoom")]
    [SerializeField] float moonEnterMaxZoom = 12f;
    [SerializeField] float moonSurfaceZoom = 7f;
    [SerializeField] float surfaceNearDist = 2f;
    [SerializeField] float surfaceFarDist = 18f;

    [Header("Astronaut Zoom")]
    [SerializeField] float astronautZoom = 5.5f;
    [SerializeField] Vector3 astronautOffset = new Vector3(0f, 1.2f, -10f);

    Transform landingPad;
    Transform target;
    Camera cam;
    Vector3 activeFollowOffset;
    GameController.GamePhase targetPhase = GameController.GamePhase.LandingRun;

    void Awake() => Instance = this;

    public void Init()
    {
        cam = GetComponent<Camera>();
        landingPad = LandingPadPlacer.Instance ? LandingPadPlacer.Instance.transform : null;
        activeFollowOffset = followOffset;
    }

    void LateUpdate()
    {
        if (!target) return;

        FollowTarget();
        DistanceBasedZoom();
    }

    public void SetTarget(Transform newTarget, bool instantFocus = false)
    {
        GameController.GamePhase phase = GameController.Instance
            ? GameController.Instance.Phase
            : GameController.GamePhase.LandingRun;

        SetTarget(newTarget, phase, instantFocus);
    }

    public void SetTarget(Transform newTarget, GameController.GamePhase phase, bool instantFocus = false)
    {
        target = newTarget;
        targetPhase = phase;
        activeFollowOffset = phase == GameController.GamePhase.EVA ? astronautOffset : followOffset;

        if (instantFocus) SetInstantFocus();
    }

    public void SetInstantFocus()
    {
        if (!target) return;
        if (!cam) cam = GetComponent<Camera>();

        transform.position = target.position + activeFollowOffset + shakeOffset;
        cam.orthographicSize = CalcTargetZoom();
    }

    void FollowTarget()
    {
        Vector3 desired = target.position + activeFollowOffset;
        Vector3 basePos = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);
        transform.position = basePos + shakeOffset;
    }

    void DistanceBasedZoom()
    {
        float targetZoom = CalcTargetZoom();
        float smooth = zoomSmooth;

        GravityManager2D gm = GravityManager2D.Instance;
        if (gm && target && !gm.IsMoonGravityActiveAt(target.position) && gm.GetZeroGravityBlendAt(target.position) > 0.001f)
            smooth = zeroGSmooth;

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, smooth * Time.deltaTime);
    }

    float CalcTargetZoom()
    {
        if (targetPhase == GameController.GamePhase.EVA)
            return astronautZoom;

        if (!target) return maxZoom;

        if (landingPad)
        {
            float distanceToPad = Vector2.Distance(target.position, landingPad.position);
            if (distanceToPad <= zoomOutDistance)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(zoomInDistance, zoomOutDistance, distanceToPad));
                return Mathf.Lerp(minZoom, maxZoom, t);
            }
        }

        GravityManager2D gm = GravityManager2D.Instance;
        if (gm)
        {
            float moonT = gm.GetMoonBlendAt(target.position);
            if (moonT > 0.001f)
            {
                float surfaceDistance = GetSurfaceDistance(gm.transform.position);
                if (surfaceDistance <= surfaceFarDist)
                {
                    float enterZoom = Mathf.Lerp(maxZoom, moonEnterMaxZoom, moonT);
                    float nearT = 1f - Mathf.Clamp01(
                        Mathf.InverseLerp(surfaceNearDist, surfaceFarDist, surfaceDistance));

                    return Mathf.Lerp(enterZoom, moonSurfaceZoom, nearT);
                }
            }

            if (!gm.IsMoonGravityActiveAt(target.position))
            {
                float zeroT = gm.GetZeroGravityBlendAt(target.position);
                if (zeroT > 0.001f)
                    return Mathf.Lerp(maxZoom, zeroGMaxZoom, zeroT);
            }
        }

        return maxZoom;
    }

    float GetSurfaceDistance(Vector3 moonCenter)
    {
        if (!target) return surfaceFarDist;

        Vector2 origin = target.position;
        Vector2 direction = (Vector2)moonCenter - origin;
        if (direction.sqrMagnitude < 0.0001f) return 0f;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction.normalized, 1000f);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider && hit.collider.CompareTag("Moon"))
                return hit.distance;
        }

        return surfaceFarDist;
    }
}

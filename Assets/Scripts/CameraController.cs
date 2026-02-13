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

    Transform landingPad;
    Transform target;
    Camera cam;

    void Awake() => Instance = this;

    public void Init()
    {
        cam = GetComponent<Camera>();
        landingPad = LandingPadPlacer.Instance ? LandingPadPlacer.Instance.transform : null;
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

        transform.position = target.position + followOffset + shakeOffset;
        cam.orthographicSize = CalcTargetZoom();
    }

    void FollowTarget()
    {
        Vector3 desired = target.position + followOffset;
        Vector3 basePos = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);
        transform.position = basePos + shakeOffset;
    }

    void DistanceBasedZoom()
    {
        float targetZoom = CalcTargetZoom();
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomSmooth * Time.deltaTime);
    }

    float CalcTargetZoom()
    {
        if (isAstronaut) return astronautZoom;

        var gm = GravityManager2D.Instance;

        // 1) PAD: wenn in Reichweite -> ran zoomen
        if (landingPad && target)
        {
            float d = Vector2.Distance(target.position, landingPad.position);
            if (d <= zoomOutDistance)
            {
                float t = Mathf.Clamp01(Mathf.InverseLerp(zoomInDistance, zoomOutDistance, d));
                return Mathf.Lerp(minZoom, maxZoom, t);
            }
        }

        // 2) MOON SURFACE: nur wenn im Mondbereich + Oberfläche in Range -> ran zoomen
        if (gm && target)
        {
            float distToCenter = Vector2.Distance(target.position, gm.transform.position);
            float moonEnterT = Mathf.Clamp01(Mathf.InverseLerp(gm.moonEnterRadius, gm.moonFullRadius, distToCenter));

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

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, 1000f);

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Moon"))
                return hit.distance;
        }

        return surfaceFarDist; // fallback
    }

    public void SetTarget(Transform newTarget, bool instantFocus = false)
    {
        target = newTarget;
        isAstronaut = MoonEVAController.Instance.astronaut != null;
        followOffset = isAstronaut ? astronautOffset : new Vector3(0f, 2f, -10f);

        if (instantFocus) SetInstantFocus();
    }
}

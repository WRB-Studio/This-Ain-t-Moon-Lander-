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

    Transform landingPad;
    Transform target;
    Camera cam;

    void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        cam = GetComponent<Camera>();
        landingPad = LandingPadPlacer.Instance.transform;
        target = LanderController.Instance.transform;
    }

    void LateUpdate()
    {
        FollowTarget();
        DistanceBasedZoom();
    }

    public void SetInstantFocus()
    {
        transform.position = target.position + followOffset + shakeOffset;

        if (landingPad)
        {
            float dist = Vector2.Distance(target.position, landingPad.position);
            float t = Mathf.InverseLerp(zoomInDistance, zoomOutDistance, dist);
            cam.orthographicSize = Mathf.Lerp(minZoom, maxZoom, t);
        }
        else
        {
            cam.orthographicSize = maxZoom;
        }
    }

    void FollowTarget()
    {
        Vector3 desired = target.position + followOffset;
        Vector3 basePos = Vector3.Lerp(transform.position, desired, followSmooth * Time.deltaTime);
        transform.position = basePos + shakeOffset;
    }

    void DistanceBasedZoom()
    {
        float dist = Vector2.Distance(target.position, landingPad.position);
        float t = Mathf.InverseLerp(zoomInDistance, zoomOutDistance, dist);
        float targetZoom = Mathf.Lerp(minZoom, maxZoom, t);

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomSmooth * Time.deltaTime);
    }
}

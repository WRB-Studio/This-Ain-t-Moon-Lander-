using UnityEngine;

public class LandingPadPlacer : MonoBehaviour
{
    public static LandingPadPlacer Instance;

    [Header("Refs")]
    public Transform leftLeg;
    public Transform rightLeg;

    [Header("Placement")]
    public int tries = 30;
    public float yStep = 0.5f;
    public float maxLift = 20f;
    public float clearance = 0.05f;

    [Tooltip("Defines which portion of the landscape is used for landing pad placement. 1 = full length, 0.5 = middle 50%.")]
    [Range(0.1f, 1f)] public float padSpawnRange = 0.5f;

    [Header("Raycast (legs)")]
    public float raycastUp = 5f;
    public float raycastDown = 50f;

    [Header("Legs")]
    public float legBottomPadding = 0.02f;
    public bool scaleLegs = true;

    GameObject landscape;
    Transform pad;
    EdgeCollider2D groundEdge;
    Collider2D padCollider;

    void Awake() => Instance = this;

    public void Init()
    {
        pad = transform;
        padCollider = GetComponentInChildren<Collider2D>();
        CacheLandscape();
    }

    public void SetRandomPlaceForPad()
    {
        if (!pad) pad = transform;
        if (!padCollider) padCollider = GetComponentInChildren<Collider2D>();
        if (!landscape) CacheLandscape();

        PlacePad();
    }

    void CacheLandscape()
    {
        RandomLandscape randomLandscape = RandomLandscape.Instance;
        if (!randomLandscape)
            randomLandscape = FindFirstObjectByType<RandomLandscape>();

        landscape = randomLandscape ? randomLandscape.gameObject : GameObject.Find("RandomLandscape");
        groundEdge = landscape ? landscape.GetComponent<EdgeCollider2D>() : null;
    }

    public void PlacePad()
    {
        if (!landscape)
        {
            CacheLandscape();
            if (!landscape) return;
        }

        LineRenderer line = landscape.GetComponent<LineRenderer>();
        groundEdge = landscape.GetComponent<EdgeCollider2D>();

        if (!line || line.positionCount < 2)
        {
            Debug.LogWarning("LandingPadPlacer: LineRenderer missing or too few points.");
            return;
        }

        if (!groundEdge)
            Debug.LogWarning("LandingPadPlacer: EdgeCollider2D missing on landscape.");

        int count = line.positionCount;
        float halfUnused = (1f - padSpawnRange) * 0.5f;
        int minIndex = Mathf.Clamp(Mathf.FloorToInt(count * halfUnused), 0, count - 1);
        int maxIndexExclusive = Mathf.Clamp(Mathf.CeilToInt(count * (1f - halfUnused)), minIndex + 1, count);

        for (int attempt = 0; attempt < Mathf.Max(1, tries); attempt++)
        {
            int index = Random.Range(minIndex, maxIndexExclusive);
            Vector3 groundPoint = line.GetPosition(index);
            Vector3 candidate = new(groundPoint.x, groundPoint.y + yStep, pad.position.z);

            if (!LiftUntilClear(ref candidate)) continue;

            pad.position = candidate;
            UpdateLegs();
            return;
        }

        Debug.LogWarning("LandingPadPlacer: Could not find valid pad position.");
    }

    bool LiftUntilClear(ref Vector3 position)
    {
        if (!padCollider || !groundEdge)
        {
            position.y += clearance;
            return true;
        }

        Bounds bounds = padCollider.bounds;
        Vector2 size = bounds.size;
        Vector2 localOffset = (Vector2)(padCollider.transform.position - pad.position);

        for (float lifted = 0f; lifted <= maxLift; lifted += yStep)
        {
            Vector2 center = (Vector2)position + localOffset;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);

            bool overlapsLandscape = false;
            foreach (Collider2D hit in hits)
            {
                if (!hit || hit.transform.IsChildOf(pad)) continue;
                if (!hit.CompareTag("Landscape")) continue;

                overlapsLandscape = true;
                break;
            }

            if (!overlapsLandscape)
            {
                position.y += clearance;
                return true;
            }

            position.y += yStep;
        }

        return false;
    }

    void UpdateLegs()
    {
        if (!scaleLegs) return;
        UpdateLeg(leftLeg);
        UpdateLeg(rightLeg);
    }

    void UpdateLeg(Transform leg)
    {
        if (!leg) return;

        Vector3 origin = leg.position + Vector3.up * raycastUp;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, raycastUp + raycastDown);
        if (!hit.collider || !hit.collider.CompareTag("Landscape")) return;

        float topY = leg.position.y;
        float bottomY = hit.point.y + legBottomPadding;
        float worldLength = Mathf.Max(0.01f, topY - bottomY);
        float worldPerLocalY = leg.lossyScale.y / Mathf.Max(0.0001f, leg.localScale.y);

        Vector3 scale = leg.localScale;
        scale.y = worldLength / Mathf.Max(0.0001f, worldPerLocalY);
        leg.localScale = scale;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!padCollider) padCollider = GetComponentInChildren<Collider2D>();
        if (padCollider)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(padCollider.bounds.center, padCollider.bounds.size);
        }

        if (!landscape)
        {
            RandomLandscape randomLandscape = FindFirstObjectByType<RandomLandscape>();
            landscape = randomLandscape ? randomLandscape.gameObject : null;
        }

        if (!landscape) return;

        LineRenderer line = landscape.GetComponent<LineRenderer>();
        if (!line || line.positionCount < 2) return;

        int count = line.positionCount;
        float halfUnused = (1f - padSpawnRange) * 0.5f;
        float y = -5f;
        float height = 20f;

        int leftIndex = Mathf.Clamp((int)(count * halfUnused), 0, count - 1);
        int rightIndex = Mathf.Clamp((int)(count * (1f - halfUnused)) - 1, 0, count - 1);

        Vector3 left = line.GetPosition(leftIndex);
        Vector3 right = line.GetPosition(rightIndex);
        left.y = right.y = y;

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(left + Vector3.down * height, left + Vector3.up * height);
        Gizmos.DrawLine(right + Vector3.down * height, right + Vector3.up * height);
    }
#endif
}

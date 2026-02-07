using UnityEngine;

public class LandingPadPlacer : MonoBehaviour
{
    public static LandingPadPlacer Instance;

    [Header("Refs")]
    private GameObject landscape;
    private Transform pad;

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

    EdgeCollider2D groundEdge;
    Collider2D padCollider;

    void Awake()
    {        
        Instance = this;
    }

    public void Init()
    {

    }

    public void SetRandomPlaceForPad()
    {
        pad = transform;
        padCollider = pad.GetComponentInChildren<Collider2D>();
        landscape = FindFirstObjectByType<RandomLandscape>() ? FindFirstObjectByType<RandomLandscape>().gameObject : GameObject.Find("RandomLandscape");

        PlacePad();
    }

    public void PlacePad()
    {
        if (!landscape) return;

        var lr = landscape.GetComponent<LineRenderer>();
        groundEdge = landscape.GetComponent<EdgeCollider2D>();

        if (!lr || lr.positionCount < 2)
        {
            Debug.LogWarning("LandingPadPlacer: LineRenderer missing or too few points.");
            return;
        }

        if (!groundEdge)
            Debug.LogWarning("LandingPadPlacer: EdgeCollider2D missing on landscape (needed for collision check).");

        for (int t = 0; t < tries; t++)
        {
            int n = lr.positionCount;

            float halfUnused = (1f - padSpawnRange) * 0.5f;
            int minIdx = Mathf.FloorToInt(n * halfUnused);
            int maxIdx = Mathf.CeilToInt(n * (1f - halfUnused));

            int idx = Random.Range(minIdx, maxIdx);


            Vector3 groundPoint = lr.GetPosition(idx);

            Vector3 pos = new Vector3(groundPoint.x, groundPoint.y + yStep, pad.position.z);

            if (!LiftUntilClear(ref pos)) continue;

            pad.position = pos;

            if (scaleLegs) UpdateLeg(leftLeg);
            if (scaleLegs) UpdateLeg(rightLeg);

            return;
        }

        Debug.LogWarning("LandingPadPlacer: Could not find valid pad position.");
    }

    bool LiftUntilClear(ref Vector3 pos)
    {
        // fallback: ohne collider oder ground -> einfach leicht hoch
        if (!padCollider || !groundEdge)
        {
            pos.y += clearance;
            return true;
        }

        float lifted = 0f;

        // wir berechnen padCollider Center/Size relativ zum pad
        Bounds b = padCollider.bounds;
        Vector2 size = b.size;
        Vector2 localOffset = (Vector2)(padCollider.transform.position - pad.position);

        while (lifted <= maxLift)
        {
            Vector2 center = (Vector2)pos + localOffset;

            // OverlapBoxAll und dann Tag-Filter
            var hits = Physics2D.OverlapBoxAll(center, size, 0f);
            bool overlapsLandscape = false;

            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i];
                if (!c) continue;

                // ignorier eigenes Pad
                if (c.transform.IsChildOf(pad)) continue;

                // nur Landscape zählt
                if (c.CompareTag("Landscape"))
                {
                    overlapsLandscape = true;
                    break;
                }
            }

            if (!overlapsLandscape)
            {
                pos.y += clearance;
                return true;
            }

            pos.y += yStep;
            lifted += yStep;
        }

        return false;
    }

    void UpdateLeg(Transform leg)
    {
        if (!leg) return;

        Vector3 origin = leg.position + Vector3.up * raycastUp;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, raycastUp + raycastDown);

        if (!hit) return;
        if (!hit.collider || !hit.collider.CompareTag("Landscape")) return;

        float topY = leg.position.y;
        float bottomY = hit.point.y + legBottomPadding;
        float worldLen = Mathf.Max(0.01f, topY - bottomY);

        // Wie viel Welt-Y entspricht 1 localScale.y Einheit?
        float worldPerLocalY = leg.lossyScale.y / Mathf.Max(0.0001f, leg.localScale.y);

        // localScale.y so setzen, dass World-Höhe = worldLen wird
        Vector3 s = leg.localScale;
        s.y = worldLen / Mathf.Max(0.0001f, worldPerLocalY);
        leg.localScale = s;

        // Position NICHT ändern (dein Setup wächst nach unten)
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!padCollider) return;

        Bounds b = padCollider.bounds;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(b.center, b.size);


        //Pad spawn range on landscape
        if (!landscape) return;
        var lr = landscape.GetComponent<LineRenderer>();
        if (!lr || lr.positionCount < 2) return;

        int n = lr.positionCount;
        float h = (1f - padSpawnRange) * .5f, y = -5f, t = 20f;

        Vector3 l = lr.GetPosition((int)(n * h));
        Vector3 r = lr.GetPosition((int)(n * (1f - h)) - 1);
        l.y = r.y = y;

        Gizmos.color = new(.2f, .6f, 1f, .9f);
        Gizmos.DrawLine(l, r);
        Gizmos.DrawLine(l + Vector3.down * t, l + Vector3.up * t);
        Gizmos.DrawLine(r + Vector3.down * t, r + Vector3.up * t);
    }
#endif
}

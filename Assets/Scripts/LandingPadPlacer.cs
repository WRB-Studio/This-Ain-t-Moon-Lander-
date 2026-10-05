using UnityEngine;
using UnityEngine.Serialization;

public class LandingPadPlacer : MonoBehaviour
{
    public static LandingPadPlacer Instance;

    [Header("Refs")]
    private GameObject landscape;
    private Transform pad;

    public Transform leftLeg;
    public Transform rightLeg;

    [Header("Placement")]
    [FormerlySerializedAs("yStep")]
    [Tooltip("Distance between the pad collider's bottom and the planned terrain surface.")]
    [Min(0.05f)] public float groundOffset = 0.5f;
    [Tooltip("Maximum extra lift if the planned pad placement overlaps terrain.")]
    [Min(0f)] public float maxLift = 20f;
    [Tooltip("Additional clearance after finding a collision-free placement.")]
    [Min(0f)] public float clearance = 0.05f;

    [Tooltip("Defines which portion of the landscape is used for landing pad placement. 1 = full length, 0.5 = middle 50%.")]
    [Range(0.1f, 1f)] public float padSpawnRange = 0.5f;
    [Tooltip("Minimum horizontal distance between pads added to the existing landscape.")]
    [Min(1f)] public float minimumPadSpacing = 20f;
    [Tooltip("Maximum distance from the last landing to a newly added pad.")]
    [Min(1f)] public float maxNextPadDistance = 45f;

    [Header("Raycast (legs)")]
    public float raycastUp = 5f;
    public float raycastDown = 50f;

    [Header("Legs")]
    public float legBottomPadding = 0.02f;
    public bool scaleLegs = true;

    EdgeCollider2D groundEdge;
    Collider2D padCollider;
    readonly System.Collections.Generic.List<Collider2D> overlapHits = new();
    readonly System.Collections.Generic.List<RaycastHit2D> legHits = new();

    void Awake()
    {        
        if (!Instance) Instance = this;
    }

    public void Init()
    {
        pad = transform;
        padCollider = GetComponentInChildren<Collider2D>();
        var terrain = RandomLandscape.Instance ? RandomLandscape.Instance : FindFirstObjectByType<RandomLandscape>();
        if (terrain) landscape = terrain.gameObject;
    }

    public void SetRandomPlaceForPad()
    {
        Init();
        PlacePad();
    }

    public float GetRequiredGroundHalfWidth()
    {
        if (!padCollider) padCollider = GetComponentInChildren<Collider2D>();
        return padCollider ? padCollider.bounds.extents.x
            + Mathf.Abs(padCollider.bounds.center.x - transform.position.x) + 0.5f : 1f;
    }

    public void PlacePad()
    {
        if (!landscape || !pad) Init();
        if (!landscape) return;

        groundEdge = landscape.GetComponent<EdgeCollider2D>();

        if (!groundEdge)
            Debug.LogWarning("LandingPadPlacer: EdgeCollider2D missing on landscape (needed for collision check).");

        var plannedTerrain = landscape.GetComponent<RandomLandscape>();
        if (!plannedTerrain)
        {
            Debug.LogWarning("LandingPadPlacer: RandomLandscape is required to plan the landing zone.");
            return;
        }
        if (!plannedTerrain.HasLayout) plannedTerrain.GenerateNewLevel();
        if (!plannedTerrain.HasLayout) return;
        Vector2 zone = plannedTerrain.LandingZone;
        float bottomOffset = padCollider ? padCollider.bounds.min.y - pad.position.y : 0f;
        Vector3 position = new Vector3(zone.x, zone.y + Mathf.Max(0.05f, groundOffset) - bottomOffset, pad.position.z);
        Physics2D.SyncTransforms();
        if (!LiftUntilClear(ref position))
        {
            Debug.LogWarning("LandingPadPlacer: Planned landing zone could not fit the pad.");
            return;
        }
        pad.position = position;
        Physics2D.SyncTransforms();
        if (scaleLegs) UpdateLeg(leftLeg);
        if (scaleLegs) UpdateLeg(rightLeg);
    }

    public LandingPadPlacer CreateNextPad(Vector2 lastLanding)
    {
        var next = Instantiate(GameController.Instance.landingPadPrefab, transform.parent);
        next.groundOffset = groundOffset;
        next.maxLift = maxLift;
        next.clearance = clearance;
        next.padSpawnRange = padSpawnRange;
        next.minimumPadSpacing = minimumPadSpacing;
        next.maxNextPadDistance = maxNextPadDistance;
        next.raycastUp = raycastUp;
        next.raycastDown = raycastDown;
        next.legBottomPadding = legBottomPadding;
        next.scaleLegs = scaleLegs;
        next.name = "LandingPad " + (GameController.Instance.level + 1);
        next.Init();
        if (!next.TryPlaceOnExistingTerrain(lastLanding))
        {
            next.gameObject.SetActive(false);
            Destroy(next.gameObject);
            return null;
        }
        Instance = next;
        return next;
    }

    public void RestorePosition(Vector3 position)
    {
        Init();
        groundEdge = landscape.GetComponent<EdgeCollider2D>();
        transform.position = position;
        Physics2D.SyncTransforms();
        if (scaleLegs) { UpdateLeg(leftLeg); UpdateLeg(rightLeg); }
    }

    public bool HasParkedShip(LanderController approaching)
    {
        if (!padCollider) padCollider = GetComponentInChildren<Collider2D>();
        if (!padCollider) return false;
        var surface = padCollider.bounds;
        foreach (var ship in FindObjectsByType<LanderController>(FindObjectsSortMode.None))
        {
            if (ship == approaching || !ship.IsParkedOnPad) continue;
            var hull = ship.GetComponent<Collider2D>().bounds;
            if (hull.min.x <= surface.max.x && hull.max.x >= surface.min.x
                && hull.min.y <= surface.max.y + 0.25f && hull.max.y >= surface.min.y) return true;
        }
        return false;
    }

    bool TryPlaceOnExistingTerrain(Vector2 lastLanding)
    {
        var terrain = RandomLandscape.Instance;
        if (!terrain || !terrain.HasLayout) return false;
        groundEdge = terrain.GetComponent<EdgeCollider2D>();
        var points = terrain.GetComponent<LineRenderer>();
        var existingPads = FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None);
        float halfWidth = GetRequiredGroundHalfWidth();
        float span = points.GetPosition(points.positionCount - 1).x - points.GetPosition(0).x;
        float middle = points.GetPosition(0).x + span * 0.5f;
        float range = span * Mathf.Clamp(padSpawnRange, 0.1f, 1f) * 0.5f;
        var candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < points.positionCount; i++)
        {
            float x = points.GetPosition(i).x;
            if (Mathf.Abs(x - middle) + halfWidth <= range && Mathf.Abs(x - lastLanding.x) <= maxNextPadDistance)
                candidates.Add(i);
        }
        float bottomOffset = padCollider ? padCollider.bounds.min.y - pad.position.y : 0f;
        while (candidates.Count > 0)
        {
            int index = Random.Range(0, candidates.Count);
            float x = points.GetPosition(candidates[index]).x;
            candidates[index] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);
            bool occupied = false;
            foreach (var other in existingPads)
            {
                if (other == this) continue;
                float separation = Mathf.Max(minimumPadSpacing, halfWidth + other.GetRequiredGroundHalfWidth());
                if (Mathf.Abs(x - other.transform.position.x) < separation) { occupied = true; break; }
            }
            if (occupied) continue;
            float groundY = terrain.GetHighestGround(x - halfWidth, x + halfWidth);
            Vector3 position = new Vector3(x, groundY + Mathf.Max(0.05f, groundOffset) - bottomOffset, pad.position.z);
            if (!LiftUntilClear(ref position)) continue;
            if (Vector2.Distance(position, lastLanding) > maxNextPadDistance) continue;
            pad.position = position;
            Physics2D.SyncTransforms();
            if (scaleLegs) { UpdateLeg(leftLeg); UpdateLeg(rightLeg); }
            return true;
        }
        return false;
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
        Vector2 localOffset = (Vector2)(b.center - pad.position);

        while (lifted <= maxLift)
        {
            Vector2 center = (Vector2)pos + localOffset;

            // OverlapBoxAll und dann Tag-Filter
            Physics2D.OverlapBox(center, size, 0f, new ContactFilter2D { useTriggers = false }, overlapHits);
            bool overlapsLandscape = false;

            for (int i = 0; i < overlapHits.Count; i++)
            {
                var c = overlapHits[i];
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

            const float step = 0.25f;
            pos.y += step;
            lifted += step;
        }

        return false;
    }

    void UpdateLeg(Transform leg)
    {
        if (!leg || !groundEdge) return;

        Vector3 origin = leg.position + Vector3.up * raycastUp;
        Physics2D.Raycast(origin, Vector2.down, new ContactFilter2D { useTriggers = false }, legHits, raycastUp + raycastDown);
        RaycastHit2D hit = default;
        foreach (var candidate in legHits)
        {
            if (candidate.collider != groundEdge) continue;
            hit = candidate;
            break;
        }
        if (!hit) return;

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

    void OnDestroy() { if (Instance == this) Instance = null; }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!padCollider) return;

        Bounds b = padCollider.bounds;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(b.center, b.size);

        //Pad spawn range on landscape
        if (!landscape || !pad) Init();
        if (!landscape) return;
        var lr = landscape.GetComponent<LineRenderer>();
        if (!lr || lr.positionCount < 2) return;

        int n = lr.positionCount;
        float h = (1f - Mathf.Clamp(padSpawnRange, 0.1f, 1f)) * .5f, y = -5f, t = 20f;
        int leftIndex = Mathf.Clamp((int)(n * h), 0, n - 1);
        int rightIndex = Mathf.Clamp((int)(n * (1f - h)) - 1, 0, n - 1);

        Vector3 l = lr.GetPosition(leftIndex);
        Vector3 r = lr.GetPosition(rightIndex);
        l.y = r.y = y;

        Gizmos.color = new(.2f, .6f, 1f, .9f);
        Gizmos.DrawLine(l, r);
        Gizmos.DrawLine(l + Vector3.down * t, l + Vector3.up * t);
        Gizmos.DrawLine(r + Vector3.down * t, r + Vector3.up * t);
    }
#endif
}

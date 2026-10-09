using UnityEngine;

public class AsteroidOutpost : MonoBehaviour
{
    public static AsteroidOutpost Instance { get; private set; }
    [SerializeField] Vector2 offsetFromStation = new(650f, 480f);
    [SerializeField] Transform geometry;
    [SerializeField] Transform cargoSpawn;
    [SerializeField] StationLandingPad landingPad;
    [SerializeField, Min(1f)] float gravityRange = 68f;
    [SerializeField, Min(0f)] float gravityStrength = 1.8f;
    public Transform CargoSpawn => cargoSpawn;
    public StationLandingPad LandingPad => landingPad;
    public bool IsAvailable => SpaceStation.Instance && SpaceStation.Instance.IsAvailable;

    void Awake() => Instance = this;
    void Update() => SyncLayout();
    public void SyncLayout()
    {
        if (!SpaceStation.Instance) return;
        transform.position = GravityManager2D.Instance.transform.position
            + (Vector3)(SpaceStation.Instance.OffsetFromMoon + offsetFromStation);
        geometry.gameObject.SetActive(IsAvailable);
    }
    public Vector2 GetGravity(Vector2 position)
    {
        if (!IsAvailable) return Vector2.zero;
        Vector2 local = position - (Vector2)transform.position;
        float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gravityRange - 12f, gravityRange, local.magnitude));
        // The old outpost still has a working deck gravity generator.
        return Vector2.down * gravityStrength * blend;
    }
    public bool IsNear(Vector2 position) => IsAvailable && Vector2.Distance(position, transform.position) < gravityRange + 45f;
    void OnDestroy() { if (Instance == this) Instance = null; }
}

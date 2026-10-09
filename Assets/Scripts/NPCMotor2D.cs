using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class NPCMotor2D : MonoBehaviour
{
    public const string GroundLayer = "NPCGround";
    public const string CharacterLayer = "NPC";
    [SerializeField, Min(0f)] float fallbackGravity = 3.5f;
    [SerializeField, Min(0.01f)] float groundProbeDistance = 0.12f;
    Rigidbody2D body;
    CapsuleCollider2D shape;
    StationResident resident;
    CargoPassenger passenger;
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    float requestedSpeed;
    public bool IsGrounded { get; private set; }
    public Rigidbody2D Body { get { Cache(); return body; } }
    public Vector2 FeetPosition { get { Cache(); return body.position + (Vector2)transform.TransformVector(shape.offset + Vector2.down * shape.size.y * 0.5f); } }
    public float FootOffset { get { Cache(); return transform.TransformVector(shape.offset + Vector2.down * shape.size.y * 0.5f).y; } }

    void Awake()
    {
        Cache();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        int layer = LayerMask.NameToLayer(CharacterLayer);
        if (layer >= 0) gameObject.layer = layer;
    }
    void Cache()
    {
        if (!body) body = GetComponent<Rigidbody2D>();
        if (!shape) shape = GetComponent<CapsuleCollider2D>();
        if (!resident) resident = GetComponent<StationResident>();
        if (!passenger) passenger = GetComponent<CargoPassenger>();
    }
    public void Walk(float speed) => requestedSpeed = speed;
    public void Stop() => requestedSpeed = 0f;
    void FixedUpdate()
    {
        bool active = (!resident || resident.isActiveAndEnabled) && (!passenger || passenger.isActiveAndEnabled);
        body.simulated = active;
        if (!active) return;
        Vector2 gravity = GravityManager2D.Instance ? GravityManager2D.Instance.GetGravity(body.position) : Vector2.down * fallbackGravity;
        body.AddForce(gravity * body.mass);
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask(GroundLayer), useTriggers = false };
        int count = shape.Cast(Vector2.down, filter, groundHits, groundProbeDistance);
        IsGrounded = false;
        Vector2 normal = Vector2.up;
        for (int i = 0; i < count; i++)
            if (groundHits[i].normal.y > 0.45f) { normal = groundHits[i].normal; IsGrounded = true; break; }
        if (IsGrounded)
        {
            Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;
            float fallingSpeed = Mathf.Min(0f, Vector2.Dot(body.linearVelocity, normal));
            body.linearVelocity = tangent * requestedSpeed + normal * fallingSpeed;
        }
        else body.linearVelocity = new Vector2(requestedSpeed, body.linearVelocity.y);
    }
    public void Teleport(Vector3 position, Vector2 velocity = default)
    {
        Cache();
        transform.position = position; body.position = position;
        body.linearVelocity = velocity; body.angularVelocity = 0f;
        requestedSpeed = 0f; IsGrounded = false;
    }
    void OnDisable() { requestedSpeed = 0f; IsGrounded = false; }
}

using UnityEngine;

public class SpacecraftSandboxPilot : MonoBehaviour
{
    public float walkSpeed = 1.5f;
    Rigidbody2D body;
    Collider2D shape;
    CharacterVisual visual;
    readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    float movement;
    void Awake()
    {
        body = GetComponent<Rigidbody2D>(); shape = GetComponent<Collider2D>();
        visual = GetComponentInChildren<CharacterVisual>();
        body.gravityScale = 0f; body.constraints = RigidbodyConstraints2D.FreezeRotation;
    }
    public void Move(float value) => movement = value;
    public void PlaceFeet(Vector3 point)
    {
        gameObject.SetActive(true);
        body.linearVelocity = Vector2.zero;
        float offset = shape.bounds.min.y - transform.position.y;
        point.y -= offset; point.y += 0.03f;
        transform.position = point; body.position = point;
    }
    void FixedUpdate()
    {
        body.AddForce(Vector2.down * (3.5f * body.mass));
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask(NPCMotor2D.GroundLayer), useTriggers = false };
        int count = shape.Cast(Vector2.down, filter, hits, 0.12f);
        Vector2 velocity = body.linearVelocity;
        bool grounded = false;
        for (int i = 0; i < count; i++)
            if (hits[i].normal.y > 0.45f)
            {
                Vector2 normal = hits[i].normal;
                velocity = new Vector2(normal.y, -normal.x) * (movement * walkSpeed) + normal * Mathf.Min(0f, Vector2.Dot(velocity, normal));
                grounded = true; break;
            }
        if (!grounded) velocity.x = movement * walkSpeed;
        body.linearVelocity = velocity;
        if (visual)
        {
            visual.SetWalking(grounded && Mathf.Abs(movement) > 0.01f, Mathf.Abs(velocity.x));
            if (Mathf.Abs(movement) > 0.01f) visual.FaceLeft(movement < 0f);
        }
    }
    void OnDisable() => movement = 0f;
}

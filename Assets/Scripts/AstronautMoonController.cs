using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AstronautMoonController : MonoBehaviour
{
    [Header("Anim")]
    [SerializeField] bool invertFlip;
    [SerializeField] float moveEpsilon = 0.0001f;

    [Header("Moon Hop (visual only)")]
    [SerializeField] float hopAmp = 0.03f;
    [SerializeField] float hopFreq = 6f;

    [Header("Movement")]
    [SerializeField] float maxMoveSpeed = 6f;
    [SerializeField] float rotateSmooth = 12f;
    [Range(0f, 1f)] [SerializeField] float inputDeadZone = 0.15f;

    Transform visualRoot;
    Vector3 visualBaseLocalPos;
    Animator animator;
    SpriteRenderer spriteRenderer;
    Rigidbody2D rb;
    Transform moonCenter;

    Vector2 desiredDirWorld;
    bool lastFlip;

    static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();

        if (transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
            visualBaseLocalPos = visualRoot.localPosition;
        }
    }

    void Start()
    {
        moonCenter = GravityManager2D.Instance ? GravityManager2D.Instance.transform : null;
    }

    void Update() => ReadInputDir();

    void FixedUpdate()
    {
        if (!moonCenter || !GravityManager2D.Instance) return;

        Vector2 toCenter = (Vector2)moonCenter.position - rb.position;
        if (toCenter.sqrMagnitude < 0.0001f) return;

        Vector2 radialIn = toCenter.normalized;
        Vector2 radialOut = -radialIn;
        Vector2 tangent = new Vector2(-radialOut.y, radialOut.x);

        ApplyGravity();
        ApplyUprightRotation(radialOut);

        float targetTangent = CalcTargetTangentSpeed(tangent);
        ApplyTangentialVelocity(radialIn, tangent, targetTangent);
        UpdateVisuals(targetTangent);
    }

    void ReadInputDir()
    {
        desiredDirWorld = Vector2.zero;
        if (!Camera.main) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0))
        {
            desiredDirWorld = ScreenToWorldDir(Input.mousePosition);
            return;
        }
#endif

        if (Input.touchCount <= 0) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            desiredDirWorld = ScreenToWorldDir(touch.position);
    }

    Vector2 ScreenToWorldDir(Vector2 screenPos)
    {
        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        Vector2 direction = (Vector2)world - rb.position;
        return direction.sqrMagnitude < 0.0001f ? Vector2.zero : direction.normalized;
    }

    void ApplyGravity()
    {
        Vector2 gravity = GravityManager2D.Instance.GetGravityAt(rb.position);
        if (gravity.sqrMagnitude > 0.0001f)
            rb.AddForce(gravity * rb.mass, ForceMode2D.Force);
    }

    void ApplyUprightRotation(Vector2 radialOut)
    {
        float desiredZ = Mathf.Atan2(radialOut.y, radialOut.x) * Mathf.Rad2Deg - 90f;
        float k = 1f - Mathf.Exp(-rotateSmooth * Time.fixedDeltaTime);
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, desiredZ, k));
    }

    float CalcTargetTangentSpeed(Vector2 tangent)
    {
        float move = Vector2.Dot(desiredDirWorld, tangent);
        if (Mathf.Abs(move) < inputDeadZone) move = 0f;
        return move * maxMoveSpeed;
    }

    void ApplyTangentialVelocity(Vector2 radialIn, Vector2 tangent, float targetTangent)
    {
        Vector2 velocity = rb.linearVelocity;
        float radialSpeed = Vector2.Dot(velocity, radialIn);
        rb.linearVelocity = radialIn * radialSpeed + tangent * targetTangent;
    }

    void UpdateVisuals(float targetTangent)
    {
        bool walking = Mathf.Abs(targetTangent) > moveEpsilon;
        if (animator) animator.SetBool(AnimIsWalking, walking);

        if (walking && spriteRenderer)
        {
            bool flip = targetTangent > 0f;
            lastFlip = invertFlip ? !flip : flip;
        }

        if (spriteRenderer) spriteRenderer.flipX = lastFlip;
        if (!visualRoot) return;

        float hop = walking ? Mathf.Sin(Time.time * hopFreq) * hopAmp : 0f;
        visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, hop, 0f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Lander")) return;

        LanderController lander = other.GetComponentInParent<LanderController>();
        if (lander)
            MoonEVAController.Instance.ShowEnterLander(lander);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Lander")) return;

        LanderController lander = other.GetComponentInParent<LanderController>();
        if (lander)
            MoonEVAController.Instance.HideEnterLander(lander);
    }
}

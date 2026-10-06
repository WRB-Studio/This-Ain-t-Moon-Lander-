using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AstronautMoonController : MonoBehaviour
{
    [Header("Anim")]
    [SerializeField] bool invertFlip = false;
    [SerializeField] float moveEpsilon = 0.0001f;

    [Header("Moon Hop (visual only)")]
    Transform visualRoot;      // Sprite/Child, nicht das Rigidbody-Objekt
    [SerializeField] float hopAmp = 0.03f;      // Weltunits, klein!
    [SerializeField] float hopFreq = 6f;        // Hz
    Vector3 visualBaseLocalPos;

    [Header("Movement")]
    [SerializeField] float maxMoveSpeed = 6f;
    [SerializeField] float rotateSmooth = 12f;
    [Range(0f, 1f)][SerializeField] float inputDeadZone = 0.15f;

    Animator anim;
    SpriteRenderer sr;
    Rigidbody2D rb;
    Transform moonCenter;

    Vector2 desiredDirWorld;
    bool lastFlip;
    Camera gameCamera;

    static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();

        gameCamera = Camera.main;
        visualRoot = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (visualRoot) visualBaseLocalPos = visualRoot.localPosition;
    }

    void Start()
    {
        CacheMoonCenter();
        var body = GetComponent<Collider2D>();
        foreach (var ship in FindObjectsByType<LanderController>(FindObjectsSortMode.None))
            if (ship.StationPad && ship.landerState == LanderController.eLanderState.LandedPad)
                foreach (var collider in ship.GetComponentsInChildren<Collider2D>())
                    if (!collider.isTrigger) Physics2D.IgnoreCollision(body, collider);
    }

    void Update() => ReadInputDir();

    void FixedUpdate()
    {
        if (!moonCenter || !GameController.Instance || GameController.Instance.Phase != GameController.GamePhase.EVA) return;

        Vector2 toCenter = GetToMoonCenter();
        if (toCenter.sqrMagnitude < 0.0001f) return;

        Vector2 radialIn = toCenter.normalized;
        if (SpaceStation.Instance && SpaceStation.Instance.GetGravity(rb.position).sqrMagnitude > 0f) radialIn = Vector2.down;
        Vector2 radialOut = -radialIn;
        Vector2 tangent = new Vector2(-radialOut.y, radialOut.x);

        ApplyMoonGravity(radialIn);
        ApplyUprightRotation(radialOut);

        float targetTangent = CalcTargetTangentSpeed(tangent);
        ApplyTangentialVelocity(radialIn, tangent, targetTangent);

        UpdateAnimAndFlip(targetTangent);
    }

    void CacheMoonCenter()
    {
        moonCenter = GravityManager2D.Instance ? GravityManager2D.Instance.transform : null;
    }

    void ReadInputDir()
    {
        desiredDirWorld = Vector2.zero;
        if (!gameCamera) gameCamera = Camera.main;
        if (!gameCamera || !GameController.Instance.IsPlaying) return;
        if (LanderUI.Instance.TryGetGameplayPointer(out var screenPosition))
            desiredDirWorld = ScreenToWorldDir(screenPosition);
    }
    Vector2 ScreenToWorldDir(Vector2 screenPos)
    {
        var wp = gameCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        Vector2 dir = (Vector2)wp - rb.position;
        return dir.sqrMagnitude < 0.0001f ? Vector2.zero : dir.normalized;
    }

    Vector2 GetToMoonCenter() => (Vector2)moonCenter.position - rb.position;

    void ApplyMoonGravity(Vector2 radialIn)
    {
        var stationGravity = SpaceStation.Instance ? SpaceStation.Instance.GetGravity(rb.position) : Vector2.zero;
        rb.AddForce(stationGravity.sqrMagnitude > 0f ? stationGravity
            : radialIn * GravityManager2D.Instance.moonGravityStrength, ForceMode2D.Force);
    }

    void ApplyUprightRotation(Vector2 radialOut)
    {
        float desiredZ = Mathf.Atan2(radialOut.y, radialOut.x) * Mathf.Rad2Deg - 90f;
        float z = Mathf.LerpAngle(rb.rotation, desiredZ, 1f - Mathf.Exp(-rotateSmooth * Time.fixedDeltaTime));
        rb.MoveRotation(z);
    }

    float CalcTargetTangentSpeed(Vector2 tangent)
    {
        float move = Vector2.Dot(desiredDirWorld, tangent);
        if (Mathf.Abs(move) < inputDeadZone) move = 0f;
        return move * maxMoveSpeed;
    }

    void ApplyTangentialVelocity(Vector2 radialIn, Vector2 tangent, float targetTangent)
    {
        var v = rb.linearVelocity;

        // radialen Anteil behalten
        float radialSpeed = Vector2.Dot(v, radialIn);
        Vector2 vRadial = radialIn * radialSpeed;

        // tangentialen Anteil NICHT auf 0 resetten, sondern direkt setzen
        Vector2 vTangent = tangent * targetTangent;

        rb.linearVelocity = vRadial + vTangent;
    }

    void UpdateAnimAndFlip(float targetTangent)
    {
        bool isWalking = Mathf.Abs(targetTangent) > moveEpsilon;
        if (anim) anim.SetBool(AnimIsWalking, isWalking);

        if (isWalking && sr)
        {
            bool flip = targetTangent > 0f;
            lastFlip = invertFlip ? !flip : flip;
        }

        if (sr) sr.flipX = lastFlip;
        if (!visualRoot) return;

        // �hop� nur beim laufen
        bool onStation = SpaceStation.Instance && SpaceStation.Instance.GetGravity(rb.position).sqrMagnitude > 0f;
        if (isWalking && !onStation)
        {
            float y = Mathf.Sin(Time.time * hopFreq) * hopAmp;
            visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, y, 0f);
        }
        else
        {
            visualRoot.localPosition = visualBaseLocalPos;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Lander")) MoonEVAController.Instance.RegisterLander(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Lander") && MoonEVAController.Instance)
            MoonEVAController.Instance.UnregisterLander(other);
    }
}

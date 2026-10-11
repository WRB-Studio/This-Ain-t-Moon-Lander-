using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AstronautMoonController : MonoBehaviour
{
    [Header("Anim")]
    [Tooltip("Kehrt die Blickrichtung der Grafik um.")]
    [SerializeField] bool invertFlip = false;
    [Tooltip("Ab dieser Geschwindigkeit startet die Laufanimation.")]
    [SerializeField] float moveEpsilon = 0.0001f;

    [Header("Moon Hop (visual only)")]
    Transform visualRoot;      // Sprite/Child, nicht das Rigidbody-Objekt
    [Tooltip("Höhe des sichtbaren Mond-Hüpfens. Ändert keine Physik.")]
    [SerializeField] float hopAmp = 0.03f;      // Weltunits, klein!
    [Tooltip("Tempo des sichtbaren Mond-Hüpfens. Höher = schneller.")]
    [SerializeField] float hopFreq = 6f;        // Hz
    Vector3 visualBaseLocalPos;

    [Header("Movement")]
    [Tooltip("Basis-Gehgeschwindigkeit für Station und Mond, in Einheiten pro Sekunde.")]
    [SerializeField] float maxMoveSpeed = 6f;
    [Tooltip("Geschwindigkeitsfaktor auf Stationen und Außenposten. 0.75 = 75 %.")]
    [SerializeField, Range(0.1f, 1f)] float stationSpeedMultiplier = 0.75f;
    [Tooltip("Geschwindigkeitsfaktor auf dem Mond. 0.75 = 75 %.")]
    [SerializeField, Range(0.1f, 1f)] float moonSpeedMultiplier = 0.75f;
    [Tooltip("Tempo der Ausrichtung zum Boden. Höher = schneller.")]
    [SerializeField] float rotateSmooth = 12f;
    [Tooltip("Ignoriert kleine seitliche Eingaben. Höher = stärkere Eingabe nötig.")]
    [Range(0f, 1f)][SerializeField] float inputDeadZone = 0.15f;
    [Tooltip("Abstand zum Charakter als Anteil der Bildschirmhöhe, ab dem das Gehen beginnt.")]
    [SerializeField, Range(0f, .2f)] float touchWalkDistance = .015f;
    [Tooltip("Abstand als Anteil der Bildschirmhöhe für volles Lauftempo.")]
    [SerializeField, Range(.05f, .5f)] float touchSprintDistance = .1f;
    [Tooltip("Gehgeschwindigkeit direkt außerhalb der Ruhezone, relativ zum Basistempo.")]
    [SerializeField, Range(.1f, 1f)] float minimumWalkMultiplier = .6f;
    [Tooltip("Sprinttempo relativ zur Basis-Gehgeschwindigkeit.")]
    [SerializeField, Min(1f)] float sprintMultiplier = 1.4f;
    float inputSpeed;
    [Header("Zero-G")]
    [Tooltip("Unter dieser lokalen Beschleunigung treibt der Astronaut frei und kann nicht laufen.")]
    [SerializeField, Min(0f)] float zeroGThreshold = .05f;
    [Tooltip("Sekunden freies Treiben, bevor der Astronaut verloren geht.")]
    [SerializeField, Min(1f)] float zeroGSurvivalTime = 5f;
    float zeroGElapsed;
    bool zeroGWarningShown;
    public bool IsZeroGAt(Vector2 position) => GravityManager2D.Instance
        && GravityManager2D.Instance.GetGravity(position).sqrMagnitude <= zeroGThreshold * zeroGThreshold;
    public bool CanSavePosition => rb && IsGrounded && !IsZeroGAt(rb.position);
    public bool IsFloating { get; private set; }
    [Tooltip("Abstand des Bodenfühlers entlang der lokalen Schwerkraft.")]
    [SerializeField, Min(.01f)] float groundProbeDistance = .15f;
    public bool IsGrounded { get; private set; }
    Collider2D shape;
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];

    Animator anim;
    SpriteRenderer sr;
    CharacterVisual characterVisual;
    Rigidbody2D rb;

    Vector2 desiredDirWorld;
    bool lastFlip;
    Camera gameCamera;

    static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        shape = GetComponent<Collider2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        characterVisual = GetComponentInChildren<CharacterVisual>();

        gameCamera = Camera.main;
        visualRoot = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (visualRoot) visualBaseLocalPos = visualRoot.localPosition;
    }

    void Start()
    {
        var body = GetComponent<Collider2D>();
        foreach (var ship in FindObjectsByType<LanderController>(FindObjectsSortMode.None))
            if (ship.StationPad && ship.landerState == LanderController.eLanderState.LandedPad)
                foreach (var collider in ship.GetComponentsInChildren<Collider2D>())
                    if (!collider.isTrigger) Physics2D.IgnoreCollision(body, collider);
    }

    void Update()
    {
        ReadInputDir();
        var game = GameController.Instance;
        if (!game || game.Phase != GameController.GamePhase.EVA || !game.IsPlaying) return;
        if (!IsZeroGAt(rb.position))
        {
            zeroGElapsed = 0;
            ClearZeroGWarning();
            return;
        }
        if (PauseMenu.IsPaused || RadioController.Instance && RadioController.Instance.IsOpen
            || StoryTextController.Instance && StoryTextController.Instance.IsShowingDialogue
            || CargoMission.Instance && CargoMission.Instance.IsShowing
            || StationConversation.Instance && StationConversation.Instance.IsShowing) return;
        zeroGElapsed += Time.deltaTime;
        float remaining = Mathf.Max(0, zeroGSurvivalTime - zeroGElapsed);
        LanderUI.Instance.ShowAstronautZeroGWarning(remaining);
        zeroGWarningShown = true;
        if (remaining <= 0)
        {
            ClearZeroGWarning();
            game.HandleAstronautLost();
        }
    }
    void ClearZeroGWarning()
    {
        if (zeroGWarningShown && LanderUI.Instance) LanderUI.Instance.ShowHideDeadZoneWarning(false);
        zeroGWarningShown = false;
    }
    void OnDisable() => ClearZeroGWarning();

    void FixedUpdate()
    {
        if (!GravityManager2D.Instance || !GameController.Instance || GameController.Instance.Phase != GameController.GamePhase.EVA) return;
        Vector2 gravity = GravityManager2D.Instance.GetGravity(rb.position);
        IsFloating = gravity.sqrMagnitude <= zeroGThreshold * zeroGThreshold;
        if (characterVisual) characterVisual.SetFloating(IsFloating);
        if (IsFloating)
        {
            IsGrounded = false;
            desiredDirWorld = Vector2.zero;
            inputSpeed = 0f;
            UpdateAnimAndFlip(0, true);
            // Keep momentum and orientation; waving limbs does not propel the astronaut.
            return;
        }
        Vector2 radialIn = gravity.normalized;
        Vector2 stationGravity = SpaceStation.Instance ? SpaceStation.Instance.GetGravity(rb.position) : Vector2.zero;
        if (stationGravity.sqrMagnitude == 0f && AsteroidOutpost.Instance)
            stationGravity = AsteroidOutpost.Instance.GetGravity(rb.position);
        bool onStation = stationGravity.sqrMagnitude > 0f;
        Vector2 radialOut = -radialIn;
        Vector2 tangent = new Vector2(-radialOut.y, radialOut.x);

        rb.AddForce(gravity * rb.mass, ForceMode2D.Force);
        ApplyUprightRotation(radialOut);
        IsGrounded = CheckGround(radialIn);
        if (!IsGrounded)
        {
            UpdateAnimAndFlip(0, onStation);
            return;
        }

        float targetTangent = CalcTargetTangentSpeed(tangent);
        targetTangent *= onStation ? stationSpeedMultiplier : moonSpeedMultiplier;
        ApplyTangentialVelocity(radialIn, tangent, targetTangent);

        UpdateAnimAndFlip(targetTangent, onStation);
    }

    void ReadInputDir()
    {
        desiredDirWorld = Vector2.zero;
        inputSpeed = 0f;
        if (!gameCamera) gameCamera = Camera.main;
        if (!gameCamera || !GameController.Instance || !GameController.Instance.IsPlaying || IsFloating) return;
        if (LanderUI.Instance.TryGetGameplayPointer(out var screenPosition))
        {
            desiredDirWorld = ScreenToWorldDir(screenPosition);
            float distance = Vector2.Distance(screenPosition, gameCamera.WorldToScreenPoint(rb.position)) / Mathf.Max(1, Screen.height);
            float intensity = Mathf.InverseLerp(touchWalkDistance, Mathf.Max(touchWalkDistance + .01f, touchSprintDistance), distance);
            inputSpeed = distance <= touchWalkDistance ? 0f : Mathf.Lerp(minimumWalkMultiplier, sprintMultiplier, Mathf.SmoothStep(0, 1, intensity));
        }
    }
    Vector2 ScreenToWorldDir(Vector2 screenPos)
    {
        var wp = gameCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        Vector2 dir = (Vector2)wp - rb.position;
        return dir.sqrMagnitude < 0.0001f ? Vector2.zero : dir.normalized;
    }

    void ApplyUprightRotation(Vector2 radialOut)
    {
        float desiredZ = Mathf.Atan2(radialOut.y, radialOut.x) * Mathf.Rad2Deg - 90f;
        float z = Mathf.LerpAngle(rb.rotation, desiredZ, 1f - Mathf.Exp(-rotateSmooth * Time.fixedDeltaTime));
        rb.MoveRotation(z);
    }

    bool CheckGround(Vector2 down)
    {
        if (!shape) return false;
        int count = shape.Cast(down, new ContactFilter2D { useTriggers = false }, groundHits, groundProbeDistance);
        for (int i = 0; i < count; i++)
        {
            var collider = groundHits[i].collider;
            if (!collider || collider.GetComponentInParent<NPCMotor2D>() || collider.GetComponentInParent<LanderController>()) continue;
            if (Vector2.Dot(groundHits[i].normal, -down) > .35f) return true;
        }
        return false;
    }

    float CalcTargetTangentSpeed(Vector2 tangent)
    {
        float move = Vector2.Dot(desiredDirWorld, tangent);
        if (Mathf.Abs(move) < inputDeadZone) move = 0f;
        return Mathf.Sign(move) * (move == 0 ? 0 : maxMoveSpeed * inputSpeed);
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

    void UpdateAnimAndFlip(float targetTangent, bool onStation)
    {
        bool isWalking = Mathf.Abs(targetTangent) > moveEpsilon;

        if (isWalking)
        {
            bool flip = targetTangent > 0f;
            lastFlip = invertFlip ? !flip : flip;
        }

        if (characterVisual)
        {
            characterVisual.SetMoonHop(hopAmp, hopFreq);
            characterVisual.SetWalking(isWalking, Mathf.Abs(targetTangent), !onStation);
            characterVisual.FaceLeft(lastFlip);
            return;
        }

        if (anim) anim.SetBool(AnimIsWalking, isWalking);
        if (sr) sr.flipX = lastFlip;
        if (!visualRoot) return;

        // �hop� nur beim laufen
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

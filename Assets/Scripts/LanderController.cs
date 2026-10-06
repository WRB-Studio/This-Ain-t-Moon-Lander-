using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(SpriteRenderer))]
public class LanderController : MonoBehaviour
{
    public static LanderController Instance;

    public int landerIndex = -1;
    public int unlockCost = 1000;
    public bool isSecretLander = false;
    public bool isActive = false;

    [HideInInspector] public bool controlsEnabled;
    public bool IsTractorHeld { get; private set; }
    RigidbodyConstraints2D constraintsBeforeTractor;
    public enum eLanderState { None, Flying, LandedPad, LandedMoon, CrashedLandscape, CrashedMoon, CrashedPad, OutOfFuel, DeadZone };
    [HideInInspector] public eLanderState landerState = eLanderState.None;

    [Header("Respawn (relative to LandingPad)")]
    [Tooltip("Minimum sideways offset from the target pad when spawning.")]
    [Min(0f)] public float minDistanceXToPad = 8f;
    public float maxDistanceXToPad = 6f;
    public float minDistanceYToPad = 6f;
    public float maxDistanceYToPad = 12f;

    public float minWorldY = -2f;              // harter Mindest-Y (optional)
    public float clearance = 0.2f;             // Abstand zum Boden
    public int tries = 30;

    [Header("Lander movement settings")]
    public float thrustForce = 9.5f;
    public float rotationSpeed = 180f;
    public float rotationSmooth = 0.12f;
    public float maxFallSpeed = -6f;

    [Header("Analog Steering")]
    [Tooltip("Bereich in der Mitte des Schiffs ohne Drehung.\nVerhindert Zittern beim reinen Gasgeben.")]
    public float steeringDeadzone = 0.15f;
    [Tooltip("Seitlicher Abstand (in lokalen Einheiten), ab dem volle Drehstärke erreicht wird.")]
    public float steeringRange = 2.0f;
    [Tooltip("Maximale Steuerintensität (1 = 100%).\nBegrenzt den Einfluss des Touch-Inputs.")]
    public float maxSteer = 1f;
    [Tooltip("Wie schnell sich die Steuerung an neue Touch-Positionen anpasst.\nHöher = direkter, niedriger = smoother.")]
    public float steerResponse = 8f;

    float baseRotationSpeed, baseRotationSmooth, baseThrustForce;
    bool baseCached = false;

    float steer01; // geglätteter steering wert (-1..+1)

    [HideInInspector] public float fuelMax = 3.5f;
    [Header("Fuel")]
    public float fuelBurnPerSec = 1.0f;
    [HideInInspector] public float currentFuel;
    public float fuelPerUnit = 0.25f;
    [Range(0f, 1f)] public float fuelBufferPercent = 0.4f;
    public float fuelEmptyDelay = 5f;
    private float fuelEmptyDelayCounter = 0;
    private bool fuelEmptyTriggered;
    bool fuelInitialized;

    [Header("Landing Rules")]
    public float safeSpeed = 2.0f;
    public float safeAngleDeg = 10f;
    public float safeVerticalSpeed = 1.5f;

    [Header("Dead Zone rules")]
    public float deadZoneExplodeDelay = 5;
    [HideInInspector] public bool deadZoneTriggered;
    [HideInInspector] public float deadZoneTimer;

    [Header("FX")]
    public GameObject crashEffect;

    [Header("Thrust effect")]
    [HideInInspector] public List<Transform> thrustEffects;
    [SerializeField] float thrustGrowSpeed = 6f;
    [SerializeField] float thrustMaxY = 1f;
    private AudioSource sfxThrustSound;

    [HideInInspector] public Rigidbody2D rb;

    private float targetRotation;
    private bool isThrusting;

    Collider2D hull;
    SpriteRenderer spriteRenderer;
    Camera gameCamera;
    float gravityScale;
    float lastMoonContact = float.NegativeInfinity;
    readonly HashSet<Collider2D> moonContacts = new();
    readonly HashSet<Collider2D> padContacts = new();
    float lastPadContact = float.NegativeInfinity;
    readonly HashSet<Collider2D> deadZones = new();
    readonly List<Collider2D> spawnHits = new();
    Vector2 currentGravity;

    public Vector2 Gravity => currentGravity;
    public bool IsCrashed => landerState == eLanderState.CrashedLandscape
        || landerState == eLanderState.CrashedMoon || landerState == eLanderState.CrashedPad
        || landerState == eLanderState.OutOfFuel || landerState == eLanderState.DeadZone;
    public bool IsTouchingMoon => !IsCrashed && (moonContacts.Count > 0 || Time.time - lastMoonContact < 0.2f);
    public bool IsTouchingPad => !IsCrashed && padContacts.Count > 0;
    public float FuelFraction => fuelMax > 0f ? Mathf.Clamp01(currentFuel / fuelMax) : 0f;
    public bool IsParkedOnPad => !isActive && landerState == eLanderState.LandedPad;
    public float GravityAngle => currentGravity.sqrMagnitude > 0.0001f
        ? Vector2.Angle(transform.up, -currentGravity) : 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hull = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        gravityScale = rb.gravityScale;
        rb.gravityScale = 0f;
        if (isActive)
        {
            if (Instance && Instance != this)
            {
                Debug.LogError("More than one active lander in the scene.", this);
                isActive = false;
            }
            else Instance = this;
        }
        if (!isActive) Park();
    }

    public void Init()
    {
        gameCamera = Camera.main;
        targetRotation = rb.rotation;
        thrustEffects ??= new List<Transform>();
        thrustEffects.Clear();
        foreach (Transform child in transform)
            if (child.name.Contains("ThrustEffect")) thrustEffects.Add(child);
        if (!baseCached)
        {
            CacheFlightSettings();
            baseCached = true;
        }
        currentGravity = GravityManager2D.Instance.GetGravity(rb.position);
        ApplySpaceTuning(GravityManager2D.Instance.GetZeroBlend(rb.position));
    }

    void CacheFlightSettings()
    {
        baseRotationSpeed = rotationSpeed;
        baseRotationSmooth = rotationSmooth;
        baseThrustForce = thrustForce;
    }

    public void ApplyConfiguration(LanderController prefab)
    {
        landerIndex = prefab.landerIndex;
        unlockCost = prefab.unlockCost;
        isSecretLander = prefab.isSecretLander;
        minDistanceXToPad = prefab.minDistanceXToPad;
        maxDistanceXToPad = prefab.maxDistanceXToPad;
        minDistanceYToPad = prefab.minDistanceYToPad;
        maxDistanceYToPad = prefab.maxDistanceYToPad;
        minWorldY = prefab.minWorldY;
        clearance = prefab.clearance;
        tries = prefab.tries;
        rotationSpeed = prefab.rotationSpeed;
        rotationSmooth = prefab.rotationSmooth;
        thrustForce = prefab.thrustForce;
        maxFallSpeed = prefab.maxFallSpeed;
        steeringDeadzone = prefab.steeringDeadzone;
        steeringRange = prefab.steeringRange;
        maxSteer = prefab.maxSteer;
        steerResponse = prefab.steerResponse;
        fuelBurnPerSec = prefab.fuelBurnPerSec;
        fuelPerUnit = prefab.fuelPerUnit;
        fuelBufferPercent = prefab.fuelBufferPercent;
        fuelEmptyDelay = prefab.fuelEmptyDelay;
        safeSpeed = prefab.safeSpeed;
        safeAngleDeg = prefab.safeAngleDeg;
        safeVerticalSpeed = prefab.safeVerticalSpeed;
        deadZoneExplodeDelay = prefab.deadZoneExplodeDelay;
        crashEffect = prefab.crashEffect;
        thrustGrowSpeed = prefab.thrustGrowSpeed;
        thrustMaxY = prefab.thrustMaxY;
        var prefabBody = prefab.GetComponent<Rigidbody2D>();
        rb.mass = prefabBody.mass;
        rb.linearDamping = prefabBody.linearDamping;
        rb.angularDamping = prefabBody.angularDamping;
        gravityScale = prefabBody.gravityScale;
        CacheFlightSettings();
        ApplySpaceTuning(GravityManager2D.Instance.GetZeroBlend(rb.position));
    }

    public static void ChangeLander(LanderController newLander)
    {
        var old = Instance;
        if (!old || !newLander || old == newLander) return;
        old.Park();
        old.isActive = false;
        Instance = newLander;
        newLander.isActive = true;
        newLander.Init();
        if (!newLander.fuelInitialized)
        {
            newLander.fuelMax = old.fuelMax;
            newLander.currentFuel = newLander.fuelMax * (newLander.isSecretLander ? 1f : 0.75f);
            newLander.fuelInitialized = true;
        }
    }

    public void Park()
    {
        SetTractorHold(false);
        controlsEnabled = false;
        StopThrust();
        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        rb.bodyType = RigidbodyType2D.Static;
        hull.isTrigger = landerState != eLanderState.LandedPad;
    }

    public void ResumeFlight()
    {
        if (!sfxThrustSound) sfxThrustSound = AudioManager.Instance.CreateThrusterSound();
        fuelEmptyDelayCounter = 0f;
        fuelEmptyTriggered = false;
        targetRotation = rb.rotation;
        steer01 = 0f;
        controlsEnabled = true;
        hull.isTrigger = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        if (landerState == eLanderState.LandedMoon) lastMoonContact = Time.time;
    }

    void Update()
    {
        if (!isActive) return;
        UpdateThrustEffect(isThrusting);
        if (IsTractorHeld) return;
        if (!controlsEnabled || landerState != eLanderState.Flying
            || GameController.Instance.Phase != GameController.GamePhase.Flight) return;
        if (currentFuel > 0f)
        {
            fuelEmptyDelayCounter = 0f;
            fuelEmptyTriggered = false;
        }
        else if (!fuelEmptyTriggered && (fuelEmptyDelayCounter += Time.deltaTime) >= fuelEmptyDelay)
        {
            fuelEmptyTriggered = true;
            Crash(eLanderState.OutOfFuel);
        }
        if (deadZoneTriggered && (deadZoneTimer -= Time.deltaTime) <= 0f)
            Crash(eLanderState.DeadZone);
    }

    void FixedUpdate()
    {
        if (IsTractorHeld) return;
        if (isActive && GameController.Instance.Phase == GameController.GamePhase.Countdown)
        {
            Park();
            hull.isTrigger = false;
            return;
        }
        if (!isActive || rb.bodyType != RigidbodyType2D.Dynamic) return;
        var gravity = GravityManager2D.Instance;
        float blend = 1f - Mathf.Exp(-gravity.gravitySmooth * Time.fixedDeltaTime);
        currentGravity = Vector2.Lerp(currentGravity, gravity.GetGravity(rb.position), blend);
        ApplySpaceTuning(gravity.GetZeroBlend(rb.position));
        rb.AddForce(currentGravity * (rb.mass * gravityScale), ForceMode2D.Force);
        if (!controlsEnabled)
        {
            StopThrust();
            return;
        }
        bool leftMoon = landerState == eLanderState.LandedMoon && !IsTouchingMoon;
        bool leftPad = landerState == eLanderState.LandedPad && padContacts.Count == 0
            && Time.time - lastPadContact >= 0.2f;
        if (leftMoon || leftPad)
        {
            landerState = eLanderState.Flying;
            GameController.Instance.BeginFlight();
            MoonEVAController.Instance.RefreshAction();
        }
        rb.linearVelocity = LimitFallVelocity(rb.linearVelocity, currentGravity * gravityScale,
            Mathf.Abs(maxFallSpeed), Time.fixedDeltaTime);
        Vector2 pointer = default;
        isThrusting = currentFuel > 0f && LanderUI.Instance.TryGetGameplayPointer(out pointer);
        if (isThrusting)
        {
            ApplyThrust(pointer);
            currentFuel = Mathf.Max(0f, currentFuel - fuelBurnPerSec * Time.fixedDeltaTime);
        }
        else steer01 = 0f;
        targetRotation += gravity.GetRotationAssist(transform, currentGravity) * Time.fixedDeltaTime;
        float rotationBlend = 1f - Mathf.Pow(1f - Mathf.Clamp01(rotationSmooth), Time.fixedDeltaTime / 0.02f);
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetRotation, rotationBlend));
        HandleThrustSound(isThrusting);
    }

    static Vector2 LimitFallVelocity(Vector2 velocity, Vector2 acceleration, float limit, float deltaTime)
    {
        if (acceleration.sqrMagnitude < 0.0001f) return velocity;
        Vector2 down = acceleration.normalized;
        float excess = Vector2.Dot(velocity, down) - limit;
        // Preserve incoming momentum when gravity starts or changes direction.
        if (excess > 0f)
            velocity -= down * Mathf.Min(excess, acceleration.magnitude * deltaTime);
        return velocity;
    }

    void ApplyThrust(Vector2 screenPosition)
    {
        if (!gameCamera) gameCamera = Camera.main;
        if (!gameCamera) return;
        Vector2 local = transform.InverseTransformPoint(gameCamera.ScreenToWorldPoint(screenPosition));
        float amount = Mathf.Clamp01((Mathf.Abs(local.x) - steeringDeadzone) / Mathf.Max(0.0001f, steeringRange));
        float steering = Mathf.Sign(local.x) * amount * Mathf.Clamp01(maxSteer);
        float blend = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime);
        steer01 = Mathf.Lerp(steer01, steering, blend);
        targetRotation -= steer01 * rotationSpeed * Time.fixedDeltaTime;
        rb.AddForce(transform.up * thrustForce, ForceMode2D.Force);
    }

    public bool IsSafeLanding(float speed, float verticalSpeed, float angle, bool moon, float tolerance = 1f)
        => speed <= safeSpeed * tolerance && (moon
            || (verticalSpeed <= safeVerticalSpeed * tolerance && angle <= safeAngleDeg * tolerance));

    void OnCollisionEnter2D(Collision2D collision) => HandleCollision(collision);

    void HandleCollision(Collision2D collision)
    {
        if (!isActive) return;
        bool moon = collision.collider.CompareTag("Moon");
        if (moon) RecordMoonContact(collision.collider);
        bool pad = collision.collider.CompareTag("LandingPad");
        if (pad) RecordPadContact(collision.collider);
        bool slippedOntoLandscape = landerState == eLanderState.LandedPad && collision.collider.CompareTag("Landscape");
        if (!slippedOntoLandscape && (landerState != eLanderState.Flying || !controlsEnabled)) return;
        Vector2 down = currentGravity.sqrMagnitude > 0.0001f ? currentGravity.normalized : Vector2.down;
        float speed = collision.relativeVelocity.magnitude;
        float vertical = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, down));
        float angle = Mathf.Abs(Mathf.DeltaAngle(0f, rb.rotation));
        var padPlacer = pad ? collision.collider.GetComponentInParent<LandingPadPlacer>() : null;
        var otherShip = collision.collider.GetComponentInParent<LanderController>();
        bool occupied = padPlacer && padPlacer.HasParkedShip(this);
        if ((moon || pad) && !occupied && IsSafeLanding(speed, vertical, angle, moon))
        {
            landerState = moon ? eLanderState.LandedMoon : eLanderState.LandedPad;
            StopThrust();
            GameController.Instance.HandleLanding(collision, moon);
            return;
        }
        float impact = Mathf.InverseLerp(1.5f, 10f, speed);
        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCrash,
            Mathf.Lerp(0.6f, 1f, impact), Mathf.Lerp(0.9f, 1.15f, impact));
        Crash(pad || (otherShip && otherShip.IsParkedOnPad) ? eLanderState.CrashedPad
            : moon ? eLanderState.CrashedMoon : eLanderState.CrashedLandscape);
    }

    void RecordMoonContact(Collider2D collider)
    {
        moonContacts.Add(collider);
        lastMoonContact = Time.time;
    }

    void RecordPadContact(Collider2D collider)
    {
        padContacts.Add(collider);
        lastPadContact = Time.time;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (isActive && landerState == eLanderState.LandedPad && collision.collider.CompareTag("Landscape"))
            HandleCollision(collision);
        if (isActive && collision.collider.CompareTag("Moon")) RecordMoonContact(collision.collider);
        if (isActive && collision.collider.CompareTag("LandingPad")) RecordPadContact(collision.collider);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Moon"))
        {
            moonContacts.Remove(collision.collider);
            lastMoonContact = Time.time;
        }
        if (collision.collider.CompareTag("LandingPad"))
        {
            padContacts.Remove(collision.collider);
            lastPadContact = Time.time;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive || !controlsEnabled || !other.CompareTag("DeadZone")) return;
        deadZones.Add(other);
        if (deadZoneTriggered) return;
        deadZoneTriggered = true;
        deadZoneTimer = deadZoneExplodeDelay;
        LanderUI.Instance.ShowHideDeadZoneWarning(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isActive || !other.CompareTag("DeadZone")) return;
        deadZones.Remove(other);
        if (deadZones.Count > 0) return;
        deadZoneTriggered = false;
        deadZoneTimer = deadZoneExplodeDelay;
        LanderUI.Instance.ShowHideDeadZoneWarning(false);
    }

    void Crash(eLanderState state)
    {
        if (!isActive || IsCrashed || (landerState != eLanderState.LandedPad
            && (landerState != eLanderState.Flying || !controlsEnabled))) return;
        landerState = state;
        controlsEnabled = false;
        StopThrust();
        deadZoneTriggered = false;
        deadZones.Clear();
        moonContacts.Clear();
        padContacts.Clear();
        spriteRenderer.enabled = false;
        hull.enabled = false;
        rb.bodyType = RigidbodyType2D.Static;
        if (crashEffect) Destroy(Instantiate(crashEffect, transform.position, Quaternion.identity), 10f);
        GameController.Instance.HandleCrash(state);
    }

    public void ApplySpaceTuning(float zeroT)
    {
        zeroT = Mathf.Clamp01(zeroT);
        rotationSpeed = baseRotationSpeed * Mathf.Lerp(1f, 2f, zeroT);
        rotationSmooth = baseRotationSmooth * Mathf.Lerp(1f, 2f, zeroT);
        thrustForce = baseThrustForce * Mathf.Lerp(1f, 0.7f, zeroT);
    }

    void StopThrust()
    {
        isThrusting = false;
        HandleThrustSound(false);
        if (thrustEffects == null) return;
        foreach (var effect in thrustEffects)
            if (effect) effect.localScale = new Vector3(effect.localScale.x, 0f, effect.localScale.z);
    }

    public void HandleThrustSound(bool thrusting)
    {
        if (!sfxThrustSound) return;
        if (thrusting && !sfxThrustSound.isPlaying) sfxThrustSound.Play();
        else if (!thrusting && sfxThrustSound.isPlaying) sfxThrustSound.Stop();
    }

    void UpdateThrustEffect(bool thrusting)
    {
        if (thrustEffects == null) return;
        float target = thrusting ? thrustMaxY : 0f;
        foreach (var effect in thrustEffects)
        {
            if (!effect) continue;
            var scale = effect.localScale;
            scale.y = Mathf.Lerp(scale.y, target, 1f - Mathf.Exp(-thrustGrowSpeed * Time.deltaTime));
            effect.localScale = scale;
        }
    }

    public void SetTractorHold(bool held)
    {
        if (IsTractorHeld == held || !rb) return;
        if (held)
        {
            constraintsBeforeTractor = rb.constraints;
            StopThrust();
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else rb.constraints = constraintsBeforeTractor;
        IsTractorHeld = held;
    }

    public void ResetLander()
    {
        SetTractorHold(false);
        if (!sfxThrustSound) sfxThrustSound = AudioManager.Instance.CreateThrusterSound();
        controlsEnabled = false;
        StopThrust();
        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        rb.bodyType = RigidbodyType2D.Static;
        rb.rotation = 0f;
        targetRotation = steer01 = fuelEmptyDelayCounter = 0f;
        landerState = eLanderState.None;
        fuelEmptyTriggered = deadZoneTriggered = false;
        deadZones.Clear();
        moonContacts.Clear();
        padContacts.Clear();
        lastMoonContact = float.NegativeInfinity;
        lastPadContact = float.NegativeInfinity;
        deadZoneTimer = deadZoneExplodeDelay;
        spriteRenderer.enabled = hull.enabled = true;
        hull.isTrigger = false;
        SetRandomPosition();
        currentGravity = GravityManager2D.Instance.GetGravity(transform.position);
        CalculateStartFuel(LandingPadPlacer.Instance.transform.position);
        MoonEVAController.Instance.RefreshAction();
    }

    public ShipSave CaptureState()
    {
        var state = new ShipSave
        {
            definitionId = landerIndex, fuel = currentFuel, fuelMax = fuelMax,
            fuelInitialized = fuelInitialized,
            state = landerState, bodyType = rb.bodyType, controlsEnabled = controlsEnabled,
            targetRotation = targetRotation, gravity = currentGravity,
            fuelEmptyTimer = fuelEmptyDelayCounter, deadZoneTriggered = deadZoneTriggered, deadZoneTimer = deadZoneTimer
        };
        state.Capture(transform, rb);
        return state;
    }

    public void RestoreState(ShipSave state, bool active)
    {
        Init();
        isActive = active;
        landerState = state.state;
        controlsEnabled = active && state.controlsEnabled && !IsCrashed;
        fuelMax = Mathf.Max(0.01f, state.fuelMax);
        currentFuel = Mathf.Clamp(state.fuel, 0f, fuelMax);
        fuelInitialized = state.fuelInitialized;
        targetRotation = state.targetRotation;
        currentGravity = state.gravity;
        fuelEmptyDelayCounter = Mathf.Max(0f, state.fuelEmptyTimer);
        fuelEmptyTriggered = false;
        deadZoneTriggered = state.deadZoneTriggered;
        deadZoneTimer = state.deadZoneTimer;
        rb.bodyType = active ? state.bodyType : RigidbodyType2D.Static;
        spriteRenderer.enabled = hull.enabled = !IsCrashed;
        hull.isTrigger = landerState != eLanderState.LandedPad && (!active
            || (state.bodyType == RigidbodyType2D.Static && landerState == eLanderState.LandedMoon));
        state.Restore(transform, rb);
        if (landerState == eLanderState.LandedMoon) lastMoonContact = Time.time;
        StopThrust();
    }

#if UNITY_EDITOR
    public void DebugPlace(Vector3 position, bool landedMoon)
    {
        ResetLander();
        transform.SetPositionAndRotation(position, Quaternion.identity);
        rb.position = position;
        rb.rotation = targetRotation = 0f;
        currentGravity = GravityManager2D.Instance.GetGravity(position);
        fuelMax = Mathf.Max(20f, fuelMax);
        currentFuel = fuelMax;
        StartLander();
        if (landedMoon)
        {
            landerState = eLanderState.LandedMoon;
            lastMoonContact = Time.time;
        }
        Physics2D.SyncTransforms();
    }
#endif

    public void StartLander()
    {
        if (!sfxThrustSound) sfxThrustSound = AudioManager.Instance.CreateThrusterSound();
        controlsEnabled = true;
        hull.isTrigger = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        landerState = eLanderState.Flying;
        targetRotation = rb.rotation;
    }

    public void SetRandomPosition()
    {
        var pad = LandingPadPlacer.Instance ? LandingPadPlacer.Instance : FindFirstObjectByType<LandingPadPlacer>();
        if (!pad) return;
        Vector2 padPosition = pad.transform.position;
        float levelFactor = GameController.Instance ? Mathf.Clamp(GameController.Instance.level, 1, 30) / 2f : 0.5f;
        float xRange = Mathf.Max(0f, maxDistanceXToPad) + levelFactor;
        float xMin = Mathf.Clamp(minDistanceXToPad, 0f, xRange);
        float yMin = minDistanceYToPad + levelFactor;
        float yMax = Mathf.Max(yMin, maxDistanceYToPad + levelFactor);
        var terrain = RandomLandscape.Instance;
        Vector2 position = padPosition;
        float hullRadius = Mathf.Max(hull.bounds.extents.x, hull.bounds.extents.y);
        Physics2D.SyncTransforms();
        for (int attempt = 0; attempt < Mathf.Max(1, tries); attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            position = padPosition + new Vector2(side * Random.Range(xMin, xRange), Random.Range(yMin, yMax));
            if (terrain && terrain.HasLayout)
            {
                position.x = terrain.ClampSpawnX(position.x, hullRadius + clearance);
                if (Mathf.Abs(position.x - padPosition.x) < xMin) continue;
                float routeHeight = terrain.GetHighestGround(Mathf.Min(position.x, padPosition.x) - hullRadius,
                    Mathf.Max(position.x, padPosition.x) + hullRadius);
                position.y = Mathf.Max(position.y, routeHeight + hullRadius + Mathf.Max(0f, clearance));
            }
            position.y = Mathf.Max(position.y, minWorldY);
            if (!IsColliding(position))
            {
                transform.position = position;
                return;
            }
        }
        position.x = padPosition.x;
        position.y = Mathf.Max(position.y, padPosition.y + yMax + 20f);
        for (int attempt = 0; attempt < 200 && IsColliding(position); attempt++) position.y += 2f;
        if (IsColliding(position)) Debug.LogWarning("Could not find a clear lander spawn.", this);
        transform.position = position;
    }

    public void CalculateStartFuel(Vector2 padPosition)
    {
        float levelFactor = GameController.Instance ? GameController.Instance.level / 30f : 0f;
        float buffer = Mathf.Max(0f, fuelBufferPercent - levelFactor);
        fuelMax = Mathf.Max(0.01f, Vector2.Distance(transform.position, padPosition) * fuelPerUnit * (1f + buffer));
        currentFuel = fuelMax;
        fuelInitialized = true;
    }

    bool IsColliding(Vector2 position)
    {
        var collider = hull ? hull : GetComponent<Collider2D>();
        if (!collider) return false;
        Vector2 center = position + (Vector2)(collider.bounds.center - transform.position);
        float radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) + clearance;
        var filter = new ContactFilter2D { useTriggers = true };
        Physics2D.OverlapCircle(center, radius, filter, spawnHits);
        foreach (var hit in spawnHits)
            if (hit && !hit.transform.IsChildOf(transform) && (!hit.isTrigger || hit.GetComponentInParent<LanderController>())) return true;
        return false;
    }

    void OnDestroy()
    {
        if (sfxThrustSound && AudioManager.Instance) AudioManager.Instance.ReleaseSound(sfxThrustSound);
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!isActive) return;
        var collider = GetComponent<Collider2D>();
        if (!collider) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(collider.bounds.center, Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) + clearance);
    }
#endif
}

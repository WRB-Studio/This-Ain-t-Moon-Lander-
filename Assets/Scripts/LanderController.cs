using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LanderController : MonoBehaviour
{
    public static LanderController Active { get; private set; }
    public static LanderController Instance => Active; // compatibility for existing project code
    public static event Action<LanderController> ActiveChanged;

    [Header("Identity / Unlock")]
    [SerializeField] string landerId;
    public int landerIndex = -1;
    public int unlockCost = 1000;
    public bool isSecretLander;
    public bool isActive;

    public string LanderId => string.IsNullOrWhiteSpace(landerId) ? $"lander-{landerIndex}" : landerId;

    [Header("States")]
    public bool controlsEnabled;
    public enum eLanderState { None, Flying, LandedPad, LandedMoon, CrashedLandscape, CrashedMoon, CrashedPad, OutOfFuel, DeadZone }
    public eLanderState landerState = eLanderState.None;

    [Header("Respawn (relative to LandingPad)")]
    public float maxDistanceXToPad = 6f;
    public float minDistanceYToPad = 6f;
    public float maxDistanceYToPad = 12f;
    public float minWorldY = -2f;
    public float clearance = 0.2f;
    public int tries = 30;

    [Header("Lander movement settings")]
    public float thrustForce = 9.5f;
    public float maxFallSpeed = -6f;
    public float currentSpeed;

    [Header("Ship capabilities")]
    [Min(0.1f)] public float fuelCapacityMultiplier = 1f;

    [Header("Mobile Steering")]
    [Tooltip("Maximale Neigung bei vorhandener Gravitation.")]
    [Range(5f, 80f)] public float maxGravityTilt = 60f;

    [Tooltip("Seitlicher Totbereich um das Schiff als Anteil der Bildschirmbreite.")]
    [Range(0f, 0.2f)] public float steeringDeadzoneScreen = 0.04f;

    [Tooltip("Ab diesem seitlichen Touch-Abstand wird die maximale Neigung erreicht (Anteil der Bildschirmbreite).")]
    [Range(0.05f, 0.5f)] public float fullSteerScreen = 0.24f;

    public float gravityTurnSpeed = 100f;
    public float zeroGTurnSpeed = 150f;
    public float steerResponse = 10f;
    public float zeroGSteeringThreshold = 0.35f;
    public float autoLevelSpeed = 55f;

    [Header("Fuel")]
    public float fuelMax = 3.5f;
    public float fuelBurnPerSec = 1f;
    public float currentFuel;
    public float fuelPerUnit = 0.25f;
    [Range(0f, 1f)] public float fuelBufferPercent = 0.4f;
    public float fuelEmptyDelay = 5f;

    [Header("Landing Rules")]
    public float safeSpeed = 2f;
    public float safeAngleDeg = 10f;
    public float safeVerticalSpeed = 1.5f;

    [Header("Dead Zone rules")]
    public float deadZoneExplodeDelay = 5f;
    [HideInInspector] public bool deadZoneTriggered;
    [HideInInspector] public float deadZoneTimer;

    [Header("FX")]
    public GameObject crashEffect;

    [Header("Thrust effect")]
    [HideInInspector] public List<Transform> thrustEffects;
    [SerializeField] float thrustGrowSpeed = 6f;
    [SerializeField] float thrustMaxY = 1f;

    [HideInInspector] public Rigidbody2D rb;

    public Vector2 CurrentGravity { get; private set; }
    public bool IsThrusting => isThrusting;
    public bool IsMoonContact => moonContact;
    public float Fuel01 => fuelMax <= 0f ? 0f : Mathf.Clamp01(currentFuel / fuelMax);

    static AudioSource sfxThrustSound;

    float baseThrustForce;
    float targetRotation;
    float steer01;
    float fuelEmptyDelayCounter;
    bool fuelEmptyTriggered;
    bool isThrusting;
    bool moonContact;

    readonly List<RaycastResult> uiRaycastResults = new();
    PointerEventData pointerEventData;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        if (isActive)
        {
            SetActiveLander(this);
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Static;
            Collider2D col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;
        }
    }

    void OnDestroy()
    {
        if (Active != this) return;
        Active = null;
        ActiveChanged?.Invoke(null);
    }

    public void Init()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        targetRotation = rb.rotation;
        steer01 = 0f;
        moonContact = false;
        CurrentGravity = GravityManager2D.Instance
            ? GravityManager2D.Instance.GetGravityAt(rb.position)
            : Vector2.zero;

        RefreshThrustEffects();
        RefreshBaseStats();
    }

    static void SetActiveLander(LanderController lander)
    {
        if (Active == lander) return;
        Active = lander;
        ActiveChanged?.Invoke(lander);
    }

    public static void ChangeLander(LanderController newLander)
    {
        LanderController old = Active;
        if (!old || !newLander || old == newLander) return;

        old.isActive = false;
        old.controlsEnabled = false;
        old.isThrusting = false;
        old.HandleThrustSound(false);

        Rigidbody2D oldRb = old.rb ? old.rb : old.GetComponent<Rigidbody2D>();
        oldRb.bodyType = RigidbodyType2D.Static;
        Collider2D oldCol = old.GetComponent<Collider2D>();
        if (oldCol) oldCol.isTrigger = true;

        newLander.isActive = true;
        SetActiveLander(newLander);
        newLander.Init();

        newLander.controlsEnabled = true;
        newLander.rb.bodyType = RigidbodyType2D.Dynamic;
        newLander.landerState = eLanderState.LandedMoon;
        Collider2D newCol = newLander.GetComponent<Collider2D>();
        if (newCol) newCol.isTrigger = false;

        if (newLander.currentFuel <= 0f)
            newLander.currentFuel = newLander.fuelMax * 0.75f;
        else
            newLander.currentFuel = Mathf.Min(newLander.currentFuel, newLander.fuelMax);
    }

    public void ApplyConfigurationFrom(LanderController preset)
    {
        if (!preset || preset == this) return;

        landerId = preset.landerId;
        landerIndex = preset.landerIndex;
        unlockCost = preset.unlockCost;
        isSecretLander = preset.isSecretLander;

        maxDistanceXToPad = preset.maxDistanceXToPad;
        minDistanceYToPad = preset.minDistanceYToPad;
        maxDistanceYToPad = preset.maxDistanceYToPad;
        minWorldY = preset.minWorldY;
        clearance = preset.clearance;
        tries = preset.tries;

        thrustForce = preset.thrustForce;
        maxFallSpeed = preset.maxFallSpeed;
        fuelCapacityMultiplier = preset.fuelCapacityMultiplier;

        maxGravityTilt = preset.maxGravityTilt;
        steeringDeadzoneScreen = preset.steeringDeadzoneScreen;
        fullSteerScreen = preset.fullSteerScreen;
        gravityTurnSpeed = preset.gravityTurnSpeed;
        zeroGTurnSpeed = preset.zeroGTurnSpeed;
        steerResponse = preset.steerResponse;
        zeroGSteeringThreshold = preset.zeroGSteeringThreshold;
        autoLevelSpeed = preset.autoLevelSpeed;

        fuelBurnPerSec = preset.fuelBurnPerSec;
        fuelPerUnit = preset.fuelPerUnit;
        fuelBufferPercent = preset.fuelBufferPercent;
        fuelEmptyDelay = preset.fuelEmptyDelay;

        safeSpeed = preset.safeSpeed;
        safeAngleDeg = preset.safeAngleDeg;
        safeVerticalSpeed = preset.safeVerticalSpeed;
        deadZoneExplodeDelay = preset.deadZoneExplodeDelay;

        crashEffect = preset.crashEffect;
        thrustGrowSpeed = preset.thrustGrowSpeed;
        thrustMaxY = preset.thrustMaxY;

        RefreshBaseStats();
    }

    public void RefreshThrustEffects()
    {
        thrustEffects ??= new List<Transform>();
        thrustEffects.Clear();

        foreach (Transform child in transform)
            if (child.name.Contains("ThrustEffect"))
                thrustEffects.Add(child);
    }

    void RefreshBaseStats()
    {
        baseThrustForce = thrustForce;
    }

    void Update()
    {
        if (!isActive) return;

        UpdateFuelEmptyLogic();
        UpdateDeadZone();
    }

    void FixedUpdate()
    {
        if (!isActive || !rb) return;

        UpdateGravity();
        currentSpeed = rb.linearVelocity.magnitude;
        UpdateControls();
        ApplyPhysics();
        UpdateThrustEffect(isThrusting);

        if (rb.bodyType == RigidbodyType2D.Dynamic && CurrentGravity.sqrMagnitude > 0.0001f)
            rb.AddForce(CurrentGravity * rb.mass, ForceMode2D.Force);

        if (!isThrusting || rb.bodyType != RigidbodyType2D.Dynamic) return;

        rb.AddForce(transform.up * thrustForce, ForceMode2D.Force);
        BurnFuel();
    }

    void UpdateGravity()
    {
        GravityManager2D gm = GravityManager2D.Instance;
        if (!gm)
        {
            CurrentGravity = Vector2.zero;
            return;
        }

        Vector2 targetGravity = gm.GetGravityAt(rb.position);
        float k = 1f - Mathf.Exp(-gm.gravitySmooth * Time.fixedDeltaTime);
        CurrentGravity = Vector2.Lerp(CurrentGravity, targetGravity, k);

        gm.zeroBlend = gm.GetSpaceBlendAt(rb.position);
        ApplySpaceTuning(gm.zeroBlend);
    }

    void UpdateControls()
    {
        if (!controlsEnabled)
        {
            isThrusting = false;
            HandleThrustSound(false);
            return;
        }

        bool hasTouch = TouchControl(out Vector2 screenPos);
        isThrusting = hasTouch && currentFuel > 0f;

        if (isThrusting)
            UpdateSteeringTarget(screenPos);
        else
            UpdateAutoLevel();

        HandleThrustSound(isThrusting);
    }

    void ApplyPhysics()
    {
        if (rb.bodyType == RigidbodyType2D.Dynamic)
            ClampFallSpeed();

        if (!controlsEnabled || landerState != eLanderState.Flying || rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        float turnSpeed = HasSteeringGravity() ? gravityTurnSpeed : zeroGTurnSpeed;
        float nextRotation = Mathf.MoveTowardsAngle(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(nextRotation);
    }

    void ClampFallSpeed()
    {
        if (CurrentGravity.sqrMagnitude < 0.0001f) return;

        Vector2 velocity = rb.linearVelocity;
        Vector2 down = CurrentGravity.normalized;
        float downSpeed = Vector2.Dot(velocity, down);
        float maxDownSpeed = Mathf.Abs(maxFallSpeed);

        if (downSpeed > maxDownSpeed)
            velocity -= down * (downSpeed - maxDownSpeed);

        rb.linearVelocity = velocity;
    }

    bool TouchControl(out Vector2 screenPos)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            return !IsPointerOverInteractiveUI(screenPos);
        }
#endif

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                screenPos = touch.position;
                return !IsPointerOverInteractiveUI(screenPos);
            }
        }

        screenPos = default;
        return false;
    }

    bool IsPointerOverInteractiveUI(Vector2 screenPos)
    {
        EventSystem eventSystem = EventSystem.current;
        if (!eventSystem) return false;

        pointerEventData ??= new PointerEventData(eventSystem);
        pointerEventData.position = screenPos;

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults)
            if (result.gameObject && result.gameObject.GetComponentInParent<Selectable>())
                return true;

        return false;
    }

    void UpdateSteeringTarget(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (!cam) return;

        if (HasSteeringGravity())
        {
            Vector2 gravityUp = -CurrentGravity.normalized;
            Vector2 gravityRight = new Vector2(gravityUp.y, -gravityUp.x);

            Vector2 shipScreen = cam.WorldToScreenPoint(transform.position);
            Vector2 rightScreen = cam.WorldToScreenPoint(transform.position + (Vector3)gravityRight);
            Vector2 rightScreenDir = rightScreen - shipScreen;
            if (rightScreenDir.sqrMagnitude < 0.0001f) return;
            rightScreenDir.Normalize();

            float lateral = Vector2.Dot(screenPos - shipScreen, rightScreenDir) / Mathf.Max(1f, Screen.width);
            float abs = Mathf.Abs(lateral);

            float input = 0f;
            if (abs > steeringDeadzoneScreen)
            {
                float t = Mathf.InverseLerp(
                    steeringDeadzoneScreen,
                    Mathf.Max(steeringDeadzoneScreen + 0.001f, fullSteerScreen),
                    abs);

                input = Mathf.Clamp01(t) * Mathf.Sign(lateral);
            }

            float k = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime);
            steer01 = Mathf.Lerp(steer01, input, k);

            float upright = GravityUpRotation(CurrentGravity);
            targetRotation = upright - steer01 * maxGravityTilt;
            return;
        }

        Vector2 shipScreenPos = cam.WorldToScreenPoint(transform.position);
        float aimDeadzone = Screen.width * steeringDeadzoneScreen;
        if ((screenPos - shipScreenPos).sqrMagnitude <= aimDeadzone * aimDeadzone) return;

        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        Vector2 dir = (Vector2)world - rb.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        steer01 = 0f;
        targetRotation = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
    }

    void UpdateAutoLevel()
    {
        if (!HasSteeringGravity()) return;

        float k = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime);
        steer01 = Mathf.Lerp(steer01, 0f, k);

        float upright = GravityUpRotation(CurrentGravity);
        targetRotation = Mathf.MoveTowardsAngle(targetRotation, upright, autoLevelSpeed * Time.fixedDeltaTime);
    }

    bool HasSteeringGravity()
        => CurrentGravity.sqrMagnitude >= zeroGSteeringThreshold * zeroGSteeringThreshold;

    static float GravityUpRotation(Vector2 gravity)
    {
        Vector2 up = -gravity.normalized;
        return Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
    }

    void UpdateThrustEffect(bool thrusting)
    {
        if (thrustEffects == null) return;

        bool show = landerState == eLanderState.Flying && thrusting;
        foreach (Transform effect in thrustEffects)
        {
            if (!effect) continue;

            Vector3 scale = effect.localScale;
            scale.y = Mathf.Lerp(scale.y, show ? thrustMaxY : 0f, thrustGrowSpeed * Time.fixedDeltaTime);
            effect.localScale = scale;
        }
    }

    public void HandleThrustSound(bool thrusting)
    {
        if (!sfxThrustSound) return;

        if (landerState == eLanderState.LandedMoon)
        {
            sfxThrustSound.Stop();
            return;
        }

        if (thrusting && !sfxThrustSound.isPlaying)
            sfxThrustSound.Play();
        else if (!thrusting && sfxThrustSound.isPlaying)
            sfxThrustSound.Stop();
    }

    void BurnFuel()
    {
        currentFuel = Mathf.Max(0f, currentFuel - fuelBurnPerSec * Time.fixedDeltaTime);
    }

    void UpdateFuelEmptyLogic()
    {
        if (currentFuel > 0f)
        {
            fuelEmptyDelayCounter = 0f;
            fuelEmptyTriggered = false;
            return;
        }

        if (fuelEmptyTriggered) return;

        fuelEmptyDelayCounter += Time.deltaTime;
        if (fuelEmptyDelayCounter < fuelEmptyDelay) return;

        fuelEmptyTriggered = true;
        Crash(eLanderState.OutOfFuel);
    }

    void UpdateDeadZone()
    {
        if (!deadZoneTriggered) return;

        deadZoneTimer -= Time.deltaTime;
        if (deadZoneTimer > 0f) return;

        deadZoneTriggered = false;
        Crash(eLanderState.DeadZone);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        bool isMoon = col.collider.CompareTag("Moon");
        if (isMoon) moonContact = true;

        if (landerState != eLanderState.Flying) return;

        bool isLandingPad = col.collider.CompareTag("LandingPad");
        bool isLandscape = col.collider.CompareTag("Landscape");

        Vector2 relVel = col.relativeVelocity;
        float impactSpeed = relVel.magnitude;

        Vector2 downDir = CurrentGravity.sqrMagnitude > 0.0001f ? CurrentGravity.normalized : Vector2.down;
        float verticalImpact = Mathf.Abs(Vector2.Dot(relVel, downDir));

        float angle = isMoon && CurrentGravity.sqrMagnitude > 0.0001f
            ? Mathf.Abs(Mathf.DeltaAngle(GravityUpRotation(CurrentGravity), rb.rotation))
            : Mathf.Abs(Mathf.DeltaAngle(0f, rb.rotation));

        bool okSpeed = impactSpeed <= safeSpeed;
        bool okAngle = angle <= safeAngleDeg;
        bool okVertical = verticalImpact <= safeVerticalSpeed;

        if (isLandingPad && okSpeed && okAngle && okVertical)
        {
            Land(col, eLanderState.LandedPad);
        }
        else if (isMoon && okSpeed)
        {
            LandOnMoon(col);
        }
        else if (isLandingPad)
        {
            PlayCrashImpact(col);
            Crash(eLanderState.CrashedPad);
        }
        else if (isMoon)
        {
            PlayCrashImpact(col);
            Crash(eLanderState.CrashedMoon);
        }
        else
        {
            PlayCrashImpact(col);
            Crash(isLandscape ? eLanderState.CrashedLandscape : eLanderState.CrashedLandscape);
        }
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (col.collider.CompareTag("Moon"))
            moonContact = true;
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (!col.collider.CompareTag("Moon")) return;

        moonContact = false;
        if (landerState == eLanderState.LandedMoon)
            landerState = eLanderState.Flying;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (deadZoneTriggered || !other.CompareTag("DeadZone")) return;

        deadZoneTriggered = true;
        deadZoneTimer = deadZoneExplodeDelay;
        LanderUI.Instance.ShowHideDeadZoneWarning(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("DeadZone")) return;

        deadZoneTriggered = false;
        deadZoneTimer = deadZoneExplodeDelay;
        LanderUI.Instance.ShowHideDeadZoneWarning(false);
    }

    void Land(Collision2D col, eLanderState state)
    {
        landerState = state;
        controlsEnabled = false;
        isThrusting = false;
        HandleThrustSound(false);

        ScoringController.Instance.CalculateScore(col);
        LanderUI.Instance.ShowGameOver(landerState);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: 1.2f);
        ImpactFX.Instance.PlayImpactEffect(landerState);
    }

    void LandOnMoon(Collision2D col)
    {
        landerState = eLanderState.LandedMoon;
        moonContact = true;

        MoonEVAController eva = MoonEVAController.Instance;
        if (!eva.isOnMoonLanded)
        {
            eva.isOnMoonLanded = true;
            controlsEnabled = false;
            ScoringController.Instance.CalculateScore(col);
            LanderUI.Instance.ShowGameOver(landerState, true);
        }
        else
        {
            LanderUI.Instance.SetPanelBottomCenter();
        }

        ImpactFX.Instance.PlayImpactEffect(landerState);
    }

    void Crash(eLanderState state)
    {
        landerState = state;
        controlsEnabled = false;
        isThrusting = false;

        ImpactFX.Instance.PlayImpactEffect(landerState);
        HandleThrustSound(false);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer) spriteRenderer.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        rb.bodyType = RigidbodyType2D.Static;

        if (crashEffect)
            Destroy(Instantiate(crashEffect, transform.position, Quaternion.identity), 10f);

        LanderUI.Instance.ShowGameOver(landerState);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: -0.8f);
    }

    void PlayCrashImpact(Collision2D col)
    {
        float t = Mathf.InverseLerp(1.5f, 10f, col.relativeVelocity.magnitude);
        float volume = Mathf.Lerp(0.6f, 1f, t);
        float pitch = Mathf.Lerp(0.9f, 1.15f, t);
        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCrash, volume, pitch, false);
    }

    public void ApplySpaceTuning(float zeroT)
    {
        float thrustMultiplier = Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(zeroT));
        thrustForce = baseThrustForce * thrustMultiplier;
    }

    public void ResetLander()
    {
        if (!rb) Init();
        if (!sfxThrustSound && AudioManager.Instance)
            sfxThrustSound = AudioManager.Instance.CreateThrusterSound();

        HandleThrustSound(false);
        controlsEnabled = false;
        isThrusting = false;

        rb.bodyType = RigidbodyType2D.Static;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        transform.rotation = Quaternion.identity;
        targetRotation = 0f;
        steer01 = 0f;
        moonContact = false;
        fuelEmptyDelayCounter = 0f;
        fuelEmptyTriggered = false;
        deadZoneTriggered = false;
        deadZoneTimer = deadZoneExplodeDelay;

        foreach (Transform effect in thrustEffects)
            if (effect) effect.localScale = new Vector3(effect.localScale.x, 0f, effect.localScale.z);

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer) spriteRenderer.enabled = true;

        Collider2D col = GetComponent<Collider2D>();
        if (col)
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        SetRandomPosition();
        CalculateStartFuel(LandingPadPlacer.Instance.transform.position);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: 1f);
    }

    public void StartLander()
    {
        if (!sfxThrustSound && AudioManager.Instance)
            sfxThrustSound = AudioManager.Instance.CreateThrusterSound();

        targetRotation = 0f;
        steer01 = 0f;
        transform.rotation = Quaternion.identity;
        controlsEnabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        landerState = eLanderState.Flying;

        ScoringController.Instance.BeginRun();
    }

    public void SetRandomPosition()
    {
        LandingPadPlacer pad = LandingPadPlacer.Instance;
        if (!pad) pad = FindFirstObjectByType<LandingPadPlacer>();
        if (!pad) return;

        Vector2 padPos = pad.transform.position;
        float levelFactor = GameController.Instance ? GameController.Instance.level / 2f : 0f;

        float xRange = maxDistanceXToPad + levelFactor;
        float yMin = minDistanceYToPad + levelFactor;
        float yMax = maxDistanceYToPad + levelFactor;

        Vector2 candidate = new Vector2(
            padPos.x + UnityEngine.Random.Range(-xRange, xRange),
            Mathf.Max(padPos.y + UnityEngine.Random.Range(yMin, yMax), minWorldY));

        int safety = 0;
        while (IsColliding(candidate) && safety++ < 200)
            candidate.y += 2f;

        if (IsColliding(candidate))
            candidate = new Vector2(padPos.x, padPos.y + yMax + 20f);

        transform.position = candidate;
    }

    public void CalculateStartFuel(Vector2 padPos)
    {
        float dist = Vector2.Distance(transform.position, padPos);
        float levelFactor = GameController.Instance ? GameController.Instance.level / 30f : 0f;
        float buffer = Mathf.Max(0f, fuelBufferPercent - levelFactor);

        fuelMax = dist * fuelPerUnit * (1f + buffer) * Mathf.Max(0.1f, fuelCapacityMultiplier);
        currentFuel = fuelMax;
    }

    bool IsColliding(Vector2 worldPos)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col) return false;

        Vector2 centerOffset = (Vector2)(col.bounds.center - transform.position);
        Vector2 center = worldPos + centerOffset;
        Vector2 extents = col.bounds.extents;
        float radius = Mathf.Max(extents.x, extents.y) + clearance;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (Collider2D hit in hits)
        {
            if (!hit || hit.transform == transform || hit.isTrigger) continue;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!isActive) return;

        Collider2D col = GetComponent<Collider2D>();
        if (!col) return;

        Vector2 offset = (Vector2)(col.bounds.center - transform.position);
        Vector2 extents = col.bounds.extents;
        float radius = Mathf.Max(extents.x, extents.y) + clearance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere((Vector2)transform.position + offset, radius);
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LanderController : MonoBehaviour
{
    public static LanderController Instance;

    public int landerIndex = -1;
    public int unlockCost = 1000;
    public bool isSecretLander = false;
    public bool isActive = false;

    [Header("States")]
    public bool controlsEnabled;
    public enum eLanderState { None, Flying, LandedPad, LandedMoon, CrashedLandscape, CrashedMoon, CrashedPad, OutOfFuel, DeadZone };
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

    [Header("Mobile Steering")]
    [Tooltip("Maximale Neigung bei vorhandener Gravitation. Touch links/rechts setzt einen absoluten Zielwinkel statt endlos weiterzudrehen.")]
    [Range(5f, 80f)] public float maxGravityTilt = 60f;

    [Tooltip("Seitlicher Totbereich um das Schiff als Anteil der Bildschirmbreite.")]
    [Range(0f, 0.2f)] public float steeringDeadzoneScreen = 0.04f;

    [Tooltip("Ab diesem seitlichen Touch-Abstand wird die maximale Neigung erreicht (Anteil der Bildschirmbreite).")]
    [Range(0.05f, 0.5f)] public float fullSteerScreen = 0.24f;

    [Tooltip("Drehgeschwindigkeit bei vorhandener Gravitation in Grad/Sekunde.")]
    public float gravityTurnSpeed = 100f;

    [Tooltip("Drehgeschwindigkeit im Zero-G in Grad/Sekunde.")]
    public float zeroGTurnSpeed = 150f;

    [Tooltip("Wie weich die seitliche Touch-Eingabe reagiert.")]
    public float steerResponse = 10f;

    [Tooltip("Unterhalb dieser Gravitationsstärke wird direkt in Richtung Touch gezielt (360° Zero-G-Steuerung).")]
    public float zeroGSteeringThreshold = 0.35f;

    [Tooltip("Wie schnell sich der Lander ohne Touch bei Gravitation wieder aufrichtet.")]
    public float autoLevelSpeed = 55f;

    float baseThrustForce;
    bool baseCached;
    float steer01;

    [Header("Fuel")]
    public float fuelMax = 3.5f;
    public float fuelBurnPerSec = 1.0f;
    public float currentFuel;
    public float fuelPerUnit = 0.25f;
    [Range(0f, 1f)] public float fuelBufferPercent = 0.4f;
    public float fuelEmptyDelay = 5f;
    private float fuelEmptyDelayCounter = 0;
    private bool fuelEmptyTriggered;

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
    private static AudioSource sfxThrustSound;

    [HideInInspector] public Rigidbody2D rb;

    float targetRotation;
    bool isThrusting;

    readonly List<RaycastResult> uiRaycastResults = new();
    PointerEventData pointerEventData;

    void Awake()
    {
        if (!isActive)
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }
        else
        {
            Instance = this;
        }
    }

    public void Init()
    {
        rb = GetComponent<Rigidbody2D>();
        targetRotation = rb.rotation;
        steer01 = 0f;

        thrustEffects ??= new List<Transform>();
        thrustEffects.Clear();
        foreach (Transform t in transform)
            if (t.name.Contains("ThrustEffect"))
                thrustEffects.Add(t);

        if (!baseCached)
        {
            baseThrustForce = thrustForce;
            baseCached = true;
        }

        thrustForce = baseThrustForce;
    }

    public static void ChangeLander(LanderController newLander)
    {
        var old = Instance;
        if (old == null || newLander == null || old == newLander) return;

        old.isActive = false;
        old.controlsEnabled = false;

        var oldRb = old.GetComponent<Rigidbody2D>();
        oldRb.bodyType = RigidbodyType2D.Static;
        old.GetComponent<Collider2D>().isTrigger = true;

        Instance = newLander;
        newLander.isActive = true;
        newLander.Init();

        newLander.controlsEnabled = true;
        newLander.rb.bodyType = RigidbodyType2D.Dynamic;
        newLander.landerState = eLanderState.LandedMoon;
        newLander.GetComponent<Collider2D>().isTrigger = false;

        newLander.fuelMax = old.fuelMax;
        newLander.currentFuel = newLander.fuelMax * 0.75f;

        CameraController.Instance.SetTarget(newLander.transform, instantFocus: true);
    }

    void UpdateThrustEffect(bool thrusting)
    {
        if (landerState != eLanderState.Flying)
        {
            foreach (Transform t in thrustEffects)
                t.localScale = new Vector3(t.localScale.x, 0f, t.localScale.z);
            return;
        }

        foreach (Transform t in thrustEffects)
        {
            Vector3 s = t.localScale;
            float targetY = thrusting ? thrustMaxY : 0f;
            s.y = Mathf.Lerp(s.y, targetY, thrustGrowSpeed * Time.fixedDeltaTime);
            t.localScale = s;
        }
    }

    void Update()
    {
        if (!isActive) return;

        UpdateFuelEmptyLogic();
        UpdateDeadZone();
    }

    void FixedUpdate()
    {
        if (!isActive) return;

        currentSpeed = rb.linearVelocity.magnitude;

        if (controlsEnabled)
        {
            bool hasTouch = TouchControl(out var screenPos);
            isThrusting = hasTouch && currentFuel > 0f;

            if (isThrusting)
                UpdateSteeringTarget(screenPos);
            else
                UpdateAutoLevel();

            HandleThrustSound(isThrusting);
        }
        else
        {
            isThrusting = false;
            HandleThrustSound(false);
        }

        ApplyPhysics();
        UpdateThrustEffect(isThrusting);

        if (!isThrusting) return;

        rb.AddForce(transform.up * thrustForce, ForceMode2D.Force);
        BurnFuel();
    }

    void ApplyPhysics()
    {
        Vector2 v = rb.linearVelocity;
        if (v.y < maxFallSpeed) v.y = maxFallSpeed;
        if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = v;

        if (!controlsEnabled || landerState != eLanderState.Flying || rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        float turnSpeed = HasSteeringGravity() ? gravityTurnSpeed : zeroGTurnSpeed;
        float nextRotation = Mathf.MoveTowardsAngle(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(nextRotation);
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
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                screenPos = t.position;
                return !IsPointerOverInteractiveUI(screenPos);
            }
        }

        screenPos = default;
        return false;
    }

    bool IsPointerOverInteractiveUI(Vector2 screenPos)
    {
        var eventSystem = EventSystem.current;
        if (!eventSystem) return false;

        pointerEventData ??= new PointerEventData(eventSystem);
        pointerEventData.position = screenPos;

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, uiRaycastResults);

        foreach (var result in uiRaycastResults)
            if (result.gameObject && result.gameObject.GetComponentInParent<Selectable>())
                return true;

        return false;
    }

    void UpdateSteeringTarget(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (!cam) return;

        Vector2 gravity = Physics2D.gravity;

        if (HasSteeringGravity())
        {
            Vector2 gravityUp = -gravity.normalized;
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
                float t = Mathf.InverseLerp(steeringDeadzoneScreen, Mathf.Max(steeringDeadzoneScreen + 0.001f, fullSteerScreen), abs);
                input = Mathf.Clamp01(t) * Mathf.Sign(lateral);
            }

            float k = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime);
            steer01 = Mathf.Lerp(steer01, input, k);

            float upright = GravityUpRotation(gravity);
            targetRotation = upright - steer01 * maxGravityTilt;
            return;
        }

        Vector2 shipScreen = cam.WorldToScreenPoint(transform.position);
        float aimDeadzone = Screen.width * steeringDeadzoneScreen;
        if ((screenPos - shipScreen).sqrMagnitude <= aimDeadzone * aimDeadzone) return;

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

        float upright = GravityUpRotation(Physics2D.gravity);
        targetRotation = Mathf.MoveTowardsAngle(targetRotation, upright, autoLevelSpeed * Time.fixedDeltaTime);
    }

    bool HasSteeringGravity()
        => Physics2D.gravity.sqrMagnitude >= zeroGSteeringThreshold * zeroGSteeringThreshold;

    float GravityUpRotation(Vector2 gravity)
    {
        Vector2 up = -gravity.normalized;
        return Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
    }

    public void HandleThrustSound(bool thrusting)
    {
        if (sfxThrustSound == null) return;

        if (landerState == eLanderState.LandedMoon)
        {
            sfxThrustSound.Stop();
            return;
        }

        if (thrusting && !sfxThrustSound.isPlaying)
        {
            sfxThrustSound.Play();
        }
        else if (!thrusting && sfxThrustSound.isPlaying)
        {
            sfxThrustSound.Stop();
        }
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
        }
        else if (!fuelEmptyTriggered && (fuelEmptyDelayCounter += Time.deltaTime) >= fuelEmptyDelay)
        {
            fuelEmptyTriggered = true;
            Crash(eLanderState.OutOfFuel);
        }
    }

    void UpdateDeadZone()
    {
        if (!deadZoneTriggered) return;

        if ((deadZoneTimer -= Time.deltaTime) <= 0f)
        {
            deadZoneTriggered = false;
            Crash(eLanderState.DeadZone);
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (landerState != eLanderState.Flying || landerState == eLanderState.LandedMoon) return;

        bool isLandingPad = col.collider.CompareTag("LandingPad");
        bool isLandscape = col.collider.CompareTag("Landscape");
        bool isMoon = col.collider.CompareTag("Moon");

        Vector2 relVel = col.relativeVelocity;
        float impactSpeed = relVel.magnitude;

        Vector2 g = Physics2D.gravity;
        Vector2 downDir = (g.sqrMagnitude > 0.0001f) ? g.normalized : Vector2.down;
        float vImpact = Mathf.Abs(Vector2.Dot(relVel, downDir));

        float angle;
        if (isMoon && g.sqrMagnitude > 0.0001f)
        {
            float desiredUpAngle = Mathf.Atan2((-downDir).y, (-downDir).x) * Mathf.Rad2Deg - 90f;
            angle = Mathf.Abs(Mathf.DeltaAngle(desiredUpAngle, rb.rotation));
        }
        else
        {
            angle = Mathf.Abs(Mathf.DeltaAngle(0f, rb.rotation));
        }

        bool okSpeed = impactSpeed <= safeSpeed;
        bool okAngle = angle <= safeAngleDeg;
        bool okVert = vImpact <= safeVerticalSpeed;

        bool nicePadLanding = okSpeed && okAngle && okVert;
        bool niceMoonLanding = okSpeed;

        if (isLandingPad && nicePadLanding)
        {
            Land(col, eLanderState.LandedPad);
        }
        else if (isMoon && niceMoonLanding)
        {
            LandOnMoon(col, eLanderState.LandedMoon);
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
        else if (isLandscape)
        {
            PlayCrashImpact(col);
            Crash(eLanderState.CrashedLandscape);
        }
        else
        {
            PlayCrashImpact(col);
            Crash(eLanderState.CrashedLandscape);
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (landerState == eLanderState.LandedMoon && col.collider.CompareTag("Moon"))
        {
            landerState = eLanderState.Flying;
            MoonEVAController.Instance.btnExit.gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (deadZoneTriggered) return;
        if (!other.CompareTag("DeadZone")) return;

        LanderUI.Instance.ShowHideDeadZoneWarning(true);
        deadZoneTriggered = true;
        deadZoneTimer = deadZoneExplodeDelay;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("DeadZone")) return;

        LanderUI.Instance.ShowHideDeadZoneWarning(false);
        deadZoneTriggered = false;
        deadZoneTimer = deadZoneExplodeDelay;
    }

    void Land(Collision2D col, eLanderState state)
    {
        landerState = state;
        controlsEnabled = false;

        HandleThrustSound(false);

        ScoringController.Instance.CalculateScore(col);
        LanderUI.Instance.ShowGameOver(landerState);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: 1.2f);
        ImpactFX.Instance.PlayImpactEffect(landerState);
    }

    void LandOnMoon(Collision2D col, eLanderState state)
    {
        landerState = state;

        if (!MoonEVAController.Instance.isOnMoonLanded)
        {
            MoonEVAController.Instance.isOnMoonLanded = true;
            controlsEnabled = false;
            ScoringController.Instance.CalculateScore(col);
            LanderUI.Instance.ShowGameOver(landerState, true);
        }
        else
        {
            LanderUI.Instance.SetPanelBottomCenter();
            MoonEVAController.Instance.btnExit.gameObject.SetActive(true);
        }

        ImpactFX.Instance.PlayImpactEffect(landerState);
    }

    void Crash(eLanderState state)
    {
        landerState = state;
        controlsEnabled = false;

        ImpactFX.Instance.PlayImpactEffect(landerState);
        HandleThrustSound(false);

        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;
        rb.bodyType = RigidbodyType2D.Static;

        Destroy(Instantiate(crashEffect, transform.position, Quaternion.identity), 10f);

        LanderUI.Instance.ShowGameOver(landerState);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: -0.8f);
    }

    void PlayCrashImpact(Collision2D col)
    {
        float impact = col.relativeVelocity.magnitude;
        float t = Mathf.InverseLerp(1.5f, 10f, impact);
        float vol = Mathf.Lerp(0.6f, 1.0f, t);
        float pitch = Mathf.Lerp(0.9f, 1.15f, t);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCrash, vol, pitch, false);
    }

    public void ApplySpaceTuning(float zeroT)
    {
        float thrustMul = Mathf.Lerp(1f, 0.7f, zeroT);
        thrustForce = baseThrustForce * thrustMul;
    }

    public void ResetLander()
    {
        if (!sfxThrustSound) sfxThrustSound = AudioManager.Instance.CreateThrusterSound();

        HandleThrustSound(false);
        controlsEnabled = false;

        rb.bodyType = RigidbodyType2D.Static;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        targetRotation = 0f;
        steer01 = 0f;
        deadZoneTimer = deadZoneExplodeDelay;

        foreach (Transform t in thrustEffects)
            t.localScale = new Vector3(t.localScale.x, 0f, t.localScale.z);

        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<Collider2D>().enabled = true;

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
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        controlsEnabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        landerState = eLanderState.Flying;

        ScoringController.Instance.BeginRun();
    }

    public void SetRandomPosition()
    {
        var pad = LandingPadPlacer.Instance;
        if (!pad) pad = FindFirstObjectByType<LandingPadPlacer>();

        Vector2 padPos = pad.transform.position;
        float levelFactor = GameController.Instance.level / 2f;

        float xRange = maxDistanceXToPad + levelFactor;
        float yMin = minDistanceYToPad + levelFactor;
        float yMax = maxDistanceYToPad + levelFactor;

        float x = padPos.x + Random.Range(-xRange, xRange);
        float y = padPos.y + Random.Range(yMin, yMax);
        y = Mathf.Max(y, minWorldY);

        Vector2 newRandomPosition = new Vector2(x, y);

        int safety = 0;
        while (IsColliding(newRandomPosition) && safety < 200)
        {
            newRandomPosition.y += 2f;
            safety++;
        }

        if (IsColliding(newRandomPosition))
            newRandomPosition = new Vector2(padPos.x, padPos.y + yMax + 20f);

        transform.position = newRandomPosition;
    }

    public void CalculateStartFuel(Vector2 padPos)
    {
        float dist = Vector2.Distance(transform.position, padPos);
        float levelFactor = GameController.Instance.level / 30f;
        float buffer = Mathf.Max(0f, fuelBufferPercent - levelFactor);

        fuelMax = dist * fuelPerUnit * (1f + buffer);
        currentFuel = fuelMax;
    }

    bool IsColliding(Vector2 worldPos)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (!col) return false;

        Vector2 centerOffset = (Vector2)(col.bounds.center - transform.position);
        Vector2 center = worldPos + centerOffset;
        Vector2 ext = col.bounds.extents;
        float radius = Mathf.Max(ext.x, ext.y) + clearance;

        var hits = Physics2D.OverlapCircleAll(center, radius);
        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.transform == transform) continue;
            if (h.isTrigger) continue;

            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!isActive) return;

        Collider2D col = GetComponent<Collider2D>();
        Vector2 offset = col ? (Vector2)(col.bounds.center - transform.position) : Vector2.zero;
        Vector2 ext = col.bounds.extents;
        float radius = Mathf.Max(ext.x, ext.y) + clearance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere((Vector2)transform.position + offset, radius);
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;

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

    public float minWorldY = -2f;              // harter Mindest-Y (optional)
    public float clearance = 0.2f;             // Abstand zum Boden
    public int tries = 30;

    [Header("Lander movement settings")]
    public float thrustForce = 9.5f;
    public float rotationSpeed = 180f;
    public float rotationSmooth = 0.12f;
    public float maxFallSpeed = -6f;
    public float currentSpeed;

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

    private float targetRotation;
    private bool isThrusting;


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

        // thrustEffects NICHT stapeln
        thrustEffects ??= new List<Transform>();
        thrustEffects.Clear();
        foreach (Transform t in transform)
            if (t.name.Contains("ThrustEffect"))
                thrustEffects.Add(t);

        // Base nur EINMAL speichern (nicht nach SpaceTuning!)
        if (!baseCached)
        {
            baseRotationSpeed = rotationSpeed;
            baseRotationSmooth = rotationSmooth;
            baseThrustForce = thrustForce;
            baseCached = true;
        }

        // Optional: nach Init direkt auf Base resetten
        rotationSpeed = baseRotationSpeed;
        rotationSmooth = baseRotationSmooth;
        thrustForce = baseThrustForce;
    }

    public static void ChangeLander(LanderController newLander)
    {
        var old = Instance;
        if (old == null || newLander == null || old == newLander) return;

        // OLD OFF
        old.isActive = false;
        old.controlsEnabled = false;

        var oldRb = old.GetComponent<Rigidbody2D>();
        oldRb.bodyType = RigidbodyType2D.Static;
        old.GetComponent<Collider2D>().isTrigger = true;

        // NEW ON
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

            s.y = Mathf.Lerp(s.y, targetY, thrustGrowSpeed * Time.deltaTime);
            t.localScale = s;
        }

    }

    private void Update()
    {
        if (!isActive) return;

        UpdateFuelEmptyLogic();
        UpdateDeadZone();
    }

    void FixedUpdate()
    {
        if (!isActive) return;

        currentSpeed = rb.linearVelocity.magnitude;

        UpdateThrustEffect(isThrusting);

        ApplyPhysics();

        if (!controlsEnabled) return;
        isThrusting = TouchControll(out var pos) && currentFuel > 0f;
        HandleThrustSound(isThrusting);

        if (!isThrusting) return;
        ApplyThrust(pos);
        BurnFuel();
    }

    void ApplyPhysics()
    {
        Vector2 v = rb.linearVelocity;
        if (v.y < maxFallSpeed) v.y = maxFallSpeed;
        if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = v;

        float smoothedRot = Mathf.LerpAngle(rb.rotation, targetRotation, rotationSmooth);
        rb.MoveRotation(smoothedRot);
    }

    bool TouchControll(out Vector2 screenPos)
    {
        if (LanderUI.Instance.IsPointerOverUI())
        {
            screenPos = default;
            return false;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }
#endif

        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                screenPos = t.position;
                return true;
            }
        }

        screenPos = default;
        return false;
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


    void ApplyThrust(Vector2 screenPos)
    {
        Vector3 w3 = Camera.main.ScreenToWorldPoint(screenPos);
        Vector2 worldTouch = new Vector2(w3.x, w3.y);

        // Touch relativ zum Schiff (dreht mit)
        Vector2 local = transform.InverseTransformPoint(worldTouch);

        // Analog: local.x -> [-1..+1]
        float raw = local.x;

        // Deadzone rausrechnen
        float sign = Mathf.Sign(raw);
        float abs = Mathf.Abs(raw);

        float x = 0f;
        if (abs > steeringDeadzone)
        {
            float a = abs - steeringDeadzone;
            x = Mathf.Clamp01(a / Mathf.Max(0.0001f, steeringRange));
            x *= sign; // zurück auf -/+ Seite
        }

        // optional smoothing (fühlt sich weniger zappelig an)
        float k = 1f - Mathf.Exp(-steerResponse * Time.fixedDeltaTime);
        steer01 = Mathf.Lerp(steer01, x, k);

        // Drehgeschwindigkeit proportional
        targetRotation += (-steer01) * rotationSpeed * Time.fixedDeltaTime;

        // Schub wie gehabt
        rb.AddForce(transform.up * thrustForce, ForceMode2D.Force);
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

        // Impact speed (verlässlich im Collision-Frame)
        Vector2 relVel = col.relativeVelocity;
        float impactSpeed = relVel.magnitude;

        // "Down" entlang aktueller Gravitation (nicht world-y)
        Vector2 g = Physics2D.gravity;
        Vector2 downDir = (g.sqrMagnitude > 0.0001f) ? g.normalized : Vector2.down;

        // Vertikal-Impact relativ zur Gravity
        float vImpact = Mathf.Abs(Vector2.Dot(relVel, downDir));

        // Angle: auf Pad/Landscape world-up, auf Moon besser gravity-up
        float angle;
        if (isMoon && g.sqrMagnitude > 0.0001f)
        {
            // desired up = gegen gravity
            float desiredUpAngle = Mathf.Atan2((-downDir).y, (-downDir).x) * Mathf.Rad2Deg - 90f; // ggf. Offset anpassen
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
        bool niceMoonLanding = okSpeed; // wie vorher: Moon nur Speed

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
            // Fallback für alles andere
            PlayCrashImpact(col);
            Crash(eLanderState.CrashedLandscape);
        }
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (landerState == eLanderState.LandedMoon &&
            col.collider.CompareTag("Moon"))
        {
            landerState = eLanderState.Flying;
            MoonEVAController.Instance.btnExit.gameObject.SetActive(false);
        }

    }


    void OnTriggerEnter2D(Collider2D other)
    {
        if (deadZoneTriggered) return;
        if (!other.CompareTag("DeadZone")) return; // Tag auf deine Trigger setzen

        LanderUI.Instance.ShowHideDeadZoneWarning(true);
        deadZoneTriggered = true;
        deadZoneTimer = deadZoneExplodeDelay;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("DeadZone")) return;

        // optional: wenn rausfliegt, Countdown abbrechen
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

        // Tuning
        float minImpact = 1.5f;
        float maxImpact = 10f;

        float t = Mathf.InverseLerp(minImpact, maxImpact, impact); // 0..1
        float vol = Mathf.Lerp(0.6f, 1.0f, t);
        float pitch = Mathf.Lerp(0.9f, 1.15f, t);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCrash, vol, pitch, false);
    }

    public void ApplySpaceTuning(float zeroT)
    {
        // Faktoren aus deinem alten Switch:
        float rotMul = Mathf.Lerp(1f, 2f, zeroT);
        float smoothMul = Mathf.Lerp(1f, 2f, zeroT);
        float thrustMul = Mathf.Lerp(1f, 0.7f, zeroT);

        rotationSpeed = baseRotationSpeed * rotMul;
        rotationSmooth = baseRotationSmooth * smoothMul;
        thrustForce = baseThrustForce * thrustMul;
    }

    public void ResetLander()
    {
        if (!sfxThrustSound) sfxThrustSound = AudioManager.Instance.CreateThrusterSound();

        HandleThrustSound(false);

        controlsEnabled = false;

        rb.bodyType = RigidbodyType2D.Static;
        //rb.linearVelocity = Vector2.zero;
        //rb.angularVelocity = 0f;

        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        targetRotation = 0f;
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

        // Falls Kollision: so lange nach oben schieben, bis frei
        int safety = 0;
        while (IsColliding(newRandomPosition) && safety < 200)
        {
            newRandomPosition.y += 2f;
            safety++;
        }

        if (IsColliding(newRandomPosition))
        {
            // Fallback: ganz sicher oberhalb des Pads
            newRandomPosition = new Vector2(padPos.x, padPos.y + yMax + 20f);
        }

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

        // Kreis um Collider-Center (relativ zum gewünschten worldPos)
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

            return true; // irgendein Collider blockt
        }
        return false;
    }


#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!isActive) return;
        // Shows the area that must be free on spawn
        Collider2D col = GetComponent<Collider2D>();
        Vector2 offset = col ? (Vector2)(col.bounds.center - transform.position) : Vector2.zero;

        Vector2 ext = col.bounds.extents;
        float radius = Mathf.Max(ext.x, ext.y) + clearance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere((Vector2)transform.position + offset, radius);
    }
#endif


}
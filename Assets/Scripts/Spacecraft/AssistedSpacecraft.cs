using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D))]
public class AssistedSpacecraft : MonoBehaviour
{
    public string displayName;
    public ShipDockingPad homePad;
    [Tooltip("Abstand vom Schiffszentrum zu den Landefüßen.")]
    public float footOffset = -1f;
    [Tooltip("Richtung der Nase in der Grafik: 0 = rechts, 90 = oben.")]
    public float forwardAngle;
    [Tooltip("Maximales Flugtempo in alle Richtungen.")]
    public float maxSpeed = 10f;
    [Tooltip("Beschleunigung beim Steuern.")]
    public float acceleration = 8f;
    [Tooltip("Bremskraft beim Loslassen.")]
    public float braking = 12f;
    [Tooltip("Seitlicher Schub beim Bremsen und Richtungswechsel.")]
    public float maneuverAcceleration = 7f;
    [Tooltip("Drehgeschwindigkeit in Grad pro Sekunde.")]
    public float turnSpeed = 160f;
    [Tooltip("Automatisches Bremsen und Ausgleichen der Schwerkraft.")]
    public bool flightAssist = true;
    [Tooltip("Aufstiegstempo, bis das Schiff sicher über dem Pad ist.")]
    [Min(.1f)] public float takeoffSpeed = 3f;
    [Tooltip("Zusätzlicher Abstand zum Pad vor dem Drehen.")]
    [Min(0f)] public float takeoffClearance = .5f;
    [Tooltip("Maximales seitliches Tempo beim Landeanflug.")]
    [Min(0f)]
    public float approachSpeed = 3f;
    [Tooltip("Maximales Sinktempo der Landehilfe.")]
    [Min(0f)]
    public float approachDescentSpeed = 0.8f;
    [Tooltip("Höchstes erlaubtes Tempo beim Aufsetzen.")]
    [Min(0f)]
    public float safeLandingSpeed = 1.8f;
    [Tooltip("Erlaubte Abweichung zur Pad-Ausrichtung in Grad.")]
    [Range(0f, 90f)]
    public float safeLandingAngle = 14f;
    [Tooltip("Ab diesem Aufpralltempo wird das Schiff beschädigt.")]
    [Min(0f)]
    public float damagingImpactSpeed = 5f;
    public float fuelCapacity = 100f;
    public float fuelBurnPerSecond = 0.6f;
    public float gravityFuelPerSecond = 0.12f;
    public SpriteRenderer[] thrustIndicators;
    Rigidbody2D body;
    Vector2 input;
    float turnInput;
    bool brakeRequested;
    float undockUntil;
    ShipDockingPad departurePad;
    float rotationRadius;
    ClassicLanderFlight classicFlight;
    bool pointerInput;
    Vector2 pointerScreen;
    public ClassicLanderFlight ClassicFlight => GetComponent<ClassicLanderFlight>();
    public bool IsClassic => ClassicFlight;
    public bool Controlled { get; private set; }
    public bool Crashed { get; private set; }
    public float Fuel { get; private set; }
    public float Speed => Body.linearVelocity.magnitude;
    public ShipDockingPad DockedPad { get; private set; }
    public ShipDockingPad ApproachPad { get; private set; }
    public Rigidbody2D Body { get { if (!body) body = GetComponent<Rigidbody2D>(); return body; } }
    void Awake()
    {
        Body.gravityScale = 0f; body.bodyType = RigidbodyType2D.Static;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Fuel = fuelCapacity;
        classicFlight = ClassicFlight;
        foreach (var collider in GetComponentsInChildren<Collider2D>())
        {
            if (collider.isTrigger || !collider.enabled) continue;
            Bounds bounds = collider.bounds;
            Vector2 offset = (Vector2)bounds.center - body.position;
            Vector2 corner = new Vector2(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) + (Vector2)bounds.extents;
            rotationRadius = Mathf.Max(rotationRadius, corner.magnitude);
        }
    }
    public void SetControl(bool value) { Controlled = value; input = Vector2.zero; turnInput = 0f; brakeRequested = false; pointerInput = false; }
    public void SetInput(Vector2 movement, float rotation, bool brake = false, Vector2? pointer = null)
    {
        input = Vector2.ClampMagnitude(movement, 1f); turnInput = rotation; brakeRequested = brake;
        pointerInput = pointer.HasValue; pointerScreen = pointer.GetValueOrDefault();
    }
    public void ResetAtHome()
    {
        if (DockedPad) DockedPad.Release(this);
        DockedPad = null; departurePad = null; undockUntil = 0f; Crashed = false; Fuel = fuelCapacity;
        input = Vector2.zero; turnInput = 0f;
        if (homePad && homePad.AvailableFor(this)) Dock(homePad);
    }
    bool Dock(ShipDockingPad pad)
    {
        if (!pad.Claim(this)) return false;
        DockedPad = pad; ApproachPad = pad; departurePad = null;
        body.linearVelocity = Vector2.zero; body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Static;
        body.rotation = pad.transform.eulerAngles.z;
        body.position = (Vector2)pad.dockPoint.position - pad.Up * footOffset + pad.Up * 0.03f;
        transform.SetPositionAndRotation(body.position, Quaternion.Euler(0f, 0f, body.rotation));
        if (classicFlight) classicFlight.ResetRotation(body.rotation);
        return true;
    }
    void Launch()
    {
        Vector2 up = DockedPad.Up;
        departurePad = classicFlight ? null : DockedPad;
        DockedPad.Release(this); DockedPad = null; ApproachPad = null;
        body.bodyType = RigidbodyType2D.Dynamic;
        if (classicFlight)
        {
            body.linearVelocity = Vector2.zero; classicFlight.ResetRotation(body.rotation);
            undockUntil = Time.time + .25f;
        }
        else
        {
            body.position += up * 0.3f; body.linearVelocity = up * Mathf.Min(takeoffSpeed, 1.5f);
            undockUntil = Time.time + 1f;
        }
    }
    void FixedUpdate()
    {
        if (Crashed) { ShowThrust(Vector2.zero); return; }
        if (DockedPad)
        {
            Fuel = Mathf.Min(fuelCapacity, Fuel + DockedPad.refillPerSecond * Time.fixedDeltaTime);
            ShowThrust(Vector2.zero);
            bool start = classicFlight ? classicFlight.ThrustRequested(input, pointerInput) : input.sqrMagnitude > .01f;
            if (Controlled && Fuel > 0f && start) Launch();
            else return;
        }
        if (body.bodyType != RigidbodyType2D.Dynamic) return;
        Vector2 gravity = ShipDockingPad.GetGravity(body.position);
        if (classicFlight)
        {
            ApproachPad = null;
            bool thrusting = classicFlight.Step(gravity, pointerInput, pointerScreen, Controlled, Fuel);
            Fuel = classicFlight.Controller.currentFuel;
            ShowThrust(thrusting ? (Vector2)transform.up : Vector2.zero);
            return;
        }
        body.AddForce(gravity * body.mass);
        if (departurePad && Vector2.Dot(body.position - (Vector2)departurePad.dockPoint.position, departurePad.Up) >= rotationRadius + takeoffClearance)
        {
            departurePad = null;
            undockUntil = Time.time + .25f;
        }
        bool departing = departurePad;
        bool assisted = flightAssist || departing || (Controlled && brakeRequested);
        ApproachPad = assisted && !departing && Time.time >= undockUntil ? ShipDockingPad.NearestApproach(body.position, this) : null;
        Vector2 steering = Controlled ? input : Vector2.zero;
        if (departing && steering.sqrMagnitude > .01f) steering = departurePad.Up;
        if (Fuel <= 0f) { ShowThrust(Vector2.zero); return; }
        Vector2 thrust = Vector2.zero;
        float rotation = body.rotation;
        if (assisted)
        {
            Vector2 targetVelocity = steering * maxSpeed;
            if (departing) targetVelocity = steering * takeoffSpeed;
            if (ApproachPad)
            {
                Vector2 up = ApproachPad.Up, right = ApproachPad.transform.right;
                float lateral = Mathf.Clamp(Vector2.Dot(targetVelocity, right), -approachSpeed, approachSpeed);
                float vertical = Vector2.Dot(targetVelocity, up);
                targetVelocity = right * lateral + up * Mathf.Max(vertical, -approachDescentSpeed);
            }
            Vector2 delta = (targetVelocity - body.linearVelocity) / Time.fixedDeltaTime;
            float strength = steering.sqrMagnitude > 0.01f ? acceleration : braking;
            if (steering.sqrMagnitude > 0.01f)
            {
                Vector2 along = steering.normalized;
                delta = along * Mathf.Clamp(Vector2.Dot(delta, along), -braking, strength)
                    + Vector2.ClampMagnitude(delta - along * Vector2.Dot(delta, along), maneuverAcceleration);
            }
            else delta = Vector2.ClampMagnitude(delta, strength);
            thrust = Vector2.ClampMagnitude(delta - gravity, strength + maneuverAcceleration);
        }
        else thrust = steering * acceleration;
        body.AddForce(thrust * body.mass);
        if (departing) rotation = departurePad.transform.eulerAngles.z;
        else if (Controlled && Mathf.Abs(turnInput) > 0.01f) rotation += turnInput * turnSpeed * Time.fixedDeltaTime;
        else if (assisted && ApproachPad)
            rotation = Mathf.MoveTowardsAngle(rotation, ApproachPad.transform.eulerAngles.z, turnSpeed * Time.fixedDeltaTime);
        else if (assisted && steering.sqrMagnitude > 0.01f)
            rotation = Mathf.MoveTowardsAngle(rotation, Mathf.Atan2(steering.y, steering.x) * Mathf.Rad2Deg - forwardAngle, turnSpeed * Time.fixedDeltaTime);
        float rotationEffort = Mathf.Abs(Mathf.DeltaAngle(body.rotation, rotation)) / Mathf.Max(.01f, turnSpeed * Time.fixedDeltaTime);
        body.angularVelocity = 0f; body.MoveRotation(rotation);
        float effort = Mathf.Clamp01(Mathf.Max(thrust.magnitude / Mathf.Max(0.01f, acceleration), rotationEffort * .15f));
        Fuel = Mathf.Max(0f, Fuel - (fuelBurnPerSecond * effort + (assisted && gravity.sqrMagnitude > 0f ? gravityFuelPerSecond : 0f)) * Time.fixedDeltaTime);
        ShowThrust(thrust);
    }
    void ShowThrust(Vector2 accelerationVector)
    {
        Vector2 local = transform.InverseTransformVector(accelerationVector);
        for (int i = 0; thrustIndicators != null && i < thrustIndicators.Length; i++)
            if (thrustIndicators[i]) thrustIndicators[i].enabled = i == 0 ? local.x > 0.2f : i == 1 ? local.x < -0.2f : i == 2 ? local.y > 0.2f : local.y < -0.2f;
    }
    void OnCollisionEnter2D(Collision2D collision) => HandleContact(collision);
    void OnCollisionStay2D(Collision2D collision) => HandleContact(collision);
    void HandleContact(Collision2D collision)
    {
        if (DockedPad || Crashed) return;
        var pad = collision.collider.GetComponent<ShipDockingPad>();
        float impact = collision.relativeVelocity.magnitude;
        if (pad && !departurePad && pad.AvailableFor(this) && Time.time >= undockUntil
            && impact <= safeLandingSpeed && Mathf.Abs(Mathf.DeltaAngle(body.rotation, pad.transform.eulerAngles.z)) <= safeLandingAngle
            && (!classicFlight || Mathf.Abs(Vector2.Dot(collision.relativeVelocity, pad.Up)) <= classicFlight.safeVerticalSpeed))
        {
            for (int i = 0; i < collision.contactCount; i++)
                if (Vector2.Dot(collision.GetContact(i).normal, pad.Up) > 0.65f) { Dock(pad); return; }
        }
        if (impact > damagingImpactSpeed || (classicFlight && Time.time >= undockUntil))
        {
            Crashed = true; input = Vector2.zero; body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f; body.bodyType = RigidbodyType2D.Static; ShowThrust(Vector2.zero);
        }
    }
    void OnDisable() { if (DockedPad) DockedPad.Release(this); }
}

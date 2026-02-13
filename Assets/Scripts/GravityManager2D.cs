using UnityEngine;

public class GravityManager2D : MonoBehaviour
{
    public static GravityManager2D Instance;

    [Header("Refs")]
    Transform lander => LanderController.Instance.transform;
    Rigidbody2D landerRb => LanderController.Instance.GetComponent<Rigidbody2D>();

    [Header("Base Gravity (always down)")]
    public Vector2 baseGravity = new Vector2(0f, -9.81f);

    [Header("Moon Gravity Gradient")]
    public float moonEnterRadius = 18f;   // ab hier beginnt Mondgrav (0)
    public float moonFullRadius = 10f;   // ab hier volle Mondgrav (1)
    public float moonGravityStrength = 9f;

    [Header("Altitude Zero-G Gradient (World Y)")]
    public float zeroGStartY = 40f;
    public float zeroGFullY = 60f;

    [Header("Smoothing")]
    public float gravitySmooth = 6f;

    [Header("Auto Rotation Assist")]
    [Range(0f, 1f)] public float rotationAssist = 0.25f;
    public float maxAssistTorque = 4f;
    public float deadZoneDeg = 1.5f;

    [HideInInspector] public float zeroBlend;

    Vector2 currentG;

    void Awake() => Instance = this;

    public void Init()
    {
        currentG = baseGravity;
        Physics2D.gravity = currentG;
    }

    void FixedUpdate()
    {
        if (!lander || !landerRb) return;

        float moonT = CalcMoonT();
        float zeroT = CalcZeroT();

        // Base bleibt immer "nach unten", Zero-G reduziert nur die Base (nicht den Mond)
        Vector2 baseG = Vector2.Lerp(baseGravity, Vector2.zero, zeroT);

        // Mond zieht radial zur Mitte
        Vector2 moonG = CalcMoonGravity(moonT);

        Vector2 targetG = baseG + moonG;

        zeroBlend = zeroT; // fürs Space-Tuning: wie "leer" die Weltgrav ist
        LanderController.Instance.ApplySpaceTuning(zeroBlend);

        ApplyWorldGravitySmooth(targetG);

        // Assist nur wenn Mond spürbar ist
        ApplyRotationAssist(moonT);
    }

    float CalcMoonT()
    {
        float dist = Vector2.Distance(lander.position, transform.position);
        return Mathf.Clamp01(Mathf.InverseLerp(moonEnterRadius, moonFullRadius, dist));
    }

    float CalcZeroT()
    {
        float y = lander.position.y;
        return Mathf.Clamp01(Mathf.InverseLerp(zeroGStartY, zeroGFullY, y));
    }

    Vector2 CalcMoonGravity(float moonT)
    {
        if (moonT <= 0f) return Vector2.zero;

        Vector2 moonDir = ((Vector2)transform.position - (Vector2)lander.position).normalized;
        return moonDir * (moonGravityStrength * moonT);
    }

    void ApplyWorldGravitySmooth(Vector2 targetG)
    {
        float k = 1f - Mathf.Exp(-gravitySmooth * Time.fixedDeltaTime);
        currentG = Vector2.Lerp(currentG, targetG, k);
        Physics2D.gravity = currentG;
    }

    void ApplyRotationAssist(float moonT)
    {
        if (moonT <= 0f) return;
        if (currentG.sqrMagnitude < 0.0001f) return;

        Vector2 desiredUp = (-currentG).normalized;
        Vector2 currentUp = lander.up;

        float angleError = Vector2.SignedAngle(currentUp, desiredUp);
        if (Mathf.Abs(angleError) < deadZoneDeg) return;

        float torque = Mathf.Clamp(angleError * rotationAssist * moonT, -maxAssistTorque, maxAssistTorque);
        landerRb.AddTorque(torque, ForceMode2D.Force);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, moonEnterRadius);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, moonFullRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-9999, zeroGStartY, 0), new Vector3(9999, zeroGStartY, 0));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-9999, zeroGFullY, 0), new Vector3(9999, zeroGFullY, 0));
    }
}

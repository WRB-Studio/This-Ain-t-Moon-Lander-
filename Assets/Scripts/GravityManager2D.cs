using UnityEngine;

public class GravityManager2D : MonoBehaviour
{
    public static GravityManager2D Instance;

    [Header("Refs")]
    Transform lander;
    Rigidbody2D landerRb;

    [Header("Base Gravity (Earth-like)")]
    public Vector2 baseGravity = new Vector2(0f, -9.81f);

    [Header("Moon Gravity Gradient")]
    public float moonEnterRadius = 18f; // 0% Mondgrav
    public float moonFullRadius = 10f; // 100% Mondgrav
    public float moonGravityStrength = 9f;

    [Header("Altitude Zero-G Gradient (World Y)")]
    public float zeroGStartY = 40f; // ab hier beginnt Gravity->0
    public float zeroGFullY = 60f; // ab hier Gravity=0

    [Header("Smoothing")]
    public float gravitySmooth = 6f;

    [Header("Auto Rotation Assist")]
    [Range(0f, 1f)] public float rotationAssist = 0.25f;
    public float maxAssistTorque = 4f;
    public float deadZoneDeg = 1.5f;

    [HideInInspector] public float zeroBlend;

    Vector2 currentG;

    void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        currentG = baseGravity;
        Physics2D.gravity = currentG;

        lander = LanderController.Instance.transform;
        landerRb = lander.GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (!lander || !landerRb) return;

        float moonT;
        Vector2 gAfterMoon = CalcGravityAfterMoon(out moonT);
                
        Vector2 targetG = ApplyZeroG(gAfterMoon, moonT, out zeroBlend);

        LanderController.Instance.ApplySpaceTuning(zeroBlend);

        ApplyWorldGravitySmooth(targetG);

        ApplyRotationAssist(moonT);
    }

    Vector2 CalcGravityAfterMoon(out float moonT)
    {
        float distToMoon = Vector2.Distance(lander.position, transform.position);
        moonT = Mathf.Clamp01(Mathf.InverseLerp(moonEnterRadius, moonFullRadius, distToMoon));

        Vector2 moonDir = ((Vector2)transform.position - (Vector2)lander.position).normalized;
        Vector2 moonG = moonDir * moonGravityStrength;

        return Vector2.Lerp(baseGravity, moonG, moonT);
    }

    Vector2 ApplyZeroG(Vector2 gAfterMoon, float moonT, out float zeroBlend)
    {
        float y = lander.position.y;
        float zeroT = Mathf.Clamp01(Mathf.InverseLerp(zeroGStartY, zeroGFullY, y));

        zeroBlend = zeroT * (1f - moonT); // Mond schlägt ZeroG
        return Vector2.Lerp(gAfterMoon, Vector2.zero, zeroBlend);
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
        // Mondradien
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, moonEnterRadius);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, moonFullRadius);

        // Höhenlinien
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-9999, zeroGStartY, 0), new Vector3(9999, zeroGStartY, 0));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-9999, zeroGFullY, 0), new Vector3(9999, zeroGFullY, 0));
    }
}

using UnityEngine;

public class GravityManager2D : MonoBehaviour
{
    public static GravityManager2D Instance;

    [Header("Refs")]
    Transform lander => LanderController.Instance.transform;

    [Header("Base Gravity (always down)")]
    public Vector2 baseGravity = new Vector2(0f, -9.81f);

    [Header("Moon Gravity Gradient")]
    public float moonEnterRadius = 18f;
    public float moonFullRadius = 10f;
    public float moonGravityStrength = 9f;

    [Header("Altitude Zero-G Gradient (World Y)")]
    public float zeroGStartY = 40f;
    public float zeroGFullY = 60f;

    [Header("Smoothing")]
    public float gravitySmooth = 6f;

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
        if (!lander) return;

        float moonT = CalcMoonT();
        float zeroT = CalcZeroT();

        Vector2 baseG = Vector2.Lerp(baseGravity, Vector2.zero, zeroT);
        Vector2 moonG = CalcMoonGravity(moonT);
        Vector2 targetG = baseG + moonG;

        zeroBlend = zeroT;
        LanderController.Instance.ApplySpaceTuning(zeroBlend);

        ApplyWorldGravitySmooth(targetG);
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

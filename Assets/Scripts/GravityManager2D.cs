using UnityEngine;

public class GravityManager2D : MonoBehaviour
{
    public static GravityManager2D Instance;

    [Header("Base Gravity")]
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

    void Awake() => Instance = this;

    public void Init()
    {
        // Gravity is now evaluated per controlled body. This keeps future ships,
        // astronauts and celestial bodies from fighting over one global vector.
        Physics2D.gravity = Vector2.zero;
    }

    public Vector2 GetGravityAt(Vector2 worldPosition)
    {
        float moonT = GetMoonBlendAt(worldPosition);
        if (moonT > 0f)
        {
            Vector2 toMoon = (Vector2)transform.position - worldPosition;
            if (toMoon.sqrMagnitude < 0.0001f) return Vector2.zero;

            return toMoon.normalized * (moonGravityStrength * moonT);
        }

        float zeroT = GetZeroGravityBlendAt(worldPosition);
        return Vector2.Lerp(baseGravity, Vector2.zero, zeroT);
    }

    public float GetMoonBlendAt(Vector2 worldPosition)
    {
        float dist = Vector2.Distance(worldPosition, transform.position);
        return Mathf.Clamp01(Mathf.InverseLerp(moonEnterRadius, moonFullRadius, dist));
    }

    public float GetZeroGravityBlendAt(Vector2 worldPosition)
        => Mathf.Clamp01(Mathf.InverseLerp(zeroGStartY, zeroGFullY, worldPosition.y));

    public float GetSpaceBlendAt(Vector2 worldPosition)
    {
        // Moon gravity owns its area completely. Base/zero-G gravity is only used
        // outside the Moon's influence, so both gravity fields are never added.
        if (GetMoonBlendAt(worldPosition) > 0f) return 0f;
        return GetZeroGravityBlendAt(worldPosition);
    }

    public bool IsMoonGravityActiveAt(Vector2 worldPosition)
        => GetMoonBlendAt(worldPosition) > 0f;

    public bool IsZeroGravityAt(Vector2 worldPosition, float threshold = 0.99f)
        => !IsMoonGravityActiveAt(worldPosition) && GetZeroGravityBlendAt(worldPosition) >= threshold;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, moonEnterRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, moonFullRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-9999f, zeroGStartY, 0f), new Vector3(9999f, zeroGStartY, 0f));

        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-9999f, zeroGFullY, 0f), new Vector3(9999f, zeroGFullY, 0f));
    }
}

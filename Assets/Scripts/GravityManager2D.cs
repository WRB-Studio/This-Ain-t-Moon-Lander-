using UnityEngine;
using UnityEngine.Serialization;

public class GravityManager2D : MonoBehaviour
{
    public static GravityManager2D Instance;
    [Header("Base Gravity")]
    public Vector2 baseGravity = new Vector2(0f, -9.81f);
    [Header("Moon Gravity Gradient")]
    public float moonEnterRadius = 18f;
    public float moonFullRadius = 10f;
    public float moonGravityStrength = 9f;
    [Header("Altitude Zero-G Gradient")]
    public float zeroGStartY = 40f;
    public float zeroGFullY = 60f;
    [Header("Smoothing")]
    public float gravitySmooth = 6f;
    [Header("Auto Rotation Assist")]
    [Range(0f, 1f)] public float rotationAssist = 0.25f;
    [FormerlySerializedAs("maxAssistTorque")]
    [Tooltip("Maximum automatic rotation correction in degrees per second; this is not physical torque.")]
    [Min(0f)] public float maxAssistSpeed = 4f;
    public float deadZoneDeg = 1.5f;

    void Awake() => Instance = this;
    public void Init() => Physics2D.gravity = baseGravity;

    public float GetMoonBlend(Vector2 position)
        => Mathf.InverseLerp(moonEnterRadius, moonFullRadius, Vector2.Distance(position, transform.position));

    public float GetZeroBlend(Vector2 position)
        => Mathf.InverseLerp(zeroGStartY, zeroGFullY, position.y);

    public Vector2 GetGravity(Vector2 position)
    {
        Vector2 toMoon = (Vector2)transform.position - position;
        return baseGravity * (1f - GetZeroBlend(position))
            + toMoon.normalized * (moonGravityStrength * GetMoonBlend(position));
    }

    public float GetRotationAssist(Transform body, Vector2 gravity)
    {
        if (gravity.sqrMagnitude < 0.0001f) return 0f;
        float error = Vector2.SignedAngle(body.up, -gravity.normalized);
        if (Mathf.Abs(error) < deadZoneDeg) return 0f;
        return Mathf.Clamp(error * rotationAssist * GetMoonBlend(body.position), -maxAssistSpeed, maxAssistSpeed);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, moonEnterRadius);
        Gizmos.DrawLine(new Vector3(-9999, zeroGStartY, 0), new Vector3(9999, zeroGStartY, 0));
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, moonFullRadius);
        Gizmos.DrawLine(new Vector3(-9999, zeroGFullY, 0), new Vector3(9999, zeroGFullY, 0));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

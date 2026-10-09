using UnityEngine;

[DisallowMultipleComponent]
public class CharacterVisual : MonoBehaviour
{
    [SerializeField] Transform facingRoot;
    [SerializeField] Animator animator;
    [SerializeField, Min(0.01f)] float referenceWalkSpeed = 2f;
    SpriteRenderer[] renderers;
    Transform hips;
    Vector3 facingBasePosition;
    float hipsBaseY;
    float moonHopHeight = 0.35f;
    float moonHopTempo = 1f;
    bool adjustMoonHop;
    bool hopping;

    void Awake()
    {
        if (facingRoot) facingBasePosition = facingRoot.localPosition;
        if (animator) hips = animator.transform.Find("Hips");
        if (hips) hipsBaseY = hips.localPosition.y;
    }

    public void SetMoonHop(float height, float frequency)
    {
        adjustMoonHop = true;
        moonHopHeight = Mathf.Max(0f, height);
        moonHopTempo = Mathf.Max(0f, frequency) / 6f;
    }

    void LateUpdate()
    {
        if (!adjustMoonHop || !facingRoot || !hips) return;
        // Compensate the clip's fixed 0.35-unit lift after the Animator has evaluated.
        float lift = hopping ? Mathf.Max(0f, hips.localPosition.y - hipsBaseY) : 0f;
        Vector3 offset = hips.parent.TransformVector(Vector3.up * lift * (moonHopHeight / 0.35f - 1f));
        if (facingRoot.parent) offset = facingRoot.parent.InverseTransformVector(offset);
        facingRoot.localPosition = facingBasePosition + offset;
    }

    void OnDisable()
    {
        hopping = false;
        if (adjustMoonHop && facingRoot) facingRoot.localPosition = facingBasePosition;
    }

    public Bounds Bounds
    {
        get
        {
            renderers ??= GetComponentsInChildren<SpriteRenderer>(true);
            Bounds bounds = new(transform.position, Vector3.zero);
            bool hasRenderer = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasRenderer) { bounds = renderer.bounds; hasRenderer = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }
    }

    static readonly int IsWalking = Animator.StringToHash("IsWalking");
    static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    static readonly int IsOnMoon = Animator.StringToHash("IsOnMoon");

    public void SetWalking(bool walking, float worldSpeed, bool onMoon = false)
    {
        hopping = walking && onMoon;
        if (!animator) return;
        animator.SetBool(IsWalking, walking);
        animator.SetBool(IsOnMoon, onMoon);
        if (!walking) return;
        float referenceSpeed = referenceWalkSpeed * Mathf.Abs(transform.lossyScale.x);
        float speed = Mathf.Max(0.1f, Mathf.Abs(worldSpeed) / Mathf.Max(0.0001f, referenceSpeed));
        animator.SetFloat(WalkSpeed, speed * (onMoon && adjustMoonHop ? moonHopTempo : 1f));
    }

    public void FaceLeft(bool left)
    {
        if (!facingRoot) return;
        Vector3 scale = facingRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (left ? -1f : 1f);
        facingRoot.localScale = scale;
    }
}

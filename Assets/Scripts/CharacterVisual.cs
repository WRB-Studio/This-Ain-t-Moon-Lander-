using UnityEngine;

[DisallowMultipleComponent]
public class CharacterVisual : MonoBehaviour
{
    [SerializeField] Transform facingRoot;
    [SerializeField] Animator animator;
    [SerializeField, Min(0.01f)] float referenceWalkSpeed = 2f;
    SpriteRenderer[] renderers;

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
        if (!animator) return;
        animator.SetBool(IsWalking, walking);
        animator.SetBool(IsOnMoon, onMoon);
        if (!walking) return;
        float referenceSpeed = referenceWalkSpeed * Mathf.Abs(transform.lossyScale.x);
        animator.SetFloat(WalkSpeed, Mathf.Max(0.1f, Mathf.Abs(worldSpeed) / Mathf.Max(0.0001f, referenceSpeed)));
    }

    public void FaceLeft(bool left)
    {
        if (!facingRoot) return;
        Vector3 scale = facingRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (left ? -1f : 1f);
        facingRoot.localScale = scale;
    }
}

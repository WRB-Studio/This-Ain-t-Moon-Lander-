using UnityEngine;

[DisallowMultipleComponent]
public class CharacterVisual : MonoBehaviour
{
    [SerializeField] Transform facingRoot;
    [SerializeField] Animator animator;
    [SerializeField, Min(0.01f)] float referenceWalkSpeed = 2f;

    static readonly int IsWalking = Animator.StringToHash("IsWalking");
    static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");

    public void SetWalking(bool walking, float worldSpeed)
    {
        if (!animator) return;
        animator.SetBool(IsWalking, walking);
        float referenceSpeed = referenceWalkSpeed * Mathf.Abs(transform.lossyScale.x);
        animator.SetFloat(WalkSpeed, walking
            ? Mathf.Max(0.1f, Mathf.Abs(worldSpeed) / Mathf.Max(0.0001f, referenceSpeed)) : 1f);
    }

    public void FaceLeft(bool left)
    {
        if (!facingRoot) return;
        Vector3 scale = facingRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (left ? -1f : 1f);
        facingRoot.localScale = scale;
    }
}

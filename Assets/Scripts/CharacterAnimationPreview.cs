using UnityEngine;

public class CharacterAnimationPreview : MonoBehaviour
{
    [SerializeField] CharacterVisual character;
    [SerializeField] bool walking;
    [SerializeField] bool faceLeft;
    [SerializeField] bool moonWalk;
    [SerializeField, Min(0f)] float worldSpeed = 2f;

    void Update()
    {
        if (!character) return;
        character.SetWalking(walking, worldSpeed, moonWalk);
        character.FaceLeft(faceLeft);
    }
}

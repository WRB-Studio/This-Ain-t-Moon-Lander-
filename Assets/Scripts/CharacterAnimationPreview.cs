using UnityEngine;

public class CharacterAnimationPreview : MonoBehaviour
{
    [SerializeField] CharacterVisual character;
    [SerializeField] bool walking;
    [SerializeField] bool faceLeft;
    [SerializeField, Min(0f)] float worldSpeed = 2f;

    void Update()
    {
        if (!character) return;
        character.SetWalking(walking, worldSpeed);
        character.FaceLeft(faceLeft);
    }
}
